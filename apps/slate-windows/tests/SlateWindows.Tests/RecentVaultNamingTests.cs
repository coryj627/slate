// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Globalization;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// WB-16 (#1311): two recent vaults with one name are told apart by the
/// shortest folder that separates them — "Notes, in Documents" beside
/// "Notes, in Dropbox" — not by two full paths a reader must hear out to the
/// end. The welcome screen's buttons and the vault-opened sentence share the
/// one authority (contract 40 OD-9).
/// </summary>
public sealed class RecentVaultNamingTests
{
    [Fact]
    public void TwoVaultsCalledNotesUnderDifferentParentsSpeakInParent()
    {
        var documents = new RecentVault(@"C:\Users\me\Documents\Notes", "Notes", 1);
        var dropbox = new RecentVault(@"C:\Users\me\Dropbox\Notes", "Notes", 2);
        RecentVault[] all = [documents, dropbox];

        Assert.Equal("Notes, in Documents", RecentVault.SpokenName(documents, all));
        Assert.Equal("Notes, in Dropbox", RecentVault.SpokenName(dropbox, all));
    }

    /// <summary>Where the nearest folders match, the first folder up the tree
    /// that differs names the vault; where no one folder separates it from
    /// every namesake, the run of folders down to its parent does.</summary>
    [Fact]
    public void TwoVaultsCalledNotesSpeakTheFirstDifferingSegmentDeeperInTheTree()
    {
        var work = new RecentVault(@"C:\Work\A\Notes", "Notes", 1);
        var home = new RecentVault(@"C:\Home\A\Notes", "Notes", 2);
        Assert.Equal("Notes, in Work", RecentVault.SpokenName(work, [work, home]));
        Assert.Equal("Notes, in Home", RecentVault.SpokenName(home, [work, home]));

        // Work\A shares A with Home\A and Work with Work\B: only the run
        // tells it apart from both.
        var workB = new RecentVault(@"C:\Work\B\Notes", "Notes", 3);
        RecentVault[] three = [work, home, workB];
        Assert.Equal(@"Notes, in Work\A", RecentVault.SpokenName(work, three));
        Assert.Equal("Notes, in Home", RecentVault.SpokenName(home, three));
        Assert.Equal("Notes, in B", RecentVault.SpokenName(workB, three));
    }

    /// <summary>The welcome button (its converter) and the vault-opened
    /// sentence speak one name.</summary>
    [Fact]
    public async Task TwoVaultsCalledNotesTheWelcomeButtonsAndVaultOpenedMatch()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "recent-vault-naming");
        string documents = Path.Combine(fixture.Root, "Documents", "Notes");
        string dropbox = Path.Combine(fixture.Root, "Dropbox", "Notes");
        Directory.CreateDirectory(documents);
        Directory.CreateDirectory(dropbox);
        var announced = new List<A11yEvent>();
        using var lifecycle = new VaultLifecycleViewModel(
            pickVault: () => Task.FromResult<string?>(null),
            enqueueUi: action => action(),
            confirmRemoveMissingRecent: _ => Task.FromResult(true),
            recentVaultsStore: new RecentVaultsStore(Path.Combine(fixture.Root, "device-state", "recent-vaults.json")),
            announce: announced.Add);
        await lifecycle.OpenVaultAsync(documents);
        lifecycle.CloseVault();
        announced.Clear();

        await lifecycle.OpenVaultAsync(dropbox);

        RecentVault entry = Assert.Single(lifecycle.RecentVaults, vault => vault.Path == dropbox);
        object button = new RecentVaultNameConverter().Convert(
            [entry, lifecycle.RecentVaults, lifecycle.RecentVaults.Count], typeof(string), null!, CultureInfo.InvariantCulture);
        Assert.Equal("Notes, in Dropbox", button);
        Assert.Equal(button, Assert.Single(announced.OfType<A11yEvent.VaultOpened>()).VaultTitle);
    }

    [Fact]
    public void TwoVaultsCalledNotesAUniqueNameStaysBare()
    {
        var alpha = new RecentVault(@"C:\Vaults\Alpha", "Alpha", 1);
        var documents = new RecentVault(@"C:\Users\me\Documents\Notes", "Notes", 2);
        var dropbox = new RecentVault(@"C:\Users\me\Dropbox\Notes", "Notes", 3);

        Assert.Equal("Alpha", RecentVault.SpokenName(alpha, [alpha, documents, dropbox]));
        Assert.Equal("Notes", RecentVault.SpokenName(documents, [alpha, documents]));
    }
}
