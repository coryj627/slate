// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Immutable;
using uniffi.slate_uniffi;

namespace SlateWindows.Canvas;

/// <summary>§F TF-8: what a canvas prompt is for.</summary>
internal enum CanvasPromptKind
{
    ConnectLabel,
    RenameGroup,
    SetColor,
}

/// <summary>One choice row for a choices-shaped prompt.</summary>
internal sealed record CanvasPromptChoice(string? Value, string Name);

/// <summary>
/// §F TF-8 (FD-4): the prompt machinery — ONE sheet for the label
/// step, the carried Rename Group, and the carried Set Color. Text
/// prompts submit their draft; the color prompt submits a CHOICE
/// (named buttons-as-rows: color is never color-alone, and the names
/// are core's `CanvasColorName`, never a host copy). The document
/// verbs own every refusal; Escape dismisses committing nothing.
/// </summary>
internal sealed class CanvasPromptViewModel
{
    private readonly CanvasDocumentViewModel _document;
    private readonly CanvasConnectStage? _stage;
    private readonly string? _groupId;

    private CanvasPromptViewModel(
        CanvasPromptKind kind,
        CanvasDocumentViewModel document,
        CanvasConnectStage? stage,
        string? groupId,
        string title,
        string draft,
        ImmutableArray<CanvasPromptChoice> choices)
    {
        Kind = kind;
        _document = document;
        _stage = stage;
        _groupId = groupId;
        Title = title;
        Draft = draft;
        Choices = choices;
        SelectedChoice = choices.IsEmpty ? null : choices[0];
    }

    internal static CanvasPromptViewModel ConnectLabel(
        CanvasDocumentViewModel document, CanvasConnectStage stage) =>
        new(
            CanvasPromptKind.ConnectLabel,
            document,
            stage,
            null,
            $"Connect to \"{stage.TargetTitle}\"",
            string.Empty,
            []);

    internal static CanvasPromptViewModel RenameGroup(
        CanvasDocumentViewModel document, string groupId, string current) =>
        new(
            CanvasPromptKind.RenameGroup,
            document,
            null,
            groupId,
            "Rename Group",
            current,
            []);

    internal static CanvasPromptViewModel SetColor(
        CanvasDocumentViewModel document)
    {
        ImmutableArray<CanvasPromptChoice>.Builder choices =
            ImmutableArray.CreateBuilder<CanvasPromptChoice>();
        for (byte preset = 1; preset <= 6; preset++)
        {
            choices.Add(new CanvasPromptChoice(
                preset.ToString(System.Globalization.CultureInfo.InvariantCulture),
                SlateUniffiMethods.CanvasColorName(
                    new CanvasColor.Preset(preset))));
        }
        choices.Add(new CanvasPromptChoice(null, "No color"));
        return new(
            CanvasPromptKind.SetColor,
            document,
            null,
            null,
            "Set Color",
            string.Empty,
            choices.ToImmutable());
    }

    public CanvasPromptKind Kind { get; }

    public string Title { get; }

    public string Draft { get; set; }

    public bool HasTextField => Choices.IsEmpty;

    public ImmutableArray<CanvasPromptChoice> Choices { get; }

    public CanvasPromptChoice? SelectedChoice { get; set; }

    /// <summary>The staged request, held immutable for its life —
    /// the F7 pin a fact reads.</summary>
    internal CanvasConnectStage? StageForTests => _stage;

    /// <summary>Enter's arm: route the answer to the shipped verb.
    /// The connect label may be empty — Enter SKIPS, and the verb
    /// normalizes empty to null (IF-27).</summary>
    internal void Submit()
    {
        switch (Kind)
        {
            case CanvasPromptKind.ConnectLabel:
                _document.CanvasConnect(_stage!, Draft);
                break;
            case CanvasPromptKind.RenameGroup:
                _document.CanvasRenameGroup(_groupId!, Draft);
                break;
            default:
                _document.CanvasSetColor(SelectedChoice?.Value);
                break;
        }
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
