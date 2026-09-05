// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Immutable;
using uniffi.slate_uniffi;

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

/// <summary>The verbs the plan can place — every one maps onto a
/// document verb through the one dispatch table below.</summary>
internal enum CanvasContextVerb
{
    Open,
    EditCard,
    ConvertToNote,
    CreateConnectedCard,
    Duplicate,
    RenameGroup,
    LocateFile,
    ToggleMark,
    ConnectTo,
    SetColor,
    MoveIntoGroup,
    RemoveFromGroup,
    Delete,
    Ungroup,
    JumpToCard,
    EditConnection,
    DeleteConnection,
}

/// <summary>§G2 TG2-7 (G2-12): the consumer asking for rows. The
/// renderer carries no context menu (G2D-12) and is not a surface
/// here.</summary>
internal enum CanvasContextSurface
{
    Outline,
    Grid,
}

/// <summary>§G2 TG2-7 (G2-12, IG2-28): the plan's DISCRIMINATED
/// target — a node with its kind and whether it sits in a group, or a
/// connection captured as the selected-relative neighbor of the row's
/// source node. A connection row's verbs act on THAT edge directly;
/// no picker, no jump.</summary>
internal abstract record CanvasContextTarget
{
    private CanvasContextTarget()
    {
    }

    /// <summary>A node row: <paramref name="Kind"/> is core's type word
    /// (text, file, image, link, group); <paramref name="InGroup"/> is
    /// whether the row's group path is non-empty.</summary>
    internal sealed record Node(string NodeId, string Kind, bool InGroup) : CanvasContextTarget;

    /// <summary>A connection row under <paramref name="SourceNodeId"/>:
    /// the captured neighbor is the edge, seen from the source.</summary>
    internal sealed record Connection(string SourceNodeId, CanvasNeighbor Neighbor)
        : CanvasContextTarget;
}

/// <summary>
/// W6-1 §E TE-8 (IE-31), widened by §G2 TG2-7 (G2-12): the ONE
/// applicability table — rows derived from (surface, target), feeding
/// the outline's context menu AND the grid's row actions, so no
/// surface can drift from the other or from the verb inventory (the
/// derived-consumer discipline; the censuses assert bidirectional
/// equality per surface and per target kind).
/// </summary>
internal static class CanvasContextMenuPlan
{
    /// <summary>Core's node kinds, for the censuses.</summary>
    internal static readonly ImmutableArray<string> NodeKinds =
        ["text", "file", "image", "link", "group"];

    /// <summary>The verbs the GRID projection can place over any kind,
    /// in the plan's order — the grid's action list is built from these
    /// and each action asks the plan, per row, whether it applies.</summary>
    internal static readonly ImmutableArray<CanvasContextVerb> GridVerbs =
    [
        CanvasContextVerb.Open,
        CanvasContextVerb.ToggleMark,
        CanvasContextVerb.Delete,
        CanvasContextVerb.Ungroup,
    ];

    /// <summary>The one label table (mac's labels verbatim): the plan's
    /// rows and the grid's action names both read it.</summary>
    internal static string Label(CanvasContextVerb verb) => verb switch
    {
        CanvasContextVerb.Open => CanvasPhrase.OpenRowAction,
        CanvasContextVerb.EditCard => CanvasPhrase.EditCardRowAction,
        CanvasContextVerb.ConvertToNote => CanvasPhrase.ConvertToNoteRowAction,
        CanvasContextVerb.CreateConnectedCard => CanvasPhrase.CreateConnectedCardRowAction,
        CanvasContextVerb.Duplicate => CanvasPhrase.DuplicateRowAction,
        CanvasContextVerb.RenameGroup => CanvasPhrase.RenameGroupRowAction,
        CanvasContextVerb.LocateFile => CanvasPhrase.LocateFileRowAction,
        CanvasContextVerb.ToggleMark => CanvasPhrase.ToggleMarkRowAction,
        CanvasContextVerb.ConnectTo => CanvasPhrase.ConnectToRowAction,
        CanvasContextVerb.SetColor => CanvasPhrase.SetColorRowAction,
        CanvasContextVerb.MoveIntoGroup => CanvasPhrase.MoveIntoGroupRowAction,
        CanvasContextVerb.RemoveFromGroup => CanvasPhrase.RemoveFromGroupRowAction,
        CanvasContextVerb.Delete => CanvasPhrase.DeleteRowAction,
        CanvasContextVerb.Ungroup => CanvasPhrase.UngroupRowAction,
        CanvasContextVerb.JumpToCard => CanvasPhrase.JumpToCardRowAction,
        CanvasContextVerb.EditConnection => CanvasPhrase.EditConnectionRowAction,
        CanvasContextVerb.DeleteConnection => CanvasPhrase.DeleteConnectionRowAction,
        _ => throw new ArgumentOutOfRangeException(nameof(verb), verb, "no label for the verb"),
    };

    /// <summary>The rows for one (surface, target), in mac's order
    /// (<c>CanvasOutlineView.swift</c>'s menu, IG2-29). Every row is
    /// LIVE: the last staged reason retired with the grid's Toggle Mark
    /// (G2-12). A group's removal is Ungroup (ED-3 — the algebra's one
    /// group removal, no button promising more); the grid has no
    /// connection rows, so a connection target there yields none.</summary>
    internal static ImmutableArray<CanvasContextMenuRow> RowsFor(
        CanvasContextSurface surface, CanvasContextTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        return (surface, target) switch
        {
            (CanvasContextSurface.Outline, CanvasContextTarget.Node node) => OutlineNodeRows(node),
            (CanvasContextSurface.Outline, CanvasContextTarget.Connection) => ConnectionRows(),
            (CanvasContextSurface.Grid, CanvasContextTarget.Node node) => GridNodeRows(node),
            (CanvasContextSurface.Grid, CanvasContextTarget.Connection) => [],
            _ => throw new ArgumentOutOfRangeException(nameof(surface), surface, "no such surface"),
        };
    }

    private static ImmutableArray<CanvasContextMenuRow> OutlineNodeRows(CanvasContextTarget.Node node)
    {
        bool group = node.Kind == "group";
        ImmutableArray<CanvasContextMenuRow>.Builder rows =
            ImmutableArray.CreateBuilder<CanvasContextMenuRow>();
        rows.Add(Live(CanvasContextVerb.Open));
        if (node.Kind == "text")
        {
            rows.Add(Live(CanvasContextVerb.EditCard));
            rows.Add(Live(CanvasContextVerb.ConvertToNote));
        }
        rows.Add(Live(CanvasContextVerb.CreateConnectedCard));
        rows.Add(Live(CanvasContextVerb.Duplicate));
        if (group)
        {
            rows.Add(Live(CanvasContextVerb.RenameGroup));
        }
        if (node.Kind is "file" or "image")
        {
            rows.Add(Live(CanvasContextVerb.LocateFile));
        }
        rows.Add(Live(CanvasContextVerb.ToggleMark));
        rows.Add(Live(CanvasContextVerb.ConnectTo));
        // Set Color on EVERY kind, groups included (G2-12 — mac's menu
        // offers it to a group row too).
        rows.Add(Live(CanvasContextVerb.SetColor));
        rows.Add(Live(CanvasContextVerb.MoveIntoGroup));
        if (node.InGroup)
        {
            rows.Add(Live(CanvasContextVerb.RemoveFromGroup));
        }
        rows.Add(Live(group ? CanvasContextVerb.Ungroup : CanvasContextVerb.Delete));
        return rows.ToImmutable();
    }

    private static ImmutableArray<CanvasContextMenuRow> ConnectionRows() =>
    [
        Live(CanvasContextVerb.JumpToCard),
        Live(CanvasContextVerb.EditConnection),
        Live(CanvasContextVerb.DeleteConnection),
    ];

    private static ImmutableArray<CanvasContextMenuRow> GridNodeRows(CanvasContextTarget.Node node) =>
    [
        Live(CanvasContextVerb.Open),
        Live(CanvasContextVerb.ToggleMark),
        Live(node.Kind == "group" ? CanvasContextVerb.Ungroup : CanvasContextVerb.Delete),
    ];

    private static CanvasContextMenuRow Live(CanvasContextVerb verb) =>
        new(Label(verb), true, null, verb);
}

/// <summary>
/// §G2 TG2-7 (G2-12, IG2-60): the ONE static mapping from a planned
/// verb onto the document's verb, for EVERY context consumer. A node
/// target's verb seats its row silently first (TG-0's rule) unless the
/// verb names its row itself; a connection target seats its SOURCE
/// silently — the edge is selected-relative — and acts on the captured
/// edge directly, never the other endpoint, never a picker.
/// </summary>
internal static class CanvasContextDispatch
{
    /// <param name="activate">The surface's own activation of the row —
    /// the same path Enter runs — for the Open row.</param>
    internal static void Execute(
        CanvasDocumentViewModel model,
        CanvasContextTarget target,
        CanvasContextVerb verb,
        object? owner,
        Action activate)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(activate);
        switch (target)
        {
            case CanvasContextTarget.Node node:
                ExecuteOnNode(model, node, verb, owner, activate);
                break;
            case CanvasContextTarget.Connection connection:
                ExecuteOnConnection(model, connection, verb, owner);
                break;
            default:
                break;
        }
    }

    private static void ExecuteOnNode(
        CanvasDocumentViewModel model,
        CanvasContextTarget.Node node,
        CanvasContextVerb verb,
        object? owner,
        Action activate)
    {
        string nodeId = node.NodeId;
        switch (verb)
        {
            // The verbs that name their row themselves.
            case CanvasContextVerb.Open:
                activate();
                return;
            case CanvasContextVerb.EditCard:
                model.RequestCardEditor(nodeId);
                return;
            case CanvasContextVerb.RenameGroup:
                model.RequestGroupRename(nodeId);
                return;
            case CanvasContextVerb.Ungroup:
                model.CanvasUngroup(nodeId);
                return;
            default:
                break;
        }
        // Every selection verb: seat the row SILENTLY, then the verb
        // (§G TG-0 G1, IG-25; §G2 IG2-60).
        model.SeatSelectionSilently(nodeId);
        switch (verb)
        {
            case CanvasContextVerb.ToggleMark:
                model.ToggleMark();
                break;
            case CanvasContextVerb.ConvertToNote:
                model.RequestConvertToNote(owner);
                break;
            case CanvasContextVerb.CreateConnectedCard:
                _ = model.CanvasCreateConnectedCard(owner: owner);
                break;
            case CanvasContextVerb.Duplicate:
                _ = model.CanvasDuplicate(owner);
                break;
            case CanvasContextVerb.LocateFile:
                model.RequestVaultPick(CanvasVaultPickPurpose.Locate, owner);
                break;
            case CanvasContextVerb.ConnectTo:
                model.OpenCardPicker(CanvasCardPickerPurpose.ConnectTo);
                break;
            case CanvasContextVerb.SetColor:
                model.RequestSetColor();
                break;
            case CanvasContextVerb.MoveIntoGroup:
                model.RequestMoveIntoGroup(owner);
                break;
            case CanvasContextVerb.RemoveFromGroup:
                _ = model.CanvasRemoveFromGroup(owner);
                break;
            case CanvasContextVerb.Delete:
                model.CanvasDeleteSelection();
                break;
            default:
                // A connection verb never reaches a node target: the plan
                // does not place one there.
                break;
        }
    }

    private static void ExecuteOnConnection(
        CanvasDocumentViewModel model,
        CanvasContextTarget.Connection connection,
        CanvasContextVerb verb,
        object? owner)
    {
        // The edge is selected-relative: its SOURCE is the seat — a
        // no-op when the source already hosts the connection rows —
        // never the other endpoint.
        model.SeatSelectionSilently(connection.SourceNodeId);
        switch (verb)
        {
            case CanvasContextVerb.JumpToCard:
                model.FollowConnection(connection.Neighbor);
                break;
            case CanvasContextVerb.EditConnection:
                model.RequestEditConnection(connection.Neighbor, owner);
                break;
            case CanvasContextVerb.DeleteConnection:
                _ = model.CanvasDeleteConnection(connection.Neighbor, owner);
                break;
            default:
                break;
        }
    }
}
