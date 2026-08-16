// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Search;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W5-2 SD-4 (#742): reading-view tag activation is rerouted to the
/// tag-scoped search overlay, while the editor tag path keeps the
/// sidebar filter. These facts run the REAL wiring — tab seam →
/// workspace event → vault lifecycle → overlay / sidebar — over a real
/// session, because the divergence is precisely about which seam the
/// gesture travels, and a fake at any joint would let a re-pointed
/// seam pass. The reroute fact fails on every observable if someone
/// re-points reading activation back at the shared editor seam: the
/// overlay stays closed, the editor event fires, the sidebar filter
/// arms, and the residue string speaks.
/// </summary>
public sealed class ReadingTagSearchRerouteTests : IDisposable
{
    private readonly List<string> _roots = [];
    private readonly object _announceGate = new();
    private readonly List<A11yEvent> _announced = [];

    public void Dispose()
    {
        foreach (string root in _roots)
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    /// <summary>
    /// The SD-4 reroute, end to end: the reading seam opens the overlay
    /// with the query cleared and Tag scope armed, the editor seam
    /// stays dark, the sidebar filter stays empty, and no host-composed
    /// string speaks — the overlay's own tag-listing summary (contract
    /// S2) is the only voice on this path.
    /// </summary>
    [Fact]
    public async Task ReadingTagActivationOpensTagScopedSearchNotTheSidebarFilter()
    {
        string root = NewVault("reading-reroute");
        using VaultLifecycleViewModel lifecycle = NewLifecycle(root);
        await lifecycle.OpenVaultAsync(root);
        WorkspaceViewModel workspace = Assert.IsType<WorkspaceViewModel>(lifecycle.Workspace);
        var editorTagEvents = new List<string>();
        workspace.EditorTagActivated += (_, tag) => editorTagEvents.Add(tag);
        workspace.OpenPath("note0.md");
        WorkspaceTabViewModel tab = workspace.ActiveGroup.ActiveTab!;
        Assert.Equal("note0.md", tab.Path);
        await SettleSidebarAsync(lifecycle);
        int announcedBefore = AnnouncedCount();

        // The same internal seam ReadingActivation.cs invokes for a
        // Tag run (ReadingViewTests pins that hop on the built
        // document's own hyperlinks, which needs an STA thread).
        tab.ActivateTagFromReading("atag");

        SearchOverlayViewModel search = lifecycle.Search;
        Assert.True(search.IsOpen);
        Assert.Equal(string.Empty, search.Query);
        Assert.Equal("atag", search.TagScopeName);
        // The editor path stayed dark: no event, no sidebar filter.
        Assert.Empty(editorTagEvents);
        Assert.Equal(string.Empty, lifecycle.FileSidebar!.FilterText);
        // And no host-composed announcement — "Filtered files by tag"
        // belongs to the editor path alone (SD-4).
        Assert.DoesNotContain(
            AnnouncedSince(announcedBefore),
            announcement => announcement is A11yEvent.HostComposed);
    }

    /// <summary>
    /// The half SD-4 must NOT disturb: an editor tag still filters the
    /// sidebar through <c>EditorTagActivated</c> and still speaks the
    /// W0.5-3 residue string — mac's editor renders tags as unclickable
    /// plain text, so this Windows-only affordance has no mac twin to
    /// converge on — and it never touches the search overlay.
    /// </summary>
    [Fact]
    public async Task EditorTagActivationStillFiltersTheSidebarAndSpeaksTheResidueString()
    {
        string root = NewVault("editor-unchanged");
        using VaultLifecycleViewModel lifecycle = NewLifecycle(root);
        await lifecycle.OpenVaultAsync(root);
        WorkspaceViewModel workspace = Assert.IsType<WorkspaceViewModel>(lifecycle.Workspace);
        workspace.OpenPath("note0.md");
        WorkspaceTabViewModel tab = workspace.ActiveGroup.ActiveTab!;
        Assert.Equal("note0.md", tab.Path);
        tab.EditorInteractions!.RefreshMathRangesForTests();
        tab.EditorInteractions.RefreshArtifactCacheForTests();
        await SettleSidebarAsync(lifecycle);
        int announcedBefore = AnnouncedCount();

        int inTag = tab.Text.IndexOf("#atag", StringComparison.Ordinal) + 2;
        Assert.True(tab.EditorInteractions.ActivateAt(inTag));

        Assert.Equal("tag:\"atag\"", lifecycle.FileSidebar!.FilterText);
        A11yEvent.HostComposed residue = Assert.Single(
            AnnouncedSince(announcedBefore).OfType<A11yEvent.HostComposed>());
        Assert.Equal("Filtered files by tag atag.", residue.Text);
        // The reroute is one-directional: the editor path never opens
        // the search overlay.
        Assert.False(lifecycle.Search.IsOpen);
    }

    /// <summary>
    /// The shell's modal gate (SD-4): a refusal — the shell answering
    /// "a sheet is up" — leaves the overlay fully untouched: not
    /// opened, query not cleared, no scope armed. The overlay must
    /// never open invisibly beneath a sheet, and a silently armed tag
    /// scope on a closed overlay would misdirect the next
    /// Ctrl+Shift+F search.
    /// </summary>
    [Fact]
    public async Task ARefusedModalGateLeavesTheOverlayUntouched()
    {
        string root = NewVault("gate-refusal");
        using VaultLifecycleViewModel lifecycle = NewLifecycle(root);
        await lifecycle.OpenVaultAsync(root);
        WorkspaceViewModel workspace = Assert.IsType<WorkspaceViewModel>(lifecycle.Workspace);
        workspace.OpenPath("note0.md");
        WorkspaceTabViewModel tab = workspace.ActiveGroup.ActiveTab!;
        lifecycle.Search.Query = "retained";
        lifecycle.SearchOpenAdmission = () => false;

        tab.ActivateTagFromReading("atag");

        Assert.False(lifecycle.Search.IsOpen);
        Assert.Equal("retained", lifecycle.Search.Query);
        Assert.Null(lifecycle.Search.TagScopeName);

        // The same activation with the way clear opens normally.
        lifecycle.SearchOpenAdmission = () => true;
        tab.ActivateTagFromReading("atag");

        Assert.True(lifecycle.Search.IsOpen);
        Assert.Equal(string.Empty, lifecycle.Search.Query);
        Assert.Equal("atag", lifecycle.Search.TagScopeName);
    }

    // ---- Helpers --------------------------------------------------------

    private VaultLifecycleViewModel NewLifecycle(string root) =>
        new(
            pickVault: () => Task.FromResult<string?>(root),
            enqueueUi: action => action(),
            recentVaultsStore: new RecentVaultsStore(
                Path.Combine(root, "device-state", "recent-vaults.json")),
            announce: Record,
            sessionLoadWorker: work => Task.FromResult(work()));

    private void Record(A11yEvent announcement)
    {
        lock (_announceGate)
        {
            _announced.Add(announcement);
        }
    }

    private int AnnouncedCount()
    {
        lock (_announceGate)
        {
            return _announced.Count;
        }
    }

    private List<A11yEvent> AnnouncedSince(int index)
    {
        lock (_announceGate)
        {
            return [.. _announced.Skip(index)];
        }
    }

    /// <summary>The W1 close barrier settle (the sync-lifecycle suite's
    /// discipline): background tree work publishes announcements, so the
    /// deltas above start from a quiet baseline.</summary>
    private static async Task SettleSidebarAsync(VaultLifecycleViewModel lifecycle)
    {
        if (lifecycle.FileSidebar is FilesSidebarViewModel sidebar)
        {
            await sidebar.TreeRefreshCompletion.WaitAsync(TimeSpan.FromSeconds(20));
        }
    }

    private string NewVault(string label)
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            $"slate-windows-test-tag-reroute-{label}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        File.WriteAllText(
            Path.Combine(root, "note0.md"),
            "# Note 0\n\nBody with #atag inline.\n");
        _roots.Add(root);
        return root;
    }
}
