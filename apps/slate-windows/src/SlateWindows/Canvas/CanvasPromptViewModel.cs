// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Immutable;
using uniffi.slate_uniffi;

namespace SlateWindows.Canvas;

/// <summary>§G TG-1 (IG-38): what a submit ANSWERED — the sheet's
/// closure follows the result, never the keypress. Refused keeps the
/// sheet and its draft (the verb spoke); Pending keeps it until the
/// named operation LANDS; Completed closes now.</summary>
internal enum CanvasPromptSubmit
{
    Refused,
    Pending,
    Completed,
}

/// <summary>One choice row for a choices-shaped prompt.</summary>
internal sealed record CanvasPromptChoice(string? Value, string Name);

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

    internal static CanvasPromptViewModel MarksList(
        CanvasDocumentViewModel document, object owner, Action<CanvasPromptViewModel> closeIfCurrent) =>
        new CanvasMarksListPrompt(document, owner, closeIfCurrent);
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
            GroupId, Draft, completion: outcome => { if (Landed(outcome)) { onLanded(); } });
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
    : System.ComponentModel.INotifyPropertyChanged
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
    internal bool Confirm()
    {
        return SelectedRow is { } row
            && _document.HandleCardPick(Request, row.NodeId);
    }
}
