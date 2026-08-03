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
    private AddPropertyViewModel? _addPropertySheet;
    private BulkRenameViewModel? _bulkRenameSheet;
    private System.Windows.Input.ICommand? _openAddPropertySheetCommand;
    private System.Windows.Input.ICommand? _closeAddPropertySheetCommand;
    private System.Windows.Input.ICommand? _openBulkRenameSheetCommand;
    private System.Windows.Input.ICommand? _closeBulkRenameSheetCommand;
    private System.Windows.Input.ICommand? _pickWikilinkTargetCommand;

    /// <summary>The conflict dialog request: (filename, key,
    /// keep-mine, reload-from-disk). The window layer presents it;
    /// tests intercept it.</summary>
    internal Action<string, string, Action, Action>? PropertyConflictDialog { get; set; }

    /// <summary>Window-supplied vault-rooted .md picker for wikilink
    /// rows; returns the vault-relative target (minus .md) or null
    /// on cancel.</summary>
    internal Func<string?>? WikilinkPicker { get; set; }

    /// <summary>Non-null while the add-property sheet is open.</summary>
    public AddPropertyViewModel? AddPropertySheet
    {
        get => _addPropertySheet;
        private set => SetField(ref _addPropertySheet, value);
    }

    /// <summary>Non-null while the bulk-rename sheet is open.</summary>
    public BulkRenameViewModel? BulkRenameSheet
    {
        get => _bulkRenameSheet;
        private set => SetField(ref _bulkRenameSheet, value);
    }

    public System.Windows.Input.ICommand OpenAddPropertySheetCommand =>
        _openAddPropertySheetCommand ??= new RelayCommand(
            _ => OpenAddPropertySheet(), _ => true);

    public System.Windows.Input.ICommand CloseAddPropertySheetCommand =>
        _closeAddPropertySheetCommand ??= new RelayCommand(
            _ => AddPropertySheet = null, _ => true);

    public System.Windows.Input.ICommand OpenBulkRenameSheetCommand =>
        _openBulkRenameSheetCommand ??= new RelayCommand(
            _ => OpenBulkRenameSheet(), _ => true);

    public System.Windows.Input.ICommand CloseBulkRenameSheetCommand =>
        _closeBulkRenameSheetCommand ??= new RelayCommand(
            _ => CloseBulkRenameSheet(), _ => true);

    /// <summary>The wikilink "Pick…" button: dialog result lands in
    /// the row DRAFT (contract 2 — no write until commit).</summary>
    public System.Windows.Input.ICommand PickWikilinkTargetCommand =>
        _pickWikilinkTargetCommand ??= new RelayCommand(
            parameter =>
            {
                if (parameter is PropertyRowViewModel row
                    && WikilinkPicker?.Invoke() is { } target)
                {
                    row.EditorText = target;
                }
            },
            _ => true);

    internal void OpenAddPropertySheet(bool synchronousForTests = false)
    {
        var properties = EnsureActiveTabProperties(synchronousForTests);
        var sheet = new AddPropertyViewModel(
            () => properties is { LoadError: null } header
                && ActiveGroup.ActiveTab is { IsMarkdown: true }
                ? header.CurrentKeys
                : null,
            (key, value) => CommitAddProperty(key, value),
            _announce);
        AddPropertySheet = sheet;
        sheet.SheetShown();
    }

    internal void OpenBulkRenameSheet(bool synchronousForTests = false)
    {
        var sheet = CreateBulkRename(synchronousForTests);
        BulkRenameSheet = sheet;
        sheet.SheetShown();
    }

    internal void CloseBulkRenameSheet()
    {
        BulkRenameSheet?.CancelInFlight();
        BulkRenameSheet?.Shutdown();
        BulkRenameSheet = null;
    }

    /// <summary>The ADD write: same seam as row commits, with the
    /// header's read-time hash as the CAS token (contract 1 — adds
    /// have no row to pin one). Success closes the sheet; failure
    /// keeps it open with the draft intact (§2.4).</summary>
    internal bool CommitAddProperty(string key, PropertyValue value)
    {
        if (ActiveGroup.ActiveTab is not WorkspaceTabViewModel { IsMarkdown: true } tab
            || tab.Properties is not { LoadError: null } properties
            || properties.ContentHash.Length == 0)
        {
            return false;
        }
        if (tab.IsDirty)
        {
            _announce(new A11yEvent.HostComposed(
                // W0.5-3 residue: dirty-tab refusal (recorded
                // divergence), same copy as the row seam.
                "Save the note before editing properties. The editor has unsaved "
                    + $"changes in {System.IO.Path.GetFileName(tab.Path)}.",
                A11yPriority.High));
            return false;
        }
        string hash = properties.ContentHash;
        if (tab.IsExternallyStale
            || !string.Equals(tab.SavedContentHash, hash, StringComparison.Ordinal))
        {
            _announce(new A11yEvent.PropertyEditConflict(
                System.IO.Path.GetFileName(tab.Path)));
            RefreshPropertiesFor(tab.Path);
            return false;
        }
        string path = tab.Path;
        tab.WriteProperty(
            hash,
            expectedHash => _session.SetProperty(path, key, value, expectedHash),
            (report, failure, postFailureDiskHash) =>
            {
                if (report is not null)
                {
                    AddPropertySheet = null;
                    ReconcileTabsAfterPropertyWrite(path, report.NewContentHash);
                    RefreshPropertiesFor(path);
                    Panels.NoteSaved(path);
                    _announce(new A11yEvent.PropertyChanged(key, false));
                    return;
                }
                AddPropertySheet?.MarkAddFailed();
                if (failure is VaultException.WriteConflict)
                {
                    _announce(new A11yEvent.PropertyEditConflict(
                        System.IO.Path.GetFileName(path)));
                }
                else
                {
                    _announce(new A11yEvent.PropertyEditFailed(
                        failure?.Message ?? "unknown failure"));
                }
                RefreshPropertiesFor(path);
            });
        return true;
    }

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
            persistExpansion: PersistTabState,
            synchronousForTests: synchronousForTests);
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
        if (!row.ValidateForCommit())
        {
            return true;
        }
        if (row.WriteInFlight)
        {
            AnnounceWriteInFlightRefusal();
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

    private void AnnounceWriteInFlightRefusal() =>
        _announce(new A11yEvent.HostComposed(
            // W0.5-3 residue: the in-flight disabled reason is spoken
            // (double-activation on immediate-commit controls cannot
            // issue two writes against one hash).
            "Wait for the current save to finish.", A11yPriority.High));

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
            AnnounceWriteInFlightRefusal();
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
                ReconcileTabsAfterPropertyWrite(path, report.NewContentHash);
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
                ReconcileTabsAfterPropertyWrite(path, postFailureDiskHash);
                RefreshPropertiesFor(path);
                Panels.NoteSaved(path);
            }
        };

    /// <summary>Build the bulk-rename sheet VM, wired to this
    /// workspace: the old key prefills from the active note's first
    /// property, the dirty-draft gate scans every open tab's header,
    /// and applied reports reconcile open tabs (contract 8).</summary>
    internal BulkRenameViewModel CreateBulkRename(bool synchronousForTests = false)
    {
        var sheet = new BulkRenameViewModel(
            _session,
            _announce,
            () => Groups.SelectMany(group => group.Tabs)
                .Any(tab => tab.Properties?.AnyRowDirty == true),
            ReconcileTabsAfterBulkRename,
            synchronousForTests);
        if (ActiveGroup.ActiveTab is WorkspaceTabViewModel { IsMarkdown: true } tab
            && tab.Properties?.Rows.FirstOrDefault() is { } firstRow)
        {
            sheet.OldKey = firstRow.Key;
        }
        return sheet;
    }

    /// <summary>Contract 8: after an APPLY, every open tab whose path
    /// appears in the report ends either re-baselined to that entry's
    /// new content hash (clean tabs) or flagged externally stale
    /// (dirty tabs) — no open tab may retain a stale SavedContentHash
    /// that would let a later save clobber the rename. Failures to
    /// re-read announce loudly instead of passing silently.</summary>
    internal void ReconcileTabsAfterBulkRename(RenameReport report)
    {
        bool anyReloadFailed = false;
        foreach (var affected in report.Affected)
        {
            if (!affected.Applied || affected.NewContentHash is null)
            {
                continue;
            }
            try
            {
                ReconcileTabsAfterPropertyWrite(affected.Path, affected.NewContentHash);
                RefreshPropertiesFor(affected.Path);
                Panels.NoteSaved(affected.Path);
            }
            catch (Exception exception) when (
                exception is VaultException or InvalidOperationException)
            {
                anyReloadFailed = true;
            }
        }
        if (anyReloadFailed)
        {
            _announce(new A11yEvent.RenameReloadFailed(
                "Some open notes could not be reloaded."));
        }
    }

    /// <summary>Contract 8 fan-out: every open same-path tab either
    /// re-baselines its buffer to the just-written disk state (clean
    /// tabs) or takes the stale-flag honesty (dirty tabs).</summary>
    internal void ReconcileTabsAfterPropertyWrite(string path, string newContentHash)
    {
        foreach (WorkspaceTabViewModel tab in Groups.SelectMany(group => group.Tabs))
        {
            if (tab.IsMarkdown
                && string.Equals(tab.Path, path, StringComparison.Ordinal))
            {
                tab.RebaselineAfterPropertyWrite(newContentHash, _announce);
            }
        }
    }

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
