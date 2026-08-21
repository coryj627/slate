// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.Windows.Input;
using uniffi.slate_uniffi;

namespace SlateWindows.Templates;

/// <summary>The picker's presentation state (contracts doc T3) — mac's
/// <c>TemplateAvailability</c>, minus the standalone `loading` gap
/// between request and landing (the Windows enumeration is synchronous,
/// finding 3, so `Loading` is observable only through the injectable
/// enumeration seam in tests).</summary>
internal enum TemplatePickerState
{
    Loading,
    Empty,
    Failed,
    Available,
}

/// <summary>
/// One picker row. The accessible name is mac's
/// <c>rowAccessibilityLabel</c> verbatim: `"{name}. {description}."`
/// when a description exists, else the bare name.
/// </summary>
internal sealed class TemplatePickerRowViewModel
{
    public TemplatePickerRowViewModel(TemplateSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);
        Summary = summary;
    }

    public TemplateSummary Summary { get; }

    public string Name => Summary.Name;

    public string? Description =>
        string.IsNullOrEmpty(Summary.Description) ? null : Summary.Description;

    public string AccessibleName =>
        Description is { } description ? $"{Name}. {description}." : Name;
}

/// <summary>
/// The template picker sheet (W5-3 T3): lists the vault's templates via
/// core `list_templates` — core owns discovery, ordering, and the
/// description pick; this VM re-derives none of it. Enumeration runs
/// fresh on every open and on every Try Again (mac re-fetches per
/// open); the announcement partition is contracts doc T10 — a
/// canonical <c>TemplatePickerOpened</c> for a row-bearing present,
/// the availability residue strings for empty/failed.
/// </summary>
internal sealed class TemplatePickerViewModel : BindableBase
{
    /// <summary>mac `templateAvailabilityEmptyReason`, verbatim.</summary>
    internal const string EmptyReason =
        "Add a Markdown file to this vault’s configured template folder "
        + "to create from a template.";

    /// <summary>mac `templateAvailabilityFailedReason`, verbatim.</summary>
    internal const string FailedReason =
        "Slate couldn’t load templates. Check the configured template "
        + "folder and try again.";

    private readonly Func<IReadOnlyList<TemplateSummary>> _enumerate;
    private readonly Action<TemplateSummary> _activated;
    private readonly Action _cancelled;
    private readonly Action<A11yEvent> _announce;

    private TemplatePickerState _state = TemplatePickerState.Loading;
    private IReadOnlyList<TemplatePickerRowViewModel> _rows = [];
    private ICommand? _activateCommand;
    private ICommand? _retryCommand;
    private ICommand? _cancelCommand;

    public TemplatePickerViewModel(
        Func<IReadOnlyList<TemplateSummary>> enumerate,
        string destinationDescription,
        Action<TemplateSummary> activated,
        Action cancelled,
        Action<A11yEvent> announce)
    {
        ArgumentNullException.ThrowIfNull(enumerate);
        ArgumentNullException.ThrowIfNull(destinationDescription);
        ArgumentNullException.ThrowIfNull(activated);
        ArgumentNullException.ThrowIfNull(cancelled);
        ArgumentNullException.ThrowIfNull(announce);
        _enumerate = enumerate;
        DestinationDescription = destinationDescription;
        _activated = activated;
        _cancelled = cancelled;
        _announce = announce;
    }

    /// <summary>The frozen destination, as prose (T12).</summary>
    public string DestinationDescription { get; }

    /// <summary>mac's picker subtitle with the Windows chord (program
    /// decision 12 for the chord text; the sentence shape is mac's).
    /// The chord is READ FROM THE TABLE — a hardcoded copy here would
    /// be exactly the advertisement drift PINV-5 exists to stop
    /// (red team, tests finding 13a) — and a chordless row degrades
    /// to the sentence without the chord snippet (codoki: the
    /// null-forgiving lookup would have turned a future
    /// unbind-the-chord edit — NewNote's exact shipped shape — into
    /// stray punctuation or a binding-time throw).</summary>
    public string Subtitle => ComposeSubtitle(
        DestinationDescription,
        Commands.ChordTable.Find(Commands.ChordTable.Ids.NewFromTemplate)?.WindowsChord);

    /// <summary>Pure composition seam so both chord arms are unit-
    /// pinned — the live table lookup is static and cannot be made to
    /// return a chordless row headless.</summary>
    internal static string ComposeSubtitle(
        string destinationDescription, string? windowsChord) =>
        string.IsNullOrEmpty(windowsChord)
            ? $"Create in {destinationDescription}. Escape to cancel."
            : $"Create in {destinationDescription}. {windowsChord}. Escape to cancel.";

    /// <summary>mac `emptyStateDetail`, verbatim.</summary>
    public static string EmptyStateDetail =>
        "Create a .md file in this vault’s configured template folder to add one.";

    /// <summary>mac's failed-state guidance, verbatim.</summary>
    public static string FailedStateDetail =>
        "Check the configured template folder, then try again.";

    public TemplatePickerState State
    {
        get => _state;
        private set
        {
            if (SetField(ref _state, value))
            {
                // The state drives four mutually exclusive XAML
                // visibilities; derived flags keep the bindings
                // converter-free (the AddPropertySheet idiom).
                OnPropertyChanged(nameof(IsLoading));
                OnPropertyChanged(nameof(IsEmpty));
                OnPropertyChanged(nameof(IsFailed));
                OnPropertyChanged(nameof(IsAvailable));
            }
        }
    }

    public bool IsLoading => State == TemplatePickerState.Loading;

    public bool IsEmpty => State == TemplatePickerState.Empty;

    public bool IsFailed => State == TemplatePickerState.Failed;

    public bool IsAvailable => State == TemplatePickerState.Available;

    public IReadOnlyList<TemplatePickerRowViewModel> Rows
    {
        get => _rows;
        private set => SetField(ref _rows, value);
    }

    public ICommand ActivateCommand => _activateCommand ??= new RelayCommand(
        parameter =>
        {
            if (parameter is TemplatePickerRowViewModel row)
            {
                _activated(row.Summary);
            }
        },
        _ => true);

    // Always executable: the Try Again button is only VISIBLE in the
    // empty/failed states, and RelayCommand has no requery wiring — a
    // state-dependent CanExecute here would go stale, not strict.
    public ICommand RetryCommand => _retryCommand ??= new RelayCommand(
        _ => Load(), _ => true);

    public ICommand CancelCommand => _cancelCommand ??= new RelayCommand(
        _ => _cancelled(), _ => true);

    /// <summary>
    /// Enumerate and land a terminal state. Called once at present and
    /// again by every Try Again; each landing announces (mac announces
    /// per presenting load — T10's partition decides which voice).
    /// </summary>
    public void Load()
    {
        State = TemplatePickerState.Loading;
        IReadOnlyList<TemplateSummary> summaries;
        try
        {
            summaries = _enumerate();
        }
        catch (VaultException)
        {
            Rows = [];
            State = TemplatePickerState.Failed;
            // W0.5-3 residue: template availability copy serves double
            // duty as the picker's visible guidance (contracts doc T10).
            _announce(new A11yEvent.HostComposed(FailedReason, A11yPriority.Medium));
            return;
        }

        var rows = new List<TemplatePickerRowViewModel>(summaries.Count);
        foreach (TemplateSummary summary in summaries)
        {
            rows.Add(new TemplatePickerRowViewModel(summary));
        }

        Rows = rows;
        if (rows.Count == 0)
        {
            State = TemplatePickerState.Empty;
            // W0.5-3 residue: template availability copy serves double
            // duty as the picker's visible guidance (contracts doc T10).
            _announce(new A11yEvent.HostComposed(EmptyReason, A11yPriority.Medium));
            return;
        }

        State = TemplatePickerState.Available;
        _announce(new A11yEvent.TemplatePickerOpened((uint)rows.Count));
    }
}
