// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Windows.Input;

namespace SlateWindows;

/// <summary>
/// The shell's modal overlays, in paint order (last wins).
/// </summary>
/// <remarks>
/// <para>
/// Ten surfaces are declared as siblings in one <c>Grid</c> cell of
/// <c>MainWindow.xaml</c> with no <c>Panel.ZIndex</c> anywhere, so which
/// one paints on top is decided by XAML declaration order. This enum
/// makes that order explicit data instead of an accident of line
/// numbering, and gives the shell something to ask "is anything modal
/// open, and which?".
/// </para>
/// <para>
/// Written as the W5-1 design pass after the red-team protocol's
/// stopping rule fired: three consecutive rounds produced blockers in the
/// same key-routing branch, and each fix was incomplete because nothing
/// knew the SET of open surfaces. A guard written for one overlay
/// silently omitted the other eight.
/// </para>
/// <para>
/// <c>SearchOverlay</c> (W5-2, #742) sits between Quick Open and the
/// palette: mac allows the palette to open OVER an open search overlay —
/// its Return monitor guards <c>!isCommandPaletteOpen</c> for exactly
/// that stacking — so the palette must be declared after search and
/// paint above it.
/// </para>
/// </remarks>
internal enum ModalSurface
{
    QuickOpen,
    SearchOverlay,
    CommandPalette,
    AddProperty,
    BulkRename,
    CitationDetails,
    CitationSummary,
    FilesCiting,
    DashboardEditor,
    BaseQueryBuilder,
}

/// <summary>
/// What the shell needs to know about a modal surface while it owns the
/// keyboard.
/// </summary>
/// <param name="Surface">Which surface this describes.</param>
/// <param name="OwnsTextField">
/// Whether the surface hosts a text field that must keep the editing
/// chords. Text editing reaches a <c>TextBox</c> through
/// <c>InputBindings</c>, which WPF runs only for UNHANDLED key events, so
/// a surface that swallows every modified key kills paste, select-all and
/// Shift-selection inside its own box.
/// </param>
internal readonly record struct ModalSurfaceDescriptor(
    ModalSurface Surface,
    bool OwnsTextField);

/// <summary>
/// What the shell should do when the palette chord arrives.
/// </summary>
internal enum PaletteOpenDecision
{
    /// <summary>Nothing is in the way; open it.</summary>
    Open,

    /// <summary>Quick Open is up and the palette supersedes it.</summary>
    DismissQuickOpenThenOpen,

    /// <summary>A higher surface owns the screen; refuse.</summary>
    Refuse,
}

/// <summary>
/// Which modal surfaces are open, one named flag each.
/// </summary>
/// <remarks>
/// The shell fills this in from its view models; everything downstream
/// is pure. Named fields rather than a lambda so a wrong-property error
/// is visible at the assignment site — <c>AddProperty</c> next to
/// <c>AddPropertySheet</c> — instead of buried in a switch nothing
/// exercises.
/// </remarks>
internal readonly record struct ModalSurfaceState(
    bool QuickOpen,
    bool SearchOverlay,
    bool CommandPalette,
    bool AddProperty,
    bool BulkRename,
    bool CitationDetails,
    bool CitationSummary,
    bool FilesCiting,
    bool DashboardEditor,
    bool BaseQueryBuilder);

/// <summary>
/// The modal-surface precedence rules, as pure functions.
/// </summary>
/// <remarks>
/// Extracted from <c>MainWindow</c> so they can be gated. The round-3
/// blocker — the palette opening underneath seven sheets — was fixed in
/// the design pass with a guard that NO test drove: removing any single
/// sheet predicate left the suite green, which is the same
/// fix-without-a-gate class the review rounds kept finding. Precedence
/// is a pure decision over an enum, so there is no reason it should be
/// reachable only through a live window.
/// </remarks>
internal static class ModalSurfaces
{
    /// <summary>
    /// The topmost open surface, or <see langword="null"/>.
    /// </summary>
    /// <param name="isOpen">
    /// Answers whether a given surface is currently open. Enumerated in
    /// <see cref="ModalSurface"/> paint order, so the LAST open one wins.
    /// </param>
    internal static ModalSurface? TopmostOpen(Func<ModalSurface, bool> isOpen)
    {
        ArgumentNullException.ThrowIfNull(isOpen);
        ModalSurface? top = null;
        foreach (ModalSurface surface in Enum.GetValues<ModalSurface>())
        {
            if (isOpen(surface))
            {
                top = surface;
            }
        }

        return top;
    }

    /// <summary>The topmost open surface for a given state.</summary>
    internal static ModalSurface? TopmostOpen(ModalSurfaceState state) =>
        TopmostOpen(surface => IsOpen(surface, state));

    /// <summary>
    /// Whether the search overlay owns the keyboard: open AND topmost.
    /// </summary>
    /// <remarks>
    /// Codex round 1 (#742): a palette-invoked sheet opens OVER search
    /// without closing it — the palette admits itself above search by
    /// design, and a sheet-opening command presents before the palette
    /// dismisses. The key branch routed on <c>Search.IsOpen</c> alone,
    /// so the hidden overlay stole Enter (activating an invisible result
    /// and opening a note behind the sheet) and Escape (closing the
    /// hidden overlay instead of the sheet). Ownership, not openness, is
    /// the routing question. Search deliberately REMAINS OPEN beneath the
    /// sheet — mac parity, where nothing closes it either — and regains
    /// the keyboard when the sheet closes and it is topmost again.
    /// </remarks>
    /// <summary>
    /// Whether Quick Open's chord may act, given what is already up
    /// (codex round 5, #742). The Ctrl+O branch was unconditional, so
    /// Quick Open opened BENEATH any sheet — and the round-4 picker
    /// exclusion then closed a search overlay under that sheet too,
    /// leaving only the hidden picker active. Search is admitted
    /// because the switcher-open observer closes it (the picker
    /// exclusion); Quick Open re-opens over itself per W1; the palette
    /// and every sheet refuse.
    /// </summary>
    internal static PaletteOpenDecision DecideQuickOpenOpen(ModalSurface? topmost) =>
        topmost switch
        {
            null => PaletteOpenDecision.Open,
            ModalSurface.QuickOpen => PaletteOpenDecision.Open,
            ModalSurface.SearchOverlay => PaletteOpenDecision.Open,
            _ => PaletteOpenDecision.Refuse,
        };

    internal static bool SearchOwnsKeys(ModalSurfaceState state) =>
        TopmostOpen(state) == ModalSurface.SearchOverlay;

    /// <summary>
    /// Whether one named surface is open in <paramref name="state"/>.
    /// </summary>
    /// <remarks>
    /// No wildcard arm: a switch expression that does not cover every
    /// <see cref="ModalSurface"/> member raises CS8509, so adding a
    /// surface without mapping it breaks the build. The previous version
    /// DID have a wildcard and its doc comment claimed compile-time
    /// exhaustiveness it did not have — codex caught that.
    /// </remarks>
    internal static bool IsOpen(ModalSurface surface, ModalSurfaceState state) =>
        surface switch
        {
            ModalSurface.QuickOpen => state.QuickOpen,
            ModalSurface.SearchOverlay => state.SearchOverlay,
            ModalSurface.CommandPalette => state.CommandPalette,
            ModalSurface.AddProperty => state.AddProperty,
            ModalSurface.BulkRename => state.BulkRename,
            ModalSurface.CitationDetails => state.CitationDetails,
            ModalSurface.CitationSummary => state.CitationSummary,
            ModalSurface.FilesCiting => state.FilesCiting,
            ModalSurface.DashboardEditor => state.DashboardEditor,
            ModalSurface.BaseQueryBuilder => state.BaseQueryBuilder,
        };

    /// <summary>
    /// Whether the palette may open, given what is already up.
    /// </summary>
    /// <remarks>
    /// The palette refuses beneath any sheet: every sheet is declared
    /// AFTER it in <c>MainWindow.xaml</c> and carries its own scrim, so a
    /// palette opened underneath is invisible and unreachable by pointer
    /// while still owning every keystroke. Quick Open is the one surface
    /// it supersedes — declared before it, so the palette paints on top
    /// legibly.
    /// </remarks>
    internal static PaletteOpenDecision DecidePaletteOpen(ModalSurface? topmost) =>
        topmost switch
        {
            null => PaletteOpenDecision.Open,

            // PD-2: re-opening while open is allowed and clears the query.
            ModalSurface.CommandPalette => PaletteOpenDecision.Open,

            // W5-2 (S11): the palette opens OVER an open search overlay —
            // mac parity, where the search overlay's Return monitor guards
            // !isCommandPaletteOpen for exactly this stacking. Search is
            // declared before the palette in XAML, so the palette paints on
            // top legibly and owns the keyboard while both are open.
            ModalSurface.SearchOverlay => PaletteOpenDecision.Open,

            ModalSurface.QuickOpen => PaletteOpenDecision.DismissQuickOpenThenOpen,

            _ => PaletteOpenDecision.Refuse,
        };

    /// <summary>
    /// Whether the search overlay's chord may act, given what is already
    /// up (W5-2, contract S11).
    /// </summary>
    /// <remarks>
    /// Mirrors <see cref="DecidePaletteOpen"/> and reuses its decision
    /// enum. <c>SearchOverlay</c> answers <see cref="PaletteOpenDecision.Open"/>
    /// because Ctrl+Shift+F TOGGLES: the view model's <c>Toggle</c>
    /// closes an already-open overlay, so this function's only job is to
    /// keep the chord from firing beneath a higher surface. The palette
    /// and every sheet refuse — the palette may open over search (mac
    /// parity), never the reverse.
    /// </remarks>
    internal static PaletteOpenDecision DecideSearchOpen(ModalSurface? topmost) =>
        topmost switch
        {
            null => PaletteOpenDecision.Open,

            ModalSurface.SearchOverlay => PaletteOpenDecision.Open,

            ModalSurface.QuickOpen => PaletteOpenDecision.DismissQuickOpenThenOpen,

            _ => PaletteOpenDecision.Refuse,
        };
}

/// <summary>
/// The keys a focused text field must keep while a modal surface owns the
/// keyboard.
/// </summary>
/// <remarks>
/// An allow-list rather than a shell-chord deny-list on purpose: the
/// deny-list was written for W1 and has not tracked every chord added
/// since — Ctrl+J, Ctrl+Shift+E and Ctrl+R are all missing from it — so
/// inverting the test would let those fire underneath an overlay. Naming
/// what text editing needs is both smaller and stable.
/// </remarks>
internal static class TextEditingChords
{
    /// <summary>
    /// Whether <paramref name="key"/> with <paramref name="modifiers"/>
    /// must reach a focused text field.
    /// </summary>
    /// <remarks>
    /// <c>internal</c>, not <c>private</c> on the window, so the whole
    /// table is pinned by unit facts rather than only by a journey — the
    /// AltGr arm below shipped broken through two review rounds precisely
    /// because no unit test could reach it.
    /// </remarks>
    /// <param name="rightAltDown">
    /// Whether the RIGHT Alt key is physically down. Injected rather than
    /// read here so the AltGr arm below is testable from both sides — it
    /// shipped broken through two review rounds, and a table that queries
    /// live keyboard state cannot be pinned by a unit fact at all.
    /// </param>
    internal static bool Allows(Key key, ModifierKeys modifiers, bool rightAltDown)
    {
        // Shift alone: capitals, and Shift+arrow/Home/End selection.
        if (modifiers == ModifierKeys.Shift)
        {
            return true;
        }

        if (modifiers == ModifierKeys.Control)
        {
            return key is Key.A or Key.C or Key.V or Key.X or Key.Z or Key.Y
                or Key.Left or Key.Right or Key.Home or Key.End
                or Key.Back or Key.Delete;
        }

        if (modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            // Ctrl+Shift+Z is redo; the rest are word-wise selection.
            return key is Key.Z
                or Key.Left or Key.Right or Key.Home or Key.End;
        }

        // AltGr reaches WPF as Control|Alt, so a naive deny-list swallows
        // it and an overlay silently drops ordinary letters on every
        // layout that uses it — nine of them in Polish, plus @ and the
        // euro sign in German. Distinguished from a real Ctrl+Alt chord by
        // the RIGHT Alt key being physically down.
        //
        // Shift is STRIPPED before the comparison, not required absent.
        // AltGr+Shift is how those same nine Polish letters are typed in
        // UPPERCASE, and an exact-equality test on Control|Alt swallowed
        // every one of them — the first version of this fix corrected the
        // lowercase forms and left the capitals broken.
        if ((modifiers & ~ModifierKeys.Shift) == (ModifierKeys.Control | ModifierKeys.Alt))
        {
            return rightAltDown;
        }

        return false;
    }

    /// <summary>
    /// Production entry point: reads the live right-Alt state.
    /// </summary>
    internal static bool Allows(Key key, ModifierKeys modifiers) =>
        Allows(key, modifiers, Keyboard.IsKeyDown(Key.RightAlt));
}
