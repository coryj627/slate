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
