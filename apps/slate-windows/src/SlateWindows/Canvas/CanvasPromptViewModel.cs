// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Immutable;
using uniffi.slate_uniffi;

namespace SlateWindows.Canvas;

/// <summary>§G TG-1 (IG-38): what a submit ANSWERED — the sheet's
/// closure follows the result, never the keypress. Refused keeps the
/// sheet and its draft (the verb spoke); Pending keeps it until the
/// named operation LANDS; Completed closes now.</summary>
internal abstract record CanvasPromptSubmit
{
    private CanvasPromptSubmit()
    {
    }

    internal static readonly CanvasPromptSubmit Refused = new RefusedAnswer();
    internal static readonly CanvasPromptSubmit Pending = new PendingAnswer();
    internal static readonly CanvasPromptSubmit Completed = new CompletedAnswer();

    private sealed record RefusedAnswer : CanvasPromptSubmit;

    private sealed record PendingAnswer : CanvasPromptSubmit;

    private sealed record CompletedAnswer : CanvasPromptSubmit;

    /// <summary>§G2 TG2-1 (G2-4, IG2-44): a STAGED prompt's answer — the
    /// successor rides in the result itself, never in a side property,
    /// so an Advanced without a successor cannot be represented and a
    /// successor cannot attach to any other answer. The workspace swaps
    /// the sheet atomically on it.</summary>
    internal sealed record Advanced : CanvasPromptSubmit
    {
        internal Advanced(CanvasPromptViewModel next)
        {
            ArgumentNullException.ThrowIfNull(next);
            Next = next;
        }

        internal CanvasPromptViewModel Next { get; }
    }
}

/// <summary>§G2 TG2-1 (IG2-47): a choice carries an optional STATUS —
/// the outline row's ordinal clause for a connection — bound to the
/// item's UIA ItemStatus so parallel same-named rows stay distinct.</summary>
internal sealed record CanvasPromptChoice(string? Value, string Name, string? Status = null);

/// <summary>
/// §G TG-1 (IG-37): the prompt machinery as a SEALED HIERARCHY — one
/// sheet, one modal membership (TF-8), every variant carrying its own
/// payload (the connect stage, the group id, the color target) and
/// its own <see cref="Submit"/> arm. There is no default arm: a
/// variant that cannot submit cannot exist. The bindable surface is
/// the base's, so the XAML sheet is the same for every variant.
/// </summary>
internal abstract class CanvasPromptViewModel : System.ComponentModel.INotifyPropertyChanged
{
    private string _title;
    private ImmutableArray<CanvasPromptChoice> _choices;
    private CanvasPromptChoice? _selectedChoice;

    private protected CanvasPromptViewModel(
        CanvasDocumentViewModel document,
        string title,
        string draft,
        ImmutableArray<CanvasPromptChoice> choices)
    {
        Document = document;
        _title = title;
        Draft = draft;
        _choices = choices;
        _selectedChoice = choices.IsEmpty ? null : choices[0];
    }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    private protected CanvasDocumentViewModel Document { get; }

    public string Title
    {
        get => _title;
        private protected set
        {
            _title = value;
            Raise(nameof(Title));
        }
    }

    public string Draft { get; set; }

    public bool HasTextField => Choices.IsEmpty && !ShowsClearMarks;

    public ImmutableArray<CanvasPromptChoice> Choices
    {
        get => _choices;
        private protected set
        {
            _choices = value;
            Raise(nameof(Choices));
            Raise(nameof(HasRows));
        }
    }

    public CanvasPromptChoice? SelectedChoice
    {
        get => _selectedChoice;
        set
        {
            _selectedChoice = value;
            Raise(nameof(SelectedChoice));
        }
    }

    /// <summary>§G TG-2 (IG-43): whether the sheet offers the Clear
    /// All Marks control — the marks list only.</summary>
    public virtual bool ShowsClearMarks => false;

    /// <summary>§G TG-2 (IG-43): whether the rows list can take focus
    /// — false for a zero-row marks list, so the Clear control is the
    /// first focusable in the dialog.</summary>
    public bool HasRows => !Choices.IsEmpty;

    /// <summary>The workspace's one hook when the sheet stops being
    /// current, however it closed — a live variant unsubscribes.</summary>
    internal virtual void Closed()
    {
    }

    private protected void Raise(string name) =>
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));

    /// <summary>Enter's arm: route the answer to the shipped verb and
    /// ANSWER what happened. A Pending answer names its operation
    /// through <paramref name="onLanded"/>, which the workspace runs
    /// — marshalled home — when that exact operation lands, to close
    /// this exact sheet if it is still the current one.</summary>
    internal abstract CanvasPromptSubmit Submit(Action onLanded);

    /// <summary>The landing arms of a funnel completion — the write
    /// LANDED (installed, or committed-unpresented) — the only arms
    /// that may close a sheet (G5's table).</summary>
    private protected static bool Landed(CanvasOperationOutcome outcome) =>
        outcome is CanvasOperationOutcome.Installed
            or CanvasOperationOutcome.Unindexed
            or CanvasOperationOutcome.RefreshRefused;

    internal static CanvasPromptViewModel ConnectLabel(
        CanvasDocumentViewModel document, CanvasConnectStage stage) =>
        new CanvasConnectLabelPrompt(document, stage);

    internal static CanvasPromptViewModel RenameGroup(
        CanvasDocumentViewModel document, string groupId, string current) =>
        new CanvasRenameGroupPrompt(document, groupId, current);

    internal static CanvasPromptViewModel SetColor(CanvasDocumentViewModel document) =>
        new CanvasSetColorPrompt(document, marked: false);

    /// <summary>§G TG-4 (GD-5, IG-55): the same prompt over the MARKED
    /// target — its title carries NO count (the submit's snapshot is
    /// the truth; a title count would be a second, stale reading).</summary>
    internal static CanvasPromptViewModel SetColorMarked(CanvasDocumentViewModel document) =>
        new CanvasSetColorPrompt(document, marked: true);

    internal static CanvasPromptViewModel GroupMarked(CanvasDocumentViewModel document) =>
        new CanvasGroupMarkedPrompt(document);

    internal static CanvasPromptViewModel MarksList(
        CanvasDocumentViewModel document, object owner, Action<CanvasPromptViewModel> closeIfCurrent) =>
        new CanvasMarksListPrompt(document, owner, closeIfCurrent);

    internal static CanvasPromptViewModel NewGroup(
        CanvasDocumentViewModel document, CanvasPromptContext context) =>
        new CanvasNewGroupPrompt(document, context);

    internal static CanvasPromptViewModel AddLink(
        CanvasDocumentViewModel document, CanvasPromptContext context) =>
        new CanvasAddLinkPrompt(document, context);

    internal static CanvasPromptViewModel MoveIntoGroup(
        CanvasDocumentViewModel document, CanvasPromptContext context,
        IReadOnlyList<CanvasOutlineRow> groups) =>
        new CanvasMoveIntoGroupPrompt(document, context, groups);

    internal static CanvasPromptViewModel PickConnection(
        CanvasDocumentViewModel document, CanvasPromptContext context,
        IReadOnlyList<CanvasNeighbor> neighbors, bool toDelete) =>
        new CanvasPickConnectionPrompt(document, context, neighbors, toDelete);

    internal static CanvasPromptViewModel EditConnectionDirection(
        CanvasDocumentViewModel document, CanvasPromptContext context, CanvasNeighbor neighbor) =>
        CanvasEditConnectionDirectionPrompt.For(document, context, neighbor);

    internal static CanvasPromptViewModel UngroupConfirm(
        CanvasDocumentViewModel document, string groupId, string title) =>
        new CanvasUngroupConfirmPrompt(document, groupId, title);

    /// <summary>§G2 TG2-1 (G2D-11): mac's label rule — the EMPTY draft is
    /// a null label (spoken "Untitled"); whitespace is kept, untrimmed.</summary>
    internal static string? NormalizeLabel(string draft) => draft.Length == 0 ? null : draft;
}

/// <summary>
/// §G TG-2 (G4): the marks list — the picker's sibling on the prompt
/// sheet, a currency-bound LIVE projection: rows are the projected
/// marked set in reading order, keyed by node id and named the way
/// every Windows projection names a card, reprojected on every
/// publication; the active row survives by id and a removed active
/// row's successor is the next at the same ordinal, else the previous.
/// Enter JUMPS: the sheet closes FIRST and the A14 landing posts
/// after, addressed to the owner captured at open; a Visual owner is
/// switched to the outline; a filtered-out row clears the filter
/// with the filter line fired NOW as the one arbiter. Delete UNMARKS
/// the active row (spoken); the sheet closes when the STORE empties,
/// however it emptied.
/// </summary>
internal sealed class CanvasMarksListPrompt : CanvasPromptViewModel
{
    private readonly object _owner;
    private readonly Action<CanvasPromptViewModel> _closeIfCurrent;

    internal CanvasMarksListPrompt(
        CanvasDocumentViewModel document, object owner, Action<CanvasPromptViewModel> closeIfCurrent)
        : base(document, "Marked Cards", string.Empty, [])
    {
        _owner = owner;
        _closeIfCurrent = closeIfCurrent;
        Reproject();
        document.PublicationApplied += OnPublicationApplied;
    }

    public override bool ShowsClearMarks => true;

    internal object Owner => _owner;

    internal override void Closed() => Document.PublicationApplied -= OnPublicationApplied;

    /// <summary>Enter: JUMP. Close first, land after (IG-39): the
    /// selection seats silently now; the A14 request posts at
    /// background priority so it runs after the workspace has cleared
    /// this sheet; the Visual owner switches to the outline first
    /// (IG-40); a filtered-out row clears the filter and fires its
    /// line now (IG-42). A14's own outcomes — delivered, pending,
    /// dropped — are the landing's, not this sheet's (IG-41).</summary>
    internal override CanvasPromptSubmit Submit(Action onLanded)
    {
        ArgumentNullException.ThrowIfNull(onLanded);
        if (SelectedChoice?.Value is not { } nodeId)
        {
            return CanvasPromptSubmit.Refused;
        }
        Document.SeatSelectionSilently(nodeId);
        if (Document.Selection.ActiveSurface == CanvasSurfaceKind.Visual)
        {
            Document.ShowSurface(CanvasSurfaceKind.Outline);
        }
        if (Document.FilterActive && Document.FilteredOutline.All(row => row.NodeId != nodeId))
        {
            Document.Navigator.ClearFilter();
            Document.FireFilterLineNow();
        }
        object owner = _owner;
        CanvasDocumentViewModel document = Document;
        _ = System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.Background,
            () => document.RequestFocusLanding(owner, nodeId));
        return CanvasPromptSubmit.Completed;
    }

    /// <summary>Delete: unmark the active row through the document's
    /// idempotent verb (spoken); the successor takes the highlight,
    /// and the store-empty close rides the reprojection.</summary>
    internal void UnmarkActive()
    {
        if (SelectedChoice?.Value is { } nodeId)
        {
            _ = Document.Unmark(nodeId);
        }
    }

    private void OnPublicationApplied(CanvasPublication _) => Reproject();

    private void Reproject()
    {
        if (Document.AppliedPublication is { MarkedIntent.Count: 0 })
        {
            _closeIfCurrent(this);
            return;
        }
        string? active = SelectedChoice?.Value;
        int ordinal = Choices.IsEmpty || active is null
            ? 0
            : Math.Max(0, Choices.ToList().FindIndex(c => c.Value == active));
        ImmutableArray<CanvasPromptChoice> rows = Document.ProjectMarkedRows() is { } projected
            ? [.. projected.Select(row => new CanvasPromptChoice(
                row.NodeId,
                CanvasPhrase.CardReference(row.Kind, row.SpeakableName) + ", marked"))]
            : Choices;
        Choices = rows;
        Title = "Marked Cards (" + rows.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + ")";
        if (rows.IsEmpty)
        {
            SelectedChoice = null;
            return;
        }
        CanvasPromptChoice? kept = rows.FirstOrDefault(c => c.Value == active);
        SelectedChoice = kept ?? rows[Math.Min(ordinal, rows.Length - 1)];
    }
}

/// <summary>§F TF-8 / §G TG-1: the label step of Connect To… — the
/// immutable stage is the payload; Enter with an empty field skips
/// (the verb normalizes empty to null).</summary>
internal sealed class CanvasConnectLabelPrompt : CanvasPromptViewModel
{
    internal CanvasConnectLabelPrompt(CanvasDocumentViewModel document, CanvasConnectStage stage)
        : base(document, "Connect to " + Quote(stage.TargetTitle), string.Empty, [])
    {
        Stage = stage;
    }

    internal CanvasConnectStage Stage { get; }

    internal override CanvasPromptSubmit Submit(Action onLanded)
    {
        ArgumentNullException.ThrowIfNull(onLanded);
        CanvasMutationOperation? operation = Document.CanvasConnect(
            Stage, Draft, completion: outcome => { if (Landed(outcome)) { onLanded(); } });
        return operation is null ? CanvasPromptSubmit.Refused : CanvasPromptSubmit.Pending;
    }

    private static string Quote(string title) => "\u0022" + title + "\u0022";
}

/// <summary>§G TG-5 (G5): the Group Marked Cards label step — a text
/// kind; Enter with an empty field means an unlabeled group (the
/// verb normalizes). The marked set is captured at SUBMIT.</summary>
internal sealed class CanvasGroupMarkedPrompt : CanvasPromptViewModel
{
    internal CanvasGroupMarkedPrompt(CanvasDocumentViewModel document)
        : base(document, "Group Marked Cards", string.Empty, [])
    {
    }

    internal override CanvasPromptSubmit Submit(Action onLanded)
    {
        ArgumentNullException.ThrowIfNull(onLanded);
        CanvasMutationOperation? operation = Document.SubmitGroupMarked(
            Draft, completion: outcome => { if (Landed(outcome)) { onLanded(); } });
        return operation is null ? CanvasPromptSubmit.Refused : CanvasPromptSubmit.Pending;
    }
}

/// <summary>The carried Rename Group prompt (FD-4): the group id is
/// the payload, the draft seeds from the current title.</summary>
internal sealed class CanvasRenameGroupPrompt : CanvasPromptViewModel
{
    internal CanvasRenameGroupPrompt(
        CanvasDocumentViewModel document, string groupId, string current)
        : base(document, "Rename Group", current, [])
    {
        GroupId = groupId;
    }

    internal string GroupId { get; }

    internal override CanvasPromptSubmit Submit(Action onLanded)
    {
        ArgumentNullException.ThrowIfNull(onLanded);
        CanvasMutationOperation? operation = Document.CanvasRenameGroup(
            GroupId, NormalizeLabel(Draft), completion: outcome => { if (Landed(outcome)) { onLanded(); } });
        return operation is null ? CanvasPromptSubmit.Refused : CanvasPromptSubmit.Pending;
    }
}

/// <summary>§G2 TG2-1 (G2-2, G2D-11): New Group… — a text kind whose
/// submit runs the shipped verb with mac's label rule (empty is a null
/// label, untrimmed) and the invoking tab as the operation's owner.</summary>
internal sealed class CanvasNewGroupPrompt : CanvasPromptViewModel
{
    internal CanvasNewGroupPrompt(CanvasDocumentViewModel document, CanvasPromptContext context)
        : base(document, "New Group", string.Empty, [])
    {
        Context = context;
    }

    internal CanvasPromptContext Context { get; }

    internal override CanvasPromptSubmit Submit(Action onLanded)
    {
        ArgumentNullException.ThrowIfNull(onLanded);
        if (!Document.IsCurrentLoaded(Context.Identity))
        {
            Document.SpeakPickDifferentTarget();
            return CanvasPromptSubmit.Refused;
        }
        CanvasMutationOperation? operation = Document.CanvasNewGroup(
            NormalizeLabel(Draft), owner: Context.Owner,
            completion: outcome => { if (Landed(outcome)) { onLanded(); } });
        return operation is null ? CanvasPromptSubmit.Refused : CanvasPromptSubmit.Pending;
    }
}

/// <summary>§G2 TG2-1 (G2-5, G2D-10): Add Link Card… — a text kind whose
/// submit TRIMS the draft (mac's rule) and runs the shipped verb; the
/// verb's NotAUrl arm is the only validation and answers Refused with
/// the draft kept.</summary>
internal sealed class CanvasAddLinkPrompt : CanvasPromptViewModel
{
    internal CanvasAddLinkPrompt(CanvasDocumentViewModel document, CanvasPromptContext context)
        : base(document, "Add Link Card", string.Empty, [])
    {
        Context = context;
    }

    internal CanvasPromptContext Context { get; }

    internal override CanvasPromptSubmit Submit(Action onLanded)
    {
        ArgumentNullException.ThrowIfNull(onLanded);
        if (!Document.IsCurrentLoaded(Context.Identity))
        {
            Document.SpeakPickDifferentTarget();
            return CanvasPromptSubmit.Refused;
        }
        CanvasMutationOperation? operation = Document.CanvasAddLinkCard(
            Draft.Trim(), owner: Context.Owner,
            completion: outcome => { if (Landed(outcome)) { onLanded(); } });
        return operation is null ? CanvasPromptSubmit.Refused : CanvasPromptSubmit.Pending;
    }
}

/// <summary>§G2 TG2-1 (G2-7, IG2-51): E8's and ED-3's Ungroup-or-Cancel
/// confirmation for a group's DELETE — two choices, Ungroup first;
/// Ungroup runs the algebra's one group removal (the cards stay),
/// Cancel changes nothing. Either answer closes the sheet now: the
/// verb speaks its own sentence and the sheet has nothing to wait
/// for.</summary>
internal sealed class CanvasUngroupConfirmPrompt : CanvasPromptViewModel
{
    internal CanvasUngroupConfirmPrompt(CanvasDocumentViewModel document, string groupId, string title)
        : base(
            document,
            "Delete Group " + Quote(title) + "?",
            string.Empty,
            [new CanvasPromptChoice("ungroup", "Ungroup — remove the frame, keep its cards"), new CanvasPromptChoice(null, "Cancel")])
    {
        GroupId = groupId;
    }

    internal string GroupId { get; }

    internal override CanvasPromptSubmit Submit(Action onLanded)
    {
        ArgumentNullException.ThrowIfNull(onLanded);
        if (SelectedChoice?.Value == "ungroup")
        {
            Document.CanvasUngroup(GroupId);
        }
        return CanvasPromptSubmit.Completed;
    }

    private static string Quote(string title) => "\u0022" + title + "\u0022";
}

/// <summary>§G2 TG2-2 (G2-2, IG2-35, IG2-19): the typed, immutable
/// context a prompt-backed flow captures at OPEN — the invoking owner
/// (TE-1's initiating surface), the exact loaded identity, and the
/// source node the verb acts on (null for the verbs that need none).
/// A submit against another identity is a guess and refuses
/// <c>PickDifferentTarget</c> with the sheet kept.</summary>
internal sealed record CanvasPromptContext(
    object? Owner,
    CanvasLoaded Identity,
    string? SourceNode);

/// <summary>§G2 TG2-2 (G2-5): Move into Group… — choices over EVERY
/// group in reading order (mac includes the current one; a move into
/// it re-places inside), named the way every projection names a card.
/// Enter runs the shipped verb; Pending closes when it lands.</summary>
internal sealed class CanvasMoveIntoGroupPrompt : CanvasPromptViewModel
{
    internal CanvasMoveIntoGroupPrompt(
        CanvasDocumentViewModel document,
        CanvasPromptContext context,
        IReadOnlyList<CanvasOutlineRow> groups)
        : base(
            document,
            "Move into Group",
            string.Empty,
            [.. groups.Select(g => new CanvasPromptChoice(
                g.NodeId, CanvasPhrase.CardReference(g.Kind, g.SpeakableName)))])
    {
        Context = context;
    }

    internal CanvasPromptContext Context { get; }

    internal override CanvasPromptSubmit Submit(Action onLanded)
    {
        ArgumentNullException.ThrowIfNull(onLanded);
        if (!Document.IsCurrentLoaded(Context.Identity))
        {
            Document.SpeakPickDifferentTarget();
            return CanvasPromptSubmit.Refused;
        }
        if (SelectedChoice?.Value is not { } groupId)
        {
            return CanvasPromptSubmit.Refused;
        }
        CanvasMutationOperation? operation = Document.CanvasMoveIntoGroup(
            groupId, owner: Context.Owner,
            completion: outcome => { if (Landed(outcome)) { onLanded(); } });
        return operation is null ? CanvasPromptSubmit.Refused : CanvasPromptSubmit.Pending;
    }
}

/// <summary>§G2 TG2-2 (G2-5, IG2-11/13/14/46): the connection picker for
/// Delete Connection… and Edit Connection… — reached only with MANY
/// connections (mac routes one directly). Rows are keyed by edge and
/// named by the SAME pure render the outline's connection rows carry,
/// with the ordinal clause as the row's status. Enter re-validates the
/// edge is still the source card's neighbor, then deletes from the
/// selected-relative neighbor, or ADVANCES to the direction stage.</summary>
internal sealed class CanvasPickConnectionPrompt : CanvasPromptViewModel
{
    private readonly IReadOnlyList<CanvasNeighbor> _neighbors;

    internal CanvasPickConnectionPrompt(
        CanvasDocumentViewModel document,
        CanvasPromptContext context,
        IReadOnlyList<CanvasNeighbor> neighbors,
        bool toDelete)
        : base(
            document,
            toDelete ? "Delete Connection" : "Edit Connection",
            string.Empty,
            [.. neighbors.Select((n, i) => new CanvasPromptChoice(
                n.EdgeId,
                document.ConnectionRowName(n),
                CanvasPhrase.ConnectionStatus(i + 1, neighbors.Count)))])
    {
        Context = context;
        _neighbors = neighbors;
        ToDelete = toDelete;
    }

    internal CanvasPromptContext Context { get; }

    internal bool ToDelete { get; }

    internal override CanvasPromptSubmit Submit(Action onLanded)
    {
        ArgumentNullException.ThrowIfNull(onLanded);
        if (!Document.IsCurrentLoaded(Context.Identity))
        {
            Document.SpeakPickDifferentTarget();
            return CanvasPromptSubmit.Refused;
        }
        if (SelectedChoice?.Value is not { } edgeId
            || Context.SourceNode is not { } source
            || Document.NeighborsIfKnown(source)?.FirstOrDefault(n => n.EdgeId == edgeId)
                is not { } neighbor)
        {
            Document.SpeakNoConnections();
            return CanvasPromptSubmit.Refused;
        }
        if (ToDelete)
        {
            CanvasMutationOperation? operation = Document.CanvasDeleteConnection(
                neighbor, owner: Context.Owner,
                completion: outcome => { if (Landed(outcome)) { onLanded(); } });
            return operation is null ? CanvasPromptSubmit.Refused : CanvasPromptSubmit.Pending;
        }
        return new CanvasPromptSubmit.Advanced(
            CanvasEditConnectionDirectionPrompt.For(Document, Context, neighbor));
    }
}

/// <summary>§G2 TG2-2 (G2-5, G2D-2, IG2-12): Edit Connection's FIRST stage
/// — the direction, four choices with mac's labels phrased by meaning,
/// the current one preselected from the edge's two arrow flags. Enter
/// ADVANCES to the label stage; the write happens there, once.</summary>
internal sealed class CanvasEditConnectionDirectionPrompt : CanvasPromptViewModel
{
    private CanvasEditConnectionDirectionPrompt(
        CanvasDocumentViewModel document,
        CanvasPromptContext context,
        CanvasNeighbor neighbor,
        string? currentLabel,
        CanvasConnectionDirection current)
        : base(document, "Edit Connection: Direction", string.Empty, Choices())
    {
        Context = context;
        Neighbor = neighbor;
        CurrentLabel = currentLabel;
        SelectedChoice = base.Choices.First(c => c.Value == current.ToString());
    }

    internal CanvasPromptContext Context { get; }

    internal CanvasNeighbor Neighbor { get; }

    internal string? CurrentLabel { get; }

    /// <summary>The edge's arrow flags read in EDGE coordinates (from
    /// node to node), which is the frame the verb's four arms and mac's
    /// radio share: an arrow at the to-end alone is "Points at the
    /// target"; at the from-end alone "Points back at the source".</summary>
    internal static CanvasEditConnectionDirectionPrompt For(
        CanvasDocumentViewModel document, CanvasPromptContext context, CanvasNeighbor neighbor)
    {
        CanvasSceneEdge? edge = document.SceneEdgeFor(neighbor.EdgeId);
        CanvasConnectionDirection current = (edge?.FromArrow ?? false, edge?.ToArrow ?? true) switch
        {
            (false, true) => CanvasConnectionDirection.ToTarget,
            (true, false) => CanvasConnectionDirection.FromTarget,
            (true, true) => CanvasConnectionDirection.Both,
            _ => CanvasConnectionDirection.None,
        };
        return new CanvasEditConnectionDirectionPrompt(
            document, context, neighbor, edge?.Label, current);
    }

    internal override CanvasPromptSubmit Submit(Action onLanded)
    {
        ArgumentNullException.ThrowIfNull(onLanded);
        if (SelectedChoice?.Value is not { } chosen)
        {
            return CanvasPromptSubmit.Refused;
        }
        return new CanvasPromptSubmit.Advanced(
            new CanvasEditConnectionLabelPrompt(
                Document, Context, Neighbor, Enum.Parse<CanvasConnectionDirection>(chosen), CurrentLabel));
    }

    private static ImmutableArray<CanvasPromptChoice> Choices() =>
    [
        new(nameof(CanvasConnectionDirection.ToTarget), "Points at the target"),
        new(nameof(CanvasConnectionDirection.FromTarget), "Points back at the source"),
        new(nameof(CanvasConnectionDirection.Both), "Both directions"),
        new(nameof(CanvasConnectionDirection.None), "No direction"),
    ];
}

/// <summary>§G2 TG2-2 (G2-5): Edit Connection's SECOND stage — the label,
/// prefilled with the current one; an empty draft is a null label.
/// Enter runs the shipped verb ONCE with the chosen direction, sides
/// and color preserved by the verb; Pending closes when it lands.</summary>
internal sealed class CanvasEditConnectionLabelPrompt : CanvasPromptViewModel
{
    internal CanvasEditConnectionLabelPrompt(
        CanvasDocumentViewModel document,
        CanvasPromptContext context,
        CanvasNeighbor neighbor,
        CanvasConnectionDirection direction,
        string? currentLabel)
        : base(document, "Edit Connection: Label", currentLabel ?? string.Empty, [])
    {
        Context = context;
        Neighbor = neighbor;
        Direction = direction;
    }

    internal CanvasPromptContext Context { get; }

    internal CanvasNeighbor Neighbor { get; }

    internal CanvasConnectionDirection Direction { get; }

    internal override CanvasPromptSubmit Submit(Action onLanded)
    {
        ArgumentNullException.ThrowIfNull(onLanded);
        if (!Document.IsCurrentLoaded(Context.Identity))
        {
            Document.SpeakPickDifferentTarget();
            return CanvasPromptSubmit.Refused;
        }
        CanvasMutationOperation? operation = Document.CanvasEditConnection(
            Neighbor.EdgeId, NormalizeLabel(Draft), Direction, owner: Context.Owner,
            completion: outcome => { if (Landed(outcome)) { onLanded(); } });
        return operation is null ? CanvasPromptSubmit.Refused : CanvasPromptSubmit.Pending;
    }
}

/// <summary>The carried Set Color prompt (FD-4) over the SELECTION:
/// choices are core's names, never a host copy. The Marked target
/// joins with its bulk verb (TG-4).</summary>
internal sealed class CanvasSetColorPrompt : CanvasPromptViewModel
{
    internal CanvasSetColorPrompt(CanvasDocumentViewModel document, bool marked)
        : base(document, marked ? "Set Color for Marked Cards" : "Set Color", string.Empty, BuildChoices())
    {
        Marked = marked;
    }

    /// <summary>The target: the selection, or the marked set captured
    /// at SUBMIT (live-at-submit, immutable once submitted).</summary>
    internal bool Marked { get; }

    internal override CanvasPromptSubmit Submit(Action onLanded)
    {
        ArgumentNullException.ThrowIfNull(onLanded);
        string? value = SelectedChoice?.Value;
        void Landing(CanvasOperationOutcome outcome)
        {
            if (Landed(outcome))
            {
                onLanded();
            }
        }
        CanvasMutationOperation? operation = Marked
            ? Document.SubmitBulkMarked(
                "color marked",
                CanvasMarkEffect.Keep,
                "color",
                (id, _) => new CanvasOp.SetNodeColor(id, value),
                count => new CanvasA11yEvent.CanvasBulkColorSet(
                    (uint)count,
                    value is null ? null : new CanvasColor.Preset(byte.Parse(value))),
                completion: Landing)
            : Document.CanvasSetColor(value, completion: Landing);
        return operation is null ? CanvasPromptSubmit.Refused : CanvasPromptSubmit.Pending;
    }

    private static ImmutableArray<CanvasPromptChoice> BuildChoices()
    {
        ImmutableArray<CanvasPromptChoice>.Builder choices =
            ImmutableArray.CreateBuilder<CanvasPromptChoice>();
        for (byte preset = 1; preset <= 6; preset++)
        {
            choices.Add(new CanvasPromptChoice(
                preset.ToString(System.Globalization.CultureInfo.InvariantCulture),
                SlateUniffiMethods.CanvasColorName(new CanvasColor.Preset(preset))));
        }
        choices.Add(new CanvasPromptChoice(null, "No color"));
        return choices.ToImmutable();
    }
}

/// <summary>
/// §F TF-8 (F5): the card-picker sheet — the TF-7 request plus the
/// model, filter-only (the order is core's, preserved verbatim). A
/// refused pick keeps the sheet, its filter and its highlight; a
/// ROUTED pick closes it.
/// </summary>
internal sealed class CanvasCardPickerViewModel
    : System.ComponentModel.INotifyPropertyChanged, ICanvasPickerSheet
{
    private readonly CanvasDocumentViewModel _document;
    private string _filter = string.Empty;

    internal CanvasCardPickerViewModel(
        CanvasDocumentViewModel document,
        CanvasCardPickerRequest request,
        CanvasCardPickerModel model)
    {
        _document = document;
        Request = request;
        Model = model;
        Rows = model.Visible(string.Empty);
        SelectedRow = Rows.IsEmpty ? null : Rows[0];
    }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    internal CanvasCardPickerRequest Request { get; }

    internal CanvasCardPickerModel Model { get; }

    public string Title => Request.Purpose switch
    {
        CanvasCardPickerPurpose.PlaceBelow => "Place Below…",
        CanvasCardPickerPurpose.PlaceRightOf => "Place Right Of…",
        CanvasCardPickerPurpose.PlaceAbove => "Place Above…",
        CanvasCardPickerPurpose.PlaceLeftOf => "Place Left Of…",
        CanvasCardPickerPurpose.AlignWith => "Align With…",
        _ => "Connect To…",
    };

    /// <summary>§G2 TG2-3 (IG2-48): the sheet-level UIA names are the
    /// purpose's — the card picker's, here.</summary>
    public string SheetName => "Card picker";

    public string FilterName => "Filter cards";

    public string RowsName => "Cards, nearest first. Enter picks.";

    public string Filter
    {
        get => _filter;
        set
        {
            _filter = value ?? string.Empty;
            Rows = Model.Visible(_filter);
            if (SelectedRow is null || !Rows.Contains(SelectedRow))
            {
                SelectedRow = Rows.IsEmpty ? null : Rows[0];
            }
            PropertyChanged?.Invoke(
                this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Rows)));
            PropertyChanged?.Invoke(
                this,
                new System.ComponentModel.PropertyChangedEventArgs(nameof(SelectedRow)));
        }
    }

    public System.Collections.Immutable.ImmutableArray<CanvasCardPickerRow> Rows
    {
        get;
        private set;
    }

    public CanvasCardPickerRow? SelectedRow { get; set; }

    /// <summary>True when the pick ROUTED (a verb ran or a stage's
    /// prompt opened) — the sheet closes; false keeps it, filter and
    /// highlight intact (F5's state-keeping refusals).</summary>
    public bool Confirm()
    {
        return SelectedRow is { } row
            && _document.HandleCardPick(Request, row.NodeId);
    }
}
