// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Bases;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W4-6 (#738) phase E facts: saved-query documents and lifecycle,
/// dashboards, and the dock (contract C12) — announcements canonical,
/// documents shared and released, dock following the active note.
/// </summary>
public sealed class BasesQueriesTests : IDisposable
{
    private readonly FixtureVault _fixture;
    private readonly VaultSession _session;
    private readonly List<A11yEvent> _announced = [];
    private readonly string _savedQueryId;

    public BasesQueriesTests()
    {
        _fixture = FixtureVault.Create(3, "bases-queries");
        File.WriteAllText(
            Path.Combine(_fixture.Root, "Notes.base"),
            "filters: 'file.ext == \"md\"'\n" +
            "views:\n" +
            "  - type: table\n" +
            "    name: Main\n" +
            "    order:\n" +
            "      - file.name\n");
        _session = VaultSession.OpenFilesystem(_fixture.Root);
        using var cancel = new CancelToken();
        _session.ScanInitial(cancel);
        ulong scratch = _session.OpenBase("Notes.base");
        string queryJson;
        try
        {
            queryJson = _session.BaseViewQueryJson(scratch, 0);
        }
        finally
        {
            _session.CloseBase(scratch);
        }
        _savedQueryId = _session.SaveQuery(
            "All notes",
            description: null,
            queryJson,
            SavedQuerySourceSyntax.Builder);
    }

    public void Dispose()
    {
        _session.Dispose();
        _fixture.Dispose();
    }

    private WorkspaceViewModel NewWorkspace() =>
        new(_session, _fixture.Root, () => [], _announced.Add,
            startInteractionBackgroundWork: false);

    /// <summary>WB-14 (#1397): the definition probe is a file-backed base's. A
    /// saved query has no definition file — its Path is empty — so a refresh
    /// (the dock's follow, a Slate-owned change, the quick filter's clear)
    /// re-runs its query handle as before: never probed, never closed, never
    /// reopened, and its Edit View Filters keeps its own admission.</summary>
    [Fact]
    public void ASavedQueryRefreshSkipsTheDefinitionProbeAndKeepsItsHandle()
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        workspace.RefreshBaseQueries();
        workspace.RunSavedQuery(Assert.Single(workspace.SavedQueries).Id);
        BaseDocumentViewModel document =
            Assert.IsType<BaseDocumentViewModel>(workspace.ActiveGroup.ActiveTab?.Base);
        Assert.True(document.IsSavedQuery);
        Assert.Equal(BaseLoadState.Ready, document.State);
        string[] rows = [.. document.Result!.Rows.Select(row => row.FilePath)];
        Assert.Equal(3, rows.Length);
        Assert.Equal(1, document.OpensForTests);
        document.DefinitionReadFaultForTests = () => throw new VaultException.Io("a probe ran");

        document.Refresh();

        Assert.Equal(BaseLoadState.Ready, document.State);
        Assert.Equal(rows, document.Result!.Rows.Select(row => row.FilePath));
        Assert.Equal(1, document.OpensForTests);
        Assert.Null(document.DefinitionEditingDisabledReason);
        Assert.True(workspace.BasesEditViewFiltersCommand.CanExecute(null));
    }

    /// <summary>W7-7 PR 4b (AR-59, owner-decided): the rail's arrows reveal
    /// the Queries leaf as they pass it, and its refresh runs; a refresh that
    /// failed after the reader had arrowed on spoke over the next leaf. The
    /// failure speaks only while the Queries leaf is the one shown.</summary>
    [Fact]
    public void AFailedRefreshSpeaksOnlyWhileTheQueriesLeafIsShown()
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        workspace.ActiveLeaf = WorkspaceViewModel.Leaves.First(leaf => leaf.Id == "outline");
        workspace.QueriesRefreshReadForTests = () => throw new VaultException.Io("registry unreadable");
        _announced.Clear();

        workspace.RefreshBaseQueries();

        Assert.DoesNotContain(_announced, line => line is A11yEvent.BasesQueriesRefreshFailed);

        workspace.ActiveLeaf = WorkspaceViewModel.Leaves.First(leaf => leaf.Id == "queries");

        Assert.Contains(_announced, line => line is A11yEvent.BasesQueriesRefreshFailed);
    }

    [Fact]
    public void SavedQueryTabSharesTheDocumentAndExecutes()
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        workspace.RefreshBaseQueries();
        SavedQuerySummary summary = Assert.Single(workspace.SavedQueries);

        workspace.RunSavedQuery(summary.Id);

        WorkspaceTabViewModel tab =
            Assert.IsType<WorkspaceTabViewModel>(workspace.ActiveGroup.ActiveTab);
        Assert.True(tab.IsSavedQueryTab);
        Assert.True(tab.IsBaseVisible);
        BaseDocumentViewModel document =
            Assert.IsType<BaseDocumentViewModel>(tab.Base);
        Assert.True(document.IsSavedQuery);
        Assert.Equal("All notes", document.DisplayName);
        Assert.Equal(BaseLoadState.Ready, document.State);
        // Saved-query documents refuse save-sort silently (ephemeral
        // handles cannot be edited as files).
        Assert.True(document.ApplySortFromGrid(0, ascending: false));
        _announced.Clear();
        document.SaveSortToView();
        Assert.Empty(_announced);
    }

    [Fact]
    public void RenameDeleteAndPinFollowTheMacRules()
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        workspace.RefreshBaseQueries();
        SavedQuerySummary summary = Assert.Single(workspace.SavedQueries);
        _announced.Clear();

        workspace.RenameSavedQuery(summary.Id, "  ");
        Assert.IsType<A11yEvent.BasesSavedQueryRenameNameNeeded>(
            Assert.Single(_announced));
        _announced.Clear();

        workspace.RenameSavedQuery(summary.Id, "Renamed");
        var renamed = Assert.IsType<A11yEvent.BasesSavedQueryRenamed>(
            Assert.Single(_announced, e => e is A11yEvent.BasesSavedQueryRenamed));
        Assert.Equal("Renamed", renamed.Name);
        Assert.Equal("Renamed", Assert.Single(workspace.SavedQueries).Name);

        workspace.ToggleSavedQueryPin(summary.Id);
        Assert.Contains(summary.Id, workspace.PinnedSavedQueryIdsForTests);

        workspace.RunSavedQuery(summary.Id);
        Assert.True(
            ((WorkspaceTabViewModel)workspace.ActiveGroup.ActiveTab!).IsSavedQueryTab);
        _announced.Clear();

        workspace.DeleteSavedQuery(summary.Id);
        Assert.Contains(_announced, e => e is A11yEvent.BasesSavedQueryDeleted);
        Assert.Empty(workspace.SavedQueries);
        // The open tab on the deleted query closed (the mac rule).
        Assert.DoesNotContain(
            workspace.Groups.SelectMany(g => g.Tabs),
            tab => tab.IsSavedQueryTab);
        Assert.Empty(workspace.PinnedSavedQueryIdsForTests);
    }

    /// <summary>WB-16 (#1301, contract 40 AR-56): creating, updating and
    /// renaming a saved query speak it as the Saved queries list names it.
    /// "draft" and "DRAFT." read alike, so the list reads each by its place;
    /// the announcements spoke the bare name the user typed.</summary>
    [Fact]
    public void SavedQueryCreateUpdateAndRenameSpeakTheListsComposedName() => RunSta(() =>
    {
        string queryJson = _session.GetSavedQuery(_savedQueryId).QueryJson;
        _ = _session.SaveQuery("draft", null, queryJson, SavedQuerySourceSyntax.Builder);
        using WorkspaceViewModel workspace = NewWorkspace();
        workspace.RefreshBaseQueries();

        _announced.Clear();
        workspace.BasesNewQueryCommand.Execute(null);
        Assert.True(workspace.BaseQueryBuilderSheet!.SaveAsSavedQuery("DRAFT.", null), "premise: the builder did not save");
        string created = Assert.Single(_announced.OfType<A11yEvent.BasesSavedQueryCreated>()).Name;
        workspace.RefreshBaseQueries();
        string id = Assert.Single(workspace.SavedQueries, query => query.Name == "DRAFT.").Id;
        Assert.Equal(ListedSavedQueryName(workspace, id), created);

        _announced.Clear();
        workspace.EditSavedQueryInBuilder(id);
        workspace.BuilderUpdateSavedQuery();
        Assert.Equal(
            ListedSavedQueryName(workspace, id),
            Assert.Single(_announced.OfType<A11yEvent.BasesSavedQueryUpdated>()).Name);

        _announced.Clear();
        workspace.RenameSavedQuery(id, "Draft");
        string renamed = Assert.Single(_announced.OfType<A11yEvent.BasesSavedQueryRenamed>()).Name;
        workspace.RefreshBaseQueries();
        Assert.Equal(ListedSavedQueryName(workspace, id), renamed);
        Assert.NotEqual("Draft", renamed);
    });

    /// <summary>WB-16 red team: the new-query chord runs from anywhere, and
    /// the Saved queries list fills only when its leaf is shown. A query
    /// created before the list was ever loaded is named as the list will read
    /// it — among the registry's queries, not alone.</summary>
    [Fact]
    public void ASavedQueryCreatedBeforeTheListLoadsSpeaksTheListsComposedName() => RunSta(() =>
    {
        string queryJson = _session.GetSavedQuery(_savedQueryId).QueryJson;
        _ = _session.SaveQuery("draft", null, queryJson, SavedQuerySourceSyntax.Builder);
        using WorkspaceViewModel workspace = NewWorkspace();
        // The list stays unloaded through the save, as it is in production
        // until its worker's refresh publishes, after the announcement: every
        // refresh meanwhile fails before it reads.
        workspace.QueriesRefreshReadForTests = static () => throw new VaultException.Db("the list is not loaded yet");
        Assert.Empty(workspace.SavedQueries);

        _announced.Clear();
        workspace.BasesNewQueryCommand.Execute(null);
        Assert.True(workspace.BaseQueryBuilderSheet!.SaveAsSavedQuery("Draft", null), "premise: the builder did not save");
        string created = Assert.Single(_announced.OfType<A11yEvent.BasesSavedQueryCreated>()).Name;
        Assert.True(workspace.SavedQueries.Count == 0, $"premise: the list loaded before the announcement ({workspace.SavedQueries.Count})");

        workspace.QueriesRefreshReadForTests = null;
        workspace.RefreshBaseQueries();
        string id = Assert.Single(workspace.SavedQueries, query => query.Name == "Draft").Id;
        Assert.Equal(ListedSavedQueryName(workspace, id), created);
        Assert.NotEqual("Draft", created);
    });

    /// <summary>WB-16 (#1301, contract 40 AR-57): saving and updating a
    /// dashboard speak it as the Dashboards list names it, read-alike names
    /// told apart by place.</summary>
    [Fact]
    public void DashboardSaveAndUpdateSpeakTheListsComposedName() => RunSta(() =>
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        workspace.RefreshBaseQueries();
        SaveDashboard(workspace, dashboardId: null, "draft");

        _announced.Clear();
        SaveDashboard(workspace, dashboardId: null, "DRAFT.");
        string saved = Assert.Single(_announced.OfType<A11yEvent.BasesDashboardSaved>()).Name;
        string id = Assert.Single(workspace.Dashboards, dashboard => dashboard.Name == "DRAFT.").Id;
        Assert.Equal(ListedDashboardName(workspace, id), saved);
        Assert.NotEqual("DRAFT.", saved);

        _announced.Clear();
        SaveDashboard(workspace, id, "DRAFT.");
        Assert.Equal(
            ListedDashboardName(workspace, id),
            Assert.Single(_announced.OfType<A11yEvent.BasesDashboardUpdated>()).Name);
    });

    private void SaveDashboard(WorkspaceViewModel workspace, string? dashboardId, string name)
    {
        workspace.OpenDashboardEditor(dashboardId);
        DashboardEditorViewModel editor = Assert.IsType<DashboardEditorViewModel>(workspace.DashboardEditorSheet);
        if (editor.Sections.Count == 0)
        {
            editor.Sections.Add(new DashboardEditorSection(_savedQueryId, "All notes"));
        }

        editor.Name = name;
        workspace.SaveDashboardEditor();
        Assert.Null(workspace.DashboardEditorSheet);
    }

    /// <summary>The UIA name the shipped Saved queries list gives the query
    /// <paramref name="id"/>.</summary>
    private static string ListedSavedQueryName(WorkspaceViewModel workspace, string id) =>
        ListedName("QueriesSavedList", workspace, [.. workspace.SavedQueries.Select(query => query.Id)], id);

    private static string ListedDashboardName(WorkspaceViewModel workspace, string id) =>
        ListedName("QueriesDashboardsList", workspace, [.. workspace.Dashboards.Select(dashboard => dashboard.Id)], id);

    private static string ListedName(string list, WorkspaceViewModel workspace, string[] ids, string id)
    {
        string[] listed = [];
        SharedNameTests.HostedNames(list, workspace, names => listed = names);
        Assert.Equal(ids.Length, listed.Length);
        return listed[Array.IndexOf(ids, id)];
    }

    private static void RunSta(Action body) =>
        StaThread.Run(body, TimeSpan.FromSeconds(60), "STA test body timed out.");

    [Fact]
    public void DashboardEditorSavesLoadsAndDeletes()
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        workspace.RefreshBaseQueries();
        _announced.Clear();

        workspace.OpenDashboardEditor(dashboardId: null);
        DashboardEditorViewModel editor =
            Assert.IsType<DashboardEditorViewModel>(workspace.DashboardEditorSheet);
        editor.Sections.Add(new DashboardEditorSection(_savedQueryId, "All notes"));

        // Blank name refuses with the canonical sentence.
        workspace.SaveDashboardEditor();
        Assert.IsType<A11yEvent.BasesDashboardNameNeeded>(Assert.Single(_announced));
        _announced.Clear();

        editor.Name = "Reading";
        workspace.SaveDashboardEditor();
        var saved = Assert.IsType<A11yEvent.BasesDashboardSaved>(
            Assert.Single(_announced));
        Assert.Equal("Reading", saved.Name);
        Assert.Null(workspace.DashboardEditorSheet);
        DashboardSummary dashboard = Assert.Single(workspace.Dashboards);

        // The dashboard tab loads its sections with executed results.
        workspace.OpenDashboard(dashboard.Id, dashboard.Name);
        WorkspaceTabViewModel tab =
            Assert.IsType<WorkspaceTabViewModel>(workspace.ActiveGroup.ActiveTab);
        Assert.True(tab.IsDashboardTab);
        DashboardViewModel document =
            Assert.IsType<DashboardViewModel>(tab.Dashboard);
        DashboardSectionViewModel section = Assert.Single(document.Sections);
        Assert.Equal(DashboardSectionState.Ready, section.State);
        Assert.Equal("All notes", section.Title);
        Assert.NotNull(section.Result);
        _announced.Clear();

        workspace.DeleteDashboard(dashboard.Id);
        Assert.Contains(_announced, e => e is A11yEvent.BasesDashboardDeleted);
        Assert.Empty(workspace.Dashboards);
        Assert.DoesNotContain(
            workspace.Groups.SelectMany(g => g.Tabs),
            candidate => candidate.IsDashboardTab);
    }

    [Fact]
    public void DockFollowsTheActiveNote()
    {
        using WorkspaceViewModel workspace = NewWorkspace();
        _announced.Clear();

        workspace.DockBaseFileToSidebar("Notes.base", "Notes");

        BaseDocumentViewModel dock =
            Assert.IsType<BaseDocumentViewModel>(workspace.BasesDockDocument);
        Assert.Equal(BaseLoadState.Ready, dock.State);
        Assert.Equal("basesDock", workspace.ActiveLeaf.Id);
        Assert.Contains(_announced, e => e is A11yEvent.BasesDockUpdatedForNote);
        _announced.Clear();

        // Opening a note re-points the dock's this_path (500 ms on the
        // desktop; inline in synchronous mode).
        workspace.OpenPath("note1.md");
        Assert.Equal("note1.md", dock.ThisPath);
        Assert.Contains(_announced, e => e is A11yEvent.BasesDockUpdatedForNote);

        workspace.ClearBasesDock();
        Assert.Null(workspace.BasesDockDocument);
        Assert.Null(workspace.BasesDockTarget);
    }
}
