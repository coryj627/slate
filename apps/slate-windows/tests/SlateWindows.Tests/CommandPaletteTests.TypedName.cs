// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using SlateWindows.Commands;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// #1834 (plan 47 CB-34): a typed command name runs that command. Core puts a typed query's
/// best match first (contract 28 P1), and the host's selection carries who placed it: a row
/// the reader moved to is kept while a query keeps it, and a row the palette chose follows
/// the first row without a word (P7, P10; contract 40 R-11's addendum). The ranking is core's,
/// through the binding.
/// </summary>
public sealed partial class CommandPaletteTests
{
    // --- the issue's repro ----------------------------------------------------

    /// <summary>
    /// The open lands on a Recent row whose hint matches "settings"; typed one letter a
    /// publication, the name runs Settings…, not the row the palette opened on (cause 2), and
    /// not a View command that only its hint brings in (cause 1).
    /// </summary>
    [Fact]
    public void TypingACommandsNameAndPressingEnterRunsThatCommand()
    {
        var harness = new PaletteHarness(
            Registered(ChordTable.Ids.CloseTab),
            Registered(ChordTable.Ids.ClosePane),
            Registered(ChordTable.Ids.OpenSettings),
            Registered(ChordTable.Ids.Save));
        harness.Source.Recents.Add(ChordTable.Ids.CloseTab);
        CommandPaletteViewModel palette = harness.Palette;
        palette.Open();
        Assert.Equal(ChordTable.Ids.CloseTab, palette.SelectedId);

        const string Name = "settings";
        for (int typed = 1; typed <= Name.Length; typed++)
        {
            palette.Query = Name[..typed];
        }

        Assert.Contains(ChordTable.Ids.CloseTab, harness.RowIds);
        string firstRow = palette.Rows[0].Id;
        string? selected = palette.SelectedId;
        palette.InvokeSelected();
        Assert.True(
            harness.Source.Invoked.SequenceEqual([ChordTable.Ids.OpenSettings]),
            $"typing \"{Name}\" and Enter ran [{string.Join(", ", harness.Source.Invoked)}]; "
            + $"the first row was {firstRow} and the selection {selected}");
    }

    // --- P7: who placed the selection ------------------------------------------

    /// <summary>The open's row survives three queries without leading any: each time the
    /// selection is the first row, and nothing is said of it.</summary>
    [Fact]
    public void AnUntouchedSelectionFollowsTheFirstRowSilently()
    {
        PaletteHarness harness = StandardHarness();
        harness.Source.Recents.Add("slate.tasks.review");
        CommandPaletteViewModel palette = harness.Palette;
        palette.Open();
        Assert.Equal("slate.tasks.review", palette.SelectedId);

        foreach (string query in new[] { "e", "s", "a" })
        {
            harness.Announcements.Clear();
            palette.Query = query;

            Assert.Contains("slate.tasks.review", harness.RowIds);
            Assert.NotEqual("slate.tasks.review", palette.Rows[0].Id);
            Assert.Equal(palette.Rows[0].Id, palette.SelectedId);
            Assert.Empty(harness.Announcements.OfType<A11yEvent.PaletteCommandSelected>());
        }
    }

    /// <summary>Every route a reader moves by makes the row theirs: a query that keeps it
    /// without leading with it keeps it, and says nothing of it.</summary>
    [Theory]
    [InlineData("Down", "e", "slate.file.save")]
    [InlineData("Up", "e", "slate.tasks.review")]
    [InlineData("Home", "o", "slate.file.newNote")]
    [InlineData("End", "e", "slate.tasks.review")]
    [InlineData("PageDown", "e", "slate.nav.quickOpen")]
    [InlineData("PageUp", "o", "slate.file.newNote")]
    [InlineData("Select", "o", "slate.editor.bold")]
    public void AMovedSelectionThatSurvivesIsKept(string route, string query, string moved)
    {
        PaletteHarness harness = StandardHarness();
        CommandPaletteViewModel palette = harness.Palette;
        palette.Open();
        palette.PageSize = 2;
        Assert.Equal("slate.file.newNote", palette.SelectedId);

        switch (route)
        {
            case "Down": palette.MoveSelection(1); break;
            case "Up": palette.MoveSelection(-1); break;
            case "Home": palette.SelectFirst(); break;
            case "End": palette.SelectLast(); break;
            case "PageDown": palette.MovePage(1); break;
            case "PageUp": palette.MovePage(-1); break;
            case "Select": palette.Select(palette.Rows[3]); break;
            default: throw new ArgumentOutOfRangeException(nameof(route), route, null);
        }

        Assert.Equal(moved, palette.SelectedId);
        harness.Announcements.Clear();

        palette.Query = query;

        Assert.Contains(moved, harness.RowIds);
        Assert.NotEqual(moved, palette.Rows[0].Id);
        Assert.Equal(moved, palette.SelectedId);
        Assert.Empty(harness.Announcements.OfType<A11yEvent.PaletteCommandSelected>());
    }

    /// <summary>A row the reader moved to that a query removes is replaced by the first row,
    /// said once before the count. The replacement is the palette's: the next query that
    /// keeps it without leading with it moves the selection to its first row, silently.</summary>
    [Fact]
    public void AfterAMovedRowIsRemovedTheNextQueryFollowsTheFirstRow()
    {
        PaletteHarness harness = StandardHarness();
        CommandPaletteViewModel palette = harness.Palette;
        palette.Open();
        palette.SelectLast();
        Assert.Equal("slate.tasks.review", palette.SelectedId);
        harness.Announcements.Clear();

        palette.Query = "o";

        Assert.DoesNotContain("slate.tasks.review", harness.RowIds);
        string replacement = palette.Rows[0].Id;
        Assert.Equal(replacement, palette.SelectedId);
        Assert.Collection(
            harness.Announcements,
            announced => Assert.Equal(
                palette.Rows[0].Label,
                Assert.IsType<A11yEvent.PaletteCommandSelected>(announced).Label),
            announced => Assert.Equal(
                (3u, "o"),
                (Assert.IsType<A11yEvent.PaletteFilterCount>(announced).Count,
                    ((A11yEvent.PaletteFilterCount)announced).Query)));
        harness.Announcements.Clear();

        palette.Query = "e";

        Assert.Contains(replacement, harness.RowIds);
        Assert.NotEqual(replacement, palette.Rows[0].Id);
        Assert.Equal(palette.Rows[0].Id, palette.SelectedId);
        Assert.Empty(harness.Announcements.OfType<A11yEvent.PaletteCommandSelected>());
    }

    /// <summary>The open's row, which a query removes, is replaced by the first row without a
    /// word: the reader hears the count alone.</summary>
    [Fact]
    public void AnUntouchedRowAQueryRemovesIsReplacedSilently()
    {
        PaletteHarness harness = StandardHarness();
        CommandPaletteViewModel palette = harness.Palette;
        palette.Open();
        Assert.Equal("slate.file.newNote", palette.SelectedId);

        palette.Query = "q";

        Assert.Equal(["slate.nav.quickOpen"], harness.RowIds);
        Assert.Equal("slate.nav.quickOpen", palette.SelectedId);
        A11yEvent.PaletteFilterCount count =
            Assert.IsType<A11yEvent.PaletteFilterCount>(Assert.Single(harness.Announcements));
        Assert.Equal((1u, "q"), (count.Count, count.Query));
    }

    /// <summary>No rows leave no selection, and the palette's: the query that brings rows back
    /// selects the first, silently, whoever placed the selection before.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RecoveringFromNoMatchesSelectsTheFirstRowSilently(bool movedFirst)
    {
        PaletteHarness harness = StandardHarness();
        CommandPaletteViewModel palette = harness.Palette;
        palette.Open();
        if (movedFirst)
        {
            palette.SelectLast();
        }

        palette.Query = "zzzz";
        Assert.Null(palette.SelectedId);
        harness.Announcements.Clear();

        palette.Query = "sav";

        Assert.Equal("slate.file.save", palette.SelectedId);
        A11yEvent.PaletteFilterCount count =
            Assert.IsType<A11yEvent.PaletteFilterCount>(Assert.Single(harness.Announcements));
        Assert.Equal((1u, "sav"), (count.Count, count.Query));
    }

    // --- DS-10's seeds -------------------------------------------------------------

    /// <summary>The list of choices opens on the current one, and a letter typed then moves the
    /// selection to the first row it ranks: "d" leaves "Use Windows setting" listed by its
    /// "Windows", but Dark leads, and Enter chooses Dark.</summary>
    [Fact]
    public void TheCurrentChoiceIsKeptAtTheOpenAndTypingFollowsTheFirstRow()
    {
        var host = new ChoicesHarness(ThemeChoice.UseWindowsSetting);
        host.InvokeSwitchTheme();
        Assert.True(host.Palette.IsChoosing);
        Assert.Equal(ThemeChoice.UseWindowsSetting, host.Palette.SelectedId);
        host.Harness.Announcements.Clear();

        host.Palette.Query = "d";

        Assert.Contains(ThemeChoice.UseWindowsSetting, host.Harness.RowIds);
        Assert.Equal(ThemeChoice.Dark, host.Palette.Rows[0].Id);
        Assert.Equal(ThemeChoice.Dark, host.Palette.SelectedId);
        Assert.Empty(host.Harness.Announcements.OfType<A11yEvent.PaletteChoiceSelected>());

        host.Palette.InvokeSelected();

        Assert.Equal(ThemeChoice.Dark, Assert.Single(host.Chosen).Id);
    }

    /// <summary>The current choice holds until the query changes: a letter typed before the
    /// list's first rows land is a query change, and its publication moves the selection to the
    /// first row it ranks.</summary>
    [Fact]
    public void ALetterTypedBeforeTheChoicesPublishTakesTheFirstRow() => RunSta(() =>
    {
        (PaletteHarness harness, ParkedRanker ranker, List<PaletteChoice> chosen) = ChoicesParkedBeforeTheirFirstRows();
        CommandPaletteViewModel palette = harness.Palette;

        palette.Query = "d";
        ranker.Release(string.Empty);
        PumpUntilPublished(palette);

        Assert.Equal(ThemeChoice.Dark, palette.Rows[0].Id);
        Assert.Contains(ThemeChoice.UseWindowsSetting, harness.RowIds);
        Assert.Equal(ThemeChoice.Dark, palette.SelectedId);
        Assert.Empty(harness.Announcements.OfType<A11yEvent.PaletteChoiceSelected>());
        palette.InvokeSelected();
        Assert.Equal(ThemeChoice.Dark, Assert.Single(chosen).Id);
    });

    /// <summary>A modal loop over the shell before the list's first rows land seals the
    /// palette, and its end ranks the same empty query again: no query changed, so the list
    /// still opens on the current choice.</summary>
    [Fact]
    public void ASealBeforeTheChoicesPublishKeepsTheCurrentChoice() => RunSta(() =>
    {
        (PaletteHarness harness, ParkedRanker ranker, List<PaletteChoice> chosen) = ChoicesParkedBeforeTheirFirstRows();
        CommandPaletteViewModel palette = harness.Palette;
        Task parked = palette.RankCompletion;

        palette.SetModalLoop(true);
        Assert.True(palette.IsSealed);
        ranker.Release(string.Empty);
        PumpedDispatcher.PumpUntilDrained(parked);
        PumpedDispatcher.Drain();
        Assert.Empty(palette.Rows);
        palette.SetModalLoop(false);
        PumpUntilPublished(palette);

        Assert.True(palette.IsChoosing);
        Assert.Equal(3, palette.Rows.Count);
        Assert.NotEqual(ThemeChoice.UseWindowsSetting, palette.Rows[0].Id);
        Assert.Equal(ThemeChoice.UseWindowsSetting, palette.SelectedId);
        Assert.Empty(harness.Announcements.OfType<A11yEvent.PaletteChoiceSelected>());
        palette.InvokeSelected();
        Assert.Equal(ThemeChoice.UseWindowsSetting, Assert.Single(chosen).Id);
    });

    /// <summary>Switch Theme… run from the palette's own lane, on "Use Windows setting", with
    /// the list's first rank parked on the worker: the selection is the seed, and no rows are
    /// on screen yet.</summary>
    private static (PaletteHarness Harness, ParkedRanker Ranker, List<PaletteChoice> Chosen) ChoicesParkedBeforeTheirFirstRows()
    {
        (PaletteHarness harness, ParkedRanker ranker, List<PaletteChoice> chosen) = ThemeCommandsOnTheLane();
        CommandPaletteViewModel palette = harness.Palette;
        palette.Select(Assert.Single(palette.Rows, row => row.Id == SwitchThemeId));

        // The list's own first rank waits on the worker.
        ranker.Park(string.Empty);
        palette.InvokeSelected();
        Assert.True(palette.IsChoosing);
        Assert.True(
            SpinWait.SpinUntil(() => ranker.Ranked.Count(query => query.Length == 0) >= 2, TimeSpan.FromSeconds(10)),
            "the list's first rank never reached the ranker");
        Assert.True(palette.IsRankPending);
        Assert.Equal(ThemeChoice.UseWindowsSetting, palette.SelectedId);
        harness.Announcements.Clear();
        return (harness, ranker, chosen);
    }

    /// <summary>Show Tasks Review and Switch Theme… on the palette's own lane, ranked by a
    /// <see cref="ParkedRanker"/>, open and published; Switch Theme… opens the themes on
    /// "Use Windows setting".</summary>
    private static (PaletteHarness Harness, ParkedRanker Ranker, List<PaletteChoice> Chosen) ThemeCommandsOnTheLane()
    {
        var ranker = new ParkedRanker(Environment.CurrentManagedThreadId);
        var harness = new PaletteHarness(
            SynchronizationContext.Current,
            [
                Cmd("slate.tasks.review", "Show Tasks Review", CommandSection.View),
                Cmd(SwitchThemeId, "Switch Theme…", CommandSection.View),
            ],
            productionLane: true,
            rank: ranker.Rank);
        var chosen = new List<PaletteChoice>();
        var choices = new PaletteChoices(
            "Select a theme",
            "Current theme",
            ThemeChoice.Options.Select(option => new PaletteChoice(option.Value, option.Label)).ToArray(),
            ThemeChoice.UseWindowsSetting,
            chosen.Add);
        CommandPaletteViewModel palette = harness.Palette;
        harness.Source.OnInvoke = id =>
        {
            if (id == SwitchThemeId)
            {
                palette.OpenChoices(choices);
            }
        };
        palette.Open();
        PumpUntilPublished(palette);
        return (harness, ranker, chosen);
    }

    /// <summary>
    /// Codex round 1 (#1834): Esc brings back a row the reader arrowed to, and a letter typed
    /// before Back's own rows land removes it. Back has said its row already; the letter's
    /// publication replaces the reader's row and says the replacement once, before the count —
    /// Back leaves nothing armed to swallow it.
    /// </summary>
    [Fact]
    public void AQueryTypedBeforeEscBacksRowsLandStillSaysTheReplacement() => RunSta(() =>
    {
        (PaletteHarness harness, ParkedRanker ranker, _) = ThemeCommandsOnTheLane();
        CommandPaletteViewModel palette = harness.Palette;
        palette.MoveSelection(1);
        Assert.Equal(SwitchThemeId, palette.SelectedId);
        palette.InvokeSelected();
        PumpUntilPublished(palette);
        Assert.True(palette.IsChoosing);

        ranker.Park(string.Empty);
        palette.BackOrDismiss();
        Assert.True(
            SpinWait.SpinUntil(() => ranker.Ranked.Count(query => query.Length == 0) >= 3, TimeSpan.FromSeconds(10)),
            "Back's rank never reached the ranker");
        Assert.Equal(SwitchThemeId, palette.SelectedId);
        harness.Announcements.Clear();

        palette.Query = "r";
        ranker.Release(string.Empty);
        PumpUntilPublished(palette);

        Assert.Equal(["slate.tasks.review"], harness.RowIds);
        Assert.Equal("slate.tasks.review", palette.SelectedId);
        Assert.Collection(
            harness.Announcements,
            announced => Assert.Equal(
                "Show Tasks Review",
                Assert.IsType<A11yEvent.PaletteCommandSelected>(announced).Label),
            announced => Assert.Equal(
                (1u, "r"),
                (Assert.IsType<A11yEvent.PaletteFilterCount>(announced).Count,
                    ((A11yEvent.PaletteFilterCount)announced).Query)));
    });

    /// <summary>Codex round 1 (#1834): Back's own rank fails, and the next query removes the
    /// row the reader arrowed to: its replacement is said once, before the count.</summary>
    [Fact]
    public void AQueryAfterAFailedEscBackRankStillSaysTheReplacement() => RunSta(() =>
    {
        (PaletteHarness harness, ParkedRanker ranker, _) = ThemeCommandsOnTheLane();
        CommandPaletteViewModel palette = harness.Palette;
        palette.MoveSelection(1);
        palette.InvokeSelected();
        PumpUntilPublished(palette);
        Assert.True(palette.IsChoosing);

        ranker.Fail(string.Empty, new InvalidOperationException("ranking refused"));
        palette.BackOrDismiss();
        PumpUntilPublished(palette);
        Assert.Contains(HostDiagnosticEvent.PaletteWorkFailed, harness.Diagnostics);
        Assert.False(palette.IsChoosing);
        Assert.Equal(SwitchThemeId, palette.SelectedId);
        harness.Announcements.Clear();

        palette.Query = "r";
        PumpUntilPublished(palette);

        Assert.Equal("slate.tasks.review", palette.SelectedId);
        Assert.Collection(
            harness.Announcements,
            announced => Assert.Equal(
                "Show Tasks Review",
                Assert.IsType<A11yEvent.PaletteCommandSelected>(announced).Label),
            announced => Assert.Equal("r", Assert.IsType<A11yEvent.PaletteFilterCount>(announced).Query));
    });

    /// <summary>Esc from the choices brings the command's row back as the reader left it, and
    /// Back says so as it does today. A row they arrowed to is still theirs: a query keeps it
    /// while it lists it, and removing it is said. A row the palette had put first is the
    /// palette's: the next query moves the selection to its first row, silently.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void EscBackKeepsTheRowAsTheReaderLeftIt(bool arrowedTo)
    {
        var host = new ChoicesHarness();
        CommandPaletteViewModel palette = host.Palette;
        palette.Open();
        if (arrowedTo)
        {
            palette.MoveSelection(1);
        }
        else
        {
            palette.Query = "theme";
        }

        Assert.Equal(SwitchThemeId, palette.SelectedId);
        palette.InvokeSelected();
        Assert.True(palette.IsChoosing);
        host.Harness.Announcements.Clear();

        palette.BackOrDismiss();

        Assert.False(palette.IsChoosing);
        Assert.Equal(SwitchThemeId, palette.SelectedId);
        Assert.Equal(
            "Switch Theme…",
            Assert.Single(host.Harness.Announcements.OfType<A11yEvent.PaletteCommandSelected>()).Label);
        Assert.IsType<A11yEvent.PaletteCommandSelected>(host.Harness.Announcements[0]);
        host.Harness.Announcements.Clear();

        palette.Query = "s";

        Assert.Contains(SwitchThemeId, host.Harness.RowIds);
        Assert.NotEqual(SwitchThemeId, palette.Rows[0].Id);
        Assert.Empty(host.Harness.Announcements.OfType<A11yEvent.PaletteCommandSelected>());
        if (!arrowedTo)
        {
            Assert.Equal(palette.Rows[0].Id, palette.SelectedId);
            return;
        }

        Assert.Equal(SwitchThemeId, palette.SelectedId);
        host.Harness.Announcements.Clear();

        palette.Query = "r";

        Assert.DoesNotContain(SwitchThemeId, host.Harness.RowIds);
        Assert.Equal(palette.Rows[0].Id, palette.SelectedId);
        Assert.Collection(
            host.Harness.Announcements,
            announced => Assert.Equal(
                palette.Rows[0].Label,
                Assert.IsType<A11yEvent.PaletteCommandSelected>(announced).Label),
            announced => Assert.Equal("r", Assert.IsType<A11yEvent.PaletteFilterCount>(announced).Query));
    }

    // --- PD-2: a re-open's gap ---------------------------------------------------

    /// <summary>A re-open clears the selection and keeps the previous query's rows on screen
    /// until its own rows land, so no empty state flashes. A reader move in that gap would be
    /// a move over rows that are going: it does nothing, and says nothing; the re-open's rows
    /// then land on their first.</summary>
    [Fact]
    public void AReaderMoveBeforeAReOpenPublishesDoesNothing() => RunSta(() =>
    {
        LaneHost host = LaneHost.Opened();
        CommandPaletteViewModel palette = host.Palette;
        palette.Query = "o";
        host.PumpUntilPublished();
        host.Harness.Announcements.Clear();
        CommandPaletteRowViewModel[] before = [.. palette.Rows];

        host.Ranker.Park(string.Empty);
        palette.Open();
        Assert.True(
            SpinWait.SpinUntil(() => host.Ranker.Ranked.Count(query => query.Length == 0) >= 2, TimeSpan.FromSeconds(10)),
            "the re-open's rank never reached the ranker");
        Assert.True(palette.IsRankPending);
        Assert.Equal(before, palette.Rows);
        Assert.Null(palette.SelectedId);

        palette.MoveSelection(1);

        Assert.Null(palette.SelectedId);
        Assert.Empty(host.Harness.Announcements);

        host.Ranker.Release(string.Empty);
        host.PumpUntilPublished();

        Assert.Equal(5, palette.Rows.Count);
        Assert.Equal(palette.Rows[0].Id, palette.SelectedId);
        Assert.Empty(host.Harness.Announcements);
    });

    /// <summary>T4 through a re-open: a re-open whose rank fails resolves with the previous
    /// query's rows still on screen, and the keys work them, as after any failed rank.</summary>
    [Fact]
    public void AFailedReOpenRankLeavesTheKeysOnTheRowsOnScreen() => RunSta(() =>
    {
        LaneHost host = LaneHost.Opened();
        CommandPaletteViewModel palette = host.Palette;
        palette.Query = "o";
        host.PumpUntilPublished();
        CommandPaletteRowViewModel[] before = [.. palette.Rows];

        host.Ranker.Fail(string.Empty, new InvalidOperationException("ranking refused"));
        palette.Open();
        Assert.True(
            PumpedDispatcher.PumpUntil(() => !palette.IsRankPending, TimeSpan.FromSeconds(10)),
            "a failed re-open rank left the palette pending forever");
        PumpedDispatcher.PumpUntilDrained(palette.RankCompletion);
        PumpedDispatcher.Drain();
        Assert.Contains(HostDiagnosticEvent.PaletteWorkFailed, host.Harness.Diagnostics);
        Assert.Equal(before, palette.Rows);
        Assert.Null(palette.SelectedId);

        palette.MoveSelection(1);

        Assert.Equal(before[0].Id, palette.SelectedId);
        palette.InvokeSelected();
        Assert.Equal([before[0].Id], host.Harness.Source.Invoked);
    });

    // --- T7: a typed-ahead Enter ---------------------------------------------------

    /// <summary>An Enter queued ahead of the latest query's finished rank publishes it first
    /// (T7 as amended) and runs its first row, though the open's row survives the query.</summary>
    [Fact]
    public void ATypedAheadEnterRunsTheFirstRowOfTheQueryItFollows() => RunSta(() =>
    {
        LaneHost host = LaneHost.Create();
        host.Harness.Source.Recents.Add("slate.tasks.review");
        CommandPaletteViewModel palette = host.Palette;
        palette.Open();
        host.PumpUntilPublished();
        Assert.Equal("slate.tasks.review", palette.SelectedId);
        var published = new List<string[]>();
        palette.PropertyChanged += (_, change) =>
        {
            if (change.PropertyName == nameof(CommandPaletteViewModel.Rows) && palette.Rows.Count > 0)
            {
                published.Add([.. host.Harness.RowIds]);
            }
        };

        palette.Query = "e";
        Assert.True(palette.RankCompletion.Wait(TimeSpan.FromSeconds(10)), "the rank never finished");
        Assert.True(palette.IsRankPending, "premise: the finished rank's publication has not run.");
        _ = Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Input, palette.InvokeSelected);
        PumpedDispatcher.Drain();

        string[] rows = Assert.Single(published);
        Assert.Equal("slate.file.newNote", rows[0]);
        Assert.Contains("slate.tasks.review", rows);
        Assert.Equal(["slate.file.newNote"], host.Harness.Source.Invoked);
    });

    // --- P1: the strongest match leads ---------------------------------------------

    /// <summary>Replaces <c>ScoreIdentifiesTheStrongestMatchWithoutReorderingTheDisplay</c>: the
    /// first row is the strongest match, and its section leads.</summary>
    [Fact]
    public void TheDisplayLeadsWithTheStrongestMatch()
    {
        var harness = new PaletteHarness(
            Cmd("a.file.print", "Print Note", CommandSection.File, "Print or save the note"),
            Cmd("z.editor.save", "Save", CommandSection.Editor));

        harness.Palette.Open();
        harness.Palette.Query = "save";

        Assert.Equal(["z.editor.save", "a.file.print"], harness.RowIds);
        Assert.Equal(["Editor", "File"], harness.SectionTitles);
        Assert.Equal("z.editor.save", harness.Palette.SelectedId);
    }

    // --- the typed-name census (Call 8) ---------------------------------------------

    /// <summary>Two or more registered commands that share a whole typed name, and why that is
    /// right. Empty: no two do.</summary>
    private static readonly SharedTypedName[] SharedTypedNames = [];

    /// <summary>
    /// Every registered command's typed name — its label, with and without one trailing "…" —
    /// ranks it first, and so does every name after a label's first ": " that no other
    /// command shares as its own such name or its whole name. A new label that ties an old one
    /// brings #1834 back; it fails here, unless an allow-list row says why.
    /// </summary>
    [Fact]
    public void EveryRegisteredCommandsTypedNameRanksItFirst()
    {
        Command[] catalog = [.. ChordTable.RegisteredRows.Select(row => new Command(
            row.Id,
            row.Label,
            row.Hint,
            row.WindowsChord,
            row.Section))];
        Assert.True(catalog.Length > 100, "premise: the registered catalog is the app's");

        List<string> failures = TypedNameCensusFailures(catalog, SharedTypedNames, CoreRank);

        Assert.True(failures.Count == 0, $"{failures.Count} typed name(s) do not lead:\n" + string.Join("\n", failures));
    }

    /// <summary>Planted: two commands whose whole names are one typed name fail the census,
    /// unless an allow-list row with a reason names them; a row with no reason does not.</summary>
    [Fact]
    public void TheTypedNameCensusFailsTwoCommandsThatShareAWholeName()
    {
        Command[] catalog =
        [
            Cmd("a.file.duplicate", "Duplicate", CommandSection.File),
            Cmd("b.canvas.duplicate", "Duplicate…", CommandSection.Canvas),
            Cmd("c.view.other", "Other", CommandSection.View),
        ];

        string failure = Assert.Single(TypedNameCensusFailures(catalog, [], CoreRank));
        Assert.Contains("share the typed name", failure, StringComparison.Ordinal);
        Assert.Contains("a.file.duplicate", failure, StringComparison.Ordinal);
        Assert.Contains("b.canvas.duplicate", failure, StringComparison.Ordinal);

        Assert.Single(TypedNameCensusFailures(
            catalog,
            [new SharedTypedName(["b.canvas.duplicate", "a.file.duplicate"], " ")],
            CoreRank));
        Assert.Empty(TypedNameCensusFailures(
            catalog,
            [new SharedTypedName(["b.canvas.duplicate", "a.file.duplicate"], "The planted case's reason.")],
            CoreRank));
    }

    /// <summary>Planted: a ranking that leaves the name after the first ": " out loses that
    /// name to a longer label it prefixes, and the census says which.</summary>
    [Fact]
    public void TheTypedNameCensusFailsANameAfterTheColonThatLoses()
    {
        Command[] catalog =
        [
            Cmd("z.canvas.showOutline", "Canvas: Show Outline", CommandSection.Canvas),
            Cmd("a.view.showOutlinePanel", "Show Outline Panel", CommandSection.View),
        ];
        Assert.Empty(TypedNameCensusFailures(catalog, [], CoreRank));

        string failure = Assert.Single(TypedNameCensusFailures(catalog, [], ByScoreAlone));

        Assert.Contains("\"Show Outline\"", failure, StringComparison.Ordinal);
        Assert.Contains("z.canvas.showOutline", failure, StringComparison.Ordinal);
        Assert.Contains("a.view.showOutlinePanel", failure, StringComparison.Ordinal);

        // A ranking by score and id alone, as core ranked before #1834: what the tail tier
        // exists to beat.
        static PaletteSection[] ByScoreAlone(Command[] commands, string query) =>
        [
            new PaletteSection(
                "All",
                null,
                [.. CoreRank(commands, query)
                    .SelectMany(section => section.Rows)
                    .OrderByDescending(row => row.Score)
                    .ThenBy(row => row.Command.Id, StringComparer.Ordinal)]),
        ];
    }

    /// <summary>Planted: an allow-list row whose commands no longer share a whole name fails
    /// the census, so the list cannot outlive what it excuses.</summary>
    [Fact]
    public void TheTypedNameCensusFailsAStaleAllowListRow()
    {
        Command[] catalog =
        [
            Cmd("a.file.one", "One", CommandSection.File),
            Cmd("b.file.two", "Two", CommandSection.File),
        ];

        string failure = Assert.Single(TypedNameCensusFailures(
            catalog,
            [new SharedTypedName(["a.file.one", "b.file.two"], "They once shared a name.")],
            CoreRank));

        Assert.Contains("stale", failure, StringComparison.Ordinal);
        Assert.Contains("a.file.one", failure, StringComparison.Ordinal);
    }

    /// <summary>Planted: a name that ranks no row at all does not lead with its command, and
    /// the census says so.</summary>
    [Fact]
    public void TheTypedNameCensusFailsANameThatRanksNoRows()
    {
        Command[] catalog = [Cmd("a.file.one", "One", CommandSection.File)];

        string failure = Assert.Single(TypedNameCensusFailures(catalog, [], (_, _) => []));

        Assert.Contains("\"One\" leads with no row", failure, StringComparison.Ordinal);
    }

    /// <summary>An allow-list row: commands that share a whole typed name, and why.</summary>
    private sealed record SharedTypedName(string[] Ids, string Reason);

    private static PaletteSection[] CoreRank(Command[] commands, string query) =>
        SlateUniffiMethods.PaletteSections(commands, query, [], ChordTable.SidebarPinnedOrder);

    /// <summary>
    /// The census's checker: what does not lead, as one line each. A whole typed name is a
    /// label, with and without one trailing "…"; a tail is a label's text after its first
    /// ": ", likewise. Names compare as core's exact match does for these labels: canonical
    /// form, case folded, the trailing "…" dropped.
    /// </summary>
    private static List<string> TypedNameCensusFailures(
        IReadOnlyList<Command> catalog,
        IReadOnlyList<SharedTypedName> allowList,
        Func<Command[], string, PaletteSection[]> rank)
    {
        var failures = new List<string>();
        Command[] commands = [.. catalog];

        static string Key(string name)
        {
            string folded = name.Normalize(NormalizationForm.FormC).ToLowerInvariant();
            return folded.EndsWith('…') ? folded[..^1] : folded;
        }

        static string[] Spellings(string name) =>
            name.EndsWith('…') ? [name, name[..^1]] : [name];

        static string? TailOf(string label)
        {
            int colon = label.IndexOf(": ", StringComparison.Ordinal);
            return colon < 0 ? null : label[(colon + 2)..];
        }

        string? Leader(string query) =>
            rank(commands, query).SelectMany(section => section.Rows).FirstOrDefault()?.Command.Id;

        static string Ids(IEnumerable<string> ids) =>
            string.Join(", ", ids.Order(StringComparer.Ordinal));

        // Whole names two commands share: one of them cannot lead.
        var shared = commands
            .GroupBy(command => Key(command.Label), StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Select(command => command.Id).Order(StringComparer.Ordinal).ToArray())
            .ToList();
        var sharing = new HashSet<string>(shared.SelectMany(ids => ids), StringComparer.Ordinal);
        foreach (string[] ids in shared)
        {
            bool excused = allowList.Any(row =>
                !string.IsNullOrWhiteSpace(row.Reason)
                && row.Ids.Order(StringComparer.Ordinal).SequenceEqual(ids, StringComparer.Ordinal));
            if (!excused)
            {
                failures.Add($"{Ids(ids)} share the typed name \"{commands.First(command => command.Id == ids[0]).Label}\"; "
                    + "rename one, or add an allow-list row that says why");
            }
        }

        foreach (SharedTypedName row in allowList)
        {
            if (!shared.Any(ids => row.Ids.Order(StringComparer.Ordinal).SequenceEqual(ids, StringComparer.Ordinal)))
            {
                failures.Add($"the allow-list row for {Ids(row.Ids)} is stale: they no longer share a typed name");
            }
        }

        foreach (Command command in commands.Where(command => !sharing.Contains(command.Id)))
        {
            foreach (string name in Spellings(command.Label))
            {
                string? leader = Leader(name);
                if (leader != command.Id)
                {
                    failures.Add($"\"{name}\" leads with {leader ?? "no row"}, not {command.Id}");
                }
            }
        }

        // Tails no other command has as its tail or its whole name.
        var tails = commands
            .Select(command => (Command: command, Tail: TailOf(command.Label)))
            .Where(entry => entry.Tail is not null)
            .ToList();
        var wholeNames = new HashSet<string>(commands.Select(command => Key(command.Label)), StringComparer.Ordinal);
        foreach ((Command command, string? tail) in tails)
        {
            string key = Key(tail!);
            bool unshared = !wholeNames.Contains(key)
                && tails.Count(entry => Key(entry.Tail!) == key) == 1;
            if (!unshared)
            {
                continue;
            }

            foreach (string name in Spellings(tail!))
            {
                string? leader = Leader(name);
                if (leader != command.Id)
                {
                    failures.Add($"\"{name}\" (after the colon of \"{command.Label}\") leads with {leader ?? "no row"}, not {command.Id}");
                }
            }
        }

        return failures;
    }

    // --- the reader's element across a republish ----------------------------------

    /// <summary>
    /// AGENTS.md's republish rule, in the shell's palette overlay: the issue's name typed a
    /// letter a publication over a Recent row that survives it, and through every publication
    /// the keys stay in the search field while the list's selection is the first row, realized
    /// and on screen.
    /// </summary>
    [Fact]
    public void TheKeysStayInTheFieldWhileTheSelectionFollowsTheFirstRow() => RunSta(() =>
    {
        using var host = new PaletteShellHost();
        string recents = host.FileInFixture(Path.Combine("device-state", "command-palette-recents.json"));
        Directory.CreateDirectory(Path.GetDirectoryName(recents)!);
        Assert.True(new CommandPaletteRecentsStore(recents).TrySave([ChordTable.Ids.CloseTab]));
        host.OpenThePalette();
        CommandPaletteViewModel palette = host.Palette;
        Assert.Equal(ChordTable.Ids.CloseTab, palette.SelectedId);
        TextBox field = host.Shell.CommandPaletteSearchTextBox;
        ListBox list = host.Shell.CommandPaletteResultsList;
        host.AssertFocusIsOn(field);

        int survivedWithoutLeading = 0;
        const string Name = "settings";
        for (int typed = 1; typed <= Name.Length; typed++)
        {
            field.Text = Name[..typed];
            Assert.True(
                PumpedDispatcher.PumpUntil(() => !palette.IsRankPending, TimeSpan.FromSeconds(10)),
                $"\"{Name[..typed]}\" never published");
            host.Settle();

            host.AssertFocusIsOn(field);
            Assert.Same(palette.Rows[0], palette.SelectedRow);
            Assert.Same(palette.SelectedRow, list.SelectedItem);
            AssertRealizedAndOnScreen(list, palette.SelectedRow!);
            if (palette.Rows.Skip(1).Any(row => row.Id == ChordTable.Ids.CloseTab))
            {
                survivedWithoutLeading++;
            }
        }

        Assert.True(survivedWithoutLeading >= 3, $"premise: the open's row survived without leading {survivedWithoutLeading} time(s)");
        Assert.Equal(ChordTable.Ids.OpenSettings, palette.SelectedId);
    });

    // --- Call 10: a row that takes the keys -----------------------------------------

    /// <summary>A row that takes the keys away from the selection — a click, or a letter the
    /// list searches by — is the selection, and the reader's, silently (the screen reader
    /// speaks the focused row): a query that keeps it without leading with it keeps it.</summary>
    [Fact]
    public void ARowThatTakesTheKeysIsTheReaders() => RunSta(() =>
    {
        using var host = new ShippedResultsList(StandardCommands());
        CommandPaletteViewModel palette = host.Palette;
        palette.Open();
        host.Settle();
        Assert.Equal("slate.file.newNote", palette.SelectedId);
        // The keys on the palette's row first, as Tab lands them; a click then takes them.
        var selected = Assert.IsType<ListBoxItem>(host.List.ItemContainerGenerator.ContainerFromItem(palette.SelectedRow));
        Assert.True(selected.Focus(), "premise: the palette's row refused the keys");
        host.Settle();
        var row = Assert.IsType<ListBoxItem>(host.List.ItemContainerGenerator.ContainerFromItem(palette.Rows[3]));
        host.ClearObservations();

        Assert.True(row.Focus(), "premise: Toggle Bold's row refused the keys");
        host.Settle();
        Assert.Equal("slate.editor.bold", palette.SelectedId);
        Assert.Empty(host.Harness.Announcements);

        host.ChangeQuery("o");

        Assert.Contains("slate.editor.bold", host.Harness.RowIds);
        Assert.NotEqual("slate.editor.bold", palette.Rows[0].Id);
        Assert.Equal("slate.editor.bold", palette.SelectedId);
        Assert.Empty(host.SelectionAnnouncements);
    });

    /// <summary>
    /// Codex round 1 (#1834): a click on the row that already holds the keys and the selection
    /// changes neither, so neither the keys' route nor the list's selection hears it; the
    /// pointer's press does. The palette's row is the reader's after it, and a query that keeps
    /// it without leading with it keeps it.
    /// </summary>
    [Fact]
    public void ClickingTheFocusedPalettesRowMakesItTheReaders() => RunSta(() =>
    {
        using var host = new ShippedResultsList(StandardCommands());
        CommandPaletteViewModel palette = host.Palette;
        palette.Open();
        host.Settle();
        Assert.Equal("slate.file.newNote", palette.SelectedId);
        var row = Assert.IsType<ListBoxItem>(host.List.ItemContainerGenerator.ContainerFromItem(palette.SelectedRow));
        Assert.True(row.Focus(), "premise: the palette's row refused the keys");
        host.Settle();
        host.ClearObservations();

        PressOn(row);
        host.Settle();
        Assert.Equal("slate.file.newNote", palette.SelectedId);
        Assert.Empty(host.Harness.Announcements);

        host.ChangeQuery("o");

        Assert.Contains("slate.file.newNote", host.Harness.RowIds);
        Assert.NotEqual("slate.file.newNote", palette.Rows[0].Id);
        Assert.Equal("slate.file.newNote", palette.SelectedId);
        Assert.Empty(host.SelectionAnnouncements);
    });

    /// <summary>Codex round 1 (#1834): Tab lands the keys on the palette's row, which stays the
    /// palette's; a click on that same row then makes it the reader's, and a publication that
    /// keeps it without leading with it keeps it.</summary>
    [Fact]
    public void TabThenAClickOnTheSameRowMakesItTheReaders() => RunSta(() =>
    {
        using var host = new PaletteShellHost();
        string recents = host.FileInFixture(Path.Combine("device-state", "command-palette-recents.json"));
        Directory.CreateDirectory(Path.GetDirectoryName(recents)!);
        Assert.True(new CommandPaletteRecentsStore(recents).TrySave([ChordTable.Ids.CloseTab]));
        host.OpenThePalette();
        CommandPaletteViewModel palette = host.Palette;
        TextBox field = host.Shell.CommandPaletteSearchTextBox;
        Assert.Equal(ChordTable.Ids.CloseTab, palette.SelectedId);
        Assert.True(field.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next)), "Tab went nowhere");
        host.Settle();
        var row = Assert.IsType<ListBoxItem>(Keyboard.FocusedElement);
        Assert.Same(palette.SelectedRow, row.DataContext);

        PressOn(row);
        host.Settle();
        host.Heard.Clear();
        palette.Query = "s";
        Assert.True(PumpedDispatcher.PumpUntil(() => !palette.IsRankPending, TimeSpan.FromSeconds(10)), "\"s\" never published");
        host.Settle();

        Assert.Contains(palette.Rows, candidate => candidate.Id == ChordTable.Ids.CloseTab);
        Assert.NotEqual(ChordTable.Ids.CloseTab, palette.Rows[0].Id);
        Assert.Equal(ChordTable.Ids.CloseTab, palette.SelectedId);
        Assert.Empty(host.Heard.OfType<A11yEvent.PaletteCommandSelected>());
    });

    /// <summary>The lead's ruling (#1834): a press while the palette is sealed changes nothing,
    /// as every other move does nothing then (T6′). The press does not make the palette's row
    /// the reader's, so once the seal lifts a query it survives below the first row moves the
    /// selection to the first row.</summary>
    [Fact]
    public void APressWhileSealedChangesNothing() => RunSta(() =>
    {
        using var host = new ShippedResultsList(StandardCommands());
        CommandPaletteViewModel palette = host.Palette;
        palette.Open();
        host.Settle();
        Assert.Equal("slate.file.newNote", palette.SelectedId);
        var row = Assert.IsType<ListBoxItem>(host.List.ItemContainerGenerator.ContainerFromItem(palette.SelectedRow));
        palette.SetModalLoop(true);
        Assert.True(palette.IsSealed);
        host.ClearObservations();

        PressOn(row);
        host.Settle();
        Assert.Equal("slate.file.newNote", palette.SelectedId);
        Assert.Empty(host.Harness.Announcements);
        palette.SetModalLoop(false);
        Assert.False(palette.IsSealed);
        host.ChangeQuery("o");

        Assert.Contains("slate.file.newNote", host.Harness.RowIds);
        Assert.NotEqual("slate.file.newNote", palette.Rows[0].Id);
        Assert.Equal(palette.Rows[0].Id, palette.SelectedId);
        Assert.Empty(host.SelectionAnnouncements);
    });

    /// <summary>A left button's press on <paramref name="row"/>, as the pointer's preview
    /// reaches the list: the press alone, with none of the click's own focus or capture.</summary>
    private static void PressOn(ListBoxItem row) =>
        row.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
        {
            RoutedEvent = Mouse.PreviewMouseDownEvent,
            Source = row,
        });

    /// <summary>
    /// The red team's case: Tab lands the keys on the palette's own row, a Recent row whose hint
    /// matches "settings"; Shift+Tab takes them back to the field, and the name typed then runs
    /// Settings…. Had the Tab made the row the reader's, it would survive the name, and Enter
    /// would run it (#1834's second cause).
    /// </summary>
    [Fact]
    public void TabbingIntoTheListAndBackLeavesThePalettesRowThePalettes() => RunSta(() =>
    {
        using var host = new PaletteShellHost();
        host.Lifecycle.ThemePreference = new ThemePreference(new AppPreferencesStore(host.FileInFixture("preferences.json")), _ => { });
        string recents = host.FileInFixture(Path.Combine("device-state", "command-palette-recents.json"));
        Directory.CreateDirectory(Path.GetDirectoryName(recents)!);
        Assert.True(new CommandPaletteRecentsStore(recents).TrySave([ChordTable.Ids.CloseTab]));
        host.OpenThePalette();
        CommandPaletteViewModel palette = host.Palette;
        TextBox field = host.Shell.CommandPaletteSearchTextBox;
        Assert.Equal(ChordTable.Ids.CloseTab, palette.SelectedId);

        Assert.True(field.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next)), "Tab went nowhere");
        host.Settle();
        var row = Assert.IsType<ListBoxItem>(Keyboard.FocusedElement);
        Assert.Same(palette.SelectedRow, row.DataContext);
        Assert.True(field.Focus(), "the keys did not come back to the field");
        host.Settle();
        host.AssertFocusIsOn(field);

        const string Name = "settings";
        for (int typed = 1; typed <= Name.Length; typed++)
        {
            field.Text = Name[..typed];
            Assert.True(
                PumpedDispatcher.PumpUntil(() => !palette.IsRankPending, TimeSpan.FromSeconds(10)),
                $"\"{Name[..typed]}\" never published");
            host.Settle();
        }

        Assert.Contains(palette.Rows, candidate => candidate.Id == ChordTable.Ids.CloseTab);
        Assert.Equal(ChordTable.Ids.OpenSettings, palette.SelectedId);
        host.PressEnterInThePalette();
        host.Settle();
        Assert.False(palette.IsOpen);
        Assert.NotNull(host.Lifecycle.SettingsSheet);
    });

    /// <summary>
    /// With the keys on a row, the window's arrows move the selection and leave the keys where
    /// they were (filed separately). A later focus restore — the window activated again, a
    /// dialog closing back to the list — lands the keys on that row again with nothing before
    /// them, and that is no move of the reader's: the selection stays where they put it.
    /// </summary>
    [Fact]
    public void AFocusRestoreDoesNotMoveTheSelection() => RunSta(() =>
    {
        using var host = new ShippedResultsList(StandardCommands());
        CommandPaletteViewModel palette = host.Palette;
        palette.Open();
        host.Settle();
        var keys = Assert.IsType<ListBoxItem>(host.List.ItemContainerGenerator.ContainerFromItem(palette.SelectedRow));
        Assert.True(keys.Focus(), "premise: the palette's row refused the keys");
        host.Settle();
        palette.MoveSelection(1);
        host.Settle();
        Assert.Equal("slate.file.save", palette.SelectedId);
        Assert.Same(keys, Keyboard.FocusedElement);
        var olds = new List<IInputElement?>();
        KeyboardFocusChangedEventHandler record = (_, e) => olds.Add(e.OldFocus);
        Keyboard.AddGotKeyboardFocusHandler(host.List, record);

        Keyboard.ClearFocus();
        host.Settle();
        Assert.True(keys.Focus(), "premise: the row refused the restored keys");
        host.Settle();
        Keyboard.RemoveGotKeyboardFocusHandler(host.List, record);

        Assert.Null(Assert.Single(olds));
        Assert.Equal("slate.file.save", palette.SelectedId);
    });

    /// <summary>With the keys on a row, a typed letter goes to the list's own search, which
    /// matches the rows' labels: the keys and the selection land together on a row whose label
    /// starts with it, silently (the screen reader speaks the row).</summary>
    [Fact]
    public void ALetterTypedInTheListLandsOnARowItsLabelStartsWith() => RunSta(() =>
    {
        using var host = new ShippedResultsList(StandardCommands());
        CommandPaletteViewModel palette = host.Palette;
        palette.Open();
        host.Settle();
        var row = Assert.IsType<ListBoxItem>(host.List.ItemContainerGenerator.ContainerFromItem(palette.SelectedRow));
        Assert.True(row.Focus(), "premise: the palette's row refused the keys");
        host.Settle();
        host.ClearObservations();

        TextCompositionManager.StartComposition(new TextComposition(InputManager.Current, row, "t"));
        host.Settle();

        var keys = Assert.IsType<ListBoxItem>(Keyboard.FocusedElement);
        var landed = Assert.IsType<CommandPaletteRowViewModel>(keys.DataContext);
        Assert.StartsWith("T", landed.Label, StringComparison.Ordinal);
        Assert.Same(landed, palette.SelectedRow);
        Assert.Same(landed, host.List.SelectedItem);
        Assert.Empty(host.SelectionAnnouncements);
    });

    /// <summary>With the keys on a row in the list, a publication leaves the keys and the
    /// selection on one row — whether the reader's row survives the query or goes.</summary>
    [Fact]
    public void TheKeysAndTheSelectionStayOnOneRowAcrossARepublish() => RunSta(() =>
    {
        using var host = new ShippedResultsList(StandardCommands());
        CommandPaletteViewModel palette = host.Palette;
        palette.Open();
        host.Settle();
        // The keys on the palette's row first, as Tab lands them; a click then takes them.
        var selected = Assert.IsType<ListBoxItem>(host.List.ItemContainerGenerator.ContainerFromItem(palette.SelectedRow));
        Assert.True(selected.Focus(), "premise: the palette's row refused the keys");
        host.Settle();
        var row = Assert.IsType<ListBoxItem>(host.List.ItemContainerGenerator.ContainerFromItem(palette.Rows[3]));
        Assert.True(row.Focus(), "premise: Toggle Bold's row refused the keys");
        host.Settle();
        Assert.Equal("slate.editor.bold", palette.SelectedId);

        void AssertOneRow(string query)
        {
            var keys = Assert.IsType<ListBoxItem>(Keyboard.FocusedElement);
            Assert.Same(host.List, ItemsControl.ItemsControlFromItemContainer(keys));
            Assert.True(
                ReferenceEquals(keys.DataContext, palette.SelectedRow),
                $"after \"{query}\" the keys are on {(keys.DataContext as CommandPaletteRowViewModel)?.Id} "
                + $"and the selection is {palette.SelectedId}");
            Assert.Same(palette.SelectedRow, host.List.SelectedItem);
        }

        host.ChangeQuery("o");
        AssertOneRow("o");
        Assert.Equal("slate.editor.bold", palette.SelectedId);

        host.ChangeQuery("q");
        AssertOneRow("q");
        Assert.Equal("slate.nav.quickOpen", palette.SelectedId);
        Assert.Equal(["Quick Open"], host.SelectionAnnouncements);

        // The list carried the keys onto the replacement, which is the palette's, not a row
        // the reader moved to: a query that keeps it without leading with it moves on.
        host.ChangeQuery("e");
        Assert.Contains("slate.nav.quickOpen", host.Harness.RowIds);
        Assert.NotEqual("slate.nav.quickOpen", palette.Rows[0].Id);
        Assert.Equal(palette.Rows[0].Id, palette.SelectedId);
        AssertOneRow("e");
        Assert.Empty(host.SelectionAnnouncements);
    });

    // --- helpers ---------------------------------------------------------------------

    private static Command Registered(string id)
    {
        ChordTableEntry row = Assert.Single(ChordTable.RegisteredRows, candidate => candidate.Id == id);
        return new Command(row.Id, row.Label, row.Hint, row.WindowsChord, row.Section);
    }

    private static void PumpUntilPublished(CommandPaletteViewModel palette)
    {
        Assert.True(
            PumpedDispatcher.PumpUntil(() => !palette.IsRankPending, TimeSpan.FromSeconds(10)),
            "the rank never published");
        PumpedDispatcher.PumpUntilDrained(palette.FilterCountCompletion);
        PumpedDispatcher.Drain();
    }

    /// <summary>The item's row is realized, highlighted and inside the list's viewport.</summary>
    private static void AssertRealizedAndOnScreen(ListBox list, object item)
    {
        var container = list.ItemContainerGenerator.ContainerFromItem(item) as ListBoxItem;
        Assert.True(container is not null, "the selected row was never realized");
        Assert.True(container!.IsSelected, "the selected row is not highlighted");
        ScrollContentPresenter viewport = Assert.Single(DescendantsOf<ScrollContentPresenter>(list));
        Rect bounds = container.TransformToAncestor(viewport).TransformBounds(new Rect(container.RenderSize));
        Assert.True(
            bounds.Top >= -0.5 && bounds.Bottom <= viewport.ActualHeight + 0.5,
            $"the selected row is at {bounds}, outside the viewport 0..{viewport.ActualHeight}");
    }

    private static IEnumerable<T> DescendantsOf<T>(DependencyObject root)
        where T : DependencyObject
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, index);
            if (child is T match)
            {
                yield return match;
            }

            foreach (T nested in DescendantsOf<T>(child))
            {
                yield return nested;
            }
        }
    }
}
