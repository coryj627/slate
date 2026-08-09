// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Bases;
using uniffi.slate_uniffi;

namespace SlateWindows;

/// <summary>
/// W4-6 (#738) phase C: the Bases cell-write coordinator — the mac
/// basesApplyProperty funnel's twin, with the Windows route split
/// (contract C8, divergence D-14):
///
/// - target file open in a CLEAN markdown tab → the W4-4 tab write
///   seam (mutation lease bracket, CAS on the tab's saved hash,
///   rebaseline) — the tab's promise that its buffer is never
///   clobbered extends to Bases writes;
/// - open in a DIRTY tab → refuse with the W4-4 sentence (mac writes
///   regardless; recorded divergence D-14);
/// - no open tab → direct session write with NO expected hash (the
///   W4-3 NoOpenTab precedent, matching mac's nil hash).
///
/// One post-write funnel (contract C9): every registered Bases
/// document re-executes; membership changes announce deduped
/// BasesRefreshUpdated; the WRITING document's publish carries the
/// terminal cell outcome (Saved/Cleared/RowNoLongerMatches), exactly
/// one sentence per write.
/// </summary>
internal sealed partial class WorkspaceViewModel
{
    /// <summary>The active tab's Bases document, or null — every
    /// slate.bases.* command gates on this (contract C15); menu items
    /// disable through the shared CanExecute. Keyed on the ATTACHED
    /// document, not the tab kind: saved-query tabs carry a Bases
    /// document too (the mac activeBaseDocument serves both sources;
    /// red team round 1 found the IsBase-only gate left every command
    /// dead on saved-query tabs).</summary>
    internal BaseDocumentViewModel? ActiveBaseDocument =>
        ActiveGroup.ActiveTab?.Base;

    private RelayCommand? _basesNewQueryCommand;
    private RelayCommand? _basesEditViewFiltersCommand;
    private RelayCommand? _basesRefreshCommand;
    private RelayCommand? _basesWhereAmICommand;
    private RelayCommand? _basesResultsCommand;
    private RelayCommand? _basesNextViewCommand;
    private RelayCommand? _basesPreviousViewCommand;
    private RelayCommand? _basesViewSwitcherCommand;
    private RelayCommand? _basesViewAsTableCommand;
    private RelayCommand? _basesViewAsListCommand;
    private RelayCommand? _basesQuickFilterCommand;
    private RelayCommand? _basesSaveSortToViewCommand;
    private RelayCommand? _basesSortByColumnCommand;
    private RelayCommand? _basesOpenRowCommand;
    private RelayCommand? _basesCopyLinkCommand;
    private RelayCommand? _basesShowBacklinksCommand;
    private RelayCommand? _basesEditPropertyCommand;
    private RelayCommand? _basesExportCsvCommand;
    private RelayCommand? _basesExportMarkdownCommand;
    private RelayCommand? _basesCopyMarkdownCommand;

    private BaseQueryBuilderViewModel? _queryBuilder;

    /// <summary>The builder overlay's model (contract C11); null =
    /// closed. Opened by newQuery / editViewFilters / the queries
    /// leaf's Edit in Builder.</summary>
    public BaseQueryBuilderViewModel? BaseQueryBuilderSheet
    {
        get => _queryBuilder;
        private set
        {
            _queryBuilder?.Shutdown();
            SetField(ref _queryBuilder, value);
        }
    }

    public System.Windows.Input.ICommand BasesNewQueryCommand =>
        _basesNewQueryCommand ??= new RelayCommand(
            _ =>
            {
                try
                {
                    BaseQueryBuilderSheet = BaseQueryBuilderViewModel.NewQuery(
                        _session, _announce,
                        synchronousForTests: !_startInteractionBackgroundWork);
                }
                catch (VaultException failure)
                {
                    // The core seed (OpenDql) refused — an unhandled
                    // dispatcher exception otherwise (red team round 1).
                    _announce(new A11yEvent.BasesFiltersOpenFailed(failure.Message));
                    return;
                }
                _announce(new A11yEvent.BasesNewQueryBuilder());
            },
            _ => true);

    public System.Windows.Input.ICommand BasesEditViewFiltersCommand =>
        _basesEditViewFiltersCommand ??= BasesCommand(document =>
            // The edit-JSON fetch shares the FFI lock with executes,
            // so it runs off the dispatcher and the overlay opens in
            // the continuation (INV-6; red team round 1).
            document.ViewEditQueryJson((json, failure) =>
            {
                if (json is null)
                {
                    _announce(new A11yEvent.BasesFiltersOpenFailed(
                        failure ?? "The base is not open."));
                    return;
                }
                try
                {
                    BaseQueryBuilderSheet = BaseQueryBuilderViewModel.ForView(
                        _session, json, document.Path, document.ActiveViewIndex,
                        _announce,
                        synchronousForTests: !_startInteractionBackgroundWork);
                }
                catch (Exception openFailure) when (openFailure
                    is VaultException or InvalidOperationException
                        or InvalidCastException or System.Text.Json.JsonException)
                {
                    _announce(new A11yEvent.BasesFiltersOpenFailed(
                        openFailure.Message));
                    return;
                }
                _announce(new A11yEvent.BasesEditingFilters(
                    document.ActiveViewName ?? document.DisplayName));
            }));

    internal void EditSavedQueryInBuilder(string id)
    {
        SavedQuery savedQuery;
        try
        {
            savedQuery = _session.GetSavedQuery(id);
            BaseQueryBuilderSheet = BaseQueryBuilderViewModel.ForSavedQuery(
                _session, savedQuery, _announce,
                synchronousForTests: !_startInteractionBackgroundWork);
        }
        catch (Exception failure) when (failure
            is VaultException or InvalidCastException or System.Text.Json.JsonException)
        {
            // The cast/parse arms: a corrupt stored QueryJson must
            // refuse, not crash the dispatcher (red team round 1).
            _announce(new A11yEvent.BasesSavedQueryEditFailed(failure.Message));
            return;
        }
        _announce(new A11yEvent.BasesSavedQueryEditing(savedQuery.Name));
    }

    internal void CloseQueryBuilder() => BaseQueryBuilderSheet = null;

    internal void BuilderSaveToView()
    {
        if (BaseQueryBuilderSheet is not { } builder
            || builder.Context.ViewPath is not { } viewPath)
        {
            return;
        }
        BaseDocumentViewModel target = BaseDocumentFor(viewPath);
        ExpectSelfBaseWriteEcho(viewPath);
        builder.SaveToView(target, saved =>
        {
            if (saved)
            {
                RefreshBaseQueries();
            }
            // A save with NO tab on the source must not leave an
            // invisible registry document riding every refresh and
            // announcing membership changes (red team round 2).
            ReleaseUnreferencedBaseDocuments();
        });
    }

    internal void BuilderUpdateSavedQuery()
    {
        if (BaseQueryBuilderSheet is { } builder && builder.UpdateSavedQuery())
        {
            RefreshBaseQueries();
            foreach (BaseDocumentViewModel document in _baseDocuments.Values
                .Where(candidate => string.Equals(
                    candidate.SavedQueryId,
                    builder.Context.SavedQueryId,
                    StringComparison.Ordinal)))
            {
                document.Load();
            }
            // The dock's saved-query document lives outside the tab
            // registry (red team round 1: it kept executing the
            // superseded query).
            if (BasesDockDocument is { } dockDocument
                && string.Equals(
                    dockDocument.SavedQueryId,
                    builder.Context.SavedQueryId,
                    StringComparison.Ordinal))
            {
                dockDocument.Load();
            }
        }
    }

    public System.Windows.Input.ICommand BasesRefreshCommand =>
        _basesRefreshCommand ??= BasesCommand(document =>
        {
            bool wasFailed = document.State == BaseLoadState.Failed;
            document.Load();
            if (!wasFailed)
            {
                document.AnnounceRefreshed();
            }
        });

    public System.Windows.Input.ICommand BasesWhereAmICommand =>
        _basesWhereAmICommand ??= BasesCommand(document =>
            document.AnnounceEvent(document.WhereAmIEvent()));

    public System.Windows.Input.ICommand BasesResultsCommand =>
        _basesResultsCommand ??= BasesCommand(document =>
        {
            if (document.ResultsPopoverEvent() is { } popover)
            {
                document.AnnounceEvent(popover);
            }
        });

    public System.Windows.Input.ICommand BasesNextViewCommand =>
        _basesNextViewCommand ??= BasesCommand(document =>
            BasesStepView(document, +1));

    public System.Windows.Input.ICommand BasesPreviousViewCommand =>
        _basesPreviousViewCommand ??= BasesCommand(document =>
            BasesStepView(document, -1));

    public System.Windows.Input.ICommand BasesViewSwitcherCommand =>
        _basesViewSwitcherCommand ??= BasesCommand(document =>
            document.AnnounceEvent(
                new A11yEvent.BaseViewSwitcher((uint)document.Views.Count)));

    public System.Windows.Input.ICommand BasesViewAsTableCommand =>
        _basesViewAsTableCommand ??= BasesCommand(document =>
        {
            document.RequestRendererOverride(BaseRendererOverride.Table);
            document.AnnounceEvent(new A11yEvent.BaseViewMode("table"));
        });

    public System.Windows.Input.ICommand BasesViewAsListCommand =>
        _basesViewAsListCommand ??= BasesCommand(document =>
        {
            document.RequestRendererOverride(BaseRendererOverride.List);
            document.AnnounceEvent(new A11yEvent.BaseViewMode("list"));
        });

    public System.Windows.Input.ICommand BasesQuickFilterCommand =>
        _basesQuickFilterCommand ??= BasesCommand(document =>
            document.RequestQuickFilterFocus());

    public System.Windows.Input.ICommand BasesSaveSortToViewCommand =>
        _basesSaveSortToViewCommand ??= BasesCommand(document =>
        {
            if (!document.IsSavedQuery)
            {
                // The save writes the .base file; the watcher echo
                // must not force a second destructive reload (red
                // team round 2). A failed save's entry expires.
                ExpectSelfBaseWriteEcho(document.Path);
            }
            document.SaveSortToView();
        });

    public System.Windows.Input.ICommand BasesSortByColumnCommand =>
        _basesSortByColumnCommand ??= BasesCommand(document =>
            document.RequestSortCurrentColumn());

    public System.Windows.Input.ICommand BasesOpenRowCommand =>
        _basesOpenRowCommand ??= BasesRowCommand(BasesOpenRow);

    public System.Windows.Input.ICommand BasesCopyLinkCommand =>
        _basesCopyLinkCommand ??= BasesRowCommand((document, row) =>
        {
            string link = BaseWikilink(row.FilePath);
            try
            {
                System.Windows.Clipboard.SetText(link);
            }
            catch (System.Runtime.InteropServices.ExternalException)
            {
                // The clipboard is a shared OS resource another
                // process can hold; a failed copy speaks nothing
                // rather than lying that it copied.
                return;
            }
            document.AnnounceEvent(new A11yEvent.BasesLinkCopied(
                DisplayNameWithoutExtension(row.FilePath)));
        });

    public System.Windows.Input.ICommand BasesShowBacklinksCommand =>
        _basesShowBacklinksCommand ??= BasesRowCommand((document, row) =>
        {
            OpenPath(row.FilePath);
            ActiveLeaf = Leaves.Single(leaf =>
                string.Equals(leaf.Id, "backlinks", StringComparison.Ordinal));
            IsRightPaneVisible = true;
            document.AnnounceEvent(new A11yEvent.BasesBacklinksFor(
                DisplayNameWithoutExtension(row.FilePath)));
        });

    public System.Windows.Input.ICommand BasesEditPropertyCommand =>
        _basesEditPropertyCommand ??= BasesCommand(document =>
            document.RequestEditSelectedProperty());

    public System.Windows.Input.ICommand BasesExportCsvCommand =>
        _basesExportCsvCommand ??= BasesCommand(document =>
            BasesDeliverExport(document, ExportFormat.Csv));

    public System.Windows.Input.ICommand BasesExportMarkdownCommand =>
        _basesExportMarkdownCommand ??= BasesCommand(document =>
            BasesDeliverExport(document, ExportFormat.Markdown));

    public System.Windows.Input.ICommand BasesCopyMarkdownCommand =>
        _basesCopyMarkdownCommand ??= BasesCommand(document =>
        {
            if (document.State is BaseLoadState.Failed or BaseLoadState.Loading)
            {
                // C13's dispatch backstop (red team round 2: the whole
                // prompt flow ran and then silently did nothing).
                document.AnnounceEvent(new A11yEvent.BasesViewCopyFailed(
                    "the base is not ready"));
                return;
            }
            if (!BasesResolveExportScope(document, "Copy", out bool includeFilter))
            {
                return;
            }
            document.ExportText(ExportFormat.Markdown, includeFilter, text =>
            {
                if (text is null)
                {
                    return;
                }
                try
                {
                    System.Windows.Clipboard.SetText(text);
                }
                catch (System.Runtime.InteropServices.ExternalException failure)
                {
                    document.AnnounceEvent(
                        new A11yEvent.BasesViewCopyFailed(failure.Message));
                    return;
                }
                document.AnnounceEvent(new A11yEvent.BasesViewCopiedAsMarkdown());
            });
        });

    private RelayCommand BasesCommand(Action<BaseDocumentViewModel> body) =>
        new(
            _ =>
            {
                if (ActiveBaseDocument is { } document)
                {
                    body(document);
                }
            },
            _ => ActiveBaseDocument is not null);

    private RelayCommand BasesRowCommand(
        Action<BaseDocumentViewModel, BasesRow> body) =>
        new(
            _ =>
            {
                if (ActiveBaseDocument is not { } document)
                {
                    return;
                }
                if (document.SelectedRow is not { } row)
                {
                    document.AnnounceEvent(new A11yEvent.BasesRowSelectionNeeded());
                    return;
                }
                body(document, row);
            },
            _ => ActiveBaseDocument is not null);

    private void BasesStepView(BaseDocumentViewModel document, int delta)
    {
        int target = Math.Clamp(
            document.ActiveViewIndex + delta,
            0,
            Math.Max(0, document.Views.Count - 1));
        if (target == document.ActiveViewIndex)
        {
            return;
        }
        int before = document.ActiveViewIndex;
        document.SelectView(target);
        // Announce only a switch that actually happened — SelectView
        // refuses silently while loading/failed (red team round 1:
        // the command spoke the unchanged view name).
        if (document.ActiveViewIndex != before
            && document.ActiveViewName is { } name)
        {
            document.AnnounceViewSelected(name);
        }
    }

    internal void BasesOpenRow(BaseDocumentViewModel document, BasesRow row)
    {
        OpenPath(row.FilePath);
        // Task rows open their FILE here; mac lands on the task's line
        // via its task artifact. The announcement stays honest about
        // what happened (OpenedFile, not OpenedAtLine).
        document.AnnounceEvent(new A11yEvent.OpenedFile(
            System.IO.Path.GetFileName(row.FilePath)));
    }

    /// <summary>Mac's baseWikilink verbatim: the path without its
    /// extension, wrapped.</summary>
    internal static string BaseWikilink(string path)
    {
        int dot = path.LastIndexOf('.');
        int slash = path.LastIndexOf('/');
        string target = dot > slash ? path[..dot] : path;
        return "[[" + target + "]]";
    }

    private static string DisplayNameWithoutExtension(string path) =>
        System.IO.Path.GetFileNameWithoutExtension(path);

    /// <summary>The C14 scope choice for export/copy while a quick
    /// filter is active: filtered rows, all rows, or cancel.</summary>
    internal enum BasesExportScope
    {
        Filtered,
        All,
        Cancel,
    }

    /// <summary>Injectable scope prompt (the W4-4 dialog-seam
    /// pattern): production shows a modal choice; facts inject. The
    /// argument is the verb ("Export"/"Copy") for the dialog copy and
    /// the cancel announcement.</summary>
    internal Func<string, BasesExportScope> BasesExportScopePrompt { get; set; } =
        verb =>
        {
            System.Windows.MessageBoxResult choice = System.Windows.MessageBox.Show(
                "A quick filter is active. "
                + verb + " only the filtered rows?\n\n"
                + "Yes: the filtered rows shown now.\n"
                + "No: every row in the view.",
                "Slate",
                System.Windows.MessageBoxButton.YesNoCancel,
                System.Windows.MessageBoxImage.Question);
            return choice switch
            {
                System.Windows.MessageBoxResult.Yes => BasesExportScope.Filtered,
                System.Windows.MessageBoxResult.No => BasesExportScope.All,
                _ => BasesExportScope.Cancel,
            };
        };

    /// <summary>C14: with an active quick filter, export/copy must ASK
    /// filtered-vs-all — never silently emit the filtered subset (red
    /// team round 1: the prompt was absent and a filtered export
    /// shipped 3 of 500 rows unasked). Cancel announces the canonical
    /// event. No active filter → no prompt, full view.</summary>
    private bool BasesResolveExportScope(
        BaseDocumentViewModel document, string verb, out bool includeFilter)
    {
        includeFilter = true;
        if (!document.QuickFilterActive)
        {
            return true;
        }
        switch (BasesExportScopePrompt(verb))
        {
            case BasesExportScope.Filtered:
                return true;
            case BasesExportScope.All:
                includeFilter = false;
                return true;
            default:
                document.AnnounceEvent(
                    new A11yEvent.BasesQuickFilterChoiceCanceled(verb));
                return false;
        }
    }

    /// <summary>Export delivery (contract C14): the scope choice, a
    /// save panel, then core's bytes composed OFF the dispatcher;
    /// success/failure announce the canonical events.</summary>
    private void BasesDeliverExport(
        BaseDocumentViewModel document, ExportFormat format)
    {
        if (document.State is BaseLoadState.Failed or BaseLoadState.Loading)
        {
            // C13's dispatch backstop (red team round 2: the prompt
            // and the save dialog both ran before the silent no-op).
            document.AnnounceEvent(new A11yEvent.BasesViewExportFailed(
                "the base is not ready"));
            return;
        }
        if (!BasesResolveExportScope(document, "Export", out bool includeFilter))
        {
            return;
        }
        string extension = format == ExportFormat.Csv ? "csv" : "md";
        string viewName = document.ActiveViewName ?? "View";
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            FileName = document.DisplayName + " - " + viewName + "." + extension,
            DefaultExt = extension,
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }
        string targetPath = dialog.FileName;
        document.ExportText(format, includeFilter, text =>
        {
            if (text is null)
            {
                // The compose failure already announced.
                return;
            }
            try
            {
                System.IO.File.WriteAllText(targetPath, text);
            }
            catch (Exception failure) when (failure is System.IO.IOException
                or UnauthorizedAccessException)
            {
                document.AnnounceEvent(
                    new A11yEvent.BasesViewExportFailed(failure.Message));
                return;
            }
            document.AnnounceEvent(new A11yEvent.BasesViewExported());
        });
    }

    internal void RaiseBasesCommandStates()
    {
        foreach (RelayCommand? command in new[]
        {
            _basesNewQueryCommand, _basesEditViewFiltersCommand,
            _basesRefreshCommand, _basesWhereAmICommand, _basesResultsCommand,
            _basesNextViewCommand, _basesPreviousViewCommand,
            _basesViewSwitcherCommand, _basesViewAsTableCommand,
            _basesViewAsListCommand, _basesQuickFilterCommand,
            _basesSaveSortToViewCommand, _basesSortByColumnCommand,
            _basesOpenRowCommand, _basesCopyLinkCommand,
            _basesShowBacklinksCommand, _basesEditPropertyCommand,
            _basesExportCsvCommand, _basesExportMarkdownCommand,
            _basesCopyMarkdownCommand,
        })
        {
            command?.RaiseCanExecuteChanged();
        }
    }

    private int _basesWriteFunnelId;
    private readonly HashSet<string> _basesFunnelAnnouncedSummaries = new(StringComparer.Ordinal);

    private readonly Dictionary<string, DashboardViewModel> _dashboardDocuments =
        new(StringComparer.Ordinal);

    internal DashboardViewModel DashboardFor(string id, string name)
    {
        if (!_dashboardDocuments.TryGetValue(id, out DashboardViewModel? document))
        {
            document = new DashboardViewModel(
                _session, id, name, _announce,
                synchronousForTests: !_startInteractionBackgroundWork);
            _dashboardDocuments[id] = document;
            document.Load();
        }
        return document;
    }

    private void ReleaseUnreferencedDashboards()
    {
        if (_dashboardDocuments.Count == 0)
        {
            return;
        }
        var live = new HashSet<string>(StringComparer.Ordinal);
        foreach (WorkspaceTabViewModel tab in Groups.SelectMany(group => group.Tabs))
        {
            if (tab.IsDashboardTab && tab.Item.Id is { Length: > 0 } id)
            {
                live.Add(id);
            }
        }
        if (BasesDockTarget is { Kind: BasesDockTargetKind.Dashboard } dockTarget)
        {
            live.Add(dockTarget.Key);
        }
        foreach (string id in _dashboardDocuments.Keys.Where(k => !live.Contains(k)).ToList())
        {
            _dashboardDocuments[id].Shutdown();
            _dashboardDocuments.Remove(id);
        }
    }

    internal void OpenDashboard(string id, string name) =>
        OpenItem(
            new WorkspaceItemState(WorkspaceItemKind.Dashboard, string.Empty, id, name),
            WorkspaceOpenTarget.NewTab);

    internal void DeleteDashboard(string id)
    {
        try
        {
            _session.DeleteDashboard(id);
        }
        catch (VaultException failure)
        {
            _announce(new A11yEvent.BasesDashboardDeleteFailed(failure.Message));
            return;
        }
        foreach (WorkspaceGroupViewModel group in Groups)
        {
            foreach (WorkspaceTabViewModel tab in group.Tabs.Where(t =>
                t.IsDashboardTab
                && string.Equals(t.Item.Id, id, StringComparison.Ordinal)).ToList())
            {
                CloseTab(tab);
            }
        }
        if (BasesDockTarget is { Kind: BasesDockTargetKind.Dashboard } target
            && string.Equals(target.Key, id, StringComparison.Ordinal))
        {
            ClearBasesDock();
        }
        _announce(new A11yEvent.BasesDashboardDeleted());
        RefreshBaseQueries();
    }

    // --- The dashboard editor overlay (contract C12; W4-5 D-1:
    // in-window overlay, never a Popup). Missing sections repair
    // through THIS editor (divergence D-19: mac also has inline
    // per-section repair actions). ---

    private DashboardEditorViewModel? _dashboardEditor;

    public DashboardEditorViewModel? DashboardEditorSheet
    {
        get => _dashboardEditor;
        private set => SetField(ref _dashboardEditor, value);
    }

    internal void OpenDashboardEditor(string? dashboardId)
    {
        var editor = new DashboardEditorViewModel(
            dashboardId,
            dashboardId is null
                ? string.Empty
                : Dashboards.FirstOrDefault(d =>
                    string.Equals(d.Id, dashboardId, StringComparison.Ordinal))?.Name
                    ?? string.Empty);
        if (dashboardId is not null)
        {
            try
            {
                Dashboard dashboard = _session.GetDashboard(dashboardId);
                editor.OpenedModifiedAtMs = dashboard.ModifiedAtMs;
                // The FRESH registry name, not the cached summary list
                // (the list may predate a rename).
                editor.Name = dashboard.Name;
                foreach (DashboardSectionStatus status in dashboard.Sections)
                {
                    editor.Sections.Add(new DashboardEditorSection(
                        status.SavedQueryId,
                        status.SavedQueryName
                            ?? (status.Missing
                                ? $"Missing: {status.SavedQueryId}"
                                : status.SavedQueryId))
                    {
                        HeadingOverride = status.HeadingOverride ?? string.Empty,
                        ViewOverride = status.ViewOverride ?? string.Empty,
                    });
                }
            }
            catch (VaultException failure)
            {
                _announce(new A11yEvent.BasesDashboardEditFailed(failure.Message));
                return;
            }
        }
        DashboardEditorSheet = editor;
    }

    internal void CloseDashboardEditor() => DashboardEditorSheet = null;

    internal void SaveDashboardEditor()
    {
        if (DashboardEditorSheet is not { } editor)
        {
            return;
        }
        string name = editor.Name.Trim();
        if (name.Length == 0)
        {
            _announce(new A11yEvent.BasesDashboardNameNeeded());
            return;
        }
        DashboardSection[] sections = editor.DraftSections();
        try
        {
            if (editor.DashboardId is { } id)
            {
                // The C12 stale guard: the dashboard changed under the
                // open editor (another surface saved) — refuse with the
                // canonical sentence and reload the editor's sections
                // so the user edits what is actually there.
                Dashboard current = _session.GetDashboard(id);
                if (current.ModifiedAtMs != editor.OpenedModifiedAtMs)
                {
                    _announce(new A11yEvent.BasesDashboardSectionStale());
                    OpenDashboardEditor(id);
                    return;
                }
                _session.UpdateDashboard(id, name, sections);
                _announce(new A11yEvent.BasesDashboardUpdated(name));
                if (_dashboardDocuments.TryGetValue(id, out DashboardViewModel? open))
                {
                    open.Load();
                }
            }
            else
            {
                _ = _session.SaveDashboard(name, sections);
                _announce(new A11yEvent.BasesDashboardSaved(name));
            }
        }
        catch (VaultException failure)
        {
            _announce(editor.DashboardId is null
                ? new A11yEvent.BasesDashboardSaveFailed(failure.Message)
                : new A11yEvent.BasesDashboardUpdateFailed(failure.Message));
            return;
        }
        DashboardEditorSheet = null;
        RefreshBaseQueries();
    }

    // --- The dock (contract C12): one target following the active
    // note, read-only, 500 ms debounce, identity-guarded. ---

    internal BasesDockTargetState? BasesDockTarget
    {
        get => _basesDockTarget;
        private set => SetField(ref _basesDockTarget, value);
    }

    private BasesDockTargetState? _basesDockTarget;
    private BaseDocumentViewModel? _basesDockDocument;
    private DashboardViewModel? _basesDockDashboard;
    private System.Windows.Threading.DispatcherTimer? _dockFollowTimer;

    public BaseDocumentViewModel? BasesDockDocument
    {
        get => _basesDockDocument;
        private set => SetField(ref _basesDockDocument, value);
    }

    public DashboardViewModel? BasesDockDashboard
    {
        get => _basesDockDashboard;
        private set => SetField(ref _basesDockDashboard, value);
    }

    internal void DockBaseFileToSidebar(string path, string name) =>
        SetBasesDockTarget(new BasesDockTargetState(
            BasesDockTargetKind.File, path, name));

    internal void DockSavedQueryToSidebar(string id, string name) =>
        SetBasesDockTarget(new BasesDockTargetState(
            BasesDockTargetKind.SavedQuery, id, name));

    internal void DockDashboardToSidebar(string id, string name) =>
        SetBasesDockTarget(new BasesDockTargetState(
            BasesDockTargetKind.Dashboard, id, name));

    private void SetBasesDockTarget(BasesDockTargetState target)
    {
        ClearBasesDockDocuments();
        BasesDockTarget = target;
        switch (target.Kind)
        {
            case BasesDockTargetKind.File:
                var fileDocument = new BaseDocumentViewModel(
                    _session, target.Key, _announce,
                    synchronousForTests: !_startInteractionBackgroundWork)
                {
                    ThisPath = BasesDockActiveNotePath(),
                };
                // The dock rides the C9 funnel's membership dedup like
                // every other surface (red team round 1: unregistered
                // dock documents never spoke a membership change).
                fileDocument.MembershipChanged += OnBaseMembershipChanged;
                BasesDockDocument = fileDocument;
                fileDocument.Load();
                break;
            case BasesDockTargetKind.SavedQuery:
                BaseDocumentViewModel queryDocument =
                    BaseDocumentViewModel.ForSavedQuery(
                        _session, target.Key, target.Name, _announce,
                        synchronousForTests: !_startInteractionBackgroundWork);
                queryDocument.ThisPath = BasesDockActiveNotePath();
                queryDocument.MembershipChanged += OnBaseMembershipChanged;
                BasesDockDocument = queryDocument;
                queryDocument.Load();
                break;
            case BasesDockTargetKind.Dashboard:
                var dashboard = new DashboardViewModel(
                    _session, target.Key, target.Name, _announce,
                    synchronousForTests: !_startInteractionBackgroundWork);
                BasesDockDashboard = dashboard;
                dashboard.Load();
                break;
        }
        ActiveLeaf = Leaves.Single(leaf =>
            string.Equals(leaf.Id, "basesDock", StringComparison.Ordinal));
        IsRightPaneVisible = true;
        _announce(new A11yEvent.BasesDockUpdatedForNote());
    }

    internal void ClearBasesDock()
    {
        ClearBasesDockDocuments();
        BasesDockTarget = null;
    }

    /// <summary>Rebuild the dock's FILE document at a renamed path —
    /// silent (INV-4: the user did nothing; the target moved).</summary>
    internal void RedockBaseFileSilently(string path)
    {
        if (BasesDockTarget is not { Kind: BasesDockTargetKind.File } target)
        {
            return;
        }
        ClearBasesDockDocuments();
        BasesDockTarget = target with
        {
            Key = path,
            Name = System.IO.Path.GetFileNameWithoutExtension(path),
        };
        var fileDocument = new BaseDocumentViewModel(
            _session, path, _announce,
            synchronousForTests: !_startInteractionBackgroundWork)
        {
            ThisPath = BasesDockActiveNotePath(),
        };
        fileDocument.MembershipChanged += OnBaseMembershipChanged;
        BasesDockDocument = fileDocument;
        fileDocument.Load();
    }

    private void ClearBasesDockDocuments()
    {
        _dockFollowTimer?.Stop();
        BasesDockDocument?.Shutdown();
        BasesDockDocument = null;
        BasesDockDashboard?.Shutdown();
        BasesDockDashboard = null;
    }

    private string? BasesDockActiveNotePath() =>
        ActiveGroup.ActiveTab is { IsMarkdown: true } tab ? tab.Path : null;

    /// <summary>Active-note change → 500 ms debounce → re-execute the
    /// dock with the new this_path (the mac cadence). Synchronous test
    /// mode follows inline so the facts are deterministic.</summary>
    internal void BasesDockFollowActiveNote()
    {
        if (BasesDockDocument is null)
        {
            return;
        }
        if (!_startInteractionBackgroundWork)
        {
            BasesDockFollowBody();
            return;
        }
        _dockFollowTimer?.Stop();
        _dockFollowTimer ??= new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500),
        };
        // Idempotent re-subscribe: detach BEFORE attach (four red-team
        // reports flagged the previous +=/-=/+= ordering, which netted
        // one extra handler per call).
        _dockFollowTimer.Tick -= DockFollowTick;
        _dockFollowTimer.Tick += DockFollowTick;
        _dockFollowTimer.Start();
    }

    private void DockFollowTick(object? sender, EventArgs e)
    {
        _dockFollowTimer?.Stop();
        BasesDockFollowBody();
    }

    private void BasesDockFollowBody()
    {
        if (BasesDockDocument is not { } document)
        {
            return;
        }
        string? thisPath = BasesDockActiveNotePath();
        if (string.Equals(document.ThisPath, thisPath, StringComparison.Ordinal))
        {
            return;
        }
        document.ThisPath = thisPath;
        document.Refresh();
        // Spoken only when the dock actually followed to a NOTE and
        // the pane holding it is visible (red team round 2: switching
        // note ↔ base tabs announced every time, even with the pane
        // collapsed — §2.6 unsolicited content on tab switching).
        if (thisPath is not null && IsRightPaneVisible)
        {
            _announce(new A11yEvent.BasesDockUpdatedForNote());
        }
    }

    /// <summary>ONE attach funnel for every site that gives a tab its
    /// item (AddTab, restore, duplicate, and the in-place REPLACE arm —
    /// the duplicate site's target-typed `new` hid from the first
    /// sweep, and the replace arm from the second; a helper cannot be
    /// skipped).</summary>
    private void AttachBaseDocumentIfNeeded(WorkspaceTabViewModel tab)
    {
        if (tab.IsBase)
        {
            tab.AttachBaseDocument(BaseDocumentFor(tab.Path));
        }
        else if (tab.IsSavedQueryTab && tab.Item.Id is { Length: > 0 } id)
        {
            tab.AttachBaseDocument(
                BaseDocumentForSavedQuery(id, tab.Item.Name ?? "Saved query"));
        }
        else if (tab.IsDashboardTab && tab.Item.Id is { Length: > 0 } dashboardId)
        {
            tab.AttachDashboard(
                DashboardFor(dashboardId, tab.Item.Name ?? "Dashboard"));
        }
    }

    /// <summary>The queries-leaf state (the mac BaseQueriesState twin):
    /// saved queries, base files, dashboards, and the pinned ids
    /// (app-level, persisted). Refreshed on leaf reveal and after every
    /// mutating action; failure announces BasesQueriesRefreshFailed and
    /// keeps the previous lists.</summary>
    public System.Collections.ObjectModel.ObservableCollection<SavedQuerySummary>
        SavedQueries
    { get; } = [];

    public System.Collections.ObjectModel.ObservableCollection<BaseFileSummary>
        BaseFiles
    { get; } = [];

    public System.Collections.ObjectModel.ObservableCollection<DashboardSummary>
        Dashboards
    { get; } = [];

    private readonly HashSet<string> _pinnedSavedQueryIds = new(StringComparer.Ordinal);
    private int _queriesRefreshGeneration;

    internal IReadOnlyCollection<string> PinnedSavedQueryIdsForTests => _pinnedSavedQueryIds;

    internal void RefreshBaseQueries()
    {
        int generation = Interlocked.Increment(ref _queriesRefreshGeneration);
        if (!_startInteractionBackgroundWork)
        {
            RefreshBaseQueriesBody(generation);
            return;
        }
        _ = Task.Run(() => RefreshBaseQueriesBody(generation));
    }

    private void RefreshBaseQueriesBody(int generation)
    {
        SavedQuerySummary[] savedQueries;
        BaseFileSummary[] baseFiles;
        DashboardSummary[] dashboards;
        try
        {
            savedQueries = _session.ListSavedQueries();
            baseFiles = _session.BasesList();
            dashboards = _session.ListDashboards();
        }
        catch (VaultException failure)
        {
            RunOnDispatcher(() =>
            {
                // Same generation gate as success: a stale failure
                // must not speak after a newer refresh landed.
                if (Volatile.Read(ref _queriesRefreshGeneration) == generation)
                {
                    _announce(new A11yEvent.BasesQueriesRefreshFailed(failure.Message));
                }
            });
            return;
        }
        RunOnDispatcher(() =>
        {
            if (Volatile.Read(ref _queriesRefreshGeneration) != generation)
            {
                return;
            }
            // Pinned first in pin order, then case-insensitive name
            // with id tiebreak (the mac ordering).
            SavedQueries.Clear();
            foreach (SavedQuerySummary summary in savedQueries
                .OrderBy(s => _pinnedSavedQueryIds.Contains(s.Id) ? 0 : 1)
                .ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(s => s.Id, StringComparer.Ordinal))
            {
                SavedQueries.Add(summary);
            }
            BaseFiles.Clear();
            foreach (BaseFileSummary file in baseFiles
                .OrderBy(f => f.Path, StringComparer.OrdinalIgnoreCase))
            {
                BaseFiles.Add(file);
            }
            Dashboards.Clear();
            foreach (DashboardSummary dashboard in dashboards
                .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(d => d.Id, StringComparer.Ordinal))
            {
                Dashboards.Add(dashboard);
            }
            // Pins prune to live ids (the mac rule).
            _pinnedSavedQueryIds.RemoveWhere(id =>
                !savedQueries.Any(s => string.Equals(s.Id, id, StringComparison.Ordinal)));
        });
    }

    internal void ToggleSavedQueryPin(string id)
    {
        if (!_pinnedSavedQueryIds.Remove(id))
        {
            _pinnedSavedQueryIds.Add(id);
        }
        RefreshBaseQueries();
    }

    internal void RunSavedQuery(string id)
    {
        SavedQuerySummary? summary = SavedQueries.FirstOrDefault(s =>
            string.Equals(s.Id, id, StringComparison.Ordinal));
        if (summary is null)
        {
            _announce(new A11yEvent.BasesSavedQueryMissing());
            return;
        }
        OpenItem(
            new WorkspaceItemState(
                WorkspaceItemKind.SavedQuery, string.Empty, summary.Id, summary.Name),
            WorkspaceOpenTarget.NewTab);
    }

    internal void RenameSavedQuery(string id, string name)
    {
        string trimmed = name.Trim();
        if (trimmed.Length == 0)
        {
            _announce(new A11yEvent.BasesSavedQueryRenameNameNeeded());
            return;
        }
        try
        {
            _session.RenameSavedQuery(id, trimmed);
        }
        catch (VaultException failure)
        {
            _announce(new A11yEvent.BasesSavedQueryRenameFailed(failure.Message));
            return;
        }
        _announce(new A11yEvent.BasesSavedQueryRenamed(trimmed));
        // Live surfaces retitle with the registry (red team round 1):
        // open saved-query tabs, their shared document, and the dock.
        foreach (WorkspaceTabViewModel tab in Groups
            .SelectMany(group => group.Tabs)
            .Where(candidate => candidate.IsSavedQueryTab
                && string.Equals(candidate.Item.Id, id, StringComparison.Ordinal)))
        {
            tab.RetargetName(trimmed);
        }
        if (_baseDocuments.TryGetValue(
            "query:" + id, out BaseDocumentViewModel? renamedDocument))
        {
            renamedDocument.UpdateSavedQueryName(trimmed);
        }
        if (BasesDockDocument is { } dockDocument
            && string.Equals(dockDocument.SavedQueryId, id, StringComparison.Ordinal))
        {
            dockDocument.UpdateSavedQueryName(trimmed);
        }
        if (BasesDockTarget is { Kind: BasesDockTargetKind.SavedQuery } dockTarget
            && string.Equals(dockTarget.Key, id, StringComparison.Ordinal))
        {
            BasesDockTarget = dockTarget with { Name = trimmed };
        }
        RefreshBaseQueries();
    }

    internal void DeleteSavedQuery(string id)
    {
        try
        {
            _session.DeleteSavedQuery(id);
        }
        catch (VaultException failure)
        {
            _announce(new A11yEvent.BasesSavedQueryDeleteFailed(failure.Message));
            return;
        }
        _pinnedSavedQueryIds.Remove(id);
        // Close open tabs on the deleted query (the mac rule).
        foreach (WorkspaceGroupViewModel group in Groups)
        {
            foreach (WorkspaceTabViewModel tab in group.Tabs.Where(t =>
                t.Item.Kind == WorkspaceItemKind.SavedQuery
                && string.Equals(t.Item.Id, id, StringComparison.Ordinal)).ToList())
            {
                CloseTab(tab);
            }
        }
        _announce(new A11yEvent.BasesSavedQueryDeleted());
        RefreshBaseQueries();
    }

    internal void ExportSavedQueryAsBase(string id, string path)
    {
        string trimmed = path.Trim();
        if (trimmed.Length == 0)
        {
            _announce(new A11yEvent.BasesSavedQueryExportPathNeeded());
            return;
        }
        if (System.IO.Path.IsPathRooted(trimmed)
            || trimmed.Split('/', '\\').Any(segment =>
                string.Equals(segment, "..", StringComparison.Ordinal)))
        {
            // C12's canonical out-of-vault refusal (red team round 1:
            // the raw absolute path fell through to core's InvalidPath
            // and announced the generic export failure instead). The
            // SEGMENT test catches mid-path traversal without falsely
            // refusing names that merely start with dots (round 2).
            _announce(new A11yEvent.BasesPathOutsideVault());
            return;
        }
        try
        {
            _session.ExportSavedQueryAsBase(id, trimmed);
        }
        catch (VaultException failure)
        {
            _announce(new A11yEvent.BasesSavedQueryExportFailed(failure.Message));
            return;
        }
        _announce(new A11yEvent.BasesSavedQueryExported(trimmed));
        RefreshBaseQueries();
    }

    private void InstallBaseDocumentSeams(BaseDocumentViewModel document)
    {
        document.ApplyPropertyEdit = (row, column, value) =>
            BasesApplyPropertyEdit(document, row, column, value);
        document.MembershipChanged += OnBaseMembershipChanged;
        document.OpenRowFromSurface = row => BasesOpenRow(document, row);
        document.CopyLinkFromSurface = row =>
            ((RelayCommand)BasesCopyLinkCommand).Execute(RowOverride(document, row));
        document.ShowBacklinksFromSurface = row =>
        {
            OpenPath(row.FilePath);
            ActiveLeaf = Leaves.Single(leaf =>
                string.Equals(leaf.Id, "backlinks", StringComparison.Ordinal));
            IsRightPaneVisible = true;
            document.AnnounceEvent(new A11yEvent.BasesBacklinksFor(
                DisplayNameWithoutExtension(row.FilePath)));
        };
    }

    private static object RowOverride(BaseDocumentViewModel document, BasesRow row)
    {
        document.SelectedRow = row;
        return row;
    }

    private void OnBaseMembershipChanged(int funnelId, string audioSummary)
    {
        // Deduped on the SUMMARY text within one funnel pass (the mac
        // rendered-text dedup): two surfaces over the same source must
        // not speak the same update twice.
        if (funnelId != _basesWriteFunnelId
            || !_basesFunnelAnnouncedSummaries.Add(audioSummary))
        {
            return;
        }
        _announce(new A11yEvent.BasesRefreshUpdated(audioSummary));
    }

    private void BasesApplyPropertyEdit(
        BaseDocumentViewModel document,
        BasesRow row,
        BasesColumn column,
        PropertyValue? value)
    {
        // Defence in depth (the mac funnel re-checks too): the surface
        // already refused read-only cells, but every route into a
        // write re-verifies at dispatch (contract C13's backstop).
        if (BaseCellEditPolicy.PropertyKey(column) is not { } key)
        {
            _announce(BaseCellEditPolicy.ReadOnlyEvent(column));
            return;
        }
        WorkspaceTabViewModel? tab = Groups
            .SelectMany(group => group.Tabs)
            .FirstOrDefault(candidate =>
                candidate.IsMarkdown
                && string.Equals(candidate.Path, row.FilePath, StringComparison.Ordinal));
        if (tab is { IsDirty: true })
        {
            // D-14: the Windows tab model promises the unsaved buffer
            // is never clobbered — one sentence, the W4-4 site.
            AnnounceDirtyTabPropertyRefusal(tab);
            return;
        }
        if (tab is not null)
        {
            if (tab.IsExternallyStale)
            {
                // The W4-4 stale gate (red team round 1: without it a
                // Bases write raced straight into the CAS and surfaced
                // a raw conflict message instead of the family's
                // refusal). The Bases surface has no per-row draft to
                // retry with — the user refreshes and re-edits.
                _announce(new A11yEvent.PropertyEditConflict(
                    System.IO.Path.GetFileName(tab.Path)));
                return;
            }
            if (!TryAcquirePropertyWriteLease(row.FilePath))
            {
                AnnounceWriteInFlightRefusal();
                return;
            }
            // The W4-4 seam: CAS + rebaseline live in the tab; the
            // note-scoped lease brackets the whole write (same
            // exclusion as the panel rows — one write per path at a
            // time, any origin); the completion routes into the one
            // funnel.
            tab.WriteProperty(
                tab.SavedContentHash,
                expectedHash => value is null
                    ? _session.DeleteProperty(row.FilePath, key, expectedHash)
                    : _session.SetProperty(row.FilePath, key, value, expectedHash),
                WithLeaseRelease(row.FilePath, (report, failure, _postFailureDiskHash) =>
                {
                    if (failure is not null)
                    {
                        _announce(new A11yEvent.BasesCellEditFailed(failure.Message));
                        return;
                    }
                    if (report is not null)
                    {
                        _ = tab.RebaselineAfterPropertyWrite(
                            report.NewContentHash, _announce);
                    }
                    CompleteBasesWrite(document, row, column, value);
                }));
            return;
        }
        // Tabless: direct write, no expected hash (mac parity), but
        // under the same note-scoped lease — two rapid cell edits on
        // one note must serialize, not race last-writer-wins (red
        // team round 1). Off the dispatcher in production; inline in
        // synchronous tests.
        if (!TryAcquirePropertyWriteLease(row.FilePath))
        {
            AnnounceWriteInFlightRefusal();
            return;
        }
        if (!_startInteractionBackgroundWork)
        {
            BasesTablessWriteBody(document, row, column, key, value);
            return;
        }
        _ = Task.Run(() => BasesTablessWriteBody(document, row, column, key, value));
    }

    private void BasesTablessWriteBody(
        BaseDocumentViewModel document,
        BasesRow row,
        BasesColumn column,
        string key,
        PropertyValue? value)
    {
        try
        {
            if (value is null)
            {
                _ = _session.DeleteProperty(row.FilePath, key, expectedContentHash: null);
            }
            else
            {
                _ = _session.SetProperty(row.FilePath, key, value, expectedContentHash: null);
            }
        }
        catch (Exception failure) when (failure
            is not OutOfMemoryException
                and not StackOverflowException
                and not AccessViolationException)
        {
            // Catch-ALL (the W4-4 completion-totality precedent, red
            // team round 2): any worker failure must still release the
            // note-scoped lease — a stranded entry refuses every later
            // property write to this note, panel rows included,
            // forever.
            RunOnDispatcher(() =>
            {
                _ = _propertyWritePaths.Remove(row.FilePath);
                _announce(new A11yEvent.BasesCellEditFailed(failure.Message));
            });
            return;
        }
        RunOnDispatcher(() =>
        {
            // Released on the dispatcher, where it was acquired.
            _ = _propertyWritePaths.Remove(row.FilePath);
            CompleteBasesWrite(document, row, column, value);
        });
    }

    /// <summary>The one post-write funnel (contract C9): bump the
    /// funnel generation, refresh every visible Bases surface —
    /// registered tab documents, the dock, dashboards (red team round
    /// 1: the first cut reached only the tab registry, so the dock
    /// kept stale rows forever) — and let the WRITING document's
    /// publish announce the terminal outcome from its refreshed
    /// rows.</summary>
    private void CompleteBasesWrite(
        BaseDocumentViewModel document,
        BasesRow row,
        BasesColumn column,
        PropertyValue? value)
    {
        int funnelId = ++_basesWriteFunnelId;
        _basesFunnelAnnouncedSummaries.Clear();
        foreach (BaseDocumentViewModel other in _baseDocuments.Values)
        {
            if (!ReferenceEquals(other, document))
            {
                other.RefreshForFunnel(funnelId);
            }
        }
        if (BasesDockDocument is { } dockDocument
            && !ReferenceEquals(dockDocument, document))
        {
            dockDocument.RefreshForFunnel(funnelId);
        }
        foreach (DashboardViewModel dashboard in _dashboardDocuments.Values)
        {
            dashboard.Load();
        }
        BasesDockDashboard?.Load();
        document.RefreshForFunnel(
            funnelId,
            onPublished: () => AnnounceBasesCellOutcome(document, row, column, value));
    }

    private void AnnounceBasesCellOutcome(
        BaseDocumentViewModel document,
        BasesRow row,
        BasesColumn column,
        PropertyValue? value)
    {
        bool stillPresent = document.Result is { } result
            && result.Rows.Any(candidate =>
                string.Equals(candidate.FilePath, row.FilePath, StringComparison.Ordinal)
                && candidate.TaskOrdinal == row.TaskOrdinal);
        if (!stillPresent)
        {
            _announce(new A11yEvent.BasesCellRowNoLongerMatches());
            return;
        }
        _announce(value is null
            ? new A11yEvent.BasesCellCleared(column.Label)
            : new A11yEvent.BasesCellSaved(
                column.Label, BaseCellEditPolicy.DisplayValue(value)));
    }

    private void RunOnDispatcher(Action action)
    {
        if (System.Windows.Application.Current?.Dispatcher is { } dispatcher
            && !dispatcher.CheckAccess())
        {
            _ = dispatcher.InvokeAsync(action);
            return;
        }
        action();
    }

    private int _basesVaultRefreshTicket;

    /// <summary>Changed .base paths ACCUMULATE across a debounce
    /// burst (red team round 2: the last-ticket-wins pattern dropped
    /// earlier events' payloads, so a mixed git-pull burst refreshed
    /// a changed base against its superseded parse). UI-thread only.</summary>
    private readonly HashSet<string> _pendingChangedBasePaths = new(StringComparer.Ordinal);

    /// <summary>Paths whose next .base change is OUR OWN just-landed
    /// save (builder save-to-view, save-sort-to-view): the document
    /// already reloaded its views and re-executed, so the watcher echo
    /// must not force a second destructive Load — that wiped the
    /// transient quick filter and flickered Loading (red team round
    /// 2). Entries expire so a lost event cannot suppress a real
    /// external change forever.</summary>
    private readonly Dictionary<string, DateTime> _selfBaseWriteEchoes =
        new(StringComparer.Ordinal);

    internal void ExpectSelfBaseWriteEcho(string path) =>
        _selfBaseWriteEchoes[path] = DateTime.UtcNow.AddSeconds(5);

    private bool ConsumeSelfBaseWriteEcho(string path)
    {
        if (!_selfBaseWriteEchoes.TryGetValue(path, out DateTime expiry))
        {
            return false;
        }
        _ = _selfBaseWriteEchoes.Remove(path);
        return expiry > DateTime.UtcNow;
    }

    /// <summary>C9's vault-event arm (red team round 1: absent — a
    /// property-panel write, task toggle, editor save, or external
    /// edit never reached any Bases surface): any .md/.base change
    /// re-executes every visible surface after a 500 ms quiet period,
    /// SILENTLY (INV-4/§2.6 — nothing here was user-initiated on a
    /// Bases surface; the in-app cell-write funnel owns
    /// announcements). A changed .base definition reloads its own
    /// document (the open handle holds the superseded parse); .md
    /// membership changes re-execute on the current handle.</summary>
    internal void NotifyBasesOfVaultChange(string path)
    {
        bool isBaseFile = path.EndsWith(".base", StringComparison.OrdinalIgnoreCase);
        if (!isBaseFile && !path.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        if (_baseDocuments.Count == 0
            && _dashboardDocuments.Count == 0
            && BasesDockDocument is null
            && BasesDockDashboard is null)
        {
            return;
        }
        if (isBaseFile && !ConsumeSelfBaseWriteEcho(path))
        {
            _ = _pendingChangedBasePaths.Add(path);
        }
        int ticket = Interlocked.Increment(ref _basesVaultRefreshTicket);
        if (!_startInteractionBackgroundWork)
        {
            RefreshBasesSurfacesForVaultChange();
            return;
        }
        _ = Task.Delay(500).ContinueWith(
            _ => RunOnDispatcher(() =>
            {
                if (ticket == _basesVaultRefreshTicket)
                {
                    RefreshBasesSurfacesForVaultChange();
                }
            }),
            TaskScheduler.Default);
    }

    private void RefreshBasesSurfacesForVaultChange()
    {
        var changedBasePaths = new HashSet<string>(
            _pendingChangedBasePaths, StringComparer.Ordinal);
        _pendingChangedBasePaths.Clear();
        foreach (BaseDocumentViewModel document in _baseDocuments.Values)
        {
            if (changedBasePaths.Contains(document.Path))
            {
                document.Load();
            }
            else
            {
                document.Refresh();
            }
        }
        if (BasesDockDocument is { } dockDocument)
        {
            if (changedBasePaths.Contains(dockDocument.Path))
            {
                dockDocument.Load();
            }
            else
            {
                dockDocument.Refresh();
            }
        }
        foreach (DashboardViewModel dashboard in _dashboardDocuments.Values)
        {
            dashboard.Load();
        }
        BasesDockDashboard?.Load();
    }
}
