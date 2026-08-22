// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// #1077 Phase 4 — the host half of canonical file identity
/// (docs/plans/32 contracts I6 and I8). The scenario these pin is the
/// W5-3 codex round-2 stale overwrite: a parked tab of a deleted
/// <c>Ghost.md</c>, a new <c>ghost.md</c> created (one physical file on
/// NTFS/APFS), every ordinal comparison blind to it, and the parked
/// tab's hashless save burying the new note. I6 re-seats the tab; I8
/// makes the hashless save a create that cannot land on an existing
/// file even when no re-seat has run.
/// </summary>
public sealed class CanonicalIdentityTests
{
    /// <summary>I6: volume-probed — the scenario can only arise where
    /// the volume aliases spellings, so the fact is vacuous elsewhere.
    /// Mirrors core's probe-not-assume rule.</summary>
    [Fact]
    public void AMissingTabReseatsWhenItsFileComesBackUnderAnotherSpelling()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "identity-reseat");
        File.WriteAllText(Path.Combine(fixture.Root, "Probe.md"), "p");
        bool aliasing = File.Exists(Path.Combine(fixture.Root, "probe.md"));
        File.Delete(Path.Combine(fixture.Root, "Probe.md"));
        if (!aliasing)
        {
            return;
        }

        using VaultSession session = OpenScannedSession(fixture.Root);
        using var workspace = NewWorkspace(session, fixture.Root);

        // A parked tab of a note that is gone — opened, then swept by the
        // lifecycle's Deleted arm, which is exactly what a persistence
        // restore of a vanished note produces. Its load failed, so it
        // carries NO content hash.
        workspace.OpenPath("Ghost.md");
        WorkspaceTabViewModel tab = workspace.ActiveGroup.ActiveTab!;
        workspace.InvalidatePath("Ghost.md");
        Assert.True(tab.IsMissingFromDisk);

        // The user creates the note again under another spelling.
        session.CreateExclusive("ghost.md", "fresh\n");
        workspace.ReseatMissingTabs();

        Assert.Equal("ghost.md", tab.Path);
        Assert.False(tab.IsMissingFromDisk);
        Assert.Equal("fresh\n", tab.Text);

        // Its next save is a compare-and-swap on the reloaded bytes: it
        // extends the new note instead of burying it.
        tab.Text = "fresh\nmore\n";
        Assert.True(tab.Save(), tab.Status);
        Assert.Equal(
            "fresh\nmore\n",
            File.ReadAllText(Path.Combine(fixture.Root, "ghost.md")));
    }

    /// <summary>I6: a DIRTY missing tab keeps its buffer — it is
    /// retargeted to the stored spelling (so its save names the right
    /// identity) and never reloaded over the user's text.</summary>
    [Fact]
    public void ADirtyMissingTabIsRetargetedButNeverReloaded()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "identity-dirty");
        File.WriteAllText(Path.Combine(fixture.Root, "Probe.md"), "p");
        bool aliasing = File.Exists(Path.Combine(fixture.Root, "probe.md"));
        File.Delete(Path.Combine(fixture.Root, "Probe.md"));
        if (!aliasing)
        {
            return;
        }

        using VaultSession session = OpenScannedSession(fixture.Root);
        using var workspace = NewWorkspace(session, fixture.Root);
        workspace.OpenPath("Ghost.md");
        WorkspaceTabViewModel tab = workspace.ActiveGroup.ActiveTab!;
        workspace.InvalidatePath("Ghost.md");
        tab.Text = "unsaved\n";
        Assert.True(tab.IsDirty);

        session.CreateExclusive("ghost.md", "fresh\n");
        workspace.ReseatMissingTabs();

        Assert.Equal("ghost.md", tab.Path);
        Assert.Equal("unsaved\n", tab.Text);
        Assert.True(tab.IsDirty);
        // And its hashless save is a create onto an occupied path (I8):
        // a conflict, never an overwrite of the fresh note.
        Assert.False(tab.Save());
        Assert.StartsWith("Save blocked", tab.Status, StringComparison.Ordinal);
        Assert.Equal(
            "fresh\n",
            File.ReadAllText(Path.Combine(fixture.Root, "ghost.md")));
    }

    /// <summary>I8: a tab with no content hash saves as a CREATE — it
    /// never lands on an existing file, even when no re-seat sweep has
    /// run (the external create between sweep and save), and a free
    /// path is still created as the ordinary first save. Not volume-
    /// dependent: the same spelling is enough.</summary>
    [Fact]
    public void AHashlessSaveNeverLandsOnAnExistingFile()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "identity-hashless");
        using VaultSession session = OpenScannedSession(fixture.Root);
        using var workspace = NewWorkspace(session, fixture.Root);

        // A tab that never loaded bytes: no content hash.
        workspace.OpenPath("Late.md");
        WorkspaceTabViewModel tab = workspace.ActiveGroup.ActiveTab!;
        tab.Text = "mine\n";
        // Someone lands a file there before the save.
        session.CreateExclusive("Late.md", "theirs\n");

        Assert.False(tab.Save());
        Assert.StartsWith("Save blocked", tab.Status, StringComparison.Ordinal);
        Assert.Equal(
            "theirs\n",
            File.ReadAllText(Path.Combine(fixture.Root, "Late.md")));

        // A free path: the ordinary first save of a new tab still creates.
        workspace.OpenPath("Free.md", WorkspaceOpenTarget.NewTab);
        WorkspaceTabViewModel free = workspace.ActiveGroup.ActiveTab!;
        free.Text = "new\n";
        Assert.True(free.Save(), free.Status);
        Assert.Equal(
            "new\n",
            File.ReadAllText(Path.Combine(fixture.Root, "Free.md")));
        // And the tab now carries a hash: its NEXT save is a CAS save.
        free.Text = "new\nmore\n";
        Assert.True(free.Save(), free.Status);
        Assert.Equal(
            "new\nmore\n",
            File.ReadAllText(Path.Combine(fixture.Root, "Free.md")));
    }

    /// <summary>I6 wiring: the lifecycle's publication handler calls the
    /// re-seat for Created AND Renamed events. A source pin — the
    /// handler is private and its events come from a live watcher.</summary>
    [Fact]
    public void TheLifecycleReseatsMissingTabsOnCreatedAndRenamedPublications()
    {
        string source = File.ReadAllText(FindSource("VaultLifecycleViewModel.cs"));
        int handler = source.IndexOf("private void HandleFileChange(", StringComparison.Ordinal);
        Assert.True(handler >= 0, "HandleFileChange must exist");
        string body = source[handler..];
        Assert.Contains(
            "@event.Kind is FileChangeKind.Created or FileChangeKind.Renamed",
            body,
            StringComparison.Ordinal);
        Assert.Contains("Workspace?.ReseatMissingTabs();", body, StringComparison.Ordinal);
    }

    private static string FindSource(string fileName)
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string candidate = Path.Combine(
                dir.FullName, "apps", "slate-windows", "src", "SlateWindows", fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
            dir = dir.Parent;
        }
        throw new FileNotFoundException(fileName);
    }

    private static WorkspaceViewModel NewWorkspace(VaultSession session, string root) =>
        new(session, root, () => [], _ => { }, null, null);

    private static VaultSession OpenScannedSession(string root)
    {
        VaultSession session = VaultSession.OpenFilesystem(root);
        using var cancel = new CancelToken();
        session.ScanInitial(cancel);
        return session;
    }
}
