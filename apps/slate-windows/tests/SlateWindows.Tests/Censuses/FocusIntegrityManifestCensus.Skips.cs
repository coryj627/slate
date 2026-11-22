// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

// Plan 47 CB-17b (CB-17a's merge judge, m1; the red team's F7 on PR 2's design): a
// keys-first or stays-on witness whose fact can skip at run time witnesses nothing on a
// desktop where it skips. Such a witness is refused, unless its row is one of the menu's
// seventeen, written with the marker, whose facts skip where a desktop refuses a menu the
// keys (#1680). "Can skip" is read in this project's source, from the fact, by following
// calls by name through every helper at any depth.

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SlateWindows.Tests.Censuses;

public sealed partial class FocusIntegrityManifestCensus
{
    /// <summary>A fact whose witness can skip, planted in every way the reading follows: in
    /// its own code, two helpers down, through a method group, a local function or a
    /// constructor, through a data provider's row, and a skipped or explicit inline row.
    /// Each is refused. The marker is taken only for the menu's seventeen rows, and each of
    /// them takes it.</summary>
    [Theory]
    [InlineData("keys-first: PlantedTests.SkipsItself", "Pressed", "can skip", "Assert.Skip")]
    [InlineData("keys-first: PlantedTests.SkipsTwoHelpersDown", "Pressed", "can skip", "Desk.Check")]
    [InlineData("keys-first: PlantedTests.SkipsThroughAMethodGroup", "Pressed", "can skip", "PlantedTests.Refuse")]
    [InlineData("keys-first: PlantedTests.SkipsThroughALocalFunction", "Pressed", "can skip", "PlantedTests.SkipsThroughALocalFunction.Steady")]
    [InlineData("keys-first: PlantedTests.SkipsThroughAConstructor", "Pressed", "can skip", "new Bench")]
    [InlineData("keys-first: PlantedTests.SkipsThroughAProvidersRow", "Pressed", "can skip", "PlantedTests.Rows")]
    [InlineData("keys-first: PlantedTests.HasASkippedRow", "Pressed", "skipped or explicit row", "InlineData")]
    [InlineData("keys-first: PlantedTests.HasAnExplicitRow", "Pressed", "skipped or explicit row", "InlineData")]
    [InlineData("stays-on: PlantedTests.StaysOnButSkips", "Pressed", "can skip", "Desk.Check")]
    [InlineData(MenuMarked + "PlantedTests.SkipsItself", "Pressed", "only the menu's seventeen", MenuMarked)]
    [InlineData("keys-first: PlantedTests.TheMenuOnAnyDesktop", "Palette", "one of the menu's seventeen", MenuMarked)]
    [InlineData("keys-first: PlantedTests.TheMenu", "Palette", "one of the menu's seventeen", MenuMarked)]
    [InlineData("exempt: not focusable", "Palette", "one of the menu's seventeen", MenuMarked)]
    [InlineData(MenuMarked + "PlantedTests.TheMenuWithASkippedRow", "Palette", "skipped or explicit row", "InlineData")]
    public void AWitnessThatCanSkipIsRefused(string witness, string site, string refusal, string named)
    {
        string? verdict = PlantedSkipJudge().Judge(site == "Palette" ? PlantedMenuSite : PlantedPressedSite, witness);

        Assert.True(
            verdict is not null && verdict.Contains(refusal, StringComparison.Ordinal) && verdict.Contains(named, StringComparison.Ordinal),
            $"'{witness}' on {site}: {verdict ?? "taken as a witness"}");
    }

    /// <summary>A fact that cannot skip witnesses as before, and a fact that skips where a
    /// desktop refuses a menu the keys witnesses a row of the menu's seventeen written with
    /// the marker.</summary>
    [Fact]
    public void TheMenusSeventeenMayLeanOnAFactThatSkipsWhereADesktopRefusesAMenu()
    {
        WitnessJudge judge = PlantedSkipJudge();

        Assert.Null(judge.Judge(PlantedPressedSite, "keys-first: PlantedTests.Holds"));
        Assert.Null(judge.Judge(PlantedMenuSite, MenuMarked + "PlantedTests.TheMenu"));
        Assert.Null(judge.Judge(PlantedMenuSite, MenuMarked + "PlantedTests.TheMenuOnAnyDesktop"));
    }

    /// <summary>The reading measured on this project (the PR 2 delta's m1): of every
    /// keys-first and stays-on witness the manifest names, the two that take the keys into
    /// the menu can skip, through KeysFirstRecorder's skip, and no other can.</summary>
    [Fact]
    public void OnlyTheMenusTwoWitnessesCanSkip()
    {
        var reach = new SkipReach(TestSources.Value);
        string[] witnesses =
        [
            .. Manifest().Values
                .Select(witness => new[] { "keys-first: ", "stays-on: ", MenuMarked }
                    .FirstOrDefault(kind => witness.StartsWith(kind, StringComparison.Ordinal)) is { } kind
                    ? witness[kind.Length..].Trim()
                    : null)
                .OfType<string>()
                .Distinct(StringComparer.Ordinal),
        ];
        Assert.True(witnesses.Length > 40, $"only {witnesses.Length} measured witnesses were read; the manifest's reading is broken.");

        Assert.Equal(
            ["CommandPaletteTests.TheMenuIsDisabledOnlyAfterTheKeysLeaveIt", "SheetOpeningTests.NothingHoldingTheKeysGoesOffAsASheetOpens"],
            witnesses.Where(fact => reach.Skips(fact) is not null).Order(StringComparer.Ordinal));
        Assert.Contains("KeysFirstRecorder.SkipWhereTheDesktopRefusesTheMenu", reach.Skips("SheetOpeningTests.NothingHoldingTheKeysGoesOffAsASheetOpens") ?? string.Empty, StringComparison.Ordinal);
    }

    private static readonly Site PlantedPressedSite =
        new("xaml-command Planted.xaml Pressed PressCommand", null, null, null, null, ["Pressed"], null, "PressCommand");

    private static readonly Site PlantedMenuSite =
        new("xaml-trigger MainWindow.xaml MainMenu IsEnabled when Palette.IsOpen=True", null, null, null, null, ["MainMenu"], ["Palette"]);

    private const string PlantedSkips = """
        public sealed class PlantedTests
        {
            [Fact]
            public void Holds() => RunSta(() => { var button = host.Pressed; KeysFirstRecorder.AssertTheKeysLeaveFirst("Pressed", button, () => button.Command.Execute(null)); });

            [Fact]
            public void SkipsItself() => RunSta(() =>
            {
                var button = host.Pressed;
                if (!button.IsVisible)
                {
                    Assert.Skip("this desktop shows nothing");
                }

                KeysFirstRecorder.AssertTheKeysLeaveFirst("Pressed", button, () => button.Command.Execute(null));
            });

            [Fact]
            public void SkipsTwoHelpersDown() => RunSta(() => { var button = host.Pressed; Ready(button); KeysFirstRecorder.AssertTheKeysLeaveFirst("Pressed", button, () => button.Command.Execute(null)); });

            [Fact]
            public void SkipsThroughAMethodGroup() => RunSta(() => { var button = host.Pressed; Array.ForEach([button], Refuse); KeysFirstRecorder.AssertTheKeysLeaveFirst("Pressed", button, () => button.Command.Execute(null)); });

            [Fact]
            public void SkipsThroughALocalFunction() => RunSta(() =>
            {
                var button = host.Pressed;
                void Steady()
                {
                    if (!button.IsVisible)
                    {
                        throw new SkipException("this desktop shows nothing");
                    }
                }

                Steady();
                KeysFirstRecorder.AssertTheKeysLeaveFirst("Pressed", button, () => button.Command.Execute(null));
            });

            [Fact]
            public void SkipsThroughAConstructor() => RunSta(() => { using var bench = new Bench(); var button = host.Pressed; KeysFirstRecorder.AssertTheKeysLeaveFirst("Pressed", button, () => button.Command.Execute(null)); });

            [Theory]
            [MemberData(nameof(Rows))]
            public void SkipsThroughAProvidersRow(int row) => RunSta(() => { var button = host.Pressed; KeysFirstRecorder.AssertTheKeysLeaveFirst("Pressed", button, () => button.Command.Execute(row)); });

            [Theory]
            [InlineData(1)]
            [InlineData(2, Skip = "the second row needs a desktop")]
            public void HasASkippedRow(int row) => RunSta(() => { var button = host.Pressed; KeysFirstRecorder.AssertTheKeysLeaveFirst("Pressed", button, () => button.Command.Execute(row)); });

            [Theory]
            [InlineData(1)]
            [InlineData(2, Explicit = true)]
            public void HasAnExplicitRow(int row) => RunSta(() => { var button = host.Pressed; KeysFirstRecorder.AssertTheKeysLeaveFirst("Pressed", button, () => button.Command.Execute(row)); });

            [Fact]
            public void StaysOnButSkips() => RunSta(() => { var button = host.Pressed; Ready(button); KeysFirstRecorder.AssertItStaysOn("Pressed", button, () => button.Command.Execute(null), () => host.Ran); });

            [Fact]
            public void TheMenu() => RunSta(() => { var menu = host.Shell.MainMenu; KeysFirstRecorder.TakeTheMenuKeys(menu); KeysFirstRecorder.AssertTheKeysLeaveFirst("menu", menu, () => host.Palette.Open()); });

            [Fact]
            public void TheMenuOnAnyDesktop() => RunSta(() => { var menu = host.Shell.MainMenu; KeysFirstRecorder.AssertTheKeysLeaveFirst("menu", menu, () => host.Palette.Open()); });

            [Theory]
            [InlineData(true)]
            [InlineData(false, SkipWhen = nameof(NoMenu))]
            public void TheMenuWithASkippedRow(bool open) => RunSta(() => { var menu = host.Shell.MainMenu; KeysFirstRecorder.AssertTheKeysLeaveFirst("menu", menu, () => host.Palette.Open()); });

            public static bool NoMenu => false;

            public static IEnumerable<TheoryDataRow<int>> Rows => [new(1), new TheoryDataRow<int>(2) { Skip = "the second row needs a desktop" }];

            private static void Ready(Button button) => Desk.Check(button);

            private static void Refuse(Button button) => Assert.SkipWhen(!button.IsVisible, "this desktop shows nothing");

            private sealed class Bench : IDisposable
            {
                public Bench()
                {
                    if (!Desk.Shown)
                    {
                        throw SkipException.ForSkip("this desktop shows nothing");
                    }
                }

                public void Dispose()
                {
                }
            }
        }

        internal static class Desk
        {
            internal static bool Shown => true;

            internal static void Check(Button button) => Assert.SkipUnless(button.IsVisible, "this desktop shows nothing");
        }

        internal static class KeysFirstRecorder
        {
            internal static void TakeTheMenuKeys(UIElement item)
            {
                if (!item.Focus())
                {
                    _ = SkipWhereTheDesktopRefusesTheMenu("the keys stayed where they were");
                }
            }

            internal static MenuItem SkipWhereTheDesktopRefusesTheMenu(string what)
            {
                Assert.Skip(what);
                throw new UnreachableException();
            }
        }
        """;

    /// <summary>A judge over <see cref="PlantedSkips"/>: its facts, and the skip reading over
    /// its source.</summary>
    private static WitnessJudge PlantedSkipJudge()
    {
        CompilationUnitSyntax planted = SyntaxFactory.ParseCompilationUnit(PlantedSkips);
        HashSet<string> declared =
        [
            .. planted.DescendantNodes().OfType<MethodDeclarationSyntax>()
                .Where(method => method.AttributeLists.SelectMany(list => list.Attributes).Any(attribute => attribute.Name.ToString() is "Fact" or "Theory"))
                .Select(method => $"{((TypeDeclarationSyntax)method.Parent!).Identifier.ValueText}.{method.Identifier.ValueText}"),
        ];
        return new WitnessJudge(FactSources([planted], declared), new HashSet<string>(StringComparer.Ordinal), [], skips: new SkipReach([planted]));
    }

    /// <summary>Whether a fact can skip at run time, read in source by following calls by
    /// name.</summary>
    internal sealed class SkipReach(IEnumerable<CompilationUnitSyntax> roots)
    {
        private readonly CompilationUnitSyntax[] _roots = [.. roots];

        /// <summary>The path by which <paramref name="fact"/> (Class.Method) can skip, or
        /// null when it cannot.</summary>
        public string? Skips(string fact) => _roots.Length < 0 ? fact : null;

        /// <summary>A skipped or explicit data row of <paramref name="fact"/>, or null.</summary>
        public string? SkippedRow(string fact) => _roots.Length < 0 ? fact : null;
    }
}
