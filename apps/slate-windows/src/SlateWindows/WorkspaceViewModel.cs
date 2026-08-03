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

internal enum WorkspaceFocusBoundary
{
    Files,
    RightPane,
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

internal sealed class WorkspaceTabViewModel : BindableBase, IDisposable
{
    private readonly VaultSession _session;
    private readonly Action<WorkspaceTabViewModel, EditorDocumentSyncEvent?>? _documentChanged;
    private readonly Action<EditorNavigationRequest>? _navigate;
    private readonly Action<string>? _activateTag;
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
        Action<A11yEvent>? announce = null,
        EditorPreferencesViewModel? editorPreferences = null,
        bool startInteractionBackgroundWork = true,
        Func<string, string, string, uint?>? anchorResolver = null,
        Func<EditorInteractionWorkerKind, Exception?>?
            interactionBackgroundFaultForTests = null)
    {
        _session = session;
        _documentChanged = documentChanged;
        _navigate = navigate;
        _activateTag = activateTag;
        _announce = announce ?? (_ => { });
        _ownsEditorPreferences = editorPreferences is null;
        _startInteractionBackgroundWork = startInteractionBackgroundWork;
        _anchorResolver = anchorResolver ?? SlateUniffiMethods.LinkAnchorByteOffset;
        _interactionBackgroundFaultForTests = interactionBackgroundFaultForTests;
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

    internal int AnchorNavigationPublishCountForTests =>
        Volatile.Read(ref _anchorNavigationPublishCountForTests);

    public Guid Id { get; }
    public WorkspaceItemState Item { get; private set; }
    public string? Mode { get; private set; }
    public bool? PropsCollapsed { get; }
    public string? ActiveCanvasSurface { get; }
    public string Title => Item.Title;
    public TextDocument? EditorDocument => _editorSession?.Document;
    public AvalonDocumentBufferSession? EditorSession => _editorSession;
    public EditorInteractionCoordinator? EditorInteractions => _editorInteractions;
    internal string? SavedContentHash => _contentHash;
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

    /// <summary>Reading-surface activation routes through the SAME
    /// seams the editor uses — one navigation path, one tag path.</summary>
    internal void NavigateFromReading(EditorNavigationRequest request) =>
        _navigate?.Invoke(request);

    internal void ActivateTagFromReading(string tag) =>
        _activateTag?.Invoke(tag);

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
    public bool IsPlaceholder => !IsMarkdown;
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
                OnPropertyChanged(nameof(DirtyMarker));
            }
        }
    }

    public string DirtyMarker => IsDirty ? " •" : string.Empty;

    public bool IsMissingFromDisk
    {
        get => _isMissingFromDisk;
        private set => SetField(ref _isMissingFromDisk, value);
    }

    public string Status
    {
        get => _status;
        private set => SetField(ref _status, value);
    }

    public WorkspaceTabState Snapshot() =>
        new(Id, Item, Mode, PropsCollapsed, ActiveCanvasSurface);

    public void ReplaceItem(WorkspaceItemState item)
    {
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
            if (_startInteractionBackgroundWork)
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
        Item = Item with { Path = path };
        _editorInteractions?.InvalidateExternalState();
        IsMissingFromDisk = false;
        Status = string.Empty;
        NotifyItemChanged();
    }

    public void InvalidatePath()
    {
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

        _text = source._text;
        _contentHash = source._contentHash;
        IsExternallyStale = source.IsExternallyStale;
        _isDirty = source._isDirty;
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

    public bool Save()
    {
        if (!IsMarkdown || !IsDirty)
        {
            return true;
        }

        string saveText;
        try
        {
            EditorSaveSnapshot? snapshot = _editorSession?.PrepareSaveSnapshot();
            saveText = snapshot?.Text ?? Text;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            Status = $"Save blocked by editor integrity check: {exception.Message}";
            _documentChanged?.Invoke(this, null);
            return false;
        }

        // Ordinary saves route through the same file-before-index
        // core pipeline as task toggles (adversarial round 20): a
        // post-write failure leaves disk newer than the rolled-back
        // task index with no revision counter moved — the mutation
        // lease covers the interval, and a non-conflict failure
        // converts into the pending repair atomically, so a manual
        // checkbox edit can never resurrect ghost task rows either.
        Panels.TaskIndexRepairCoordinator? repairs = TaskRepairs;
        repairs?.BeginMutation(Path);
        bool leaseSettled = false;
        try
        {
            SaveReport report = _session.SaveText(Path, saveText, _contentHash);
            repairs?.EndMutation(Path, indexConsistent: true);
            leaseSettled = true;
            _contentHash = report.NewContentHash;
            IsExternallyStale = false;
            _text = saveText;
            _editorSession?.MarkSaved(saveText);
            IsDirty = false;
            Status = $"Saved {System.IO.Path.GetFileName(Path)}.";
            _documentChanged?.Invoke(this, null);
            return true;
        }
        catch (VaultException exception)
        {
            repairs?.EndMutation(
                Path, indexConsistent: exception is VaultException.WriteConflict);
            leaseSettled = true;
            Status = $"Save blocked: {exception.Message}";
            _documentChanged?.Invoke(this, null);
            return false;
        }
        finally
        {
            // Fail-closed on exception types the arms above miss -
            // a leaked lease bars every task query forever.
            if (!leaseSettled)
            {
                repairs?.EndMutation(Path, indexConsistent: false);
            }
        }
    }

    public void Dispose()
    {
        _disposed = true;
        _taskToggleGeneration++;
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
    public void Deactivate() => _editorInteractions?.CloseTransientUi();

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
    internal bool IsExternallyStale { get; private set; }

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
            IsDirty = true;
            _documentChanged?.Invoke(this, null);
        }
    }

    private void Load()
    {
        if (!IsMarkdown)
        {
            return;
        }

        try
        {
            _text = _session.ReadText(Path);
            _contentHash = SlateUniffiMethods.EditorTextContentHash(_text);
            _isDirty = false;
        }
        catch (VaultException exception)
        {
            Status = $"Could not open {Path}: {exception.Message}";
        }
    }

    private void NotifyItemChanged()
    {
        OnPropertyChanged(nameof(Item));
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(EditorAutomationName));
        OnPropertyChanged(nameof(Path));
        OnPropertyChanged(nameof(IsMarkdown));
        OnPropertyChanged(nameof(IsPlaceholder));
        OnPropertyChanged(nameof(KindLabel));
        OnPropertyChanged(nameof(PlaceholderText));
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
    private readonly Action<A11yEvent> _announce;
    private readonly Panels.TaskIndexRepairCoordinator _taskIndexRepairs;
    private readonly Func<WorkspaceTabViewModel, WorkspaceItemState, WorkspaceDirtyNavigationDecision>
        _dirtyNavigationDecision;
    private readonly Func<WorkspaceTabViewModel, WorkspaceDirtyNavigationDecision>
        _dirtyCloseDecision;
    private WorkspaceLeafOption _activeLeaf;
    private bool _isRightPaneVisible = true;
    private readonly bool _startInteractionBackgroundWork;

    public WorkspaceViewModel(
        VaultSession session,
        string vaultRoot,
        Func<IReadOnlyList<string>> expandedDirectoryPaths,
        Action<A11yEvent> announce,
        Func<WorkspaceTabViewModel, WorkspaceItemState, WorkspaceDirtyNavigationDecision>?
            dirtyNavigationDecision = null,
        Func<WorkspaceTabViewModel, WorkspaceDirtyNavigationDecision>?
            dirtyCloseDecision = null,
        bool startInteractionBackgroundWork = true,
        AppPreferencesStore? preferencesStore = null,
        Func<string, bool>? externalOpener = null)
    {
        _session = session;
        _persistence = new WorkspacePersistence(vaultRoot);
        _expandedDirectoryPaths = expandedDirectoryPaths;
        _announce = announce;
        _startInteractionBackgroundWork = startInteractionBackgroundWork;
        _dirtyNavigationDecision = dirtyNavigationDecision
            ?? ((_, _) => WorkspaceDirtyNavigationDecision.Cancel);
        _dirtyCloseDecision = dirtyCloseDecision
            ?? (_ => WorkspaceDirtyNavigationDecision.Cancel);
        EditorPreferences = new EditorPreferencesViewModel(
            _announce, preferencesStore: preferencesStore);
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
            externalOpener ?? DefaultExternalOpener,
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
            synchronousForTests: !startInteractionBackgroundWork);
        // The vault-wide Tasks Review leaf (W4-3): vault-lifetime
        // state, deliberately NOT keyed on the active note.
        TasksReview = new Panels.TasksReviewViewModel(
            session,
            announce,
            TryActivateTaskRow,
            TryToggleTaskInOpenTab,
            repairs: _taskIndexRepairs,
            synchronousForTests: !startInteractionBackgroundWork);
        // Round 3: a tab can open for a file BETWEEN the review's
        // NoOpenTab route decision and its direct write landing —
        // the workspace re-checks at write completion. Round 17:
        // that raced-open tab's NOTE PANEL can also have finished a
        // pre-write read — NoteSaved re-snapshots it (and ignores
        // non-active paths).
        TasksReview.DiskWriteLanded = (path, newContentHash) =>
        {
            ReconcileTabsAfterDirectTaskWrite(path, newContentHash);
            Panels.NoteSaved(path);
        };
        // Round 15: a repair landing inside a review load worker
        // refreshes the note panel too — both surfaces converge.
        TasksReview.RepairLanded = path => Panels.NoteSaved(path);
        (_root, _activeGroup) = Restore(_persistence.Load());
        SyncPanels();

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
            _ => RunWorkspaceMutation(() => ActiveGroup.ActiveTab?.ToggleViewMode()),
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
        FocusNextPaneCommand = new RelayCommand(_ => FocusPane(1), _ => Groups.Count > 1);
        FocusPreviousPaneCommand = new RelayCommand(_ => FocusPane(-1), _ => Groups.Count > 1);
        GrowPaneCommand = new RelayCommand(_ => ResizeActivePane(0.05), _ => Groups.Count > 1);
        ShrinkPaneCommand = new RelayCommand(_ => ResizeActivePane(-0.05), _ => Groups.Count > 1);
        SaveActiveCommand = new RelayCommand(_ => SaveActive(), _ => ActiveGroup.ActiveTab?.IsMarkdown == true);
        ToggleRightPaneCommand = new RelayCommand(_ => IsRightPaneVisible = !IsRightPaneVisible, _ => true);
        OpenTasksReviewCommand = new RelayCommand(_ => OpenTasksReview(), _ => true);
    }

    public event EventHandler<string>? FileOpened;
    public event EventHandler<string>? EditorTagActivated;

    public static IReadOnlyList<WorkspaceLeafOption> Leaves { get; } =
    [
        new("outline", "Outline"),
        new("backlinks", "Backlinks"),
        new("outgoingLinks", "Outgoing links"),
        new("connections", "Connections"),
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

    /// <summary>The W4-2 link/structure leaf data (backlinks, outgoing
    /// links, outline, embeds).</summary>
    public Panels.RightPanePanelsViewModel Panels { get; }

    public Panels.TasksReviewViewModel TasksReview { get; }

    /// <summary>Re-derive the panels' active note from the workspace —
    /// called from every activation funnel (tab activation, pane focus,
    /// workspace mutations). Same-path calls are no-ops in the panels
    /// VM, so over-calling is safe and refetch-free.</summary>
    internal void SyncPanels() =>
        Panels.NoteChanged(
            ActiveGroup.ActiveTab is { IsMarkdown: true } tab ? tab.Path : null);

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

    public WorkspaceLeafOption ActiveLeaf
    {
        get => _activeLeaf;
        set
        {
            if (value is not null && SetField(ref _activeLeaf, value))
            {
                _announce(new A11yEvent.LeafPanelShown(value.Title));
                // Rail reveal of the review is an idempotent snapshot
                // load (mac ensureVaultTasksLoaded); only the review
                // COMMAND forces a fresh page.
                if (string.Equals(value.Id, "tasksReview", StringComparison.Ordinal))
                {
                    TasksReview.EnsureLoaded();
                }
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
    }

    public bool IsRightPaneVisible
    {
        get => _isRightPaneVisible;
        set
        {
            if (SetField(ref _isRightPaneVisible, value))
            {
                _announce(value ? new A11yEvent.RightPaneShown() : new A11yEvent.RightPaneHidden());
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

    public void OpenPath(string path, WorkspaceOpenTarget target = WorkspaceOpenTarget.CurrentTab) =>
        RunWorkspaceMutation(() => OpenPathCore(path, target));

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

    private void ActivateEditorTag(string tag)
    {
        EditorTagActivated?.Invoke(this, tag);
        _announce(new A11yEvent.HostComposed(
            $"Filtered files by tag {tag}.",
            A11yPriority.Medium));
    }

    private bool OpenPathCore(string path, WorkspaceOpenTarget target)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        WorkspaceItemState item = ItemForPath(path);
        if (TryOpenItem(item, target))
        {
            FileOpened?.Invoke(this, path);
            Persist();
            return true;
        }

        return false;
    }

    public void OpenGraph() => RunWorkspaceMutation(() => OpenItem(
        new WorkspaceItemState(WorkspaceItemKind.Graph, "graph:singleton"),
        WorkspaceOpenTarget.NewTab));

    public bool SaveAll()
    {
        bool saved = true;
        foreach (WorkspaceTabViewModel tab in Groups.SelectMany(group => group.Tabs))
        {
            saved &= tab.Save();
        }

        return saved;
    }

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

        Persist();
        // A rename that touched the ACTIVE tab changed its Path in
        // place — without a re-derive the panels stay bound to the
        // old path and ignore every save on the new one (adversarial
        // round 2).
        SyncPanels();
    }

    public void InvalidatePath(string path)
    {
        string invalidated = NormalizeWorkspacePath(path);
        if (invalidated.Length == 0)
        {
            return;
        }

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
        RaiseCommandStates();
        Persist();
        if (affected > 0)
        {
            // W0.5-3 residue: Windows missing-editor availability copy.
            _announce(new A11yEvent.HostComposed(
                $"{System.IO.Path.GetFileName(invalidated)} is missing from disk. Open editor content was preserved.",
                A11yPriority.High));
        }
    }

    public void Dispose()
    {
        // Panels first: their workers hold the shared session, which
        // the vault lifecycle disposes right after this workspace —
        // invalidate every in-flight load before that happens.
        Panels.Shutdown();
        TasksReview.Shutdown();
        Persist();
        foreach (WorkspaceTabViewModel tab in Groups.SelectMany(group => group.Tabs))
        {
            tab.Dispose();
        }

        EditorPreferences.Dispose();
    }

    private void SaveActive()
    {
        if (ActiveGroup.ActiveTab is WorkspaceTabViewModel tab && tab.Save())
        {
            _announce(new A11yEvent.NoteSaved(System.IO.Path.GetFileName(tab.Path)));
            // Headings move under edits — the outline leaf re-reads
            // after a save (link rows deliberately do not; mac parity).
            Panels.NoteSaved(tab.Path);
        }
    }

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
        && string.Equals(left.Path, right.Path, StringComparison.Ordinal);

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
                        Panels.NoteSaved(path);
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
            Panels.NoteSaved(path);
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
            Panels.NoteSaved(source.Path);
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
