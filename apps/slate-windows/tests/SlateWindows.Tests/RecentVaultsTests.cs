// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Input;
using SlateWindows.Canvas;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// WB-12 (WD7): a recent vault whose folder holds nothing but Slate's own
/// data (a real <c>.slate</c> folder; the shell's <c>desktop.ini</c> and
/// <c>Thumbs.db</c> ignored) is not opened as an empty vault unasked. Open
/// Recent asks, as mac's alert offers Open Anyway; No says what the folder
/// holds in observable terms — never a claim that the vault was renamed or
/// replaced — keeps the entry and leaves the keys on the welcome view, and
/// Yes opens it (a vault opened and left empty is the same shape). A folder
/// with any other entry opens as before, and a missing one still asks
/// whether to remove it.
/// </summary>
public sealed class RecentVaultsTests
{
    [Fact]
    public void OpeningARecentFolderThatHoldsOnlySlateDataSaysSoAndKeepsTheEntry() => RunSta(() =>
    {
        using var shell = new WelcomeShell();
        shell.ReleaseParks();
        string emptied = shell.StateFolder("emptied");
        Directory.CreateDirectory(Path.Combine(emptied, ".slate"));
        File.WriteAllText(Path.Combine(emptied, ".slate", "workspace.json"), "{}");
        File.WriteAllText(Path.Combine(emptied, "desktop.ini"), "[.ShellClassInfo]");
        RecentVault recent = shell.RecentVaults.Add(RecentVault.FromPath(emptied))[0];
        IInputElement before = Keyboard.FocusedElement;
        shell.Posted.Clear();

        shell.Lifecycle.OpenRecentCommand.Execute(recent);
        shell.WaitForTheOpenToEnd();

        string expected = $"This folder holds only Slate's data (.slate); the vault's files are not here: {emptied}";
        Assert.Equal(expected, shell.Lifecycle.StatusText);
        RenderedAnnouncement spoken = Assert.Single(shell.Posted);
        Assert.Equal(expected, spoken.Text);
        Assert.Empty(shell.OpenRequests);
        Assert.Equal(1, shell.SlateOnlyAsks);
        Assert.Equal(0, shell.ConfirmCount);
        Assert.False(shell.Lifecycle.IsVaultOpen);
        Assert.Contains(shell.RecentVaults.Load(), entry => string.Equals(entry.Path, emptied, StringComparison.OrdinalIgnoreCase));
        Assert.True(shell.Shell.WelcomeRoot.IsVisible, "the welcome view gave way");
        Assert.Same(before, Keyboard.FocusedElement);
    });

    [Fact]
    public void ARecentFolderThatHoldsOnlySlateDataOpensWhenTheReaderSaysSo() => RunSta(() =>
    {
        using var shell = new WelcomeShell();
        shell.ReleaseParks();
        shell.OpenSlateOnlyAnyway = true;
        string empty = shell.StateFolder("empty-vault");
        Directory.CreateDirectory(Path.Combine(empty, ".slate"));
        RecentVault recent = shell.RecentVaults.Add(RecentVault.FromPath(empty))[0];

        shell.Lifecycle.OpenRecentCommand.Execute(recent);
        shell.WaitForTheOpenToEnd();

        Assert.Equal(1, shell.SlateOnlyAsks);
        Assert.Equal([empty], shell.OpenRequests);
        Assert.True(shell.Lifecycle.IsVaultOpen);
    });

    [Theory]
    [InlineData("a .slate file, not a folder")]
    [InlineData("a note beside .slate")]
    public void OnlyARealSlateFolderAloneIsAsked(string shape) => RunSta(() =>
    {
        using var shell = new WelcomeShell();
        shell.ReleaseParks();
        string folder = shell.StateFolder("shape");
        if (shape == "a .slate file, not a folder")
        {
            File.WriteAllText(Path.Combine(folder, ".slate"), "not a folder");
            Assert.False(VaultLifecycleViewModel.HoldsOnlySlateData(folder));
            return;
        }

        Directory.CreateDirectory(Path.Combine(folder, ".slate"));
        File.WriteAllText(Path.Combine(folder, "note.md"), "# Note\n");
        RecentVault recent = shell.RecentVaults.Add(RecentVault.FromPath(folder))[0];

        shell.Lifecycle.OpenRecentCommand.Execute(recent);
        shell.WaitForTheOpenToEnd();

        Assert.Equal(0, shell.SlateOnlyAsks);
        Assert.Equal([folder], shell.OpenRequests);
    });

    [Fact]
    public void OpeningARecentFolderWithFilesOpensAsToday() => RunSta(() =>
    {
        using var shell = new WelcomeShell(WelcomeShell.TheFixtureVault);
        shell.ReleaseParks();
        Directory.CreateDirectory(Path.Combine(shell.VaultRoot, ".slate"));
        RecentVault recent = shell.Lifecycle.RecentVaults[0];
        Assert.Equal(shell.VaultRoot, recent.Path);

        shell.Lifecycle.OpenRecentCommand.Execute(recent);
        shell.WaitForTheOpenToEnd();

        Assert.Equal([shell.VaultRoot], shell.OpenRequests);
        Assert.True(shell.Lifecycle.IsVaultOpen);
    });

    [Fact]
    public void AMissingRecentFolderStillAsksToRemove() => RunSta(() =>
    {
        using var shell = new WelcomeShell();
        shell.ReleaseParks();
        string missing = shell.StateFolder("gone", create: false);
        RecentVault recent = shell.RecentVaults.Add(RecentVault.FromPath(missing))[0];

        shell.Lifecycle.OpenRecentCommand.Execute(recent);
        shell.WaitForTheOpenToEnd();

        Assert.Equal(1, shell.ConfirmCount);
        Assert.Equal($"Vault not found: {missing}", shell.Lifecycle.StatusText);
        Assert.Empty(shell.OpenRequests);
        Assert.Contains(shell.RecentVaults.Load(), entry => string.Equals(entry.Path, missing, StringComparison.OrdinalIgnoreCase));
    });

    private static void RunSta(Action body)
    {
        Func<bool> priorOverlayProbe = CanvasSurfaceView.ShellOverlayIsOpen;
        try
        {
            StaThread.RunPumped(body, TimeSpan.FromSeconds(90), "STA test body timed out.");
        }
        finally
        {
            CanvasSurfaceView.ShellOverlayIsOpen = priorOverlayProbe;
        }
    }
}
