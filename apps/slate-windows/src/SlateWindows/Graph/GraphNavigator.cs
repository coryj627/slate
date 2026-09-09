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
internal sealed class GraphNavigator
{
    private readonly GraphViewState _viewState;
    private readonly Func<GraphDocumentViewModel?> _document;
    private readonly Func<string?> _openAdmissionReason;
    private readonly Func<GraphPreset, GraphPresetOpenReport> _openForPreset;
    private readonly Dictionary<(Key Key, ModifierKeys Modifiers), Func<bool>> _chords = [];
    private IGraphSurfacePresenter? _presenter;

    /// <param name="viewState">The workspace's one view state (B2-1).</param>
    /// <param name="document">The seated document, or null while no graph tab exists.</param>
    /// <param name="openAdmissionReason">The workspace's open-admission seam
    /// (Term P3): null admits.</param>
    /// <param name="openForPreset">The workspace's preset funnel (C-3 (iii)–(v)):
    /// sets the arm, runs the open's mutation, reports the boundary's verdict.</param>
    internal GraphNavigator(
        GraphViewState viewState,
        Func<GraphDocumentViewModel?> document,
        Func<string?> openAdmissionReason,
        Func<GraphPreset, GraphPresetOpenReport> openForPreset)
    {
        ArgumentNullException.ThrowIfNull(viewState);
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(openAdmissionReason);
        ArgumentNullException.ThrowIfNull(openForPreset);
        _viewState = viewState;
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
    }

    private void AddChord(Key key, ModifierKeys modifiers, Func<bool> handler) =>
        _chords.Add((key, modifiers), handler);

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

    /// <summary>Rungs 1 and 2; rung 0 (an open panel) is the surface's own
    /// pre-emption and rung 3 is the shell's — the press bubbles.</summary>
    private bool EscapeFromKey()
    {
        if (_presenter is not { IsLive: true } presenter)
        {
            return false;
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
}
