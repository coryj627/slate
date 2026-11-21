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
                // Collision-check against the AUTHORITATIVE top-level
                // YAML keys (round 6), never the flattened rows.
                ? new PropertyAddIntent(tab.Path, header.ContentHash, header.TopLevelKeys)
                : null;
        var sheet = new AddPropertyViewModel(intent, CommitAddProperty, _announce);
        AddPropertySheet = sheet;
        sheet.SheetShown();
    }

    /// <summary>Window-supplied modal admission for the bulk-rename
    /// sheet (#1118, the TemplateOpenAdmission shape): consults
    /// <c>ModalSurfaces.DecideBulkRenameOpen</c> and performs any
    /// dismissal it calls for, so the chord, the menu item, and a
    /// palette row all pass one gate INSIDE the open. Null (headless
    /// tests) admits — the rule lives in the window layer where the
    /// surface state is.</summary>
    internal Func<bool>? BulkRenameOpenAdmission { get; set; }

    internal void OpenBulkRenameSheet(bool synchronousForTests = false)
    {
        if (BulkRenameOpenAdmission?.Invoke() == false)
        {
            return;
        }

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

    /// <summary>Plan 47 CB-9b test seam: runs first in a bulk rename apply's
    /// turn in the notes' chain, before its worker starts. A fact makes it
    /// throw.</summary>
    internal Action? BulkRenameApplyTurnHookForTests { get; set; }

    /// <summary>Plan 47 CB-9b test seam: runs before each of a tag edit's
    /// reads of the index at its turn, with "before" or "after" the core
    /// call. A fact makes one throw.</summary>
    internal Action<string>? TagEditIndexReadHookForTests { get; set; }

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
        string path = intent.Path;
        AdmitPropertyWrite(
            path,
            () => AddAdmitted(path, sheet, key, value, freshHash: false),
            abandoned: () =>
            {
                ReleasePropertyWriteLease(path);
                sheet?.MarkAddFailed();
            });
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
            () => DiscardAddDraftAndReload(path, sheet));
    }

    /// <summary>Keep Mine for adds — the sanctioned fresh-hash
    /// re-read, operation-specific (it re-issues the ADD).</summary>
    private void RetryAddWithFreshHash(
        string path, AddPropertyViewModel? sheet, string key, PropertyValue value)
    {
        if (FindTabForPath(path) is not { IsMarkdown: true })
        {
            return;
        }
        if (!TryAcquirePropertyWriteLease(path))
        {
            AnnounceWriteInFlightRefusal();
            return;
        }
        AdmitPropertyWrite(
            path,
            () => AddAdmitted(path, sheet, key, value, freshHash: true),
            abandoned: () =>
            {
                ReleasePropertyWriteLease(path);
                sheet?.MarkAddFailed();
            });
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
                // Plan 47 CB-9b (#1955's CB-9a item 3): every step runs even
                // when one before it throws (a dirty tab's sentence, say).
                AdmittedPublication.RunEvery(
                    () => _ = ReconcileTabsAfterPropertyWrite(path, report.NewContentHash),
                    () => RefreshPropertiesFor(path),
                    () => NotePersisted(path),
                    () => _announce(new A11yEvent.PropertyChanged(key, false)));
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
                    () => DiscardAddDraftAndReload(path, sheet));
                return;
            }
            // Plan 47 CB-9b (#1955's CB-9a item 3): the read-back reconcile
            // runs even when the failure's sentence throws.
            AdmittedPublication.RunEvery(
                () => _announce(new A11yEvent.PropertyEditFailed(
                    failure is null ? "unknown failure" : FailureDetail.Of(failure))),
                () => ReconcileAfterReadBack(path, postFailureDiskHash));
        };

    /// <summary>Reload after an ADD conflict: the edit the dialog was
    /// about is the SHEET's draft, so that is what gets discarded
    /// (round 6) — open row drafts are unrelated and survive.</summary>
    private void DiscardAddDraftAndReload(string path, AddPropertyViewModel? sheet)
    {
        if (ReferenceEquals(AddPropertySheet, sheet))
        {
            AddPropertySheet = null;
        }
        RefreshPropertiesFor(path, ReloadAnnounce.SuccessAndFailure);
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
            write = expectedHash => _session.DeletePropertyByIdentity(path, row.KeyIdentity, expectedHash);
        }
        else
        {
            PropertyValue value = PropertyValueCodec.Encode(dispatched);
            write = expectedHash => _session.SetPropertyByIdentity(path, row.KeyIdentity, value, expectedHash);
        }
        AdmitPropertyWrite(
            path,
            () => RowWriteAdmitted(path, row, deleted, dispatched, write, freshHash: false),
            abandoned: () => EndRowWrite(path, row));
    }

    /// <summary>The shared refusal gates. True = refused (announced
    /// once, nothing written, draft intact).</summary>
    /// <summary>ONE dirty-tab refusal sentence for every property
    /// write route — the panel rows (W4-4) and the Bases cells (W4-6
    /// D-14 shares the same divergence rationale).</summary>
    internal void AnnounceDirtyTabPropertyRefusal(WorkspaceTabViewModel tab) =>
        // W0.5-3 residue: dirty-tab refusal is a recorded
        // divergence — mac's body-only buffer never conflicts
        // with frontmatter writes; Windows whole-file tabs must
        // refuse (w_d notes).
        _announce(new A11yEvent.HostComposed(
            "Save the note before editing properties. The editor has unsaved "
                + $"changes in {System.IO.Path.GetFileName(tab.Path)}.",
            A11yPriority.High));

    private bool RefusePropertyWriteGates(
        WorkspaceTabViewModel tab, PropertyRowViewModel row, bool deleted)
    {
        if (row.WriteInFlight || PropertyWriteInFlightFor(row.OwnerPath))
        {
            AnnounceWriteInFlightRefusal();
            return true;
        }
        return RefuseHeldPropertyWrite(tab, row, deleted);
    }

    /// <summary>WB-2: the refusal gates without the in-flight gate, which an
    /// admitted write's own lease would trip — the dirty tab, then a stale
    /// tab or a row hash that is not the tab's saved hash.</summary>
    private bool RefuseHeldPropertyWrite(
        WorkspaceTabViewModel tab, PropertyRowViewModel row, bool deleted)
    {
        if (tab.IsDirty)
        {
            AnnounceDirtyTabPropertyRefusal(tab);
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
            () => ReloadDiscardingOnOwner(path, row));
    }

    /// <summary>Keep Mine: re-issue THE SAME OPERATION against the
    /// CURRENT disk hash with the latest preserved draft — the only
    /// place a non-snapshot hash is ever used, at the user's
    /// explicit choice.</summary>
    private void RetryPropertyEditWithFreshHash(PropertyRowViewModel row, bool deleted)
    {
        if (FindTabForPath(row.OwnerPath) is not { IsMarkdown: true }
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
                return _session.DeletePropertyByIdentity(path, row.KeyIdentity, freshHash);
            };
        }
        else
        {
            PropertyValue value = PropertyValueCodec.Encode(dispatched);
            write = _ =>
            {
                string freshHash = _session.ReadNoteParts(path).ContentHash;
                return _session.SetPropertyByIdentity(path, row.KeyIdentity, value, freshHash);
            };
        }
        AdmitPropertyWrite(
            path,
            () => RowWriteAdmitted(path, row, deleted, dispatched, write, freshHash: true),
            abandoned: () => EndRowWrite(path, row));
    }

    /// <summary>Reload-from-disk resolution: the outcome is spoken
    /// at the refresh's COMPLETION by the header VM — PropertiesReloaded
    /// on success, PropertiesReloadFailed with the reason on failure
    /// (contract 9; never an eager success echo).</summary>
    /// <summary>Reload after a ROW conflict: the conflicted row's own
    /// header discards that draft and speaks the outcome; every other
    /// same-path header refreshes silently and KEEPS its drafts
    /// (round 7 — the discard must not land on whichever duplicate
    /// was enumerated first).</summary>
    private void ReloadDiscardingOnOwner(string path, PropertyRowViewModel row)
    {
        if (row.Owner is { } owner)
        {
            owner.ReloadDiscarding(row.KeyIdentity);
            foreach (WorkspaceTabViewModel tab in Groups.SelectMany(group => group.Tabs))
            {
                if (string.Equals(tab.Path, path, StringComparison.Ordinal)
                    && tab.Properties is { } peer
                    && !ReferenceEquals(peer, owner))
                {
                    peer.RefreshProperties();
                }
            }
            return;
        }
        RefreshPropertiesFor(path, ReloadAnnounce.SuccessAndFailure);
    }

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
                // Plan 47 CB-9b (#1955's CB-9a item 3): every step runs even
                // when one before it throws (a dirty tab's sentence, say).
                AdmittedPublication.RunEvery(
                    () => _ = ReconcileTabsAfterPropertyWrite(path, report.NewContentHash),
                    () => RefreshPropertiesFor(path),
                    () => NotePersisted(path),
                    () => _announce(new A11yEvent.PropertyChanged(row.Key, deleted)));
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
            // Plan 47 CB-9b (#1955's CB-9a item 3): the read-back reconcile
            // runs even when the failure's sentence throws.
            AdmittedPublication.RunEvery(
                () => _announce(new A11yEvent.PropertyEditFailed(
                    failure is null ? "unknown failure" : FailureDetail.Of(failure))),
                () => ReconcileAfterReadBack(path, postFailureDiskHash));
        };

    /// <summary>The W4-3 read-back rule: a non-conflict failure whose
    /// disk hash moved re-baselines tabs and re-derives headers. Plan 47
    /// CB-9b (the red team's m3; #1955's CB-9a item 3): the reconcile, the
    /// header refresh and the panels are steps of their own, so a dirty
    /// tab whose sentence throws skips neither the refresh nor the panels;
    /// the first fault is rethrown once every step has run.</summary>
    private void ReconcileAfterReadBack(string path, string? postFailureDiskHash)
    {
        if (postFailureDiskHash is { } diskHash
            && FindTabForPath(path) is { } tab
            && !string.Equals(
                tab.SavedContentHash, diskHash, StringComparison.Ordinal))
        {
            AdmittedPublication.RunEvery(
                () => _ = ReconcileTabsAfterPropertyWrite(path, diskHash),
                () => RefreshPropertiesFor(path),
                () => NotePersisted(path));
        }
    }

    /// <summary>Build the bulk-rename sheet VM, wired to this
    /// workspace: the old key prefills from the active note's first
    /// property, the dirty-draft gate scans every open tab's header,
    /// and each apply is admitted into its notes' chains, where its
    /// report reconciles open tabs (contract 8; plan 47 CB-9b).</summary>
    internal BulkRenameViewModel CreateBulkRename(bool synchronousForTests = false)
    {
        var sheet = new BulkRenameViewModel(
            _session,
            _announce,
            AnyOpenDraftDirty,
            ApplyBulkRenameAdmitted,
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
    /// their own canonical PropertiesReloadFailed at publish.
    /// Plan 47 CB-9b (the red team's m2; #1955's CB-9a item 3): each
    /// renamed note's reconcile, header refresh and panels are steps of
    /// their own, so a dirty tab whose sentence throws skips no note after
    /// it; the one RenameReloadFailed is the last step, and the first fault
    /// is rethrown once every step has run.</summary>
    internal void ReconcileTabsAfterBulkRename(RenameReport report)
    {
        bool anyReloadFailed = false;
        List<Action> steps = [];
        foreach (var affected in report.Affected)
        {
            if (!affected.Applied || affected.NewContentHash is not { } newContentHash)
            {
                continue;
            }
            string path = affected.Path;
            steps.Add(() =>
            {
                if (!ReconcileTabsAfterPropertyWrite(path, newContentHash))
                {
                    anyReloadFailed = true;
                }
            });
            steps.Add(() => RefreshPropertiesFor(path));
            steps.Add(() => NotePersisted(path));
        }
        // Codex round 3: a note core wrote and then failed to index is
        // reported failed, and the index still holds its old hash, so the
        // catch-up passes its tabs: they are reconciled from a read of the
        // disk, with a header refresh that speaks nothing.
        foreach (var failed in report.Failed)
        {
            if (!WriteMayHaveLanded(failed.Message))
            {
                continue;
            }
            string path = failed.Path;
            steps.Add(() =>
            {
                if (!ReconcileTabsFromARead(path))
                {
                    anyReloadFailed = true;
                }
            });
            steps.Add(() => RefreshPropertiesFor(path, ReloadAnnounce.None));
            steps.Add(() => NotePersisted(path));
        }
        steps.Add(() =>
        {
            if (anyReloadFailed)
            {
                _announce(new A11yEvent.RenameReloadFailed(
                    "Some open notes could not be reloaded."));
            }
        });
        AdmittedPublication.RunEvery([.. steps]);
    }

    /// <summary>Whether any open tab's header holds a draft not yet
    /// committed: the bulk rename's apply refuses while one does, at the
    /// press and again at its turn.</summary>
    private bool AnyOpenDraftDirty() =>
        Groups.SelectMany(group => group.Tabs).Any(tab => tab.Properties?.AnyRowDirty == true);

    // ---- Plan 47 CB-9b (#1889; CD5(a)): the bulk rename's apply in the notes' chains ----
    //
    // The apply rewrites every note that has the key, so it takes its turn in
    // the chains of the notes its armed preview listed (core picks the notes
    // at its turn, from the index, so no set computed before it is complete;
    // a note that gained the key after the preview is the leftover race, and
    // each writer's compare-and-swap ends it in one refused write). It waits
    // for every save and operation admitted for those notes before it, a save
    // of one of them asked meanwhile waits for its publication, and its turn
    // re-checks the header-draft gate. Its publication, inside the turn,
    // reconciles the notes' tabs, catches up a tab a rescan or a re-seat had
    // to leave while it held the note, and speaks the summary. Every path
    // ends the operation.

    // Plan 47 CB-9b (Design 3): the tokens of every admitted apply not yet
    // ended, whichever sheet asked for it (a sheet the headless open replaced
    // included), and how many of teardown's settles are pumping.
    private readonly HashSet<CancelToken> _admittedApplies = new(ReferenceEqualityComparer.Instance);
    private int _teardownSettles;

    /// <summary>Plan 47 CB-9b (Design 3): cancel every admitted apply. Teardown
    /// does it before its settle pumps and before it joins the workers.</summary>
    private void CancelAdmittedApplies()
    {
        foreach (CancelToken token in _admittedApplies)
        {
            token.Cancel();
        }
    }

    /// <summary>The sheet's apply request: admitted over the armed
    /// preview's notes, its token kept until the apply ends, and cancelled
    /// at once when teardown's settle is pumping. An apply that never runs,
    /// because the chain closed first, ends on the sheet as one that did not
    /// run.</summary>
    private void ApplyBulkRenameAdmitted(BulkRenameViewModel sheet, BulkRenameViewModel.BulkRenameApply apply)
    {
        _ = _admittedApplies.Add(apply.Cancel);
        if (_teardownSettles > 0)
        {
            apply.Cancel.Cancel();
        }
        AdmitPropertyWrite(
            apply.Paths,
            () => BulkRenameApplyAtItsTurn(sheet, apply),
            abandoned: () =>
            {
                _ = _admittedApplies.Remove(apply.Cancel);
                sheet.EndApplyUnrun(apply);
            });
    }

    /// <summary>The apply at its turn: a torn-down workspace or a closed
    /// chain ends it unrun; a dirty header draft refuses it with the press's
    /// sentence; otherwise its worker starts, tracked by the chain, and its
    /// publication ends the operation. A cancelled token (Escape, or
    /// teardown's settle, while it waited) still runs: core answers at once
    /// with every note cancelled. Every fault but the fatal three, here or in
    /// a step before the worker starts, ends the apply as a failure with the
    /// existing RenameFailed.</summary>
    private Task BulkRenameApplyAtItsTurn(BulkRenameViewModel sheet, BulkRenameViewModel.BulkRenameApply apply)
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            BulkRenameApplyTurnHookForTests?.Invoke();
            if (_workspaceDisposed || _saves.IsClosed)
            {
                EndBulkRenameApply(apply, () => sheet.EndApplyUnrun(apply), done);
                return done.Task;
            }
            if (AnyOpenDraftDirty())
            {
                EndBulkRenameApply(apply, () => sheet.RefuseApplyForDirtyDrafts(apply), done);
                return done.Task;
            }
            BulkRenameViewModel.ApplyOutcome? outcome = null;
            Task worker = sheet.StartApplyWork(apply, ran => Volatile.Write(ref outcome, ran));
            _saves.TrackWorker(worker);
            if (worker.IsCompleted)
            {
                FinishBulkRenameApply(sheet, apply, Volatile.Read(ref outcome), done);
                return done.Task;
            }
            // The backstop and the publication in one: when the worker ends,
            // however it ends, the apply is finished on the save dispatcher.
            // A body that never reported (a shutdown skipped it, or it faulted
            // outside its catch) finishes as an apply that did not run.
            _ = worker.ContinueWith(
                _ => RunOnSaveDispatcher(() => FinishBulkRenameApply(sheet, apply, Volatile.Read(ref outcome), done)),
                CancellationToken.None,
                TaskContinuationOptions.None,
                TaskScheduler.Default);
        }
        catch (Exception exception) when (
            exception is not OutOfMemoryException
                and not StackOverflowException
                and not AccessViolationException)
        {
            EndBulkRenameApply(apply, () => sheet.PublishApply(apply, null, FailureDetail.Of(exception)), done);
        }
        return done.Task;
    }

    /// <summary>An apply that ends without its worker's publication: its
    /// token leaves the admitted set, the sheet's step runs, then the
    /// operation ends, carrying the step's fault if it threw.</summary>
    private void EndBulkRenameApply(
        BulkRenameViewModel.BulkRenameApply apply, Action onTheSheet, TaskCompletionSource done)
    {
        _ = _admittedApplies.Remove(apply.Cancel);
        try
        {
            onTheSheet();
        }
        catch (Exception exception)
        {
            _ = done.TrySetException(exception);
            return;
        }
        _ = done.TrySetResult();
    }

    /// <summary>
    /// The apply's publication, on the save dispatcher and inside its turn,
    /// as steps that each run even when one before throws
    /// (<see cref="AdmittedPublication.RunEvery"/>): the reconcile of the
    /// renamed notes' tabs (contract 8, skipped once the workspace is torn
    /// down), the catch-up of the listed notes' clean tabs, the summary
    /// (spoken whenever a report exists, today's disk-truth rule), and the
    /// sheet's own publication. An apply whose worker never reported ends
    /// on the sheet as one that did not run. The operation ends when this
    /// returns.
    /// </summary>
    private void FinishBulkRenameApply(
        BulkRenameViewModel sheet,
        BulkRenameViewModel.BulkRenameApply apply,
        BulkRenameViewModel.ApplyOutcome? outcome,
        TaskCompletionSource done)
    {
        _ = _admittedApplies.Remove(apply.Cancel);
        try
        {
            if (outcome is null)
            {
                sheet.EndApplyUnrun(apply);
            }
            else
            {
                RenameReport? report = outcome.Report;
                AdmittedPublication.RunEvery(
                    () =>
                    {
                        if (report is not null && !_workspaceDisposed)
                        {
                            ReconcileTabsAfterBulkRename(report);
                        }
                    },
                    () =>
                    {
                        if (!_workspaceDisposed)
                        {
                            CatchUpCleanTabsAfterAdmittedWrite(apply.Paths);
                        }
                    },
                    () =>
                    {
                        if (report is not null)
                        {
                            _announce(new A11yEvent.RenameSummary(
                                Applied: true,
                                Renamed: (uint)report.Affected.Length,
                                Skipped: (uint)report.Skipped.Length,
                                Failed: (uint)report.Failed.Length));
                        }
                    },
                    () => sheet.PublishApply(apply, report, outcome.Error));
            }
        }
        catch (Exception exception)
        {
            _ = done.TrySetException(exception);
            return;
        }
        _ = done.TrySetResult();
    }

    /// <summary>
    /// Plan 47 CB-9b (Design 2's catch-up), inside an admitted operation's
    /// ticket over <paramref name="paths"/>. While the operation held a
    /// note, a rescan's reload had to mark a clean tab on it stale and keep
    /// its bytes, and a re-seat had to mark a returning tab back on disk with
    /// its old bytes (both ask the chain and keep the tab while it is held).
    /// Nothing reloads either afterwards, so here every clean Markdown tab
    /// with no save pending whose saved hash is not the index's is brought
    /// up to disk from a read made now, with no undo step back
    /// (<see cref="WorkspaceTabViewModel.RebaselineToRead"/>). The index is
    /// read in slices of core's bound, for the notes with such a tab only. A
    /// read that fails leaves its tabs as they are, silently: nothing is
    /// spoken here.
    /// </summary>
    private void CatchUpCleanTabsAfterAdmittedWrite(IReadOnlyCollection<string> paths)
    {
        HashSet<string> listed = new(paths, StringComparer.Ordinal);
        string[] open = [.. Groups.SelectMany(group => group.Tabs)
            .Where(tab => tab.IsMarkdown && listed.Contains(tab.Path) && CanCatchUp(tab))
            .Select(tab => tab.Path)
            .Distinct(StringComparer.Ordinal)];
        if (open.Length == 0)
        {
            return;
        }
        Dictionary<string, string?> indexed;
        try
        {
            indexed = IndexedHashesInSlices(open);
        }
        catch (Exception exception) when (
            exception is not OutOfMemoryException
                and not StackOverflowException
                and not AccessViolationException)
        {
            return;
        }
        foreach (string path in open)
        {
            if (indexed.GetValueOrDefault(path) is not { Length: > 0 } hash)
            {
                continue;
            }
            WorkspaceTabViewModel[] behind = [.. Groups.SelectMany(group => group.Tabs)
                .Where(tab => tab.IsMarkdown
                    && string.Equals(tab.Path, path, StringComparison.Ordinal)
                    && CanCatchUp(tab)
                    && !string.Equals(tab.SavedContentHash, hash, StringComparison.Ordinal))];
            if (behind.Length == 0)
            {
                continue;
            }
            IdentifiedText read;
            try
            {
                read = _session.ReadTextIdentified(path);
            }
            catch (VaultException)
            {
                continue;
            }
            foreach (WorkspaceTabViewModel tab in behind)
            {
                _ = tab.RebaselineToRead(read);
            }
        }
    }

    private static bool CanCatchUp(WorkspaceTabViewModel tab) =>
        !tab.IsDirty && !tab.HasPendingSaves && !tab.IsMissingFromDisk;

    /// <summary>Plan 47 CB-9b test seam (Codex round 3): the slice size of
    /// <see cref="IndexedHashesInSlices"/>; null uses core's bound.</summary>
    internal int? IndexedHashSliceForTests { get; set; }

    /// <summary>Plan 47 CB-9b test seam (Codex round 3): told the path count
    /// of each index lookup <see cref="IndexedHashesInSlices"/>
    /// makes.</summary>
    internal Action<int>? IndexedHashLookupHookForTests { get; set; }

    /// <summary>The index's content hash of each path, read in slices of
    /// core's bound on one lookup (the rescan's slicing,
    /// <c>VaultLifecycleViewModel.ReadIndexedPaths</c>).</summary>
    private Dictionary<string, string?> IndexedHashesInSlices(IReadOnlyList<string> paths)
    {
        var indexed = new Dictionary<string, string?>(StringComparer.Ordinal);
        int slice = IndexedHashSliceForTests ?? (int)Math.Max(1U, SlateUniffiMethods.MaxIndexedHashPaths());
        using var cancel = new CancelToken();
        for (int start = 0; start < paths.Count; start += slice)
        {
            string[] part = [.. paths.Skip(start).Take(slice)];
            IndexedHashLookupHookForTests?.Invoke(part.Length);
            IReadOnlyList<string?> hashes = _session.IndexedContentHashes(part, cancel);
            for (int index = 0; index < part.Length; index++)
            {
                indexed[part[index]] = hashes[index];
            }
        }
        return indexed;
    }

    // ---- Plan 47 CB-9b (#1889; CD5(a)): the tag edit in the notes' chains -------
    //
    // A tag added to or removed from the checked notes in the Files sidebar
    // rewrites each of them, so it takes its turn in the chains of all of
    // them: it waits for every save and operation admitted for any of them
    // before it, and a save of one of them asked meanwhile waits for its
    // publication. It runs on the dispatcher, as it always has, so on an idle
    // chain it runs in the turn of the press. Core's report names no file it
    // wrote, so the turn reads the index's hash of each checked note with an
    // open Markdown tab before and after the core call (the index row commits
    // with each write), and the notes whose hash moved are the ones it wrote.
    // A rescan that re-indexes one of those notes between the two reads makes
    // it look written: its clean tab is brought up to the disk, and a dirty
    // one hears the property sentence.

    // Plan 47 CB-9b (Codex round 2): the tag edits admitted and not yet run,
    // each with the paths it will write. On main the tag edit ran at the
    // press; queued, it must follow this window's renames and moves
    // (RetargetPath) and drop a note that left with no destination
    // (InvalidatePath), as the tabs do, or at its turn core would tag
    // whatever note now sits at a path the reader checked. Another session's
    // rename while it waits is the race main has at the press: core tags
    // what its index holds at the path.
    private readonly List<QueuedTagEdit> _queuedTagEdits = [];

    /// <summary>A tag edit admitted and not yet run, and the paths it will
    /// write, which this window's renames, moves and deletes keep
    /// current.</summary>
    private sealed class QueuedTagEdit(FilesSidebarViewModel.TagEditRequest request)
    {
        internal FilesSidebarViewModel.TagEditRequest Request { get; } = request;

        internal List<string> Paths { get; } = [.. request.Paths];
    }

    /// <summary>The sidebar's tag edit: admitted over the checked notes, and
    /// run at its turn over the paths those notes have then (Codex round 2).
    /// A torn-down workspace admits nothing.</summary>
    internal void EditTagAdmitted(FilesSidebarViewModel.TagEditRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (_workspaceDisposed)
        {
            return;
        }
        var queued = new QueuedTagEdit(request);
        _queuedTagEdits.Add(queued);
        AdmitPropertyWrite(
            request.Paths,
            () =>
            {
                _ = _queuedTagEdits.Remove(queued);
                return EditTagAtItsTurn(queued.Request with { Paths = [.. queued.Paths] });
            },
            abandoned: () => _ = _queuedTagEdits.Remove(queued));
    }

    /// <summary>Plan 47 CB-9b (Codex round 2): this window renamed or moved
    /// <paramref name="source"/> to <paramref name="destination"/>. Each
    /// queued tag edit's path under it follows by the rule the tabs follow
    /// (<see cref="TryRetargetPath"/>), and the new path's chain continues
    /// from the old one's, so a save of the moved note waits for the tag
    /// edit as it would have at the old path.</summary>
    private void RetargetQueuedTagEdits(string source, string destination)
    {
        foreach (QueuedTagEdit queued in _queuedTagEdits)
        {
            for (int index = 0; index < queued.Paths.Count; index++)
            {
                if (TryRetargetPath(queued.Paths[index], source, destination, out string retargeted))
                {
                    _saves.ContinueChain(queued.Paths[index], retargeted);
                    queued.Paths[index] = retargeted;
                }
            }
        }
    }

    /// <summary>Plan 47 CB-9b (Codex round 2): the note at
    /// <paramref name="invalidated"/>, or under it, left with no destination.
    /// Each queued tag edit drops it, so a note created there later is never
    /// tagged in its place.</summary>
    private void DropQueuedTagEditPaths(string invalidated)
    {
        foreach (QueuedTagEdit queued in _queuedTagEdits)
        {
            _ = queued.Paths.RemoveAll(path => IsSameOrDescendantPath(path, invalidated));
        }
    }

    /// <summary>
    /// The tag edit at its turn, inline on the dispatcher. A torn-down
    /// workspace or a closed chain ends it unrun and silent. Otherwise the
    /// index is read for the checked notes with an open Markdown tab, the
    /// core call is made, and the index is read again, each in its own
    /// <c>try</c>: a core call that throws (a VaultException, or any fault but
    /// the fatal three) is the failure the sidebar speaks, and a read that
    /// fails sends the reconcile to a read of the disk. Then, as steps that
    /// each run even when one before throws: each written note's tabs
    /// reconciled by the property write's rule (a clean tab takes the bytes
    /// with no undo step back, a dirty one hears the property sentence), its
    /// headers refreshed and its panels told; the catch-up of the clean tabs a
    /// rescan or a re-seat had to leave while the edit waited; and the
    /// sidebar's publication of core's summary or the failure, whatever the
    /// reads did. The operation ends when this returns.
    /// </summary>
    private Task EditTagAtItsTurn(FilesSidebarViewModel.TagEditRequest request)
    {
        if (_workspaceDisposed || _saves.IsClosed)
        {
            return Task.CompletedTask;
        }
        HashSet<string> named = new(request.Paths, StringComparer.Ordinal);
        string[] open = [.. Groups.SelectMany(group => group.Tabs)
            .Where(tab => tab.IsMarkdown && named.Contains(tab.Path))
            .Select(tab => tab.Path)
            .Distinct(StringComparer.Ordinal)];
        Dictionary<string, string?>? before = TagEditIndexRead(open, "before");
        TagEditReport? report = null;
        string? failure = null;
        try
        {
            request.HookForTests();
            report = request.Add
                ? _session.AddTagToFiles([.. request.Paths], request.Tag)
                : _session.RemoveTagFromFiles([.. request.Paths], request.Tag);
        }
        catch (Exception exception) when (
            exception is not OutOfMemoryException
                and not StackOverflowException
                and not AccessViolationException)
        {
            failure = FailureDetail.Of(exception);
        }
        Dictionary<string, string?>? after = TagEditIndexRead(open, "after");
        // Codex round 3: a note core wrote and then failed to index is
        // reported skipped, and its index hash did not move.
        HashSet<string> landed = report is null
            ? new(StringComparer.Ordinal)
            : new(
                report.Skipped.Where(skip => WriteMayHaveLanded(skip.Reason)).Select(skip => skip.Path),
                StringComparer.Ordinal);
        List<Action> steps = [];
        foreach (string path in open)
        {
            if (before is null || after is null || landed.Contains(path))
            {
                // A read of the index failed, or core wrote the note and then
                // failed its index commit: the note's tabs are reconciled from
                // a read of the disk instead.
                steps.Add(() => _ = ReconcileTabsFromARead(path));
                steps.Add(() => RefreshPropertiesFor(path, ReloadAnnounce.None));
                steps.Add(() => NotePersisted(path));
            }
            else if (after.GetValueOrDefault(path) is { Length: > 0 } written
                && !string.Equals(before.GetValueOrDefault(path), written, StringComparison.Ordinal))
            {
                steps.Add(() => _ = ReconcileTabsAfterPropertyWrite(path, written));
                steps.Add(() => RefreshPropertiesFor(path, ReloadAnnounce.None));
                steps.Add(() => NotePersisted(path));
            }
        }
        steps.Add(() => CatchUpCleanTabsAfterAdmittedWrite(open));
        steps.Add(() => request.Publish(report, failure));
        try
        {
            AdmittedPublication.RunEvery([.. steps]);
        }
        catch (Exception exception)
        {
            return Task.FromException(exception);
        }
        return Task.CompletedTask;
    }

    /// <summary>A tag edit's read of the index for <paramref name="open"/>
    /// (<see cref="IndexedHashesInSlices"/>: in slices of core's bound, with a
    /// token of its own), or null when it failed. With no note to read, it
    /// reads nothing.</summary>
    private Dictionary<string, string?>? TagEditIndexRead(string[] open, string when)
    {
        if (open.Length == 0)
        {
            return new Dictionary<string, string?>(StringComparer.Ordinal);
        }
        try
        {
            TagEditIndexReadHookForTests?.Invoke(when);
            return IndexedHashesInSlices(open);
        }
        catch (Exception exception) when (
            exception is not OutOfMemoryException
                and not StackOverflowException
                and not AccessViolationException)
        {
            return null;
        }
    }

    // ---- Plan 47 CB-9b (#1889; CD5(b)): clean tabs follow a link rewrite --------

    /// <summary>
    /// A rename or a move from the Files sidebar (a batch move, or the undo or
    /// redo of one, included) rewrote the links of the notes in
    /// <paramref name="rewritten"/>: core's report, each note under its own
    /// compare-and-swap, made outside the save chain (CD5(b)), in this same
    /// dispatcher turn and after the moved pairs were retargeted. Per note, in
    /// report order, from the first outcome's hash before to the last
    /// outcome's hash after (one note can be rewritten twice, chained): each
    /// clean Markdown tab with no save pending that holds the bytes from
    /// before the rewrite takes a read made now, when the read holds the bytes
    /// after it, with no undo step back across them and its caret kept on its
    /// line (<see cref="WorkspaceTabViewModel.RebaselineAfterLinkRewrite"/>).
    /// A note with a save or an operation admitted is left to it, and so are
    /// a dirty tab and a tab on other bytes: the change event marks them
    /// stale. Nothing is spoken: the change's own sentence counts the notes
    /// whose links it updated. A fault here is logged, never thrown into the
    /// structural change that raised it.
    /// </summary>
    internal void RebaselineCleanTabsAfterLinkRewrite(IReadOnlyList<RewriteOutcome> rewritten)
    {
        ArgumentNullException.ThrowIfNull(rewritten);
        if (_workspaceDisposed)
        {
            return;
        }
        foreach (IGrouping<string, RewriteOutcome> note in rewritten.GroupBy(outcome => outcome.Path, StringComparer.Ordinal))
        {
            string path = note.Key;
            string before = note.First().HashBefore;
            string after = note.Last().HashAfter;
            try
            {
                WorkspaceTabViewModel[] behind = [.. Groups.SelectMany(group => group.Tabs)
                    .Where(tab => tab.IsMarkdown
                        && string.Equals(tab.Path, path, StringComparison.Ordinal)
                        && !tab.IsDirty
                        && !tab.HasPendingSaves
                        && string.Equals(tab.SavedContentHash, before, StringComparison.Ordinal))];
                if (behind.Length == 0 || _saves.HasAdmittedSaveFor(path))
                {
                    continue;
                }
                IdentifiedText read;
                try
                {
                    read = _session.ReadTextIdentified(path);
                }
                catch (VaultException)
                {
                    continue;
                }
                foreach (WorkspaceTabViewModel tab in behind)
                {
                    _ = tab.RebaselineAfterLinkRewrite(read, after);
                }
            }
            catch (Exception exception) when (
                exception is not OutOfMemoryException
                    and not StackOverflowException
                    and not AccessViolationException)
            {
                HostLog.Write(HostDiagnosticEvent.VaultCommandFailed, exception);
            }
        }
    }

    /// <summary>Plan 47 CB-9b (Codex round 3): whether a note core reported
    /// skipped or failed may hold bytes core wrote. Core's SavedButUnindexed
    /// error is the one failure it returns after a write is durable (the file
    /// written, the index commit failed), and its text, "saved but not
    /// indexed: ..." (crates/slate-core/src/lib.rs), is carried by the tag
    /// edit's skip reason and the bulk rename's failure message. Any other
    /// skip or failure wrote nothing, or met a note changed outside Slate,
    /// which the change event reports.</summary>
    private static bool WriteMayHaveLanded(string reason) =>
        reason.Contains("saved but not indexed:", StringComparison.Ordinal);

    /// <summary>Plan 47 CB-9b test seam (Codex round 4): runs just before a
    /// reconcile from a read reads the note at the path it is given. A fact
    /// holds the file there so the read fails.</summary>
    internal Action<string>? ReadBackHookForTests { get; set; }

    /// <summary>Plan 47 CB-9b: a note's open Markdown tabs reconciled from a
    /// read of the disk, by the property write's rule (a clean tab takes the
    /// bytes with no undo step back, a dirty tab hears the property
    /// sentence). Used where the index cannot say what was written: a failed
    /// index read, and a write whose index commit failed. False when a tab's
    /// reload failed, or when the read failed, which marks the note's tabs
    /// stale with the property sentence, once (Codex round 4). A note with no
    /// open Markdown tab reconciles nothing.</summary>
    private bool ReconcileTabsFromARead(string path)
    {
        WorkspaceTabViewModel[] tabs = [.. Groups.SelectMany(group => group.Tabs)
            .Where(tab => tab.IsMarkdown && string.Equals(tab.Path, path, StringComparison.Ordinal))];
        if (tabs.Length == 0)
        {
            return true;
        }
        IdentifiedText read;
        try
        {
            ReadBackHookForTests?.Invoke(path);
            read = _session.ReadTextIdentified(path);
        }
        catch (VaultException)
        {
            // Codex round 4: the tabs cannot take bytes they cannot read, and
            // nothing repairs them later (the index still holds the old hash),
            // so each is marked stale, and the note's first tab speaks the
            // property sentence once (Call 2(b)'s occasion). A tab whose
            // sentence throws skips no tab after it.
            AdmittedPublication.RunEvery([.. tabs.Select<WorkspaceTabViewModel, Action>((tab, index) => () =>
                tab.MarkPropertyStaleAfterAFailedReadBack(index == 0 ? _announce : _ => { }))]);
            return false;
        }
        return ReconcileTabsAfterPropertyWrite(path, SlateUniffiMethods.EditorTextContentHash(read.Text));
    }

    /// <summary>Contract 8 fan-out: every open same-path tab either
    /// re-baselines its buffer to the just-written disk state (clean
    /// tabs) or takes the stale-flag honesty (dirty tabs). Returns
    /// false when any tab's reload attempt failed.</summary>
    internal bool ReconcileTabsAfterPropertyWrite(string path, string newContentHash)
    {
        bool allSettled = true;
        // Plan 47 CB-9b (#1955's CB-9a item 3): each tab is a step of its
        // own, so a dirty tab whose sentence throws skips no tab after it;
        // the first fault is rethrown once every tab has run.
        WorkspaceTabViewModel[] tabs = [.. Groups.SelectMany(group => group.Tabs)
            .Where(tab => tab.IsMarkdown && string.Equals(tab.Path, path, StringComparison.Ordinal))];
        AdmittedPublication.RunEvery([.. tabs.Select<WorkspaceTabViewModel, Action>(tab => () =>
        {
            if (!tab.RebaselineAfterPropertyWrite(newContentHash, _announce))
            {
                allSettled = false;
            }
        })]);
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
        string path,
        ReloadAnnounce announce = ReloadAnnounce.FailureOnly,
        string? discardDraftKey = null)
    {
        ReloadAnnounce pending = announce;
        // The discard is scoped to the row the dialog was about, and
        // only on the header that owns the outcome (round 6): peer
        // duplicates refresh silently AND keep their drafts.
        string? pendingDiscard = discardDraftKey;
        foreach (WorkspaceTabViewModel tab in Groups.SelectMany(group => group.Tabs))
        {
            if (string.Equals(tab.Path, path, StringComparison.Ordinal)
                && tab.Properties is { } properties)
            {
                properties.RefreshProperties(pending, pendingDiscard);
                pending = ReloadAnnounce.None;
                pendingDiscard = null;
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

    // ---- WB-2: admitted property writes (#1325; WD2, answered 2026-10-21) ----
    //
    // A property write rewrites the note's file, so it joins the note's save
    // chain: it never writes beside a save admitted before it, and a save
    // requested after it waits for its publication. A write admitted behind
    // a save also waits until a header has re-read the saved bytes and takes
    // its CAS token from that fresh read — the header stays the single hash
    // authority (contract 4) — and re-validates the edited row there. The
    // lease is held from dispatch to the operation's end. Every outcome that
    // writes nothing releases the lease and the in-flight flag BEFORE it
    // speaks or opens the conflict dialog (Keep Mine re-acquires the lease
    // and is admitted behind this operation), then ends the operation; a
    // closed coordinator counts as abandoned at every step.

    /// <summary>Admit <paramref name="operation"/> into the note's chain.
    /// <paramref name="abandoned"/> runs when the operation never ran: the
    /// coordinator was closed when it was admitted, or closed before its
    /// turn came.</summary>
    private void AdmitPropertyWrite(string path, Func<Task> operation, Action abandoned)
    {
        bool started = false;
        Task<bool> admitted = _saves.Admit(path, _ =>
        {
            started = true;
            return operation();
        });
        AbandonIfNeverStarted(admitted, () => started, abandoned);
    }

    /// <summary>Plan 47 CB-9b (#1889; CD5(a)): the same admission over
    /// several notes at once (<see cref="WorkspaceSaveCoordinator.Admit(IReadOnlyCollection{string}, Func{WorkspaceSaveCoordinator.SaveTicket, Task})"/>),
    /// for an operation that may rewrite any of them.</summary>
    private void AdmitPropertyWrite(IReadOnlyCollection<string> paths, Func<Task> operation, Action abandoned)
    {
        bool started = false;
        Task<bool> admitted = _saves.Admit(paths, _ =>
        {
            started = true;
            return operation();
        });
        AbandonIfNeverStarted(admitted, () => started, abandoned);
    }

    /// <summary><paramref name="abandoned"/>, on the dispatcher, when the
    /// admitted operation never ran.</summary>
    private void AbandonIfNeverStarted(Task<bool> admitted, Func<bool> started, Action abandoned)
    {
        if (admitted.IsCompleted)
        {
            if (!started())
            {
                abandoned();
            }
            return;
        }
        admitted.ContinueWith(
            _ => _saves.Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Normal,
                new Action(() =>
                {
                    if (!started())
                    {
                        abandoned();
                    }
                })),
            CancellationToken.None,
            TaskContinuationOptions.None,
            TaskScheduler.Default);
    }

    private void ReleasePropertyWriteLease(string path) =>
        _ = _propertyWritePaths.Remove(path);

    /// <summary>A row write that writes nothing: the lease and the row's
    /// in-flight flag go before anything is spoken.</summary>
    private void EndRowWrite(string path, PropertyRowViewModel row)
    {
        ReleasePropertyWriteLease(path);
        row.WriteInFlight = false;
    }

    /// <summary>The header of the first open tab on <paramref name="path"/>
    /// that shows it — every same-path header reads the same bytes.</summary>
    private NotePropertiesViewModel? HeaderFor(string path) =>
        Groups.SelectMany(group => group.Tabs)
            .Where(tab => string.Equals(tab.Path, path, StringComparison.Ordinal))
            .Select(tab => tab.Properties)
            .FirstOrDefault(header => header is not null
                && string.Equals(header.Path, path, StringComparison.Ordinal));

    /// <summary>Run <paramref name="next"/> with the wait's outcome on the
    /// dispatcher the save chain runs on: in this turn when the wait is
    /// already over (an idle chain), else posted there when it ends.</summary>
    private void AfterHeaderWait(
        Task<HeaderRefreshOutcome> wait, Action<HeaderRefreshOutcome> next, Action<Exception> fault)
    {
        void Run()
        {
            try
            {
                next(wait.Result);
            }
            catch (Exception exception)
            {
                fault(exception);
            }
        }
        if (wait.IsCompleted)
        {
            Run();
            return;
        }
        wait.ContinueWith(
            _ => _saves.Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Normal,
                new Action(Run)),
            CancellationToken.None,
            TaskContinuationOptions.None,
            TaskScheduler.Default);
    }

    /// <summary>A write completion that ends the operation once it has run
    /// (published, or its failure spoken).</summary>
    private static Action<SaveReport?, VaultException?, string?> EndingWith(
        Action<SaveReport?, VaultException?, string?> completion, TaskCompletionSource done) =>
        (report, failure, postFailureDiskHash) =>
        {
            try
            {
                completion(report, failure, postFailureDiskHash);
            }
            catch (Exception exception)
            {
                _ = done.TrySetException(exception);
                return;
            }
            _ = done.TrySetResult();
        };

    /// <summary>The admitted row write (set or delete). A first write waits
    /// for the header; a Keep Mine retry (<paramref name="freshHash"/>)
    /// does not — admitted behind the save, its worker-side disk read
    /// already sees the saved bytes.</summary>
    private Task RowWriteAdmitted(
        string path,
        PropertyRowViewModel row,
        bool deleted,
        PropertyDraft dispatched,
        Func<string?, SaveReport> write,
        bool freshHash)
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            if (_saves.IsClosed || FindTabForPath(path) is not { IsMarkdown: true } tab)
            {
                EndRowWrite(path, row);
                done.TrySetResult();
                return done.Task;
            }
            if (tab.IsDirty)
            {
                // A taken-back edit is an unsaved change once the save it
                // waited for published.
                EndRowWrite(path, row);
                AnnounceDirtyTabPropertyRefusal(tab);
                done.TrySetResult();
                return done.Task;
            }
            if (freshHash)
            {
                StartRowWrite(tab, path, row, row, deleted, dispatched, write, expectedHash: null, done);
                return done.Task;
            }
            if (row.Owner is not { } owner)
            {
                EndRowWrite(path, row);
                done.TrySetResult();
                return done.Task;
            }
            AfterHeaderWait(
                owner.WhenPublished(path, tab.SavedContentHash),
                outcome => RowWriteAfterHeaderWait(path, row, owner, deleted, dispatched, write, outcome, done),
                exception =>
                {
                    EndRowWrite(path, row);
                    _ = done.TrySetException(exception);
                });
        }
        catch (Exception exception)
        {
            EndRowWrite(path, row);
            _ = done.TrySetException(exception);
        }
        return done.Task;
    }

    private void RowWriteAfterHeaderWait(
        string path,
        PropertyRowViewModel row,
        NotePropertiesViewModel owner,
        bool deleted,
        PropertyDraft dispatched,
        Func<string?, SaveReport> write,
        HeaderRefreshOutcome outcome,
        TaskCompletionSource done)
    {
        if (_saves.IsClosed
            || outcome == HeaderRefreshOutcome.Abandoned
            || FindTabForPath(path) is not { IsMarkdown: true } tab)
        {
            // The header and the draft in it are gone: nothing to say (the
            // row seam's "finds no tab and does nothing" posture).
            EndRowWrite(path, row);
            done.TrySetResult();
            return;
        }
        if (outcome == HeaderRefreshOutcome.Failed)
        {
            EndRowWrite(path, row);
            _announce(new A11yEvent.PropertyEditFailed(owner.LoadError ?? "unknown failure"));
            done.TrySetResult();
            return;
        }
        // The held gates on the fresh header: the row it rebuilt for the
        // edited key (carrying the reader's draft, parked by key), its hash
        // the tab's saved hash, and its committed value the one the edit
        // was made against.
        PropertyRowViewModel? fresh = owner.Rows.FirstOrDefault(candidate =>
            string.Equals(candidate.KeyIdentity, row.KeyIdentity, StringComparison.Ordinal));
        bool writes = fresh is not null
            && !tab.IsDirty
            && !tab.IsExternallyStale
            && string.Equals(fresh.ContentHash, tab.SavedContentHash, StringComparison.Ordinal)
            && PropertyDraft.ValueEquals(fresh.CommittedBaseline, row.CommittedBaseline);
        if (!writes)
        {
            EndRowWrite(path, row);
            if (fresh is null)
            {
                // The saved bytes removed the key: the committed-from row's
                // pre-save hash fails the hash gate.
                if (!RefuseHeldPropertyWrite(tab, row, deleted))
                {
                    AnnouncePropertyConflict(tab, row, deleted);
                }
            }
            else if (!RefuseHeldPropertyWrite(tab, fresh, deleted))
            {
                // The saved bytes changed the edited key's value.
                AnnouncePropertyConflict(tab, fresh, deleted);
            }
            done.TrySetResult();
            return;
        }
        StartRowWrite(tab, path, row, fresh!, deleted, dispatched, write, fresh!.ContentHash, done);
    }

    /// <summary>Dispatch the write on <paramref name="target"/>, the row the
    /// header shows now; the committed-from row is no longer in flight.</summary>
    private void StartRowWrite(
        WorkspaceTabViewModel tab,
        string path,
        PropertyRowViewModel committedFrom,
        PropertyRowViewModel target,
        bool deleted,
        PropertyDraft dispatched,
        Func<string?, SaveReport> write,
        string? expectedHash,
        TaskCompletionSource done)
    {
        committedFrom.WriteInFlight = false;
        target.WriteInFlight = true;
        Task worker = tab.WriteProperty(
            expectedHash,
            write,
            WithLeaseRelease(path, EndingWith(PropertyWriteCompletion(path, target, deleted, dispatched), done)));
        _saves.TrackWorker(worker);
        EndPropertyWriteIfTheWorkerFaults(worker, path, done, () => target.WriteInFlight = false);
    }

    /// <summary>Plan 47 CB-9b (#1955's CB-9a item 1): the backstop of a
    /// property write's worker. The worker catches every fault but out of
    /// memory, stack overflow and access violation; one of those posts no
    /// completion, so nothing above would end the operation, release the
    /// note's write lease or clear the write's in-flight state. On the save
    /// dispatcher, and only while the operation has not ended, the lease is
    /// released, <paramref name="notLanded"/> runs, and the operation ends
    /// carrying the fault (its ticket logs it once). Nothing is spoken, as
    /// nothing was for it before.</summary>
    private void EndPropertyWriteIfTheWorkerFaults(
        Task worker, string path, TaskCompletionSource done, Action notLanded) =>
        _ = worker.ContinueWith(
            faulted =>
            {
                Exception fault = faulted.Exception!.GetBaseException();
                RunOnSaveDispatcher(() =>
                {
                    if (done.Task.IsCompleted)
                    {
                        return;
                    }
                    ReleasePropertyWriteLease(path);
                    try
                    {
                        notLanded();
                    }
                    catch (Exception exception)
                    {
                        HostLog.Write(HostDiagnosticEvent.VaultCommandFailed, exception);
                    }
                    finally
                    {
                        _ = done.TrySetException(fault);
                    }
                });
            },
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);

    /// <summary>The admitted add. A first add waits for a header and adds on
    /// its fresh hash; a Keep Mine retry (<paramref name="freshHash"/>)
    /// writes on the disk hash its worker reads.</summary>
    private Task AddAdmitted(
        string path, AddPropertyViewModel? sheet, string key, PropertyValue value, bool freshHash)
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void EndUnwritten(Action? speak = null)
        {
            ReleasePropertyWriteLease(path);
            speak?.Invoke();
            done.TrySetResult();
        }
        try
        {
            if (_saves.IsClosed || FindTabForPath(path) is not { IsMarkdown: true } tab)
            {
                EndUnwritten(() => sheet?.MarkAddFailed());
                return done.Task;
            }
            if (tab.IsDirty)
            {
                EndUnwritten(() =>
                {
                    AnnounceDirtyTabPropertyRefusal(tab);
                    sheet?.MarkAddFailed();
                });
                return done.Task;
            }
            if (freshHash)
            {
                Task freshWorker = tab.WriteProperty(
                    null,
                    _ =>
                    {
                        string fresh = _session.ReadNoteParts(path).ContentHash;
                        return _session.SetProperty(path, key, value, fresh);
                    },
                    WithLeaseRelease(path, EndingWith(AddWriteCompletion(path, sheet, key, value), done)));
                _saves.TrackWorker(freshWorker);
                EndPropertyWriteIfTheWorkerFaults(freshWorker, path, done, () => sheet?.MarkAddFailed());
                return done.Task;
            }
            if (HeaderFor(path) is not { } header)
            {
                EndUnwritten(() => sheet?.MarkAddFailed());
                return done.Task;
            }
            AfterHeaderWait(
                header.WhenPublished(path, tab.SavedContentHash),
                outcome =>
                {
                    if (_saves.IsClosed
                        || outcome == HeaderRefreshOutcome.Abandoned
                        || FindTabForPath(path) is not { IsMarkdown: true } live)
                    {
                        EndUnwritten(() => sheet?.MarkAddFailed());
                        return;
                    }
                    if (outcome == HeaderRefreshOutcome.Failed)
                    {
                        EndUnwritten(() =>
                        {
                            _announce(new A11yEvent.PropertyEditFailed(header.LoadError ?? "unknown failure"));
                            sheet?.MarkAddFailed();
                        });
                        return;
                    }
                    if (live.IsDirty)
                    {
                        EndUnwritten(() =>
                        {
                            AnnounceDirtyTabPropertyRefusal(live);
                            sheet?.MarkAddFailed();
                        });
                        return;
                    }
                    // The add's gates against the fresh header: its hash the
                    // tab's saved hash, and the key not added by the saved
                    // bytes (ordinal, the sheet's own collision rule).
                    if (live.IsExternallyStale
                        || !string.Equals(live.SavedContentHash, header.ContentHash, StringComparison.Ordinal)
                        || header.TopLevelKeys.Contains(key, StringComparer.Ordinal))
                    {
                        EndUnwritten(() => AnnounceAddConflict(path, sheet, key, value));
                        return;
                    }
                    string expected = header.ContentHash;
                    Task worker = live.WriteProperty(
                        expected,
                        expectedHash => _session.SetProperty(path, key, value, expectedHash),
                        WithLeaseRelease(path, EndingWith(AddWriteCompletion(path, sheet, key, value), done)));
                    _saves.TrackWorker(worker);
                    EndPropertyWriteIfTheWorkerFaults(worker, path, done, () => sheet?.MarkAddFailed());
                },
                exception =>
                {
                    ReleasePropertyWriteLease(path);
                    sheet?.MarkAddFailed();
                    _ = done.TrySetException(exception);
                });
        }
        catch (Exception exception)
        {
            // The commit was accepted, so its sheet says the add did not land.
            ReleasePropertyWriteLease(path);
            sheet?.MarkAddFailed();
            _ = done.TrySetException(exception);
        }
        return done.Task;
    }
}
