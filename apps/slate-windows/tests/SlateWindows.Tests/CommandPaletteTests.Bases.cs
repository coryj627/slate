// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows.Threading;
using SlateWindows.Bases;
using SlateWindows.Commands;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// WB-14 (#1397): the palette's Edit View Filters row on a base whose
/// definition is gone — the real <see cref="PaletteCommandSource"/> over the
/// workspace's real command. The row is unavailable with the definition's
/// own reason, and Enter speaks that reason once and never reaches the
/// command: the reasoned command, not the generic sentence a plain
/// <c>RelayCommand</c> would leave.
/// </summary>
public sealed partial class CommandPaletteTests
{
    [Fact]
    public void TheEditViewFiltersRowSpeaksTheDefinitionReasonAndNeverRunsTheCommand()
    {
        using FixtureVault fixture = FixtureVault.Create(2, "palette-bases");
        File.WriteAllText(
            Path.Combine(fixture.Root, "Notes.base"),
            "filters: 'file.ext == \"md\"'\nviews:\n  - type: table\n    name: Main\n    order:\n      - file.name\n");
        using VaultSession session = VaultSession.OpenFilesystem(fixture.Root);
        using (var cancel = new CancelToken())
        {
            session.ScanInitial(cancel);
        }
        var workspaceAnnounced = new List<A11yEvent>();
        using var workspace = new WorkspaceViewModel(
            session, fixture.Root, () => [], workspaceAnnounced.Add,
            startInteractionBackgroundWork: false);
        workspace.OpenPath("Notes.base");
        BaseDocumentViewModel document =
            Assert.IsType<BaseDocumentViewModel>(workspace.ActiveGroup.ActiveTab?.Base);
        Assert.Equal(BaseLoadState.Ready, document.State);
        File.Delete(Path.Combine(fixture.Root, "Notes.base"));
        document.Refresh();
        Assert.Equal(BaseLoadState.Failed, document.State);
        string reason = Assert.IsType<string>(document.DefinitionEditingDisabledReason);
        workspaceAnnounced.Clear();

        string recents = Path.Combine(fixture.Root + "-device", "command-palette-recents.json");
        try
        {
            using var source = new PaletteCommandSource(
                new WorkspaceCommandHost(workspace),
                Dispatcher.CurrentDispatcher,
                new CommandPaletteRecentsStore(recents));
            var announced = new List<A11yEvent>();
            var palette = new CommandPaletteViewModel(source, announced.Add, lane: new InlineWorkLane());
            palette.Open();
            CommandPaletteRowViewModel row =
                Assert.Single(palette.Rows, candidate => candidate.Id == ChordTable.Ids.BasesEditViewFilters);
            Assert.True(row.IsUnavailable);
            Assert.Equal(reason, row.DisabledReason);

            palette.Invoke(row);

            A11yEvent.PaletteCommandUnavailable spoken =
                Assert.IsType<A11yEvent.PaletteCommandUnavailable>(Assert.Single(announced));
            Assert.Equal(reason, spoken.Reason);
            Assert.NotEqual(SlateCommandRegistrar.UnavailableReason, spoken.Reason);
            Assert.True(palette.IsOpen);
            Assert.Null(workspace.BaseQueryBuilderSheet);
            Assert.DoesNotContain(workspaceAnnounced, e => e is A11yEvent.BasesFiltersOpenFailed);
        }
        finally
        {
            if (Directory.Exists(fixture.Root + "-device"))
            {
                Directory.Delete(fixture.Root + "-device", recursive: true);
            }
        }
    }

    /// <summary>A command host over a workspace whose vault commands are
    /// inert: the palette's open asks every listed row for its reason, and
    /// those resolve the vault commands too.</summary>
    private sealed class WorkspaceCommandHost(WorkspaceViewModel workspace) : ISlateCommandHost
    {
        public WorkspaceViewModel? Workspace => workspace;

        public FilesSidebarViewModel? FileSidebar => null;

        public QuickSwitcherViewModel? QuickSwitcher => null;

        public bool IsVaultOpen => true;

        public System.Windows.Input.ICommand OpenVaultCommand { get; } = new RelayCommand(_ => { }, _ => true);

        public System.Windows.Input.ICommand CloseVaultCommand { get; } = new RelayCommand(_ => { }, _ => true);

        public System.Windows.Input.ICommand ToggleSearchCommand { get; } = new RelayCommand(_ => { }, _ => true);

        public System.Windows.Input.ICommand FindInNoteCommand { get; } = new RelayCommand(_ => { }, _ => true);
    }
}
