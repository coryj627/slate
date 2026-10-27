// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// WB-10 (#1293, plan 43 21b; design
/// <c>docs/plans/43_mac_backlog_delivery/21_change_feed_design.md</c> §4.3–§4.7,
/// §6): the Windows host of core's change feed. Another Slate session on the
/// same vault — another window, or <c>slate-cli</c> — is a second
/// <see cref="VaultSession"/> with its own connection to the shared cache, as
/// a second process is. The vault watcher stays off for the process
/// (<c>Support/VaultWatcherSwitch.cs</c>), so another session's writes land on
/// the Refresh a fact presses; the watcher fact lifts it.
/// </summary>
public sealed partial class RescanTests
{
    /// <summary>The owner's decision Q7 retired AR-30 (the TGC-17 rule: its
    /// witness, <c>AnotherSessionsChangesAreShownButNotCounted</c>, is
    /// rewritten first). Another session's modify, create and delete are
    /// applied — the open tab reloads, the deleted note's tab is marked
    /// missing, the tree and Quick Open follow — and an explicit Refresh
    /// counts them exactly. Its window is acknowledged, so the next Refresh
    /// counts nothing. Under Q2 a foreground run applies another session's
    /// change silently: it speaks only its own scan's.</summary>
    [Fact]
    public void AnotherSessionsChangesAreAppliedAndCountedExactly() => RunSta(() =>
    {
        using var h = new Harness("two-sessions", ("mod.md", "before\n"), ("gone.md", "gone\n"));
        WorkspaceTabViewModel mod = h.Open("mod.md");
        WorkspaceTabViewModel gone = h.Open("gone.md");
        using (VaultSession other = VaultSession.OpenFilesystem(h.Root))
        {
            _ = other.SaveText("mod.md", "changed by another window\n", null);
            _ = other.CreateExclusive("new.md", "# New\n");
            other.DeleteFile("gone.md");
        }

        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));

        Assert.Equal(["Files refreshed. 2 new or changed, 1 removed."], h.Spoken);
        Assert.Equal("changed by another window\n", mod.Text);
        Assert.False(mod.IsDirty);
        Assert.True(gone.IsMissingFromDisk);
        Assert.Contains(h.Sidebar.RootNodes, node => node.Path == "new.md");
        Assert.DoesNotContain(h.Sidebar.RootNodes, node => node.Path == "gone.md");
        Assert.Contains("new.md", h.QuickOpen.FilePathsForTests);
        Assert.DoesNotContain("gone.md", h.QuickOpen.FilePathsForTests);

        h.Events.Clear();
        h.Now += TimeSpan.FromMinutes(1);
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));
        Assert.Equal([NoChanges], h.Spoken);

        using (VaultSession other = VaultSession.OpenFilesystem(h.Root))
        {
            _ = other.SaveText("mod.md", "and again\n", null);
        }

        h.Events.Clear();
        h.Now += TimeSpan.FromMinutes(1);
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Foreground));
        Assert.Empty(h.Events);
        Assert.Equal("and again\n", mod.Text);

        h.Now += TimeSpan.FromMinutes(1);
        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));
        Assert.Equal([NoChanges], h.Spoken);
    });
}
