// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using uniffi.slate_uniffi;

namespace SlateWindows.FileManagement;

/// <summary>What one Move-To row is.</summary>
internal enum MoveToRowKind
{
    /// <summary>The pinned destination "" row.</summary>
    VaultRoot,

    /// <summary>Create the typed folder, then move — one user
    /// gesture, two core ops (F4).</summary>
    NewFolder,

    /// <summary>An existing vault folder.</summary>
    Folder,
}

/// <summary>One picker row.</summary>
internal sealed class MoveToRowViewModel
{
    internal MoveToRowViewModel(MoveToRowKind kind, string destination, string label)
    {
        Kind = kind;
        Destination = destination;
        Label = label;
    }

    public MoveToRowKind Kind { get; }

    /// <summary>The vault-relative destination ("" for the root; for
    /// the New Folder row, the typed path to create).</summary>
    public string Destination { get; }

    public string Label { get; }

    /// <summary>The full path detail, shown when the leaf alone is
    /// ambiguous; null for the pinned rows and top-level
    /// folders.</summary>
    public string? Detail =>
        Kind == MoveToRowKind.Folder && Destination.Contains('/')
            ? Destination
            : null;

    public string AccessibleName =>
        Detail is { } detail ? $"{Label}. {detail}." : Label;
}

/// <summary>
/// The Move-To folder picker sheet (W5-4 F4): the keyboard-first,
/// drag-free move path. The sidebar hands it the LEGAL destination set
/// — illegal destinations (each moving item's current parent; a moving
/// folder's own subtree) are filtered before the pick, never refused
/// after it — and the typed "New Folder…" path obeys the same
/// legality through the injected predicate. Filter-as-you-type; the
/// "Vault root" row stays PINNED whenever the root is legal.
/// </summary>
internal sealed class MoveToPickerViewModel : BindableBase
{
    private readonly IReadOnlyList<string> _folders;
    private readonly bool _rootIsLegal;
    private readonly Action<string> _confirmed;
    private readonly Action<string> _createAndMove;
    private readonly Action _cancelled;
    private readonly Func<string, bool> _newFolderPathAllowed;
    private readonly Action<A11yEvent> _announce;

    private string _filterText = string.Empty;
    private IReadOnlyList<MoveToRowViewModel> _rows = [];
    private MoveToRowViewModel? _selectedRow;
    private bool _rebuilding;
    private ICommand? _activateCommand;
    private ICommand? _cancelCommand;

    internal MoveToPickerViewModel(
        IReadOnlyList<string> legalFolders,
        bool rootIsLegal,
        string itemNoun,
        Action<string> confirmed,
        Action<string> createAndMove,
        Action cancelled,
        Func<string, bool> newFolderPathAllowed,
        Action<A11yEvent> announce)
    {
        ArgumentNullException.ThrowIfNull(legalFolders);
        ArgumentNullException.ThrowIfNull(itemNoun);
        ArgumentNullException.ThrowIfNull(confirmed);
        ArgumentNullException.ThrowIfNull(createAndMove);
        ArgumentNullException.ThrowIfNull(cancelled);
        ArgumentNullException.ThrowIfNull(newFolderPathAllowed);
        ArgumentNullException.ThrowIfNull(announce);
        _folders = legalFolders;
        _rootIsLegal = rootIsLegal;
        ItemNoun = itemNoun;
        _confirmed = confirmed;
        _createAndMove = createAndMove;
        _cancelled = cancelled;
        _newFolderPathAllowed = newFolderPathAllowed;
        _announce = announce;
        RebuildRows(announceCount: false);
    }

    /// <summary>What is moving, as prose — "a.md" or "3 items".</summary>
    public string ItemNoun { get; }

    public string Title => $"Move {ItemNoun} to…";

    public string Subtitle =>
        "Type to filter folders. Enter moves. Escape to cancel.";

    public string FilterText
    {
        get => _filterText;
        set
        {
            if (SetField(ref _filterText, value))
            {
                RebuildRows(announceCount: true);
            }
        }
    }

    public IReadOnlyList<MoveToRowViewModel> Rows
    {
        get => _rows;
        private set => SetField(ref _rows, value);
    }

    /// <summary>Red team (a11y 1): selection driven from the filter
    /// box announces the row — the quick-open pattern this picker
    /// cites announces BOTH halves (count per filter landing, row per
    /// selection move); without them the keyboard-first flow is
    /// silent to AT and Enter moves files to an unheard
    /// destination.</summary>
    public MoveToRowViewModel? SelectedRow
    {
        get => _selectedRow;
        set
        {
            if (SetField(ref _selectedRow, value)
                && !_rebuilding
                && value is not null)
            {
                _announce(new A11yEvent.RowSelected(value.AccessibleName));
            }
        }
    }

    public ICommand ActivateCommand => _activateCommand ??= new RelayCommand(
        parameter =>
        {
            if (parameter is not MoveToRowViewModel row)
            {
                return;
            }

            if (row.Kind == MoveToRowKind.NewFolder)
            {
                _createAndMove(row.Destination);
            }
            else
            {
                _confirmed(row.Destination);
            }
        },
        _ => true);

    public ICommand CancelCommand => _cancelCommand ??= new RelayCommand(
        _ => _cancelled(), _ => true);

    private void RebuildRows(bool announceCount)
    {
        string filter = FilterText.Trim();
        var rows = new List<MoveToRowViewModel>();
        // PINNED (F4): the root row survives any filter — a typed
        // query must never make the root unreachable (red team,
        // contracts 6).
        if (_rootIsLegal)
        {
            rows.Add(new MoveToRowViewModel(
                MoveToRowKind.VaultRoot, string.Empty, "Vault root"));
        }

        rows.AddRange(_folders
            .Where(folder => filter.Length == 0
                || folder.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .Select(folder => new MoveToRowViewModel(
                MoveToRowKind.Folder, folder, LeafOf(folder))));

        // The create-then-move row: the typed filter IS the new
        // folder's vault-relative path, so the gesture stays
        // keyboard-first — type a name, pick the row. Hidden when the
        // path is illegal or already exists ANYWHERE (the legality
        // predicate covers the filtered-out folders too — an existing
        // destination must never resurface as a "create" that refuses
        // typed).
        string typedPath = filter.Trim('/');
        if (typedPath.Length > 0 && _newFolderPathAllowed(typedPath))
        {
            rows.Add(new MoveToRowViewModel(
                MoveToRowKind.NewFolder,
                typedPath,
                $"New Folder “{typedPath}”"));
        }

        _rebuilding = true;
        try
        {
            Rows = rows;
            // Default selection prefers the first row the QUERY
            // produced: with text typed, the pinned root would
            // otherwise steal Enter from the filtered match.
            SelectedRow = filter.Length == 0
                ? rows.FirstOrDefault()
                : rows.FirstOrDefault(row => row.Kind != MoveToRowKind.VaultRoot)
                    ?? rows.FirstOrDefault();
        }
        finally
        {
            _rebuilding = false;
        }

        if (announceCount)
        {
            int destinations = rows.Count;
            // W0.5-3 residue: Move-To filter landing copy.
            _announce(new A11yEvent.HostComposed(
                $"{destinations:N0} {(destinations == 1 ? "destination" : "destinations")}.",
                A11yPriority.Medium));
        }
    }

    private static string LeafOf(string vaultPath)
    {
        int slash = vaultPath.LastIndexOf('/');
        return slash >= 0 ? vaultPath[(slash + 1)..] : vaultPath;
    }
}
