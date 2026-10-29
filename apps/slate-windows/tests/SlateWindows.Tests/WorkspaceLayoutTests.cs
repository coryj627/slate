// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// WB-16 (#1301, contract 40 AR-55): reopening a closed tab whose file is
/// gone speaks the tab as its group's strip names it once the tab is marked
/// missing. The name was composed before the mark, so it lacked the state
/// the strip reads ("missing from disk") — two different names for one
/// tab.
/// </summary>
public sealed class WorkspaceLayoutTests
{
    [Fact]
    public void AMissingFileReopenNamesTheTabAsTheStripReadsItAfterMarking()
    {
        using var fixture = FixtureVault.Create(0, "workspace-layout");
        Directory.CreateDirectory(Path.Combine(fixture.Root, "a"));
        Directory.CreateDirectory(Path.Combine(fixture.Root, "b"));
        File.WriteAllText(Path.Combine(fixture.Root, "a", "notes.md"), "# A\n");
        string gone = Path.Combine(fixture.Root, "b", "notes.md");
        File.WriteAllText(gone, "# B\n");
        using VaultSession session = VaultSession.OpenFilesystem(fixture.Root);
        using (var cancel = new CancelToken())
        {
            session.ScanInitial(cancel);
        }

        var announced = new List<A11yEvent>();
        using var workspace = new WorkspaceViewModel(
            session, fixture.Root, () => [], announced.Add,
            dirtyCloseDecision: _ => WorkspaceDirtyNavigationDecision.Save,
            startInteractionBackgroundWork: false);
        workspace.OpenPath("a/notes.md");
        workspace.OpenPath("b/notes.md", WorkspaceOpenTarget.NewTab);
        WorkspaceGroupViewModel group = workspace.ActiveGroup;
        Assert.Equal(2, group.Tabs.Count);
        Assert.Equal(group.Tabs[0].Title, group.Tabs[1].Title);
        workspace.CloseTabCommand.Execute(group.ActiveTab);
        File.Delete(gone);
        announced.Clear();

        workspace.ReopenClosedTabCommand.Execute(null);

        WorkspaceTabViewModel reopened = Assert.IsType<WorkspaceTabViewModel>(group.ActiveTab);
        Assert.True(reopened.IsMissingFromDisk, "premise: the reopened tab was not marked missing");
        var missing = Assert.Single(announced.OfType<A11yEvent.ReopenTargetMissing>());
        Assert.Equal(group.SpokenNameOf(reopened), missing.Filename);
    }
}
