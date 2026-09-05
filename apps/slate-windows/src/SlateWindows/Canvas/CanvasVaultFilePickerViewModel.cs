// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Immutable;
using uniffi.slate_uniffi;

namespace SlateWindows.Canvas;

/// <summary>§G2 TG2-3 (G2-6, IG2-48): the ONE picker-sheet contract the
/// workspace's picker slot holds — the card picker and the vault file
/// picker both inhabit <c>ModalSurface.CanvasCardPicker</c> through it,
/// and the XAML sheet binds the same names for either. The rows are
/// each sheet's own typed rows; the sheet-level names are the
/// purpose's, never a card-specific constant.</summary>
internal interface ICanvasPickerSheet
{
    string Title { get; }

    string Filter { get; set; }

    /// <summary>The dialog's UIA name.</summary>
    string SheetName { get; }

    /// <summary>The filter field's UIA name.</summary>
    string FilterName { get; }

    /// <summary>The rows list's UIA name.</summary>
    string RowsName { get; }

    /// <summary>True when the pick ROUTED (a verb ran or a stage opened) —
    /// the sheet closes; false keeps it, filter and highlight intact.</summary>
    bool Confirm();
}

/// <summary>What the vault file picker is for — the admission predicate
/// and the verb the pick feeds.</summary>
internal enum CanvasVaultPickPurpose
{
    Note,
    Media,
    Locate,
}

/// <summary>§G2 TG2-3 (G2-6, IG2-19/49/50): the vault picker's REQUEST — a
/// value captured at open: the purpose, the target card for Locate, the
/// exact loaded identity, the invoking owner, and the model GENERATION
/// the sheet shows. Presentation and submit both check it: a different
/// loaded identity, a superseded generation, or an owner that is no
/// longer current is a guess and refuses <c>PickDifferentTarget</c>.</summary>
internal sealed record CanvasVaultPickRequest(
    CanvasVaultPickPurpose Purpose,
    string? TargetId,
    CanvasLoaded Identity,
    object? Owner,
    CanvasVaultFilePickerModel Model);

/// <summary>One picker row: the value the verb takes, the label a reader
/// hears, and the status the item carries (the path, for a file).</summary>
internal sealed record CanvasPickerRow(string Value, string Label, string? Status);

/// <summary>
/// §G2 TG2-3 (G2-6): the vault file picker — the card picker's sibling in
/// the card picker's own arm and slot. Rows are the model's classified
/// generation narrowed by the filter and capped at 200 with "type to
/// narrow" (ED-2), named by the file's display name (else its name)
/// with the path as the row's status. Enter routes the pick through
/// the document, which re-validates the request before any verb runs.
/// </summary>
internal sealed class CanvasVaultFilePickerViewModel
    : System.ComponentModel.INotifyPropertyChanged, ICanvasPickerSheet
{
    private readonly CanvasDocumentViewModel _document;
    private string _filter = string.Empty;

    internal CanvasVaultFilePickerViewModel(
        CanvasDocumentViewModel document, CanvasVaultPickRequest request)
    {
        _document = document;
        Request = request;
        Rows = Project(request.Model.Visible(string.Empty));
        SelectedRow = Rows.IsEmpty ? null : Rows[0];
    }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    internal CanvasVaultPickRequest Request { get; }

    public string Title => Request.Purpose switch
    {
        CanvasVaultPickPurpose.Note => "Add Note to Canvas…",
        CanvasVaultPickPurpose.Media => "Add Media…",
        _ => "Locate File…",
    };

    public string SheetName => Request.Purpose switch
    {
        CanvasVaultPickPurpose.Note => "Note picker",
        CanvasVaultPickPurpose.Media => "Media picker",
        _ => "File picker",
    };

    public string FilterName => "Filter files";

    public string RowsName => Request.Purpose switch
    {
        CanvasVaultPickPurpose.Note => "Vault notes. Type to narrow; Enter picks.",
        CanvasVaultPickPurpose.Media => "Vault media files. Type to narrow; Enter picks.",
        _ => "Vault files. Type to narrow; Enter picks.",
    };

    public string Filter
    {
        get => _filter;
        set
        {
            _filter = value ?? string.Empty;
            Rows = Project(Request.Model.Visible(_filter));
            if (SelectedRow is null || !Rows.Contains(SelectedRow))
            {
                SelectedRow = Rows.IsEmpty ? null : Rows[0];
            }
            PropertyChanged?.Invoke(
                this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Rows)));
            PropertyChanged?.Invoke(
                this, new System.ComponentModel.PropertyChangedEventArgs(nameof(SelectedRow)));
        }
    }

    public ImmutableArray<CanvasPickerRow> Rows { get; private set; }

    public CanvasPickerRow? SelectedRow { get; set; }

    public bool Confirm() =>
        SelectedRow is { } row && _document.HandleVaultPick(Request, row.Value);

    /// <summary>The owner half's refusal, spoken through the sheet's OWN
    /// document — the active tab may no longer be a canvas at all.</summary>
    internal void RefuseStale() => _document.SpeakPickDifferentTarget();

    private static ImmutableArray<CanvasPickerRow> Project(ImmutableArray<FileSummary> files) =>
        [.. files.Select(f => new CanvasPickerRow(f.Path, f.DisplayName ?? f.Name, f.Path))];
}
