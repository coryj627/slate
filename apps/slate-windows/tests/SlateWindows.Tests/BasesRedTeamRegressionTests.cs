// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Concurrent;
using SlateWindows.Bases;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W4-6 (#738) red-team round 1 regressions — each fact bites a
/// defect the adversarial pass found in the first cut:
/// - the tab REPLACE arm never attached Bases documents (the app's
///   default current-tab open route shipped a dead pane);
/// - ExecuteBody read the stale <c>_views</c> field before the posted
///   assignment landed (every first asynchronous load banner-lied);
/// - funnel outcomes lived in a single overwritable slot (a second
///   write before the first publish silenced the first outcome);
/// - Load cleared the quick filter but not the sort indicator;
/// - saved-query tabs were excluded from ActiveBaseDocument;
/// - the builder silently dropped typed-but-invalid rows (a typo
///   could REMOVE a view's filters while announcing success);
/// - C14's filtered-vs-all choice was absent;
/// - assorted canonical refusals (out-of-vault export, stale
///   dashboard editor) fell through to generic failures.
/// </summary>
public sealed class BasesRedTeamRegressionTests : IDisposable
{
    private readonly string _root;
    private readonly VaultSession _session;
    private readonly List<A11yEvent> _announced = [];

    public BasesRedTeamRegressionTests()
    {
        _root = Path.Combine(
            Path.GetTempPath(), $"slate-windows-test-bases-redteam-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
        WriteNote("note0.md", "todo");
        WriteNote("note1.md", "todo");
        WriteNote("note2.md", "done");
        File.WriteAllText(
            Path.Combine(_root, "Status.base"),
            "filters: 'status == \"todo\"'\n" +
            "views:\n" +
            "  - type: table\n" +
            "    name: Main\n" +
            "    order:\n" +
            "      - file.name\n" +
            "      - note.status\n");
        File.WriteAllText(
            Path.Combine(_root, "Others.base"),
            "filters: 'file.ext == \"md\"'\n" +
            "views:\n" +
            "  - type: table\n" +
            "    name: Main\n" +
            "    order:\n" +
            "      - file.name\n");
        // VIEW-level filters: BaseViewEditQueryJson is view-scoped, so
        // only these enter the builder as a preserved row.
        File.WriteAllText(
            Path.Combine(_root, "ViewFiltered.base"),
            "views:\n" +
            "  - type: table\n" +
            "    name: Main\n" +
            "    filters: 'status == \"todo\"'\n" +
            "    order:\n" +
            "      - file.name\n");
        _session = VaultSession.OpenFilesystem(_root);
        using var cancel = new CancelToken();
        _session.ScanInitial(cancel);
    }

    public void Dispose()
    {
        _session.Dispose();
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private void WriteNote(string name, string status) =>
        File.WriteAllText(
            Path.Combine(_root, name),
            $"---\nstatus: {status}\n---\n\n# {name}\n\nBody.\n");

    private WorkspaceViewModel NewWorkspace() =>
        new(_session, _root, () => [], _announced.Add,
            startInteractionBackgroundWork: false);

    // --- Tab lifecycle (the replace arm) ---

    [Fact]
    public void CurrentTabOpenOverANoteAttachesTheBasesDocument()
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        workspace.OpenPath("note0.md");
        WorkspaceTabViewModel tab =
            Assert.IsType<WorkspaceTabViewModel>(workspace.ActiveGroup.ActiveTab);
        Assert.False(tab.IsBase);

        // The DEFAULT open target reuses the current tab in place —
        // the route the files sidebar and queries leaf take.
        workspace.OpenPath("Status.base");

        Assert.Same(tab, workspace.ActiveGroup.ActiveTab);
        Assert.True(tab.IsBase);
        BaseDocumentViewModel document =
            Assert.IsType<BaseDocumentViewModel>(tab.Base);
        Assert.Same(document, workspace.BaseDocumentFor("Status.base"));
        Assert.Same(document, workspace.ActiveBaseDocument);
        Assert.Equal(BaseLoadState.Ready, document.State);
    }

    [Fact]
    public void CurrentTabOpenSwapsBaseDocumentsAndReleasesTheOrphan()
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        workspace.OpenPath("Status.base");
        WorkspaceTabViewModel tab =
            Assert.IsType<WorkspaceTabViewModel>(workspace.ActiveGroup.ActiveTab);
        BaseDocumentViewModel first =
            Assert.IsType<BaseDocumentViewModel>(tab.Base);

        workspace.OpenPath("Others.base");

        BaseDocumentViewModel second =
            Assert.IsType<BaseDocumentViewModel>(tab.Base);
        Assert.NotSame(first, second);
        Assert.Equal("Others.base", second.Path);
        // The orphaned document was RELEASED at the replace (INV-2) —
        // a later Load must refuse to mutate its shut-down state.
        first.Load();
        Assert.NotEqual(BaseLoadState.Loading, first.State);

        // Base → markdown clears the attach entirely.
        workspace.OpenPath("note0.md");
        Assert.Null(tab.Base);
        Assert.False(tab.IsBase);
        Assert.Null(workspace.ActiveBaseDocument);
    }

    [Fact]
    public void SavedQueryTabsCarryTheActiveBaseDocument()
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        string id = _session.SaveQuery(
            "Todo query", null,
            /* core-produced seed */ SeedQueryJson(),
            SavedQuerySourceSyntax.Builder);
        workspace.RefreshBaseQueries();

        workspace.RunSavedQuery(id);

        WorkspaceTabViewModel tab =
            Assert.IsType<WorkspaceTabViewModel>(workspace.ActiveGroup.ActiveTab);
        Assert.True(tab.IsSavedQueryTab);
        // The IsBase-only gate left every slate.bases.* command dead
        // on saved-query tabs (red team round 1).
        Assert.NotNull(workspace.ActiveBaseDocument);
        Assert.Same(tab.Base, workspace.ActiveBaseDocument);
    }

    private string SeedQueryJson()
    {
        ulong handle = _session.OpenDql("TABLE file.name", thisPath: null);
        try
        {
            return _session.BaseViewQueryJson(handle, 0);
        }
        finally
        {
            _session.CloseBase(handle);
        }
    }

    // --- Async publish ordering (masked by synchronous test mode) ---

    [Fact]
    public async Task AsyncFirstLoadPublishesTheRealViewNotAStaleFieldRead()
    {
        var pump = new PumpSynchronizationContext();
        BaseDocumentViewModel document = NewAsyncDocument(pump, "Status.base");

        document.Load();
        await document.DrainForTests();

        // The worker finished; every publish is still QUEUED on the UI
        // context — exactly the window where the first cut's ExecuteBody
        // read the empty _views field and queued the wrong
        // "No executable base views were found." banner.
        Assert.Equal(BaseLoadState.Loading, document.State);
        pump.Drain();
        Assert.Equal(BaseLoadState.Ready, document.State);
        Assert.Null(document.StateMessage);
        Assert.Single(document.Views);
        Assert.Equal(2, document.Result!.Rows.Length);
        document.Shutdown();
    }

    [Fact]
    public async Task FunnelOutcomesSurviveASupersedingExecute()
    {
        var pump = new PumpSynchronizationContext();
        BaseDocumentViewModel document = NewAsyncDocument(pump, "Status.base");
        document.Load();
        await document.DrainForTests();
        pump.Drain();
        Assert.Equal(BaseLoadState.Ready, document.State);

        var outcomes = new List<string>();
        document.RefreshForFunnel(1, () => outcomes.Add("first"));
        await document.DrainForTests();
        // The second write lands BEFORE the first publish is pumped —
        // its generation bump invalidates publish #1.
        document.RefreshForFunnel(2, () => outcomes.Add("second"));
        await document.DrainForTests();

        pump.Drain();

        // The single-slot first cut overwrote the first continuation:
        // "first" was never spoken. The queue drains BOTH at the
        // surviving publish, in write order.
        Assert.Equal(["first", "second"], outcomes);
        document.Shutdown();
    }

    private BaseDocumentViewModel NewAsyncDocument(
        SynchronizationContext context, string path)
    {
        SynchronizationContext? previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(context);
        try
        {
            return new BaseDocumentViewModel(
                _session, path, _announced.Add, synchronousForTests: false);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }

    // --- Sort indicator vs reload ---

    [Fact]
    public void LoadClearsThePublishedSortIndicator()
    {
        var document = new BaseDocumentViewModel(
            _session, "Status.base", _announced.Add, synchronousForTests: true);
        document.Load();
        Assert.True(document.ApplySortFromGrid(0, ascending: false));
        Assert.Equal((0, false), document.SortState);

        // The reload reopens the handle — the engine sort dies with it,
        // and the indicator must fall too or SaveSortToView persists a
        // sort the rows don't have (red team round 1).
        document.Load();

        Assert.Null(document.SortState);
        document.Shutdown();
    }

    // --- C5 leg 4: tab switch-away clears the quick filter ---

    [Fact]
    public void SwitchingAwayFromABaseTabClearsTheQuickFilterSilently()
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        workspace.OpenPath("Status.base");
        WorkspaceTabViewModel baseTab =
            Assert.IsType<WorkspaceTabViewModel>(workspace.ActiveGroup.ActiveTab);
        BaseDocumentViewModel document =
            Assert.IsType<BaseDocumentViewModel>(baseTab.Base);
        document.QuickFilterText = "note0";
        document.ApplyQuickFilter();
        Assert.True(document.QuickFilterActive);
        Assert.Single(document.Result!.Rows);
        // Cleared BEFORE the switch so the silence assertion below
        // covers the deactivation itself (round 2: a post-switch
        // clear made it vacuous).
        _announced.Clear();
        workspace.OpenPath("note2.md", WorkspaceOpenTarget.NewTab);

        // Activating the base tab's sibling deactivated the base tab.
        Assert.False(document.QuickFilterActive);
        Assert.Equal(string.Empty, document.QuickFilterText);
        Assert.Equal(2, document.Result!.Rows.Length);
        // Silent (INV-4): the unfiltered re-execute speaks NOTHING of
        // the Bases family — the tab open's own announcements
        // (TabFocused etc.) are the switch's, not the clear's.
        Assert.DoesNotContain(
            _announced,
            e => e is A11yEvent.BaseQuickFilterResult
                or A11yEvent.BaseRefreshed
                or A11yEvent.BasesRefreshUpdated);
    }

    // --- Builder: typed-but-invalid rows refuse, never drop ---

    [Fact]
    public void BuilderSaveToViewRefusesOnATypoInsteadOfStrippingFilters()
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        BaseDocumentViewModel document = workspace.BaseDocumentFor("ViewFiltered.base");
        string? json = null;
        document.ViewEditQueryJson(
            document.ActiveViewIndex, (fetched, _) => json = fetched);
        var builder = BaseQueryBuilderViewModel.ForView(
            _session, json!, document.Path, document.ActiveViewIndex,
            _announced.Add, synchronousForTests: true);
        // The D-18 authoring flow: delete the preserved row, then typo
        // the replacement.
        builder.RemoveCondition(builder.ConditionRows.Single());
        BuilderConditionRow typo = builder.AddCondition();
        typo.Expression = "file.ext ==";
        _announced.Clear();

        bool? saved = null;
        builder.SaveToView(document, result => saved = result);

        Assert.False(saved);
        Assert.Contains(_announced, e => e is A11yEvent.BasesViewSaveFailed);
        Assert.NotEqual(string.Empty, builder.SaveError);
        // The view's filters SURVIVED — the first cut dropped the
        // invalid row, emitted RemoveViewKey("filters"), and announced
        // success.
        Assert.Contains(
            "status == \"todo\"",
            File.ReadAllText(Path.Combine(_root, "ViewFiltered.base")),
            StringComparison.Ordinal);
        builder.Shutdown();
    }

    // --- C14: the filtered-vs-all choice ---

    [Fact]
    public void FilteredCopyHonorsTheScopeChoiceAndCancelAnnounces()
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        workspace.OpenPath("Status.base");
        BaseDocumentViewModel document =
            Assert.IsType<BaseDocumentViewModel>(workspace.ActiveBaseDocument);
        document.QuickFilterText = "note0";
        document.ApplyQuickFilter();
        Assert.True(document.QuickFilterActive);

        // Cancel: the canonical sentence, nothing composed.
        workspace.BasesExportScopePrompt = _ => WorkspaceViewModel.BasesExportScope.Cancel;
        _announced.Clear();
        workspace.BasesCopyMarkdownCommand.Execute(null);
        var canceled = Assert.IsType<A11yEvent.BasesQuickFilterChoiceCanceled>(
            Assert.Single(_announced));
        Assert.Equal("Copy", canceled.Verb);

        // All-rows: the compose runs WITHOUT the filter.
        string? allRows = null;
        document.ExportText(
            ExportFormat.Markdown, includeQuickFilter: false, text => allRows = text);
        Assert.Contains("note1", allRows, StringComparison.Ordinal);
        // Filtered: the compose keeps it.
        string? filtered = null;
        document.ExportText(
            ExportFormat.Markdown, includeQuickFilter: true, text => filtered = text);
        Assert.DoesNotContain("note1", filtered, StringComparison.Ordinal);
    }

    // --- Canonical refusals ---

    [Fact]
    public void OutOfVaultSavedQueryExportSpeaksTheCanonicalRefusal()
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        string id = _session.SaveQuery(
            "Exportable", null, SeedQueryJson(), SavedQuerySourceSyntax.Builder);
        _announced.Clear();

        workspace.ExportSavedQueryAsBase(id, "C:\\outside\\query.base");

        Assert.Contains(_announced, e => e is A11yEvent.BasesPathOutsideVault);
        Assert.DoesNotContain(
            _announced, e => e is A11yEvent.BasesSavedQueryExported);
    }

    [Fact]
    public void DashboardEditorRefusesAStaleSaveWithTheCanonicalSentence()
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        string queryId = _session.SaveQuery(
            "Section query", null, SeedQueryJson(), SavedQuerySourceSyntax.Builder);
        string dashboardId = _session.SaveDashboard(
            "Board", [new DashboardSection(queryId, null, null)]);
        workspace.RefreshBaseQueries();
        workspace.OpenDashboardEditor(dashboardId);
        DashboardEditorViewModel editor =
            Assert.IsType<DashboardEditorViewModel>(workspace.DashboardEditorSheet);

        // The dashboard changes UNDER the open editor. (The sleep
        // guarantees a distinct modified_at_ms tick.)
        Thread.Sleep(25);
        _session.UpdateDashboard(
            dashboardId, "Board renamed", [new DashboardSection(queryId, null, null)]);
        _announced.Clear();

        workspace.SaveDashboardEditor();

        Assert.Contains(_announced, e => e is A11yEvent.BasesDashboardSectionStale);
        Assert.DoesNotContain(_announced, e => e is A11yEvent.BasesDashboardUpdated);
        // The editor reloaded onto what is actually there.
        DashboardEditorViewModel reloaded =
            Assert.IsType<DashboardEditorViewModel>(workspace.DashboardEditorSheet);
        Assert.NotSame(editor, reloaded);
        Assert.Equal("Board renamed", reloaded.Name);
    }

    [Fact]
    public void RenameSavedQueryRetitlesOpenSurfaces()
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        string id = _session.SaveQuery(
            "Old name", null, SeedQueryJson(), SavedQuerySourceSyntax.Builder);
        workspace.RefreshBaseQueries();
        workspace.RunSavedQuery(id);
        WorkspaceTabViewModel tab =
            Assert.IsType<WorkspaceTabViewModel>(workspace.ActiveGroup.ActiveTab);
        BaseDocumentViewModel document =
            Assert.IsType<BaseDocumentViewModel>(tab.Base);
        Assert.Equal("Old name", document.DisplayName);

        workspace.RenameSavedQuery(id, "New name");

        Assert.Equal("New name", document.DisplayName);
        Assert.Equal("New name", tab.Item.Name);
    }

    // --- C8: the clean-tab route (lease + CAS + rebaseline + funnel) ---

    [Fact]
    public void CleanTabCellWriteLandsRebaselinesAndAnnouncesOnce()
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        BaseDocumentViewModel document = workspace.BaseDocumentFor("Status.base");
        workspace.OpenPath("note0.md");
        WorkspaceTabViewModel tab =
            Assert.IsType<WorkspaceTabViewModel>(workspace.ActiveGroup.ActiveTab);
        Assert.False(tab.IsDirty);
        BasesRow row = document.Result!.Rows.Single(candidate =>
            candidate.FilePath.EndsWith("note0.md", StringComparison.Ordinal));
        BasesColumn column = document.Result!.Columns.Single(candidate =>
            string.Equals(candidate.Id, "note.status", StringComparison.Ordinal));
        _announced.Clear();

        document.ApplyPropertyEdit!(row, column, new PropertyValue.Text("todo"));
        // The tab write seam completes through the dispatcher even in
        // synchronous test mode — pump until the outcome arrives.
        WaitForUi(() => _announced.Count > 0);

        // The write landed through the tab seam: file updated, tab
        // clean and NOT stale (rebaselined), exactly one outcome.
        Assert.Contains(
            "status: todo",
            File.ReadAllText(Path.Combine(_root, "note0.md")),
            StringComparison.Ordinal);
        Assert.False(tab.IsDirty);
        Assert.False(tab.IsExternallyStale);
        Assert.Equal(
            1,
            _announced.Count(e =>
                e is A11yEvent.BasesCellSaved
                    or A11yEvent.BasesCellCleared
                    or A11yEvent.BasesCellRowNoLongerMatches));
        // The lease was RELEASED by the completion: a second write is
        // not refused as in-flight.
        _announced.Clear();
        document.ApplyPropertyEdit!(row, column, new PropertyValue.Text("todo"));
        WaitForUi(() => _announced.Count > 0);
        Assert.DoesNotContain(
            _announced,
            e => e is A11yEvent.HostComposed composed
                && composed.Text.StartsWith(
                    "Wait for the current save", StringComparison.Ordinal));
    }

    // --- Round 2 regressions ---

    [Fact]
    public void RenamingAnOpenBaseRekeysTheDocumentRegistry()
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        workspace.OpenPath("Status.base");
        WorkspaceTabViewModel tab =
            Assert.IsType<WorkspaceTabViewModel>(workspace.ActiveGroup.ActiveTab);
        BaseDocumentViewModel before =
            Assert.IsType<BaseDocumentViewModel>(tab.Base);
        File.Move(
            Path.Combine(_root, "Status.base"),
            Path.Combine(_root, "Renamed.base"));

        workspace.RetargetPath("Status.base", "Renamed.base");

        // The registry re-keyed and the tab re-attached (round 2
        // blocker: the old-path document survived, its Retry reopened
        // the old path forever, and the next release sweep shut it
        // down under the live tab).
        BaseDocumentViewModel after =
            Assert.IsType<BaseDocumentViewModel>(tab.Base);
        Assert.NotSame(before, after);
        Assert.Equal("Renamed.base", after.Path);
        Assert.Equal(BaseLoadState.Ready, after.State);
        Assert.Same(after, workspace.BaseDocumentFor("Renamed.base"));
        // The superseded document is shut down: it refuses new work.
        before.Load();
        Assert.NotEqual(BaseLoadState.Loading, before.State);
    }

    [Fact]
    public async Task SortPublishSettlesAPendingFunnelOutcome()
    {
        var pump = new PumpSynchronizationContext();
        BaseDocumentViewModel document = NewAsyncDocument(pump, "Status.base");
        document.Load();
        await document.DrainForTests();
        pump.Drain();
        Assert.Equal(BaseLoadState.Ready, document.State);
        var outcomes = new List<string>();
        document.RefreshForFunnel(7, () => outcomes.Add("write"));
        await document.DrainForTests();
        // The sort supersedes the funnel execute BEFORE its publish
        // pumps — round 2: SortBody's publish bypassed the settlement
        // and the write outcome was never spoken.
        Assert.True(document.ApplySortFromGrid(0, ascending: false));
        await document.DrainForTests();

        pump.Drain();

        Assert.Equal(["write"], outcomes);
        document.Shutdown();
    }

    [Fact]
    public void PaneFocusMoveDoesNotClearTheQuickFilter()
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        workspace.OpenPath("Status.base");
        BaseDocumentViewModel document =
            Assert.IsType<BaseDocumentViewModel>(workspace.ActiveBaseDocument);
        document.QuickFilterText = "note0";
        document.ApplyQuickFilter();
        Assert.True(document.QuickFilterActive);

        // A SPLIT activates the new group; the base tab stays mounted
        // and visible — C5's fourth leg is "a different TAB", not a
        // pane focus move (round 2: the grid silently expanded while
        // the user read it from the other pane).
        workspace.OpenPath("note2.md", WorkspaceOpenTarget.SplitRight);

        Assert.True(document.QuickFilterActive);
        Assert.Single(document.Result!.Rows);
    }

    [Fact]
    public void BuilderUpdatePreservesTheSavedQueryDescription()
    {
        string id = _session.SaveQuery(
            "Described", "the description", SeedQueryJson(),
            SavedQuerySourceSyntax.Builder);
        SavedQuery saved = _session.GetSavedQuery(id);
        var editor = BaseQueryBuilderViewModel.ForSavedQuery(
            _session, saved, _announced.Add, synchronousForTests: true);

        Assert.True(editor.UpdateSavedQuery());

        // Codex round 7: the update passed description: null and core
        // persisted it — editing filters silently erased the
        // description while announcing success.
        Assert.Equal("the description", _session.GetSavedQuery(id).Description);
        editor.Shutdown();
    }

    [Fact]
    public void DashboardUpdateReachesTheDockedInstanceAndTitles()
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        string queryId = _session.SaveQuery(
            "Section query", null, SeedQueryJson(), SavedQuerySourceSyntax.Builder);
        string dashboardId = _session.SaveDashboard(
            "Board", [new DashboardSection(queryId, null, null)]);
        workspace.RefreshBaseQueries();
        workspace.DockDashboardToSidebar(dashboardId, "Board");
        workspace.OpenDashboard(dashboardId, "Board");
        WorkspaceTabViewModel tab =
            Assert.IsType<WorkspaceTabViewModel>(workspace.ActiveGroup.ActiveTab);

        workspace.OpenDashboardEditor(dashboardId);
        DashboardEditorViewModel editor =
            Assert.IsType<DashboardEditorViewModel>(workspace.DashboardEditorSheet);
        editor.Name = "Board renamed";
        workspace.SaveDashboardEditor();

        // Codex round 7: only the registry document reloaded — the
        // docked copy kept stale sections/name and the tab title kept
        // the old name until an unrelated event.
        Assert.Equal("Board renamed", workspace.BasesDockDashboard!.Name);
        Assert.Equal("Board renamed", tab.Item.Name);
    }

    private static void WaitForUi(Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(20);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "Asynchronous action timed out.");
            var frame = new System.Windows.Threading.DispatcherFrame();
            System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Background,
                new Action(() => frame.Continue = false));
            System.Windows.Threading.Dispatcher.PushFrame(frame);
            Thread.Yield();
        }
    }

    // --- The shared YAML quoter ---

    [Fact]
    public void QuoteYamlStringEscapesControlCharactersForReal()
    {
        string yaml = BaseDocumentViewModel.SlateSortYaml("weird\nid\twith\rchars", true);

        // Exactly the two structural lines — the first cut's no-op
        // Replaces let the raw control characters break the fragment.
        Assert.Equal(2, yaml.Split('\n').Length);
        Assert.Contains("\\n", yaml, StringComparison.Ordinal);
        Assert.Contains("\\t", yaml, StringComparison.Ordinal);
        Assert.Contains("\\r", yaml, StringComparison.Ordinal);
    }

    private sealed class PumpSynchronizationContext : SynchronizationContext
    {
        private readonly ConcurrentQueue<(SendOrPostCallback Callback, object? State)> _queue = [];

        public override void Post(SendOrPostCallback callback, object? state) =>
            _queue.Enqueue((callback, state));

        public void Drain()
        {
            while (_queue.TryDequeue(out var work))
            {
                work.Callback(work.State);
            }
        }
    }
}
