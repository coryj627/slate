// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Panels;
using uniffi.slate_uniffi;

namespace SlateWindows;

/// <summary>
/// W4-4 (#736): the property WRITE SEAM — the TogglePanelTask shape
/// applied to set_property/delete_property. The header VM and its
/// rows never write; every commit funnels here for the refusal
/// gates (feature contract 3), the CAS snapshot hash (contract 1),
/// and the terminal completion + single announcement per outcome
/// (contracts 4 and 9). The write worker itself lives on the tab
/// (WorkspaceTabViewModel.Properties.cs), like the toggle worker.
/// </summary>
internal sealed partial class WorkspaceViewModel
{
    /// <summary>The conflict dialog request: (filename, key,
    /// keep-mine, reload-from-disk). The window layer presents it;
    /// tests intercept it.</summary>
    internal Action<string, string, Action, Action>? PropertyConflictDialog { get; set; }

    /// <summary>Attach (or return) the active tab's header VM with
    /// the workspace seam delegates wired. Called at tab activation
    /// by the window layer and by tests.</summary>
    internal NotePropertiesViewModel? EnsureActiveTabProperties(
        bool synchronousForTests = false)
    {
        if (ActiveGroup.ActiveTab is not WorkspaceTabViewModel { IsMarkdown: true } tab)
        {
            return null;
        }
        var properties = tab.EnsureProperties(
            row => SetPanelProperty(row),
            row => _announce(new A11yEvent.HostComposed(
                // W0.5-3 residue: mac composes the revert phrase in
                // the widget; carried as the same residue family.
                $"Reverted changes to {row.Key}.", A11yPriority.Medium)),
            row => RequestPanelPropertyDelete(row),
            synchronousForTests);
        if (properties.Path != tab.Path)
        {
            properties.Load(tab.Path);
        }
        return properties;
    }

    /// <summary>Confirmation-gated delete entry: the window layer
    /// owns the dialog; tests drive the confirm delegate directly.
    /// Cancel is the DEFAULT action and never deletes (contract 5).</summary>
    internal Func<string, string, bool>? PropertyDeleteConfirmation { get; set; }

    internal bool RequestPanelPropertyDelete(PropertyRowViewModel row)
    {
        if (row.IsDirty)
        {
            _announce(new A11yEvent.HostComposed(
                PropertyPhrase.DeleteWhileDirtyReason, A11yPriority.High));
            return true;
        }
        bool confirmed = PropertyDeleteConfirmation?.Invoke(
            PropertyPhrase.DeleteConfirmTitle(row.Key),
            PropertyPhrase.DeleteConfirmMessage(row.Key)) ?? false;
        if (!confirmed)
        {
            return true;
        }
        return DeletePanelProperty(row);
    }

    /// <summary>Commit a row's draft via set_property. Refusals are
    /// total and pre-core (contract 3): validation, write-in-flight,
    /// dirty tab, externally-stale tab, and row-hash mismatch touch
    /// nothing and announce exactly once.</summary>
    internal bool SetPanelProperty(PropertyRowViewModel row)
    {
        if (ActiveGroup.ActiveTab is not WorkspaceTabViewModel { IsMarkdown: true } tab)
        {
            return false;
        }
        if (!row.ValidateForCommit() || row.WriteInFlight)
        {
            return true;
        }
        if (RefusePropertyWriteGates(tab, row))
        {
            return true;
        }
        row.WriteInFlight = true;
        PropertyValue value = PropertyValueCodec.Encode(row.Draft);
        string path = tab.Path;
        tab.WriteProperty(
            row.ContentHash,
            expectedHash => _session.SetProperty(path, row.Key, value, expectedHash),
            PropertyWriteCompletion(path, row, deleted: false));
        return true;
    }

    /// <summary>Delete a row's key via delete_property (called only
    /// after the confirmation flow chose Delete).</summary>
    internal bool DeletePanelProperty(PropertyRowViewModel row)
    {
        if (ActiveGroup.ActiveTab is not WorkspaceTabViewModel { IsMarkdown: true } tab)
        {
            return false;
        }
        if (row.WriteInFlight)
        {
            return true;
        }
        if (RefusePropertyWriteGates(tab, row))
        {
            return true;
        }
        row.WriteInFlight = true;
        string path = tab.Path;
        tab.WriteProperty(
            row.ContentHash,
            expectedHash => _session.DeleteProperty(path, row.Key, expectedHash),
            PropertyWriteCompletion(path, row, deleted: true));
        return true;
    }

    /// <summary>The shared refusal gates. True = refused (announced
    /// once, nothing written, draft intact).</summary>
    private bool RefusePropertyWriteGates(WorkspaceTabViewModel tab, PropertyRowViewModel row)
    {
        if (tab.IsDirty)
        {
            // W0.5-3 residue: dirty-tab refusal is a recorded
            // divergence — mac's body-only buffer never conflicts
            // with frontmatter writes; Windows whole-file tabs must
            // refuse (w_d notes).
            _announce(new A11yEvent.HostComposed(
                "Save the note before editing properties. The editor has unsaved "
                    + $"changes in {System.IO.Path.GetFileName(tab.Path)}.",
                A11yPriority.High));
            return true;
        }
        if (tab.IsExternallyStale
            || !string.Equals(
                tab.SavedContentHash, row.ContentHash, StringComparison.Ordinal))
        {
            AnnouncePropertyConflict(tab, row);
            return true;
        }
        return false;
    }

    private void AnnouncePropertyConflict(WorkspaceTabViewModel tab, PropertyRowViewModel row)
    {
        _announce(new A11yEvent.PropertyEditConflict(
            System.IO.Path.GetFileName(tab.Path)));
        string path = tab.Path;
        PropertyConflictDialog?.Invoke(
            System.IO.Path.GetFileName(path),
            row.Key,
            () => RetryPropertyEditWithFreshHash(path, row),
            () => ReloadPropertiesFromDisk(path));
    }

    /// <summary>Keep Mine: re-issue the edit against the CURRENT
    /// disk hash with the latest preserved draft — the only place a
    /// non-snapshot hash is ever used, at the user's explicit
    /// choice.</summary>
    private void RetryPropertyEditWithFreshHash(string path, PropertyRowViewModel row)
    {
        if (FindTabForPath(path) is not { } tab
            || !row.ValidateForCommit()
            || row.WriteInFlight)
        {
            return;
        }
        row.WriteInFlight = true;
        PropertyValue value = PropertyValueCodec.Encode(row.Draft);
        tab.WriteProperty(
            null,
            _ =>
            {
                string freshHash = _session.ReadNoteParts(path).ContentHash;
                return _session.SetProperty(path, row.Key, value, freshHash);
            },
            PropertyWriteCompletion(path, row, deleted: false));
    }

    private void ReloadPropertiesFromDisk(string path)
    {
        RefreshPropertiesFor(path);
        _announce(new A11yEvent.PropertiesReloaded());
    }

    /// <summary>Terminal completion (contract 4): success
    /// re-baselines clean same-path tabs to the report hash BEFORE
    /// any later save can be issued, re-reads headers from
    /// authoritative bytes, refreshes the note panel (frontmatter
    /// line-count changes shift task offsets), and announces once.
    /// Failure keeps the draft, announces once, and reconciles when
    /// the disk hash moved (the W4-3 read-back rule).</summary>
    internal Action<SaveReport?, VaultException?, string?> PropertyWriteCompletion(
        string path, PropertyRowViewModel row, bool deleted) =>
        (report, failure, postFailureDiskHash) =>
        {
            row.WriteInFlight = false;
            if (report is not null)
            {
                row.MarkCommitted();
                ReconcileTabsAfterDirectTaskWrite(path, report.NewContentHash);
                RefreshPropertiesFor(path);
                Panels.NoteSaved(path);
                _announce(new A11yEvent.PropertyChanged(row.Key, deleted));
                return;
            }
            if (failure is VaultException.WriteConflict)
            {
                if (FindTabForPath(path) is { } conflictTab)
                {
                    conflictTab.RefreshExternalStaleness();
                    AnnouncePropertyConflict(conflictTab, row);
                }
                else
                {
                    _announce(new A11yEvent.PropertyEditConflict(
                        System.IO.Path.GetFileName(path)));
                }
                RefreshPropertiesFor(path);
                return;
            }
            _announce(new A11yEvent.PropertyEditFailed(
                failure?.Message ?? "unknown failure"));
            if (postFailureDiskHash is not null
                && FindTabForPath(path) is { } tab
                && !string.Equals(
                    tab.SavedContentHash, postFailureDiskHash, StringComparison.Ordinal))
            {
                ReconcileTabsAfterDirectTaskWrite(path, postFailureDiskHash);
                RefreshPropertiesFor(path);
                Panels.NoteSaved(path);
            }
        };

    /// <summary>Refresh the header VM of every open tab on this path
    /// (duplicate same-path tabs each hold their own instance).</summary>
    internal void RefreshPropertiesFor(string path)
    {
        foreach (WorkspaceTabViewModel tab in Groups.SelectMany(group => group.Tabs))
        {
            if (string.Equals(tab.Path, path, StringComparison.Ordinal))
            {
                tab.Properties?.RefreshProperties();
            }
        }
    }

    private WorkspaceTabViewModel? FindTabForPath(string path)
    {
        foreach (WorkspaceTabViewModel tab in Groups.SelectMany(group => group.Tabs))
        {
            if (string.Equals(tab.Path, path, StringComparison.Ordinal))
            {
                return tab;
            }
        }
        return null;
    }
}
