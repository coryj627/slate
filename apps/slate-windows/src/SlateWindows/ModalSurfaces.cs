// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows.Input;

namespace SlateWindows;

/// <summary>
/// The shell's modal overlays, in paint order (last wins).
/// </summary>
/// <remarks>
/// <para>
/// Nine surfaces are declared as siblings in one <c>Grid</c> cell of
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
/// </remarks>
internal enum ModalSurface
{
    QuickOpen,
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
        if (modifiers == (ModifierKeys.Control | ModifierKeys.Alt))
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
