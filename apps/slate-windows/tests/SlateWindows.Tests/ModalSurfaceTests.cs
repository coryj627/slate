// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text.RegularExpressions;
using System.Windows.Input;
using SlateWindows.Commands;
using System.Xml.Linq;

namespace SlateWindows.Tests;

/// <summary>
/// W5-1 (#741) design-pass facts: the text-editing allow-list a modal
/// surface must honour.
/// </summary>
/// <remarks>
/// These exist because the allow-list shipped broken through TWO review
/// rounds — first swallowing every modified key, then still swallowing
/// AltGr — and neither defect could be caught by a unit test while the
/// table was a <c>private static</c> on <c>MainWindow</c>. Extracting it
/// is what makes the table falsifiable.
/// </remarks>
public sealed class ModalSurfaceTests
{
    [Theory]
    // Clipboard and undo must survive the modal swallow.
    [InlineData(Key.A, ModifierKeys.Control, true)]
    [InlineData(Key.C, ModifierKeys.Control, true)]
    [InlineData(Key.V, ModifierKeys.Control, true)]
    [InlineData(Key.X, ModifierKeys.Control, true)]
    [InlineData(Key.Z, ModifierKeys.Control, true)]
    // Caret and word-wise movement.
    [InlineData(Key.Left, ModifierKeys.Control, true)]
    [InlineData(Key.Back, ModifierKeys.Control, true)]
    // Shift alone: capitals and selection.
    [InlineData(Key.T, ModifierKeys.Shift, true)]
    [InlineData(Key.Home, ModifierKeys.Shift, true)]
    // Word-wise selection.
    [InlineData(Key.Right, ModifierKeys.Control | ModifierKeys.Shift, true)]
    [InlineData(Key.Z, ModifierKeys.Control | ModifierKeys.Shift, true)]
    // Shell chords must NOT be treated as text editing — the overlay still
    // swallows them so they cannot fire underneath it.
    [InlineData(Key.W, ModifierKeys.Control, false)]
    [InlineData(Key.T, ModifierKeys.Control, false)]
    [InlineData(Key.S, ModifierKeys.Control, false)]
    [InlineData(Key.J, ModifierKeys.Control, false)]
    [InlineData(Key.E, ModifierKeys.Control | ModifierKeys.Shift, false)]
    // Unmodified keys are not this table's business; the caller handles them.
    [InlineData(Key.A, ModifierKeys.None, false)]
    public void TheAllowListKeepsTextEditingAndStillSwallowsShellChords(
        Key key, ModifierKeys modifiers, bool expected) =>
        Assert.Equal(expected, TextEditingChords.Allows(key, modifiers, rightAltDown: false));

    /// <summary>
    /// AltGr reaches WPF as <c>Control|Alt</c>. Without the right-Alt arm
    /// it falls through and is swallowed, silently dropping nine ordinary
    /// Polish letters and the German at-sign and euro sign.
    /// </summary>
    /// <remarks>
    /// Both sides matter and both are pinned here: with right Alt DOWN the
    /// key is AltGr and must reach the text field; with it UP the same
    /// modifier pair is a real Ctrl+Alt chord the overlay must still
    /// swallow, or shell chords would fire underneath it.
    /// </remarks>
    [Theory]
    // Lowercase: AltGr + letter.
    [InlineData(Key.Q, false)]
    [InlineData(Key.E, false)]
    [InlineData(Key.A, false)]
    // UPPERCASE: AltGr + Shift + letter. An exact-equality test on
    // Control|Alt swallowed every one of these while passing the
    // lowercase cases — which is exactly how the first version of this
    // fix shipped.
    [InlineData(Key.Q, true)]
    [InlineData(Key.E, true)]
    [InlineData(Key.A, true)]
    public void AltGrReachesTheTextFieldButCtrlAltStillDoesNot(Key key, bool shifted)
    {
        ModifierKeys modifiers = ModifierKeys.Control | ModifierKeys.Alt
            | (shifted ? ModifierKeys.Shift : ModifierKeys.None);

        Assert.True(
            TextEditingChords.Allows(key, modifiers, rightAltDown: true),
            $"AltGr{(shifted ? "+Shift" : string.Empty)} was swallowed — "
            + "ordinary letters are undroppable on layouts that use it.");

        Assert.False(
            TextEditingChords.Allows(key, modifiers, rightAltDown: false),
            "a real Ctrl+Alt chord was treated as text editing and would "
            + "fire the shell command underneath the overlay.");
    }

    /// <summary>
    /// The palette refuses to open beneath ANY sheet, supersedes Quick
    /// Open, and re-opens over itself.
    /// </summary>
    /// <remarks>
    /// The round-3 blocker was the palette opening underneath seven
    /// sheets, and the design pass fixed it with a guard NO test drove —
    /// removing any single sheet predicate left the suite green. Codex
    /// flagged that as the same fix-without-a-gate class the review rounds
    /// kept finding, which is why the decision is now a pure function over
    /// the enum and this drives every member of it.
    /// </remarks>
    [Theory]
    [InlineData(null, PaletteOpenDecision.Open)]
    [InlineData(ModalSurface.CommandPalette, PaletteOpenDecision.Open)]
    [InlineData(ModalSurface.QuickOpen, PaletteOpenDecision.DismissQuickOpenThenOpen)]
    [InlineData(ModalSurface.AddProperty, PaletteOpenDecision.Refuse)]
    [InlineData(ModalSurface.BulkRename, PaletteOpenDecision.Refuse)]
    [InlineData(ModalSurface.CitationDetails, PaletteOpenDecision.Refuse)]
    [InlineData(ModalSurface.CitationSummary, PaletteOpenDecision.Refuse)]
    [InlineData(ModalSurface.FilesCiting, PaletteOpenDecision.Refuse)]
    [InlineData(ModalSurface.DashboardEditor, PaletteOpenDecision.Refuse)]
    [InlineData(ModalSurface.BaseQueryBuilder, PaletteOpenDecision.Refuse)]
    public void ThePaletteDefersToEverySheetAndSupersedesOnlyQuickOpen(
        object? topmost, object expected)
    {
        // object parameters because xUnit needs a public test method and
        // both enums are internal to the app assembly.
        ModalSurface? surface = topmost is null ? null : (ModalSurface)topmost;
        Assert.Equal(
            (PaletteOpenDecision)expected,
            ModalSurfaces.DecidePaletteOpen(surface));
    }

    /// <summary>
    /// Every <see cref="ModalSurface"/> member is covered by the decision
    /// table above.
    /// </summary>
    /// <remarks>
    /// Without this, adding a surface and forgetting a row would leave the
    /// new sheet silently in the "palette opens underneath it" state that
    /// round 3 found — the decision falls through to the wildcard arm and
    /// looks correct, but nothing asserts it.
    /// </remarks>
    [Fact]
    public void EveryModalSurfaceHasADecision()
    {
        ModalSurface[] covered =
        [
            ModalSurface.QuickOpen,
            ModalSurface.CommandPalette,
            ModalSurface.AddProperty,
            ModalSurface.BulkRename,
            ModalSurface.CitationDetails,
            ModalSurface.CitationSummary,
            ModalSurface.FilesCiting,
            ModalSurface.DashboardEditor,
            ModalSurface.BaseQueryBuilder,
        ];

        Assert.Equal(
            Enum.GetValues<ModalSurface>().OrderBy(surface => surface),
            covered.OrderBy(surface => surface));
    }

    /// <summary>
    /// Each surface reads its OWN flag out of the state record.
    /// </summary>
    /// <remarks>
    /// Codex's round-2 high: the precedence tests proved how a supplied
    /// surface ranks, but nothing exercised the mapping that turns live
    /// state into that surface. Pointing any arm at the wrong flag left
    /// every test green and let the palette open invisibly beneath that
    /// sheet again. Setting exactly one flag and asserting exactly one
    /// surface reads true catches a crossed arm.
    /// </remarks>
    [Fact]
    public void EachSurfaceReadsItsOwnFlag()
    {
        foreach (ModalSurface surface in Enum.GetValues<ModalSurface>())
        {
            ModalSurfaceState state = StateWithOnly(surface);

            foreach (ModalSurface candidate in Enum.GetValues<ModalSurface>())
            {
                bool expected = candidate == surface;
                Assert.True(
                    ModalSurfaces.IsOpen(candidate, state) == expected,
                    $"with only {surface} open, IsOpen({candidate}) should be "
                    + $"{expected} — an arm is reading the wrong flag.");
            }

            // And the whole pipeline agrees.
            Assert.Equal(surface, ModalSurfaces.TopmostOpen(state));
        }
    }

    /// <summary>
    /// Each field of the live state record reads the view-model property
    /// that bears its own name.
    /// </summary>
    /// <remarks>
    /// Codex's round-3 high, and the third time this exact class has bitten
    /// in this subsystem. <see cref="EachSurfaceReadsItsOwnFlag"/> builds
    /// the record itself, so it gates the pure enum mapping and nothing
    /// else — <c>MainWindow.CurrentModalSurfaceState</c>, which fills that
    /// record from live view models, had no test at all. Crossing one
    /// assignment (<c>DashboardEditor:</c> reading
    /// <c>BaseQueryBuilderSheet</c>) left the whole suite green while the
    /// palette opened beneath an open dashboard editor. The comment there
    /// claimed a wrong-property error would be "visible" in a flat list of
    /// named assignments; readability is not a gate, so here is the gate.
    ///
    /// It is a source pairing rather than a runtime one because the
    /// property is on <c>MainWindow</c> and every sheet would need a real
    /// shell to open. The named-argument syntax is what makes the pairing
    /// mechanical: the argument name and the property it reads must agree.
    /// </remarks>
    [Fact]
    public void EveryLiveStateFieldReadsThePropertyNamedAfterIt()
    {
        string source = SourceText.WithoutComments(
            File.ReadAllText(Path.Combine(
                SourceRoot(), "MainWindow.Palette.cs")));

        Match constructor = Regex.Match(
            source,
            @"new ModalSurfaceState\((?<arguments>[^;]*?)\);",
            RegexOptions.Singleline);
        Assert.True(
            constructor.Success,
            "CurrentModalSurfaceState no longer builds the record with a "
            + "single new ModalSurfaceState(...) call, so this pairing cannot "
            + "be read. Update the scrape rather than dropping the check.");

        var seen = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match argument in Regex.Matches(
            constructor.Groups["arguments"].Value,
            @"(?<name>[A-Za-z]+):\s*(?<expression>[^,)]+)"))
        {
            seen[argument.Groups["name"].Value] = argument.Groups["expression"].Value.Trim();
        }

        foreach (ModalSurface surface in Enum.GetValues<ModalSurface>())
        {
            Assert.True(
                seen.TryGetValue(surface.ToString(), out string? expression),
                $"{surface} is never assigned in CurrentModalSurfaceState, so the "
                + "palette cannot know whether it is open.");

            if (OverlayStateExpressions.TryGetValue(surface, out string? expected))
            {
                // The two overlays read an IsOpen flag rather than a
                // {Surface}Sheet property, so their exact expression is
                // pinned instead of matched by name.
                Assert.True(
                    expression == expected,
                    $"{surface} reads '{expression}', not '{expected}'.");
                continue;
            }

            Assert.True(
                expression!.Contains(surface.ToString(), StringComparison.Ordinal),
                $"{surface} reads '{expression}', which does not name {surface}. "
                + "A crossed assignment here reports the wrong surface open and "
                + "lets the palette render beneath a live sheet.");
        }
    }

    /// <summary>
    /// The two surfaces whose live flag is not a <c>{Surface}Sheet</c>
    /// property, with the expression each must read.
    /// </summary>
    private static readonly Dictionary<ModalSurface, string> OverlayStateExpressions =
        new()
        {
            [ModalSurface.QuickOpen] = "_viewModel.QuickSwitcher?.IsOpen == true",
            [ModalSurface.CommandPalette] = "_viewModel.Palette.IsOpen",
        };

    private static ModalSurfaceState StateWithOnly(ModalSurface surface) =>
        new(
            QuickOpen: surface == ModalSurface.QuickOpen,
            CommandPalette: surface == ModalSurface.CommandPalette,
            AddProperty: surface == ModalSurface.AddProperty,
            BulkRename: surface == ModalSurface.BulkRename,
            CitationDetails: surface == ModalSurface.CitationDetails,
            CitationSummary: surface == ModalSurface.CitationSummary,
            FilesCiting: surface == ModalSurface.FilesCiting,
            DashboardEditor: surface == ModalSurface.DashboardEditor,
            BaseQueryBuilder: surface == ModalSurface.BaseQueryBuilder);

    /// <summary>
    /// The topmost-open walk returns the LAST open surface in paint order,
    /// which is the one that owns the screen and the keyboard.
    /// </summary>
    [Fact]
    public void TopmostOpenReturnsTheLastOpenSurfaceInPaintOrder()
    {
        Assert.Null(ModalSurfaces.TopmostOpen(_ => false));

        // A sheet always wins over the palette and Quick Open beneath it.
        Assert.Equal(
            ModalSurface.AddProperty,
            ModalSurfaces.TopmostOpen(surface => surface
                is ModalSurface.QuickOpen
                or ModalSurface.CommandPalette
                or ModalSurface.AddProperty));

        // The palette wins over Quick Open, which is declared before it.
        Assert.Equal(
            ModalSurface.CommandPalette,
            ModalSurfaces.TopmostOpen(surface => surface
                is ModalSurface.QuickOpen or ModalSurface.CommandPalette));

        // Every surface, alone, is its own topmost.
        foreach (ModalSurface surface in Enum.GetValues<ModalSurface>())
        {
            Assert.Equal(
                surface,
                ModalSurfaces.TopmostOpen(candidate => candidate == surface));
        }
    }

    /// <summary>
    /// Every <c>Ctrl+Alt</c> chord the shell delivers must be answered by
    /// the shell-chord deny-list, because the AltGr arm un-swallows that
    /// whole modifier pair whenever right Alt is down.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the safety net under the AltGr fix. That arm returns true
    /// for ANY <c>Ctrl+Alt</c> chord while right Alt is physically down,
    /// so a shell chord on that pair would reach the surface underneath an
    /// open overlay unless the deny-list catches it first. It does today —
    /// but the deny-list was written for W1 and has already failed to
    /// track Ctrl+J, Ctrl+Shift+E and Ctrl+R, so the next Ctrl+Alt binding
    /// added without an entry is the live hazard.
    /// </para>
    /// <para>
    /// Driven from the CHORD TABLE, not from the XAML. The first version
    /// of this fact scraped <c>KeyBinding</c> elements and did not bite:
    /// Ctrl+Alt+F is delivered by a code-behind handler, so removing it
    /// from the deny-list left the fact green. The table records
    /// imperative chords too — that is the whole point of it — so it is
    /// the only source that sees the full surface.
    /// </para>
    /// </remarks>
    [Fact]
    public void EveryCtrlAltShellChordIsCoveredByTheDenyList()
    {
        // GLOBAL scope only. A focus-scoped chord — the grid's Ctrl+Alt+S,
        // the reading navigator's family — rides a RoutedCommand or a
        // surface handler that requires focus inside that surface, and the
        // palette holds focus in its own search box while open. Those
        // cannot fire underneath it, so demanding deny-list coverage for
        // them would be over-strict.
        ChordTableEntry[] ctrlAlt = ChordTable.Entries
            .Where(row => row.Scope == ChordScope.Global
                && row.WindowsChord is { } chord
                && chord.Contains("Ctrl+", StringComparison.Ordinal)
                && chord.Contains("Alt+", StringComparison.Ordinal))
            .ToArray();

        // Guard against a filter that silently matches nothing — the
        // failure mode this project has hit more than once, including in
        // the first version of this very fact.
        Assert.NotEmpty(ctrlAlt);

        foreach (ChordTableEntry row in ctrlAlt)
        {
            (Key key, ModifierKeys modifiers) = ParseChord(row.WindowsChord!);
            Assert.True(
                MainWindow.IsUnderlyingShellShortcutForTests(key, modifiers),
                $"{row.Id} delivers '{row.WindowsChord}' but the shell-chord "
                + "deny-list does not answer it. While an overlay is open and "
                + "right Alt is down, the AltGr arm would let it fire underneath.");
        }
    }

    /// <summary>
    /// Parses a chord-table string such as "Ctrl+Alt+Shift+Left".
    /// </summary>
    private static (Key Key, ModifierKeys Modifiers) ParseChord(string chord)
    {
        string[] tokens = chord.Split('+', StringSplitOptions.RemoveEmptyEntries);
        ModifierKeys modifiers = ModifierKeys.None;
        foreach (string token in tokens[..^1])
        {
            modifiers |= token switch
            {
                "Ctrl" => ModifierKeys.Control,
                "Alt" => ModifierKeys.Alt,
                "Shift" => ModifierKeys.Shift,
                _ => throw new InvalidOperationException($"unknown modifier '{token}'"),
            };
        }

        string keyToken = tokens[^1];
        Key key = keyToken switch
        {
            "=" => Key.OemPlus,
            "-" => Key.OemMinus,
            "\\" => Key.Oem5,
            "[" => Key.OemOpenBrackets,
            "]" => Key.OemCloseBrackets,
            _ when keyToken.Length == 1 && char.IsDigit(keyToken[0]) =>
                Key.D0 + (keyToken[0] - '0'),
            _ => (Key)Enum.Parse(typeof(Key), keyToken, ignoreCase: true),
        };

        return (key, modifiers);
    }

    /// <summary>
    /// The paint order the enum encodes is the order
    /// <c>MainWindow.xaml</c> declares the overlays in — the thing that
    /// actually decides which surface is on top, since the file sets no
    /// <c>Panel.ZIndex</c> anywhere.
    /// </summary>
    [Fact]
    public void ModalSurfaceOrderMatchesTheXamlDeclarationOrder()
    {
        string xaml = File.ReadAllText(
            Path.Combine(SourceRoot(), "MainWindow.xaml"));

        Assert.DoesNotContain(
            "Panel.ZIndex",
            xaml,
            StringComparison.Ordinal);

        (ModalSurface Surface, string AutomationId)[] expected =
        [
            (ModalSurface.QuickOpen, "QuickSwitcher"),
            (ModalSurface.CommandPalette, "CommandPalette"),
            (ModalSurface.AddProperty, "AddPropertySheet"),
            (ModalSurface.BulkRename, "BulkRenameSheet"),
            (ModalSurface.CitationDetails, "CitationDetailsSheet"),
            (ModalSurface.CitationSummary, "CitationSummarySheet"),
            (ModalSurface.FilesCiting, "FilesCitingSheet"),
            (ModalSurface.DashboardEditor, "DashboardEditorSheet"),
            (ModalSurface.BaseQueryBuilder, "BaseQueryBuilderSheet"),
        ];

        int previous = -1;
        foreach ((ModalSurface surface, string automationId) in expected)
        {
            int at = xaml.IndexOf(
                $"AutomationProperties.AutomationId=\"{automationId}\"",
                StringComparison.Ordinal);
            Assert.True(at >= 0, $"{automationId} is not declared in MainWindow.xaml");
            Assert.True(
                at > previous,
                $"{surface} is declared before the surface that should paint "
                + "beneath it — ModalSurface order no longer matches the XAML, "
                + "so the topmost-surface calculation is wrong.");
            previous = at;
        }
    }

    private static string SourceRoot() =>
        Path.Combine(RepoRoot(), "apps", "slate-windows", "src", "SlateWindows");

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Cargo.toml")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not locate repository root.");
    }
}
