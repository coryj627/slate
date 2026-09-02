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
internal abstract class CanvasPromptViewModel
{
    private protected CanvasPromptViewModel(
        CanvasDocumentViewModel document,
        string title,
        string draft,
        ImmutableArray<CanvasPromptChoice> choices)
    {
        Document = document;
        Title = title;
        Draft = draft;
        Choices = choices;
        SelectedChoice = choices.IsEmpty ? null : choices[0];
    }

    private protected CanvasDocumentViewModel Document { get; }

    public string Title { get; }

    public string Draft { get; set; }

    public bool HasTextField => Choices.IsEmpty;

    public ImmutableArray<CanvasPromptChoice> Choices { get; }

    public CanvasPromptChoice? SelectedChoice { get; set; }

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
        new CanvasSetColorPrompt(document);
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
    internal CanvasSetColorPrompt(CanvasDocumentViewModel document)
        : base(document, "Set Color", string.Empty, BuildChoices())
    {
    }

    internal override CanvasPromptSubmit Submit(Action onLanded)
    {
        ArgumentNullException.ThrowIfNull(onLanded);
        CanvasMutationOperation? operation = Document.CanvasSetColor(
            SelectedChoice?.Value,
            completion: outcome => { if (Landed(outcome)) { onLanded(); } });
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
