// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows.Controls;
using System.Windows.Input;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W7-7 PR 7 (#1252, R-9) over PR 4 (#1247, R-5): the rescan's tree refresh
/// republishes the Files tree under the reader — fresh nodes for every row —
/// through the same publication the organic refresh takes, so the tree's
/// landing and the selection's reconcile keep the keys on the reader's row.
/// </summary>
public sealed partial class FilesRegionLandingTests
{
    /// <summary>With the keys on the selected note, a note created outside
    /// Slate that sorts ahead of it, published by the rescan's refresh,
    /// leaves them on the SAME note's fresh row — never on the bare tree and
    /// never on the first row — and the refresh opens nothing and says
    /// nothing.</summary>
    [Fact]
    public void ARescansTreeRefreshKeepsTheKeysOnTheReadersRow() => RunSta(() =>
    {
        using var host = new Host();
        host.Initialize();
        FileTreeNodeViewModel reader = host.Sidebar.RootNodes[^1];
        host.Sidebar.SelectedNode = reader;
        host.Pane.UpdateLayout();
        PumpedDispatcher.Drain();
        Assert.True(host.Shell.LandOnFilesTree());
        PumpedDispatcher.Drain();
        Assert.Same(reader, FocusedNode());
        var opened = new List<string>();
        host.Sidebar.OpenTargetRequested += (_, request) => opened.Add(request.Path);
        host.ForgetFocusChanges();
        host.Announced.Clear();

        File.WriteAllText(Path.Combine(host.Root, "0-created-outside.md"), "# Outside\n");
        using (var scan = new CancelToken())
        {
            _ = host.Session.Rescan(scan);
        }

        Task<ulong> refresh = host.Sidebar.RefreshForRescanAsync(reportCount: false, CancellationToken.None);
        Assert.True(PumpedDispatcher.PumpUntil(() => refresh.IsCompleted), "the rescan's tree refresh");
        host.Pane.UpdateLayout();
        PumpedDispatcher.Drain();

        Assert.Equal(0UL, refresh.Result);
        Assert.Equal("0-created-outside.md", host.Sidebar.RootNodes[0].Path);
        FileTreeNodeViewModel? focused = FocusedNode();
        Assert.True(
            focused is not null && focused.Path == reader.Path,
            $"the rescan's refresh left the keys on {Keyboard.FocusedElement}, not on {reader.Path}'s row");
        Assert.NotSame(reader, focused);
        Assert.Equal(reader.Path, host.Sidebar.SelectedNode?.Path);
        Assert.IsAssignableFrom<TreeViewItem>(Keyboard.FocusedElement);
        Assert.Empty(opened);
        Assert.Empty(host.Announced);
        host.AssertTreeNeverFocused();
    });

    private sealed partial class Host
    {
        public VaultSession Session => _session ?? throw new InvalidOperationException("The host is not initialized.");

        public string Root => _fixture.Root;
    }
}
