// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Reflection;
using System.Text.RegularExpressions;
using SlateWindows.Commands;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W5-1 (#741) command-palette view-model facts. Ranking, section order,
/// section titles, and match spans come from the real
/// <c>palette_sections</c> through the binding — a fake ranker would let
/// the host's layout drift from core's, which is the exact failure
/// contract P1 exists to prevent.
/// </summary>
public sealed class CommandPaletteTests
{
    // --- P1: core ranks, the host renders --------------------------------

    [Fact]
    public void SectionsRenderInCoreOrderNotCommandSectionEnumOrder()
    {
        // SECTION_ORDER puts Canvas / Bases / Graph / Sidebar between
        // Editor and Tasks; the generated enum declares them after
        // Plugins. Sorting by enum value would yield
        // Editor, Tasks, Canvas, Sidebar.
        var harness = new PaletteHarness(
            Cmd("slate.tasks.review", "Tasks Review", CommandSection.Tasks),
            Cmd("slate.canvas.addCard", "Add Card", CommandSection.Canvas),
            Cmd("slate.sidebar.open", "Open Sidebar", CommandSection.Sidebar),
            Cmd("slate.editor.bold", "Toggle Bold", CommandSection.Editor));

        harness.Palette.Open();

        Assert.Equal(
            ["Editor", "Canvas", "Sidebar", "Tasks"],
            harness.SectionTitles);
        Assert.Equal(
            ["slate.editor.bold", "slate.canvas.addCard", "slate.sidebar.open", "slate.tasks.review"],
            harness.RowIds);
    }

    [Fact]
    public void RecentSectionIsSnapshotFirstAndItsRowsLeaveTheirNativeSections()
    {
        PaletteHarness harness = StandardHarness();
        harness.Source.Recents.Add("slate.editor.bold");

        harness.Palette.Open();

        Assert.Equal(["Recent", "File", "Navigation", "Tasks"], harness.SectionTitles);
        Assert.Null(harness.Palette.Sections[0].Kind);
        Assert.Equal("slate.editor.bold", Assert.Single(harness.Palette.Sections[0].Rows).Id);
        Assert.DoesNotContain("Editor", harness.SectionTitles);
    }

    [Fact]
    public void SidebarPinnedOrderIsForwardedToCoreSoTheCatalogOrderSurvives()
    {
        // Registry.List() sorts by (section, id), so forwarding an empty
        // list would render Sidebar alphabetically instead of in catalog
        // order (contract P16).
        var harness = new PaletteHarness(
            Cmd("slate.sidebar.aaa", "Alpha", CommandSection.Sidebar),
            Cmd("slate.sidebar.zzz", "Zulu", CommandSection.Sidebar));
        harness.Source.SidebarPinnedOrder = ["slate.sidebar.zzz", "slate.sidebar.aaa"];

        harness.Palette.Open();

        Assert.Equal(["slate.sidebar.zzz", "slate.sidebar.aaa"], harness.RowIds);
    }

    [Fact]
    public void ScoreIdentifiesTheStrongestMatchWithoutReorderingTheDisplay()
    {
        var harness = new PaletteHarness(
            Cmd("a.file.print", "Print Note", CommandSection.File, "Print or save the note"),
            Cmd("z.editor.save", "Save", CommandSection.Editor));

        harness.Palette.Open();
        harness.Palette.Query = "save";

        // Display order stays section-grouped: File before Editor.
        Assert.Equal(["a.file.print", "z.editor.save"], harness.RowIds);
        CommandPaletteRowViewModel strongest = harness.Palette.Rows
            .OrderByDescending(row => row.Score)
            .ThenBy(row => row.Id, StringComparer.Ordinal)
            .First();
        Assert.Equal("z.editor.save", strongest.Id);
    }

    // --- P18: one computation per query change ---------------------------

    [Fact]
    public void SectionsAreComputedOncePerQueryChangeAndStored()
    {
        PaletteHarness harness = StandardHarness();
        harness.Palette.Open();

        IReadOnlyList<CommandPaletteSectionViewModel> first = harness.Palette.Sections;
        Assert.Same(first, harness.Palette.Sections);
        Assert.Same(harness.Palette.Rows[0], harness.Palette.Sections[0].Rows[0]);

        harness.Palette.Query = "save";
        Assert.NotSame(first, harness.Palette.Sections);
    }

    // --- P4: the snapshot rule -------------------------------------------

    [Fact]
    public void CommandsAndRecentsAreSnapshotOncePerOpenNeverPerKeystroke()
    {
        PaletteHarness harness = StandardHarness();

        harness.Palette.Open();
        harness.Palette.Query = "s";
        harness.Palette.Query = "sa";
        harness.Palette.Query = "sav";
        harness.Palette.Query = string.Empty;

        Assert.Equal(1, harness.Source.ListCommandsCalls);
        Assert.Equal(1, harness.Source.LoadRecentsCalls);

        // A command registered after the palette opened stays invisible
        // until the next open.
        harness.Source.Commands.Add(Cmd("slate.file.late", "Late Arrival", CommandSection.File));
        harness.Palette.Query = "late";
        Assert.Empty(harness.Palette.Rows);

        harness.Palette.Dismiss();
        harness.Palette.Open();
        harness.Palette.Query = "late";
        Assert.Equal("slate.file.late", Assert.Single(harness.Palette.Rows).Id);
        Assert.Equal(2, harness.Source.ListCommandsCalls);
    }

    /// <summary>
    /// Re-opening an already-open palette clears the query WITHOUT taking
    /// a second snapshot.
    /// </summary>
    /// <remarks>
    /// Codex's round-4 high, and the one finding of that round that was a
    /// product defect rather than a test-quality one. PD-2 makes the chord
    /// re-open rather than toggle, so <c>Open</c> is re-entered while open
    /// — and it re-read the registry and re-read recents from disk on
    /// every press, three lines below a comment promising a whole-lifetime
    /// snapshot. The existing P4 fact only covered keystrokes and
    /// dismiss-then-open, so the reachable path was the uncovered one.
    /// </remarks>
    [Fact]
    public void ReOpeningWhileOpenClearsTheQueryButKeepsTheOriginalSnapshot()
    {
        PaletteHarness harness = StandardHarness();
        harness.Palette.Open();
        harness.Palette.Query = "save";

        // A command registered after the first open must stay invisible:
        // the re-open is the same lifetime, not a new one.
        harness.Source.Commands.Add(
            Cmd("slate.file.late", "Late Arrival", CommandSection.File));
        harness.Source.Recents.Add("slate.file.late");

        harness.Palette.Open();

        Assert.True(harness.Palette.IsOpen);
        Assert.Equal(string.Empty, harness.Palette.Query);
        Assert.Equal(1, harness.Source.ListCommandsCalls);
        Assert.Equal(1, harness.Source.LoadRecentsCalls);

        harness.Palette.Query = "late";
        Assert.Empty(harness.Palette.Rows);
        Assert.DoesNotContain("Recent", harness.SectionTitles);
    }

    // --- P6 / PINV-6: byte -> UTF-16 conversion ---------------------------

    [Fact]
    public void MatchRunsConvertUtf8ByteOffsetsToUtf16CodeUnits()
    {
        // U+1F5C2 is 4 UTF-8 bytes and 2 UTF-16 code units, so every
        // offset after it differs between the two indexings. Core
        // reports "Open" at bytes [5, 9).
        const string label = "\U0001F5C2 Open Vault…";

        IReadOnlyList<CommandPaletteMatchRun> runs =
            CommandPaletteViewModel.ToMatchRuns(label, [new MatchSpan(5, 9)]);

        CommandPaletteMatchRun run = Assert.Single(runs);
        Assert.Equal(new CommandPaletteMatchRun(3, 4), run);
        Assert.Equal("Open", label.Substring(run.Start, run.Length));
    }

    [Fact]
    public void MatchRunsNeverSplitASurrogatePairOrDropOutOfRangeSpans()
    {
        const string label = "\U0001F5C2ab";

        // A span interior to the emoji clamps to its boundary rather
        // than cutting the surrogate pair in half.
        Assert.Equal(
            [new CommandPaletteMatchRun(0, 2)],
            CommandPaletteViewModel.ToMatchRuns(label, [new MatchSpan(1, 4)]));
        // Degenerate and past-the-end spans drop rather than throw.
        Assert.Empty(CommandPaletteViewModel.ToMatchRuns(label, [new MatchSpan(2, 2)]));
        Assert.Empty(CommandPaletteViewModel.ToMatchRuns(label, [new MatchSpan(90, 99)]));
        Assert.Equal(
            [new CommandPaletteMatchRun(2, 2)],
            CommandPaletteViewModel.ToMatchRuns(label, [new MatchSpan(4, 99)]));
    }

    [Fact]
    public void EllipsisLabelBoldsCharactersNotBytesThroughTheRealRanker()
    {
        // 28+ shipped labels end in a 3-byte, 1-char ellipsis. Reading
        // core's byte offsets as UTF-16 indexes here walks past the end
        // of the string.
        var harness = new PaletteHarness(
            Cmd("slate.file.rename", "Rename…", CommandSection.File));

        harness.Palette.Open();
        harness.Palette.Query = "e…";

        CommandPaletteRowViewModel row = Assert.Single(harness.Palette.Rows);
        Assert.Equal(
            [new CommandPaletteMatchRun(1, 1), new CommandPaletteMatchRun(6, 1)],
            row.LabelMatchRuns);
        Assert.Equal(
            ["R", "e", "name", "…"],
            row.LabelSegments.Select(segment => segment.Text));
        Assert.Equal(
            [false, true, false, true],
            row.LabelSegments.Select(segment => segment.IsMatch));
        Assert.Equal(row.Label, string.Concat(row.LabelSegments.Select(segment => segment.Text)));
    }

    [Fact]
    public void HintOnlyMatchKeepsTheRowAndRendersTheLabelFullyUnbolded()
    {
        var harness = new PaletteHarness(
            Cmd(
                "slate.vault.rescan",
                "Rescan Vault",
                CommandSection.Vault,
                "Walk the vault and refresh the index"),
            Cmd("slate.file.save", "Save", CommandSection.File));

        harness.Palette.Open();
        harness.Palette.Query = "walk";

        CommandPaletteRowViewModel row = Assert.Single(harness.Palette.Rows);
        Assert.Equal("slate.vault.rescan", row.Id);
        Assert.Empty(row.LabelMatchRuns);
        CommandPaletteLabelSegment segment = Assert.Single(row.LabelSegments);
        Assert.Equal("Rescan Vault", segment.Text);
        Assert.False(segment.IsMatch);
    }

    // --- P7 / PD-1: the keyboard model ------------------------------------

    [Fact]
    public void ArrowNavigationIsOneFlatCycleThatWrapsAcrossSections()
    {
        PaletteHarness harness = StandardHarness();
        harness.Palette.Open();

        Assert.Equal(
            [
                "slate.file.newNote",
                "slate.file.save",
                "slate.nav.quickOpen",
                "slate.editor.bold",
                "slate.tasks.review",
            ],
            harness.RowIds);
        Assert.Equal("slate.file.newNote", harness.Palette.SelectedId);

        harness.Palette.MoveSelection(1);
        Assert.Equal("slate.file.save", harness.Palette.SelectedId);
        // Crosses the File -> Navigation section boundary without
        // stopping on the header.
        harness.Palette.MoveSelection(1);
        Assert.Equal("slate.nav.quickOpen", harness.Palette.SelectedId);

        harness.Palette.SelectLast();
        harness.Palette.MoveSelection(1);
        Assert.Equal("slate.file.newNote", harness.Palette.SelectedId);
        harness.Palette.MoveSelection(-1);
        Assert.Equal("slate.tasks.review", harness.Palette.SelectedId);
    }

    [Fact]
    public void FromNoSelectionDownLandsOnTheFirstRowAndUpOnTheLast()
    {
        PaletteHarness harness = StandardHarness();
        harness.Palette.Open();
        CommandPaletteRowViewModel stale = harness.Palette.Rows[1];
        Assert.Equal("slate.file.save", stale.Id);

        // "Save" has no 'o', so the stale row leaves the result set —
        // activating it is how a host reaches the no-selection state
        // with rows still on screen.
        harness.Palette.Query = "o";
        Assert.Equal(
            ["slate.file.newNote", "slate.nav.quickOpen", "slate.editor.bold"],
            harness.RowIds);

        harness.Palette.Select(stale);
        Assert.Null(harness.Palette.SelectedId);
        Assert.Null(harness.Palette.SelectedRow);

        harness.Palette.MoveSelection(1);
        Assert.Equal("slate.file.newNote", harness.Palette.SelectedId);

        harness.Palette.Select(stale);
        harness.Palette.MoveSelection(-1);
        Assert.Equal("slate.editor.bold", harness.Palette.SelectedId);
    }

    [Fact]
    public void HomeEndAndPageKeysNavigateTheFlatCycleAndClampAtTheEnds()
    {
        PaletteHarness harness = StandardHarness();
        harness.Palette.Open();
        harness.Palette.PageSize = 2;

        harness.Palette.SelectLast();
        Assert.Equal("slate.tasks.review", harness.Palette.SelectedId);
        harness.Palette.SelectFirst();
        Assert.Equal("slate.file.newNote", harness.Palette.SelectedId);

        harness.Palette.MovePage(1);
        Assert.Equal("slate.nav.quickOpen", harness.Palette.SelectedId);
        harness.Palette.MovePage(1);
        Assert.Equal("slate.tasks.review", harness.Palette.SelectedId);
        // Page keys clamp; they do not wrap the way the arrows do.
        harness.Palette.MovePage(1);
        Assert.Equal("slate.tasks.review", harness.Palette.SelectedId);
        harness.Palette.MovePage(-1);
        Assert.Equal("slate.nav.quickOpen", harness.Palette.SelectedId);
        harness.Palette.MovePage(-1);
        Assert.Equal("slate.file.newNote", harness.Palette.SelectedId);
        harness.Palette.MovePage(-1);
        Assert.Equal("slate.file.newNote", harness.Palette.SelectedId);
    }

    [Fact]
    public void SelectionIsPreservedWhenTheSelectedIdSurvivesAQueryChange()
    {
        PaletteHarness harness = StandardHarness();
        harness.Palette.Open();
        harness.Palette.Select(harness.Palette.Rows[3]);
        Assert.Equal("slate.editor.bold", harness.Palette.SelectedId);
        harness.Announcements.Clear();

        harness.Palette.Query = "o";

        // The survivor is deliberately the LAST row: if it were the
        // first, "preserved" and "snapped to the first row" would be
        // indistinguishable and this fact would pass vacuously.
        Assert.Equal(
            ["slate.file.newNote", "slate.nav.quickOpen", "slate.editor.bold"],
            harness.RowIds);
        Assert.Equal("slate.editor.bold", harness.Palette.SelectedId);
        // Preserved selection is not a selection change, so the AT hears
        // only the filter count.
        Assert.IsType<A11yEvent.PaletteFilterCount>(Assert.Single(harness.Announcements));
    }

    [Fact]
    public void SelectionSnapsToTheFirstRowWhenTheSelectedIdVanishes()
    {
        PaletteHarness harness = StandardHarness();
        harness.Palette.Open();
        harness.Palette.SelectLast();
        Assert.Equal("slate.tasks.review", harness.Palette.SelectedId);

        // "Tasks Review" has no 'o', so the selection vanishes into a
        // three-row result — first and last are distinguishable.
        harness.Palette.Query = "o";

        Assert.Equal(
            ["slate.file.newNote", "slate.nav.quickOpen", "slate.editor.bold"],
            harness.RowIds);
        Assert.Equal("slate.file.newNote", harness.Palette.SelectedId);
    }

    [Fact]
    public void SelectionBecomesNullOnZeroMatchesAndRecoversOnTheNextQuery()
    {
        PaletteHarness harness = StandardHarness();
        harness.Palette.Open();

        harness.Palette.Query = "zzzzzz";

        Assert.Empty(harness.Palette.Rows);
        Assert.Null(harness.Palette.SelectedId);
        Assert.Null(harness.Palette.SelectedRow);
        Assert.True(harness.Palette.ShowsNoMatches);

        harness.Palette.Query = "save";
        Assert.Equal("slate.file.save", harness.Palette.SelectedId);
        Assert.False(harness.Palette.ShowsNoMatches);
    }

    // --- P8: unavailable commands are shown and selectable ---------------

    [Fact]
    public void UnavailableRowsKeepTheirPlaceInTheCycleAndCarryTheirReason()
    {
        PaletteHarness harness = StandardHarness();
        harness.Source.DisabledReasons["slate.file.save"] =
            SlateCommandRegistrar.StructuralMutationBusyReason;
        harness.Palette.Open();
        harness.Announcements.Clear();

        harness.Palette.MoveSelection(1);

        CommandPaletteRowViewModel row = Assert.Single(
            harness.Palette.Rows,
            candidate => candidate.Id == "slate.file.save");
        Assert.True(row.IsUnavailable);
        Assert.Equal(SlateCommandRegistrar.StructuralMutationBusyReason, row.DisabledReason);
        Assert.Equal("slate.file.save", harness.Palette.SelectedId);
        A11yEvent.PaletteCommandSelected selected =
            Assert.IsType<A11yEvent.PaletteCommandSelected>(Assert.Single(harness.Announcements));
        Assert.Equal("Save", selected.Label);
        Assert.Equal(SlateCommandRegistrar.StructuralMutationBusyReason, selected.DisabledReason);

        // Still a stop in the cycle: Down continues past it.
        harness.Palette.MoveSelection(1);
        Assert.Equal("slate.nav.quickOpen", harness.Palette.SelectedId);
    }

    // --- P9: invocation ordering ------------------------------------------

    [Fact]
    public void SuccessfulInvocationRestoresFocusInvokesRecordsThenDismisses()
    {
        PaletteHarness harness = StandardHarness();
        harness.Palette.Open();
        harness.Palette.Query = "save";
        harness.Log.Clear();
        harness.Source.LogAvailabilityChecks = true;

        harness.Palette.InvokeSelected();

        Assert.Equal(
            [
                "focus",
                "availability:slate.file.save",
                "invoke:slate.file.save",
                "record:slate.file.save",
                "dismiss",
            ],
            harness.Log);
        Assert.False(harness.Palette.IsOpen);
    }

    /// <summary>
    /// The shell's focus subscriber moves focus <b>now</b>, not on a
    /// queued dispatcher callback.
    /// </summary>
    /// <remarks>
    /// Codex's round-3 medium, and the gap the ordering test above cannot
    /// see: it logs <c>"focus"</c> when the EVENT is raised, so the
    /// production subscriber could be replaced with a no-op and the
    /// sequence would still read correctly. The subscriber originally
    /// queued <c>Focus()</c> at <c>Input</c> priority, which meant that on
    /// a double-click the <c>ListBoxItem</c> still held focus while the
    /// availability gate ran, the unavailable reason was announced, and
    /// the command itself executed — P9's ordering satisfied on paper and
    /// not in fact.
    ///
    /// A source fact rather than a runtime one because the subscriber is a
    /// <c>MainWindow</c> member and a real shell would be needed to raise
    /// it. It catches both regressions that matter: deleting the call, and
    /// putting it back on the dispatcher.
    /// </remarks>
    [Fact]
    public void TheShellFocusesTheSearchBoxSynchronously()
    {
        string body = SourceText.WithoutComments(
            File.ReadAllText(Path.Combine(
                SourceText.ShellSourceRoot(), "MainWindow.Palette.cs")));
        Match subscriber = Regex.Match(
            body,
            @"private void Palette_SearchFocusRequested\([^)]*\)\s*=>(?<body>[^;]*);");
        Assert.True(
            subscriber.Success,
            "Palette_SearchFocusRequested is gone or no longer an expression "
            + "body; P9's focus step has no gate until this scrape is updated.");

        string call = subscriber.Groups["body"].Value;
        Assert.Contains("CommandPaletteSearchTextBox.Focus()", call, StringComparison.Ordinal);
        Assert.DoesNotContain("InvokeAsync", call, StringComparison.Ordinal);
        Assert.DoesNotContain("BeginInvoke", call, StringComparison.Ordinal);
    }

    [Fact]
    public void UnavailableCommandAnnouncesItsReasonVerbatimAndNeverReachesInvoke()
    {
        const string reason = "Wait for the current save to finish.";
        PaletteHarness harness = StandardHarness();
        harness.Palette.Open();
        harness.Palette.Query = "save";
        // Availability flips after the row was rendered; the gate must
        // re-ask rather than trust the render-time value.
        Assert.False(Assert.Single(harness.Palette.Rows).IsUnavailable);
        harness.Source.DisabledReasons["slate.file.save"] = reason;
        harness.Log.Clear();
        harness.Announcements.Clear();
        harness.Source.LogAvailabilityChecks = true;

        harness.Palette.InvokeSelected();

        Assert.Equal(
            ["focus", "availability:slate.file.save", "announce:unavailable:" + reason],
            harness.Log);
        Assert.Empty(harness.Source.Invoked);
        Assert.Empty(harness.Source.Recorded);
        Assert.True(harness.Palette.IsOpen);
        A11yEvent.PaletteCommandUnavailable announced =
            Assert.IsType<A11yEvent.PaletteCommandUnavailable>(
                Assert.Single(harness.Announcements));
        // Verbatim: no "Unavailable: " prefix composed host-side.
        Assert.Equal(reason, announced.Reason);
    }

    [Fact]
    public void ActionFailedAnnouncesTheFailureAndLeavesThePaletteOpen()
    {
        PaletteHarness harness = StandardHarness();
        harness.Source.InvokeFailures["slate.file.save"] =
            new CommandException.ActionFailed("Disk is full.");
        harness.Palette.Open();
        harness.Palette.Query = "save";
        harness.Announcements.Clear();
        harness.Log.Clear();

        harness.Palette.InvokeSelected();

        Assert.True(harness.Palette.IsOpen);
        Assert.Equal(["slate.file.save"], harness.Source.Invoked);
        Assert.Empty(harness.Source.Recorded);
        Assert.DoesNotContain("dismiss", harness.Log);
        A11yEvent.PaletteCommandFailed failed =
            Assert.IsType<A11yEvent.PaletteCommandFailed>(Assert.Single(harness.Announcements));
        Assert.Equal("Save", failed.Label);
        Assert.Equal("Disk is full.", failed.Detail);
    }

    [Fact]
    public void UnknownIdAnnouncesNotFoundAndLeavesThePaletteOpen()
    {
        PaletteHarness harness = StandardHarness();
        harness.Source.InvokeFailures["slate.file.save"] =
            new CommandException.UnknownId("slate.file.save");
        harness.Palette.Open();
        harness.Palette.Query = "save";
        harness.Announcements.Clear();
        harness.Log.Clear();

        harness.Palette.InvokeSelected();

        Assert.True(harness.Palette.IsOpen);
        Assert.Empty(harness.Source.Recorded);
        Assert.DoesNotContain("dismiss", harness.Log);
        A11yEvent.PaletteCommandNotFound notFound =
            Assert.IsType<A11yEvent.PaletteCommandNotFound>(Assert.Single(harness.Announcements));
        Assert.Equal("slate.file.save", notFound.Id);
    }

    /// <summary>
    /// The §2.6 residue budget the owner approved on 2026-08-13: the
    /// palette adds exactly two host-composed availability strings and no
    /// third.
    /// </summary>
    /// <remarks>
    /// Pinned rather than left as a comment because a budget enforced only
    /// by convention drifts upward one plausible string at a time. This
    /// drives every announcing path the palette has — selection,
    /// filtering, all three failure outcomes, and the no-vault refusal —
    /// and asserts the residue set is exactly the approved pair. A new
    /// host-composed reason fails here and goes back to the owner.
    /// </remarks>
    [Fact]
    public void ThePaletteAddsExactlyTheTwoApprovedHostComposedTexts()
    {
        PaletteHarness harness = StandardHarness();
        harness.Source.DisabledReasons["slate.tasks.review"] =
            SlateCommandRegistrar.UnavailableReason;
        harness.Source.InvokeFailures["slate.file.save"] =
            new CommandException.ActionFailed(SlateCommandRegistrar.NoVaultReason);

        harness.Palette.Open();
        harness.Palette.Query = "review";
        harness.Palette.InvokeSelected();
        harness.Palette.Query = "save";
        harness.Palette.InvokeSelected();
        harness.Palette.Query = "zzznothingmatches";
        harness.Palette.Dismiss();

        harness.Source.IsVaultOpen = false;
        harness.Palette.Open();

        string[] residue = harness.Announcements
            .OfType<A11yEvent.HostComposed>()
            .Select(announcement => announcement.Text)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(text => text, StringComparer.Ordinal)
            .ToArray();

        // The palette itself composes nothing: its availability copy
        // travels as PaletteCommandUnavailable, which core renders.
        Assert.Empty(residue);

        // Asserting the reasons that came back out of the fake would be
        // circular — this test seeded them. The budget lives on the
        // command layer, so pin THAT: reflect over the registrar's string
        // constants and require exactly the approved vocabulary. A fourth
        // string added anywhere in the bridge fails here and goes back to
        // the owner, which a round-trip through a fake could never catch.
        string[] declaredCopy = typeof(SlateCommandRegistrar)
            .GetFields(BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(field => field.IsLiteral && !field.IsInitOnly && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .OrderBy(text => text, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            new[]
            {
                // The two the owner approved on 2026-08-13.
                SlateCommandRegistrar.NoVaultReason,
                SlateCommandRegistrar.UnavailableReason,

                // Not new copy: mac authored this one, and the bridge
                // mirrors it byte-for-byte so a refusal carrying it is
                // classified as a rejection rather than a failure.
                SlateCommandRegistrar.StructuralMutationBusyReason,
            }.OrderBy(text => text, StringComparer.Ordinal),
            declaredCopy);
    }

    [Theory]
    [InlineData(nameof(SlateCommandRegistrar.NoVaultReason))]
    [InlineData(nameof(SlateCommandRegistrar.UnavailableReason))]
    public void TheBridgesOwnRefusalsRouteToUnavailableNotFailed(string which)
    {
        // The regression this pins: the palette used to compare against
        // mac's structural-busy string, so a refusal thrown by the WINDOWS
        // bridge — which uses different copy — fell through to
        // PaletteCommandFailed and announced "Save failed: Open a vault to
        // use this command." A rejection reported as a failure.
        string reason = which == nameof(SlateCommandRegistrar.NoVaultReason)
            ? SlateCommandRegistrar.NoVaultReason
            : SlateCommandRegistrar.UnavailableReason;

        PaletteHarness harness = StandardHarness();
        harness.Source.InvokeFailures["slate.file.save"] =
            new CommandException.ActionFailed(reason);
        harness.Palette.Open();
        harness.Palette.Query = "save";
        harness.Announcements.Clear();

        harness.Palette.InvokeSelected();

        A11yEvent.PaletteCommandUnavailable unavailable =
            Assert.IsType<A11yEvent.PaletteCommandUnavailable>(
                Assert.Single(harness.Announcements));
        // Verbatim, no prefix (contract P10).
        Assert.Equal(reason, unavailable.Reason);
        Assert.Empty(harness.Source.Recorded);
        Assert.DoesNotContain("dismiss", harness.Log);
    }

    [Fact]
    public void StructuralBusyActionFailedRoutesToUnavailableNotFailed()
    {
        PaletteHarness harness = StandardHarness();
        harness.Source.InvokeFailures["slate.file.save"] = new CommandException.ActionFailed(
            SlateCommandRegistrar.StructuralMutationBusyReason);
        harness.Palette.Open();
        harness.Palette.Query = "save";
        harness.Announcements.Clear();

        harness.Palette.InvokeSelected();

        A11yEvent.PaletteCommandUnavailable unavailable =
            Assert.IsType<A11yEvent.PaletteCommandUnavailable>(
                Assert.Single(harness.Announcements));
        Assert.Equal(
            SlateCommandRegistrar.StructuralMutationBusyReason,
            unavailable.Reason);
        Assert.True(harness.Palette.IsOpen);
    }

    [Fact]
    public void InvokingWithNoSelectionDoesNothing()
    {
        PaletteHarness harness = StandardHarness();
        harness.Palette.Open();
        harness.Palette.Query = "zzzzzz";
        harness.Log.Clear();

        harness.Palette.InvokeSelected();

        Assert.Empty(harness.Log);
        Assert.True(harness.Palette.IsOpen);
    }

    // --- P10: announcement triggers ---------------------------------------

    [Fact]
    public void FilterCountFiresOnEveryNonEmptyKeystrokeAndIsSuppressedOnEmptyQuery()
    {
        PaletteHarness harness = StandardHarness();

        harness.Palette.Open();
        Assert.Empty(harness.Announcements);

        harness.Palette.Query = "s";
        harness.Palette.Query = "sa";
        harness.Palette.Query = "sav";

        uint[] counts = [.. harness.Announcements
            .OfType<A11yEvent.PaletteFilterCount>()
            .Select(count => count.Count)];
        Assert.Equal(3, counts.Length);
        Assert.Equal(
            ["s", "sa", "sav"],
            harness.Announcements.OfType<A11yEvent.PaletteFilterCount>().Select(c => c.Query));
        Assert.Equal(1u, counts[^1]);

        harness.Announcements.Clear();
        harness.Palette.Query = string.Empty;
        Assert.Empty(harness.Announcements.OfType<A11yEvent.PaletteFilterCount>());
    }

    [Fact]
    public void FirstSelectionChangeAfterOpenIsSilentAndTheSecondIsNot()
    {
        PaletteHarness harness = StandardHarness();

        harness.Palette.Open();

        // Open selects the first row; that change is suppressed.
        Assert.Equal("slate.file.newNote", harness.Palette.SelectedId);
        Assert.Empty(harness.Announcements.OfType<A11yEvent.PaletteCommandSelected>());

        harness.Palette.MoveSelection(1);
        A11yEvent.PaletteCommandSelected selected = Assert.Single(
            harness.Announcements.OfType<A11yEvent.PaletteCommandSelected>());
        Assert.Equal("Save", selected.Label);
        Assert.Null(selected.DisabledReason);

        // Re-opening re-arms the suppression.
        harness.Announcements.Clear();
        harness.Palette.Open();
        Assert.Empty(harness.Announcements.OfType<A11yEvent.PaletteCommandSelected>());
    }

    // --- P14: open / close and host copy ----------------------------------

    [Fact]
    public void OpeningWithoutAVaultAnnouncesAndLeavesThePaletteClosed()
    {
        PaletteHarness harness = StandardHarness();
        harness.Source.IsVaultOpen = false;

        harness.Palette.Open();

        Assert.False(harness.Palette.IsOpen);
        Assert.Empty(harness.Palette.Rows);
        Assert.Equal(0, harness.Source.ListCommandsCalls);
        Assert.IsType<A11yEvent.CommandPaletteNeedsVault>(Assert.Single(harness.Announcements));
        // Not the empty-registry state: the palette is simply not open.
        Assert.False(harness.Palette.ShowsEmptyRegistry);
    }

    [Fact]
    public void OpeningWhileOpenReopensRatherThanToggling()
    {
        PaletteHarness harness = StandardHarness();
        harness.Palette.Open();
        harness.Palette.Query = "save";
        Assert.Single(harness.Palette.Rows);

        harness.Palette.Open();

        Assert.True(harness.Palette.IsOpen);
        Assert.Equal(string.Empty, harness.Palette.Query);
        Assert.Equal(5, harness.Palette.Rows.Count);

        // Was `2`, and that pinned a behaviour the governing contract
        // forbids. P4: "Neither refreshes while the palette is open." A
        // re-open never closes the palette, so the snapshot must survive
        // it — the rows above are recomputed from the ORIGINAL array.
        // Codex found the contradiction between this line and P4; both
        // the line and the production code were mine.
        Assert.Equal(1, harness.Source.ListCommandsCalls);
    }

    [Fact]
    public void EmptyRegistryCopyIsTheContractCopy()
    {
        var harness = new PaletteHarness();

        harness.Palette.Open();

        Assert.True(harness.Palette.IsOpen);
        Assert.True(harness.Palette.ShowsEmptyRegistry);
        Assert.False(harness.Palette.ShowsNoMatches);
        Assert.Equal("No commands available", CommandPaletteViewModel.EmptyRegistryTitle);
        Assert.Equal(
            "Open a vault to access the palette.",
            CommandPaletteViewModel.EmptyRegistryDetail);
    }

    [Fact]
    public void NoMatchesCopyQuotesTheQueryOnlyInTheVisibleLine()
    {
        PaletteHarness harness = StandardHarness();
        harness.Palette.Open();

        harness.Palette.Query = "zzzzzz";

        Assert.True(harness.Palette.ShowsNoMatches);
        Assert.False(harness.Palette.ShowsEmptyRegistry);
        Assert.Equal("No matches", CommandPaletteViewModel.NoMatchesTitle);
        Assert.Equal(
            "No command matches \"zzzzzz\". Try fewer letters or a different word.",
            harness.Palette.NoMatchesDetail);
        Assert.Equal(
            "No command matches zzzzzz. Try fewer letters or a different word.",
            harness.Palette.NoMatchesAccessibleName);
    }

    [Fact]
    public void DismissClosesTheOverlayAndDropsItsRows()
    {
        PaletteHarness harness = StandardHarness();
        harness.Palette.Open();

        harness.Palette.Dismiss();

        Assert.False(harness.Palette.IsOpen);
        Assert.Empty(harness.Palette.Rows);
        Assert.Empty(harness.Palette.Sections);
        Assert.Null(harness.Palette.SelectedRow);
        Assert.Contains("dismiss", harness.Log);
        Assert.Empty(harness.Announcements);
    }

    // --- helpers ----------------------------------------------------------

    private static PaletteHarness StandardHarness() => new(
        Cmd("slate.file.newNote", "New Note", CommandSection.File),
        Cmd("slate.file.save", "Save", CommandSection.File),
        Cmd("slate.nav.quickOpen", "Quick Open", CommandSection.Navigation),
        Cmd("slate.editor.bold", "Toggle Bold", CommandSection.Editor),
        Cmd("slate.tasks.review", "Tasks Review", CommandSection.Tasks));

    private static Command Cmd(
        string id,
        string label,
        CommandSection section,
        string? accessibilityHint = null,
        string? hotkeyHint = null) =>
        new(id, label, accessibilityHint, hotkeyHint, section);

    private sealed class PaletteHarness
    {
        public PaletteHarness(params Command[] commands)
        {
            Source = new FakePaletteCommandSource(Log);
            Source.Commands.AddRange(commands);
            Palette = new CommandPaletteViewModel(Source, Record);
            Palette.SearchFocusRequested += (_, _) => Log.Add("focus");
            Palette.Dismissed += (_, _) => Log.Add("dismiss");
        }

        public FakePaletteCommandSource Source { get; }

        public CommandPaletteViewModel Palette { get; }

        public List<A11yEvent> Announcements { get; } = [];

        public List<string> Log { get; } = [];

        public IEnumerable<string> RowIds => Palette.Rows.Select(row => row.Id);

        public IEnumerable<string> SectionTitles => Palette.Sections.Select(section => section.Title);

        private void Record(A11yEvent announcement)
        {
            Announcements.Add(announcement);
            Log.Add("announce:" + Describe(announcement));
        }

        private static string Describe(A11yEvent announcement) => announcement switch
        {
            A11yEvent.PaletteFilterCount count => $"filter:{count.Count}:{count.Query}",
            A11yEvent.PaletteCommandSelected selected =>
                $"selected:{selected.Label}:{selected.DisabledReason ?? "-"}",
            A11yEvent.PaletteCommandUnavailable unavailable => $"unavailable:{unavailable.Reason}",
            A11yEvent.PaletteCommandFailed failed => $"failed:{failed.Label}:{failed.Detail ?? "-"}",
            A11yEvent.PaletteCommandNotFound notFound => $"notfound:{notFound.Id}",
            A11yEvent.CommandPaletteNeedsVault => "needsvault",
            _ => announcement.GetType().Name,
        };
    }

    private sealed class FakePaletteCommandSource(List<string> log) : IPaletteCommandSource
    {
        public List<Command> Commands { get; } = [];

        public List<string> Recents { get; } = [];

        public string[] SidebarPinnedOrder { get; set; } = [];

        public Dictionary<string, string> DisabledReasons { get; } = [];

        public Dictionary<string, Exception> InvokeFailures { get; } = [];

        public List<string> Invoked { get; } = [];

        public List<string> Recorded { get; } = [];

        public bool IsVaultOpen { get; set; } = true;

        /// <summary>
        /// The availability vocabulary the command layer owns. Seeded with
        /// the real bridge's reasons so these facts bite the production
        /// strings rather than a fake's private invention.
        /// </summary>
        public HashSet<string> AvailabilityRejections { get; } =
        [
            SlateCommandRegistrar.NoVaultReason,
            SlateCommandRegistrar.UnavailableReason,
            SlateCommandRegistrar.StructuralMutationBusyReason,
        ];

        public bool IsAvailabilityRejection(string message) =>
            AvailabilityRejections.Contains(message);

        public int ListCommandsCalls { get; private set; }

        public int LoadRecentsCalls { get; private set; }

        /// <summary>
        /// Off by default: rendering asks every row, which would drown
        /// the ordering log. The invocation-ordering facts turn it on
        /// immediately before activating.
        /// </summary>
        public bool LogAvailabilityChecks { get; set; }

        public Command[] ListCommands()
        {
            ListCommandsCalls++;
            return [.. Commands];
        }

        public string[] LoadRecents()
        {
            LoadRecentsCalls++;
            return [.. Recents];
        }

        public string? DisabledReason(string commandId)
        {
            if (LogAvailabilityChecks)
            {
                log.Add("availability:" + commandId);
            }

            return DisabledReasons.TryGetValue(commandId, out string? reason) ? reason : null;
        }

        public void Invoke(string commandId)
        {
            log.Add("invoke:" + commandId);
            Invoked.Add(commandId);
            if (InvokeFailures.TryGetValue(commandId, out Exception? failure))
            {
                throw failure;
            }
        }

        public void RecordInvocation(string commandId)
        {
            log.Add("record:" + commandId);
            Recorded.Add(commandId);
        }
    }
}
