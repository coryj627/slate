// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Canvas;
using uniffi.slate_uniffi;

namespace SlateWindows;

/// <summary>
/// W6-1 PR A (#745): the canvas document registry and the three surface
/// commands — the workspace half of the canvas surface, living under
/// <c>Canvas/</c> so the funnel census (contract A6) covers it, exactly
/// as mac keeps its <c>AppState+Canvas*.swift</c> extensions inside
/// <c>Sources/SlateMac/Canvas/</c> for the same reason.
///
/// One document per byte-exact vault-relative path (contract A1), the
/// W4-6 Bases registry pattern: <see cref="CanvasDocumentFor"/> is the
/// only construction site, the release sweep is the only shutdown site,
/// and a rename re-keys rather than mutating a document's path (CD-32).
/// </summary>
internal sealed partial class WorkspaceViewModel
{
    private readonly Dictionary<string, CanvasDocumentViewModel> _canvasDocuments =
        new(StringComparer.Ordinal);

    /// <summary>The active tab's canvas document, or null — every
    /// <c>slate.canvas.*</c> command gates on this (the Bases
    /// <c>ActiveBaseDocument</c> precedent). Keyed on the ATTACHED
    /// document, not the tab kind.</summary>
    internal CanvasDocumentViewModel? ActiveCanvasDocument =>
        ActiveGroup.ActiveTab?.Canvas;

    private const string CanvasKeyPrefix = "canvas:";

    private static string CanvasKey(string path) => CanvasKeyPrefix + path;

    /// <summary>The registry's 0→1 transition (contract A1): a miss
    /// constructs, installs the seams and loads — which is where the
    /// once-per-open degraded announcement lands (contract A4), so a
    /// second pane on the same path is a hit and hears nothing.</summary>
    internal CanvasDocumentViewModel CanvasDocumentFor(
        string path, CanvasSelection? seedSelection = null, string? retargetedFrom = null)
    {
        string key = CanvasKey(path);
        if (!_canvasDocuments.TryGetValue(key, out CanvasDocumentViewModel? document))
        {
            document = new CanvasDocumentViewModel(
                _session,
                path,
                new CanvasAnnouncer(_announceRendered),
                synchronousForTests: !_startInteractionBackgroundWork,
                retargetedFrom: retargetedFrom,
                // Contract C13: the verbosity is READ at every announce,
                // not copied in — a live switch reaches every open canvas
                // with nothing to push.
                verbosity: () => CanvasPreferences.Verbosity);
            if (seedSelection is not null)
            {
                document.Selection.SeedFrom(seedSelection);
            }
            // §E TE-8: a row surface's Edit Card reaches the sheet
            // through the document's request — the workspace owns the
            // one sheet property the modal machinery watches.
            document.CardEditorRequested +=
                nodeId => CanvasCardEditorSheet = document.OpenCardEditor(nodeId);
            _canvasDocuments[key] = document;
            InstallCanvasDocumentSeams(document);
            document.Load();
        }
        return document;
    }

    /// <summary>The document never opens tabs or launches the shell; it
    /// hands the workspace what it decided (contract A13), which owns
    /// the ONE navigation seam and the shared external-link policy's
    /// opener.</summary>
    private void InstallCanvasDocumentSeams(CanvasDocumentViewModel document)
    {
        document.OpenFileCardFromSurface = (path, anchor) =>
        {
            bool navigated = false;
            RunWorkspaceMutation(() =>
            {
                navigated = OpenPathCore(path, WorkspaceOpenTarget.CurrentTab);
                if (navigated && anchor is not null)
                {
                    WorkspaceGroupViewModel group = ActiveGroup;
                    WorkspaceTabViewModel? tab = group.ActiveTab;
                    _ = tab?.NavigateToAnchor(
                        anchor,
                        null,
                        _announce,
                        () => ReferenceEquals(ActiveGroup, group)
                            && ReferenceEquals(group.ActiveTab, tab));
                }
            });
            return navigated;
        };
        document.OpenExternalLinkFromSurface = target => _externalOpener(target);
        // The media hand-off (contract A13/CD-38): the document holds a
        // VAULT-RELATIVE target and the workspace owns the root. The
        // policy resolves the target through an OPENED HANDLE,
        // revalidates containment immediately before the launch, and
        // hands `_externalOpener` the fully-resolved terminal path — so
        // ShellExecute's own re-resolution has no reparse point left to
        // redirect. Fail closed; the TOCTOU residual is CD-38.
        document.OpenMediaCardFromSurface = target =>
            CanvasMediaPolicy.OpenMediaInVault(_vaultRoot, target, _externalOpener);
        // Contract A15: the persisted token follows the shared surface
        // for EVERY tab on this path, since they share the document.
        document.SurfaceChanged += (sender, surface) =>
        {
            string? token = surface switch
            {
                CanvasSurfaceKind.Table => "table",
                CanvasSurfaceKind.Visual => "visual",
                _ => null,
            };
            foreach (WorkspaceTabViewModel tab in Groups
                .SelectMany(group => group.Tabs)
                .Where(candidate => ReferenceEquals(candidate.Canvas, sender)))
            {
                tab.SetActiveCanvasSurface(token);
            }
            Persist();
        };
    }

    /// <summary>
    /// Force the open document at this path to re-read the file
    /// (W6-1 B2). The registry is keyed by path and a hit returns the
    /// document as it stands, so a disk change the shell itself made —
    /// a history restore is the reachable one today; PR E's funnel and
    /// the file watcher are next — leaves the surface contradicting the
    /// bytes. Selection survives wherever the node id still exists:
    /// <c>PublishReady</c> only re-seats when the selected node is gone.
    /// </summary>
    private void ReloadCanvasDocumentAt(string path)
    {
        if (_canvasDocuments.TryGetValue(
            CanvasKey(path), out CanvasDocumentViewModel? document))
        {
            document.Load();
        }
    }

    /// <summary>The Bases sweep, verbatim in shape (contract A1): the
    /// live key set comes from the open tabs, so no counter can
    /// disagree with what the user can see. A retired document closes
    /// its handle and takes its selection and marks with it.</summary>
    private void ReleaseUnreferencedCanvasDocuments()
    {
        if (_canvasDocuments.Count == 0)
        {
            return;
        }
        var live = new HashSet<string>(StringComparer.Ordinal);
        // The TAB objects, not just their paths: a request is addressed
        // to a tab, and a document that survives because a SECOND pane
        // still shows it must not go on holding the closed pane's
        // request — or its object graph (contracts A14/C10).
        List<WorkspaceTabViewModel> canvasTabs = [];
        foreach (WorkspaceTabViewModel tab in Groups.SelectMany(group => group.Tabs))
        {
            if (tab.IsCanvas)
            {
                _ = live.Add(CanvasKey(tab.Path));
                canvasTabs.Add(tab);
            }
        }
        foreach (string key in _canvasDocuments.Keys
            .Where(candidate => !live.Contains(candidate))
            .ToList())
        {
            CanvasDocumentViewModel retired = _canvasDocuments[key];
            retired.Shutdown();
            TrackRetiredBasesWork(retired.WhenHandleClosed());
            _ = _canvasDocuments.Remove(key);
        }
        // The documents that SURVIVED: this is the tab-set boundary, so
        // it is where a request addressed to a tab that is gone stops
        // being pending.
        foreach (CanvasDocumentViewModel document in _canvasDocuments.Values)
        {
            // Asked PER DOCUMENT, not once for the window. A tab that is
            // still open is not thereby still a live address for THIS
            // document: retarget a tab from canvas X to canvas Y and the
            // tab survives, so a global "is this tab still some canvas
            // owner" answered yes and X went on holding a request no
            // surface would ever deliver — the pane showing that tab now
            // renders Y. Ownership is the pairing of a tab WITH a
            // document, so that is what the predicate asks.
            var liveOwners = new HashSet<object>(
                canvasTabs.Where(tab => ReferenceEquals(tab.Canvas, document)),
                ReferenceEqualityComparer.Instance);
            document.DropRequestsAddressedOutside(liveOwners);
        }
    }

    /// <summary>
    /// Rename/move (CD-32): the registry keys by path and a document's
    /// path is immutable, so a rename retires the old document and
    /// attaches a fresh one at the new spelling — the Bases
    /// <c>RetargetBaseDocuments</c> shape, whose round-2 blocker was
    /// exactly the alternative (a renamed tab keeping a document that
    /// reopens the OLD path forever). The previous selection and marks
    /// ride across: a rename is not a close.
    /// </summary>
    private void RetargetCanvasDocuments(string source, string destination)
    {
        // Whether a document is ALREADY open at the destination decides
        // whether the reload below is redundant: the re-key loop's
        // `CanvasDocumentFor` LOADS on a miss, so reloading after it
        // would open the same file twice and speak the degraded-load
        // sentence twice with it.
        bool destinationWasOpen =
            _canvasDocuments.ContainsKey(CanvasKey(destination));
        foreach (string oldKey in _canvasDocuments.Keys
            .Where(key => TryRetargetPath(
                key[CanvasKeyPrefix.Length..], source, destination, out _))
            .ToList())
        {
            CanvasDocumentViewModel oldDocument = _canvasDocuments[oldKey];
            _ = _canvasDocuments.Remove(oldKey);
            oldDocument.Shutdown();
            TrackRetiredBasesWork(oldDocument.WhenHandleClosed());
            _ = TryRetargetPath(
                oldDocument.Path, source, destination, out string newPath);
            foreach (WorkspaceTabViewModel tab in Groups
                .SelectMany(group => group.Tabs)
                .Where(candidate => candidate.IsCanvas
                    && string.Equals(candidate.Path, newPath, StringComparison.Ordinal)))
            {
                tab.AttachCanvasDocument(CanvasDocumentFor(
                    newPath,
                    seedSelection: oldDocument.Selection,
                    retargetedFrom: oldDocument.Path));
            }
        }
        // A rename lands NEW BYTES at the DESTINATION, and a document
        // ALREADY open there is now stale (W6-1 B3). Two shapes reach
        // this: an atomic save (write `x.tmp`, rename it onto the open
        // `board.canvas` — the source was never open, so the loop above
        // did nothing at all), and both-open, where the loop re-keyed
        // the source's tabs onto the destination's existing document
        // without re-reading it. Same answer for both, and it must come
        // AFTER the re-key so the surviving document is the one that
        // reloads.
        //
        // A destination that was NOT open is skipped: the loop just
        // constructed and loaded it, and loading again would re-read the
        // file and announce the degraded-load sentence a second time.
        if (destinationWasOpen)
        {
            ReloadCanvasDocumentAt(destination);
        }
    }

    /// <summary>Vault close: every document holds the shared session and
    /// a native handle. Shut down and drained with the Bases documents
    /// in the one bounded teardown drain (contract A1/A17).</summary>
    private void ShutdownCanvasDocuments(List<Task> drains)
    {
        foreach (CanvasDocumentViewModel document in _canvasDocuments.Values)
        {
            document.Shutdown();
            drains.Add(document.WhenHandleClosed());
        }
        _canvasDocuments.Clear();
    }

    // --- Surface commands (contract A18) --------------------------------

    private RelayCommand? _canvasShowOutlineCommand;
    private RelayCommand? _canvasShowTableCommand;
    private RelayCommand? _canvasShowVisualCommand;

    public System.Windows.Input.ICommand CanvasShowOutlineCommand =>
        _canvasShowOutlineCommand ??= new RelayCommand(
            _ => ActiveCanvasDocument?.ShowSurface(CanvasSurfaceKind.Outline),
            _ => ActiveCanvasDocument is not null);

    /// <summary>Enabled in W6-1 PR B, now that the projection exists
    /// (contract B10): the same one surface switch the header's radio
    /// drives, so the state, the persisted token and the spoken sentence
    /// cannot disagree.</summary>
    public System.Windows.Input.ICommand CanvasShowTableCommand =>
        _canvasShowTableCommand ??= new RelayCommand(
            _ => ActiveCanvasDocument?.ShowSurface(CanvasSurfaceKind.Table),
            _ => ActiveCanvasDocument is not null);

    /// <summary>The visual surface switch (contract A18; §D TD-6
    /// flips it executable — B12's rule: the PR that makes a command
    /// executable is the one that delivers it).</summary>
    public System.Windows.Input.ICommand CanvasShowVisualCommand =>
        _canvasShowVisualCommand ??= new RelayCommand(
            _ => ActiveCanvasDocument?.ShowSurface(CanvasSurfaceKind.Visual),
            _ => ActiveCanvasDocument is not null);

    // --- Viewport (§D D14, task TD-5) -----------------------------------
    // Each command routes to the navigator's verb; the verb owns the
    // load gate, the pane resolution and the no-pane refusal (§D D7),
    // so the palette and the chord speak identically.

    private CanvasCardEditorViewModel? _canvasCardEditorSheet;

    /// <summary>W6-1 §E TE-7: the open card editor sheet — the modal
    /// flat read, the sheet-presentation observer and the menu's
    /// disable trigger all watch THIS one workspace property (the
    /// sheet convention every census enforces).</summary>
    public CanvasCardEditorViewModel? CanvasCardEditorSheet
    {
        get => _canvasCardEditorSheet;
        private set => SetField(ref _canvasCardEditorSheet, value);
    }

    /// <summary>Open the editor for the active canvas selection (M8's
    /// carve-out rides the sheet's own Esc). Refusals — no document,
    /// no selection, not a text card — announce through the
    /// document's arms and open nothing.</summary>
    public void OpenCanvasCardEditor()
    {
        if (ActiveCanvasDocument is not { } document
            || document.Selection.Selected is not { } selected)
        {
            return;
        }
        CanvasCardEditorSheet = document.OpenCardEditor(selected);
    }

    /// <summary>The sheet closed — a commit, a no-op, or a deliberate
    /// discard; never a refusal, whose whole point is the sheet
    /// standing.</summary>
    public void CloseCanvasCardEditor() => CanvasCardEditorSheet = null;

    public System.Windows.Input.ICommand CanvasZoomInCommand =>
        _canvasZoomInCommand ??= new RelayCommand(
            _ => ActiveCanvasDocument?.Navigator.ZoomIn(),
            _ => ActiveCanvasDocument is not null);

    public System.Windows.Input.ICommand CanvasZoomOutCommand =>
        _canvasZoomOutCommand ??= new RelayCommand(
            _ => ActiveCanvasDocument?.Navigator.ZoomOut(),
            _ => ActiveCanvasDocument is not null);

    public System.Windows.Input.ICommand CanvasActualSizeCommand =>
        _canvasActualSizeCommand ??= new RelayCommand(
            _ => ActiveCanvasDocument?.Navigator.ActualSize(),
            _ => ActiveCanvasDocument is not null);

    public System.Windows.Input.ICommand CanvasFitCanvasCommand =>
        _canvasFitCanvasCommand ??= new RelayCommand(
            _ => ActiveCanvasDocument?.Navigator.FitCanvas(),
            _ => ActiveCanvasDocument is not null);

    public System.Windows.Input.ICommand CanvasZoomToSelectionCommand =>
        _canvasZoomToSelectionCommand ??= new RelayCommand(
            _ => ActiveCanvasDocument?.Navigator.ZoomToSelection(),
            _ => ActiveCanvasDocument is not null);

    private System.Windows.Input.ICommand? _canvasZoomInCommand;
    private System.Windows.Input.ICommand? _canvasZoomOutCommand;
    private System.Windows.Input.ICommand? _canvasActualSizeCommand;
    private System.Windows.Input.ICommand? _canvasFitCanvasCommand;
    private System.Windows.Input.ICommand? _canvasZoomToSelectionCommand;

    // --- Navigator, filter, Where-am-I, modes (contract C1) -------------

    private RelayCommand? _canvasNextCardCommand;
    private RelayCommand? _canvasPreviousCardCommand;
    private RelayCommand? _canvasEnterGroupCommand;
    private RelayCommand? _canvasExitGroupCommand;
    private RelayCommand? _canvasFollowForwardCommand;
    private RelayCommand? _canvasFollowBackCommand;
    private RelayCommand? _canvasTracePathCommand;
    private RelayCommand? _canvasWhereAmICommand;
    private RelayCommand? _canvasFilterCardsCommand;
    private RelayCommand? _canvasClearFilterCommand;
    private RelayCommand? _canvasCommitModeCommand;
    private RelayCommand? _canvasCancelModeCommand;
    private RelayCommand? _canvasToggleFollowSelectionCommand;

    /// <summary>
    /// Every navigator row is enabled whenever a canvas is active,
    /// whatever its load state — rule R1's "commands are always
    /// reachable" plus t0's never-silent rule: a verb the palette
    /// disabled cannot tell the user why the canvas will not answer, and
    /// answering accurately is exactly what the navigator's state mapping
    /// is for (contract C4).
    /// </summary>
    private RelayCommand NavigatorCommand(Action<CanvasNavigator> verb) =>
        new(
            _ =>
            {
                if (ActiveCanvasDocument is { } document)
                {
                    verb(document.Navigator);
                }
            },
            _ => ActiveCanvasDocument is not null);

    public System.Windows.Input.ICommand CanvasNextCardCommand =>
        _canvasNextCardCommand ??= NavigatorCommand(
            navigator => navigator.NextCard());

    public System.Windows.Input.ICommand CanvasPreviousCardCommand =>
        _canvasPreviousCardCommand ??= NavigatorCommand(
            navigator => navigator.PreviousCard());

    public System.Windows.Input.ICommand CanvasEnterGroupCommand =>
        _canvasEnterGroupCommand ??= NavigatorCommand(
            navigator => navigator.EnterGroup());

    public System.Windows.Input.ICommand CanvasExitGroupCommand =>
        _canvasExitGroupCommand ??= NavigatorCommand(
            navigator => navigator.ExitGroup());

    public System.Windows.Input.ICommand CanvasFollowConnectionForwardCommand =>
        _canvasFollowForwardCommand ??= NavigatorCommand(
            navigator => navigator.FollowConnection(forward: true));

    public System.Windows.Input.ICommand CanvasFollowConnectionBackCommand =>
        _canvasFollowBackCommand ??= NavigatorCommand(
            navigator => navigator.FollowConnection(forward: false));

    public System.Windows.Input.ICommand CanvasTracePathCommand =>
        _canvasTracePathCommand ??= NavigatorCommand(
            navigator => navigator.TracePath());

    public System.Windows.Input.ICommand CanvasWhereAmICommand =>
        _canvasWhereAmICommand ??= NavigatorCommand(
            navigator => navigator.WhereAmI());

    public System.Windows.Input.ICommand CanvasFilterCardsCommand =>
        _canvasFilterCardsCommand ??= NavigatorCommand(
            navigator => navigator.FilterCards());

    public System.Windows.Input.ICommand CanvasClearFilterCommand =>
        _canvasClearFilterCommand ??= NavigatorCommand(
            navigator => navigator.ClearFilter());

    /// <summary>
    /// M6/M2 through the palette. Unlike the movement rows these are
    /// gated on a mode being active: there is no vocabulary for "no mode
    /// is running", and the registrar's own unavailable sentence IS the
    /// answer a disabled row gives (contract C9).
    /// </summary>
    public System.Windows.Input.ICommand CanvasCommitModeCommand =>
        _canvasCommitModeCommand ??= new RelayCommand(
            _ => ActiveCanvasDocument?.Navigator.CommitMode(),
            _ => ActiveCanvasDocument?.Modes.CanCommitOrCancel == true);

    public System.Windows.Input.ICommand CanvasCancelModeCommand =>
        _canvasCancelModeCommand ??= new RelayCommand(
            _ => ActiveCanvasDocument?.Navigator.CancelMode(),
            _ => ActiveCanvasDocument?.Modes.CanCommitOrCancel == true);

    /// <summary>The follow toggle, live with the viewport (§D TD-6):
    /// the navigator's verb owns the gate and the sentence, so the
    /// palette and any future chord speak identically.</summary>
    public System.Windows.Input.ICommand CanvasToggleFollowSelectionCommand =>
        _canvasToggleFollowSelectionCommand ??= new RelayCommand(
            _ => ActiveCanvasDocument?.Navigator.ToggleFollowSelection(),
            _ => ActiveCanvasDocument is not null);
}
