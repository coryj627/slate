// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Panels;
using uniffi.slate_uniffi;

namespace SlateWindows;

/// <summary>
/// W4-4 (#736): the property WRITE SEAM — the TogglePanelTask shape
/// applied to set_property/delete_property. The header VM and its
/// rows never write; every commit funnels here for the refusal
/// gates (feature contract 3), the CAS snapshot identity
/// (contract 1), and the terminal completion + single announcement
/// per outcome (contracts 4 and 9). The write worker itself lives
/// on the tab (WorkspaceTabViewModel.Properties.cs), like the
/// toggle worker.
///
/// Adversarial-round-1 posture: every operation resolves its target
/// tab by the OWNER PATH captured at the read that produced it —
/// rows carry OwnerPath, the add sheet carries an immutable
/// PropertyAddIntent — never by whichever tab happens to be active
/// at commit time. Conflict retries are OPERATION-SPECIFIC: a
/// conflicted delete retries as a delete, never as a set.
/// </summary>
internal sealed partial class WorkspaceViewModel
{
    private AddPropertyViewModel? _addPropertySheet;
    private BulkRenameViewModel? _bulkRenameSheet;

    /// <summary>The NOTE-scoped property-write lease (adversarial
    /// round 2, contract 3): exclusion is per PATH, not per visual
    /// tab — duplicate same-path tabs share one lease, and closing
    /// the tab that carried an in-flight write cannot free the note
    /// for a second write. Acquired on the dispatcher thread before
    /// scheduling; released by the wrapped terminal completion just
    /// before it runs, so a completion may legally chain a retry.</summary>
    private readonly HashSet<string> _propertyWritePaths = new(StringComparer.Ordinal);

    internal bool PropertyWriteInFlightFor(string path) =>
        _propertyWritePaths.Contains(path);

    private bool TryAcquirePropertyWriteLease(string path) =>
        _propertyWritePaths.Add(path);

    private Action<SaveReport?, VaultException?, string?> WithLeaseRelease(
        string path, Action<SaveReport?, VaultException?, string?> completion) =>
        (report, failure, postFailureDiskHash) =>
        {
            _ = _propertyWritePaths.Remove(path);
            completion(report, failure, postFailureDiskHash);
        };
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

    /// <summary>Open the add sheet with an IMMUTABLE intent captured
    /// NOW (contract 1): owner path, the header's publish hash, and
    /// the key set all pin the exact read the sheet validates and
    /// writes against — commit never re-resolves the active tab or
    /// re-reads a hash. No authoritative header data yet (pending
    /// load or load error) → the intent is null and every Add
    /// refuses pre-core (contract 6 totality).</summary>
    internal void OpenAddPropertySheet(bool synchronousForTests = false)
    {
        var properties = EnsureActiveTabProperties(synchronousForTests);
        PropertyAddIntent? intent =
            ActiveGroup.ActiveTab is WorkspaceTabViewModel { IsMarkdown: true } tab
            && properties is { LoadError: null } header
            && header.ContentHash.Length > 0
                ? new PropertyAddIntent(tab.Path, header.ContentHash, header.CurrentKeys)
                : null;
        var sheet = new AddPropertyViewModel(intent, CommitAddProperty, _announce);
        AddPropertySheet = sheet;
        sheet.SheetShown();
    }

    internal void OpenBulkRenameSheet(bool synchronousForTests = false)
    {
        var sheet = CreateBulkRename(synchronousForTests);
        sheet.CloseSettled += () =>
        {
            sheet.Shutdown();
            if (ReferenceEquals(BulkRenameSheet, sheet))
            {
                BulkRenameSheet = null;
            }
        };
        BulkRenameSheet = sheet;
        sheet.SheetShown();
    }

    /// <summary>Close request: an idle sheet closes immediately; a
    /// sheet with an in-flight run cancels and stays visible until
    /// the run's terminal publish settles (adversarial round 1,
    /// contract 7 — a mid-apply close must not discard the partial
    /// report or its tab reconciliation).</summary>
    internal void CloseBulkRenameSheet() => BulkRenameSheet?.RequestClose();

    /// <summary>The ADD write (dispatched = true). Refusals announce
    /// their reason and return false — the sheet reports an
    /// undispatched add honestly (contract 6). The completion binds
    /// the CAPTURED sheet instance, so a close-and-reopen during the
    /// write can never close or mark a replacement sheet.</summary>
    internal bool CommitAddProperty(PropertyAddIntent intent, string key, PropertyValue value)
    {
        if (FindTabForPath(intent.Path) is not { IsMarkdown: true } tab)
        {
            _announce(new A11yEvent.HostComposed(
                // W0.5-3 residue: the owner note is gone; the draft
                // stays parked in the sheet.
                PropertyPhrase.NoNoteError, A11yPriority.High));
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
        AddPropertyViewModel? sheet = AddPropertySheet;
        if (tab.IsExternallyStale
            || !string.Equals(
                tab.SavedContentHash, intent.ContentHash, StringComparison.Ordinal))
        {
            AnnounceAddConflict(intent.Path, sheet, key, value);
            return false;
        }
        if (!TryAcquirePropertyWriteLease(intent.Path))
        {
            AnnounceWriteInFlightRefusal();
            return false;
        }
        tab.WriteProperty(
            intent.ContentHash,
            expectedHash => _session.SetProperty(intent.Path, key, value, expectedHash),
            WithLeaseRelease(
                intent.Path, AddWriteCompletion(intent.Path, sheet, key, value)));
        return true;
    }

    /// <summary>Add conflicts get the SAME resolution surface as row
    /// conflicts (contract 1): Keep Mine re-issues THIS add intent
    /// against a fresh disk hash; Reload refreshes the owner header
    /// while the sheet keeps the draft.</summary>
    private void AnnounceAddConflict(
        string path, AddPropertyViewModel? sheet, string key, PropertyValue value)
    {
        sheet?.MarkAddFailed();
        _announce(new A11yEvent.PropertyEditConflict(
            System.IO.Path.GetFileName(path)));
        PropertyConflictDialog?.Invoke(
            System.IO.Path.GetFileName(path),
            key,
            () => RetryAddWithFreshHash(path, sheet, key, value),
            () => ReloadPropertiesFromDisk(path));
    }

    /// <summary>Keep Mine for adds — the sanctioned fresh-hash
    /// re-read, operation-specific (it re-issues the ADD).</summary>
    private void RetryAddWithFreshHash(
        string path, AddPropertyViewModel? sheet, string key, PropertyValue value)
    {
        if (FindTabForPath(path) is not { IsMarkdown: true } tab)
        {
            return;
        }
        if (!TryAcquirePropertyWriteLease(path))
        {
            AnnounceWriteInFlightRefusal();
            return;
        }
        tab.WriteProperty(
            null,
            _ =>
            {
                string freshHash = _session.ReadNoteParts(path).ContentHash;
                return _session.SetProperty(path, key, value, freshHash);
            },
            WithLeaseRelease(path, AddWriteCompletion(path, sheet, key, value)));
    }

    private Action<SaveReport?, VaultException?, string?> AddWriteCompletion(
        string path, AddPropertyViewModel? sheet, string key, PropertyValue value) =>
        (report, failure, postFailureDiskHash) =>
        {
            if (report is not null)
            {
                if (ReferenceEquals(AddPropertySheet, sheet))
                {
                    AddPropertySheet = null;
                }
                _ = ReconcileTabsAfterPropertyWrite(path, report.NewContentHash);
                RefreshPropertiesFor(path);
                Panels.NoteSaved(path);
                _announce(new A11yEvent.PropertyChanged(key, false));
                return;
            }
            sheet?.MarkAddFailed();
            if (failure is VaultException.WriteConflict)
            {
                FindTabForPath(path)?.RefreshExternalStaleness();
                _announce(new A11yEvent.PropertyEditConflict(
                    System.IO.Path.GetFileName(path)));
                // The RESOLUTION owns the follow-up refresh
                // (adversarial round 4): a trailing refresh here
                // would supersede Reload's announced request, and
                // Cancel's contract is "the panel stays as it was".
                PropertyConflictDialog?.Invoke(
                    System.IO.Path.GetFileName(path),
                    key,
                    () => RetryAddWithFreshHash(path, sheet, key, value),
                    () => ReloadPropertiesFromDisk(path));
                return;
            }
            _announce(new A11yEvent.PropertyEditFailed(
                failure?.Message ?? "unknown failure"));
            ReconcileAfterReadBack(path, postFailureDiskHash);
        };

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
    /// nothing and announce exactly once. The target tab is resolved
    /// by the row's OWNER PATH (contract 1) — a row from a note the
    /// tab has navigated away from finds no tab and does nothing.</summary>
    internal bool SetPanelProperty(PropertyRowViewModel row)
    {
        if (FindTabForPath(row.OwnerPath) is not { IsMarkdown: true } tab)
        {
            return false;
        }
        if (!row.ValidateForCommit())
        {
            return true;
        }
        if (RefusePropertyWriteGates(tab, row, deleted: false))
        {
            return true;
        }
        DispatchRowWrite(tab, row, deleted: false);
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
        if (FindTabForPath(row.OwnerPath) is not { IsMarkdown: true } tab)
        {
            return false;
        }
        if (RefusePropertyWriteGates(tab, row, deleted: true))
        {
            return true;
        }
        DispatchRowWrite(tab, row, deleted: true);
        return true;
    }

    /// <summary>One dispatch path for row writes: the snapshot hash,
    /// the row's own in-flight flag, and the NOTE-SCOPED write lease
    /// (contract 3 — set, delete, add, and retries mutually exclude
    /// per path, refused pre-core with the spoken reason). The draft
    /// is CAPTURED at dispatch (adversarial round 2): the completion
    /// commits the dispatched value, never whatever the user typed
    /// while the write was in flight.</summary>
    private void DispatchRowWrite(
        WorkspaceTabViewModel tab, PropertyRowViewModel row, bool deleted)
    {
        string path = row.OwnerPath;
        if (!TryAcquirePropertyWriteLease(path))
        {
            AnnounceWriteInFlightRefusal();
            return;
        }
        row.WriteInFlight = true;
        PropertyDraft dispatched = PropertyDraft.Copy(row.Draft);
        Func<string?, SaveReport> write;
        if (deleted)
        {
            write = expectedHash => _session.DeleteProperty(path, row.Key, expectedHash);
        }
        else
        {
            PropertyValue value = PropertyValueCodec.Encode(dispatched);
            write = expectedHash => _session.SetProperty(path, row.Key, value, expectedHash);
        }
        tab.WriteProperty(
            row.ContentHash,
            write,
            WithLeaseRelease(path, PropertyWriteCompletion(path, row, deleted, dispatched)));
    }

    /// <summary>The shared refusal gates. True = refused (announced
    /// once, nothing written, draft intact).</summary>
    private bool RefusePropertyWriteGates(
        WorkspaceTabViewModel tab, PropertyRowViewModel row, bool deleted)
    {
        if (row.WriteInFlight || PropertyWriteInFlightFor(row.OwnerPath))
        {
            AnnounceWriteInFlightRefusal();
            return true;
        }
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
            AnnouncePropertyConflict(tab, row, deleted);
            return true;
        }
        return false;
    }

    /// <summary>Conflict resolution is OPERATION-SPECIFIC
    /// (adversarial round 1, contracts 1 and 5): a conflicted delete
    /// retries as a DELETE — Keep Mine must never turn a confirmed
    /// destructive operation into a set.</summary>
    private void AnnouncePropertyConflict(
        WorkspaceTabViewModel tab, PropertyRowViewModel row, bool deleted)
    {
        _announce(new A11yEvent.PropertyEditConflict(
            System.IO.Path.GetFileName(tab.Path)));
        string path = row.OwnerPath;
        PropertyConflictDialog?.Invoke(
            System.IO.Path.GetFileName(path),
            row.Key,
            () => RetryPropertyEditWithFreshHash(row, deleted),
            () => ReloadPropertiesFromDisk(path));
    }

    /// <summary>Keep Mine: re-issue THE SAME OPERATION against the
    /// CURRENT disk hash with the latest preserved draft — the only
    /// place a non-snapshot hash is ever used, at the user's
    /// explicit choice.</summary>
    private void RetryPropertyEditWithFreshHash(PropertyRowViewModel row, bool deleted)
    {
        if (FindTabForPath(row.OwnerPath) is not { IsMarkdown: true } tab
            || (!deleted && !row.ValidateForCommit())
            || row.WriteInFlight)
        {
            return;
        }
        string path = row.OwnerPath;
        if (!TryAcquirePropertyWriteLease(path))
        {
            AnnounceWriteInFlightRefusal();
            return;
        }
        row.WriteInFlight = true;
        PropertyDraft dispatched = PropertyDraft.Copy(row.Draft);
        Func<string?, SaveReport> write;
        if (deleted)
        {
            write = _ =>
            {
                string freshHash = _session.ReadNoteParts(path).ContentHash;
                return _session.DeleteProperty(path, row.Key, freshHash);
            };
        }
        else
        {
            PropertyValue value = PropertyValueCodec.Encode(dispatched);
            write = _ =>
            {
                string freshHash = _session.ReadNoteParts(path).ContentHash;
                return _session.SetProperty(path, row.Key, value, freshHash);
            };
        }
        tab.WriteProperty(
            null,
            write,
            WithLeaseRelease(path, PropertyWriteCompletion(path, row, deleted, dispatched)));
    }

    /// <summary>Reload-from-disk resolution: the outcome is spoken
    /// at the refresh's COMPLETION by the header VM — PropertiesReloaded
    /// on success, PropertiesReloadFailed with the reason on failure
    /// (contract 9; never an eager success echo).</summary>
    private void ReloadPropertiesFromDisk(string path) =>
        RefreshPropertiesFor(path, ReloadAnnounce.SuccessAndFailure);

    /// <summary>Terminal completion (contract 4): success
    /// re-baselines clean same-path tabs to the report hash BEFORE
    /// any later save can be issued, re-reads headers from
    /// authoritative bytes, refreshes the note panel (frontmatter
    /// line-count changes shift task offsets), and announces once.
    /// Failure keeps the draft, announces once, and reconciles when
    /// the disk hash moved (the W4-3 read-back rule).</summary>
    internal Action<SaveReport?, VaultException?, string?> PropertyWriteCompletion(
        string path, PropertyRowViewModel row, bool deleted, PropertyDraft dispatched) =>
        (report, failure, postFailureDiskHash) =>
        {
            row.WriteInFlight = false;
            if (report is not null)
            {
                row.MarkCommitted(dispatched);
                _ = ReconcileTabsAfterPropertyWrite(path, report.NewContentHash);
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
                    // The RESOLUTION owns the follow-up refresh
                    // (adversarial round 4): Keep Mine's completion
                    // and Reload each schedule their own; Cancel's
                    // contract is "the panel stays as it was".
                    AnnouncePropertyConflict(conflictTab, row, deleted);
                }
                else
                {
                    _announce(new A11yEvent.PropertyEditConflict(
                        System.IO.Path.GetFileName(path)));
                }
                return;
            }
            _announce(new A11yEvent.PropertyEditFailed(
                failure?.Message ?? "unknown failure"));
            ReconcileAfterReadBack(path, postFailureDiskHash);
        };

    /// <summary>The W4-3 read-back rule: a non-conflict failure whose
    /// disk hash moved re-baselines tabs and re-derives headers.</summary>
    private void ReconcileAfterReadBack(string path, string? postFailureDiskHash)
    {
        if (postFailureDiskHash is not null
            && FindTabForPath(path) is { } tab
            && !string.Equals(
                tab.SavedContentHash, postFailureDiskHash, StringComparison.Ordinal))
        {
            _ = ReconcileTabsAfterPropertyWrite(path, postFailureDiskHash);
            RefreshPropertiesFor(path);
            Panels.NoteSaved(path);
        }
    }

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
    /// that would let a later save clobber the rename. Rebaseline
    /// failures aggregate into ONE RenameReloadFailed (adversarial
    /// round 1 — the old try/catch could never fire because the
    /// callees report, not throw); header refresh failures speak
    /// their own canonical PropertiesReloadFailed at publish.</summary>
    internal void ReconcileTabsAfterBulkRename(RenameReport report)
    {
        bool anyReloadFailed = false;
        foreach (var affected in report.Affected)
        {
            if (!affected.Applied || affected.NewContentHash is null)
            {
                continue;
            }
            if (!ReconcileTabsAfterPropertyWrite(affected.Path, affected.NewContentHash))
            {
                anyReloadFailed = true;
            }
            RefreshPropertiesFor(affected.Path);
            Panels.NoteSaved(affected.Path);
        }
        if (anyReloadFailed)
        {
            _announce(new A11yEvent.RenameReloadFailed(
                "Some open notes could not be reloaded."));
        }
    }

    /// <summary>Contract 8 fan-out: every open same-path tab either
    /// re-baselines its buffer to the just-written disk state (clean
    /// tabs) or takes the stale-flag honesty (dirty tabs). Returns
    /// false when any tab's reload attempt failed.</summary>
    internal bool ReconcileTabsAfterPropertyWrite(string path, string newContentHash)
    {
        bool allSettled = true;
        foreach (WorkspaceTabViewModel tab in Groups.SelectMany(group => group.Tabs))
        {
            if (tab.IsMarkdown
                && string.Equals(tab.Path, path, StringComparison.Ordinal)
                && !tab.RebaselineAfterPropertyWrite(newContentHash, _announce))
            {
                allSettled = false;
            }
        }
        return allSettled;
    }

    /// <summary>Refresh the header VM of every open tab on this path
    /// (duplicate same-path tabs each hold their own instance). One
    /// user action announces ONCE (adversarial rounds 2+3,
    /// contract 9): the outcome mode — success AND failure — is
    /// granted to the first EXISTING header only; peer duplicates
    /// refresh silently. Post-write funnels default to FailureOnly
    /// (honest containment without a success echo).</summary>
    internal void RefreshPropertiesFor(
        string path, ReloadAnnounce announce = ReloadAnnounce.FailureOnly)
    {
        ReloadAnnounce pending = announce;
        foreach (WorkspaceTabViewModel tab in Groups.SelectMany(group => group.Tabs))
        {
            if (string.Equals(tab.Path, path, StringComparison.Ordinal)
                && tab.Properties is { } properties)
            {
                properties.RefreshProperties(pending);
                pending = ReloadAnnounce.None;
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
