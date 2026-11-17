// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows.Threading;
using ICSharpCode.AvalonEdit.Document;
using SlateWindows.Panels;
using uniffi.slate_uniffi;

namespace SlateWindows;

/// <summary>
/// W4-4 (#736): the tab-owned half of the property surfaces — the
/// per-tab header VM (drafts and expansion survive tab switches;
/// duplicate same-path tabs each hold their own) and the property
/// WRITE WORKER, a mirror of PerformTaskToggle: mutation lease with
/// a fail-closed finally, terminal completion regardless of tab
/// disposal, post-failure disk-hash read-back. The worker also owns
/// the TAB-SCOPED property-write lease (contract 3, adversarial
/// round 1): set, delete, add, and Keep-Mine retries all funnel
/// through WriteProperty, so at most one property write per tab is
/// ever in flight and the second attempt is refused pre-core.
/// </summary>
internal sealed partial class WorkspaceTabViewModel
{
    private NotePropertiesViewModel? _properties;

    /// <summary>The per-tab properties header. Created lazily by the
    /// workspace when the tab first shows a markdown note; null for
    /// non-markdown tabs. PUBLIC because WPF bindings only reflect
    /// over public members (the Reading precedent); notifies so the
    /// header region can appear after the attach.</summary>
    public NotePropertiesViewModel? Properties
    {
        get => _properties;
        private set => SetField(ref _properties, value);
    }

    /// <summary>Test seam: thrown after the core write lands, before
    /// the completion publishes (the W4-3 fault pattern).</summary>
    internal Action? PropertyWriteFaultForTests { get; set; }

    /// <summary>WB-2 test seam: runs on the property write's worker before
    /// the core write — a fact parks the write there, or counts the writes
    /// that started.</summary>
    internal Action? PropertyWriteHookForTests { get; set; }

    internal NotePropertiesViewModel EnsureProperties(
        Action<PropertyRowViewModel> commit,
        Action<PropertyRowViewModel> revertAnnounce,
        Action<PropertyRowViewModel> requestDelete,
        Action? persistExpansion = null,
        bool synchronousForTests = false)
    {
        if (_properties is null)
        {
            var created = new NotePropertiesViewModel(
                _session, commit, revertAnnounce, requestDelete, _announce,
                synchronousForTests);
            if (PropsCollapsed == true)
            {
                created.IsExpanded = false;
            }
            if (persistExpansion is not null)
            {
                created.PropertyChanged += (_, args) =>
                {
                    if (args.PropertyName == nameof(NotePropertiesViewModel.IsExpanded))
                    {
                        persistExpansion();
                    }
                };
            }
            Properties = created;
        }
        return _properties!;
    }

    internal void ShutdownProperties() => Properties?.Shutdown();

    /// <summary>#1794: the header belongs to a note. It leaves with the note when
    /// something else opens into this tab, and the tab keeps whether it was collapsed
    /// for the next note here.</summary>
    private void RetireProperties()
    {
        if (_properties is { } properties)
        {
            PropsCollapsed = properties.IsExpanded ? null : true;
            properties.Shutdown();
            Properties = null;
        }
    }

    /// <summary>A property write (set/delete/rename) rewrote this
    /// tab's file WITHOUT publishing through its buffer. Contract 8:
    /// a CLEAN tab re-baselines — reload the buffer from disk and
    /// adopt the new hash, exactly like a save settles — while a
    /// dirty tab takes the stale-flag honesty with PROPERTY copy
    /// (adversarial round 1: never the task-toggle wording) because
    /// overwriting unsaved edits is worse than a reopen prompt.
    /// Plan 47 CB-9b (Design 0): a clean tab, and each same-path peer,
    /// keeps no undo step back across the written bytes, and its caret
    /// keeps its line (<see cref="TakeAnotherWritersBytes"/>).
    /// Returns false only when the reload attempt itself failed —
    /// the caller aggregates that into RenameReloadFailed.</summary>
    internal bool RebaselineAfterPropertyWrite(
        string newContentHash, Action<A11yEvent> announce)
    {
        ArgumentNullException.ThrowIfNull(announce);
        if (!IsMarkdown || _disposed)
        {
            return true;
        }
        if (string.Equals(_contentHash, newContentHash, StringComparison.Ordinal))
        {
            IsExternallyStale = false;
            return true;
        }
        if (IsDirty)
        {
            MarkPropertyStale(announce);
            return true;
        }
        IdentifiedText read;
        try
        {
            read = _session.ReadTextIdentified(Path);
        }
        catch (VaultException)
        {
            // Can't re-read: honest containment beats a silent stale
            // baseline — and the failure is REPORTED, not swallowed.
            MarkPropertyStale(announce);
            return false;
        }
        AdoptAnotherWritersRead(read);
        return true;
    }

    /// <summary>
    /// Plan 47 CB-9b (Design 2's catch-up): a clean tab with no save pending
    /// takes <paramref name="read"/>, made from disk by an admitted operation
    /// inside its ticket, as its text, identity and saved baseline, with no
    /// undo step back across it and its caret kept on its line; its same-path
    /// peers take it too. It never speaks and never marks a tab stale. It
    /// returns false, changing nothing, for a tab that is not a live Markdown
    /// tab, is dirty, or has a save pending, whose next save publishes to it.
    /// A tab that already holds the read's bytes keeps its buffer and only
    /// loses a stale mark.
    /// </summary>
    internal bool RebaselineToRead(IdentifiedText read)
    {
        ArgumentNullException.ThrowIfNull(read);
        if (!IsMarkdown || _disposed || IsDirty || HasPendingSaves)
        {
            return false;
        }
        if (string.Equals(
            _contentHash, SlateUniffiMethods.EditorTextContentHash(read.Text), StringComparison.Ordinal))
        {
            IsExternallyStale = false;
            return true;
        }
        AdoptAnotherWritersRead(read);
        return true;
    }

    /// <summary>
    /// Plan 47 CB-9b (Design 5; CD5(b)): a rename or a move rewrote the links
    /// of this tab's note, and <paramref name="read"/>, made now, should hold
    /// the bytes after the rewrite (<paramref name="hashAfter"/>, core's hash
    /// in the report). A clean tab with no save pending takes the read as
    /// <see cref="RebaselineToRead"/> does: its text, identity and saved
    /// baseline, with no undo step back across it, its caret kept on its line
    /// and its stale mark cleared, and its same-path peers take it too. It
    /// never speaks and never marks a tab stale. It returns false, changing
    /// nothing, for a read that does not hold the bytes asked for, a dirty
    /// tab, or a tab with a save pending.
    /// </summary>
    internal bool RebaselineAfterLinkRewrite(IdentifiedText read, string hashAfter)
    {
        ArgumentNullException.ThrowIfNull(read);
        return string.Equals(read.ContentHash, hashAfter, StringComparison.Ordinal) && RebaselineToRead(read);
    }

    /// <summary>A clean tab's adoption of another writer's bytes, read from
    /// disk: its text and baseline (<see cref="TakeAnotherWritersBytes"/>),
    /// the read's identity, clean and not stale, its interactions told, and
    /// its same-path peers told the same way.</summary>
    private void AdoptAnotherWritersRead(IdentifiedText read)
    {
        string fresh = read.Text;
        TakeAnotherWritersBytes(fresh);
        _text = fresh;
        _contentHash = SlateUniffiMethods.EditorTextContentHash(fresh);
        FileIdentity = read.FileIdentity;
        EnforcesFileIdentity = false;
        IsDirty = false;
        IsExternallyStale = false;
        _editorInteractions?.InvalidateExternalState();
        TellPeersOfAnotherWritersBytes();
    }

    // Plan 47 CB-9b (Design 0): true while this tab tells its same-path
    // peers that another writer's bytes became its text and baseline, so each
    // peer takes them the same way (MirrorDocumentStateFrom) instead of
    // adopting the baseline over an undo step that reaches the old bytes.
    private bool _tookAnotherWritersBytes;

    /// <summary>Plan 47 CB-9b (Design 0; the plan lead's word of 2026-11-17):
    /// another writer's bytes, read from disk, become this tab's text and its
    /// saved baseline with no undo step back across them
    /// (<see cref="AvalonDocumentBufferSession.ReplaceAsSavedBaseline"/>), and
    /// the caret keeps its line and column where the new text allows, by the
    /// rescan's rule (<see cref="ReloadKeepingCaretLine"/>): a line past the
    /// new end clamps to the last line, and the column to that line. The
    /// caret lands through the bound offset, never a caret-navigation request,
    /// which would take keyboard focus into the editor.</summary>
    private void TakeAnotherWritersBytes(string text)
    {
        if (_editorSession is not { } session)
        {
            return;
        }
        TextDocument document = session.Document;
        TextLocation caret = document.GetLocation(Math.Clamp(EditorCaretOffset, 0, document.TextLength));
        session.ReplaceAsSavedBaseline(text);
        int line = Math.Clamp(caret.Line, 1, document.LineCount);
        DocumentLine target = document.GetLineByNumber(line);
        int column = Math.Clamp(caret.Column, 1, target.Length + 1);
        EditorCaretOffset = target.Offset + column - 1;
        BumpContentGeneration();
        OnPropertyChanged(nameof(Text));
    }

    /// <summary>The state change every same-path peer mirrors, said as
    /// another writer's bytes: each peer takes them through
    /// <see cref="TakeAnotherWritersBytes"/>, with its own caret.</summary>
    private void TellPeersOfAnotherWritersBytes()
    {
        _tookAnotherWritersBytes = true;
        try
        {
            _documentChanged?.Invoke(this, null);
        }
        finally
        {
            _tookAnotherWritersBytes = false;
        }
    }

    private void MarkPropertyStale(Action<A11yEvent> announce)
    {
        IsExternallyStale = true;
        _editorInteractions?.InvalidateExternalState();
        Status = "Properties changed on disk, but the editor no longer matches it. "
            + "Save or reopen the note to reconcile.";
        // W0.5-3 residue: the containment state is spoken with
        // PROPERTY copy (the task-toggle wording would be factually
        // wrong here).
        announce(new A11yEvent.HostComposed(Status, A11yPriority.High));
    }

    /// <summary>Run one property write off the UI thread. The
    /// completion ALWAYS fires (on the dispatcher) with either the
    /// report or the failure plus the post-failure disk hash — the
    /// caller owns announcements, reconciliation, and the
    /// NOTE-scoped write lease (the workspace acquires it before
    /// calling and releases it in the wrapped completion — round 2:
    /// per-path exclusion survives this tab closing mid-flight). Returns
    /// the worker (WB-2): an admitted write hands it to the save
    /// coordinator, so teardown joins it before the session goes.</summary>
    internal Task WriteProperty(
        string? expectedHash,
        Func<string?, SaveReport> write,
        Action<SaveReport?, VaultException?, string?> completion)
    {
        string path = Path;
        Panels.TaskIndexRepairCoordinator? repairs = TaskRepairs;
        Action? hook = PropertyWriteHookForTests;
        return Task.Run(() =>
        {
            SaveReport? report = null;
            VaultException? failure = null;
            string? postFailureDiskHash = null;
            // The mutation LEASE brackets the session write
            // (W4-3 round 19): property writes rewrite the file and
            // reindex it — tasks included — so the shared quarantine
            // must see the stale-index interval from the file write
            // onward. A non-conflict failure converts the lease into
            // the pending repair atomically; a leaked lease bars
            // every task query forever, so the finally fails closed.
            repairs?.BeginMutation(path);
            bool leaseSettled = false;
            try
            {
                try
                {
                    hook?.Invoke();
                    report = write(expectedHash);
                    PropertyWriteFaultForTests?.Invoke();
                    repairs?.EndMutation(path, indexConsistent: true);
                    leaseSettled = true;
                }
                catch (Exception inner) when (
                    inner is not OutOfMemoryException
                        and not StackOverflowException
                        and not AccessViolationException)
                {
                    // Catch-all (adversarial round 1, completion
                    // totality): ANY worker failure must still reach
                    // the terminal completion — a stranded completion
                    // leaks the write lease and the row's in-flight
                    // flag forever. The fault seam can throw AFTER
                    // the core write landed; the completion keys on
                    // report-vs-failure, so a landed-then-faulted
                    // write presents as a FAILURE (the read-back
                    // reconcile recovers the landed bytes).
                    report = null;
                    repairs?.EndMutation(
                        path, indexConsistent: inner is VaultException.WriteConflict);
                    leaseSettled = true;
                    failure = inner as VaultException
                        ?? new VaultException.InvalidArgument(inner.Message);
                    postFailureDiskHash = ReadBackDiskHashAfterFailure(path, failure);
                }
            }
            finally
            {
                if (!leaseSettled)
                {
                    repairs?.EndMutation(path, indexConsistent: false);
                }
            }

            if (_dispatcher.HasShutdownStarted || _dispatcher.HasShutdownFinished)
            {
                return;
            }
            _dispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new Action(() => completion(report, failure, postFailureDiskHash)));
        });
    }
}
