// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Commands;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// Plan 47 CB-16 (#1830, "a fact covers cancelling from the palette"): the palette's Cancel
/// Import, run through the registrar as the reader runs it, says "Cancelling import…" at
/// once and the import's summary when it settles, in core's words.
/// </summary>
public sealed partial class CommandPaletteTests
{
    [Fact]
    public void CancelImportFromThePaletteSpeaksTheCancellation() => RunSta(() =>
    {
        string root = Path.Combine(Path.GetTempPath(), $"slate-cb16-palette-import-{Guid.NewGuid():N}");
        string source = root + "-source";
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(root, "a.md"), "# A\n");
        File.WriteAllText(Path.Combine(source, "n0000.md"), "# 0\n");
        File.WriteAllText(Path.Combine(source, "n0001.md"), "# 1\n");
        var announced = new List<A11yEvent>();
        using var gate = new ManualResetEventSlim();
        using var lifecycle = new VaultLifecycleViewModel(
            pickVault: () => Task.FromResult<string?>(root),
            enqueueUi: action => action(),
            recentVaultsStore: new RecentVaultsStore(Path.Combine(root + "-device", "recent-vaults.json")),
            announce: announced.Add,
            pickImportSources: () => Task.FromResult<IReadOnlyList<string>>(Directory.GetFiles(source)),
            importWorker: (work, ignoredToken) => Task.Run(() =>
            {
                _ = gate.Wait(TimeSpan.FromSeconds(30));
                work();
            }),
            paletteRecentsStore: new CommandPaletteRecentsStore(
                Path.Combine(root + "-device", "command-palette-recents.json")));
        PumpedDispatcher.PumpUntilDrained(lifecycle.OpenVaultAsync(root), TimeSpan.FromSeconds(30));
        FilesSidebarViewModel sidebar = Assert.IsType<FilesSidebarViewModel>(lifecycle.FileSidebar);
        PumpedDispatcher.PumpUntilDrained(sidebar.TreeRefreshCompletion, TimeSpan.FromSeconds(30));
        sidebar.ImportCommand.Execute(null);
        Assert.True(PumpedDispatcher.PumpUntil(() => sidebar.IsImportRunning), "premise: the import never started copying");

        CommandPaletteViewModel palette = lifecycle.Palette;
        palette.Open();
        palette.Query = "cancel import";
        Assert.True(PumpedDispatcher.PumpUntil(() => !palette.IsRankPending, TimeSpan.FromSeconds(10)), "the palette's rows never published");
        announced.Clear();
        try
        {
            palette.Invoke(Assert.Single(palette.Rows, row => row.Id == ChordTable.Ids.CancelImport));
            PumpedDispatcher.Drain();

            Assert.Single(announced, heard => heard is A11yEvent.ImportCancelling);
            Assert.False(sidebar.CancelImportCommand.CanExecute(null), "Cancel import stayed on after the palette ran it");
        }
        finally
        {
            gate.Set();
        }

        PumpedDispatcher.PumpUntilDrained(sidebar.ImportCompletion, TimeSpan.FromSeconds(30));
        PumpedDispatcher.Drain();
        int cancelling = announced.FindIndex(heard => heard is A11yEvent.ImportCancelling);
        int summary = announced.FindIndex(heard => heard is A11yEvent.ImportFinished);
        Assert.True(summary > cancelling, "the import's summary did not follow \"Cancelling import…\"");
        Assert.True(((A11yEvent.ImportFinished)announced[summary]).Cancelled, "the summary did not say the import was cancelled");
        Assert.Single(announced, heard => heard is A11yEvent.ImportFinished);
    });
}
