// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows.Input;
using uniffi.slate_uniffi;

namespace SlateWindows.Graph;

/// <summary>
/// What the navigator needs from the graph surface the reader is in
/// (W6-2 PR C, contract C-1): the presenter seam — three questions, two
/// focus moves, one dismissal, one liveness — implemented by
/// <see cref="GraphSurfaceView"/> and attached on the false→true edge of
/// its keyboard focus and on every chord.
/// </summary>
internal interface IGraphSurfacePresenter
{
    /// <summary>Raise rule F's landing request for this pane (Term F1)
    /// — never an immediate <c>Focus()</c>.</summary>
    void RequestProjectionFocus();

    /// <summary>Put the keys in the filter field (C-5's grid gesture).</summary>
    void FocusFilterField();

    /// <summary>Dismiss a transient region inside the surface — the
    /// Where-am-I panel (C-8) — re-seating the reader through rule F;
    /// false when there was nothing to dismiss.</summary>
    bool DismissTransientRegion();

    /// <summary>Whether the projection — the grid or the state host —
    /// owns keyboard focus.</summary>
    bool ProjectionHasFocus { get; }

    /// <summary>Whether the filter REGION — the field, the count region or
    /// Clear — holds the keys (IGO-14).</summary>
    bool FilterRegionHasKeys { get; }

    /// <summary>Whether this surface is still in the tree and attached to
    /// a live document — a verb that moves focus asks first.</summary>
    bool IsLive { get; }
}

/// <summary>What the workspace's preset funnel reports after the open's
/// mutation (rule P, Term P3): whether the follow method consumed the
/// arm, and whether the graph is effective now.</summary>
internal readonly record struct GraphPresetOpenReport(bool ArmConsumed, bool GraphEffective);

/// <summary>
/// W6-2 PR C (#746), contract C-1: the graph's command layer — one per
/// workspace, constructed after the relay and the view state and before
/// the graph document and the Connections leaf, reached by the
/// workspace's commands through the registrar (rule R-E), by the
/// surface's field (C-5) and by the chord half. The VERB half is
/// <see cref="RunPreset"/>, <see cref="SetNameQuery"/>,
/// <see cref="ClearNameQuery"/> (and PR C's Where-am-I, C-8); the CHORD
/// half is <see cref="Bind"/> and <see cref="HandleKey"/>, the canvas
/// navigator's four-statement body verbatim, walled by a census.
/// </summary>
internal sealed class GraphNavigator : BindableBase
{
    private readonly GraphViewState _viewState;
    private readonly GraphPreferencesViewModel _preferences;
    private readonly Func<GraphDocumentViewModel?> _document;
    private readonly Func<string?> _openAdmissionReason;
    private readonly Func<GraphPreset, GraphPresetOpenReport> _openForPreset;
    private readonly Dictionary<(Key Key, ModifierKeys Modifiers), Func<bool>> _chords = [];
    private IGraphSurfacePresenter? _presenter;
    private Func<GraphA11yEvent.GraphWhereAmI?>? _tableReadback;
    private Func<GraphA11yEvent.GraphWhereAmI?>? _diagramReadback;
    private string? _whereAmIText;

    /// <param name="viewState">The workspace's one view state (B2-1).</param>
    /// <param name="document">The seated document, or null while no graph tab exists.</param>
    /// <param name="openAdmissionReason">The workspace's open-admission seam
    /// (Term P3): null admits.</param>
    /// <param name="openForPreset">The workspace's preset funnel (C-3 (iii)–(v)):
    /// sets the arm, runs the open's mutation, reports the boundary's verdict.</param>
    internal GraphNavigator(
        GraphViewState viewState,
        GraphPreferencesViewModel preferences,
        Func<GraphDocumentViewModel?> document,
        Func<string?> openAdmissionReason,
        Func<GraphPreset, GraphPresetOpenReport> openForPreset)
    {
        ArgumentNullException.ThrowIfNull(viewState);
        ArgumentNullException.ThrowIfNull(preferences);
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(openAdmissionReason);
        ArgumentNullException.ThrowIfNull(openForPreset);
        _viewState = viewState;
        _preferences = preferences;
        _document = document;
        _openAdmissionReason = openAdmissionReason;
        _openForPreset = openForPreset;
        Bind();
    }

    /// <summary>
    /// The Graph-scoped chord map. Every entry is a
    /// <c>ChordScope.Graph</c> row in the chord table, and
    /// <c>ChordTableTests</c> scrapes THIS registration in both
    /// directions — a chord handled here with no row, or a row claiming
    /// this scope that nothing here delivers, fails (C-11).
    /// </summary>
    private void Bind()
    {
        // C-7: the Escape ladder's rungs, delivered from the surface's
        // tunnelling handler while the surface has the keys.
        AddChord(Key.Escape, ModifierKeys.None, EscapeFromKey);
        // C-8: Where-am-I — the chord the canvas row shares, disjoint by
        // DELIVERY (C-11); unconsumed while the seam does not answer.
        AddChord(
            Key.I,
            ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift,
            WhereAmIFromKey);
    }

    private void AddChord(Key key, ModifierKeys modifiers, Func<bool> handler) =>
        _chords.Add((key, modifiers), handler);

    /// <summary>The registered chords (C-1's map): the residue the wall's
    /// fact reads, so the registration is asserted on THIS navigator and
    /// not on a dictionary of the test's own (IPG-6).</summary>
    internal IReadOnlyCollection<(Key Key, ModifierKeys Modifiers)> ChordsForTests => _chords.Keys;

    /// <summary>The chord half's one entry (C-1): the null guard, the
    /// attachment, the lookup, the handler — and no branch.</summary>
    internal bool HandleKey(Key key, ModifierKeys modifiers, IGraphSurfacePresenter presenter)
    {
        ArgumentNullException.ThrowIfNull(presenter);
        AttachPresenter(presenter);
        return _chords.TryGetValue((key, modifiers), out Func<bool>? action) && action();
    }

    /// <summary>The surface the reader is in, attached on every chord and
    /// on the false→true edge of its keyboard focus (C-1).</summary>
    internal void AttachPresenter(IGraphSurfacePresenter presenter)
    {
        ArgumentNullException.ThrowIfNull(presenter);
        _presenter = presenter;
    }

    /// <summary>Detach ONLY when the presenter is this surface (the
    /// canvas's rule): a closed tab's surface on <c>Unloaded</c>, a
    /// replaced model's old arm (IGN-12).</summary>
    internal void DetachPresenter(IGraphSurfacePresenter presenter)
    {
        ArgumentNullException.ThrowIfNull(presenter);
        if (ReferenceEquals(_presenter, presenter))
        {
            _presenter = null;
            // C-8: the panel's text goes with the pane that showed it.
            WhereAmIText = null;
        }
    }

    internal IGraphSurfacePresenter? PresenterForTests => _presenter;

    // --- The presets (contract C-3, rule P) --------------------------------

    /// <summary>
    /// A preset: admission first (Term P3), then the view state's query
    /// written as ONE record — the argument DIRECTLY core's crossing (C-4,
    /// C-15 v) — then the workspace's open under the arm (Term P2), then
    /// the boundary's report read, not inferred: consumed → route (a)
    /// loaded; unconsumed with the graph effective → route (b), the
    /// document's request entry (rule Q); otherwise the recorded query is
    /// restored and nothing loads (IGO-22). One load per invocation.
    /// </summary>
    public void RunPreset(GraphPreset preset)
    {
        // (i) The admission, asked before anything is written; a refusal
        // ends the verb (the seam's owner speaks, as the mac's gate does).
        if (_openAdmissionReason() is not null)
        {
            return;
        }
        // (ii) The record the restore needs, then the preset's query.
        var recorded = new GraphVisibilityQuery(_viewState.Filter, _viewState.NameQuery, _viewState.KindOnly);
        _viewState.ApplyQuery(SlateUniffiMethods.GraphPresetQuery(preset));
        // (iii)–(v) The arm, the mutation, the follow method, the boundary.
        GraphPresetOpenReport report = _openForPreset(preset);
        if (report.ArmConsumed)
        {
            return;
        }
        // (vi) Route (b): the already-effective graph, the seated live
        // document — the mac's `:450–451`, A-5's class.
        if (report.GraphEffective
            && _document() is { IsRetired: false } document
            && document.Request(new GraphRequest.Preset(preset)))
        {
            return;
        }
        // The mutation never made the graph effective: the query written at
        // (ii) is restored, so an inactive tab cannot later load it under a
        // Summary policy.
        _viewState.ApplyQuery(recorded);
    }

    // --- The needle (contract C-6) -----------------------------------------

    /// <summary>The ONE writer of the needle from the surface: raw (core
    /// trims, 0b-6), only when it differs, then the seated live document's
    /// request; with no document, or a retired one, the write happens and
    /// nothing is issued.</summary>
    public void SetNameQuery(string raw)
    {
        ArgumentNullException.ThrowIfNull(raw);
        if (string.Equals(raw, _viewState.NameQuery, StringComparison.Ordinal))
        {
            return;
        }
        _viewState.NameQuery = raw;
        // Term W7: the needle's field updated and the save scheduled
        // (C-6), whether or not a document is seated.
        _preferences.SetNameQuery(raw);
        if (_document() is { IsRetired: false } document)
        {
            _ = document.Request(new GraphRequest.Needle());
        }
    }

    public void ClearNameQuery() => SetNameQuery(string.Empty);

    /// <summary>The grid's Ctrl+F (C-5): the field, in the surface that has
    /// the keys.</summary>
    internal void FocusFilterField()
    {
        if (_presenter is { IsLive: true } presenter)
        {
            presenter.FocusFilterField();
        }
    }

    // --- The Escape ladder (contract C-7) ------------------------------------

    /// <summary>Rungs 0, 1 and 2; rung 3 is the shell's — the press bubbles.</summary>
    private bool EscapeFromKey()
    {
        if (_presenter is not { IsLive: true } presenter)
        {
            return false;
        }
        // Rung 0 (C-8): an open Where-am-I panel takes the press AHEAD of a
        // live needle — the pane the reader is in dismisses it and re-seats
        // by the panel's own rules.
        if (presenter.DismissTransientRegion())
        {
            return true;
        }
        if (_viewState.NameQuery.Length > 0)
        {
            // Rung 1: the raw needle is non-empty — cleared through the
            // navigator (a token; the count is the line, on publish), then
            // a landing that waits for the clear's token (Term F3).
            ClearNameQuery();
            presenter.RequestProjectionFocus();
            return true;
        }
        if (presenter.FilterRegionHasKeys)
        {
            // Rung 2: the filter region holds the keys with no needle — a
            // landing delivered at once when quiescent (Term F2).
            presenter.RequestProjectionFocus();
            return true;
        }
        return false;
    }
    // --- Where-am-I (contract C-8) -------------------------------------------

    /// <summary>The panel's text: the ONE event rendered by the relay's
    /// renderer (IGO-31); null while no panel is open; cleared on detach.</summary>
    public string? WhereAmIText
    {
        get => _whereAmIText;
        private set => SetField(ref _whereAmIText, value);
    }

    /// <summary>Raised by the document at every lineage edge and at its
    /// retirement, by the workspace when the effective tab changes, and
    /// here when a seam is installed or cleared: the command's CanExecute
    /// re-evaluates (IGN-13).</summary>
    internal event Action? WhereAmIAvailabilityChanged;

    /// <summary>The TABLE's readback seam, installed by the document at its
    /// seat and cleared (null) at its retirement.</summary>
    internal void InstallTableReadback(Func<GraphA11yEvent.GraphWhereAmI?>? readback)
    {
        _tableReadback = readback;
        WhereAmIAvailabilityChanged?.Invoke();
    }

    /// <summary>Clear the table's seam only when it is THIS one — a retired
    /// document never clears its successor's.</summary>
    internal void ClearTableReadback(Func<GraphA11yEvent.GraphWhereAmI?> readback)
    {
        ArgumentNullException.ThrowIfNull(readback);
        if (ReferenceEquals(_tableReadback, readback))
        {
            InstallTableReadback(null);
        }
    }

    /// <summary>The DIAGRAM's readback seam — null until PR D's diagram
    /// installs it (the mac's graphDiagramWhereAmIEvent).</summary>
    internal void InstallDiagramReadback(Func<GraphA11yEvent.GraphWhereAmI?>? readback)
    {
        _diagramReadback = readback;
        WhereAmIAvailabilityChanged?.Invoke();
    }

    /// <summary>The document's lineage edges and its retirement re-evaluate
    /// the availability through this.</summary>
    internal void NotifyWhereAmIAvailabilityChanged() => WhereAmIAvailabilityChanged?.Invoke();

    /// <summary>The ACTIVE projection's seam, chosen by the view state's Mode.</summary>
    private Func<GraphA11yEvent.GraphWhereAmI?>? ActiveReadback =>
        _viewState.Mode == GraphSurfaceMode.Diagram ? _diagramReadback : _tableReadback;

    /// <summary>The effective graph's active projection must answer. A
    /// graph retained behind another tab or in an inactive pane cannot
    /// provide the current location.</summary>
    private GraphA11yEvent.GraphWhereAmI? ReadWhereAmI() =>
        _document() is { IsRetired: false, IsEffective: true }
            ? ActiveReadback?.Invoke()
            : null;

    public bool CanWhereAmI => ReadWhereAmI() is not null;

    /// <summary>The verb: ONE event from the seam, rendered for the panel and
    /// announced through the document's seam — composed once, rendered
    /// twice by one deterministic renderer (IGO-31). False when the seam
    /// does not answer.</summary>
    public bool WhereAmI()
    {
        if (ReadWhereAmI() is not { } @event)
        {
            return false;
        }
        WhereAmIText = GraphAnnouncer.RenderLabel(@event);
        _document()?.AnnounceWhereAmI(@event);
        return true;
    }

    /// <summary>The chord arm: unconsumed when the effective document's
    /// seam does not answer, so the press falls through while the graph
    /// is inactive or the lineage is not quiescent (C-8).</summary>
    private bool WhereAmIFromKey() => WhereAmI();

    /// <summary>The panel's Close and Escape's rung 0: the text cleared, every
    /// pane's panel collapsing with it.</summary>
    internal void CloseWhereAmI() => WhereAmIText = null;

}
