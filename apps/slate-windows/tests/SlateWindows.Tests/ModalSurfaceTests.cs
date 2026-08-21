// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
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
    // SD-5 (replacing the original S11 stacking arm after the round-10
    // root cause analysis): the palette SUPERSEDES an open search
    // overlay exactly as it supersedes Quick Open — no persistent
    // stacking, per 28_palette_contracts.md's own rule.
    [InlineData(ModalSurface.SearchOverlay, PaletteOpenDecision.DismissSearchThenOpen)]
    [InlineData(ModalSurface.QuickOpen, PaletteOpenDecision.DismissQuickOpenThenOpen)]
    [InlineData(ModalSurface.AddProperty, PaletteOpenDecision.Refuse)]
    [InlineData(ModalSurface.BulkRename, PaletteOpenDecision.Refuse)]
    [InlineData(ModalSurface.CitationDetails, PaletteOpenDecision.Refuse)]
    [InlineData(ModalSurface.CitationSummary, PaletteOpenDecision.Refuse)]
    [InlineData(ModalSurface.FilesCiting, PaletteOpenDecision.Refuse)]
    [InlineData(ModalSurface.DashboardEditor, PaletteOpenDecision.Refuse)]
    [InlineData(ModalSurface.BaseQueryBuilder, PaletteOpenDecision.Refuse)]
    [InlineData(ModalSurface.TemplatePicker, PaletteOpenDecision.Refuse)]
    [InlineData(ModalSurface.TemplateFlow, PaletteOpenDecision.Refuse)]
    public void ThePaletteDefersToEverySheetAndSupersedesBothPickers(
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
    /// The search chord's precedence (W5-2, contract S11), every member
    /// covered: it opens over nothing, toggles over itself (the view
    /// model's Toggle closes an already-open overlay), supersedes Quick
    /// Open, and refuses beneath the palette and every sheet — the
    /// palette supersedes search (SD-5), never the reverse.
    /// </summary>
    [Theory]
    [InlineData(null, PaletteOpenDecision.Open)]
    [InlineData(ModalSurface.SearchOverlay, PaletteOpenDecision.Open)]
    [InlineData(ModalSurface.QuickOpen, PaletteOpenDecision.DismissQuickOpenThenOpen)]
    [InlineData(ModalSurface.CommandPalette, PaletteOpenDecision.Refuse)]
    [InlineData(ModalSurface.AddProperty, PaletteOpenDecision.Refuse)]
    [InlineData(ModalSurface.BulkRename, PaletteOpenDecision.Refuse)]
    [InlineData(ModalSurface.CitationDetails, PaletteOpenDecision.Refuse)]
    [InlineData(ModalSurface.CitationSummary, PaletteOpenDecision.Refuse)]
    [InlineData(ModalSurface.FilesCiting, PaletteOpenDecision.Refuse)]
    [InlineData(ModalSurface.DashboardEditor, PaletteOpenDecision.Refuse)]
    [InlineData(ModalSurface.BaseQueryBuilder, PaletteOpenDecision.Refuse)]
    [InlineData(ModalSurface.TemplatePicker, PaletteOpenDecision.Refuse)]
    [InlineData(ModalSurface.TemplateFlow, PaletteOpenDecision.Refuse)]
    public void TheSearchChordDefersToEverySheetAndThePalette(
        object? topmost, object expected)
    {
        ModalSurface? surface = topmost is null ? null : (ModalSurface)topmost;
        Assert.Equal(
            (PaletteOpenDecision)expected,
            ModalSurfaces.DecideSearchOpen(surface));
    }

    /// <summary>
    /// The template flow (W5-3, T9) supersedes both pickers, retires
    /// the palette (mac's registry-dispatch rule — the same arm serves
    /// the palette-invoked command during P9's transient), refuses
    /// beneath every sheet, and refuses re-entry over its own
    /// surfaces: the flow holds user input worth more than a stale
    /// query, so PD-2's reopen-clears semantic deliberately does not
    /// apply.
    /// </summary>
    [Theory]
    [InlineData(null, PaletteOpenDecision.Open)]
    [InlineData(ModalSurface.QuickOpen, PaletteOpenDecision.DismissQuickOpenThenOpen)]
    [InlineData(ModalSurface.SearchOverlay, PaletteOpenDecision.DismissSearchThenOpen)]
    [InlineData(ModalSurface.CommandPalette, PaletteOpenDecision.DismissPaletteThenOpen)]
    [InlineData(ModalSurface.AddProperty, PaletteOpenDecision.Refuse)]
    [InlineData(ModalSurface.BulkRename, PaletteOpenDecision.Refuse)]
    [InlineData(ModalSurface.CitationDetails, PaletteOpenDecision.Refuse)]
    [InlineData(ModalSurface.CitationSummary, PaletteOpenDecision.Refuse)]
    [InlineData(ModalSurface.FilesCiting, PaletteOpenDecision.Refuse)]
    [InlineData(ModalSurface.DashboardEditor, PaletteOpenDecision.Refuse)]
    [InlineData(ModalSurface.BaseQueryBuilder, PaletteOpenDecision.Refuse)]
    [InlineData(ModalSurface.TemplatePicker, PaletteOpenDecision.Refuse)]
    [InlineData(ModalSurface.TemplateFlow, PaletteOpenDecision.Refuse)]
    public void TheTemplateFlowSupersedesTheOverlaysAndDefersToEverySheet(
        object? topmost, object expected)
    {
        ModalSurface? surface = topmost is null ? null : (ModalSurface)topmost;
        Assert.Equal(
            (PaletteOpenDecision)expected,
            ModalSurfaces.DecideTemplateOpen(surface));
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
            ModalSurface.SearchOverlay,
            ModalSurface.CommandPalette,
            ModalSurface.AddProperty,
            ModalSurface.BulkRename,
            ModalSurface.CitationDetails,
            ModalSurface.CitationSummary,
            ModalSurface.FilesCiting,
            ModalSurface.DashboardEditor,
            ModalSurface.BaseQueryBuilder,
            ModalSurface.TemplatePicker,
            ModalSurface.TemplateFlow,
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
        CSharpSource shell = CSharpSource.Load("MainWindow.Palette.cs");

        // Singleness first. A second construction site anywhere in this
        // file would otherwise be read instead of the live one, silently
        // — the first-match-not-the-right-match bug that bit three
        // separate scrapes in #741.
        ObjectCreationExpressionSyntax[] constructions = shell.Root
            .DescendantNodes()
            .OfType<ObjectCreationExpressionSyntax>()
            .Where(creation => creation.Type is IdentifierNameSyntax type
                && type.Identifier.ValueText == nameof(ModalSurfaceState))
            .ToArray();
        Assert.True(
            constructions.Length == 1,
            "MainWindow.Palette.cs builds ModalSurfaceState in "
            + $"{constructions.Length} places. This pairing reads one of them, "
            + "so any other is unchecked — scope the query before adding a "
            + "second construction site.");

        ObjectCreationExpressionSyntax construction = constructions[0];
        SyntaxNode scope =
            (SyntaxNode?)construction.FirstAncestorOrSelf<MemberDeclarationSyntax>()
            ?? shell.Root;

        var seen = new Dictionary<string, ExpressionSyntax>(StringComparer.Ordinal);
        foreach (ArgumentSyntax argument in construction.ArgumentList!.Arguments)
        {
            Assert.True(
                argument.NameColon is not null,
                $"ModalSurfaceState is built with the positional argument "
                + $"'{argument}'. The pairing below reads argument NAMES, and a "
                + "positional list silently reorders without any of these "
                + "assertions noticing.");
            seen[argument.NameColon!.Name.Identifier.ValueText] = argument.Expression;
        }

        Assert.Equal(Enum.GetValues<ModalSurface>().Length, seen.Count);

        foreach (ModalSurface surface in Enum.GetValues<ModalSurface>())
        {
            Assert.True(
                seen.TryGetValue(surface.ToString(), out ExpressionSyntax? argument),
                $"{surface} is never assigned in CurrentModalSurfaceState, so the "
                + "palette cannot know whether it is open.");

            // Resolved through any local alias, so extracting the read into
            // a well-named bool cannot hide a wrong property behind it.
            ExpressionSyntax effective = CSharpSource.Resolve(argument!, scope);

            if (OverlayStateExpressions.TryGetValue(surface, out string? expected))
            {
                // The two overlays read an IsOpen flag rather than a
                // {Surface}Sheet property, so their exact expression is
                // pinned instead of matched by name.
                Assert.Equal(expected, CSharpSource.Normalize(effective));
                continue;
            }

            // An EXACT member name, never a substring. The workspace names
            // these inconsistently — AddPropertySheet and DashboardEditorSheet
            // carry the suffix, CitationDetails and FilesCiting do not — so
            // either spelling is accepted, but only as a whole name. A
            // substring test would let a hypothetical AddPropertyRow stand in
            // for AddPropertySheet, which is the shielding this replaces.
            string[] accepted = [surface.ToString(), surface + "Sheet"];
            Assert.True(
                CSharpSource.MemberNames(effective).Intersect(accepted).Any(),
                $"{surface} reads '{effective}', which never touches "
                + $"{string.Join(" or ", accepted)}. A crossed assignment here "
                + "reports the wrong surface open and lets the palette render "
                + "beneath a live sheet.");
        }
    }

    /// <summary>
    /// The two surfaces whose live flag is not a <c>{Surface}Sheet</c>
    /// property, with the expression each must read.
    /// </summary>
    private static readonly Dictionary<ModalSurface, string> OverlayStateExpressions =
        new()
        {
            [ModalSurface.QuickOpen] = "_viewModel.QuickSwitcher?.IsOpen==true",
            [ModalSurface.SearchOverlay] = "_viewModel.Search.IsOpen",
            [ModalSurface.CommandPalette] = "_viewModel.Palette.IsOpen",
        };

    private static ModalSurfaceState StateWithOnly(ModalSurface surface) =>
        new(
            QuickOpen: surface == ModalSurface.QuickOpen,
            SearchOverlay: surface == ModalSurface.SearchOverlay,
            CommandPalette: surface == ModalSurface.CommandPalette,
            AddProperty: surface == ModalSurface.AddProperty,
            BulkRename: surface == ModalSurface.BulkRename,
            CitationDetails: surface == ModalSurface.CitationDetails,
            CitationSummary: surface == ModalSurface.CitationSummary,
            FilesCiting: surface == ModalSurface.FilesCiting,
            DashboardEditor: surface == ModalSurface.DashboardEditor,
            BaseQueryBuilder: surface == ModalSurface.BaseQueryBuilder,
            TemplatePicker: surface == ModalSurface.TemplatePicker,
            TemplateFlow: surface == ModalSurface.TemplateFlow);

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

        // W5-2: the palette outranks an open search overlay. Since SD-5
        // the two never coexist outside a palette invoke's transient
        // window; the ranking is invariant 6's backstop, and the reason
        // search is declared before the palette.
        Assert.Equal(
            ModalSurface.CommandPalette,
            ModalSurfaces.TopmostOpen(surface => surface
                is ModalSurface.SearchOverlay or ModalSurface.CommandPalette));

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
            (ModalSurface.SearchOverlay, "SearchOverlay"),
            (ModalSurface.CommandPalette, "CommandPalette"),
            (ModalSurface.AddProperty, "AddPropertySheet"),
            (ModalSurface.BulkRename, "BulkRenameSheet"),
            (ModalSurface.CitationDetails, "CitationDetailsSheet"),
            (ModalSurface.CitationSummary, "CitationSummarySheet"),
            (ModalSurface.FilesCiting, "FilesCitingSheet"),
            (ModalSurface.DashboardEditor, "DashboardEditorSheet"),
            (ModalSurface.BaseQueryBuilder, "BaseQueryBuilderSheet"),
            (ModalSurface.TemplatePicker, "TemplatePickerSheet"),
            (ModalSurface.TemplateFlow, "TemplateFlowSheet"),
        ];

        // Exhaustiveness (red team W5-3, tests finding 3): the two
        // template surfaces shipped ABSENT from this list, so swapping
        // their XAML order — the exact key/paint divergence the enum
        // exists to prevent — left the census green. A surface added
        // to the enum without a row here now fails loudly.
        Assert.Equal(
            Enum.GetValues<ModalSurface>().OrderBy(surface => surface),
            expected.Select(entry => entry.Surface).OrderBy(surface => surface));

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

    /// <summary>
    /// The binding path the Menu's disable trigger must read for each
    /// surface. The two overlays and the palette expose <c>IsOpen</c>
    /// flags; the nine sheets are object properties (open == non-null)
    /// and must route through <c>IsNotNullConverter</c>.
    /// </summary>
    private static readonly Dictionary<ModalSurface, string> MenuDisableBindings =
        new()
        {
            [ModalSurface.QuickOpen] = "QuickSwitcher.IsOpen",
            [ModalSurface.SearchOverlay] = "Search.IsOpen",
            [ModalSurface.CommandPalette] = "Palette.IsOpen",
            [ModalSurface.AddProperty] = "Workspace.AddPropertySheet",
            [ModalSurface.BulkRename] = "Workspace.BulkRenameSheet",
            [ModalSurface.CitationDetails] = "Workspace.CitationDetails",
            [ModalSurface.CitationSummary] = "Workspace.CitationSummary",
            [ModalSurface.FilesCiting] = "Workspace.FilesCiting",
            [ModalSurface.DashboardEditor] = "Workspace.DashboardEditorSheet",
            [ModalSurface.BaseQueryBuilder] = "Workspace.BaseQueryBuilderSheet",
            [ModalSurface.TemplatePicker] = "Workspace.TemplatePickerSheet",
            [ModalSurface.TemplateFlow] = "Workspace.TemplateFlowSheet",
        };

    /// <summary>
    /// Every path in <see cref="MenuDisableBindings"/> resolves against
    /// the live view-model types by reflection.
    /// </summary>
    /// <remarks>
    /// Round-2 finding F3: the XAML gate verified the trigger list
    /// against the markup, but a renamed sheet property recompiles the
    /// C# state reader while the XAML path STRING silently stops firing
    /// — the round-1 menu bug reborn with the gate green. Pinning each
    /// segment against the real property closes that residual
    /// under-match.
    /// </remarks>
    [Fact]
    public void SearchOwnsKeysOnlyWhenItIsTheTopmostSurface()
    {
        // Codex round 1: under the original stacking design a
        // palette-invoked sheet sat OVER a still-open search overlay,
        // and routing on IsOpen alone let the hidden overlay steal the
        // sheet's Enter and Escape. SD-5 made those states unreachable;
        // the facts stay because this function is invariant 3's
        // backstop against a future stacking violation.
        Assert.True(ModalSurfaces.SearchOwnsKeys(
            StateWithOnly(ModalSurface.SearchOverlay)));

        foreach (ModalSurface above in new[]
        {
            ModalSurface.CommandPalette,
            ModalSurface.AddProperty,
            ModalSurface.BulkRename,
            ModalSurface.CitationSummary,
        })
        {
            ModalSurfaceState state = StateWith(ModalSurface.SearchOverlay, above);
            Assert.False(
                ModalSurfaces.SearchOwnsKeys(state),
                $"search owned the keyboard beneath an open {above}.");
        }

        // All closed: nothing owns the keyboard.
        Assert.False(ModalSurfaces.SearchOwnsKeys(default));
    }

    /// <summary>
    /// The shell's search key branch routes through
    /// <c>SearchOwnsKeys</c>, not a bare <c>IsOpen</c> read.
    /// </summary>
    [Fact]
    public void TheSearchKeyBranchRoutesOnOwnershipNotOpenness()
    {
        Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax route =
            CSharpSource.Load("MainWindow.xaml.cs").Method("Window_PreviewKeyDown");

        Assert.True(
            route.DescendantNodes()
                .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.IfStatementSyntax>()
                .Any(statement => CSharpSource.Normalize(statement.Condition)
                    == "ModalSurfaces.SearchOwnsKeys(CurrentModalSurfaceState)"),
            "Window_PreviewKeyDown no longer gates the search branch on "
            + "ModalSurfaces.SearchOwnsKeys(CurrentModalSurfaceState) — a "
            + "bare IsOpen read lets a hidden overlay steal a sheet's keys.");
    }

    /// <summary>
    /// The sheet restore path hands focus to the search box when the
    /// captured target cannot take it and search is topmost.
    /// </summary>
    /// <remarks>
    /// Codex round 2: a palette-invoked sheet captures the PALETTE box
    /// as its return target, the palette dismisses beneath the sheet,
    /// and the failed <c>Focus()</c> was ignored — the exposed search
    /// overlay owned the keys but not text focus.
    /// </remarks>
    /// <summary>
    /// Restore sites the topmost-search rule covers, and the ones
    /// exempt with a reason. Discovery below asserts every
    /// <c>Restore*Focus*</c> method in the shell is in exactly one
    /// list, so a fifth implementation cannot appear uncovered — the
    /// round-3 and round-4 findings were both exactly that.
    /// </summary>
    private static readonly (string File, string Method)[] CoveredRestoreSites =
    [
        ("MainWindow.Properties.cs", "RestoreFocusAfterSheet"),
        ("MainWindow.Citations.cs", "RestoreFocusTo"),
        ("MainWindow.Bases.cs", "RestoreBasesOverlayFocus"),
        ("MainWindow.Palette.cs", "RestoreFocusAfterPalette"),
        ("MainWindow.Templates.cs", "RestoreFocusAfterTemplates"),
        // Red team after codex round 11: this restore previously lived
        // anonymous inside the QuickSwitcher Dismissed handler, where
        // this census could not discover it — invariant 4 was true
        // only nominally, and a sheet landing over an open Quick Open
        // had focus stolen back out from behind its scrim.
        ("MainWindow.xaml.cs", "RestoreFocusAfterQuickOpen"),
    ];

    private static readonly Dictionary<(string File, string Method), string> ExemptRestoreSites =
        new()
        {
            [("MainWindow.Search.cs", "RestoreFocusAfterSearch")] =
                "runs when SEARCH ITSELF closes, so search cannot be the "
                + "topmost surface during it by construction.",
        };

    [Fact]
    public void TheSheetRestoreFallsBackToTheSearchBoxWhenSearchIsTopmost()
    {
        // Discovery: every Restore*Focus* method in the shell partials
        // must be covered or exempt — hardcoding the list is how the
        // Citation Summary (round 3) and palette (round 4) sites hid.
        string root = SourceText.ShellSourceRoot();
        foreach (string path in System.IO.Directory.GetFiles(root, "MainWindow*.cs"))
        {
            string file = System.IO.Path.GetFileName(path);
            foreach (Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax method
                in CSharpSource.Load(file).Root
                    .DescendantNodes()
                    .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>())
            {
                string name = method.Identifier.ValueText;
                if (!name.StartsWith("Restore", StringComparison.Ordinal)
                    || !name.Contains("Focus", StringComparison.Ordinal))
                {
                    continue;
                }

                bool covered = CoveredRestoreSites.Contains((file, name));
                bool exempt = ExemptRestoreSites.ContainsKey((file, name));
                Assert.True(
                    covered ^ exempt,
                    $"{file}.{name} is a focus-restore implementation in "
                    + "neither the covered list nor the exempt list — a "
                    + "surface closing above an open search overlay through "
                    + "it would strand text focus, invisibly.");
            }
        }

        foreach ((string file, string methodName) in CoveredRestoreSites)
        {
            Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax restore =
                CSharpSource.Load(file).Method(methodName);
            var invocations = restore.DescendantNodes()
                .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax>()
                .ToList();
            int guard = invocations.FindIndex(call =>
                CSharpSource.Normalize(call.Expression) == "TryFocusSearchIfTopmost");
            Assert.True(
                guard >= 0,
                $"{file}.{methodName} no longer routes through "
                + "TryFocusSearchIfTopmost — a surface closing above a "
                + "still-open search overlay leaves the exposed search box "
                + "without text focus.");

            // ORDERING, not mere presence (codex round 4): the guard must
            // precede every competing .Focus() attempt, or a successful
            // restore to an element BEHIND the overlay wins the race.
            for (var index = 0; index < guard; index++)
            {
                // The CALLEE only — normalizing the whole call would
                // match Dispatcher.InvokeAsync's lambda text, whose
                // body legitimately contains the guarded restores.
                Assert.False(
                    CSharpSource.Normalize(invocations[index].Expression)
                        .EndsWith(".Focus", StringComparison.Ordinal),
                    $"{file}.{methodName} attempts a focus restore BEFORE "
                    + "the topmost-search guard — search topmost must win "
                    + "even against a restore that would succeed.");
            }
        }

        // And the shared helper itself must consult ownership and focus
        // the search box.
        Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax helper =
            CSharpSource.Load("MainWindow.Search.cs").Method("TryFocusSearchIfTopmost");
        Assert.True(
            CSharpSource.Invokes(helper, "ModalSurfaces.SearchOwnsKeys"),
            "TryFocusSearchIfTopmost no longer consults SearchOwnsKeys.");
        Assert.True(
            CSharpSource.Invokes(helper, "SearchOverlaySearchTextBox.Focus"),
            "TryFocusSearchIfTopmost no longer focuses the search box.");
    }

    /// <summary>
    /// Quick Open's chord refuses beneath every sheet and the palette
    /// (codex round 5): the Ctrl+O branch was unconditional, and the
    /// picker exclusion then closed a search overlay under the sheet
    /// too, leaving only the hidden picker taking keys.
    /// </summary>
    [Theory]
    [InlineData(null, PaletteOpenDecision.Open)]
    [InlineData(ModalSurface.QuickOpen, PaletteOpenDecision.Open)]
    [InlineData(ModalSurface.SearchOverlay, PaletteOpenDecision.Open)]
    [InlineData(ModalSurface.CommandPalette, PaletteOpenDecision.Refuse)]
    [InlineData(ModalSurface.AddProperty, PaletteOpenDecision.Refuse)]
    [InlineData(ModalSurface.BulkRename, PaletteOpenDecision.Refuse)]
    [InlineData(ModalSurface.CitationDetails, PaletteOpenDecision.Refuse)]
    [InlineData(ModalSurface.CitationSummary, PaletteOpenDecision.Refuse)]
    [InlineData(ModalSurface.FilesCiting, PaletteOpenDecision.Refuse)]
    [InlineData(ModalSurface.DashboardEditor, PaletteOpenDecision.Refuse)]
    [InlineData(ModalSurface.BaseQueryBuilder, PaletteOpenDecision.Refuse)]
    [InlineData(ModalSurface.TemplatePicker, PaletteOpenDecision.Refuse)]
    [InlineData(ModalSurface.TemplateFlow, PaletteOpenDecision.Refuse)]
    public void QuickOpenRefusesBeneathEverySheetAndThePalette(
        object? topmost, object expected)
    {
        ModalSurface? surface = topmost is null ? null : (ModalSurface)topmost;
        Assert.Equal(
            (PaletteOpenDecision)expected,
            ModalSurfaces.DecideQuickOpenOpen(surface));
    }

    /// <summary>
    /// The Ctrl+O branch's <c>Open()</c> is CONTROL-DEPENDENT on the
    /// admission decision, not merely near it.
    /// </summary>
    /// <remarks>
    /// Codex round 6: the previous presence pin passed with
    /// <c>_ = DecideQuickOpenOpen(...); QuickSwitcher.Open();</c> — the
    /// decision computed and ignored, the recorded under-match class.
    /// The pin now locates the <c>if</c> whose CONDITION is the
    /// decision comparison and requires the <c>Open()</c> call inside
    /// its statement.
    /// </remarks>
    [Fact]
    public void TheQuickOpenChordBranchConsultsTheAdmissionDecision()
    {
        Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax route =
            CSharpSource.Load("MainWindow.xaml.cs").Method("Window_PreviewKeyDown");

        var admission = route.DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.IfStatementSyntax>()
            .Where(statement => CSharpSource.Normalize(statement.Condition)
                == "ModalSurfaces.DecideQuickOpenOpen(OpenModalSurface)==PaletteOpenDecision.Open")
            .ToList();
        Assert.True(
            admission.Count == 1,
            $"expected exactly one admission-gated if in Window_PreviewKeyDown, "
            + $"found {admission.Count} — Quick Open opens beneath any sheet "
            + "again if the gate is gone, and two gates would mean a second "
            + "unaudited open path.");
        Assert.True(
            admission[0].Statement.DescendantNodesAndSelf()
                .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax>()
                .Any(call => CSharpSource.Normalize(call.Expression)
                    .EndsWith("QuickSwitcher.Open", StringComparison.Ordinal)),
            "QuickSwitcher.Open() is no longer inside the admission-gated "
            + "if — the decision is computed but does not control the open.");

        // REACHABILITY (codex round 8): the branch must precede the
        // search-ownership branch, or the selective swallow marks Ctrl+O
        // handled and the gate is dead code while search is open.
        var searchBranch = route.DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.IfStatementSyntax>()
            .First(statement => CSharpSource.Normalize(statement.Condition)
                == "ModalSurfaces.SearchOwnsKeys(CurrentModalSurfaceState)");
        Assert.True(
            admission[0].SpanStart < searchBranch.SpanStart,
            "the Ctrl+O admission branch sits AFTER the search-ownership "
            + "branch — the swallow eats Ctrl+O first, and the picker "
            + "handoff is unreachable while search is open.");
    }

    /// <summary>
    /// The reverse picker handoff (codex round 5): search superseding
    /// Quick Open adopts the pre-SWITCHER focus, or Escape falls back to
    /// the editor instead of the element the user came from.
    /// </summary>
    [Fact]
    public void TheSearchSupersessionAdoptsThePreSwitcherFocus()
    {
        Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax clear =
            CSharpSource.Load("MainWindow.Search.cs").Method("TryClearTheWayForSearch");
        string body = CSharpSource.Normalize(clear);
        // Consume-then-adopt: the consume must run UNCONDITIONALLY (so
        // the switcher's own restore is inert either way), and the
        // adoption keeps an older search token when one exists.
        Assert.Contains(
            "preSwitcher=ConsumePreSwitcherFocus()", body, StringComparison.Ordinal);
        Assert.Contains(
            "_focusBeforeSearch??=preSwitcher", body, StringComparison.Ordinal);
    }

    /// <summary>
    /// The palette supersession (SD-5, replacing the original S11
    /// stacking arm after the round-10 root cause analysis): the
    /// palette-chord dismissal arm closes an open search overlay and
    /// adopts its captured pre-open focus, consume-first so search's
    /// own queued restore cannot race the handoff. This is what
    /// retired the round-9 IsEnabled triggers and the round-10 UIA
    /// Control View exposure with them: a closed overlay is collapsed,
    /// and a collapsed overlay is not in the UIA tree at all.
    /// </summary>
    [Fact]
    public void ThePaletteSupersessionClosesSearchAndAdoptsItsFocus()
    {
        Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax clear =
            CSharpSource.Load("MainWindow.Palette.cs")
                .Method("TryClearTheWayForThePalette");

        // The arm hangs off the SAME pure decision the every-member
        // theory drives, so flipping the decision table and gutting
        // the arm each fail a named gate. (Chord-path REACHABILITY —
        // that Ctrl+Shift+P actually calls this method — is pinned
        // separately by ThePaletteChordBranchConsultsTheAdmissionDecision;
        // the red team found this comment's first draft claiming that
        // coverage without owning it.)
        Microsoft.CodeAnalysis.CSharp.Syntax.SwitchStatementSyntax gate = clear
            .DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.SwitchStatementSyntax>()
            .Single();
        Assert.Equal(
            "ModalSurfaces.DecidePaletteOpen(OpenModalSurface)",
            CSharpSource.Normalize(gate.Expression));

        Microsoft.CodeAnalysis.CSharp.Syntax.SwitchSectionSyntax arm =
            Assert.Single(
                gate.Sections,
                section => section.Labels
                    .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.CaseSwitchLabelSyntax>()
                    .Any(label => CSharpSource.Normalize(label.Value)
                        == "PaletteOpenDecision.DismissSearchThenOpen"));

        // Control-dependence AND ordering, inside the arm itself:
        // consume, then close, then adopt. Close-before-consume hands
        // the restore race back to search's Dismissed path, and
        // adopt-before-close would capture a token search then nulls.
        string body = string.Concat(
            arm.Statements.Select(statement => CSharpSource.Normalize(statement)));
        int consume = body.IndexOf(
            "preSearch=ConsumePreSearchFocus()", StringComparison.Ordinal);
        int close = body.IndexOf(
            "_viewModel.Search.Supersede()", StringComparison.Ordinal);
        int adopt = body.IndexOf(
            "_focusBeforePalette=preSearch", StringComparison.Ordinal);
        Assert.True(consume >= 0,
            "the dismissal arm no longer consumes the pre-search focus "
            + "INTO the local the adoption reads — a discarded consume "
            + "leaves the palette's restore targeting the collapsed "
            + "search box.");
        Assert.True(close > consume,
            "the dismissal arm supersedes search before consuming its "
            + "focus (or not at all — a plain Close here also drops the "
            + "scope) — search's own queued restore races the handoff.");
        Assert.True(adopt > close,
            "the dismissal arm never adopts the consumed focus into "
            + "_focusBeforePalette after the supersession — Escape from "
            + "the palette falls back to the editor instead of the "
            + "element the user came from.");

        // Red team A4: exactly ONE assignment to _focusBeforePalette in
        // the arm — a second, later assignment (nulling or re-capturing)
        // would defeat the adoption while every pin above stays green.
        Assert.Equal(
            1,
            arm.Statements
                .SelectMany(statement => statement.DescendantNodesAndSelf())
                .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.AssignmentExpressionSyntax>()
                .Count(assignment => CSharpSource.Normalize(assignment.Left)
                    == "_focusBeforePalette"));

        // The arm ADMITS the open: a false return would dismiss search
        // and then refuse the palette, stranding the user surface-less.
        Microsoft.CodeAnalysis.CSharp.Syntax.ReturnStatementSyntax admit =
            Assert.Single(arm.Statements
                .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ReturnStatementSyntax>());
        Assert.Equal("true", CSharpSource.Normalize(admit.Expression!));

        // The Quick Open arm adopts too (red team F2 after round 11):
        // dismissal without adoption let the palette's ??= capture the
        // collapsing switcher box, and Escape landed in the editor
        // instead of the element the user came from.
        Microsoft.CodeAnalysis.CSharp.Syntax.SwitchSectionSyntax quickOpenArm =
            Assert.Single(
                gate.Sections,
                section => section.Labels
                    .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.CaseSwitchLabelSyntax>()
                    .Any(label => CSharpSource.Normalize(label.Value)
                        == "PaletteOpenDecision.DismissQuickOpenThenOpen"));
        string quickOpenBody = string.Concat(
            quickOpenArm.Statements.Select(
                statement => CSharpSource.Normalize(statement)));
        int adoptSwitcher = quickOpenBody.IndexOf(
            "_focusBeforePalette=ConsumePreSwitcherFocus()",
            StringComparison.Ordinal);
        int dismissSwitcher = quickOpenBody.IndexOf(
            "_viewModel.QuickSwitcher!.Dismiss()", StringComparison.Ordinal);
        Assert.True(adoptSwitcher >= 0,
            "the Quick Open arm no longer adopts the pre-switcher focus "
            + "into _focusBeforePalette.");
        Assert.True(dismissSwitcher > adoptSwitcher,
            "the Quick Open arm dismisses before adopting — the ??= "
            + "capture then grabs the collapsing switcher box.");

        // The palette restore runs BOTH adoption twins (rounds 6 and
        // 11) and the supersession stand-down; the picker dismissal
        // restores all stand down while another surface owns the
        // moment, which is what keeps a supersession from flashing and
        // announcing the editor mid-handoff, and a landing sheet from
        // having focus stolen back out from behind its scrim.
        Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax paletteRestore =
            CSharpSource.Load("MainWindow.Palette.cs").Method("RestoreFocusAfterPalette");
        Assert.True(
            CSharpSource.Invokes(paletteRestore, "AdoptPaletteFocusIntoSearch"),
            "RestoreFocusAfterPalette dropped the round-6 search adoption.");
        Assert.True(
            CSharpSource.Invokes(paletteRestore, "AdoptPaletteFocusIntoQuickOpen"),
            "RestoreFocusAfterPalette dropped the Quick Open adoption twin.");
        foreach ((string file, string method) in new[]
        {
            ("MainWindow.Search.cs", "RestoreFocusAfterSearch"),
            ("MainWindow.Palette.cs", "RestoreFocusAfterPalette"),
            ("MainWindow.xaml.cs", "RestoreFocusAfterQuickOpen"),
        })
        {
            Assert.Contains(
                "if(OpenModalSurfaceisnotnull){return;}",
                CSharpSource.Normalize(CSharpSource.Load(file).Method(method)),
                StringComparison.Ordinal);
        }

        // Codex round 11: the adopted token SURVIVES only because the
        // palette's open observer captures with ??= — a plain
        // assignment overwrites the adoption with the focused,
        // about-to-collapse search box the moment Palette.Open raises
        // IsOpen, and every pin above stays textually intact. The
        // exclusion is the broad "_focusBeforePalette=" (red team A4:
        // the narrower uncast spelling missed a cast-and-assign
        // mutation); "??=" does not contain that substring.
        string observer = CSharpSource.Normalize(
            CSharpSource.Load("MainWindow.Palette.cs")
                .Method("Palette_PropertyChanged"));
        Assert.Contains(
            "_focusBeforePalette??=Keyboard.FocusedElement",
            observer,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "_focusBeforePalette=",
            observer,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// The palette CHORD consults the admission (red team A1 after
    /// codex round 11 — the milestone's exact defect signature, found
    /// in this branch's own new gate): every unit pin bound the
    /// decision to the arm inside <c>TryClearTheWayForThePalette</c>,
    /// but nothing pinned that Ctrl+Shift+P calls that method at all —
    /// a bare <c>Palette.Open()</c> in the chord branch left every
    /// gate green while the palette stacked over a live search again.
    /// The Quick Open twin got this pin after rounds 6/8; the palette
    /// now has it too: every <c>Palette.Open()</c> in the key route is
    /// control-dependent on either the admission or the PD-2 re-open
    /// branch.
    /// </summary>
    [Fact]
    public void ThePaletteChordBranchConsultsTheAdmissionDecision()
    {
        Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax route =
            CSharpSource.Load("MainWindow.xaml.cs").Method("Window_PreviewKeyDown");

        var admission = route.DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.IfStatementSyntax>()
            .Where(statement => CSharpSource.Normalize(statement.Condition)
                == "TryClearTheWayForThePalette()")
            .ToList();
        Assert.True(
            admission.Count == 1,
            $"expected exactly one TryClearTheWayForThePalette-gated if in "
            + $"Window_PreviewKeyDown, found {admission.Count}.");

        var reopen = route.DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.IfStatementSyntax>()
            .Single(statement => CSharpSource.Normalize(statement.Condition)
                == "_viewModel.Palette.IsOpen");

        foreach (Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax open
            in route.DescendantNodes()
                .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax>()
                .Where(call => CSharpSource.Normalize(call.Expression)
                    == "_viewModel.Palette.Open"))
        {
            bool insideAdmission = open.Ancestors().Contains(admission[0].Statement);
            bool insideReopen = open.Ancestors().Contains(reopen.Statement);
            Assert.True(
                insideAdmission || insideReopen,
                "a Palette.Open() in the key route is gated by neither the "
                + "admission decision nor the PD-2 re-open branch — the "
                + "palette opens beneath whatever is up.");
        }

        Assert.True(
            admission[0].Statement.DescendantNodesAndSelf()
                .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax>()
                .Any(call => CSharpSource.Normalize(call.Expression)
                    == "_viewModel.Palette.Open"),
            "the admission-gated if no longer contains the Open() — the "
            + "decision is computed but does not control the open.");
    }

    /// <summary>
    /// The presentation-time admission covers every sheet and is wired
    /// (codex round 11): a sheet may present from a deferred
    /// continuation — the files-citing load, the bases edit-JSON
    /// fetch, a parked citation summary — after the dispatch-time
    /// modal decision has gone stale, so the lifecycle closes the
    /// pickers reactively when a sheet property becomes non-null. One
    /// nameof arm per sheet member of <see cref="ModalSurface"/>, so a
    /// new sheet without an arm fails here by name; nameof makes each
    /// arm's property compile-checked, so no reflection pin is needed
    /// on top. The behavioural halves live in
    /// <c>SheetPresentationAdmissionTests</c>.
    /// </summary>
    [Fact]
    public void TheSheetPresentationObserverCoversEverySheetAndIsWired()
    {
        CSharpSource source = CSharpSource.Load("VaultLifecycleViewModel.cs");
        Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax handler =
            source.Method("Workspace_SheetPresented");

        Microsoft.CodeAnalysis.CSharp.Syntax.SwitchExpressionSyntax presented = handler
            .DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.SwitchExpressionSyntax>()
            .Single();
        foreach (ModalSurface sheet in Enum.GetValues<ModalSurface>()
            .Where(surface => surface > ModalSurface.CommandPalette))
        {
            string property = sheet switch
            {
                ModalSurface.AddProperty => "AddPropertySheet",
                ModalSurface.BulkRename => "BulkRenameSheet",
                ModalSurface.CitationDetails => "CitationDetails",
                ModalSurface.CitationSummary => "CitationSummary",
                ModalSurface.FilesCiting => "FilesCiting",
                ModalSurface.DashboardEditor => "DashboardEditorSheet",
                ModalSurface.BaseQueryBuilder => "BaseQueryBuilderSheet",
                ModalSurface.TemplatePicker => "TemplatePickerSheet",
                ModalSurface.TemplateFlow => "TemplateFlowSheet",
                _ => throw new Xunit.Sdk.XunitException(
                    $"{sheet} is a sheet with no property mapping here — "
                    + "add it AND the observer arm."),
            };
            Assert.True(
                presented.Arms.Any(arm =>
                    CSharpSource.Normalize(arm.Pattern)
                        == $"nameof(WorkspaceViewModel.{property})"
                    && CSharpSource.Normalize(arm.Expression)
                        == $"workspace.{property}isnotnull"),
                $"Workspace_SheetPresented has no arm for {sheet} "
                + $"(WorkspaceViewModel.{property}) — that sheet can land "
                + "over an open picker unnoticed.");
        }

        // The dismissals exist and EVERY one is control-dependent on a
        // presented sheet (red team A2: the first draft ordered the
        // guard against one of the three, so hoisting Palette.Dismiss
        // above the guard — dismissing on every workspace property
        // change — stayed green). Supersede, not Close, for search:
        // the scope survives a sheet landing exactly as it survives
        // the palette. Backing fields, not the lazy getters, so a
        // window-free host's first presentation does not construct the
        // palette as a side effect.
        Microsoft.CodeAnalysis.CSharp.Syntax.IfStatementSyntax guard = handler
            .DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.IfStatementSyntax>()
            .Single(statement =>
                CSharpSource.Normalize(statement.Condition) == "!presented");
        foreach (string dismissal in new[]
        {
            "_search?.Supersede();",
            "QuickSwitcher?.Dismiss();",
            "_palette?.Dismiss();",
        })
        {
            Microsoft.CodeAnalysis.CSharp.Syntax.ExpressionStatementSyntax statement =
                Assert.Single(
                    handler.DescendantNodes()
                        .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ExpressionStatementSyntax>(),
                    candidate => CSharpSource.Normalize(candidate) == dismissal);
            Assert.True(
                guard.SpanStart < statement.SpanStart,
                $"{dismissal} runs BEFORE the presented guard — it would "
                + "fire on every workspace property change, not just "
                + "presentations.");
        }

        // Reachability AND position: subscribed when the workspace is
        // created, AFTER the Workspace assignment (so the shell's sheet
        // handlers subscribe first and the sheet's focus grab queues
        // before any restore the dismissals queue — red team B4: this
        // ordering was previously comment-only), and unsubscribed at
        // teardown.
        Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax initialize =
            source.Method("InitializeWorkspace");
        Microsoft.CodeAnalysis.CSharp.Syntax.ExpressionStatementSyntax assignment =
            Assert.Single(
                initialize.DescendantNodes()
                    .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ExpressionStatementSyntax>(),
                candidate => CSharpSource.Normalize(candidate) == "Workspace=workspace;");
        Microsoft.CodeAnalysis.CSharp.Syntax.ExpressionStatementSyntax subscription =
            Assert.Single(
                initialize.DescendantNodes()
                    .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ExpressionStatementSyntax>(),
                candidate => CSharpSource.Normalize(candidate)
                    == "workspace.PropertyChanged+=Workspace_SheetPresented;");
        Assert.True(
            assignment.SpanStart < subscription.SpanStart,
            "Workspace_SheetPresented subscribes BEFORE the Workspace "
            + "assignment — the lifecycle's dismissals would then queue "
            + "their restores before the shell's sheet focus grab, and "
            + "the restore steals focus from behind the landing sheet.");
        Microsoft.CodeAnalysis.CSharp.Syntax.ExpressionStatementSyntax unsubscription =
            Assert.Single(
                source.Root.DescendantNodes()
                    .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ExpressionStatementSyntax>(),
                candidate => CSharpSource.Normalize(candidate)
                    == "Workspace.PropertyChanged-=Workspace_SheetPresented;");
        Assert.NotNull(unsubscription);
    }

    /// <summary>
    /// The ancestry walk survives a non-Visual focus token (codex round
    /// 7): a focused reading-view Hyperlink is a FrameworkContentElement,
    /// and VisualTreeHelper.GetParent THROWS for one — the palette
    /// dismissal crashed instead of preserving the search-predates-palette
    /// lineage.
    /// </summary>
    [Fact]
    public void TheAncestryWalkSurvivesAFocusedHyperlink()
    {
        Exception? failure = null;
        var thread = new System.Threading.Thread(() =>
        {
            try
            {
                var hyperlink = new System.Windows.Documents.Hyperlink(
                    new System.Windows.Documents.Run("link"));
                var text = new System.Windows.Controls.TextBlock(hyperlink);
                var inside = new System.Windows.Controls.Border { Child = text };
                var outside = new System.Windows.Controls.Border();

                // Walk from the Hyperlink: must not throw, must find the
                // enclosing border, must not find an unrelated one.
                Assert.True(WalksTo(hyperlink, inside));
                Assert.False(WalksTo(hyperlink, outside));
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(System.Threading.ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null)
        {
            throw new Xunit.Sdk.XunitException(failure.ToString());
        }
    }

    /// <summary>
    /// Every ancestry walker in the shell uses the hybrid parent — a
    /// census, because round 7 fixed one walker and round 8 found its
    /// clone still crashing on non-Visual focus.
    /// </summary>
    [Fact]
    public void EveryAncestryWalkerUsesTheHybridParent()
    {
        string root = SourceText.ShellSourceRoot();
        var found = 0;
        foreach (string path in System.IO.Directory.GetFiles(root, "MainWindow*.cs"))
        {
            string file = System.IO.Path.GetFileName(path);
            foreach (Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax method
                in CSharpSource.Load(file).Root
                    .DescendantNodes()
                    .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>())
            {
                if (!method.Identifier.ValueText.StartsWith(
                    "IsDescendantOf", StringComparison.Ordinal))
                {
                    continue;
                }

                found++;
                Assert.True(
                    CSharpSource.Invokes(method, "FocusAncestry.Parent"),
                    $"{file}.{method.Identifier.ValueText} does not walk via "
                    + "FocusAncestry.Parent — VisualTreeHelper.GetParent throws "
                    + "for non-Visual focus (a reading-view Hyperlink).");
                Assert.False(
                    CSharpSource.Invokes(method, "VisualTreeHelper.GetParent"),
                    $"{file}.{method.Identifier.ValueText} still calls "
                    + "VisualTreeHelper.GetParent directly.");
            }
        }

        Assert.True(found >= 2, $"expected at least the palette and search "
            + $"walkers; found {found} — the census is scanning nothing.");
    }

    private static bool WalksTo(
        System.Windows.DependencyObject start,
        System.Windows.DependencyObject ancestor)
    {
        for (System.Windows.DependencyObject? node = start;
            node is not null;
            node = FocusAncestry.Parent(node))
        {
            if (ReferenceEquals(node, ancestor))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The two-string refusal partition is pinned — the strings
    /// literally, the routing structurally (verification round,
    /// finding 2): the code was correct but swapping the strings or
    /// inverting the own-flow condition left the whole suite green,
    /// the exact constant-vs-constant blindness the empty/failed
    /// reasons were cured of in the same wave.
    /// </summary>
    [Fact]
    public void TheRefusalSpeaksMacsTwoStringPartition()
    {
        // The LITERAL mac strings (AppState.swift:7901-7904).
        Assert.Equal(
            "Finish or cancel the current template note before starting another.",
            MainWindow.TemplateFlowBusyReason);
        Assert.Equal(
            "Finish or cancel the current dialog before creating from a template.",
            MainWindow.TemplateDialogBusyReason);

        // The routing: re-entry over the flow's own surfaces speaks
        // flow-busy, everything else dialog-busy.
        string admission = CSharpSource.Normalize(
            CSharpSource.Load("MainWindow.Templates.cs")
                .Method("TryClearTheWayForTemplates"));
        Assert.Contains(
            "boolrefusedByOwnFlow=OpenModalSurface"
            + "isModalSurface.TemplatePickerorModalSurface.TemplateFlow",
            admission,
            StringComparison.Ordinal);
        Assert.Contains(
            "refusedByOwnFlow?TemplateFlowBusyReason:TemplateDialogBusyReason",
            admission,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Esc cancels from BOTH template sheets' key routes (red team,
    /// tests finding 9): every unit fact drives CancelCommand directly
    /// and the journey Escapes only the picker, so deleting either
    /// Escape case left Esc dead on that sheet with the suite green.
    /// T7 promises Esc at every step.
    /// </summary>
    [Theory]
    [InlineData("TemplatePickerOverlay_PreviewKeyDown")]
    [InlineData("TemplateFlowOverlay_PreviewKeyDown")]
    public void TheTemplateSheetKeyRoutesCancelOnEscape(string handler)
    {
        string route = CSharpSource.Normalize(
            CSharpSource.Load("MainWindow.Templates.cs").Method(handler));
        Assert.Contains("caseKey.Escape:", route, StringComparison.Ordinal);
        Assert.Contains(
            "CancelCommand.Execute(null)", route, StringComparison.Ordinal);
    }

    /// <summary>
    /// The template flow's one-gate design is WIRED, not just designed
    /// (red team W5-3, tests finding 2 — the milestone's exact defect
    /// signature, again): the pure DecideTemplateOpen theory proves the
    /// table, but nothing bound (a) the window handing the workspace
    /// its admission, (b) the workspace consulting it before any
    /// presentation, (c) the admission consulting the decision and
    /// performing the dismissals, or (d) the chord reaching the guarded
    /// open from BOTH branches — deleting any one left the suite green
    /// while the flow presented beneath sheets or lost an opener.
    /// </summary>
    [Fact]
    public void TheTemplateOpenersShareOneWiredAdmission()
    {
        // (a) The window wires the seam.
        Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax wire =
            CSharpSource.Load("MainWindow.Templates.cs").Method("WireWorkspaceTemplates");
        Assert.Contains(
            "workspace.TemplateOpenAdmission=TryClearTheWayForTemplates",
            CSharpSource.Normalize(wire),
            StringComparison.Ordinal);

        // (b) The workspace consults the seam BEFORE any presentation:
        // the refusal return precedes the sheet assignment in source.
        Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax open =
            CSharpSource.Load("WorkspaceViewModel.Templates.cs").Method("OpenTemplatePicker");
        string openText = CSharpSource.Normalize(open);
        int admissionAt = openText.IndexOf(
            "TemplateOpenAdmission?.Invoke()==false", StringComparison.Ordinal);
        int presentAt = openText.IndexOf(
            "TemplatePickerSheet=picker", StringComparison.Ordinal);
        Assert.True(
            admissionAt >= 0 && presentAt > admissionAt,
            "OpenTemplatePicker no longer consults TemplateOpenAdmission "
            + "before presenting — a refused open would still present, or "
            + "present first and refuse after.");

        // (c) The admission consults the decision and can perform every
        // dismissal arm plus the refusal announcement.
        Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax admission =
            CSharpSource.Load("MainWindow.Templates.cs").Method("TryClearTheWayForTemplates");
        Assert.True(
            CSharpSource.Invokes(admission, "ModalSurfaces.DecideTemplateOpen"),
            "TryClearTheWayForTemplates no longer consults DecideTemplateOpen.");
        string admissionText = CSharpSource.Normalize(admission);
        foreach (string dismissal in new[]
        {
            "_viewModel.QuickSwitcher!.Dismiss",
            "_viewModel.Search.Supersede",
            "_viewModel.Palette.Dismiss",
            "_announcer.Post",
        })
        {
            Assert.Contains(dismissal, admissionText, StringComparison.Ordinal);
        }

        // (d) Both chord branches reach the guarded open: the ordinary
        // branch, and the carve-out inside the palette-open block —
        // without the latter, Ctrl+Shift+N under the palette is a
        // silent dead key (three-way red-team convergence).
        Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax route =
            CSharpSource.Load("MainWindow.xaml.cs").Method("Window_PreviewKeyDown");
        // A conditional access parses as a MemberBinding invocation
        // (".OpenTemplatePicker"); match the whole ?.-chain instead.
        var opens = route.DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ConditionalAccessExpressionSyntax>()
            .Where(access => CSharpSource.Normalize(access)
                == "_viewModel.Workspace?.OpenTemplatePicker()")
            .ToList();
        Assert.Equal(2, opens.Count);
        Microsoft.CodeAnalysis.CSharp.Syntax.IfStatementSyntax paletteBlock =
            route.DescendantNodes()
                .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.IfStatementSyntax>()
                .Single(statement => CSharpSource.Normalize(statement.Condition)
                    == "_viewModel.Palette.IsOpen");
        Assert.Equal(
            1,
            opens.Count(call => call.Ancestors().Contains(paletteBlock.Statement)));
    }

    /// <summary>
    /// The pickers are mutually exclusive (codex round 4): Quick Open
    /// paints BELOW search, so opening it under an open search overlay
    /// put focus in a hidden box. The shell's switcher-open observer
    /// must close search and adopt the pre-search focus as the
    /// switcher's return target.
    /// </summary>
    [Fact]
    public void QuickOpenOpeningClosesAnOpenSearchOverlay()
    {
        Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax observer =
            CSharpSource.Load("MainWindow.xaml.cs").Method("QuickSwitcher_PropertyChanged");

        Assert.True(
            CSharpSource.Invokes(observer, "_viewModel.Search.Supersede"),
            "the switcher-open observer no longer supersedes an open search "
            + "overlay — Quick Open opens BENEATH it and typing lands in a "
            + "hidden box (and a plain Close here would drop the scope).");
        Assert.True(
            CSharpSource.Invokes(observer, "ConsumePreSearchFocus"),
            "the switcher-open observer no longer adopts the pre-search "
            + "focus, so its restore would target the collapsed search box.");
    }

    private static ModalSurfaceState StateWith(
        ModalSurface first, ModalSurface second) =>
        new(
            QuickOpen: first is ModalSurface.QuickOpen || second is ModalSurface.QuickOpen,
            SearchOverlay: first is ModalSurface.SearchOverlay || second is ModalSurface.SearchOverlay,
            CommandPalette: first is ModalSurface.CommandPalette || second is ModalSurface.CommandPalette,
            AddProperty: first is ModalSurface.AddProperty || second is ModalSurface.AddProperty,
            BulkRename: first is ModalSurface.BulkRename || second is ModalSurface.BulkRename,
            CitationDetails: first is ModalSurface.CitationDetails || second is ModalSurface.CitationDetails,
            CitationSummary: first is ModalSurface.CitationSummary || second is ModalSurface.CitationSummary,
            FilesCiting: first is ModalSurface.FilesCiting || second is ModalSurface.FilesCiting,
            DashboardEditor: first is ModalSurface.DashboardEditor || second is ModalSurface.DashboardEditor,
            BaseQueryBuilder: first is ModalSurface.BaseQueryBuilder || second is ModalSurface.BaseQueryBuilder,
            TemplatePicker: first is ModalSurface.TemplatePicker || second is ModalSurface.TemplatePicker,
            TemplateFlow: first is ModalSurface.TemplateFlow || second is ModalSurface.TemplateFlow);

    [Fact]
    public void EveryMenuDisablePathResolvesAgainstTheLiveViewModels()
    {
        foreach ((ModalSurface surface, string path) in MenuDisableBindings)
        {
            System.Type owner = typeof(VaultLifecycleViewModel);
            foreach (string segment in path.Split('.'))
            {
                System.Reflection.PropertyInfo? property = owner.GetProperty(
                    segment,
                    System.Reflection.BindingFlags.Public
                        | System.Reflection.BindingFlags.NonPublic
                        | System.Reflection.BindingFlags.Instance);
                Assert.True(
                    property is not null,
                    $"{surface}: binding path segment '{segment}' of '{path}' "
                    + $"does not resolve on {owner.Name} — the menu trigger "
                    + "is silently dead while the C# state reader still "
                    + "compiles.");
                owner = property!.PropertyType;
            }
        }
    }

    /// <summary>
    /// The Menu disables under EVERY modal surface, one trigger per
    /// <see cref="ModalSurface"/> member.
    /// </summary>
    /// <remarks>
    /// <para>
    /// W5-2 red-team round 1: the trigger list covered only the three
    /// overlays, so Workspace ▸ Search Vault… opened the overlay
    /// INVISIBLY beneath any of the seven sheets — and File ▸ Quick
    /// Open… had carried the same gap since W1. The declarative fix is
    /// mac parity (AppKit disables the menu bar while a sheet is up,
    /// which is why mac's unguarded menu commands are safe); this gate
    /// is what keeps surface #11 from being forgotten the way the seven
    /// sheets were: a new enum member without a mapping entry, or a
    /// mapping entry without a trigger, fails here by name.
    /// </para>
    /// <para>
    /// A XAML scrape rather than a live-window test for the recorded
    /// drift-gate reason: the trigger list is declarative data, and
    /// every sheet would need a real shell to open.
    /// </para>
    /// </remarks>
    [Fact]
    public void TheMenuDisablesUnderEveryModalSurface()
    {
        XDocument document = XDocument.Load(
            Path.Combine(SourceRoot(), "MainWindow.xaml"));
        XElement[] menus = document.Descendants()
            .Where(element => element.Name.LocalName == "Menu")
            .ToArray();
        XElement menu = Assert.Single(menus);

        XElement style = menu.Elements()
            .Single(element => element.Name.LocalName == "Menu.Style");
        XElement[] triggers = style.Descendants()
            .Where(element => element.Name.LocalName == "DataTrigger")
            .ToArray();

        foreach (ModalSurface surface in Enum.GetValues<ModalSurface>())
        {
            Assert.True(
                MenuDisableBindings.TryGetValue(surface, out string? path),
                $"{surface} has no entry in MenuDisableBindings — a new "
                + "modal surface needs a Menu disable trigger AND a row in "
                + "this map, or its menu commands run invisibly beneath it.");

            XElement? trigger = triggers.FirstOrDefault(
                candidate => BindsPath(candidate, path!));
            Assert.True(
                trigger is not null,
                $"MainWindow.xaml's Menu style has no DataTrigger binding "
                + $"'{path}' — with {surface} open the menu stays enabled and "
                + "its commands open overlays beneath the surface (the "
                + "round-1 finding).");

            Assert.Equal("True", trigger!.Attribute("Value")?.Value);
            if (!path!.EndsWith(".IsOpen", StringComparison.Ordinal))
            {
                // Object-typed sheet properties: without the converter
                // the trigger compares a view model to the string
                // "True" and never fires.
                Assert.Contains(
                    "IsNotNullConverter",
                    trigger.Attribute("Binding")!.Value,
                    StringComparison.Ordinal);
            }

            XElement setter = Assert.Single(trigger.Descendants()
, element => element.Name.LocalName == "Setter");
            Assert.Equal("IsEnabled", setter.Attribute("Property")?.Value);
            Assert.Equal("False", setter.Attribute("Value")?.Value);
        }
    }

    /// <summary>
    /// Whether a DataTrigger's Binding reads exactly
    /// <paramref name="path"/> — a whole-path match, so
    /// <c>Search.IsOpen</c> cannot be satisfied by a hypothetical
    /// <c>Search.IsOpenedOnce</c>.
    /// </summary>
    private static bool BindsPath(XElement trigger, string path)
    {
        string? binding = trigger.Attribute("Binding")?.Value;
        string prefix = "{Binding " + path;
        if (binding is null
            || !binding.StartsWith(prefix, StringComparison.Ordinal)
            || binding.Length == prefix.Length)
        {
            return false;
        }

        return binding[prefix.Length] is ',' or '}' or ' ';
    }

    /// <summary>
    /// The search overlay's field row carries the "Close search" button
    /// (contract S15, red-team round 1): mac's overlay has one
    /// (<c>SearchOverlay.swift:169-198</c>, label and hint verbatim),
    /// and without it Esc was the only dismissal for a surface the menu
    /// can open by pointer. The journey clicks it; this pins the
    /// anatomy and the wiring so the assertion does not depend on an
    /// interactive desktop.
    /// </summary>
    [Fact]
    public void TheSearchOverlayCarriesItsCloseButton()
    {
        XDocument document = XDocument.Load(
            Path.Combine(SourceRoot(), "MainWindow.xaml"));
        XElement button = Assert.Single(document.Descendants()
, element =>
                    element.Name.LocalName == "Button"
                    && element.Attribute(
                        "AutomationProperties.AutomationId")?.Value
                        == "SearchOverlayClose");

        Assert.Equal(
            "Close search",
            button.Attribute("AutomationProperties.Name")?.Value);
        Assert.Equal(
            "Closes the search overlay and returns to the previous view.",
            button.Attribute("AutomationProperties.HelpText")?.Value);
        Assert.Equal(
            "SearchOverlayClose_Click",
            button.Attribute("Click")?.Value);

        // And the handler actually closes: a live Close() call on the
        // search view model, read from the parsed source rather than a
        // string scrape so a commented-out body cannot answer.
        CSharpSource shell = CSharpSource.Load("MainWindow.Search.cs");
        MethodDeclarationSyntax handler = shell.Root
            .DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Single(method =>
                method.Identifier.ValueText == "SearchOverlayClose_Click");
        Assert.Contains(
            handler.DescendantNodes().OfType<InvocationExpressionSyntax>(),
            invocation => invocation.Expression is MemberAccessExpressionSyntax
            {
                Name.Identifier.ValueText: "Close",
            });
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
