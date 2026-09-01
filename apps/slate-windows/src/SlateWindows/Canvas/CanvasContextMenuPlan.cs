// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Immutable;

namespace SlateWindows.Canvas;

/// <summary>One planned row: the verb's label, whether it is live,
/// and — for a staged verb — the reason a reader can hear (the mac
/// RowAction contract: temporarily unavailable stays VISIBLE with its
/// why; not-applicable is ABSENT, decided by the plan, never by a
/// hand-list at a surface).</summary>
internal sealed record CanvasContextMenuRow(
    string Name,
    bool Enabled,
    string? DisabledReason,
    CanvasContextVerb Verb);

/// <summary>The verbs the plan can place — the wiring maps each onto
/// the document's funnel verb (or the editor/mark stubs).</summary>
internal enum CanvasContextVerb
{
    Open,
    ToggleMark,
    Delete,
    Ungroup,
    EditCard,
    RenameGroup,
    SetColor,
}

/// <summary>
/// W6-1 §E TE-8 (IE-31): the ONE applicability table — rows derived
/// from the row's KIND with explicit exclusions, feeding BOTH the
/// table's row actions and the outline's context menu, so the two
/// surfaces cannot drift from each other or from the verb inventory
/// (the derived-consumer discipline; the census asserts equality).
/// </summary>
internal static class CanvasContextMenuPlan
{
    /// <summary>The rows for one outline/table row's kind, in the
    /// mac order: Open, Toggle Mark, then the kind's own verbs.
    /// Toggle Mark stays STAGED with its reason (PR G); Delete is
    /// LIVE for card kinds; a group's removal is Ungroup (ED-3 — the
    /// algebra's one group removal, no button promising more).</summary>
    internal static ImmutableArray<CanvasContextMenuRow> RowsFor(string kind)
    {
        ArgumentNullException.ThrowIfNull(kind);
        ImmutableArray<CanvasContextMenuRow>.Builder rows =
            ImmutableArray.CreateBuilder<CanvasContextMenuRow>();
        rows.Add(new(CanvasPhrase.OpenRowAction, true, null, CanvasContextVerb.Open));
        rows.Add(new(
            CanvasPhrase.ToggleMarkRowAction,
            false,
            CanvasPhrase.MarkingArrivesLater,
            CanvasContextVerb.ToggleMark));
        if (kind == "group")
        {
            // §F TF-8 (FD-4): the prompt machinery landed — the row
            // goes live on the §E commit path.
            rows.Add(new(
                CanvasPhrase.RenameGroupRowAction, true,
                null, CanvasContextVerb.RenameGroup));
            rows.Add(new(
                CanvasPhrase.UngroupRowAction, true, null, CanvasContextVerb.Ungroup));
        }
        else
        {
            if (kind == "text")
            {
                rows.Add(new(
                    CanvasPhrase.EditCardRowAction, true, null,
                    CanvasContextVerb.EditCard));
            }
            rows.Add(new(
                CanvasPhrase.SetColorRowAction, true,
                null, CanvasContextVerb.SetColor));
            rows.Add(new(
                CanvasPhrase.DeleteRowAction, true, null, CanvasContextVerb.Delete));
        }
        return rows.ToImmutable();
    }
}
