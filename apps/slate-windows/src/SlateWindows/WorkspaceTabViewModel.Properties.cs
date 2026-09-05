// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows.Threading;
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

    /// <summary>A property write (set/delete/rename) rewrote this
    /// tab's file WITHOUT publishing through its buffer. Contract 8:
    /// a CLEAN tab re-baselines — reload the buffer from disk and
    /// adopt the new hash, exactly like a save settles — while a
    /// dirty tab takes the stale-flag honesty with PROPERTY copy
    /// (adversarial round 1: never the task-toggle wording) because
    /// overwriting unsaved edits is worse than a reopen prompt.
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
        string fresh;
        try
        {
            fresh = _session.ReadText(Path);
        }
        catch (VaultException)
        {
            // Can't re-read: honest containment beats a silent stale
            // baseline — and the failure is REPORTED, not swallowed.
            MarkPropertyStale(announce);
            return false;
        }
        if (_editorSession is not null)
        {
            _editorSession.ReplaceAll(fresh);
            _editorSession.MarkSaved(fresh);
        }
        _text = fresh;
        _contentHash = SlateUniffiMethods.EditorTextContentHash(fresh);
        IsDirty = false;
        IsExternallyStale = false;
        _editorInteractions?.InvalidateExternalState();
        _documentChanged?.Invoke(this, null);
        return true;
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
    /// per-path exclusion survives this tab closing mid-flight).</summary>
    internal void WriteProperty(
        string? expectedHash,
        Func<string?, SaveReport> write,
        Action<SaveReport?, VaultException?, string?> completion)
    {
        string path = Path;
        Panels.TaskIndexRepairCoordinator? repairs = TaskRepairs;
        _ = Task.Run(() =>
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
