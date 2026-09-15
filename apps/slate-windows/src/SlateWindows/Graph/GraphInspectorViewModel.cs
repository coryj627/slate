// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows.Input;
using uniffi.slate_uniffi;

namespace SlateWindows.Graph;

/// <summary>One row of the inspector's Groups section (Term Y1) — a
/// projection of the view state's list: the 1-based index the labels
/// compose with (the mac's <c>index + 1</c>), the rule's three fields and
/// the row's remove command (Term Y5).</summary>
internal sealed record GraphInspectorGroupRow(int Index, string Query, GraphColorToken ColorToken, GraphRingStyle RingStyle, ICommand RemoveCommand);

/// <summary>
/// W6-2 PR E (#746), contract E-2; rule I Terms I6 and I7; rules X, Y, Z
/// and K: the inspector's view model — ONE per workspace (E-12 i),
/// constructed after the preferences and the view state and before the
/// navigator and the graph document, holding NO copy of either source
/// (A-2; E-12 vii): every bound property reads through to the view state
/// (the filter, the needle, the groups) or the preferences (the display,
/// the forces), and every write is one call into the frozen seam that owns
/// the field — the document's ChangeFilter for the flags (Term X1), the
/// navigator's SetNameQuery for the needle (Term X3), the view state's
/// Groups setter and the preferences' SetGroups for the groups (Term Y2),
/// the preferences' SetDisplay for the display (Term Z1), the preferences'
/// SetForces then the document's force seams for the forces (Term K2). The
/// pane's controls are enabled only while the graph is EFFECTIVE (Term
/// I7): the workspace recomputes <see cref="IsGraphEffective"/> from rule
/// L's funnel and at the document's seat and retirement. It posts nothing
/// itself (E-12 iv).
/// </summary>
internal sealed class GraphInspectorViewModel : BindableBase, IDisposable
{
    private static readonly Lazy<IReadOnlyList<GraphColorTokenSpec>> ColorTokensOnce =
        new(() => SlateUniffiMethods.GraphColorTokens());

    private static readonly Lazy<IReadOnlyList<GraphRingStyleSpec>> RingStylesOnce =
        new(() => SlateUniffiMethods.GraphRingStyles());

    private readonly GraphViewState _viewState;
    private readonly GraphPreferencesViewModel _preferences;
    private readonly Func<GraphNavigator?> _navigator;
    private readonly Func<GraphDocumentViewModel?> _document;
    private IReadOnlyList<GraphInspectorGroupRow> _rows;
    private bool _isGraphEffective;
    private bool _disposed;

    public GraphInspectorViewModel(
        GraphViewState viewState,
        GraphPreferencesViewModel preferences,
        Func<GraphNavigator?> navigator,
        Func<GraphDocumentViewModel?> document)
    {
        ArgumentNullException.ThrowIfNull(viewState);
        ArgumentNullException.ThrowIfNull(preferences);
        ArgumentNullException.ThrowIfNull(navigator);
        ArgumentNullException.ThrowIfNull(document);
        _viewState = viewState;
        _preferences = preferences;
        _navigator = navigator;
        _document = document;
        _rows = RowsOf(viewState.Groups);
        AddGroupCommand = new RelayCommand(_ => AddGroup(), _ => true);
        _viewState.PropertyChanged += OnViewStateChanged;
        _preferences.DisplayChanged += OnDisplayChanged;
        _preferences.ForcesChanged += OnForcesChanged;
    }

    // --- Rule X: the filters, read through the view state ------------------------

    public bool IncludeAttachments => _viewState.Filter.IncludeAttachments;

    public bool IncludeGhosts => _viewState.Filter.IncludeGhosts;

    public bool OrphansOnly => _viewState.Filter.OrphansOnly;

    public string NameQuery => _viewState.NameQuery;

    /// <summary>Term X1: the three flags, ONE write — the document's
    /// ChangeFilter (ApplyQuery with the overlay cleared, then rule Q's
    /// Filter pair), THEN the preferences' SetFilters; a refused change
    /// (retired, unseated, the current flags — Term X4) persists nothing.
    /// With no document nothing is written (the controls are disabled
    /// outside an effective graph, Term I7).</summary>
    public void SetBackendFilter(GraphFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        if (_document() is { } document && document.ChangeFilter(filter))
        {
            _preferences.SetFilters(filter);
        }
    }

    /// <summary>Term X3 (ED-Q2, ED-12): the needle's ONE writer is the
    /// navigator's — the header's field and this one are two views of it.</summary>
    public void SetNameQuery(string raw)
    {
        ArgumentNullException.ThrowIfNull(raw);
        _navigator()?.SetNameQuery(raw);
    }

    // --- Rule Y: the groups, a projection of the view state's list ---------------

    public IReadOnlyList<GraphInspectorGroupRow> Groups => _rows;

    public bool HasNoGroups => _rows.Count == 0;

    /// <summary>Term Y4 (0bD-12): core's vectors, fetched once per process, never a case typed.</summary>
    public IReadOnlyList<GraphColorTokenSpec> ColorTokens => ColorTokensOnce.Value;

    public IReadOnlyList<GraphRingStyleSpec> RingStyles => RingStylesOnce.Value;

    public ICommand AddGroupCommand { get; }

    /// <summary>Term Y2: every edit builds the NEW list and writes it to the
    /// two seams — the view state's setter (the diagram's epoch follows it,
    /// Term G3) then the preferences' trigger. Silent (ED-Q4).</summary>
    public void SetGroups(IReadOnlyList<GraphGroup> groups)
    {
        ArgumentNullException.ThrowIfNull(groups);
        GraphGroup[] list = [.. groups];
        _viewState.Groups = list;
        _preferences.SetGroups(list);
    }

    /// <summary>Term Y3: append a group in core's next style (the mac's
    /// <c>addGraphGroup</c>; 0b-12), so successive groups differ on both channels.</summary>
    public void AddGroup()
    {
        GraphGroupStyle style = SlateUniffiMethods.GraphConfigNextGroupStyle((uint)_viewState.Groups.Count);
        SetGroups([.. _viewState.Groups, new GraphGroup(string.Empty, style.ColorToken, style.RingStyle)]);
    }

    /// <summary>Term Y5: the row's index removed and the list written; an
    /// index outside the list is nothing.</summary>
    public void RemoveGroup(int index)
    {
        IReadOnlyList<GraphGroup> groups = _viewState.Groups;
        if (index < 0 || index >= groups.Count)
        {
            return;
        }
        SetGroups([.. groups.Take(index), .. groups.Skip(index + 1)]);
    }

    public void SetGroupQuery(int index, string query)
    {
        ArgumentNullException.ThrowIfNull(query);
        EditGroup(index, group => group with { Query = query });
    }

    public void SetGroupColor(int index, GraphColorToken token) =>
        EditGroup(index, group => group with { ColorToken = token });

    public void SetGroupRing(int index, GraphRingStyle style) =>
        EditGroup(index, group => group with { RingStyle = style });

    private void EditGroup(int index, Func<GraphGroup, GraphGroup> edit)
    {
        IReadOnlyList<GraphGroup> groups = _viewState.Groups;
        if (index < 0 || index >= groups.Count)
        {
            return;
        }
        GraphGroup edited = edit(groups[index]);
        if (edited == groups[index])
        {
            return;
        }
        GraphGroup[] list = [.. groups];
        list[index] = edited;
        SetGroups(list);
    }

    // --- Rule Z: the display, read through the preferences -------------------------

    public bool Arrows => _preferences.CurrentConfig.Display.Arrows;

    public double TextFadeZoom => _preferences.CurrentConfig.Display.TextFadeZoom;

    public double NodeSizeMultiplier => _preferences.CurrentConfig.Display.NodeSizeMultiplier;

    public double LinkThickness => _preferences.CurrentConfig.Display.LinkThickness;

    /// <summary>Term Z1: the preferences' trigger — the field, its event,
    /// the schedule; the document forwards the change and the renderer
    /// redraws (Term Z2). Silent (ED-Q4); equal values are a no-op.</summary>
    public void SetDisplay(GraphDisplay display)
    {
        ArgumentNullException.ThrowIfNull(display);
        _preferences.SetDisplay(display);
    }

    public void SetArrows(bool arrows) => SetDisplay(_preferences.CurrentConfig.Display with { Arrows = arrows });

    public void SetTextFadeZoom(double value) => SetDisplay(_preferences.CurrentConfig.Display with { TextFadeZoom = value });

    public void SetNodeSizeMultiplier(double value) => SetDisplay(_preferences.CurrentConfig.Display with { NodeSizeMultiplier = value });

    public void SetLinkThickness(double value) => SetDisplay(_preferences.CurrentConfig.Display with { LinkThickness = value });

    // --- Rule K: the forces, read through the preferences ---------------------------

    public double Center => _preferences.CurrentConfig.Forces.Center;

    public double Repel => _preferences.CurrentConfig.Forces.Repel;

    public double Link => _preferences.CurrentConfig.Forces.Link;

    public double LinkDistance => _preferences.CurrentConfig.Forces.LinkDistance;

    /// <summary>Term K2 — three seams, one order: (i) the preferences'
    /// SetForces (Term W7; a no-op for equal forces); (ii) the changed
    /// control SPOKEN through the document's seam — BEFORE the arm and the
    /// restart, so the value precedes the settle under every scheduler
    /// (IGU-2); (iii) the document's ApplyForces — the gate, the arm, the
    /// restart over a live model; the install's re-apply under a build.</summary>
    public void SetForces(GraphForcesConfig forces)
    {
        ArgumentNullException.ThrowIfNull(forces);
        GraphForcesConfig old = _preferences.CurrentConfig.Forces;
        _preferences.SetForces(forces);
        GraphDocumentViewModel? document = _document();
        if (ChangedForce(old, forces) is { } change)
        {
            document?.AnnounceForceValue(change.Control, change.Percent);
        }
        _ = document?.ApplyForces(forces);
    }

    public void SetCenter(double value) => SetForces(_preferences.CurrentConfig.Forces with { Center = value });

    public void SetRepel(double value) => SetForces(_preferences.CurrentConfig.Forces with { Repel = value });

    public void SetLink(double value) => SetForces(_preferences.CurrentConfig.Forces with { Link = value });

    public void SetLinkDistance(double value) => SetForces(_preferences.CurrentConfig.Forces with { LinkDistance = value });

    /// <summary>Term K3 (ED-6): the FIRST of Center, Repel, Link, LinkDistance
    /// whose value differs, at its percent — a half rounded AWAY from zero,
    /// Swift's <c>rounded()</c> (IGW-2) — the mac's <c>changedForce</c>;
    /// null when none differs.</summary>
    public static (GraphForceControl Control, uint Percent)? ChangedForce(GraphForcesConfig old, GraphForcesConfig @new)
    {
        ArgumentNullException.ThrowIfNull(old);
        ArgumentNullException.ThrowIfNull(@new);
        if (@new.Center != old.Center)
        {
            return (GraphForceControl.Center, PercentOf(@new.Center));
        }
        if (@new.Repel != old.Repel)
        {
            return (GraphForceControl.Repel, PercentOf(@new.Repel));
        }
        if (@new.Link != old.Link)
        {
            return (GraphForceControl.Link, PercentOf(@new.Link));
        }
        if (@new.LinkDistance != old.LinkDistance)
        {
            return (GraphForceControl.LinkDistance, PercentOf(@new.LinkDistance));
        }
        return null;
    }

    private static uint PercentOf(double value) =>
        (uint)Math.Max(0, Math.Round(value * 100, MidpointRounding.AwayFromZero));

    // --- Term I7: the effectiveness gate; Term Y6: the read-only state --------------

    /// <summary>True iff a graph document is seated AND effective — the one
    /// predicate every graph line is gated on; the pane's controls are
    /// enabled by it and the inactive notice shown while it is false (E-D8).</summary>
    public bool IsGraphEffective
    {
        get => _isGraphEffective;
        private set => SetField(ref _isGraphEffective, value);
    }

    /// <summary>Recomputed by the workspace at rule L's effectiveness edge
    /// and at the document's seat and retirement.</summary>
    internal void RefreshGraphEffectiveness() =>
        IsGraphEffective = _document() is { IsRetired: false } document && document.IsEffective;

    /// <summary>Term Y6 (E-D6): false after a failed read — the edits stay
    /// live and every save is refused; the pane says so with the reason.</summary>
    public bool IsWritable => _preferences.IsWritable;

    public string? LoadFailure => _preferences.LoadFailure;

    // --- The notifications from outside writes (E-2) ---------------------------------

    private void OnViewStateChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(GraphViewState.Filter):
                OnPropertyChanged(nameof(IncludeAttachments));
                OnPropertyChanged(nameof(IncludeGhosts));
                OnPropertyChanged(nameof(OrphansOnly));
                break;
            case nameof(GraphViewState.NameQuery):
                OnPropertyChanged(nameof(NameQuery));
                break;
            case nameof(GraphViewState.Groups):
                _rows = RowsOf(_viewState.Groups);
                OnPropertyChanged(nameof(Groups));
                OnPropertyChanged(nameof(HasNoGroups));
                break;
            default:
                break;
        }
    }

    private void OnDisplayChanged()
    {
        OnPropertyChanged(nameof(Arrows));
        OnPropertyChanged(nameof(TextFadeZoom));
        OnPropertyChanged(nameof(NodeSizeMultiplier));
        OnPropertyChanged(nameof(LinkThickness));
    }

    private void OnForcesChanged()
    {
        OnPropertyChanged(nameof(Center));
        OnPropertyChanged(nameof(Repel));
        OnPropertyChanged(nameof(Link));
        OnPropertyChanged(nameof(LinkDistance));
    }

    private IReadOnlyList<GraphInspectorGroupRow> RowsOf(IReadOnlyList<GraphGroup> groups)
    {
        var rows = new List<GraphInspectorGroupRow>(groups.Count);
        for (int i = 0; i < groups.Count; i++)
        {
            int index = i;
            GraphGroup group = groups[i];
            rows.Add(new GraphInspectorGroupRow(
                index + 1,
                group.Query,
                group.ColorToken,
                group.RingStyle,
                new RelayCommand(_ => RemoveGroup(index), _ => true)));
        }
        return rows;
    }

    /// <summary>The subscriptions to the two sources released with the workspace (Term I6).</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _viewState.PropertyChanged -= OnViewStateChanged;
        _preferences.DisplayChanged -= OnDisplayChanged;
        _preferences.ForcesChanged -= OnForcesChanged;
    }
}
