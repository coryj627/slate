// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows.Threading;
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
}
