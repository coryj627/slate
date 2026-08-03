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
/// disposal, post-failure disk-hash read-back.
/// </summary>
internal sealed partial class WorkspaceTabViewModel
{
    private NotePropertiesViewModel? _properties;

    /// <summary>The per-tab properties header. Created lazily by the
    /// workspace when the tab first shows a markdown note; null for
    /// non-markdown tabs. Notifies so the header region can appear
    /// after the attach.</summary>
    internal NotePropertiesViewModel? Properties
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
                _session, commit, revertAnnounce, requestDelete, synchronousForTests);
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
    /// dirty tab takes the W4-3 stale-flag honesty (the divergence
    /// copy, verbatim) because overwriting unsaved edits is worse
    /// than a reopen prompt.</summary>
    internal void RebaselineAfterPropertyWrite(
        string newContentHash, Action<A11yEvent> announce)
    {
        if (!IsMarkdown || _disposed)
        {
            return;
        }
        if (string.Equals(_contentHash, newContentHash, StringComparison.Ordinal))
        {
            IsExternallyStale = false;
            return;
        }
        if (IsDirty)
        {
            ReconcileAfterExternalTaskWrite(newContentHash, announce);
            return;
        }
        string fresh;
        try
        {
            fresh = _session.ReadText(Path);
        }
        catch (VaultException)
        {
            // Can't re-read: honest containment beats a silent stale
            // baseline.
            ReconcileAfterExternalTaskWrite(newContentHash, announce);
            return;
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
    }

    /// <summary>Run one property write off the UI thread. The
    /// completion ALWAYS fires (on the dispatcher) with either the
    /// report or the failure plus the post-failure disk hash — the
    /// caller owns announcements and reconciliation.</summary>
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
                    inner is VaultException or InvalidOperationException)
                {
                    // The fault seam can throw AFTER the core write
                    // landed; the completion keys on report-vs-failure,
                    // so a landed-then-faulted write must present as a
                    // FAILURE (the read-back reconcile recovers the
                    // landed bytes) — never as a clean success.
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
