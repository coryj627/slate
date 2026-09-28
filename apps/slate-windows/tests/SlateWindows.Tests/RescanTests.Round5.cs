// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Bases;
using SlateWindows.Canvas;
using SlateWindows.Commands;
using SlateWindows.Reading;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W7-7 PR 7, codex PR 7 round 4: a case-only re-seat keeps a canvas or
/// base tab's document (1); no structural sidebar operation starts during a
/// rescan (2); a reading-mode note the rescan reloads re-projects as the
/// rescan's, awaited and silent (3); every child operation of a grouped
/// dependent is counted (6).
/// </summary>
public sealed partial class RescanTests
{
    private const string MainBase = "filters: 'file.ext == \"md\"'\nviews:\n  - type: table\n    name: Main\n";

    // --- (1) a case-only re-seat keeps a canvas or base tab's document --------

    /// <summary>Round 4, finding 1: a case-only rename of an open board, made
    /// outside Slate, re-seats its tab WITH a live board — attached to the
    /// stored spelling and loaded — never a detached, dead pane.</summary>
    [Fact]
    public void ACaseOnlyReseatKeepsACanvasTabsBoard() => RunSta(() =>
    {
        using var h = new Harness("reseat-canvas", ("board.canvas", OneNodeCanvas));
        if (!h.VolumeAliasesCase())
        {
            return;
        }

        WorkspaceTabViewModel tab = h.Open("board.canvas");
        CanvasDocumentViewModel board = Assert.IsType<CanvasDocumentViewModel>(tab.Canvas);
        h.PumpUntil(() => board.RowFor("first") is not null, "the board's first load");
        File.Move(Path.Combine(h.Root, "board.canvas"), Path.Combine(h.Root, "Board.canvas"));

        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));

        Assert.Equal("Board.canvas", tab.Path);
        Assert.False(tab.IsMissingFromDisk);
        CanvasDocumentViewModel reseated = Assert.IsType<CanvasDocumentViewModel>(tab.Canvas);
        Assert.Equal("Board.canvas", reseated.Path);
        Assert.NotNull(reseated.RowFor("first"));
        Assert.Equal(["Files refreshed. 1 new or changed, 1 removed."], h.Spoken);
    });

    /// <summary>Round 4, finding 1, the base arm: the re-seated tab keeps a
    /// live base document on the stored spelling, its view run.</summary>
    [Fact]
    public void ACaseOnlyReseatKeepsABaseTabsDocument() => RunSta(() =>
    {
        using var h = new Harness("reseat-base", ("notes.base", MainBase), ("a.md", "# A\n"));
        if (!h.VolumeAliasesCase())
        {
            return;
        }

        WorkspaceTabViewModel tab = h.Open("notes.base");
        h.PumpUntil(() => tab.Base?.State == BaseLoadState.Ready, "the base's first load");
        File.Move(Path.Combine(h.Root, "notes.base"), Path.Combine(h.Root, "Notes.base"));

        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));

        Assert.Equal("Notes.base", tab.Path);
        Assert.False(tab.IsMissingFromDisk);
        BaseDocumentViewModel reseated = Assert.IsType<BaseDocumentViewModel>(tab.Base);
        Assert.Equal("Notes.base", reseated.Path);
        Assert.Equal(BaseLoadState.Ready, reseated.State);
        Assert.Equal(["a.md"], BaseRows(tab));
        Assert.Equal(["Files refreshed. 1 new or changed, 1 removed."], h.Spoken);
    });

    /// <summary>Round 4, finding 1: the re-seated board's or base's load is
    /// the run's own operation — awaited, and its failure one counted error
    /// ("1 error"), at the stored spelling.</summary>
    [Theory]
    [InlineData("canvas")]
    [InlineData("base")]
    public void ACaseOnlyReseatedDocumentsFailedLoadIsCounted(string kind) => RunSta(() =>
    {
        (string Before, string After, string Text) file = kind == "canvas"
            ? ("board.canvas", "Board.canvas", OneNodeCanvas)
            : ("notes.base", "Notes.base", MainBase);
        using var h = new Harness($"reseat-{kind}-fails", (file.Before, file.Text), ("a.md", "# A\n"));
        if (!h.VolumeAliasesCase())
        {
            return;
        }

        WorkspaceTabViewModel tab = h.Open(file.Before);
        h.PumpUntil(
            () => kind == "canvas"
                ? tab.Canvas?.RowFor("first") is not null
                : tab.Base?.State == BaseLoadState.Ready,
            $"the {kind}'s first load");
        var failedAt = new List<string>();
        h.Workspace.RescanPublicationForTests = async (reloading, path, reload) =>
        {
            if (reloading == kind)
            {
                failedAt.Add(path);
                throw new IOException($"injected re-seated {kind} load failure");
            }

            await reload();
        };
        File.Move(Path.Combine(h.Root, file.Before), Path.Combine(h.Root, file.After));

        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));

        Assert.Equal([file.After], failedAt);
        A11yEvent.VaultRescanIncomplete incomplete =
            Assert.IsType<A11yEvent.VaultRescanIncomplete>(Assert.Single(h.Events));
        Assert.Equal(1UL, incomplete.Errors);
    });

    // --- (2) no structural sidebar operation starts during a rescan ------------

    /// <summary>A rescan parked in its scan, the sidebar's structural
    /// commands' state recorded around it.</summary>
    private static void DuringAParkedRescan(Harness h, Action<Harness> during)
    {
        int scans = h.ScanCalls;
        h.ParkAfterCall = scans + 1;
        Task run = h.Lifecycle.RescanAsync(RescanReason.Explicit);
        h.Context.RunUntil(() => h.Parked.IsSet, "the scan parking");
        during(h);
        h.Release.Set();
        h.Context.Await(run);
    }

    private static string? ReasonOf(System.Windows.Input.ICommand command) =>
        (command as IUnavailableReason)?.UnavailableReason;

    /// <summary>Round 4, finding 2: an Import requested while a rescan runs
    /// is refused — unavailable, saying why, and a direct Execute starts
    /// nothing — and available again once the rescan ends; its state is
    /// requeried at both boundaries.</summary>
    [Fact]
    public void AnImportCannotStartWhileARescanRuns() => RunSta(() =>
    {
        var picker = new TaskCompletionSource<IReadOnlyList<string>>();
        using var h = new Harness("import-during-rescan", pickImportSources: () => picker.Task, files: [("alpha.md", "# Alpha\n")]);
        int changes = 0;
        h.Sidebar.ImportCommand.CanExecuteChanged += (_, _) => changes++;

        DuringAParkedRescan(h, h =>
        {
            Assert.True(changes > 0, "the rescan's start requeried Import");
            Assert.False(h.Sidebar.ImportCommand.CanExecute(null));
            Assert.Equal(SlateCommandRegistrar.StructuralMutationBusyReason, ReasonOf(h.Sidebar.ImportCommand));
            h.Sidebar.ImportCommand.Execute(null);
            Assert.False(h.Sidebar.IsImporting);
        });

        Assert.True(h.Sidebar.ImportCommand.CanExecute(null));
        Assert.Null(ReasonOf(h.Sidebar.ImportCommand));
    });

    /// <summary>Round 4, finding 2, the Delete arm.</summary>
    [Fact]
    public void ADeleteCannotStartWhileARescanRuns() => RunSta(() =>
    {
        using var h = new Harness("delete-during-rescan", ("alpha.md", "# Alpha\n"), ("beta.md", "# Beta\n"));
        bool confirmed = false;
        h.Sidebar.ConfirmRecycle = _ =>
        {
            confirmed = true;
            return false;
        };
        h.Sidebar.SelectedNode = Assert.Single(h.Sidebar.RootNodes, node => node.Path == "beta.md");

        DuringAParkedRescan(h, h =>
        {
            Assert.False(h.Sidebar.DeleteCommand.CanExecute(null));
            Assert.Equal(SlateCommandRegistrar.StructuralMutationBusyReason, ReasonOf(h.Sidebar.DeleteCommand));
            h.Sidebar.DeleteCommand.Execute(null);
            Assert.False(h.Sidebar.IsTrashing);
        });

        Assert.False(confirmed);
        Assert.True(h.Sidebar.DeleteCommand.CanExecute(null));
    });

    /// <summary>Round 4, finding 2, the batch-trash arm.</summary>
    [Fact]
    public void ABatchTrashCannotStartWhileARescanRuns() => RunSta(() =>
    {
        using var h = new Harness("batch-trash-during-rescan", ("alpha.md", "# Alpha\n"), ("beta.md", "# Beta\n"));
        bool confirmed = false;
        h.Sidebar.ConfirmRecycle = _ =>
        {
            confirmed = true;
            return false;
        };
        Assert.Single(h.Sidebar.RootNodes, node => node.Path == "alpha.md").IsBatchSelected = true;
        Assert.Single(h.Sidebar.RootNodes, node => node.Path == "beta.md").IsBatchSelected = true;

        DuringAParkedRescan(h, h =>
        {
            Assert.False(h.Sidebar.BatchTrashCommand.CanExecute(null));
            Assert.Equal(SlateCommandRegistrar.StructuralMutationBusyReason, ReasonOf(h.Sidebar.BatchTrashCommand));
            h.Sidebar.BatchTrashCommand.Execute(null);
            Assert.False(h.Sidebar.IsTrashing);
        });

        Assert.False(confirmed);
        Assert.True(h.Sidebar.BatchTrashCommand.CanExecute(null));
    });

    // --- (3) a reloaded reading-mode note re-projects as the rescan's --------

    /// <summary>Round 4, finding 3: a reading-mode note changed outside Slate
    /// is reloaded in place; its new reading model's projection is the
    /// rescan's — the document re-sync does not complete while that fetch is
    /// parked, and completes once it publishes.</summary>
    [Fact]
    public void AReloadedReadingNotesProjectionHoldsTheDocumentReSync() => RunSta(() => PumpedDispatcher.Run(() =>
    {
        using var w = new BackgroundReadingWorkspace("reading-reload-parked");
        File.WriteAllText(Path.Combine(w.Root, "host.md"), "# Host\n\n![[embedded]]\n\nChanged outside Slate.\n");
        using (var scan = new CancelToken())
        {
            _ = w.Session.Rescan(scan);
        }

        using var parked = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);
        w.HostTab.ReadingCreatedForTests = reading => reading.FetchFaultForTests = () =>
        {
            parked.Set();
            _ = release.Wait(TimeSpan.FromSeconds(30));
            return null;
        };
        w.Announced.Clear();
        using var cancellation = new CancellationTokenSource();

        Task<WorkspaceViewModel.RescanDocumentsOutcome> documents = w.ReSyncHostAsync(cancellation.Token);
        Assert.True(PumpedDispatcher.PumpUntil(() => parked.IsSet), "the reloaded note's projection parking");
        PumpedDispatcher.Drain();
        Assert.False(documents.IsCompleted, "the re-sync completed before the reloaded note's projection published");

        release.Set();
        Assert.True(PumpedDispatcher.PumpUntil(() => documents.IsCompleted), "the document re-sync");
        Assert.Equal(0UL, documents.Result.Failed);
        Assert.Empty(w.Announced);
    }));

    /// <summary>Round 4, finding 3: the reloaded note's projection FAILS —
    /// counted as one failed operation of the re-sync, and Reading speaks
    /// nothing of its own.</summary>
    [Fact]
    public void AReloadedReadingNotesFailedProjectionIsCountedAndSilent() => RunSta(() => PumpedDispatcher.Run(() =>
    {
        using var w = new BackgroundReadingWorkspace("reading-reload-fails");
        File.WriteAllText(Path.Combine(w.Root, "host.md"), "# Host\n\n![[embedded]]\n\nChanged outside Slate.\n");
        using (var scan = new CancelToken())
        {
            _ = w.Session.Rescan(scan);
        }

        w.HostTab.ReadingCreatedForTests = reading =>
            reading.FetchFaultForTests = () => new IOException("injected reading fetch failure");
        w.Announced.Clear();
        using var cancellation = new CancellationTokenSource();

        Task<WorkspaceViewModel.RescanDocumentsOutcome> documents = w.ReSyncHostAsync(cancellation.Token);
        Assert.True(
            PumpedDispatcher.PumpUntil(() => documents.IsCompleted, TimeSpan.FromSeconds(20)),
            "the document re-sync");
        // The reloaded note's projection settles after the re-sync without the fix.
        PumpedDispatcher.PumpUntil(() => false, TimeSpan.FromSeconds(1));

        Assert.Equal(1UL, documents.Result.Failed);
        Assert.Empty(w.Announced);
    }));

    /// <summary>Round 4, finding 3: the reloaded note's projection runs under
    /// the RUN's token — a close cancelling the run while that fetch is
    /// parked ends the re-sync cancelled, and the released fetch stops at its
    /// cancellation boundary: nothing publishes, nothing fails, nothing is
    /// said.</summary>
    [Fact]
    public void AReloadedReadingNotesProjectionTakesTheRunsToken() => RunSta(() => PumpedDispatcher.Run(() =>
    {
        using var w = new BackgroundReadingWorkspace("reading-reload-cancelled");
        File.WriteAllText(Path.Combine(w.Root, "host.md"), "# Host\n\n![[embedded]]\n\nChanged outside Slate.\n");
        using (var scan = new CancelToken())
        {
            _ = w.Session.Rescan(scan);
        }

        ReadingContentViewModel? created = null;
        using var parked = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);
        w.HostTab.ReadingCreatedForTests = reading =>
        {
            created = reading;
            reading.FetchFaultForTests = () =>
            {
                parked.Set();
                _ = release.Wait(TimeSpan.FromSeconds(30));
                return null;
            };
        };
        w.Announced.Clear();
        using var cancellation = new CancellationTokenSource();

        Task<WorkspaceViewModel.RescanDocumentsOutcome> documents = w.ReSyncHostAsync(cancellation.Token);
        Assert.True(PumpedDispatcher.PumpUntil(() => parked.IsSet), "the reloaded note's projection parking");
        cancellation.Cancel();
        Assert.True(PumpedDispatcher.PumpUntil(() => documents.IsCompleted), "the cancelled re-sync ending");
        Assert.True(documents.IsCanceled);

        release.Set();
        ReadingContentViewModel reading = Assert.IsType<ReadingContentViewModel>(created);
        Assert.True(
            PumpedDispatcher.PumpUntil(() => reading.WhenRefreshWorkDrained().IsCompleted),
            "the released fetch");
        PumpedDispatcher.Drain();

        Assert.Null(reading.Document);
        Assert.Null(reading.LastTerminalFailureForTests);
        Assert.Empty(w.Announced);
    }));

    // --- (6) every child of a grouped dependent is counted ---------------------

    /// <summary>Round 4, finding 6: two open bases whose re-runs both fail
    /// are TWO failed operations — "2 errors" — never one for their group.</summary>
    [Fact]
    public void TwoFailedBasePublicationsAreTwoErrors() => RunSta(() =>
    {
        using var h = new Harness(
            "two-bases-fail",
            ("One.base", MainBase),
            ("Two.base", MainBase),
            ("a.md", "# A\n"));
        WorkspaceTabViewModel one = h.Open("One.base");
        WorkspaceTabViewModel two = h.Open("Two.base");
        h.PumpUntil(
            () => one.Base?.State == BaseLoadState.Ready && two.Base?.State == BaseLoadState.Ready,
            "both bases' first loads");
        h.Workspace.RescanPublicationForTests = async (publishing, _, publish) =>
        {
            if (publishing == "base-refresh")
            {
                throw new IOException("injected base publication failure");
            }

            await publish();
        };
        h.Write("b.md", "# B\n");

        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));

        A11yEvent.VaultRescanIncomplete incomplete =
            Assert.IsType<A11yEvent.VaultRescanIncomplete>(Assert.Single(h.Events));
        Assert.Equal(2UL, incomplete.Errors);
    });

    /// <summary>Round 4, finding 6, the graph arm: the graph document's
    /// probe and the Connections leaf's are two operations — both failing
    /// is "2 errors".</summary>
    [Fact]
    public void AFailedGraphAndConnectionsProbeAreTwoErrors() => RunSta(() =>
    {
        using Harness h = DependentsHarness("graph-connections-fail");
        h.Workspace.OpenGraph();
        h.PumpUntil(() => h.Workspace.GraphDocument is { IsRetired: false }, "the graph tab");
        for (int turn = 0; turn < 20; turn++)
        {
            h.Context.Drain();
            PumpedDispatcher.Drain();
            Thread.Sleep(5);
        }

        h.Events.Clear();
        h.Workspace.RescanPublicationForTests = async (publishing, _, publish) =>
        {
            if (publishing is "graph-document" or "connections")
            {
                throw new IOException($"injected {publishing} publication failure");
            }

            await publish();
        };
        h.Write("embedded.md", "Embedded after, changed outside Slate.\n");

        h.Context.Await(h.Lifecycle.RescanAsync(RescanReason.Explicit));

        A11yEvent.VaultRescanIncomplete incomplete =
            Assert.IsType<A11yEvent.VaultRescanIncomplete>(Assert.Single(h.Events));
        Assert.Equal(2UL, incomplete.Errors);
    });
}
