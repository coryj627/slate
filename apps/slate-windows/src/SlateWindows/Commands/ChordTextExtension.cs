// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Windows.Markup;

namespace SlateWindows.Commands;

/// <summary>
/// Resolves a menu item's accelerator text from the chord table:
/// <c>InputGestureText="{local:ChordText slate.workspace.newTab}"</c>.
/// </summary>
/// <remarks>
/// <para>
/// PINV-5 says a Windows chord string is authored in exactly one place.
/// It was not: menu <c>InputGestureText</c> values were hand-typed in
/// XAML, so the menu could advertise one chord while the table, the
/// palette row, and the spoken hotkey said another. The drift test could
/// not see it either — it compared bare strings across the whole table,
/// so two commands swapping gestures stayed green.
/// </para>
/// <para>
/// Resolving through the table makes the menu a CONSUMER of it, which is
/// what the contract always claimed. An unknown id throws at parse time
/// rather than rendering an empty accelerator, because a menu item
/// silently losing its chord is exactly the drift this is meant to stop.
/// </para>
/// </remarks>
[MarkupExtensionReturnType(typeof(string))]
internal sealed class ChordTextExtension : MarkupExtension
{
    public ChordTextExtension()
    {
    }

    public ChordTextExtension(string commandId) => CommandId = commandId;

    /// <summary>The <c>slate.*</c> or <c>windows.*</c> row to read.</summary>
    public string CommandId { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        ChordTableEntry row = ChordTable.Find(CommandId)
            ?? throw new InvalidOperationException(
                $"InputGestureText references '{CommandId}', which has no chord-table "
                + "row. Menu accelerators are resolved from the table (PINV-5), so the "
                + "id must exist there.");

        return row.WindowsChord
            ?? throw new InvalidOperationException(
                $"'{CommandId}' has a chord-table row but no Windows chord, so it "
                + "cannot supply an accelerator. Remove the InputGestureText or give "
                + "the row a chord.");
    }
}
