// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Input;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit.Document;
using SlateWindows.Graph;
using SlateWindows.Reading;
using uniffi.slate_uniffi;

namespace SlateWindows;

internal enum WorkspaceOpenTarget
{
    CurrentTab,
    NewTab,
    SplitRight,
    SplitDown,
}

/// <summary>Where a request puts the keys at the workspace's edge.</summary>
internal enum WorkspaceFocusBoundary
{
    /// <summary>Ctrl+Alt+Left at the window's edge: the Files tree.</summary>
    Files,

    /// <summary>A command that shows a right-pane leaf and moves to it
    /// (Ctrl+R's review, Show History, the Connections and inspector
    /// routes): the keys go INTO the shown leaf — its own landing, else its
    /// first stop (W7-7 PR 4, #1247, R-5).</summary>
    RightPane,

    /// <summary>Ctrl+Alt+Right at the window's edge: arrival at the right
    /// pane by direction. Its stop is the rail's row unless the shown leaf
    /// owns a landing (the Connections anchor, the inspector's first stop)
    /// — Ctrl+Alt+Arrow's semantics are not a leaf reveal's (W7-6 §6,
    /// W7-7 §14).</summary>
    RightPaneEdge,
}

internal enum WorkspaceDirtyNavigationDecision
{
    Cancel,
    Save,
    Discard,
}

internal sealed record WorkspaceLeafOption(string Id, string Title);

internal abstract class BindableBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>What the tab did with a toggle request (adversarial
/// round 2): the legacy bool conflated "busy — refused, announced"
/// with "started", so review routing armed refresh state for
/// operations that never ran. Refused stays SILENT — the caller
/// owns that announcement (the reading-view precedent).</summary>
internal enum TabTaskToggle
{
    /// <summary>Not a saved markdown tab; nothing announced.</summary>
    Refused,

    /// <summary>An earlier toggle is still in flight; the refusal
    /// was announced here.</summary>
    RefusedBusy,

    /// <summary>The guarded toggle started; completion arrives as a
    /// whole-document refresh.</summary>
    Started,
}

internal sealed partial class WorkspaceTabViewModel : BindableBase, IDisposable
{
    private readonly VaultSession _session;
    private readonly Action<WorkspaceTabViewModel, EditorDocumentSyncEvent?>? _documentChanged;
    private readonly Action<EditorNavigationRequest>? _navigate;
    private readonly Action<string>? _activateTag;
    private readonly Action<string>? _activateTagFromReading;
    private readonly Action<A11yEvent> _announce;
    private readonly bool _ownsEditorPreferences;
    private readonly bool _startInteractionBackgroundWork;
    private readonly Func<string, string, string, uint?> _anchorResolver;
    private readonly Func<EditorInteractionWorkerKind, Exception?>?
        _interactionBackgroundFaultForTests;
    private AvalonDocumentBufferSession? _editorSession;
    private EditorInteractionCoordinator? _editorInteractions;
    private string _text = string.Empty;
    private string? _contentHash;
    private bool _isDirty;
    private bool _isMissingFromDisk;
    private string _status = string.Empty;
    private int _editorCaretOffset;
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
    private bool _disposed;
    // #1280: the epochs that retire an in-flight save's publication.
    // _itemEpoch moves when the tab is disposed or re-pointed at another
    // item; _saveEpoch moves then AND when the tab's file is renamed
    // (retarget) or deleted (invalidated) under the write.
    private int _saveEpoch;
    private int _itemEpoch;
    // #1280 (codex round 2a): the tab's queued save — admitted, not yet
    // started, joined by later requests — and its latest admitted save,
    // which a dirty-tab admission settles before it asks.
    private SaveBatch? _queuedSave;
    private Task<bool> _latestSave = Task.FromResult(true);
    // #1280 (codex round 4): the item epoch of the latest save that faulted
    // — past the D-10 outcomes, a step after adoption included — or -1. It
    // belongs to that item: a later successful save of it clears it, and an
    // item change leaves it behind.
    private int _faultedSaveItemEpoch = -1;
    private bool _taskToggleInFlight;
    private int _taskToggleGeneration;
    private int _anchorNavigationGeneration;
    private int _anchorNavigationPublishCountForTests;

    public WorkspaceTabViewModel(
        VaultSession session,
        WorkspaceTabState state,
        Action<WorkspaceTabViewModel, EditorDocumentSyncEvent?>? documentChanged = null,
        Action<EditorNavigationRequest>? navigate = null,
        Action<string>? activateTag = null,
        Action<string>? activateTagFromReading = null,
        Action<A11yEvent>? announce = null,
        EditorPreferencesViewModel? editorPreferences = null,
        bool? startInteractionBackgroundWork = null,
        Func<string, string, string, uint?>? anchorResolver = null,
        Func<EditorInteractionWorkerKind, Exception?>?
            interactionBackgroundFaultForTests = null)
    {
        _session = session;
        _documentChanged = documentChanged;
        _navigate = navigate;
        _activateTag = activateTag;
        _activateTagFromReading = activateTagFromReading;
        _announce = announce ?? (_ => { });
        _ownsEditorPreferences = editorPreferences is null;
        // Unspecified = the host decides (#1129): background work
        // needs a UI thread to come back to, and only the dispatcher
        // context serializes publishes with this thread, so a headless
        // host runs the work inline. The workspace resolves the same
        // way and hands its answer down; a tab built directly (the
        // tab-level facts) resolves here. An EXPLICIT value is honored.
        _startInteractionBackgroundWork = startInteractionBackgroundWork
            ?? SlateWindows.Panels.PanelWorkScheduler.CurrentContextIsUiDispatcher();
        _anchorResolver = anchorResolver ?? SlateUniffiMethods.LinkAnchorByteOffset;
        _interactionBackgroundFaultForTests = interactionBackgroundFaultForTests;
        // W7-7 PR 3 (#1246, R-4; codex PR 3 round 5): the spoken state follows
        // every path that announces the dirty or missing state.
        PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(IsDirty) or nameof(IsMissingFromDisk))
            {
                OnPropertyChanged(nameof(SpokenState));
            }
        };
        EditorPreferences = editorPreferences ?? new EditorPreferencesViewModel(_announce);
        Id = state.Id;
        Item = state.Item;
        Mode = state.Mode;
        PropsCollapsed = state.PropsCollapsed;
        ActiveCanvasSurface = state.ActiveCanvasSurface;
        Load();
        InitializeEditorSession();

        // A tab RESTORED with the persisted "reading" token must project
        // immediately: the projection previously started only from the
        // toggle path, so a session that ended in reading mode restored
        // as an empty surface ("Reading view document blank" — the
        // 2026-07-27 manual pass) with no keyboard route out but closing
        // the tab.
        if (IsReadingMode && IsMarkdown)
        {
            Reading = new ReadingContentViewModel(
                _session, this, _announce,
                synchronousForTests: !_startInteractionBackgroundWork);
            if (_startInteractionBackgroundWork)
            {
                Reading.Activate();
            }
            else
            {
                Reading.Refresh();
            }
        }
    }

    /// <summary>Test seam (W7-7 PR 7, codex PR 7 round 4 finding 3): the
    /// Reading model an in-place replace creates, before it projects.</summary>
    internal Action<ReadingContentViewModel>? ReadingCreatedForTests { get; set; }

    internal int AnchorNavigationPublishCountForTests =>
        Volatile.Read(ref _anchorNavigationPublishCountForTests);

    public Guid Id { get; }
    public WorkspaceItemState Item { get; private set; }
    public string? Mode { get; private set; }
    public bool? PropsCollapsed { get; }
    /// <summary>W6-1 PR A (contract A15): the persisted surface token,
    /// `"table" | "visual"` with outline written as ABSENT. The surface
    /// switcher drives it through
    /// <see cref="SetActiveCanvasSurface"/>.</summary>
    public string? ActiveCanvasSurface { get; private set; }
    public string Title => Item.Title;
    public TextDocument? EditorDocument => _editorSession?.Document;
    public AvalonDocumentBufferSession? EditorSession => _editorSession;
    public EditorInteractionCoordinator? EditorInteractions => _editorInteractions;
    internal string? SavedContentHash => _contentHash;

    private long _contentGeneration;

    /// <summary>W7-7 PR 7 (#1252, R-9): a monotonic generation of what this
    /// tab shows — bumped by every item replacement (a transient tab reused
    /// in place included), every text change, every baseline or dirty-state
    /// change and every missing or staleness mark. A rescan's worker read
    /// captures it with the tab; the reload applies only if it is
    /// unchanged in the dispatcher turn of the apply.</summary>
    internal long ContentGeneration => _contentGeneration;

    private void BumpContentGeneration() => _contentGeneration++;
    internal string? LoadFailure { get; private set; }
    public EditorPreferencesViewModel EditorPreferences { get; }
    public string EditorAutomationName =>
        $"{System.IO.Path.GetFileName(Path)} editor";
    public string Path => Item.Path;

    public bool IsMarkdown => Item.Kind == WorkspaceItemKind.Markdown;

    /// <summary>The persisted `"reading"` token (schema v1, G17).</summary>
    public bool IsReadingMode => string.Equals(Mode, "reading", StringComparison.Ordinal);
    public bool IsEditorVisible => IsMarkdown && !IsReadingMode;
    public bool IsReadingVisible => IsMarkdown && IsReadingMode;

    /// <summary>Created on first entry into reading mode; null before.</summary>
    public ReadingContentViewModel? Reading { get; private set; }

    /// <summary>Reading-surface navigation routes through the SAME seam
    /// the editor uses — one navigation path.</summary>
    internal void NavigateFromReading(EditorNavigationRequest request) =>
        _navigate?.Invoke(request);

    /// <summary>There are TWO tag paths since W5-2 (divergence SD-4,
    /// <c>29_search_overlay_contracts.md</c>): a reading-view tag opens
    /// the tag-scoped search overlay — mac parity,
    /// <c>ReadingLinkRouter.swift:243-258</c> — while an editor tag
    /// keeps the sidebar filter, because mac's editor renders tags as
    /// unclickable plain text and the editor path is a Windows-only
    /// affordance with no mac twin to converge on.</summary>
    internal void ActivateTagFromReading(string tag) =>
        _activateTagFromReading?.Invoke(tag);

    /// <summary>
    /// `slate.editor.toggleViewMode` (Ctrl+Shift+E, mac ⇧⌘E — W3-1
    /// #728): flip the persisted per-tab mode and (de)activate the
    /// reading projection. The reading VM is created lazily and kept
    /// across toggles so flipping back is a cache hit, not a re-parse
    /// (§10.1 memoization).
    /// </summary>
    public void ToggleViewMode()
    {
        if (!IsMarkdown)
        {
            return;
        }
        if (IsReadingMode)
        {
            Mode = null;
            Reading?.Deactivate();
        }
        else
        {
            Mode = "reading";
            if (Reading is null)
            {
                Reading = new ReadingContentViewModel(
                    _session, this, _announce,
                    synchronousForTests: !_startInteractionBackgroundWork);
                OnPropertyChanged(nameof(Reading));
            }
            if (_startInteractionBackgroundWork)
            {
                Reading.Activate();
            }
            else
            {
                Reading.Refresh();
            }
        }
        OnPropertyChanged(nameof(Mode));
        OnPropertyChanged(nameof(IsReadingMode));
        OnPropertyChanged(nameof(IsEditorVisible));
        OnPropertyChanged(nameof(IsReadingVisible));
    }
    public bool IsBase => Item.Kind == WorkspaceItemKind.Base;

    public bool IsSavedQueryTab => Item.Kind == WorkspaceItemKind.SavedQuery;

    public bool IsDashboardTab => Item.Kind == WorkspaceItemKind.Dashboard;

    /// <summary>W4-6 (#738): the shared per-source Bases document —
    /// attached by the workspace registry at tab creation (contract
    /// C3). Null on every other tab kind, and on Base tabs only
    /// between construction and attach.</summary>
    public Bases.BaseDocumentViewModel? Base { get; private set; }

    internal void AttachBaseDocument(Bases.BaseDocumentViewModel document)
    {
        Base = document;
        OnPropertyChanged(nameof(Base));
    }

    /// <summary>The dashboard tab's document (contract C12), attached
    /// by the same registry funnel as Base.</summary>
    public Bases.DashboardViewModel? Dashboard { get; private set; }

    internal void AttachDashboard(Bases.DashboardViewModel document)
    {
        Dashboard = document;
        OnPropertyChanged(nameof(Dashboard));
    }

    public bool IsDashboardVisible => IsDashboardTab;

    public bool IsBaseVisible => IsBase || IsSavedQueryTab;

    public bool IsCanvas => Item.Kind == WorkspaceItemKind.Canvas;

    /// <summary>W6-1 PR A (#745, contract A1): the shared per-path
    /// canvas document — attached by the workspace registry at tab
    /// creation through the ONE attach funnel. Null on every other tab
    /// kind, and on canvas tabs only between construction and
    /// attach.</summary>
    public Canvas.CanvasDocumentViewModel? Canvas { get; private set; }

    internal void AttachCanvasDocument(Canvas.CanvasDocumentViewModel document)
    {
        Canvas = document;
        if (ActiveCanvasSurface is "table" or "visual")
        {
            // The restored token seats the shared selection's surface
            // so a reopened tab lands where it left (contract A15).
            document.Selection.ActiveSurface = ActiveCanvasSurface == "table"
                ? uniffi.slate_uniffi.CanvasSurfaceKind.Table
                : uniffi.slate_uniffi.CanvasSurfaceKind.Visual;
        }
        OnPropertyChanged(nameof(Canvas));
    }

    /// <summary>Contract A15: outline persists as ABSENT (null), the
    /// mac sparse-map shape — the writer only ever emits the two
    /// non-outline tokens.</summary>
    internal void SetActiveCanvasSurface(string? surface)
    {
        ActiveCanvasSurface = surface is "table" or "visual" ? surface : null;
        OnPropertyChanged(nameof(ActiveCanvasSurface));
    }

    public bool IsCanvasVisible => IsCanvas;

    public bool IsGraph => Item.Kind == WorkspaceItemKind.Graph;

    /// <summary>W6-2 PR A (#746, contract A-1): the ONE graph document,
    /// seated by the attach funnel on every graph tab; null on every
    /// other tab kind.</summary>
    public Graph.GraphDocumentViewModel? Graph { get; private set; }

    internal void AttachGraphDocument(Graph.GraphDocumentViewModel document)
    {
        Graph = document;
        OnPropertyChanged(nameof(Graph));
    }

    public bool IsGraphVisible => IsGraph;

    /// <summary>W6-2 PR A: the placeholder retired for Graph — every kind
    /// with a surface is off this list.</summary>
    public bool IsPlaceholder =>
        !IsMarkdown && !IsBase && !IsSavedQueryTab && !IsDashboardTab && !IsCanvas && !IsGraph;
    public string KindLabel => Item.Kind switch
    {
        WorkspaceItemKind.Canvas => "Canvas",
        WorkspaceItemKind.Base => "Base",
        WorkspaceItemKind.SavedQuery => "Saved query",
        WorkspaceItemKind.Dashboard => "Dashboard",
        WorkspaceItemKind.Graph => "Graph",
        _ => "Note",
    };
    public string PlaceholderText =>
        $"{KindLabel} is docked in this workspace. Its full surface ships in its owning milestone.";

    public int EditorCaretOffset
    {
        get => _editorCaretOffset;
        set => SetField(
            ref _editorCaretOffset,
            Math.Clamp(value, 0, EditorDocument?.TextLength ?? 0));
    }

    public string Text
    {
        get => _editorSession?.Document.Text ?? _text;
        set
        {
            if (_editorSession is null)
            {
                ApplyEditorText(value);
                return;
            }

            _editorSession.ReplaceAll(value);
        }
    }

    public bool IsDirty
    {
        get => _isDirty;
        private set
        {
            if (SetField(ref _isDirty, value))
            {
                BumpContentGeneration();
                OnPropertyChanged(nameof(DirtyMarker));
                if (value)
                {
                    // The first dirty transition ends a transient tab for
                    // good: saved or undone back to clean later, it is kept.
                    IsTransient = false;
                }
            }
        }
    }

    public string DirtyMarker => IsDirty ? " •" : string.Empty;

    /// <summary>W7-7 (R-2, codex PR 2 round 4; spec review round 21): the
    /// group's transient tab — VS Code's preview tab — which a Files
    /// selection shows its note in and the next selection replaces. The flag
    /// clears permanently on the tab's first dirty transition (its own edit,
    /// or a peer's unsaved state it takes on), on an explicit open into the
    /// tab and on "open in a new tab"; nothing sets it again, and a restored
    /// tab is never transient. The selection never reads IsDirty.</summary>
    public bool IsTransient
    {
        get => _isTransient;
        internal set => SetField(ref _isTransient, value);
    }

    private bool _isTransient;

    public bool IsMissingFromDisk
    {
        get => _isMissingFromDisk;
        private set
        {
            if (SetField(ref _isMissingFromDisk, value))
            {
                BumpContentGeneration();
            }
        }
    }

    /// <summary>W7-7 PR 3 (#1246, R-4; codex PR 3 round 5): what the reader
    /// hears after the tab's name — its unsaved and missing states, "" for
    /// none. It is an INPUT to the tab strip's sibling rule (SiblingNames'
    /// StatePath: the rule joins it with ", " and checks the joined name),
    /// never appended after it: a dirty "draft" and a clean "draft, unsaved
    /// changes" would otherwise both read "draft, unsaved changes".</summary>
    public string SpokenState => (IsDirty, IsMissingFromDisk) switch
    {
        (false, false) => string.Empty,
        (true, false) => "unsaved changes",
        (false, true) => "missing from disk",
        (true, true) => "missing from disk, unsaved changes",
    };

    public string Status
    {
        get => _status;
        private set => SetField(ref _status, value);
    }

    public WorkspaceTabState Snapshot() =>
        // Sparse per-tab expansion (W4-4): collapsed persists as true,
        // expanded as absent; a header never materialized this session
        // keeps the restored value.
        new(
            Id,
            Item,
            Mode,
            Properties is { } properties
                ? (properties.IsExpanded ? null : true)
                : PropsCollapsed,
            ActiveCanvasSurface);

    public void ReplaceItem(WorkspaceItemState item)
    {
        // W7-7 PR 7 (#1252, R-9; codex's final merge-delta check, note 2;
        // contract 38 D-10): a faulted save belongs to its item — only a
        // later successful save of the item, or an actual item change,
        // leaves it behind. A same-item replace (a rescan's reload of a note
        // changed outside Slate, a re-seat, a restore's reload) is neither:
        // the fault moves to the new document with the item, and every gate
        // still refuses.
        bool carriesFault = LastSaveFaulted && item == Item;
        BumpContentGeneration();
        _saveEpoch++;
        _itemEpoch++;
        if (carriesFault)
        {
            _faultedSaveItemEpoch = _itemEpoch;
        }
        _taskToggleGeneration++;
        _taskToggleInFlight = false;
        _editorInteractions?.Dispose();
        Reading?.Dispose();
        _editorInteractions = null;
        _editorSession?.Dispose();
        _editorSession = null;
        Item = item;
        _text = string.Empty;
        _contentHash = null;
        // Navigation reuses the tab IN PLACE, so the previous item's
        // Bases/dashboard documents must not survive the replacement
        // (red team round 1: base B's title over base A's rows). The
        // caller re-attaches through AttachTabDocumentsIfNeeded and
        // releases the orphaned documents.
        Base = null;
        Dashboard = null;
        Canvas = null;
        // W6-2 PR A (contract A-9; the post-implementation pass's IPA-1):
        // a note opened INTO the graph's tab replaces the graph — the
        // document leaves the tab here and the release sweep retires it.
        Graph = null;
        OnPropertyChanged(nameof(Base));
        OnPropertyChanged(nameof(Dashboard));
        OnPropertyChanged(nameof(Canvas));
        OnPropertyChanged(nameof(Graph));
        // The staleness verdict belongs to the PREVIOUS note
        // (adversarial round 10): a reused current tab must not make
        // the replacement note inherit it — every identity guard
        // would falsely refuse the fresh rows.
        IsExternallyStale = false;
        _isDirty = false;
        _status = string.Empty;
        _isMissingFromDisk = false;
        _editorCaretOffset = 0;
        NotifyItemChanged();
        Load();
        InitializeEditorSession();

        // Navigation replaces the tab's item IN PLACE; a reading-mode tab
        // must re-project or the surface keeps showing the previous
        // note under the new title (measured 2026-07-27: activating
        // [[Target Note]] retitled the tab but kept reading the old
        // document — the disposed VM's last projection).
        Reading = null;
        if (IsReadingMode && IsMarkdown)
        {
            Reading = new ReadingContentViewModel(
                _session, this, _announce,
                synchronousForTests: !_startInteractionBackgroundWork);
            ReadingCreatedForTests?.Invoke(Reading);
            if (_reloadingForRescan)
            {
                RescanReadingPublication = Reading.ActivateForRescanAsync(
                    _rescanReload,
                    attachObserver: _startInteractionBackgroundWork);
            }
            else if (_startInteractionBackgroundWork)
            {
                Reading.Activate();
            }
            else
            {
                Reading.Refresh();
            }
        }
        OnPropertyChanged(nameof(Reading));
        OnPropertyChanged(nameof(IsReadingMode));
        OnPropertyChanged(nameof(IsEditorVisible));
        OnPropertyChanged(nameof(IsReadingVisible));
        OnPropertyChanged(nameof(Text));
        OnPropertyChanged(nameof(EditorDocument));
        OnPropertyChanged(nameof(EditorSession));
        OnPropertyChanged(nameof(EditorInteractions));
        OnPropertyChanged(nameof(EditorCaretOffset));
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(DirtyMarker));
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(IsMissingFromDisk));
    }

    public void RetargetPath(string path)
    {
        // #1280: an in-flight save wrote the OLD path; it must not speak or
        // set status for the renamed tab (codex round 1).
        _saveEpoch++;
        Item = Item with { Path = path };
        _editorInteractions?.InvalidateExternalState();
        IsMissingFromDisk = false;
        Status = string.Empty;
        NotifyItemChanged();
    }

    /// <summary>W7-7 PR 7 (#1252, R-9; codex's final merge-delta check,
    /// finding 1): the tab's file is back under the spelling the tab names
    /// while a save is admitted for it — the save's own create, say. The tab
    /// stops reporting the file missing and keeps its buffer, its document
    /// and its epochs, so the save still publishes to it (contract 38 D-10):
    /// no replace, and no epoch change that would retire the publication.</summary>
    internal void MarkBackOnDisk()
    {
        IsMissingFromDisk = false;
        Status = string.Empty;
    }

    /// <summary>Registry-item rename (saved queries, dashboards): the
    /// tab keeps its identity (Id) and retitles.</summary>
    public void RetargetName(string name)
    {
        Item = Item with { Name = name };
        NotifyItemChanged();
    }

    public void InvalidatePath()
    {
        // #1280: the file is gone under an in-flight save — its outcome
        // must not overwrite the missing-file status (codex round 1).
        _saveEpoch++;
        IsMissingFromDisk = true;
        Status = $"{Path} no longer exists on disk. Unsaved editor content is preserved.";
        _documentChanged?.Invoke(this, null);
    }

    public void InvalidateExternalState() =>
        _editorInteractions?.InvalidateExternalState();
    public void MirrorDocumentStateFrom(
        WorkspaceTabViewModel source,
        bool reconstructUndoHistory = true)
    {
        if (!IsMarkdown || !source.IsMarkdown || ReferenceEquals(this, source))
        {
            return;
        }

        BumpContentGeneration();
        _text = source._text;
        _contentHash = source._contentHash;
        IsExternallyStale = source.IsExternallyStale;
        _isDirty = source._isDirty;
        if (_isDirty)
        {
            IsTransient = false;
        }

        _isMissingFromDisk = source._isMissingFromDisk;
        _status = source._status;
        AvalonDocumentBufferSession? sourceSession = source._editorSession;
        if (_editorSession is not null && sourceSession is not null)
        {
            _editorSession.SynchronizeFromPeer(
                source.Text,
                sourceSession.SavedBaseline,
                reconstructUndoHistory);
        }
        OnPropertyChanged(nameof(Text));
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(DirtyMarker));
        OnPropertyChanged(nameof(IsMissingFromDisk));
        OnPropertyChanged(nameof(Status));
    }

    public void ApplyPeerDocumentEvent(
        WorkspaceTabViewModel source,
        EditorDocumentSyncEvent syncEvent)
    {
        if (!IsMarkdown || !source.IsMarkdown || ReferenceEquals(this, source))
        {
            return;
        }

        AvalonDocumentBufferSession session = _editorSession
            ?? throw new InvalidOperationException("A Markdown tab has no editor session.");
        BumpContentGeneration();
        switch (syncEvent)
        {
            case EditorDocumentUpdateStarted:
                session.BeginPeerUpdate();
                break;
            case EditorDocumentChange change:
                session.ApplyPeerEdit(change);
                OnPropertyChanged(nameof(Text));
                break;
            case EditorDocumentUpdateFinished:
                session.EndPeerUpdate();
                _contentHash = source._contentHash;
                _isDirty = source._isDirty;
                if (_isDirty)
                {
                    IsTransient = false;
                }

                _isMissingFromDisk = source._isMissingFromDisk;
                _status = source._status;
                if (!_isDirty)
                {
                    AvalonDocumentBufferSession sourceSession = source._editorSession
                        ?? throw new InvalidOperationException("A Markdown source tab has no editor session.");
                    session.MarkSaved(sourceSession.SavedBaseline);
                }

                OnPropertyChanged(nameof(IsDirty));
                OnPropertyChanged(nameof(DirtyMarker));
                OnPropertyChanged(nameof(IsMissingFromDisk));
                OnPropertyChanged(nameof(Status));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(syncEvent));
        }
    }

    /// <summary>Save the note and report whether it is saved (#1280). The
    /// core write runs on a worker; this caller — Save All, save-before-close
    /// and the dirty-navigation gate, each of which decides on the answer —
    /// waits in a nested dispatcher frame, so the dispatcher keeps pumping
    /// input, focus, the inline status and every notification while the file
    /// and index work runs. Anything may run inside that frame: every caller
    /// re-reads what it acts on after this returns (the pumped-wait
    /// invariant, contract 38 D-10). The explicit Save command never waits
    /// here: it requests the save and returns (<see cref="SaveAsync"/>).</summary>
    public bool Save() => PumpedWait.Result(_dispatcher, SaveAsync());

    /// <summary>
    /// Request a save (#1280). Saves are serialized through the workspace's
    /// coordinator — per tab and per file, across peer tabs — and COALESCED
    /// per tab (codex round 2a): at most one save is writing and one is
    /// queued, and a request made while one is queued joins it. The queued
    /// save captures the editor's state when it STARTS, so a joined request
    /// is served by a write that includes everything typed before it began;
    /// every joined request's <paramref name="onSaved"/> runs on that one
    /// publication, which confirms it with one NoteSaved when any joined
    /// request asked it to <paramref name="announce"/>. A request is for the
    /// item the tab shows when it is made: a queued save whose tab was
    /// re-pointed at another item before it started (a Files selection
    /// replacing a transient tab) is retired silently, and a later request
    /// queues its own save instead of joining it.
    /// </summary>
    internal Task<bool> SaveAsync(Action? onSaved = null, bool announce = false)
    {
        _dispatcher.VerifyAccess();
        if (_queuedSave is { } queued && queued.ItemEpoch == _itemEpoch)
        {
            queued.Join(onSaved, announce);
            return queued.Completion;
        }

        var batch = new SaveBatch(onSaved, announce, _itemEpoch);
        _queuedSave = batch;
        Task<bool> completion = Saves.Enqueue(
            WorkspaceSaveCoordinator.KeysFor(Id, IsMarkdown ? Path : null),
            ticket =>
            {
                if (ReferenceEquals(_queuedSave, batch))
                {
                    _queuedSave = null;
                }
                try
                {
                    StartSave(batch, ticket);
                }
                catch (Exception)
                {
                    // The coordinator fails the ticket (and logs it once).
                    RecordSaveFault(batch.ItemEpoch);
                    throw;
                }
            });
        if (completion.IsCompleted && ReferenceEquals(_queuedSave, batch))
        {
            // Refused before it could start (a closed coordinator).
            _queuedSave = null;
        }
        batch.Completion = completion;
        _latestSave = completion;
        return completion;
    }

    /// <summary>True while a save this tab admitted has not published.</summary>
    internal bool HasPendingSaves => !_latestSave.IsCompleted;

    /// <summary>True when the latest save of the item the tab shows faulted
    /// (#1280, codex round 4) — a fault after its write was adopted
    /// included, which leaves the tab clean. A gate that would pass a clean
    /// tab without asking fails closed on it: the note is not saved in the
    /// D-10 sense. A later successful save of the item clears it.</summary>
    internal bool LastSaveFaulted => !_disposed && _faultedSaveItemEpoch == _itemEpoch;

    private void RecordSaveFault(int itemEpoch) => _faultedSaveItemEpoch = itemEpoch;

    private void ClearSaveFault(int itemEpoch)
    {
        if (_faultedSaveItemEpoch == itemEpoch)
        {
            _faultedSaveItemEpoch = -1;
        }
    }

    /// <summary>Pump until every save this tab admitted — one admitted
    /// meanwhile included — has published (#1280, codex round 2a): a dirty
    /// tab's admission settles the tab's own saves before it asks, and again
    /// before it accepts Discard, so no admitted write lands after the user
    /// chose to discard. False only when the dispatcher shut down.</summary>
    internal bool SettleSaves()
    {
        _dispatcher.VerifyAccess();
        while (!_latestSave.IsCompleted)
        {
            if (!PumpedWait.Until(_dispatcher, _latestSave))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>One queued save and every request that joined it, for the
    /// item the tab showed when the first of them was made.</summary>
    private sealed class SaveBatch(Action? onSaved, bool announce, int itemEpoch)
    {
        private readonly List<Action> _onSaved = onSaved is null ? [] : [onSaved];

        internal int ItemEpoch { get; } = itemEpoch;

        internal bool Announce { get; private set; } = announce;

        internal Task<bool> Completion { get; set; } = Task.FromResult(false);

        internal IReadOnlyList<Action> OnSaved => _onSaved;

        internal void Join(Action? onSaved, bool announce)
        {
            if (onSaved is not null)
            {
                _onSaved.Add(onSaved);
            }
            Announce |= announce;
        }
    }

    /// <summary>The workspace's save coordinator; a tab built on its own
    /// (a tab-level fact) gets a private one.</summary>
    internal WorkspaceSaveCoordinator? SaveCoordinator { get; set; }

    private WorkspaceSaveCoordinator Saves =>
        SaveCoordinator ??= new WorkspaceSaveCoordinator(_dispatcher);

    /// <summary>#1280 test seam: runs on the save worker, before the core
    /// write — a test parks it there.</summary>
    internal Action? SaveWriteHookForTests { get; set; }

    /// <summary>#1280 test seam: runs on the save worker AFTER the core write
    /// landed, before its publication is queued — a test parks a landed
    /// write there.</summary>
    internal Action? SaveWrittenHookForTests { get; set; }

    /// <summary>#1280 test seam: runs on the dispatcher right after a landed
    /// write became the tab's baseline, before the rest of its publication —
    /// a fact throws here to fault a save whose tab is already clean.</summary>
    internal Action? SaveAdoptedHookForTests { get; set; }

    /// <summary>True once the tab is disposed — a pumped caller re-reads
    /// it after its frame.</summary>
    internal bool IsDisposed => _disposed;

    /// <summary>The editor's edit revision: a discard approval is pinned
    /// to it, so an edit typed after the approval is asked about again
    /// (#1280). -1 for a tab without an editor session.</summary>
    internal long EditRevision => _editorSession?.Revision ?? -1;

    /// <summary>The document the tab shows (#1280): it moves when the tab is
    /// re-pointed at another item or disposed, never when its file is
    /// renamed — a rename keeps the document and its edits. A prompt's
    /// answer is pinned to it and the edit revision.</summary>
    internal int ItemIdentity => _itemEpoch;

    /// <summary>The dispatcher half before the write: the snapshot, the
    /// integrity refusal and the repair lease; then the worker.</summary>
    private void StartSave(SaveBatch batch, WorkspaceSaveCoordinator.SaveTicket ticket)
    {
        // Disposed, or re-pointed at another item while the save waited its
        // turn: nothing of what was requested is here to save, and the
        // publication speaks only to the item it was requested for.
        if (_disposed || batch.ItemEpoch != _itemEpoch)
        {
            ticket.Complete(false);
            return;
        }
        if (!IsMarkdown || !IsDirty)
        {
            PublishSaved(batch.Announce, batch.OnSaved, Path);
            ClearSaveFault(batch.ItemEpoch);
            ticket.Complete(true);
            return;
        }

        EditorSaveSnapshot snapshot;
        try
        {
            snapshot = _editorSession?.PrepareSaveSnapshot()
                ?? new EditorSaveSnapshot(Text, Revision: -1);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // W7-7 R-7 (#1249; contract 38 D-10): the editor produced no
            // verified snapshot. ONE NoteSaveBlocked carries core's
            // integrity sentence, and the status shows the sentence that is
            // spoken. The exception's own text is host copy (or a runtime
            // message) and reaches neither; its type goes to the durable
            // log for diagnosis.
            HostLog.Write(HostDiagnosticEvent.EditorSaveIntegrityBlocked, exception);
            var blocked = new A11yEvent.NoteSaveBlocked(
                System.IO.Path.GetFileName(Path),
                SlateUniffiMethods.EditorIntegrityDetail());
            Status = SlateUniffiMethods.A11yRender(blocked).Text;
            _documentChanged?.Invoke(this, null);
            _announce(blocked);
            ticket.Complete(false);
            return;
        }

        // Ordinary saves route through the same file-before-index
        // core pipeline as task toggles (adversarial round 20): a
        // post-write failure leaves disk newer than the rolled-back
        // task index with no revision counter moved — the mutation
        // lease covers the interval, and a non-conflict failure
        // converts into the pending repair atomically, so a manual
        // checkbox edit can never resurrect ghost task rows either.
        // The lease now spans the off-dispatcher write (#1280).
        var request = new SaveRequest(
            Path,
            snapshot.Text,
            snapshot.Revision,
            _contentHash,
            _saveEpoch,
            _itemEpoch,
            TaskRepairs,
            [.. batch.OnSaved],
            batch.Announce);
        request.Repairs?.BeginMutation(request.Path);
        Action? hook = SaveWriteHookForTests;
        Action? written = SaveWrittenHookForTests;
        Task<SaveWrite> write = Task.Run(() =>
        {
            SaveWrite landed = WriteForSave(request, hook);
            written?.Invoke();
            return landed;
        });
        // The worker phase is tracked on its own (contract 35 A-1): teardown
        // joins it before the session is disposed, with no dependence on the
        // dispatcher callback below.
        Saves.TrackWorker(write);
        write.ContinueWith(
            finished => _dispatcher.BeginInvoke(
                DispatcherPriority.Normal,
                new Action(() =>
                {
                    try
                    {
                        bool saved = PublishSave(finished, request);
                        if (saved)
                        {
                            ClearSaveFault(request.ItemEpoch);
                        }
                        ticket.Complete(saved);
                    }
                    catch (Exception exception)
                    {
                        RecordSaveFault(request.ItemEpoch);
                        ticket.Fail(exception);
                    }
                })),
            CancellationToken.None,
            TaskContinuationOptions.None,
            TaskScheduler.Default);
    }

    private sealed record SaveRequest(
        string Path,
        string Text,
        long Revision,
        string? ExpectedContentHash,
        int Epoch,
        int ItemEpoch,
        Panels.TaskIndexRepairCoordinator? Repairs,
        IReadOnlyList<Action> OnSaved,
        bool Announce);

    private sealed record SaveWrite(string NewContentHash, string? Caveat);

    /// <summary>The worker half: the synchronous core write, off the
    /// dispatcher (locked decision 05 §4.1).</summary>
    private SaveWrite WriteForSave(SaveRequest request, Action? hook)
    {
        hook?.Invoke();
        if (request.ExpectedContentHash is null)
        {
            // #1077 (contract I8): a tab with no content hash has never
            // loaded bytes from the path it names — it has no basis to
            // overwrite whatever sits there now (the parked tab of a
            // deleted note whose name came back under another spelling
            // is the case that loses data). Its save is a CREATE:
            // success when the path is free, DestinationExists — the
            // conflict arm below — when it is not; never a silent
            // overwrite. Core's documented "null hash = unconditional"
            // save is unchanged; this is the host's rule. A post-publish
            // index failure is a LANDED write (#1123): the bytes are on
            // disk, so the tab is saved and says so with the caveat.
            return _session.CreateExclusiveReporting(request.Path, request.Text) switch
            {
                CreateExclusiveOutcome.Committed committed =>
                    new SaveWrite(committed.Report.NewContentHash, null),
                CreateExclusiveOutcome.PublishedUnindexed published => new SaveWrite(
                    published.ContentHash,
                    CreateOutcomes.PublishedUnindexedCaveat(
                        System.IO.Path.GetFileName(request.Path), published.ErrorMessage)),
                _ => throw new InvalidOperationException("unknown create outcome"),
            };
        }
        return new SaveWrite(
            _session.SaveText(request.Path, request.Text, request.ExpectedContentHash)
                .NewContentHash,
            null);
    }

    /// <summary>The dispatcher half after the write: settle the lease,
    /// then publish state and exactly one D-10 outcome — only to the tab and
    /// item the write was captured for (#1280, codex round 1). A tab
    /// disposed or re-pointed at another item during the write takes no
    /// state and says nothing; the bytes it wrote are on disk. A tab whose
    /// file was renamed or deleted under the write says nothing and keeps
    /// the status the rename or deletion set — a write that landed before a
    /// rename moved the file, a CAS save or a create alike (codex round 3),
    /// is adopted silently as the renamed tab's baseline, so its next save
    /// starts from the bytes on disk instead of reporting a false conflict
    /// or creating over its own file.</summary>
    private bool PublishSave(Task<SaveWrite> write, SaveRequest request)
    {
        Panels.TaskIndexRepairCoordinator? repairs = request.Repairs;
        bool leaseSettled = false;
        try
        {
            SaveWrite saved = write.GetAwaiter().GetResult();
            repairs?.EndMutation(request.Path, indexConsistent: saved.Caveat is null);
            leaseSettled = true;
            if (_disposed || request.ItemEpoch != _itemEpoch)
            {
                return true;
            }
            if (request.Epoch != _saveEpoch
                || !string.Equals(request.Path, Path, StringComparison.Ordinal))
            {
                if (!IsMissingFromDisk)
                {
                    AdoptLandedWrite(request, saved);
                    _documentChanged?.Invoke(this, null);
                }
                return true;
            }

            AdoptLandedWrite(request, saved);
            SaveAdoptedHookForTests?.Invoke();
            Status = saved.Caveat is null
                ? $"Saved {System.IO.Path.GetFileName(request.Path)}."
                : $"Saved {System.IO.Path.GetFileName(request.Path)}. {saved.Caveat}";
            _documentChanged?.Invoke(this, null);
            PublishSaved(request.Announce, request.OnSaved, request.Path);
            return true;
        }
        catch (VaultException exception)
        {
            // A refused create wrote nothing: the index is as consistent as
            // it was, the same as a WriteConflict refusal.
            repairs?.EndMutation(
                request.Path,
                indexConsistent: exception
                    is VaultException.WriteConflict
                    or VaultException.DestinationExists);
            leaseSettled = true;
            if (_disposed
                || request.Epoch != _saveEpoch
                || !string.Equals(request.Path, Path, StringComparison.Ordinal))
            {
                return false;
            }
            String filename = System.IO.Path.GetFileName(request.Path);
            if (exception is VaultException.WriteConflict)
            {
                // W7-7 R-7 (#1249; contract 38 D-10 as amended, OD-5): a
                // conflict speaks core's sentence and shows the same one.
                // The binding's message for it is two content hashes and a
                // modification time, which nobody should hear or read.
                var conflict = new A11yEvent.NoteSaveConflict(filename);
                Status = SlateUniffiMethods.A11yRender(conflict).Text;
                _documentChanged?.Invoke(this, null);
                _announce(conflict);
                return false;
            }
            // R-7: any other failure keeps its detail — core's rendering of
            // the error (vault_error_detail), never the binding's
            // field-labelled message ("@message=…") — and the status shows
            // the sentence that is spoken.
            var blocked = new A11yEvent.NoteSaveBlocked(
                filename,
                SlateUniffiMethods.VaultErrorDetail(exception));
            Status = SlateUniffiMethods.A11yRender(blocked).Text;
            _documentChanged?.Invoke(this, null);
            _announce(blocked);
            return false;
        }
        finally
        {
            // Fail-closed on exception types the arms above miss -
            // a leaked lease bars every task query forever.
            if (!leaseSettled)
            {
                repairs?.EndMutation(request.Path, indexConsistent: false);
            }
        }
    }

    /// <summary>The one confirmation of a save (#1280): NoteSaved once, when
    /// any request the save served asked for it (the explicit Save command),
    /// then every served request's callback — however many joined.</summary>
    private void PublishSaved(bool announce, IReadOnlyList<Action> onSaved, string path)
    {
        if (announce)
        {
            _announce(new A11yEvent.NoteSaved(System.IO.Path.GetFileName(path)));
        }
        foreach (Action callback in onSaved)
        {
            callback();
        }
    }

    /// <summary>The landed write's bytes become the tab's saved state: the
    /// content hash, and the editor's baseline — behind any edit typed while
    /// the write ran, which keeps the tab dirty.</summary>
    private void AdoptLandedWrite(SaveRequest request, SaveWrite saved)
    {
        _contentHash = saved.NewContentHash;
        IsExternallyStale = false;
        if (_editorSession is { } session)
        {
            _text = request.Text;
            if (session.Revision == request.Revision)
            {
                session.MarkSaved(request.Text);
            }
            else
            {
                // Typed while the write ran: disk holds the snapshot,
                // the editor keeps the newer text and stays dirty.
                session.MarkSavedBehindEdits(request.Text);
            }
            IsDirty = !session.IsAtSavedBaseline;
        }
        else
        {
            IsDirty = !string.Equals(_text, request.Text, StringComparison.Ordinal);
        }
    }

    public void Dispose()
    {
        _disposed = true;
        _saveEpoch++;
        _itemEpoch++;
        _taskToggleGeneration++;
        // W4-4: refuse new property-header work before the session
        // this tab's header reads from goes away.
        ShutdownProperties();
        // The reading projection goes first: it observes the editor
        // document and schedules background FFI work against this
        // tab's session — both torn down below.
        Reading?.Dispose();
        Reading = null;
        _editorInteractions?.Dispose();
        _editorInteractions = null;
        _editorSession?.Dispose();
        _editorSession = null;
        if (_ownsEditorPreferences)
        {
            EditorPreferences.Dispose();
        }
    }

    // Reading observation is deliberately NOT paused here: Deactivate
    // also fires when focus merely moves to another split pane while
    // this tab stays mounted and visible — pausing there would freeze
    // a visible projection against peer-pane edits. The projection
    // pauses on the true "left the surface" signal instead
    // (ReadingContentViewModel.OnSurfaceDetached, raised by the
    // surface rebind that hides it), and Dispose still tears it down.
    /// <summary>Tab switch-away housekeeping. clearBaseQuickFilter is
    /// FALSE when only the active GROUP changed (focus moved to
    /// another pane): the tab is still mounted and visible, and C5's
    /// fourth leg names "activating a different tab", not a pane
    /// focus move — clearing there silently expanded a grid the user
    /// was reading (red team round 2; the Reading comment above
    /// documents the same hazard).</summary>
    public void Deactivate(bool clearBaseQuickFilter = true)
    {
        _editorInteractions?.CloseTransientUi();
        // C5's fourth transiency leg (red team round 1: unimplemented):
        // activating a different tab clears the quick filter. The
        // shared document re-executes unfiltered SILENTLY (INV-4) —
        // Refresh, never ApplyQuickFilter, which would announce a
        // count nobody asked for.
        if (clearBaseQuickFilter && Base is { } baseDocument)
        {
            bool executedFilter = baseDocument.QuickFilterActive;
            if (executedFilter || baseDocument.QuickFilterText.Length > 0)
            {
                baseDocument.ClearQuickFilterState();
                if (executedFilter)
                {
                    baseDocument.Refresh();
                }
            }
        }
    }

    /// <summary>Toggle a task through this tab's guarded splice path.
    /// <paramref name="completion"/> (adversarial round 3) fires on
    /// the dispatcher at the toggle's TERMINAL state — with the save
    /// report when disk changed, the error when it didn't, and
    /// whether this tab was still alive to publish — EVEN when the
    /// tab was disposed mid-flight: a review-originated toggle must
    /// neither complete silently nor leave its refresh armed just
    /// because the user closed the originating tab.</summary>
    public TabTaskToggle ToggleTask(
        TaskItem task,
        Action<A11yEvent> announce,
        Action<SaveReport?, VaultException?, string?, bool>? completion = null)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(announce);
        if (!IsMarkdown || IsDirty)
        {
            return TabTaskToggle.Refused;
        }
        if (_taskToggleInFlight)
        {
            announce(new A11yEvent.HostComposed(
                "A task update is already in progress.",
                A11yPriority.Medium));
            return TabTaskToggle.RefusedBusy;
        }

        _taskToggleInFlight = true;
        int generation = ++_taskToggleGeneration;
        string path = Path;
        string? expectedHash = _contentHash;
        long revision = _editorSession!.Revision;
        EditorSavedBaseline baseline = _editorSession.SavedBaseline;
        string nextStatus = task.Completed ? " " : "x";
        _ = Task.Run(() => PerformTaskToggle(
            generation,
            path,
            expectedHash,
            revision,
            baseline,
            task,
            nextStatus,
            announce,
            completion));
        return TabTaskToggle.Started;
    }

    private sealed record TaskToggleOutcome(
        SaveReport? Report,
        VaultException? Error,
        string? UpdatedText,
        string? PostFailureDiskHash = null);

    /// <summary>Test seam (adversarial round 11): runs INSIDE the
    /// toggle worker after the core write succeeded — throwing here
    /// simulates the core's real partial-failure window (file
    /// written, index commit failed) with an actual landed write.</summary>
    internal Action? TaskToggleFaultForTests { get; set; }

    /// <summary>Test seam (W7-7 PR 7, round 24): runs INSIDE the toggle
    /// worker BEFORE the core write, so a fact can hold a toggle in flight,
    /// its write not yet committed, while a rescan applies the note's
    /// delta.</summary>
    internal Action? TaskToggleBeforeWriteForTests { get; set; }

    /// <summary>True from a task toggle's start until its dispatcher-side
    /// publish re-baselines the tab (W7-7 PR 7, round 24): a rescan's
    /// clean-tab reload waits for it, because a reload in between would
    /// move the revision the splice verifies and discard the undo
    /// history.</summary>
    internal bool IsTaskToggleInFlight => _taskToggleInFlight;

    /// <summary>The workspace's shared repair quarantine (adversarial
    /// round 19): set at tab creation so EVERY toggle route through
    /// this tab — panel, review, editor, reading view — leases the
    /// path around the session write. Null only in tab-level unit
    /// tests that construct tabs directly.</summary>
    internal Panels.TaskIndexRepairCoordinator? TaskRepairs { get; set; }

    /// <summary>Disk hash read back after a failed toggle
    /// (adversarial round 11): the core writes the FILE before
    /// committing the index, so an error without a SaveReport does
    /// NOT mean disk is unchanged. WriteConflict refuses BEFORE any
    /// write, so it skips the read; unreadable disk reports null
    /// (unknown).</summary>
    private string? ReadBackDiskHashAfterFailure(
        string path, VaultException? error)
    {
        if (error is VaultException.WriteConflict)
        {
            return null;
        }
        try
        {
            return _session.ReadNoteParts(path).ContentHash;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return null;
        }
    }

    private void PerformTaskToggle(
        int generation,
        string path,
        string? expectedHash,
        long revision,
        EditorSavedBaseline baseline,
        TaskItem task,
        string nextStatus,
        Action<A11yEvent> announce,
        Action<SaveReport?, VaultException?, string?, bool>? completion = null)
    {
        TaskToggleOutcome outcome;
        Panels.TaskIndexRepairCoordinator? repairs = TaskRepairs;
        try
        {
            string updatedText = ApplyTaskStatusToBaseline(
                baseline.Text,
                task,
                nextStatus);
            // The mutation LEASE brackets the session write
            // (adversarial round 19): the stale-index interval
            // starts at the file write, not at the dispatcher-side
            // completion — a clean ticket taken in between must
            // refuse or invalidate. A non-conflict failure converts
            // the lease into the pending repair atomically.
            repairs?.BeginMutation(path);
            bool leaseSettled = false;
            try
            {
                try
                {
                    TaskToggleBeforeWriteForTests?.Invoke();
                    SaveReport report = _session.ToggleTaskStatus(
                        path,
                        task.Ordinal,
                        nextStatus,
                        expectedHash);
                    TaskToggleFaultForTests?.Invoke();
                    repairs?.EndMutation(path, indexConsistent: true);
                    leaseSettled = true;
                    outcome = new TaskToggleOutcome(report, null, updatedText);
                }
                catch (Exception inner) when (
                    inner is VaultException or InvalidOperationException)
                {
                    repairs?.EndMutation(
                        path, indexConsistent: inner is VaultException.WriteConflict);
                    leaseSettled = true;
                    VaultException error = inner as VaultException
                        ?? new VaultException.InvalidArgument(inner.Message);
                    outcome = new TaskToggleOutcome(
                        null,
                        error,
                        null,
                        ReadBackDiskHashAfterFailure(path, error));
                }
            }
            finally
            {
                // A leaked lease bars every task query FOREVER - an
                // exception type outside the arms above (a runtime
                // panic surfacing through the FFI, say) must still
                // settle it, fail-closed.
                if (!leaseSettled)
                {
                    repairs?.EndMutation(path, indexConsistent: false);
                }
            }
        }
        catch (InvalidOperationException exception)
        {
            // The splice failed BEFORE any session write: a certain
            // no-write, no lease was taken.
            outcome = new TaskToggleOutcome(
                null,
                new VaultException.InvalidArgument(exception.Message),
                null);
        }

        if (_dispatcher.HasShutdownStarted || _dispatcher.HasShutdownFinished)
        {
            return;
        }

        _dispatcher.BeginInvoke(
            DispatcherPriority.Background,
            new Action(() => PublishTaskToggle(
                generation,
                path,
                expectedHash,
                revision,
                task,
                nextStatus,
                announce,
                outcome,
                completion)));
    }

    private void PublishTaskToggle(
        int generation,
        string path,
        string? expectedHash,
        long revision,
        TaskItem task,
        string nextStatus,
        Action<A11yEvent> announce,
        TaskToggleOutcome outcome,
        Action<SaveReport?, VaultException?, string?, bool>? completion = null)
    {
        // The completion outlives THIS TAB deliberately (adversarial
        // round 3): the dispatcher belongs to the app, so a caller
        // that needs the terminal state — the review's pending
        // refresh, the disposed-tab announcement — hears it even
        // when disposal suppresses the tab-state publish. It fires
        // AFTER the tab publish so the source tab and its mirrored
        // peers are already re-baselined when the caller reconciles.
        bool tabPublished = !_disposed && generation == _taskToggleGeneration;
        if (tabPublished)
        {
            PublishTaskToggleThroughTab(
                path, expectedHash, revision, task, nextStatus, announce, outcome);
        }
        completion?.Invoke(
            outcome.Report, outcome.Error, outcome.PostFailureDiskHash, tabPublished);
    }

    private void PublishTaskToggleThroughTab(
        string path,
        string? expectedHash,
        long revision,
        TaskItem task,
        string nextStatus,
        Action<A11yEvent> announce,
        TaskToggleOutcome outcome)
    {
        _taskToggleInFlight = false;
        if (outcome.Error is VaultException.WriteConflict)
        {
            announce(new A11yEvent.TaskToggleConflict(System.IO.Path.GetFileName(path)));
            return;
        }
        if (outcome.Error is VaultException error)
        {
            Status = $"Task could not be toggled: {error.Message}";
            announce(new A11yEvent.HostComposed(Status, A11yPriority.High));
            return;
        }

        SaveReport report = outcome.Report!;
        string updatedText = outcome.UpdatedText!;
        if (!string.Equals(Path, path, StringComparison.Ordinal)
            || !string.Equals(_contentHash, expectedHash, StringComparison.Ordinal)
            || _editorSession is null
            || _editorSession.Revision != revision
            || IsDirty)
        {
            Status = "Task toggled on disk, but the editor changed. Reopen the note before editing.";
            _documentChanged?.Invoke(this, null);
            announce(new A11yEvent.HostComposed(Status, A11yPriority.High));
            return;
        }

        int statusStartUtf16 = _editorSession.ByteToUtf16(task.CheckboxStartByte + 1);
        int statusEndUtf16 = _editorSession.ByteToUtf16(task.CheckboxEndByte - 1);
        int statusLengthUtf16 = statusEndUtf16 - statusStartUtf16;
        if (statusLengthUtf16 <= 0
            || !string.Equals(
                _editorSession.Document.GetText(statusStartUtf16, statusLengthUtf16),
                task.StatusChar,
                StringComparison.Ordinal))
        {
            Status = "Task toggled on disk, but the editor no longer matches it. Reopen the note before editing.";
            _documentChanged?.Invoke(this, null);
            announce(new A11yEvent.HostComposed(Status, A11yPriority.High));
            return;
        }

        _editorSession.Document.Replace(statusStartUtf16, statusLengthUtf16, nextStatus);
        _text = updatedText;
        _contentHash = report.NewContentHash;
        IsExternallyStale = false;
        _editorSession.MarkSavedAfterVerifiedDelta(
            new EditorSavedBaseline(
                updatedText,
                checked((uint)updatedText.Length),
                report.NewContentHash),
            revision + 1);
        IsDirty = false;
        Status = task.Completed ? "Task reopened." : "Task completed.";
        _documentChanged?.Invoke(this, null);
        announce(new A11yEvent.HostComposed(Status, A11yPriority.Medium));
    }

    /// <summary>True when the vault change stream reported this
    /// file modified and the INDEX now carries a different content
    /// hash than this tab's saved baseline (adversarial round 9):
    /// the buffer is clean but obsolete, so row hashes born from the
    /// same baseline match it vacuously — every snapshot-identity
    /// guard must refuse until the tab re-baselines. Cleared by the
    /// re-baselining writes (save, verified toggle splice, peer
    /// mirror) and re-derived on every Modified event.</summary>
    internal bool IsExternallyStale
    {
        get => _isExternallyStale;
        private set
        {
            _isExternallyStale = value;
            BumpContentGeneration();
        }
    }

    private bool _isExternallyStale;

    /// <summary>Re-derive <see cref="IsExternallyStale"/> against
    /// the index. Own saves also flow through the change stream
    /// (the #802 single emission seat), so this must COMPARE, never
    /// assume: a just-saved tab's baseline equals the index and
    /// derives false. An unreadable index leaves the flag alone.</summary>
    internal void RefreshExternalStaleness()
    {
        if (!IsMarkdown || _disposed)
        {
            return;
        }
        string indexedHash;
        try
        {
            indexedHash = _session.NoteTasks(Path, 1).ContentHash;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return;
        }
        IsExternallyStale = indexedHash.Length > 0
            && !string.Equals(_contentHash, indexedHash, StringComparison.Ordinal);
    }

    /// <summary>A task toggle wrote this tab's file WITHOUT
    /// publishing through it (adversarial round 3: the review's
    /// tabless route decided before this tab raced open, or the
    /// originating same-path tab was disposed mid-flight). A buffer
    /// still holding pre-write content is a stale editor over
    /// changed disk — reuse the splice path's divergence honesty,
    /// verbatim, and drop the interaction caches.</summary>
    internal void ReconcileAfterExternalTaskWrite(
        string newContentHash, Action<A11yEvent> announce)
    {
        ArgumentNullException.ThrowIfNull(announce);
        if (!IsMarkdown || _disposed)
        {
            return;
        }
        if (string.Equals(_contentHash, newContentHash, StringComparison.Ordinal))
        {
            // The write landed exactly where this tab already is.
            IsExternallyStale = false;
            return;
        }
        // Definitionally stale against the just-written hash: the
        // identity guards refuse until the tab re-baselines (round 9).
        IsExternallyStale = true;
        _editorInteractions?.InvalidateExternalState();
        Status = "Task toggled on disk, but the editor no longer matches it. Reopen the note before editing.";
        announce(new A11yEvent.HostComposed(Status, A11yPriority.High));
    }

    private static string ApplyTaskStatusToBaseline(
        string baseline,
        TaskItem task,
        string nextStatus)
    {
        byte[] source = Encoding.UTF8.GetBytes(baseline);
        int start = checked((int)task.CheckboxStartByte + 1);
        int end = checked((int)task.CheckboxEndByte - 1);
        if (start < 0 || end < start || end > source.Length)
        {
            throw new InvalidOperationException("The task checkbox range is invalid.");
        }

        string prefix = Encoding.UTF8.GetString(source, 0, start);
        string suffix = Encoding.UTF8.GetString(source, end, source.Length - end);
        return string.Concat(prefix, nextStatus, suffix);
    }
    public bool NavigateToAnchor(
        LinkAnchor anchor,
        string? resolvedAnchorText,
        Action<A11yEvent> announce,
        Func<bool>? isStillActive = null)
    {
        ArgumentNullException.ThrowIfNull(anchor);
        ArgumentNullException.ThrowIfNull(announce);
        if (_editorInteractions is null || _editorSession is null)
        {
            return false;
        }

        int generation = ++_anchorNavigationGeneration;
        string path = Path;
        string source = Text;
        long revision = _editorSession.Revision;
        int caretOffset = EditorCaretOffset;
        _ = Task.Run(() =>
        {
            int? targetUtf16 = null;
            try
            {
                uint? targetByte = _anchorResolver(source, anchor.Kind, anchor.Text);
                if (targetByte is uint byteOffset)
                {
                    targetUtf16 = checked((int)SlateUniffiMethods.TextByteToUtf16(
                        source,
                        byteOffset));
                }
            }
            catch (Exception exception) when (
                exception is not OutOfMemoryException
                    and not StackOverflowException
                    and not AccessViolationException)
            {
            }

            if (!_dispatcher.HasShutdownStarted && !_dispatcher.HasShutdownFinished)
            {
                _dispatcher.BeginInvoke(
                    DispatcherPriority.Background,
                    new Action(() => PublishAnchorNavigation(
                        generation,
                        path,
                        revision,
                        caretOffset,
                        anchor,
                        resolvedAnchorText,
                        targetUtf16,
                        announce,
                        isStillActive)));
            }
        });
        return true;
    }

    private void PublishAnchorNavigation(
        int generation,
        string path,
        long revision,
        int caretOffset,
        LinkAnchor anchor,
        string? resolvedAnchorText,
        int? targetUtf16,
        Action<A11yEvent> announce,
        Func<bool>? isStillActive)
    {
        Interlocked.Increment(ref _anchorNavigationPublishCountForTests);
        if (_disposed
            || generation != _anchorNavigationGeneration
            || !string.Equals(Path, path, StringComparison.Ordinal)
            || _editorSession is null
            || _editorSession.Revision != revision
            || EditorCaretOffset != caretOffset
            || isStillActive?.Invoke() == false)
        {
            return;
        }

        if (targetUtf16 is not int target)
        {
            announce(string.Equals(anchor.Kind, "block", StringComparison.Ordinal)
                ? new A11yEvent.HostComposed(
                    $"Block {anchor.Text} was not found.",
                    A11yPriority.Medium)
                : new A11yEvent.HeadingNotFound());
            return;
        }

        if (string.Equals(anchor.Kind, "block", StringComparison.Ordinal))
        {
            announce(new A11yEvent.HostComposed(
                $"Scrolled to block {anchor.Text}.",
                A11yPriority.Medium));
        }
        else
        {
            // Speak the heading's display text when the caller resolved
            // one — anchors sent by slug (outline rows, wikilinks with
            // slug anchors) would otherwise announce the slug itself.
            announce(new A11yEvent.ScrolledToHeading(
                resolvedAnchorText ?? anchor.Text));
        }
        _editorInteractions!.RequestCaret(target);
    }
    private void InitializeEditorSession()
    {
        if (IsMarkdown)
        {
            _editorSession = new AvalonDocumentBufferSession(_text, ApplyEditorSyncEvent);
            _editorInteractions = new EditorInteractionCoordinator(
                _session,
                this,
                _navigate,
                _activateTag,
                _announce,
                _startInteractionBackgroundWork,
                _interactionBackgroundFaultForTests);
        }
    }

    private void ApplyEditorSyncEvent(EditorDocumentSyncEvent syncEvent)
    {
        BumpContentGeneration();
        if (syncEvent is EditorDocumentChange)
        {
            OnPropertyChanged(nameof(Text));
        }
        else if (syncEvent is EditorDocumentUpdateFinished)
        {
            AvalonDocumentBufferSession session = _editorSession
                ?? throw new InvalidOperationException("A Markdown tab has no editor session.");
            IsDirty = !session.IsAtSavedBaseline;
        }

        _documentChanged?.Invoke(this, syncEvent);
    }

    private void ApplyEditorText(string text)
    {
        if (SetField(ref _text, text, nameof(Text)))
        {
            BumpContentGeneration();
            IsDirty = true;
            _documentChanged?.Invoke(this, null);
        }
    }

    // W7-7 PR 7 (round 28): text a worker read for an in-place reload.
    private string? _preloadedText;

    /// <summary>W7-7 PR 7 (round 28): the in-place replace, from text a
    /// worker already read — the rescan's clean-tab reload. Its
    /// <paramref name="rescan"/> token (codex PR 7 round 4, finding 3) makes
    /// a reading-mode tab's new projection the rescan's: silent, under that
    /// token, and exposed as <see cref="RescanReadingPublication"/>.</summary>
    internal void ReplaceItemWithReadText(
        WorkspaceItemState item,
        string text,
        CancellationToken rescan = default)
    {
        _preloadedText = text;
        _rescanReload = rescan;
        _reloadingForRescan = true;
        RescanReadingPublication = Task.CompletedTask;
        try
        {
            ReplaceItem(item);
        }
        finally
        {
            _preloadedText = null;
            _reloadingForRescan = false;
            _rescanReload = default;
        }
    }

    // W7-7 PR 7 (codex PR 7 round 4, finding 3): set while a rescan replaces
    // this tab in place.
    private bool _reloadingForRescan;
    private CancellationToken _rescanReload;

    /// <summary>The last rescan reload's reading projection — completing at
    /// its terminal publication, faulted by its failure; completed when the
    /// tab is not in reading mode.</summary>
    internal Task RescanReadingPublication { get; private set; } = Task.CompletedTask;

    private void Load()
    {
        LoadFailure = null;
        if (!IsMarkdown)
        {
            return;
        }

        try
        {
            // W7-7 PR 7 (round 28): a rescan's reload hands in text its
            // worker already read, so no core read runs on the dispatcher.
            _text = _preloadedText ?? _session.ReadText(Path);
            _contentHash = SlateUniffiMethods.EditorTextContentHash(_text);
            _isDirty = false;
        }
        catch (VaultException exception)
        {
            LoadFailure = exception.Message;
            Status = $"Could not open {Path}: {exception.Message}";
        }
    }

    private void NotifyItemChanged()
    {
        BumpContentGeneration();
        OnPropertyChanged(nameof(Item));
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(EditorAutomationName));
        OnPropertyChanged(nameof(Path));
        OnPropertyChanged(nameof(IsMarkdown));
        OnPropertyChanged(nameof(IsPlaceholder));
        OnPropertyChanged(nameof(KindLabel));
        OnPropertyChanged(nameof(PlaceholderText));
        // The item's KIND can change on an in-place replacement
        // (note → base, base → dashboard, …); the surface-visibility
        // bindings must re-evaluate or the wrong surface stays up
        // (red team round 1 blocker).
        OnPropertyChanged(nameof(IsBase));
        OnPropertyChanged(nameof(IsSavedQueryTab));
        OnPropertyChanged(nameof(IsDashboardTab));
        OnPropertyChanged(nameof(IsCanvas));
        OnPropertyChanged(nameof(IsBaseVisible));
        OnPropertyChanged(nameof(IsDashboardVisible));
        OnPropertyChanged(nameof(IsCanvasVisible));
        // W6-2 PR A (IPA-1): graph → note is the same in-place kind change;
        // without these the graph surface stayed Visible over the opened
        // note (the journey's Enter step, deterministic on CI and locally).
        OnPropertyChanged(nameof(IsGraph));
        OnPropertyChanged(nameof(IsGraphVisible));
    }
}

internal sealed class WorkspaceGroupViewModel : BindableBase
{
    private readonly WorkspaceViewModel _owner;
    private WorkspaceTabViewModel? _activeTab;

    public WorkspaceGroupViewModel(WorkspaceViewModel owner, Guid id)
    {
        _owner = owner;
        Id = id;
    }

    public Guid Id { get; }
    public WorkspaceViewModel Owner => _owner;
    public ObservableCollection<WorkspaceTabViewModel> Tabs { get; } = [];

    public WorkspaceTabViewModel? ActiveTab
    {
        get => _activeTab;
        set
        {
            if (ReferenceEquals(_activeTab, value))
            {
                return;
            }

            _activeTab?.Deactivate();
            if (SetField(ref _activeTab, value))
            {
                _owner.Activate(this, value);
            }
        }
    }

    internal void RestoreActive(WorkspaceTabViewModel? tab)
    {
        _activeTab = tab;
        OnPropertyChanged(nameof(ActiveTab));
    }

    /// <summary>W7-7 PR 3 (#1246, R-4; codex PR 3 round 8, OD-9): the tab's
    /// name as this group's tab strip reads it — its title among the group's
    /// tabs, told apart by path and then place, with its unsaved and missing
    /// states — the ONE spoken-name authority for a tab. The strip declares
    /// the same rule (NamePath Title, DistinguisherPath Path, Noun tab,
    /// StatePath SpokenState); every announcement that names a tab (focus,
    /// close, reopen, the tab bar, the editor pane) speaks this, so two tabs
    /// the strip reads apart are never announced alike. A tab the group does
    /// not hold reads its bare title.</summary>
    public string SpokenNameOf(WorkspaceTabViewModel tab)
    {
        ArgumentNullException.ThrowIfNull(tab);
        int index = -1;
        for (int position = 0; position < Tabs.Count && index < 0; position++)
        {
            if (ReferenceEquals(Tabs[position], tab))
            {
                index = position;
            }
        }
        return index < 0
            ? tab.Title
            : SiblingNames.Compose(
                [.. Tabs.Select(item => (string?)item.Title)],
                [.. Tabs.Select(item => (string?)item.Path)],
                "tab",
                [.. Tabs.Select(item => (string?)item.SpokenState)])[index];
    }
}

internal sealed class WorkspacePaneNodeViewModel : BindableBase
{
    private double _weight = 1;

    public WorkspacePaneNodeViewModel(WorkspaceGroupViewModel group)
    {
        Group = group;
    }

    public WorkspacePaneNodeViewModel(string axis)
    {
        Axis = axis;
    }

    public WorkspaceGroupViewModel? Group { get; }
    public string? Axis { get; }
    public bool IsGroup => Group is not null;
    public bool IsSplit => Group is null;
    public bool IsHorizontal => Axis == "horizontal";
    public ObservableCollection<WorkspacePaneNodeViewModel> Children { get; } = [];

    public double Weight
    {
        get => _weight;
        set => SetField(ref _weight, Math.Clamp(
            value,
            WorkspacePersistence.MinGroupWeight,
            1));
    }
}

/// <summary>
/// W1 workspace host: state transitions stay in this model; WPF renders native
/// TabControl peers and recursively arranged split groups.
/// </summary>
internal sealed partial class WorkspaceViewModel : BindableBase, IDisposable
{
    private readonly VaultSession _session;
    /// <summary>The vault's absolute root — the one place that composes
    /// an absolute path from a vault-relative one (W6-1 PR A: the canvas
    /// media hand-off to the shell).</summary>
    private readonly string _vaultRoot;
    private readonly Action<A11yEvent> _announce;
    /// <summary>W6-1 PR A (contract A5): the canvas coalescer's post
    /// seam. It queues RENDERED lines — the window's winner is decided
    /// after the render — so it cannot use the event seam above.</summary>
    private readonly Action<RenderedAnnouncement> _announceRendered;
    private readonly Panels.TaskIndexRepairCoordinator _taskIndexRepairs;
    /// <summary>#1280: every tab's saves, serialized per canonical path,
    /// with the workers tracked for teardown's join.</summary>
    private readonly WorkspaceSaveCoordinator _saves =
        new(System.Windows.Threading.Dispatcher.CurrentDispatcher);
    private readonly Func<WorkspaceTabViewModel, WorkspaceItemState, WorkspaceDirtyNavigationDecision>
        _dirtyNavigationDecision;
    private readonly Func<WorkspaceTabViewModel, WorkspaceDirtyNavigationDecision>
        _dirtyCloseDecision;
    private WorkspaceLeafOption _activeLeaf;
    private bool _isRightPaneVisible = true;
    private readonly bool _startInteractionBackgroundWork;

    /// <summary>The window's answers for F6 (W7-6); null until the window
    /// attaches, in which case a press does nothing. Collaborator state,
    /// not a verb — it sat in the command block by accident (final
    /// review, #1240).</summary>
    internal IShellRegionHost? ShellRegionHost { get; set; }

    /// <summary>W4-6 (#738): the per-source Bases document registry —
    /// one document per byte-exact path, shared by every tab on that
    /// source (contract C3). Documents whose last tab closed are shut
    /// down by <see cref="ReleaseUnreferencedBaseDocuments"/> at every
    /// tab-close funnel, and the whole registry at Dispose (INV-2).</summary>
    private readonly Dictionary<string, Bases.BaseDocumentViewModel> _baseDocuments =
        new(StringComparer.Ordinal);

    /// <param name="seedRow">W7-7 PR 7 (#1252, R-9 over R-5), codex's
    /// merge-delta check (finding 2): the rescan's re-seat seeds a document
    /// constructed here with the retired document's reader row — its
    /// identity, PR 3's row key (the note's path, and a task's place in it)
    /// — so the surfaces' first publication reconciles its selection onto
    /// that row, as a rename's retarget carries a board's (CD-32).</param>
    internal Bases.BaseDocumentViewModel BaseDocumentFor(string path, bool load = true, BasesRow? seedRow = null)
    {
        string key = "file:" + path;
        if (!_baseDocuments.TryGetValue(key, out Bases.BaseDocumentViewModel? document))
        {
            document = new Bases.BaseDocumentViewModel(
                _session,
                path,
                _announce,
                synchronousForTests: !_startInteractionBackgroundWork);
            _baseDocuments[key] = document;
            InstallBaseDocumentSeams(document);
            if (seedRow is not null)
            {
                document.SelectedRow = seedRow;
            }

            // W7-7 PR 7 (codex PR 7 round 5, fix 3): a caller that loads the
            // document itself (the rescan's re-seat) constructs it unloaded.
            if (load)
            {
                document.Load();
            }
        }
        return document;
    }

    internal Bases.BaseDocumentViewModel BaseDocumentForSavedQuery(string id, string name)
    {
        string key = "query:" + id;
        if (!_baseDocuments.TryGetValue(key, out Bases.BaseDocumentViewModel? document))
        {
            document = Bases.BaseDocumentViewModel.ForSavedQuery(
                _session,
                id,
                name,
                _announce,
                synchronousForTests: !_startInteractionBackgroundWork);
            _baseDocuments[key] = document;
            InstallBaseDocumentSeams(document);
            document.Load();
        }
        return document;
    }

    private void ReleaseUnreferencedBaseDocuments()
    {
        if (_baseDocuments.Count == 0)
        {
            return;
        }
        var live = new HashSet<string>(StringComparer.Ordinal);
        foreach (WorkspaceTabViewModel tab in Groups.SelectMany(group => group.Tabs))
        {
            if (tab.IsBase)
            {
                live.Add("file:" + tab.Path);
            }
            else if (tab.Item.Kind == WorkspaceItemKind.SavedQuery
                && tab.Item.Id is { Length: > 0 } id)
            {
                live.Add("query:" + id);
            }
        }
        foreach (string key in _baseDocuments.Keys.Where(k => !live.Contains(k)).ToList())
        {
            Bases.BaseDocumentViewModel retired = _baseDocuments[key];
            retired.Shutdown();
            TrackRetiredBasesWork(retired.WhenHandleClosed());
            _baseDocuments.Remove(key);
        }
    }

    public WorkspaceViewModel(
        VaultSession session,
        string vaultRoot,
        Func<IReadOnlyList<string>> expandedDirectoryPaths,
        Action<A11yEvent> announce,
        Func<WorkspaceTabViewModel, WorkspaceItemState, WorkspaceDirtyNavigationDecision>?
            dirtyNavigationDecision = null,
        Func<WorkspaceTabViewModel, WorkspaceDirtyNavigationDecision>?
            dirtyCloseDecision = null,
        bool? startInteractionBackgroundWork = null,
        AppPreferencesStore? preferencesStore = null,
        Func<string, bool>? externalOpener = null,
        Action<RenderedAnnouncement>? announceRendered = null,
        Func<int>? lifecycleGeneration = null)
    {
        _session = session;
        _persistence = new WorkspacePersistence(vaultRoot);
        _vaultRoot = vaultRoot;
        _expandedDirectoryPaths = expandedDirectoryPaths;
        _announce = announce;
        _announceRendered = announceRendered ?? (_ => { });
        // Rule A / AD-8 (IPB-1): the lifecycle's generation is a
        // CONSTRUCTION input — the restore below can seat a persisted graph
        // tab and start its first load before this constructor returns, and
        // that load's token must carry the generation the lifecycle will
        // compare it against. A host without a lifecycle reads a constant.
        LifecycleGeneration = lifecycleGeneration ?? (static () => 0);
        // Unspecified = the host decides (#1129). Background
        // interaction work needs a UI thread to come back to: every
        // panel's publish posts to the SynchronizationContext captured
        // at construction, and only WPF's dispatcher context serializes
        // those posts with this thread. Headless — a null context, or a
        // test host's pool-dispatching one — runs the work INLINE
        // instead; otherwise publishes race the constructing thread
        // (HistoryViewModel's reload publish enumerated _loaded while a
        // tab switch on the test thread cleared it — intermittent on
        // CI, in a fact that never touched the panel). An EXPLICIT
        // value is honored either way: a fact that wants production
        // scheduling headlessly says so (the citation interleaving
        // suite) and owns the drain/seam discipline that makes it safe.
        _startInteractionBackgroundWork = startInteractionBackgroundWork
            ?? SlateWindows.Panels.PanelWorkScheduler.CurrentContextIsUiDispatcher();
        _dirtyNavigationDecision = dirtyNavigationDecision
            ?? ((_, _) => WorkspaceDirtyNavigationDecision.Cancel);
        _dirtyCloseDecision = dirtyCloseDecision
            ?? (_ => WorkspaceDirtyNavigationDecision.Cancel);
        EditorPreferences = new EditorPreferencesViewModel(
            _announce, preferencesStore: preferencesStore);
        // W6-1 PR C (#745), contract C13: app-level canvas verbosity,
        // read live by every open canvas document.
        CanvasPreferences = new Canvas.CanvasPreferencesViewModel(preferencesStore);
        // Math prefs are session-honored (get_math_blocks reads them on
        // every call): apply the persisted values once at construction,
        // then on every change re-render any open reading projections
        // (nothing else re-fetches the math artifact — no text changed).
        _session.SetMathPrefs(EditorPreferences.CurrentMathPrefs);
        EditorPreferences.MathPrefsChanged += prefs =>
        {
            _session.SetMathPrefs(prefs);
            foreach (WorkspaceTabViewModel tab in
                Groups.SelectMany(group => group.Tabs))
            {
                tab.Reading?.InvalidateForPrefsChange();
            }
        };
        _activeLeaf = Leaves[0];
        // ONE repair quarantine shared by every task surface
        // (adversarial round 15): a path whose post-write index
        // repair failed is known stale, and no surface may query it
        // past another surface's quarantine.
        _taskIndexRepairs = new Panels.TaskIndexRepairCoordinator(session);
        // The right-pane link/structure leaves (W4-2). Constructed
        // BEFORE Restore so the activation funnels can sync into it.
        Panels = new Panels.RightPanePanelsViewModel(
            session,
            announce,
            (path, target) =>
            {
                bool navigated = false;
                RunWorkspaceMutation(() => navigated = OpenPathCore(path, target));
                return navigated;
            },
            _externalOpener = externalOpener ?? DefaultExternalOpener,
            (anchor, resolvedText) =>
            {
                WorkspaceGroupViewModel group = ActiveGroup;
                WorkspaceTabViewModel? tab = group.ActiveTab;
                _ = tab?.NavigateToAnchor(
                    anchor,
                    resolvedText,
                    _announce,
                    () => ReferenceEquals(ActiveGroup, group)
                        && ReferenceEquals(group.ActiveTab, tab));
            },
            TogglePanelTask,
            ScrollToPanelTaskIfCurrent,
            repairs: _taskIndexRepairs,
            synchronousForTests: !_startInteractionBackgroundWork);
        // The vault-wide Tasks Review leaf (W4-3): vault-lifetime
        // state, deliberately NOT keyed on the active note.
        TasksReview = new Panels.TasksReviewViewModel(
            session,
            announce,
            TryActivateTaskRow,
            TryToggleTaskInOpenTab,
            repairs: _taskIndexRepairs,
            synchronousForTests: !_startInteractionBackgroundWork);
        // Round 3: a tab can open for a file BETWEEN the review's
        // NoOpenTab route decision and its direct write landing —
        // the workspace re-checks at write completion. Round 17:
        // that raced-open tab's NOTE PANEL can also have finished a
        // pre-write read — NoteSaved re-snapshots it (and ignores
        // non-active paths).
        TasksReview.DiskWriteLanded = (path, newContentHash) =>
        {
            ReconcileTabsAfterDirectTaskWrite(path, newContentHash);
            NotePersisted(path);
        };
        // Round 15: a repair landing inside a review load worker
        // refreshes the note panel too — both surfaces converge.
        TasksReview.RepairLanded = path => NotePersisted(path);
        // W4-5: the citations suite. Citations is note-scoped (fed by
        // SyncPanels); Bibliography is vault-scoped and loads lazily
        // on rail reveal. Both must exist before Restore/SyncPanels so
        // the first note selection can publish into them.
        Citations = new Panels.CitationsPanelViewModel(
            session, announce, synchronousForTests: !_startInteractionBackgroundWork);
        Bibliography = new Panels.BibliographyViewModel(
            session, synchronousForTests: !_startInteractionBackgroundWork);
        // W6-2 PR B (B-1; A-10 as amended, BD-12): the workspace's one
        // graph relay FIRST, then the Connections leaf's document — BEFORE
        // Restore and the first SyncPanels so the root follow reaches it —
        // and the seeded initial mount (rule C, Term 3(a)).
        _graphRelay = NewGraphRelay();
        // W6-2 PR B2 (B2-1; A-1 and spec R-B as amended by the owner on
        // 2026-09-06): the workspace's ONE view state, beside the relay,
        // handed to the graph document and the leaf, dropped with the
        // workspace — the instance census counts this one construction.
        _graphViewState = NewGraphViewState();
        // W6-2 PR C (C-9, C-10): the ONE preferences object, read at
        // construction (the mac's eager load), seeding the view state through
        // the one mapper; a level change drops the relay's pending row line.
        _graphPreferences = NewGraphPreferences();
        _graphPreferences.VerbosityChanged += () => _graphRelay.DropPendingNavigation();
        _graphViewState.ApplyQuery(GraphPreferencesViewModel.VisibilityQueryOf(_graphPreferences.CurrentConfig.Filters));
        _graphViewState.Groups = _graphPreferences.CurrentConfig.Groups;
        // W6-2 PR D (rule M, Term M1; C-D6 closed): the persisted mode seeds
        // the view state — a persisted `diagram` is restored; the writers
        // census names this line and the document's SetMode alone.
        _graphViewState.Mode = _graphPreferences.CurrentConfig.Mode;
        // W6-2 PR E (Term I6; E-12 i): the ONE inspector view model, after
        // the preferences and the view state and before the navigator and
        // the graph document — the instance census counts this one
        // construction; it holds no copy of either source.
        _graphInspector = NewGraphInspector();
        // W6-2 PR C (C-1): the ONE navigator, after the view state and the
        // preferences and before the first document and the leaf — the
        // instance census counts this one construction.
        _graphNavigator = NewGraphNavigator();
        Connections = NewConnectionsLeaf();
        SeedInitialConnectionsMount();
        // W4-7: the history document — note-scoped (fed by SyncPanels),
        // silent on its own (HINV-4); flows live in the History
        // coordinator partial. The compare-vs-current hash: a markdown
        // tab supplies its loaded buffer hash (the mac
        // currentNoteContentHash); every OTHER path-backed kind rides
        // the loaded list's head hash — H12 forbids extension
        // special-casing, so per-row Compare works on .canvas/.base
        // histories too.
        History = new Panels.HistoryViewModel(
            session, synchronousForTests: !_startInteractionBackgroundWork);
        History.CurrentContentHashProvider = () =>
            ActiveGroup.ActiveTab is { } historyTab
            && IsPathBacked(historyTab.Item)
            && string.Equals(historyTab.Path, History.Path, StringComparison.Ordinal)
                ? (historyTab.IsMarkdown
                    ? historyTab.SavedContentHash
                    : History.HeadContentHash)
                : null;
        History.ShowChangesSinceOpen =
            EditorPreferences.HistoryShowChangesSinceOpen;
        EditorPreferences.HistoryShowChangesSinceOpenChanged +=
            enabled => History.ShowChangesSinceOpen = enabled;
        InstallHistorySeams();
        // W4-8: the sync-diagnostics document — VAULT-scoped, so it is
        // fed by neither SyncPanels nor any note funnel (SDINV-7: the
        // trigger set is {vault open, explicit refresh, watcher fire}).
        // The initial probe is deliberately NOT started here: SD4
        // requires arm-then-probe, so the vault lifecycle fires the
        // first Reload after the marker watcher is live.
        SyncDiagnostics = new Panels.SyncDiagnosticsViewModel(
            session, synchronousForTests: !_startInteractionBackgroundWork);
        InstallSyncDiagnosticsSeams();
        // Both leaves read through the session's bibliography sources,
        // so neither may query before seeding SETTLES — and the
        // bibliography also branches on how it settled.
        // BOTH leaves branch on the outcome, not just wait for it: a
        // failed seed must stop either of them presenting core's
        // surviving previous-session data as authoritative.
        Citations.AttachSeed(_bibliographySeed);
        Bibliography.AttachSeed(_bibliographySeed);
        // Seed the vault's bibliography ONCE per open, BEFORE the first
        // note selection can start a citation render. Silent on both
        // success and failure — the leaf's notice region is the
        // surface (W4-5 contract 5).
        SeedBibliographySources(synchronousForTests: !_startInteractionBackgroundWork);
        (_root, _activeGroup) = Restore(_persistence.Load());
        SyncPanels();
        // W6-2 PR B (rule C, Term 3(a), B-1): the first sync was the initial
        // value, not a change; launch with the pane visible and the leaf
        // active is the seeded mount's ONE load, consumed here.
        FinishConnectionsConstruction();

        CloseTabCommand = new RelayCommand(
            parameter => RunWorkspaceMutation(() => CloseTab(parameter)),
            parameter => parameter is WorkspaceTabViewModel);
        CloseActiveTabCommand = new RelayCommand(
            _ => RunWorkspaceMutation(() => CloseTab(ActiveGroup.ActiveTab)),
            _ => ActiveGroup.ActiveTab is not null);
        DuplicateTabCommand = new RelayCommand(
            _ => RunWorkspaceMutation(DuplicateActiveTab),
            _ => ActiveGroup.ActiveTab is { Item.Kind: not WorkspaceItemKind.Graph });
        ReopenClosedTabCommand = new RelayCommand(
            _ => RunWorkspaceMutation(ReopenClosedTab),
            _ => _closedTabs.Count > 0);
        ToggleReadingModeCommand = new RelayCommand(
            _ => RunWorkspaceMutation(ToggleActiveViewMode),
            _ => ActiveGroup.ActiveTab?.IsMarkdown == true);
        MoveTabLeftCommand = new RelayCommand(
            _ => RunWorkspaceMutation(() => MoveActiveTab(-1)),
            _ => CanMoveActiveTab(-1));
        MoveTabRightCommand = new RelayCommand(
            _ => RunWorkspaceMutation(() => MoveActiveTab(1)),
            _ => CanMoveActiveTab(1));
        NextTabCommand = new RelayCommand(
            _ => RunWorkspaceMutation(() => CycleTab(1)),
            _ => ActiveGroup.Tabs.Count > 1);
        PreviousTabCommand = new RelayCommand(
            _ => RunWorkspaceMutation(() => CycleTab(-1)),
            _ => ActiveGroup.Tabs.Count > 1);
        SplitRightCommand = new RelayCommand(
            _ => RunWorkspaceMutation(() => SplitActive("horizontal")),
            _ => CanSplitActive());
        SplitDownCommand = new RelayCommand(
            _ => RunWorkspaceMutation(() => SplitActive("vertical")),
            _ => CanSplitActive());
        ClosePaneCommand = new RelayCommand(
            _ => RunWorkspaceMutation(CloseActivePane),
            _ => Groups.Count > 1);
        FocusPaneLeftCommand = new RelayCommand(_ => FocusDirectionalPane("horizontal", -1), _ => true);
        FocusPaneRightCommand = new RelayCommand(_ => FocusDirectionalPane("horizontal", 1), _ => true);
        FocusPaneAboveCommand = new RelayCommand(_ => FocusDirectionalPane("vertical", -1), _ => true);
        FocusPaneBelowCommand = new RelayCommand(_ => FocusDirectionalPane("vertical", 1), _ => true);
        // W7-6 (#1240): F6 / Shift+F6 cycle SHELL REGIONS, not editor
        // splits (Ctrl+Alt+Arrows keep the splits). Always executable: the
        // ring exists whenever the workspace does; the modal no-op is
        // decided per press, not by CanExecute.
        FocusNextPaneCommand = new RelayCommand(_ => CycleShellRegion(1), _ => true);
        FocusPreviousPaneCommand = new RelayCommand(_ => CycleShellRegion(-1), _ => true);
        GrowPaneCommand = new RelayCommand(_ => ResizeActivePane(0.05), _ => Groups.Count > 1);
        ShrinkPaneCommand = new RelayCommand(_ => ResizeActivePane(-0.05), _ => Groups.Count > 1);
        SaveActiveCommand = new RelayCommand(_ => SaveActive(), _ => ActiveGroup.ActiveTab?.IsMarkdown == true);
        ToggleRightPaneCommand = new RelayCommand(
            _ =>
            {
                IsRightPaneVisible = !IsRightPaneVisible;
                // W6-2 PR B (rule C, Term 3(a)): the reveal route's end.
                ConsumePendingMount();
            },
            _ => true);
        OpenTasksReviewCommand = new RelayCommand(_ => OpenTasksReview(), _ => true);
    }

    public event EventHandler<string>? FileOpened;
    public event EventHandler<string>? EditorTagActivated;

    /// <summary>W5-2 SD-4: a reading-view tag activation, bound for the
    /// tag-scoped search overlay (the lifecycle subscribes). Split from
    /// <see cref="EditorTagActivated"/> — see
    /// <see cref="WorkspaceTabViewModel.ActivateTagFromReading"/> for
    /// why the two gestures no longer share a seam.</summary>
    public event EventHandler<string>? ReadingTagActivated;

    public static IReadOnlyList<WorkspaceLeafOption> Leaves { get; } =
    [
        new("outline", "Outline"),
        new("backlinks", "Backlinks"),
        new("outgoingLinks", "Outgoing links"),
        new("connections", "Connections"),
        // W6-2 PR E (Term I1): the graph inspector's leaf, its title T37.
        new("inspector", GraphPhrase.InspectorName),
        new("embeds", "Embeds"),
        new("math", "Math"),
        new("code", "Code"),
        new("diagrams", "Diagrams"),
        new("tasks", "Tasks"),
        new("tasksReview", "Tasks Review"),
        new("history", "History"),
        new("citations", "Citations"),
        new("bibliography", "Bibliography"),
        new("queries", "Queries"),
        new("basesDock", "Base dock"),
        new("syncDiagnostics", "Sync"),
    ];
    public IReadOnlyList<WorkspaceLeafOption> LeafOptions => Leaves;
    public EditorPreferencesViewModel EditorPreferences { get; }

    /// <summary>W6-1 PR C (#745): the canvas announcement verbosity
    /// (t0 §1.2), read live at every canvas announce.</summary>
    public Canvas.CanvasPreferencesViewModel CanvasPreferences { get; }

    /// <summary>The W4-2 link/structure leaf data (backlinks, outgoing
    /// links, outline, embeds).</summary>
    /// <summary>Whether interaction work runs in the background (the
    /// dispatcher-hosted app) or inline (every headless host) — the
    /// #1129 rule, pinned by PanelThreadingDisciplineTests.</summary>
    internal bool StartsInteractionBackgroundWorkForTests =>
        _startInteractionBackgroundWork;

    public Panels.RightPanePanelsViewModel Panels { get; }

    public Panels.TasksReviewViewModel TasksReview { get; }

    /// <summary>W4-5: the note-scoped citations leaf.</summary>
    public Panels.CitationsPanelViewModel Citations { get; }

    /// <summary>W4-5: the vault-scoped bibliography leaf.</summary>
    public Panels.BibliographyViewModel Bibliography { get; }

    /// <summary>Re-derive the panels' active note from the workspace —
    /// called from every activation funnel (tab activation, pane focus,
    /// workspace mutations). Same-path calls are no-ops in the panels
    /// VM, so over-calling is safe and refetch-free.</summary>
    internal void SyncPanels()
    {
        // W4-6: the dock follows the active note (contract C12).
        BasesDockFollowActiveNote();
        Panels.NoteChanged(
            ActiveGroup.ActiveTab is { IsMarkdown: true } tab ? tab.Path : null);
        // W4-5: the citations leaf follows the same active-note funnel;
        // same-path calls are no-ops, so over-calling is refetch-free.
        Citations.NoteChanged(
            ActiveGroup.ActiveTab is { IsMarkdown: true } citedTab ? citedTab.Path : null);
        // W6-2 PR B (rule C, Terms 3(d)/(g); B-19 iv): the Connections
        // leaf's root follows the same funnel — recorded inside a mutation,
        // reconciled once at its boundary; immediate outside one.
        SyncConnectionsRoot();
        // W4-7: history follows ANY path-backed tab (contract H12 — no
        // extension filtering; .canvas/.base histories are first-class).
        History.NoteChanged(
            ActiveGroup.ActiveTab is { } historyTab
            && IsPathBacked(historyTab.Item)
                ? historyTab.Path
                : null);
        // W4-4: the properties header attaches at activation, in the
        // Reading posture — background work in the app, inline in
        // tests (the flag decides the VM's mode at first creation,
        // so a test's later EnsureActiveTabProperties call gets the
        // same synchronous instance).
        EnsureActiveTabProperties(
            synchronousForTests: !_startInteractionBackgroundWork);
        // W6-2 PR A (rule L, Term 1): the graph follows the effective tab
        // through THIS funnel and nothing else.
        GraphFollowActiveTab();
    }

    /// <summary>External links launch through the shell (the default
    /// browser / mail client); the panels VM allowlists schemes before
    /// this runs.</summary>
    private static bool DefaultExternalOpener(string target)
    {
        try
        {
            _ = System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(target)
                {
                    UseShellExecute = true,
                });
            return true;
        }
        catch (Exception exception) when (
            exception is System.ComponentModel.Win32Exception
                or InvalidOperationException
                or System.IO.FileNotFoundException)
        {
            return false;
        }
    }

    /// <summary>W7-7 PR 4b (#1247; the sweep's G21, AR-59): whether the leaf
    /// is being chosen by an arrow on the rail — the rail's row, taking the
    /// keys, names the leaf, and the authored line would repeat it. The
    /// window answers; null (no window) answers no.</summary>
    internal Func<bool>? IsChoosingLeafByArrow { get; set; }

    public WorkspaceLeafOption ActiveLeaf
    {
        get => _activeLeaf;
        set
        {
            if (value is not null && SetField(ref _activeLeaf, value))
            {
                // Silent on the rail's arrow route only (G21, AR-59).
                if (IsChoosingLeafByArrow?.Invoke() != true)
                {
                    _announce(new A11yEvent.LeafPanelShown(value.Title));
                }
                // W6-2 PR E (Term I2): the inspector toggle's checked state follows.
                NotifyGraphInspectorShownChanged();
                // Rail reveal of the review is an idempotent snapshot
                // load (mac ensureVaultTasksLoaded); only the review
                // COMMAND forces a fresh page.
                if (string.Equals(value.Id, "tasksReview", StringComparison.Ordinal))
                {
                    TasksReview.EnsureLoaded();
                }
                // W4-5: same idempotent-reveal posture — revealing the
                // bibliography must never re-query the whole vault.
                if (string.Equals(value.Id, "bibliography", StringComparison.Ordinal))
                {
                    Bibliography.EnsureLoaded();
                }
                // W4-6: revealing the queries leaf refreshes its lists
                // (the mac onAppear refresh) - idempotent reads only.
                if (string.Equals(value.Id, "queries", StringComparison.Ordinal))
                {
                    RefreshBaseQueries();
                }
                // W4-7: the reveal refresh (contract H11) — page-one
                // reload of the active note's list, idempotent; the
                // ShowHistoryPanel command covers the already-active
                // re-invoke.
                if (string.Equals(value.Id, "history", StringComparison.Ordinal))
                {
                    History.Reload();
                }
                // W6-2 PR B (rule C, Term 3(b)): the leaf-shown transition,
                // after the shell's line — a switch loads only when stale.
                OnLeafShown(value);
                Persist();
            }
        }
    }

    /// <summary>W4-3 (mac openTasksReview, ⌘R → Ctrl+R): reveal the
    /// review leaf, load a FRESH first page, announce, and move
    /// focus to the pane.</summary>
    public void OpenTasksReview()
    {
        WorkspaceLeafOption leaf = Leaves.First(
            option => string.Equals(option.Id, "tasksReview", StringComparison.Ordinal));
        if (!IsRightPaneVisible)
        {
            IsRightPaneVisible = true;
        }
        ActiveLeaf = leaf;
        TasksReview.ForceReload();
        _announce(new A11yEvent.TasksReviewShown(
            SlateWindows.Panels.TasksReviewViewModel.DisplayName(
                TasksReview.ActiveFilter)));
        FocusBoundaryRequested?.Invoke(this, WorkspaceFocusBoundary.RightPane);
        // W6-2 PR B (rule C, Term 3(a)): the reveal route's end — the leaf
        // active NOW is the review, so no Connections load.
        ConsumePendingMount();
    }

    public bool IsRightPaneVisible
    {
        get => _isRightPaneVisible;
        set
        {
            if (SetField(ref _isRightPaneVisible, value))
            {
                _announce(value ? new A11yEvent.RightPaneShown() : new A11yEvent.RightPaneHidden());
                // W6-2 PR B (rule C, Terms 2 and 3(a)): a reveal arms the
                // pending mount its route consumes; a collapse clears the
                // leaf's view-local state.
                OnRightPaneVisibilityChanged(value);
                // W6-2 PR E (Term I2): the inspector toggle's checked state follows.
                NotifyGraphInspectorShownChanged();
                // W7-7 PR 4b (AR-38): the right pane's two steps follow.
                RaiseRightPaneResizeStates();
            }
        }
    }

    public ICommand CloseTabCommand { get; }
    public ICommand CloseActiveTabCommand { get; }
    public ICommand DuplicateTabCommand { get; }
    public ICommand ReopenClosedTabCommand { get; }
    public ICommand ToggleReadingModeCommand { get; }
    public ICommand MoveTabLeftCommand { get; }
    public ICommand MoveTabRightCommand { get; }
    public ICommand NextTabCommand { get; }
    public ICommand PreviousTabCommand { get; }
    public ICommand SplitRightCommand { get; }
    public ICommand SplitDownCommand { get; }
    public ICommand ClosePaneCommand { get; }
    public ICommand FocusPaneLeftCommand { get; }
    public ICommand FocusPaneRightCommand { get; }
    public ICommand FocusPaneAboveCommand { get; }
    public ICommand FocusPaneBelowCommand { get; }
    public ICommand FocusNextPaneCommand { get; }
    public ICommand FocusPreviousPaneCommand { get; }
    public ICommand GrowPaneCommand { get; }
    public ICommand ShrinkPaneCommand { get; }
    public ICommand SaveActiveCommand { get; }
    public ICommand ToggleRightPaneCommand { get; }
    public ICommand OpenTasksReviewCommand { get; }

    /// <summary>
    /// `slate.editor.toggleViewMode` on the active tab: Ctrl+Shift+E, the
    /// Editor menu item and the palette row. W7-7 PR 8 (#1253, contract
    /// R-10): focus moves with the view in BOTH directions, through the one
    /// focus funnel. The tab's own <see cref="WorkspaceTabViewModel.ToggleViewMode"/>
    /// has no route to the funnel, and its other caller, the
    /// create-from-template normalization, owns its landing already.
    /// </summary>
    private void ToggleActiveViewMode()
    {
        if (ActiveGroup.ActiveTab is not { IsMarkdown: true } tab)
        {
            return;
        }

        tab.ToggleViewMode();
        RequestActiveEditorFocus();
    }

    /// <summary>Opens a path. W7-7 (R-2): <paramref name="fromSelection"/>
    /// marks a Files selection — tree arrows, a filter result, a dual-pane
    /// row — which shows the note while keyboard focus stays on the row.
    /// Such an open asks for no editor focus, speaks no tab focus (the
    /// row's own selection line is what the reader hears), and never
    /// raises the modal dirty-navigation gate: a dirty current tab keeps
    /// its edits and the note shows in another tab of the group (codex PR 2
    /// round 2). Every other open is explicit and does all three.</summary>
    public void OpenPath(
        string path,
        WorkspaceOpenTarget target = WorkspaceOpenTarget.CurrentTab,
        bool fromSelection = false) =>
        RunWorkspaceMutation(() => OpenPathCore(path, target, requestEditorFocus: !fromSelection, fromSelection));

    private void OpenEditorNavigation(EditorNavigationRequest request) =>
        RunWorkspaceMutation(() =>
        {
            // New-tab is only ever requested by reading-view activations
            // (G22 preference); editor navigation stays current-tab.
            // TryOpenItem already reuses an existing same-target tab, so
            // the new-tab path never duplicates.
            if (!OpenPathCore(
                request.Path,
                request.OpenInNewTab
                    ? WorkspaceOpenTarget.NewTab
                    : WorkspaceOpenTarget.CurrentTab))
            {
                return;
            }

            WorkspaceTabViewModel? target = ActiveGroup.ActiveTab;
            if (target is null)
            {
                return;
            }

            _announce(new A11yEvent.InternalNavigated(
                "wikilink",
                System.IO.Path.GetFileName(request.Path)));
            WorkspaceGroupViewModel targetGroup = ActiveGroup;
            if (request.Anchor is not null)
            {
                target.NavigateToAnchor(
                    request.Anchor,
                    request.ResolvedAnchorText,
                    _announce,
                    () => ReferenceEquals(ActiveGroup, targetGroup)
                        && ReferenceEquals(targetGroup.ActiveTab, target));
            }
        });

    /// <summary>The editor half of the split tag seam (SD-4): the sidebar
    /// filters by the tag, and the filter's own core-rendered result — the
    /// <c>FileListCount</c> naming a tag scope when there is one — is the
    /// one thing said. W7-7 (R-3, codex PR 2 round 2): the host-composed
    /// "Filtered files by tag …" residue that also spoke here is gone; with
    /// the filter now finding the tag's files it was a second result
    /// line.</summary>
    private void ActivateEditorTag(string tag) =>
        EditorTagActivated?.Invoke(this, tag);

    /// <summary>The reading half of the split tag seam (SD-4): raise
    /// the event and nothing else. Deliberately NO announcement here —
    /// the search overlay's own tag-scope listing summary (contract S2)
    /// is the only voice on the reading path.</summary>
    private void ActivateReadingTag(string tag) =>
        ReadingTagActivated?.Invoke(this, tag);

    private bool OpenPathCore(
        string path,
        WorkspaceOpenTarget target,
        bool requestEditorFocus = true,
        bool fromSelection = false)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        WorkspaceItemState item = ItemForPath(path);
        if (TryOpenItem(item, target, requestEditorFocus, fromSelection))
        {
            FileOpened?.Invoke(this, path);
            Persist();
            return true;
        }

        return false;
    }

    public void OpenGraph()
    {
        // Rule P, Term P3: the SAME admission the preset funnel asks, read
        // BEFORE anything is written — "which OpenGraph() reads too". A
        // refusal writes no cause, opens no tab and starts no load. Windows
        // leaves the seam null today (C-D1), so this admits in production
        // and the facts inject a refusal through it (IPG-14).
        if (GraphOpenAdmissionReason?.Invoke() is not null)
        {
            return;
        }
        // W6-2 PR A (rule L, Term 5): the explicit Open sets its cause
        // before the mutation; the follow method consumes it at the
        // graph's transition, the boundary clears what was not consumed.
        SetGraphCause(GraphActivationCause.Open);
        RunWorkspaceMutation(() => OpenItem(
            new WorkspaceItemState(WorkspaceItemKind.Graph, "graph:singleton"),
            WorkspaceOpenTarget.NewTab));
    }

    /// <summary>Save every dirty tab (#1280). Each save pumps, and anything
    /// may run inside its frame — a tab opened, closed, moved, renamed or
    /// typed into — so the pass works over a snapshot and starts a new round
    /// whenever the tab set's stamp moved: a tab opened mid-way is saved, a
    /// closed one is skipped, and nothing is enumerated live across a frame.
    /// A clean tab is never rewritten, and a tab whose save failed (and said
    /// why) is not retried while it still shows the same item — a rename
    /// that retired its save gives it a new identity, which is retried.
    /// Saved means a round ended with the stamp unchanged, no live tab dirty
    /// and no live tab's save failed at the item it shows — within a bounded
    /// number of rounds.</summary>
    public bool SaveAll()
    {
        var failed = new Dictionary<WorkspaceTabViewModel, WorkspaceItemState>(
            ReferenceEqualityComparer.Instance);
        bool FailedAtItsItem(WorkspaceTabViewModel tab) =>
            failed.TryGetValue(tab, out WorkspaceItemState? at) && at == tab.Item;

        for (int round = 0; round < MaxPumpedAdmissionRounds; round++)
        {
            if (_workspaceDisposed)
            {
                return false;
            }
            TabSetStamp stamp = CaptureTabSet();
            bool moved = false;
            foreach (WorkspaceTabViewModel tab in stamp.Tabs)
            {
                if (_workspaceDisposed)
                {
                    return false;
                }
                if (tab.IsDisposed || !tab.IsDirty || FailedAtItsItem(tab))
                {
                    continue;
                }
                WorkspaceItemState item = tab.Item;
                if (!tab.Save())
                {
                    failed[tab] = item;
                }
                if (!stamp.StillHolds(this))
                {
                    moved = true;
                    break;
                }
            }
            if (moved)
            {
                continue;
            }
            WorkspaceTabViewModel[] live = [.. Groups
                .SelectMany(group => group.Tabs)
                .Where(tab => !tab.IsDisposed)];
            WorkspaceTabViewModel[] dirty = [.. live.Where(tab => tab.IsDirty)];
            if (dirty.All(FailedAtItsItem))
            {
                // Codex round 3: a save that failed at the item its tab still
                // shows is a "not saved" even when the tab is clean — a fault
                // after the landed write was adopted leaves no dirty tab
                // behind to say so.
                return dirty.Length == 0 && !live.Any(FailedAtItsItem);
            }
        }
        return false;
    }

    /// <summary>#1280: teardown's first step — pump until every admitted
    /// save has published, so the dirty state it then evaluates is settled
    /// and no write admitted before the prompt lands after it. False only
    /// when the dispatcher is shutting down.</summary>
    internal bool SettleSaves() => _saves.SettlePumping();

    /// <summary>#1280: true when no admitted save is waiting or writing.</summary>
    internal bool SavesIdle => _saves.IsIdle;

    /// <summary>#1280 (codex round 4): a clean tab whose latest save
    /// faulted — teardown, which would close it without asking, stays open
    /// instead.</summary>
    internal bool HasCleanTabWithAFaultedSave =>
        Groups.SelectMany(group => group.Tabs)
            .Any(tab => !tab.IsDirty && tab.LastSaveFaulted);

    /// <summary>#1280 (codex round 2a): every dirty tab and exactly what a
    /// teardown prompt asks about it — its document and edit revision — read
    /// before the prompt opens.</summary>
    internal IReadOnlyList<(WorkspaceTabViewModel Tab, int Identity, long Revision)>
        DirtyTabsForPrompt() =>
        [.. Groups
            .SelectMany(group => group.Tabs)
            .Where(tab => !tab.IsDisposed && tab.IsDirty)
            .Select(tab => (tab, tab.ItemIdentity, tab.EditRevision))];

    /// <summary>True when the dirty tabs are still exactly
    /// <paramref name="asked"/>: the same tabs, documents and edits.</summary>
    internal bool DirtyTabsStill(
        IReadOnlyList<(WorkspaceTabViewModel Tab, int Identity, long Revision)> asked) =>
        DirtyTabsForPrompt().SequenceEqual(asked);

    /// <summary>#1280 test seam: the workspace's save coordinator.</summary>
    internal WorkspaceSaveCoordinator SavesForTests => _saves;

    public void RetargetPath(string oldPath, string newPath)
    {
        string source = NormalizeWorkspacePath(oldPath);
        string destination = NormalizeWorkspacePath(newPath);
        if (source.Length == 0 || destination.Length == 0)
        {
            return;
        }

        foreach (WorkspaceTabViewModel tab in Groups.SelectMany(group => group.Tabs))
        {
            if (IsPathBacked(tab.Item)
                && TryRetargetPath(tab.Path, source, destination, out string retargeted))
            {
                // #1280 (codex round 2a): the renamed file's save chain
                // continues from the old path's, so a peer's save after the
                // rename starts from the hash a pre-rename write published.
                _saves.ContinueChain(tab.Path, retargeted);
                tab.RetargetPath(retargeted);
            }
        }

        for (int index = 0; index < _closedTabs.Count; index++)
        {
            (WorkspaceItemState item, Guid group) = _closedTabs[index];
            if (IsPathBacked(item)
                && TryRetargetPath(item.Path, source, destination, out string retargeted))
            {
                _closedTabs[index] = (item with { Path = retargeted }, group);
            }
        }

        // The Bases registry keys documents by byte-exact path, and a
        // document's Path is immutable — a rename must re-key or the
        // renamed tab keeps a document whose Retry reopens the OLD
        // path forever, and whose stale key the next release sweep
        // shuts down UNDER the live tab (red team round 2 blocker).
        RetargetBaseDocuments(source, destination);
        // Same reason, same shape, for the canvas registry (CD-32).
        RetargetCanvasDocuments(source, destination);
        // W6-2 PR B2 (rule D, the rename hook; B2D-9): the Connections
        // leaf's pin, note in view and every stack entry move by the same
        // predicate — a retarget of the PIN is Term 3(d)'s root move with
        // the classification the funnel uses.
        Connections.Retarget(source, destination, ConnectionsActiveAndMounted());

        Persist();
        // A rename that touched the ACTIVE tab changed its Path in
        // place — without a re-derive the panels stay bound to the
        // old path and ignore every save on the new one (adversarial
        // round 2).
        SyncPanels();
    }

    private void RetargetBaseDocuments(string source, string destination)
    {
        foreach (string oldKey in _baseDocuments.Keys
            .Where(key => key.StartsWith("file:", StringComparison.Ordinal)
                && TryRetargetPath(key["file:".Length..], source, destination, out _))
            .ToList())
        {
            Bases.BaseDocumentViewModel oldDocument = _baseDocuments[oldKey];
            _baseDocuments.Remove(oldKey);
            oldDocument.Shutdown();
            TrackRetiredBasesWork(oldDocument.WhenHandleClosed());
            _ = TryRetargetPath(
                oldKey["file:".Length..], source, destination, out string newPath);
            foreach (WorkspaceTabViewModel tab in Groups
                .SelectMany(group => group.Tabs)
                .Where(candidate => candidate.IsBase
                    && string.Equals(candidate.Path, newPath, StringComparison.Ordinal)))
            {
                tab.AttachBaseDocument(BaseDocumentFor(newPath));
            }
        }
        if (BasesDockDocument is { } dockDocument
            && !dockDocument.IsSavedQuery
            && TryRetargetPath(dockDocument.Path, source, destination, out string dockPath))
        {
            // Silent re-dock at the new path: the target moved, the
            // user did nothing — no announcement (INV-4).
            RedockBaseFileSilently(dockPath);
        }
    }

    /// <summary>#1077 (contract I6): after a Created or Renamed
    /// publication, a tab whose file vanished may find it back under
    /// ANOTHER spelling — <c>Ghost.md</c> deleted, <c>ghost.md</c>
    /// created, one physical file on NTFS/APFS — which every ordinal
    /// comparison in this class rightly cannot see. Identity is
    /// corrected ONCE, here, by asking core for the filesystem's stored
    /// spelling: the tab is retargeted to it and, when clean, reloaded
    /// through the same-path reload (the templates precedent) so its
    /// missing-status and content hash stop being stale bookkeeping. A
    /// DIRTY tab keeps its buffer — it is retargeted so its save names
    /// the right identity, and that save is hashless, which contract I8
    /// routes through create-exclusive: a conflict, never an overwrite.
    /// Comparators stay ordinal everywhere; nothing here re-litigates
    /// identity per comparison.</summary>
    public void ReseatMissingTabs()
    {
        foreach (WorkspaceTabViewModel tab in Groups
            .SelectMany(group => group.Tabs)
            .Where(candidate => candidate.IsMissingFromDisk
                && !string.IsNullOrEmpty(candidate.Path))
            .ToList())
        {
            string? stored;
            try
            {
                stored = _session.CanonicalPath(tab.Path);
            }
            catch (VaultException)
            {
                // An identity we cannot read is left as it is (fail
                // closed, contract I7): the tab stays missing.
                continue;
            }
            if (stored is null)
            {
                continue;
            }
            if (KeepsBufferOnReseat(tab, stored))
            {
                continue;
            }
            if (!string.Equals(stored, tab.Path, StringComparison.Ordinal))
            {
                tab.RetargetPath(stored);
            }
            tab.ReplaceItem(tab.Item);
        }
    }

    /// <summary>
    /// W7-7 PR 7 (#1252, R-9; codex's final merge-delta check, finding 1):
    /// the ONE rule both re-seats apply — a Slate-owned Created or Renamed
    /// event's (<see cref="ReseatMissingTabs"/>) and a rescan's
    /// (<c>ReseatMissingTabsAsync</c>) — before they re-seat a missing tab
    /// whose file is back at <paramref name="stored"/>. True when the tab
    /// keeps its buffer and its document:
    /// <list type="bullet">
    /// <item>while a save is admitted for the file — its case-folded path, the
    /// chain every tab on the file shares (<see cref="WorkspaceSaveCoordinator.HasAdmittedSaveFor"/>)
    /// — EVERY Markdown tab on it keeps them (contract 38 D-10). The save's
    /// own create publishes its Created event before the save publishes, and
    /// an edit made while the create ran — an undo back to the baseline
    /// included, which leaves the tab clean — must stay an unsaved change
    /// behind the created bytes. The file is back: a tab under another
    /// spelling takes the stored one (#1077, a rename's retarget), and any
    /// other clears its missing state in place
    /// (<see cref="WorkspaceTabViewModel.MarkBackOnDisk"/>) — no replace, and
    /// no epoch change, so the save still publishes to it;</item>
    /// <item>a dirty tab keeps them as before (#1077, contract I8): under
    /// another spelling it takes the stored one; under its own it stays
    /// missing — its hashless save is a create onto an occupied path.</item>
    /// </list>
    /// False: the caller re-seats the tab.
    /// </summary>
    private bool KeepsBufferOnReseat(WorkspaceTabViewModel tab, string stored)
    {
        bool respelled = !string.Equals(stored, tab.Path, StringComparison.Ordinal);
        if (tab.IsMarkdown
            && (tab.HasPendingSaves
                || _saves.HasAdmittedSaveFor(tab.Path)
                || _saves.HasAdmittedSaveFor(stored)))
        {
            if (respelled)
            {
                tab.RetargetPath(stored);
            }
            else
            {
                tab.MarkBackOnDisk();
            }

            return true;
        }

        if (tab.IsDirty)
        {
            if (respelled)
            {
                tab.RetargetPath(stored);
            }

            return true;
        }

        return false;
    }

    public void InvalidatePath(string path)
    {
        string invalidated = NormalizeWorkspacePath(path);
        if (invalidated.Length == 0)
        {
            return;
        }

        InvalidatePath(invalidated, persist: true);
    }

    /// <summary>The per-path invalidation <see cref="InvalidatePath(string)"/>
    /// and a rescan's page batch (<see cref="InvalidatePaths"/>) share: open
    /// tabs on the path marked missing (their buffers kept), closed-tab
    /// history and the Connections stack pruned. <paramref name="persist"/>
    /// false leaves the command-state refresh and the workspace persist to
    /// the batch, once per page.</summary>
    private void InvalidatePath(string invalidated, bool persist)
    {
        int affected = 0;
        foreach (WorkspaceTabViewModel tab in Groups.SelectMany(group => group.Tabs))
        {
            if (IsPathBacked(tab.Item) && IsSameOrDescendantPath(tab.Path, invalidated))
            {
                tab.InvalidatePath();
                affected++;
            }
        }

        _closedTabs.RemoveAll(entry =>
            IsPathBacked(entry.Item) && IsSameOrDescendantPath(entry.Item.Path, invalidated));
        // W6-2 PR B2 (rule D, the delete hook; B2D-9): the leaf's stack
        // entries under the deleted path are pruned, so Back never opens a
        // note that is gone; the pin and the note in view are kept (the
        // Error presentation, B1's delete route).
        Connections.Prune(invalidated);
        if (persist)
        {
            RaiseCommandStates();
            Persist();
        }

        // W7-7 PR 7 (R-9): silent under a rescan's reconciliation, which
        // speaks only its one core-rendered completion sentence.
        if (affected > 0 && !IsReconcilingSilently)
        {
            // W0.5-3 residue: Windows missing-editor availability copy.
            _announce(new A11yEvent.HostComposed(
                $"{System.IO.Path.GetFileName(invalidated)} is missing from disk. Open editor content was preserved.",
                A11yPriority.High));
        }
    }

    public void Dispose()
    {
        // #1280 (contract 35 A-1, close-before-session): no save starts
        // from here on, and every save worker already writing is JOINED
        // before this returns — the vault lifecycle disposes the session
        // right after. The join needs no dispatcher callback (the workers
        // call only core); a publication that lands later finds its tab
        // disposed and says nothing.
        _saves.CloseAndJoinWorkers();
        // W4-4 (adversarial round 2): the bulk-rename worker holds
        // the shared session too — cancel any in-flight run through
        // its CancelToken and shut the scheduler down so a terminal
        // publish can't land in this dying workspace. The rename's
        // per-file CAS writes are individually safe; cancellation
        // reports the unprocessed remainder at the core layer.
        _bulkRenameSheet?.CancelInFlight();
        _bulkRenameSheet?.Shutdown();
        _bulkRenameSheet = null;
        _addPropertySheet = null;
        // W4-5: the citation workers hold the shared session too —
        // shut them down before it dies, and drop the sheets so a
        // late publish cannot touch a dying UI.
        _filesCiting?.Shutdown();
        _filesCiting = null;
        _citationDetails = null;
        _citationSummary = null;
        Citations.Shutdown();
        Bibliography.Shutdown();
        // Settle the seed AFTER marking both leaves shut down. A body
        // parked on the gate would otherwise wait for a seed that is
        // never coming; releasing it here lets it wake, observe
        // IsShutDown, and return without publishing. Cancelled is a
        // STATUS, not a cancelled Task — a cancelled Task would throw
        // into the scheduler's catch, be swallowed, and run the body
        // anyway.
        _bibliographySeed.Cancel();
        // Panels first: their workers hold the shared session, which
        // the vault lifecycle disposes right after this workspace —
        // invalidate every in-flight load before that happens.
        Panels.Shutdown();
        TasksReview.Shutdown();
        // W4-6: every Bases document holds the shared session and a
        // native handle — closed SYNCHRONOUSLY here (codex round 1:
        // the non-blocking close could lose the race against the
        // session disposal that follows this Dispose, silently
        // skipping CloseBase — C3/INV-2 requires close-before-session,
        // exactly once).
        _workspaceDisposed = true;
        // Every handle-owning Bases worker shuts down NON-BLOCKINGLY
        // (codex round 4: a synchronous close waits on the FFI lock,
        // and a non-cancellable export/apply-edit holding it would
        // freeze the dispatcher indefinitely — INV-6/R2). The closes
        // are then awaited as ONE bounded drain before the session
        // disposes (INV-2's close-before-session, bounded rather than
        // absolute: a >5 s uncancellable FFI call at the exact moment
        // of app close forfeits the ordering rather than the UI).
        var basesDrains = new List<Task>();
        foreach (Bases.BaseDocumentViewModel document in _baseDocuments.Values)
        {
            document.Shutdown();
            basesDrains.Add(document.WhenHandleClosed());
        }
        _baseDocuments.Clear();
        foreach (Bases.DashboardViewModel dashboard in _dashboardDocuments.Values)
        {
            dashboard.Shutdown();
            basesDrains.Add(dashboard.WhenWorkDrained());
        }
        _dashboardDocuments.Clear();
        // W6-1 PR A (contract A1/A17): canvas documents hold the shared
        // session and a native handle on exactly the same terms.
        ShutdownCanvasDocuments(basesDrains);
        // W6-2 PR A (contract A-1): the graph document and its create
        // worker into the same bounded pre-session drain.
        ShutdownGraphDocument(basesDrains);
        // Retires the dock document/dashboard into the tracked set.
        ClearBasesDock();
        if (BaseQueryBuilderSheet is { } openBuilder)
        {
            openBuilder.Shutdown();
            basesDrains.Add(openBuilder.WhenWorkDrained());
        }
        // W4-7: the history document's workers hold the session too.
        History.Shutdown();
        basesDrains.Add(History.WhenWorkDrained());
        // W4-8: so do the sync probes (SDINV-8 — after Dispose no
        // publish or announcement lands).
        SyncDiagnostics.Shutdown();
        basesDrains.Add(SyncDiagnostics.WhenWorkDrained());
        // RETIRED-earlier schedulers too (a replaced builder, a
        // released dashboard, a swept document) — their in-flight
        // bodies hold ephemeral handles just the same (codex round 3).
        basesDrains.AddRange(RetiredBasesDrains);
        // W7-7 PR 7 (codex AR-18 review round 2, finding 4): every worker a
        // rescan started — cancelled mid-re-sync by the close that brought
        // us here — is drained to EMPTY, not bounded: no rescan-originated
        // core call is ever in flight when the session is disposed. Every
        // scheduler above has shut down, so none of these waits on this
        // thread.
        DrainRescanWork();
        basesDrains.RemoveAll(task => task.IsCompleted);
        if (basesDrains.Count > 0)
        {
            try
            {
                _ = Task.WaitAll([.. basesDrains], TimeSpan.FromSeconds(5));
            }
            catch (AggregateException)
            {
                // Tracked bodies never fault by contract; a straggler
                // past the bound must not wedge app close.
            }
        }
        Persist();
        foreach (WorkspaceTabViewModel tab in Groups.SelectMany(group => group.Tabs))
        {
            tab.Dispose();
        }

        EditorPreferences.Dispose();
    }

    private void SaveActive()
    {
        if (ActiveGroup.ActiveTab is not WorkspaceTabViewModel tab)
        {
            return;
        }

        // #1280 (codex round 2a): the explicit Save requests the write and
        // returns — it never waits in a frame, so holding Ctrl+S nests
        // nothing, and requests made while a save is queued join it (one
        // write, one confirmation). The tab's publication speaks NoteSaved;
        // what follows a landed save runs from there.
        ObserveSave(tab.SaveAsync(
            onSaved: () =>
            {
                // Headings move under edits — the outline leaf re-reads
                // after a save (link rows deliberately do not; mac parity).
                NotePersisted(tab.Path);
                // W4-4: frontmatter can be hand-edited in the whole-file
                // buffer on Windows — the header re-derives from the
                // just-saved bytes so its rows and CAS tokens are never
                // a stale generation behind the tab (contract 4).
                RefreshPropertiesFor(tab.Path);
            },
            announce: true));
    }

    /// <summary>#1280: the Save command's fault seam for facts. The D-10
    /// outcome of a refused write is spoken by the publication; a fault past
    /// it (a bug) is observed and logged once by its save ticket
    /// (<see cref="WorkspaceSaveCoordinator.SaveTicket.Fail"/>), whoever
    /// waits on it — this only counts the ones the Save command saw.</summary>
    private void ObserveSave(Task<bool> save) =>
        _lastSaveObservation = save.ContinueWith(
            _ => Interlocked.Increment(ref _saveFailuresObservedForTests),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

    private int _saveFailuresObservedForTests;
    private Task _lastSaveObservation = Task.CompletedTask;

    /// <summary>#1280 test seam: faulted saves the Save command observed.</summary>
    internal int SaveFailuresObservedForTests => Volatile.Read(ref _saveFailuresObservedForTests);

    /// <summary>#1280 test seam: the Save command's latest observation — a
    /// fact waits for it to finish before it counts what was logged.</summary>
    internal Task LastSaveObservationForTests => _lastSaveObservation;

    private static WorkspaceItemState ItemForPath(string path)
    {
        string extension = System.IO.Path.GetExtension(path);
        return extension.ToLowerInvariant() switch
        {
            ".canvas" => new WorkspaceItemState(WorkspaceItemKind.Canvas, path),
            ".base" => new WorkspaceItemState(WorkspaceItemKind.Base, path),
            _ => new WorkspaceItemState(WorkspaceItemKind.Markdown, path),
        };
    }

    private static bool ItemsReferToSameTarget(WorkspaceItemState left, WorkspaceItemState right) =>
        left.Kind == right.Kind
        && (left.Kind is WorkspaceItemKind.SavedQuery or WorkspaceItemKind.Dashboard
            // Registry-backed kinds have EMPTY paths — identity is the
            // registry ID (codex round 5: path-only comparison
            // collapsed every saved query/dashboard to the first open
            // tab of its kind, so the second one never opened).
            ? string.Equals(left.Id, right.Id, StringComparison.Ordinal)
            : string.Equals(left.Path, right.Path, StringComparison.Ordinal));

    // Any tab showing the note will do, transient or not (W7-7 R-2): the
    // peer only seeds a tab's state from the ONE shared document, and
    // never decides which tab an open shows — FindOpenTab does that.
    private WorkspaceTabViewModel? FindSamePathTab(
        WorkspaceItemState item,
        WorkspaceTabViewModel? excluding = null) =>
        item.Kind == WorkspaceItemKind.Markdown
            ? Groups.SelectMany(group => group.Tabs).FirstOrDefault(tab =>
                !ReferenceEquals(tab, excluding)
                && tab.IsMarkdown
                && string.Equals(tab.Path, item.Path, StringComparison.Ordinal))
            : null;

    /// <summary>The review's tab-route seam (W4-3): a file with an
    /// open tab must toggle through THAT tab's guarded path so the
    /// buffer re-baselines — a direct session write would leave the
    /// open editor stale. The result distinguishes refusals from
    /// started toggles (adversarial round 1: a lossy bool armed
    /// refresh state for refusals), and the row's snapshot hash is
    /// verified against the tab's SAVED content first — a stale
    /// ordinal against newer content could toggle a different task.</summary>
    private SlateWindows.Panels.ReviewToggleRoute TryToggleTaskInOpenTab(
        string path, TaskItem task, string expectedContentHash)
    {
        WorkspaceTabViewModel? tab = Groups
            .SelectMany(group => group.Tabs)
            .FirstOrDefault(candidate => candidate.IsMarkdown
                && string.Equals(candidate.Path, path, StringComparison.Ordinal));
        if (tab is null)
        {
            return SlateWindows.Panels.ReviewToggleRoute.NoOpenTab;
        }
        if (tab.IsDirty)
        {
            _announce(new A11yEvent.TaskToggleUnsaved(
                System.IO.Path.GetFileName(tab.Path)));
            return SlateWindows.Panels.ReviewToggleRoute.RefusedDirty;
        }
        // Round 9: an externally rewritten file leaves this clean
        // tab AND rows born from its baseline sharing the obsolete
        // hash — matching vacuously. Refusing here (instead of
        // letting the core CAS conflict) breaks the retry loop the
        // doomed write would otherwise announce forever.
        if (tab.IsExternallyStale
            || !string.Equals(
                tab.SavedContentHash, expectedContentHash, StringComparison.Ordinal))
        {
            _announce(new A11yEvent.TaskToggleConflict(
                System.IO.Path.GetFileName(tab.Path)));
            return SlateWindows.Panels.ReviewToggleRoute.RefusedStale;
        }
        return tab.ToggleTask(
            task, _announce, TaskToggleCompletion(path, task, tab.SavedContentHash)) switch
        {
            TabTaskToggle.Started => SlateWindows.Panels.ReviewToggleRoute.Started,
            // Busy: the tab announced; the review must NOT arm a
            // refresh for an operation that never ran (round 2).
            TabTaskToggle.RefusedBusy => SlateWindows.Panels.ReviewToggleRoute.RefusedBusy,
            // A dirty race between the check above and the call.
            _ => SlateWindows.Panels.ReviewToggleRoute.RefusedDirty,
        };
    }

    /// <summary>Terminal-state handling for tab-routed toggles that
    /// OUTLIVES the originating tab (adversarial rounds 3-4, both
    /// the review route and the note panel's): closing the tab
    /// mid-flight must not eat the outcome. Failures disarm the
    /// review's pending refresh; successes announce when the tab
    /// could not, re-snapshot the panels and the review, and give
    /// orphaned same-path tabs the divergence honesty.</summary>
    private Action<SaveReport?, VaultException?, string?, bool> TaskToggleCompletion(
        string path, TaskItem task, string? preToggleHash)
    {
        string fileName = System.IO.Path.GetFileName(path);
        return (report, error, postFailureDiskHash, publishedThroughTab) =>
        {
            if (report is null)
            {
                // No SaveReport does NOT mean disk is unchanged
                // (adversarial round 11): the core writes the FILE
                // before committing the index, so a post-write
                // failure leaves the checkbox flipped on disk with
                // no report. The worker read the disk back — a moved
                // hash reconciles tabs and re-snapshots both task
                // surfaces instead of retaining an obsolete clean
                // editor over changed disk.
                bool diskMoved = postFailureDiskHash is not null
                    && preToggleHash is not null
                    && !string.Equals(
                        postFailureDiskHash, preToggleHash, StringComparison.Ordinal);
                // A WriteConflict refused BEFORE any write — the one
                // failure whose no-write outcome is CERTAIN. Every
                // other failure with an unreadable read-back is
                // UNKNOWN, and unknown fails closed (round 16):
                // treating it as "no write" would let both surfaces
                // query a possibly-stale index with nothing barring
                // them.
                bool outcomeUnknown = postFailureDiskHash is null
                    && error is not VaultException.WriteConflict;
                if (diskMoved || outcomeUnknown)
                {
                    // Repair the INDEX first (adversarial round 12):
                    // the real failure window is file-written /
                    // index-uncommitted, so reloading before the
                    // repair would re-query the stale index and
                    // resurrect the pre-write state as ghost rows.
                    // Tab reconciliation is hash-truth and runs when
                    // the hash is known; the surface reloads are
                    // GATED on the repair succeeding (rounds 14-15)
                    // — a failed repair enters the SHARED
                    // quarantine, which bars both surfaces' queries
                    // until a retry lands.
                    bool repaired = _taskIndexRepairs.TryRepairNow(path, out _);
                    if (diskMoved)
                    {
                        ReconcileTabsAfterDirectTaskWrite(path, postFailureDiskHash!);
                    }
                    if (repaired)
                    {
                        NotePersisted(path);
                        TasksReview.NoteRefreshed(path);
                    }
                }
                // Idempotent after NoteRefreshed consumed the marker;
                // disarms it when disk truly never changed.
                TasksReview.ToggleAbandoned(path);
                if (!publishedThroughTab)
                {
                    if (error is VaultException.WriteConflict)
                    {
                        _announce(new A11yEvent.TaskToggleConflict(fileName));
                    }
                    else
                    {
                        _announce(new A11yEvent.HostComposed(
                            $"Task could not be toggled: {error!.Message}",
                            A11yPriority.High));
                    }
                }
                return;
            }
            if (!publishedThroughTab)
            {
                // W0.5-3 residue: WorkspaceTabViewModel.PublishTaskToggle
                _announce(new A11yEvent.HostComposed(
                    task.Completed ? "Task reopened." : "Task completed.",
                    A11yPriority.Medium));
            }
            // Refreshes are completion-driven, independent of the
            // tab's lifetime: the panel save funnel and the review
            // NoteRefreshed can't fire from a disposed tab, and
            // same-path tabs that lost their mirror source (or raced
            // open) re-baseline honesty here.
            NotePersisted(path);
            TasksReview.NoteRefreshed(path);
            ReconcileTabsAfterDirectTaskWrite(path, report.NewContentHash);
        };
    }

    /// <summary>A task toggle changed <paramref name="path"/> on disk
    /// WITHOUT publishing through an open tab (adversarial round 3):
    /// either the review's tabless route decided before a tab raced
    /// open, or the originating tab was disposed mid-flight and its
    /// same-path peers lost their mirror source. Any tab still
    /// holding pre-write content is a stale editor over changed disk
    /// — it gets the tab's own divergence honesty.</summary>
    private void ReconcileTabsAfterDirectTaskWrite(string path, string newContentHash)
    {
        foreach (WorkspaceTabViewModel tab in Groups.SelectMany(group => group.Tabs))
        {
            if (tab.IsMarkdown
                && string.Equals(tab.Path, path, StringComparison.Ordinal))
            {
                tab.ReconcileAfterExternalTaskWrite(newContentHash, _announce);
            }
        }
    }

    /// <summary>The panels' task-toggle seam (W4-3): the guarded tab
    /// path owns conflict detection, generation gating, and the
    /// canonical announcements. The tab's raw ToggleTask refuses
    /// dirty WITHOUT announcing, so the refusal is spoken here (the
    /// reading-view precedent). The row's snapshot hash is verified
    /// against the tab's SAVED content first (adversarial round 2):
    /// panel rows survive a save until the async refresh publishes,
    /// and the tab's own CAS uses the CURRENT hash — it would happily
    /// toggle whichever task inherited a stale row's ordinal.</summary>
    private bool TogglePanelTask(TaskItem task, string expectedContentHash)
    {
        if (ActiveGroup.ActiveTab is not { IsMarkdown: true } tab)
        {
            return false;
        }
        if (tab.IsDirty)
        {
            _announce(new A11yEvent.TaskToggleUnsaved(
                System.IO.Path.GetFileName(tab.Path)));
            return true;
        }
        if (tab.IsExternallyStale
            || !string.Equals(
                tab.SavedContentHash, expectedContentHash, StringComparison.Ordinal))
        {
            _announce(new A11yEvent.TaskToggleConflict(
                System.IO.Path.GetFileName(tab.Path)));
            Panels.ReloadTasks();
            return true;
        }
        // The same terminal completion as the review route
        // (adversarial round 4): a panel toggle whose tab is closed
        // mid-flight must still announce and re-snapshot.
        return tab.ToggleTask(
                task,
                _announce,
                TaskToggleCompletion(tab.Path, task, tab.SavedContentHash))
            != TabTaskToggle.Refused;
    }

    /// <summary>The review's row-activation seam (adversarial rounds
    /// 6 and 8): open the file if it isn't the active note, then
    /// verify the row's snapshot hash against the tab's SAVED
    /// content and refuse DIRTY buffers before scrolling — the
    /// snapshot's byte offset only means anything against the exact
    /// text the caret would move through, and the toggle paths
    /// already guard both conditions.</summary>
    private SlateWindows.Panels.ReviewOpenRoute TryActivateTaskRow(
        string path, TaskItem task, string expectedContentHash)
    {
        bool wasActive = ActiveGroup.ActiveTab is WorkspaceTabViewModel
        {
            IsMarkdown: true
        } active
            && string.Equals(active.Path, path, StringComparison.Ordinal);
        if (!wasActive)
        {
            bool navigated = false;
            RunWorkspaceMutation(
                () => navigated = OpenPathCore(path, WorkspaceOpenTarget.CurrentTab));
            if (!navigated)
            {
                return SlateWindows.Panels.ReviewOpenRoute.OpenFailed;
            }
        }
        if (ActiveGroup.ActiveTab is not WorkspaceTabViewModel { IsMarkdown: true } tab
            || !string.Equals(tab.Path, path, StringComparison.Ordinal))
        {
            return SlateWindows.Panels.ReviewOpenRoute.OpenFailed;
        }
        // Dirty buffers refuse (adversarial round 8): the saved hash
        // still matches the row, but the LIVE text the caret moves
        // through has shifted under unsaved edits — a saved-content
        // offset can land on unrelated words while announcing
        // success. The platform's dirty posture (editor, panel, and
        // review toggles) extends to activation; a divergence from
        // the mac review's unverified line scroll, recorded.
        if (tab.IsDirty)
        {
            return SlateWindows.Panels.ReviewOpenRoute.RefusedDirty;
        }
        // Round 9: after an EXTERNAL write, a clean-but-obsolete tab
        // and rows born from the same baseline match each other
        // vacuously — the index-derived staleness refuses too.
        if (tab.IsExternallyStale
            || !string.Equals(
                tab.SavedContentHash, expectedContentHash, StringComparison.Ordinal))
        {
            return SlateWindows.Panels.ReviewOpenRoute.RefusedStale;
        }
        ScrollToPanelTask(task);
        return wasActive
            ? SlateWindows.Panels.ReviewOpenRoute.ScrolledInPlace
            : SlateWindows.Panels.ReviewOpenRoute.Opened;
    }

    /// <summary>The note panel's activation seam (adversarial round
    /// 7, the review guard's note-panel twin): after a save, the old
    /// rows stay actionable until the async refresh publishes, and a
    /// stale byte offset against the new text moves the caret to
    /// unrelated content. A hash mismatch refuses SILENTLY — the
    /// panel's activation posture is a silent scroll, so the caret
    /// NOT moving is the honest observable — and re-snapshots.</summary>
    private void ScrollToPanelTaskIfCurrent(TaskItem task, string expectedContentHash)
    {
        if (ActiveGroup.ActiveTab is not { IsMarkdown: true } tab)
        {
            return;
        }
        // Dirty buffers refuse LOUDLY (adversarial round 8): the
        // saved hash still matches these rows, but unsaved edits
        // have shifted the live text — a saved-content offset can
        // park the caret on unrelated words. The panel's toggle
        // already announces its dirty refusal; activation joins it
        // (W0.5-3 residue: the TaskToggleUnsaved family's wording
        // with the activation verb).
        if (tab.IsDirty)
        {
            _announce(new A11yEvent.HostComposed(
                $"Cannot open this task. The editor has unsaved changes in {System.IO.Path.GetFileName(tab.Path)}. Save the note first.",
                A11yPriority.High));
            return;
        }
        if (tab.IsExternallyStale
            || !string.Equals(
                tab.SavedContentHash, expectedContentHash, StringComparison.Ordinal))
        {
            Panels.ReloadTasks();
            return;
        }
        ScrollToPanelTask(task);
    }

    /// <summary>The panels' task-activation seam (W4-3): park the
    /// caret at the task's line start — a silent scroll, the mac
    /// note-panel behavior (the caret move is the observable).</summary>
    private void ScrollToPanelTask(TaskItem task)
    {
        if (ActiveGroup.ActiveTab is not { IsMarkdown: true } tab
            || tab.EditorInteractions is null)
        {
            return;
        }
        string source = tab.Text;
        uint byteOffset = Math.Min(
            task.ByteOffset, checked((uint)Encoding.UTF8.GetByteCount(source)));
        int target = checked((int)SlateUniffiMethods.TextByteToUtf16(
            source, byteOffset));
        tab.EditorInteractions.RequestCaret(target);
    }

    private void MirrorSamePathDocumentState(
        WorkspaceTabViewModel source,
        EditorDocumentSyncEvent? syncEvent)
    {
        if (!source.IsMarkdown)
        {
            return;
        }

        foreach (WorkspaceTabViewModel peer in Groups.SelectMany(group => group.Tabs))
        {
            if (!ReferenceEquals(peer, source)
                && peer.IsMarkdown
                && string.Equals(peer.Path, source.Path, StringComparison.Ordinal))
            {
                if (syncEvent is null)
                {
                    peer.MirrorDocumentStateFrom(source, reconstructUndoHistory: false);
                }
                else
                {
                    peer.ApplyPeerDocumentEvent(source, syncEvent);
                }
            }
        }

        // A null sync event on a CLEAN buffer is a whole-document
        // refresh — the task-toggle publish re-baselines saved
        // (W4-3): the panels re-read what just changed on disk (the
        // save command's own funnel covers ordinary saves).
        if (syncEvent is null && !source.IsDirty)
        {
            NotePersisted(source.Path);
            TasksReview.NoteRefreshed(source.Path);
        }
    }

    public void InvalidateModifiedPath(string path)
    {
        string modified = NormalizeWorkspacePath(path);
        foreach (WorkspaceTabViewModel tab in Groups.SelectMany(group => group.Tabs))
        {
            if (tab.IsMarkdown
                && string.Equals(tab.Path, modified, StringComparison.Ordinal))
            {
                tab.InvalidateExternalState();
                // Round 9: an external write leaves this CLEAN tab
                // and any task rows born from its baseline sharing
                // the same obsolete hash — they match each other
                // vacuously, so the snapshot-identity guards need an
                // index-derived staleness signal to refuse on.
                tab.RefreshExternalStaleness();
            }
        }
    }

    /// <summary>
    /// W3-5: embed cards resolve content from OTHER files, so every
    /// open reading model gets the vault change stream and applies
    /// its own reverse-dependency filter (a target-note save after
    /// publication previously refreshed nothing — stale cards until
    /// a mode cycle).
    /// </summary>
    public void NotifyReadingOfVaultChange(FileChangeKind kind, string path)
    {
        string changed = NormalizeWorkspacePath(path);
        foreach (WorkspaceTabViewModel tab in Groups.SelectMany(group => group.Tabs))
        {
            tab.Reading?.NotifyVaultFileChanged(kind, changed);
        }
    }
    public void InvalidateAllInteractionStates()
    {
        foreach (WorkspaceTabViewModel tab in Groups.SelectMany(group => group.Tabs))
        {
            tab.InvalidateExternalState();
        }
    }
    private static bool IsPathBacked(WorkspaceItemState item) =>
        item.Kind is WorkspaceItemKind.Markdown or WorkspaceItemKind.Canvas or WorkspaceItemKind.Base;

    private static string NormalizeWorkspacePath(string path) =>
        string.IsNullOrWhiteSpace(path)
            ? string.Empty
            : path.Replace('\\', '/').TrimEnd('/');

    private static bool IsSameOrDescendantPath(string path, string ancestor)
    {
        string normalized = NormalizeWorkspacePath(path);
        return string.Equals(normalized, ancestor, StringComparison.Ordinal)
            || normalized.StartsWith(ancestor + "/", StringComparison.Ordinal);
    }

    private static bool TryRetargetPath(
        string path,
        string source,
        string destination,
        out string retargeted)
    {
        string normalized = NormalizeWorkspacePath(path);
        if (string.Equals(normalized, source, StringComparison.Ordinal))
        {
            retargeted = destination;
            return true;
        }

        if (normalized.StartsWith(source + "/", StringComparison.Ordinal))
        {
            retargeted = destination + normalized[source.Length..];
            return true;
        }

        retargeted = normalized;
        return false;
    }

}
