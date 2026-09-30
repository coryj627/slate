// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using SlateWindows.Commands;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// #1275 codex round 3: a command the palette runs can pump nested
/// dispatcher frames before it returns — the folder picker
/// (<c>OpenFolderDialog.ShowDialog</c>), the unsaved-changes prompt — and
/// the palette is sealed for exactly that long (contract 28 T8, I6). Here a
/// fake command's action runs the nested loop itself, with
/// <see cref="PumpedDispatcher"/>, while the palette's invoke is on the
/// stack — the shape <c>ShowDialog</c> has. Also here: which rank failures
/// resolve the palette and are logged, and which are a cancellation (T4, T5).
/// </summary>
public sealed partial class CommandPaletteTests
{
    /// <summary>
    /// Enter on the row on screen while a newer query's rank is parked and
    /// its count window still open. Inside the command's modal loop the
    /// parked rank completes and posts its rows back, and the count window
    /// runs out — and the palette publishes nothing, moves nothing and says
    /// nothing: the list on screen stays as it was, the palette still up
    /// under the loop (P9). After a success it is dismissed and has said
    /// nothing; after a failure the failure is announced, then the current
    /// query ranks again — its rows, the selection they bring, one count.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NothingThePaletteOwnsPublishesOrSpeaksInsideACommandsModalLoop(bool commandFails) => RunSta(() =>
    {
        LaneHost host = LaneHost.Opened();
        CommandPaletteViewModel palette = host.Palette;
        List<TaskCompletionSource> windows = host.HoldTheCountWindows();
        host.Harness.Announcements.Clear();
        var published = new List<string[]>();
        palette.PropertyChanged += (_, change) =>
        {
            if (change.PropertyName == nameof(CommandPaletteViewModel.Rows))
            {
                published.Add([.. host.Harness.RowIds]);
            }
        };
        if (commandFails)
        {
            host.Harness.Source.InvokeFailures["slate.file.newNote"] =
                new CommandException.ActionFailed("Disk is full.");
        }

        host.Ranker.Park("q");
        palette.Query = "q";
        host.Ranker.WaitUntilEntered("q");
        Task parkedRank = palette.RankCompletion;
        TaskCompletionSource window = Assert.Single(windows);
        string[] onScreen = [.. host.Harness.RowIds];

        (int Published, int Heard, string[] Rows, string? Selected, bool Open, bool Invoking) inside = default;
        host.Harness.Source.OnInvoke = _ =>
        {
            int heard = host.Harness.Announcements.Count;
            host.Ranker.Release("q");
            Assert.True(
                PumpedDispatcher.PumpUntil(() => parkedRank.IsCompleted, TimeSpan.FromSeconds(10)),
                "the parked rank never completed inside the loop");
            window.TrySetResult();
            PumpedDispatcher.PumpUntilDrained(palette.FilterCountCompletion);
            PumpedDispatcher.Drain();
            inside = (
                published.Count,
                host.Harness.Announcements.Count - heard,
                [.. host.Harness.RowIds],
                palette.SelectedId,
                palette.IsOpen,
                palette.IsInvoking);
        };

        palette.InvokeSelected();

        Assert.Equal(0, inside.Published);
        Assert.Equal(0, inside.Heard);
        Assert.Equal(onScreen, inside.Rows);
        Assert.Equal("slate.file.newNote", inside.Selected);
        Assert.True(inside.Open, "the palette stays up under the command's loop (P9)");
        Assert.True(inside.Invoking);
        Assert.False(palette.IsInvoking);
        Assert.Equal(["slate.file.newNote"], host.Harness.Source.Invoked);

        if (!commandFails)
        {
            Assert.False(palette.IsOpen);
            Assert.Empty(host.Harness.Announcements);
            Assert.Single(windows);
            return;
        }

        Assert.True(palette.IsOpen, "a failure leaves the palette open (P9)");
        Assert.Equal(2, windows.Count);
        Assert.True(
            PumpedDispatcher.PumpUntil(() => !palette.IsRankPending, TimeSpan.FromSeconds(10)),
            "the failure never ranked the current query again");
        PumpedDispatcher.Drain();
        Assert.Equal([["slate.nav.quickOpen"]], published);
        windows[1].SetResult();
        PumpedDispatcher.PumpUntilDrained(palette.FilterCountCompletion);
        PumpedDispatcher.Drain();
        Assert.Collection(
            host.Harness.Announcements,
            announced =>
            {
                var failed = Assert.IsType<A11yEvent.PaletteCommandFailed>(announced);
                Assert.Equal(("New Note", "Disk is full."), (failed.Label, failed.Detail));
            },
            announced => Assert.Equal(
                "Quick Open",
                Assert.IsType<A11yEvent.PaletteCommandSelected>(announced).Label),
            announced =>
            {
                var count = Assert.IsType<A11yEvent.PaletteFilterCount>(announced);
                Assert.Equal((1u, "q"), (count.Count, count.Query));
            });
    });

    /// <summary>
    /// The other ordering: the query's rows have published but its count
    /// window is still open when Enter runs the command. The window runs out
    /// inside the command's loop and the count is not spoken there. After a
    /// success nothing more is said; after a failure the unspoken count is
    /// owed, so the query ranks again and its count is spoken once.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ACountStillInItsWindowIsNotSpokenInsideACommandsLoop(bool commandFails) => RunSta(() =>
    {
        LaneHost host = LaneHost.Opened();
        CommandPaletteViewModel palette = host.Palette;
        List<TaskCompletionSource> windows = host.HoldTheCountWindows();
        palette.Query = "q";
        Assert.True(
            PumpedDispatcher.PumpUntil(() => !palette.IsRankPending, TimeSpan.FromSeconds(10)),
            "the rank never published");
        PumpedDispatcher.Drain();
        Assert.Equal("slate.nav.quickOpen", palette.SelectedId);
        TaskCompletionSource window = Assert.Single(windows);
        host.Harness.Announcements.Clear();
        if (commandFails)
        {
            host.Harness.Source.InvokeFailures["slate.nav.quickOpen"] =
                new CommandException.ActionFailed("Disk is full.");
        }

        int heardInside = -1;
        host.Harness.Source.OnInvoke = _ =>
        {
            window.TrySetResult();
            PumpedDispatcher.PumpUntilDrained(palette.FilterCountCompletion);
            PumpedDispatcher.Drain();
            heardInside = host.Harness.Announcements.Count;
        };

        palette.InvokeSelected();

        Assert.Equal(0, heardInside);
        if (!commandFails)
        {
            Assert.False(palette.IsOpen);
            Assert.Empty(host.Harness.Announcements);
            return;
        }

        Assert.Equal(2, windows.Count);
        Assert.True(
            PumpedDispatcher.PumpUntil(() => !palette.IsRankPending, TimeSpan.FromSeconds(10)),
            "the failure never ranked the current query again");
        windows[1].SetResult();
        PumpedDispatcher.PumpUntilDrained(palette.FilterCountCompletion);
        PumpedDispatcher.Drain();
        Assert.Collection(
            host.Harness.Announcements,
            announced => Assert.IsType<A11yEvent.PaletteCommandFailed>(announced),
            announced =>
            {
                var count = Assert.IsType<A11yEvent.PaletteFilterCount>(announced);
                Assert.Equal((1u, "q"), (count.Count, count.Query));
            });
    });

    /// <summary>
    /// P10 as amended at the W7-7 wave close: once its window has run out, the
    /// count is posted below input (Background), so a key the user already
    /// typed runs first. When that key closes the palette — Escape, or an
    /// Enter whose command succeeds — the count still posted says nothing
    /// afterwards: the dismissal cancels its window. When the command fails
    /// and the palette stays up, the count is owed, so the query ranks again
    /// and its count is spoken once, after the failure.
    /// </summary>
    [Theory]
    [InlineData("Escape")]
    [InlineData("Enter, the command succeeds")]
    [InlineData("Enter, the command fails")]
    public void ACountPostedBelowInputSaysNothingAfterTheKeyThatClosesThePalette(string key) => RunSta(() =>
    {
        LaneHost host = LaneHost.Opened();
        CommandPaletteViewModel palette = host.Palette;
        List<TaskCompletionSource> windows = host.HoldTheCountWindows();
        palette.Query = "q";
        Assert.True(
            PumpedDispatcher.PumpUntil(() => !palette.IsRankPending, TimeSpan.FromSeconds(10)),
            "the rank never published");
        PumpedDispatcher.Drain();
        Assert.Equal("slate.nav.quickOpen", palette.SelectedId);
        host.Harness.Announcements.Clear();
        if (key == "Enter, the command fails")
        {
            host.Harness.Source.InvokeFailures["slate.nav.quickOpen"] =
                new CommandException.ActionFailed("Disk is full.");
        }

        // The window runs out and the count is posted, not yet spoken: the
        // posting finishes off this thread, and nothing here pumps.
        Assert.Single(windows).SetResult();
        Assert.True(
            palette.FilterCountCompletion.Wait(TimeSpan.FromSeconds(10)),
            "the count was never posted");
        Assert.Empty(host.Harness.Announcements);

        // The key the user typed runs ahead of the posted count.
        if (key == "Escape")
        {
            palette.Dismiss();
        }
        else
        {
            palette.InvokeSelected();
        }

        PumpedDispatcher.Drain();
        if (key != "Enter, the command fails")
        {
            Assert.False(palette.IsOpen);
            Assert.Empty(host.Harness.Announcements);
            return;
        }

        Assert.True(palette.IsOpen, "a failure leaves the palette open (P9)");
        Assert.Equal(2, windows.Count);
        Assert.True(
            PumpedDispatcher.PumpUntil(() => !palette.IsRankPending, TimeSpan.FromSeconds(10)),
            "the failure never ranked the current query again");
        windows[1].SetResult();
        PumpedDispatcher.PumpUntilDrained(palette.FilterCountCompletion);
        PumpedDispatcher.Drain();
        Assert.Collection(
            host.Harness.Announcements,
            announced => Assert.IsType<A11yEvent.PaletteCommandFailed>(announced),
            announced =>
            {
                var count = Assert.IsType<A11yEvent.PaletteFilterCount>(announced);
                Assert.Equal((1u, "q"), (count.Count, count.Query));
            });
    });

    /// <summary>
    /// Keys and the pointer can reach the palette inside a loop that is not
    /// modal to the shell. While the command runs they are refused — a
    /// selection move (the palette's selection is re-asserted to the list),
    /// Enter (one command at a time), a re-open (it would clear the query and
    /// rank inside the loop) — and typing is kept without ranking. After the
    /// command fails, the query typed meanwhile ranks: its rows, the
    /// selection they bring, one count.
    /// </summary>
    [Fact]
    public void InsideACommandsLoopThePaletteTakesNoKeysAndATypedQueryRanksAfterAFailure() => RunSta(() =>
    {
        LaneHost host = LaneHost.Opened();
        CommandPaletteViewModel palette = host.Palette;
        host.Harness.Announcements.Clear();
        host.Harness.Source.InvokeFailures["slate.file.newNote"] =
            new CommandException.ActionFailed("Disk is full.");
        bool entered = false;
        host.Harness.Source.OnInvoke = _ =>
        {
            if (entered)
            {
                return;
            }

            entered = true;
            palette.MoveSelection(1);
            palette.SelectLast();
            palette.Select(palette.Rows[2]);
            palette.InvokeSelected();
            palette.Query = "q";
            palette.Open();
            PumpedDispatcher.Drain();

            Assert.Equal("slate.file.newNote", palette.SelectedId);
            Assert.Equal(["slate.file.newNote"], host.Harness.Source.Invoked);
            Assert.Equal("q", palette.Query);
            Assert.DoesNotContain("q", host.Ranker.Ranked);
            Assert.False(palette.IsRankPending);
            Assert.Empty(host.Harness.Announcements);
        };

        palette.InvokeSelected();

        Assert.True(palette.IsOpen);
        host.PumpUntilPublished();
        Assert.Equal(["slate.nav.quickOpen"], host.Harness.RowIds);
        Assert.Collection(
            host.Harness.Announcements,
            announced => Assert.IsType<A11yEvent.PaletteCommandFailed>(announced),
            announced => Assert.Equal(
                "Quick Open",
                Assert.IsType<A11yEvent.PaletteCommandSelected>(announced).Label),
            announced => Assert.Equal(
                "q",
                Assert.IsType<A11yEvent.PaletteFilterCount>(announced).Query));
    });

    /// <summary>
    /// A command whose own action tears the shell down and THEN fails says
    /// nothing: teardown has run, and nothing speaks after it (I8).
    /// </summary>
    [Fact]
    public void ACommandThatTearsTheShellDownAndFailsSaysNothing() => RunSta(() =>
    {
        LaneHost host = LaneHost.Opened();
        host.Harness.Announcements.Clear();
        host.Harness.Source.InvokeFailures["slate.file.newNote"] =
            new CommandException.ActionFailed("Disk is full.");
        host.Harness.Source.OnInvoke = _ =>
            VaultLifecycleViewModel.ShutDownPalette(host.Palette, new DisposalProbe(() => true));

        host.Palette.InvokeSelected();
        PumpedDispatcher.Drain();

        Assert.True(host.Palette.IsShutDown);
        Assert.Empty(host.Harness.Announcements);
        Assert.Empty(host.Harness.Source.Recorded);
    });

    /// <summary>
    /// T4: a rank of the LATEST query that throws a cancellation-shaped
    /// exception its own token did not cause — a released native object —
    /// is a failure, not a cancellation: it resolves like any other (the
    /// list as last published, nothing said) and is logged.
    /// </summary>
    [Fact]
    public void ACurrentRankThatThrowsObjectDisposedResolvesLikeAnyFailure() => RunSta(() =>
    {
        LaneHost host = LaneHost.Opened();
        CommandPaletteViewModel palette = host.Palette;
        palette.SelectLast();
        host.Harness.Announcements.Clear();
        CommandPaletteRowViewModel[] before = [.. palette.Rows];

        host.Ranker.Fail("x", new ObjectDisposedException("CommandRegistry"));
        palette.Query = "x";
        Assert.True(
            PumpedDispatcher.PumpUntil(() => !palette.IsRankPending, TimeSpan.FromSeconds(10)),
            "a disposed-object failure left the palette pending forever");
        PumpedDispatcher.PumpUntilDrained(palette.RankCompletion);
        PumpedDispatcher.Drain();

        Assert.Equal(before, palette.Rows);
        Assert.Equal("slate.tasks.review", palette.SelectedId);
        Assert.Empty(host.Harness.Announcements);
        Assert.Equal([HostDiagnosticEvent.PaletteWorkFailed], host.Harness.Diagnostics.ToArray());
    });

    /// <summary>
    /// T5: a rank already superseded when it fails — whether it throws a
    /// disposed object or anything else — changes nothing and is not
    /// logged; the newest query publishes as if it had never run.
    /// </summary>
    [Theory]
    [InlineData(nameof(ObjectDisposedException))]
    [InlineData(nameof(InvalidOperationException))]
    public void ASupersededRanksFailureIsSilentAndUnlogged(string kind) => RunSta(() =>
    {
        LaneHost host = LaneHost.Opened();
        CommandPaletteViewModel palette = host.Palette;
        host.Harness.Announcements.Clear();
        Exception failure = kind == nameof(ObjectDisposedException)
            ? new ObjectDisposedException("CommandRegistry")
            : new InvalidOperationException("ranking refused");

        host.Ranker.Park("x");
        host.Ranker.Fail("x", failure);
        palette.Query = "x";
        host.Ranker.WaitUntilEntered("x");
        palette.Query = "q";
        host.Ranker.Release("x");
        host.PumpUntilPublished();

        Assert.Empty(host.Harness.Diagnostics);
        Assert.Equal(["slate.nav.quickOpen"], host.Harness.RowIds);
        Assert.Collection(
            host.Harness.Announcements,
            announced => Assert.Equal(
                "Quick Open",
                Assert.IsType<A11yEvent.PaletteCommandSelected>(announced).Label),
            announced => Assert.Equal(
                "q",
                Assert.IsType<A11yEvent.PaletteFilterCount>(announced).Query));
    });

    /// <summary>
    /// Codex round 4's repro (#1275), green under the owner's option (a).
    /// Closing the app with a dirty tab while the palette is open runs the
    /// unsaved-changes prompt (<c>PrepareForApplicationClose</c> →
    /// <c>TryCloseWorkspace</c> → <c>_confirmUnsavedClose</c>), whose modal
    /// loop pumps the dispatcher with the palette open and not invoking.
    /// The injected confirmation runs a nested frame inside a thread-modal
    /// section (<c>ComponentDispatcher.PushModal</c>, what WPF and the common
    /// dialogs raise), and the palette hears about it through the same
    /// <see cref="ShellModalLoopMonitor"/> the shell builds. Inside the
    /// prompt a parked rank completes and its count window runs out: the
    /// palette publishes nothing and says nothing. When the prompt closes
    /// the query the seal took ranks again: its rows, its selection, one
    /// count. (The shipped shell's real message box is witnessed by the
    /// hosted facts in <c>CommandPaletteTests.Shell.cs</c>.)
    /// </summary>
    [Fact]
    public void TheUnsavedChangesPromptOnCloseHearsNothingFromThePalette() => RunSta(() =>
    {
        using FixtureVault fixture = FixtureVault.Create(1, "palette-close-prompt");
        var lane = new CommandPaletteWorkLane();
        var announced = new List<A11yEvent>();
        CommandPaletteViewModel? palette = null;
        Task? parkedRank = null;
        using var gate = new ManualResetEventSlim(false);
        int published = 0;
        (int Published, int Heard, string? Selected, bool Sealed)? duringPrompt = null;

        int PaletteHeard() => announced.Count(announcement =>
            announcement is A11yEvent.PaletteCommandSelected or A11yEvent.PaletteFilterCount);

        VaultCloseDecision Prompt()
        {
            // The prompt's modal loop: while it is up the parked rank
            // completes and posts its rows back, and its count window
            // runs out.
            ComponentDispatcher.PushModal();
            try
            {
                int publishedBefore = published;
                int heardBefore = PaletteHeard();
                gate.Set();
                Assert.True(
                    PumpedDispatcher.PumpUntil(() => parkedRank!.IsCompleted, TimeSpan.FromSeconds(10)),
                    "the parked rank never completed inside the prompt");
                PumpedDispatcher.Drain();
                Assert.True(
                    PumpedDispatcher.PumpUntil(
                        () => palette!.FilterCountCompletion.IsCompleted,
                        TimeSpan.FromSeconds(10)),
                    "the count window never ran out inside the prompt");
                PumpedDispatcher.Drain();
                duringPrompt = (
                    published - publishedBefore,
                    PaletteHeard() - heardBefore,
                    palette!.SelectedId,
                    palette.IsSealed);
            }
            finally
            {
                ComponentDispatcher.PopModal();
            }

            return VaultCloseDecision.Cancel;
        }

        using var lifecycle = new VaultLifecycleViewModel(
            pickVault: () => Task.FromResult<string?>(fixture.Root),
            enqueueUi: action => action(),
            recentVaultsStore: new RecentVaultsStore(
                Path.Combine(fixture.Root, "device-state", "recent-vaults.json")),
            announce: announced.Add,
            confirmUnsavedClose: Prompt,
            sessionLoadWorker: work => Task.FromResult(work()),
            paletteRecentsStore: new CommandPaletteRecentsStore(
                Path.Combine(fixture.Root, "device-state", "command-palette-recents.json")),
            paletteLane: lane);
        PumpedDispatcher.PumpUntilDrained(lifecycle.OpenVaultAsync(fixture.Root));
        WorkspaceViewModel workspace = Assert.IsType<WorkspaceViewModel>(lifecycle.Workspace);
        workspace.OpenPath("note0.md");
        workspace.ActiveGroup.ActiveTab!.Text += "\nUnsaved.";

        palette = lifecycle.Palette;
        palette.PropertyChanged += (_, change) =>
        {
            if (change.PropertyName == nameof(CommandPaletteViewModel.Rows))
            {
                published++;
            }
        };
        palette.Open();
        Assert.True(
            PumpedDispatcher.PumpUntil(() => !palette.IsRankPending, TimeSpan.FromSeconds(10)),
            "the open's rows never published");
        string? selectedBefore = palette.SelectedId;
        using var modalLoops = new ShellModalLoopMonitor(new Border(), palette.SetModalLoop);

        // A query whose rank is parked behind a held lane item.
        using var running = new ManualResetEventSlim(false);
        _ = lane.Run(
            () =>
            {
                running.Set();
                return gate.Wait(TimeSpan.FromSeconds(30));
            },
            CancellationToken.None);
        Assert.True(running.Wait(TimeSpan.FromSeconds(10)), "the lane never picked up the parking item");
        palette.Query = "quick open";
        parkedRank = palette.RankCompletion;
        Assert.True(palette.IsRankPending);

        // The window closes with a dirty tab: the unsaved-changes prompt.
        int publishedBeforeClose = published;
        int heardBeforeClose = announced.Count;
        Assert.False(lifecycle.PrepareForApplicationClose());

        Assert.NotNull(duringPrompt);
        Assert.True(duringPrompt.Value.Sealed, "the palette was not sealed while the prompt was up");
        Assert.True(
            duringPrompt.Value is (0, 0, _, _) && duringPrompt.Value.Selected == selectedBefore,
            $"while the unsaved-changes prompt was up the palette published {duringPrompt.Value.Published} "
            + $"time(s), made {duringPrompt.Value.Heard} announcement(s), and moved its selection from "
            + $"'{selectedBefore}' to '{duringPrompt.Value.Selected}'");

        // The prompt closed (Cancel) with the palette still up: the seal
        // lifts, and the query it took ranks again — its rows, the
        // selection they bring, one count.
        Assert.True(palette.IsOpen);
        Assert.False(palette.IsSealed);
        Assert.True(
            PumpedDispatcher.PumpUntil(() => !palette.IsRankPending, TimeSpan.FromSeconds(10)),
            "the query the seal took never ranked again");
        PumpedDispatcher.Drain();
        PumpedDispatcher.PumpUntilDrained(palette.FilterCountCompletion);
        PumpedDispatcher.Drain();
        Assert.Equal(1, published - publishedBeforeClose);
        Assert.Equal("slate.workspace.quickOpen", palette.SelectedId);
        A11yEvent[] afterPrompt = [.. announced.Skip(heardBeforeClose)];
        Assert.Collection(
            afterPrompt.Where(announcement =>
                announcement is A11yEvent.PaletteCommandSelected or A11yEvent.PaletteFilterCount),
            announced => Assert.IsType<A11yEvent.PaletteCommandSelected>(announced),
            announced => Assert.Equal(
                "quick open",
                Assert.IsType<A11yEvent.PaletteFilterCount>(announced).Query));
    });

    /// <summary>
    /// One seal, two causes: a command that raises a prompt of its own (a
    /// thread-modal loop inside the invoke) keeps the palette sealed after
    /// the prompt closes, until the command itself returns — the modal
    /// loop's end is not the seal's end while the invocation holds it. A
    /// parked rank completing after the prompt, still inside the command,
    /// lands nothing; the failure then speaks, and the query the seal took
    /// ranks once.
    /// </summary>
    [Fact]
    public void APromptInsideACommandKeepsThePaletteSealedUntilTheCommandReturns() => RunSta(() =>
    {
        LaneHost host = LaneHost.Opened();
        CommandPaletteViewModel palette = host.Palette;
        using var modalLoops = new ShellModalLoopMonitor(new Border(), palette.SetModalLoop);
        List<TaskCompletionSource> windows = host.HoldTheCountWindows();
        host.Harness.Announcements.Clear();
        host.Harness.Source.InvokeFailures["slate.file.newNote"] =
            new CommandException.ActionFailed("Disk is full.");
        int published = 0;
        palette.PropertyChanged += (_, change) =>
        {
            if (change.PropertyName == nameof(CommandPaletteViewModel.Rows))
            {
                published++;
            }
        };

        host.Ranker.Park("q");
        palette.Query = "q";
        host.Ranker.WaitUntilEntered("q");
        Task parkedRank = palette.RankCompletion;
        TaskCompletionSource window = Assert.Single(windows);

        (bool InPrompt, bool SealedAfterPrompt, int Published, int Heard)? inside = null;
        host.Harness.Source.OnInvoke = _ =>
        {
            ComponentDispatcher.PushModal();
            PumpedDispatcher.Drain();
            bool inPrompt = palette.IsInModalLoop && palette.IsSealed;
            ComponentDispatcher.PopModal();
            bool sealedAfterPrompt = palette.IsSealed;

            host.Ranker.Release("q");
            Assert.True(
                PumpedDispatcher.PumpUntil(() => parkedRank.IsCompleted, TimeSpan.FromSeconds(10)),
                "the parked rank never completed inside the command");
            window.TrySetResult();
            PumpedDispatcher.PumpUntilDrained(palette.FilterCountCompletion);
            PumpedDispatcher.Drain();
            inside = (inPrompt, sealedAfterPrompt, published, host.Harness.Announcements.Count);
        };

        palette.InvokeSelected();

        Assert.NotNull(inside);
        Assert.True(inside.Value.InPrompt, "the prompt inside the command was not reported");
        Assert.True(inside.Value.SealedAfterPrompt, "the prompt's end unsealed a palette whose command still ran");
        Assert.Equal(0, inside.Value.Published);
        Assert.Equal(0, inside.Value.Heard);
        Assert.False(palette.IsSealed);
        Assert.Equal(2, windows.Count);
        Assert.True(
            PumpedDispatcher.PumpUntil(() => !palette.IsRankPending, TimeSpan.FromSeconds(10)),
            "the failure never ranked the current query again");
        windows[1].SetResult();
        PumpedDispatcher.PumpUntilDrained(palette.FilterCountCompletion);
        PumpedDispatcher.Drain();
        Assert.Equal(1, published);
        Assert.Collection(
            host.Harness.Announcements,
            announced => Assert.IsType<A11yEvent.PaletteCommandFailed>(announced),
            announced => Assert.Equal(
                "Quick Open",
                Assert.IsType<A11yEvent.PaletteCommandSelected>(announced).Label),
            announced => Assert.Equal(
                "q",
                Assert.IsType<A11yEvent.PaletteFilterCount>(announced).Query));
    });
}
