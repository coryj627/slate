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
            // §F TF-8: the picker and prompt sheets — workspace
            // properties, because the modal machinery watches exactly
            // one place.
            document.CardPickerRequested += (request, model) =>
                CanvasCardPickerSheet =
                    new CanvasCardPickerViewModel(document, request, model);
            document.ConnectPromptRequested += stage =>
            {
                CanvasCardPickerSheet = null;
                _ = TryPresentCanvasPrompt(
                    CanvasPromptViewModel.ConnectLabel(document, stage));
            };
            document.GroupRenameRequested += (groupId, current) =>
                _ = TryPresentCanvasPrompt(
                    CanvasPromptViewModel.RenameGroup(document, groupId, current));
            document.SetColorRequested += () =>
                _ = TryPresentCanvasPrompt(CanvasPromptViewModel.SetColor(document));
            // §G2 TG2-1: the New Group and Add Link prompts carry the
            // invoking tab as the operation's owner; the group Delete's
            // confirmation is E8's Ungroup-or-Cancel.
            document.NewGroupRequested += context =>
                _ = TryPresentCanvasPrompt(CanvasPromptViewModel.NewGroup(document, context));
            document.AddLinkRequested += context =>
                _ = TryPresentCanvasPrompt(CanvasPromptViewModel.AddLink(document, context));
            // §G2 TG2-2: the choices pickers.
            document.MoveIntoGroupRequested += (context, groups) =>
                _ = TryPresentCanvasPrompt(
                    CanvasPromptViewModel.MoveIntoGroup(document, context, groups));
            document.PickConnectionRequested += (context, neighbors, toDelete) =>
                _ = TryPresentCanvasPrompt(
                    CanvasPromptViewModel.PickConnection(document, context, neighbors, toDelete));
            document.EditConnectionRequested += (context, neighbor) =>
                _ = TryPresentCanvasPrompt(
                    CanvasPromptViewModel.EditConnectionDirection(document, context, neighbor));
            // §G2 TG2-3 (IG2-50): the vault picker presents only for the
            // owner that is still current; a load that completes for a
            // tab that moved on presents nothing and says so.
            // §G2 TG2-4 (G2-9, IG2-23): the created card's editor opens ONLY for
            // an owner that is still current — the active tab, or the
            // document itself when the verb was invoked unaddressed and the
            // document is the active canvas.
            document.CreatedEditorRequested += (owner, nodeId) =>
            {
                bool current = ReferenceEquals(owner, ActiveGroup.ActiveTab)
                    || (ReferenceEquals(owner, document) && ReferenceEquals(ActiveCanvasDocument, document));
                if (current && ReferenceEquals(ActiveCanvasDocument, document))
                {
                    CanvasCardEditorSheet = document.OpenCardEditor(nodeId);
                }
            };
            // §G2 TG2-6 (IG2-6): the structural-aware route — the sidebar's
            // note creator rides into the prompt; without one (no sidebar)
            // the verb cannot write and says so.
            document.ConvertToNoteRequested += (context, suggested) =>
            {
                if (CanvasNoteCreator is not { } creator)
                {
                    document.SpeakNoteCreateFailed(suggested, "no vault to write to");
                    return;
                }
                _ = TryPresentCanvasPrompt(
                    CanvasPromptViewModel.ConvertToNote(document, context, suggested, creator));
            };
            document.ConnectedDirectionRequested += context =>
                _ = TryPresentCanvasPrompt(CanvasPromptViewModel.ConnectedDirection(document, context));
            document.VaultPickerRequested += request =>
            {
                if (!ReferenceEquals(request.Owner, ActiveGroup.ActiveTab))
                {
                    document.SpeakPickDifferentTarget();
                    return;
                }
                CanvasCardPickerSheet = new CanvasVaultFilePickerViewModel(document, request);
            };
            document.UngroupConfirmRequested += (groupId, title) =>
                _ = TryPresentCanvasPrompt(
                    CanvasPromptViewModel.UngroupConfirm(document, groupId, title));
            document.GroupMarkedRequested += () =>
                _ = TryPresentCanvasPrompt(CanvasPromptViewModel.GroupMarked(document));
            document.ColorMarkedRequested += () =>
                _ = TryPresentCanvasPrompt(CanvasPromptViewModel.SetColorMarked(document));
            document.MarksListRequested += owner =>
                _ = TryPresentCanvasPrompt(
                    CanvasPromptViewModel.MarksList(document, owner, CloseCanvasPromptIfCurrent));
            _canvasDocuments[key] = document;
            InstallCanvasDocumentSeams(document);
            document.Load();
        }
        return document;
    }

    /// <summary>§H TH-6 (IH-40): the anchor navigation's announcements on a
    /// CANVAS-origin open. A miss — the editor's generic heading-not-found,
    /// or its block-not-found sentence — is spoken as core's canvas reason
    /// with the subpath and the file, as the canvas event through the
    /// shell's announcer, once; everything else (the landing) goes to the
    /// shell as before.</summary>
    internal static void RouteCanvasAnchorAnnouncement(
        LinkAnchor anchor,
        string path,
        A11yEvent spoken,
        Action<A11yEvent> shell)
    {
        ArgumentNullException.ThrowIfNull(anchor);
        ArgumentNullException.ThrowIfNull(spoken);
        ArgumentNullException.ThrowIfNull(shell);
        bool miss = spoken is A11yEvent.HeadingNotFound
            || (spoken is A11yEvent.HostComposed composed
                && composed.Text.EndsWith(" was not found.", StringComparison.Ordinal));
        if (miss)
        {
            // §H TH-11 (E14, IH-47): the canvas document is retired at the
            // tab-set boundary the moment the note replaces it in the
            // current tab — TH-6's route through the document's announcer
            // spoke into a retired funnel and the reason was LOST. The
            // reason is core's canvas sentence either way; it rides the
            // shell's announcer as the canvas event, alive regardless of
            // the document's lifetime, once.
            shell(new A11yEvent.Canvas(new CanvasA11yEvent.CanvasBlocked(
                new CanvasBlockedReason.HeadingNotFound(
                    anchor.Text, System.IO.Path.GetFileName(path)))));
            return;
        }
        shell(spoken);
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
                    // §H TH-6 (IH-40), TH-11: the editor's generic miss becomes
                    // the canvas reason, once, through the shell's announcer.
                    _ = tab?.NavigateToAnchor(
                        anchor,
                        null,
                        spoken => RouteCanvasAnchorAnnouncement(
                            anchor, path, spoken, _announce),
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

    private ICanvasPickerSheet? _canvasCardPickerSheet;

    /// <summary>§G2 TG2-3 (G2-6, G2D-1): the ONE picker slot — the card
    /// picker or the vault file picker, through the shared contract; the
    /// modal arm is CanvasCardPicker for either.</summary>
    public ICanvasPickerSheet? CanvasCardPickerSheet
    {
        get => _canvasCardPickerSheet;
        private set => SetField(ref _canvasCardPickerSheet, value);
    }

    public void CloseCanvasCardPicker() => CanvasCardPickerSheet = null;

    /// <summary>Enter's arm on the picker sheet: a routed pick closes
    /// it; a refusal keeps it, filter and highlight intact.</summary>
    public void ConfirmCanvasCardPick()
    {
        if (CanvasCardPickerSheet is not { } sheet)
        {
            return;
        }
        // §G2 TG2-3 (IG2-50): the OWNER half of a vault pick's validation —
        // the invoking tab must still be the active one; a pick from a
        // tab that moved on is a guess, refused with the sheet kept.
        if (sheet is CanvasVaultFilePickerViewModel vault
            && !ReferenceEquals(vault.Request.Owner, ActiveGroup.ActiveTab))
        {
            vault.RefuseStale();
            return;
        }
        if (sheet.Confirm())
        {
            CanvasCardPickerSheet = null;
        }
    }

    private CanvasPromptViewModel? _canvasPromptSheet;

    public CanvasPromptViewModel? CanvasPromptSheet
    {
        get => _canvasPromptSheet;
        private set
        {
            CanvasPromptViewModel? was = _canvasPromptSheet;
            if (SetField(ref _canvasPromptSheet, value))
            {
                // §G TG-2: the one place a sheet stops being current,
                // however it closed — a live variant unsubscribes here.
                was?.Closed();
            }
        }
    }

    /// <summary>§G TG-2 (G4): Delete on the marks list unmarks the
    /// active row; on any other prompt the key means nothing.</summary>
    public void DeleteOnCanvasPrompt() =>
        (CanvasPromptSheet as CanvasMarksListPrompt)?.UnmarkActive();

    /// <summary>§G TG-2 (G3/G4): the marks list's Clear control — G3's
    /// verb, then the sheet closes with the emptied store.</summary>
    public void ClearMarksFromCanvasPrompt()
    {
        if (CanvasPromptSheet is CanvasMarksListPrompt && ActiveCanvasDocument is { } document)
        {
            document.ClearMarks();
        }
    }

    public System.Windows.Input.ICommand CanvasPromptClearMarksCommand =>
        _canvasPromptClearMarksCommand ??= new RelayCommand(
            _ => ClearMarksFromCanvasPrompt(),
            _ => CanvasPromptSheet is CanvasMarksListPrompt);

    private RelayCommand? _canvasPromptClearMarksCommand;

    public void CloseCanvasPrompt() => CanvasPromptSheet = null;

    /// <summary>§G TG-1 (IG-38): a prompt opens only when none is up
    /// — re-entrant opening is REFUSED, never an overwrite that a
    /// stale completion could later close. Unreachable from the
    /// surfaces (the sheet owns the keys and the palette refuses
    /// beneath it); the guard makes the programmatic path honest.</summary>
    internal bool TryPresentCanvasPrompt(CanvasPromptViewModel sheet)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        if (CanvasPromptSheet is not null)
        {
            return false;
        }
        CanvasPromptSheet = sheet;
        return true;
    }

    /// <summary>Enter's arm on the prompt sheet (§G TG-1, IG-38): the
    /// sheet closes on the submit's RESULT — Completed now, Pending
    /// when its exact operation LANDS (the completion marshals home
    /// and closes only if this sheet is still the current one), and
    /// never on Refused, which keeps the sheet and its draft.</summary>
    public void SubmitCanvasPrompt()
    {
        if (CanvasPromptSheet is not { } sheet)
        {
            return;
        }
        // The landing marshals HOME: the completion runs on the
        // funnel's worker (the TF-11 lesson), and the sheet is a UI
        // property — the submitting thread is the dispatcher to post to.
        System.Windows.Threading.Dispatcher home =
            System.Windows.Threading.Dispatcher.CurrentDispatcher;
        CanvasPromptSubmit result = sheet.Submit(
            () => home.BeginInvoke(() => CloseCanvasPromptIfCurrent(sheet)));
        if (result is CanvasPromptSubmit.Advanced advanced)
        {
            // §G2 TG2-1 (G2-4, IG2-44): the STAGE transition — one setter
            // call retires the predecessor (its Closed runs) and seats the
            // successor; a completion the predecessor captured compares
            // by reference and closes nothing.
            CanvasPromptSheet = advanced.Next;
        }
        else if (result == CanvasPromptSubmit.Completed)
        {
            CanvasPromptSheet = null;
        }
    }

    private void CloseCanvasPromptIfCurrent(CanvasPromptViewModel sheet)
    {
        if (ReferenceEquals(CanvasPromptSheet, sheet))
        {
            CanvasPromptSheet = null;
        }
    }

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
    private RelayCommand? _canvasNewCardCommand;
    private RelayCommand? _canvasWhereAmICommand;

    private RelayCommand? _canvasMoveModeCommand;

    private RelayCommand? _canvasPlaceBelowCommand;

    private RelayCommand? _canvasConnectToCommand;

    private RelayCommand? _canvasConnectModeCommand;

    private RelayCommand? _canvasToggleMarkCommand;

    private RelayCommand? _canvasDeleteMarkedCommand;

    private RelayCommand? _canvasGroupMarkedCommand;

    private RelayCommand? _canvasColorMarkedCommand;

    private RelayCommand? _canvasShowMarksCommand;

    private RelayCommand? _canvasClearMarksCommand;

    private RelayCommand? _canvasPlaceRightOfCommand;

    private RelayCommand? _canvasPlaceAboveCommand;

    private RelayCommand? _canvasPlaceLeftOfCommand;

    private RelayCommand? _canvasAlignWithCommand;

    private RelayCommand? _canvasResizeModeCommand;

    private RelayCommand? _canvasResizeDefaultSizeCommand;

    private RelayCommand? _canvasResizeFitContentCommand;
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

    /// <summary>§E TE-11 (E19): the New Card verb for the palette,
    /// the menu and the chord resolver. Gated only on a canvas being
    /// active - the funnel's admission table owns every other refusal
    /// and speaks it (contract C9).</summary>
    public System.Windows.Input.ICommand CanvasNewCardCommand =>
        _canvasNewCardCommand ??= new RelayCommand(
            _ => ActiveCanvasDocument?.CanvasNewCard(),
            _ => ActiveCanvasDocument is not null);

    public System.Windows.Input.ICommand CanvasWhereAmICommand =>
        _canvasWhereAmICommand ??= NavigatorCommand(
            navigator => navigator.WhereAmI());

    /// <summary>§G TG-2 (G7): Show Marked Cards — the owner is the
    /// active tab, the object the A14 landing is addressed to.</summary>
    public System.Windows.Input.ICommand CanvasShowMarksCommand =>
        _canvasShowMarksCommand ??= new RelayCommand(
            _ =>
            {
                if (ActiveCanvasDocument is { } document
                    && ActiveGroup.ActiveTab is { } tab)
                {
                    document.OpenMarksList(tab);
                }
            },
            _ => ActiveCanvasDocument is not null);

    public System.Windows.Input.ICommand CanvasGroupMarkedCommand =>
        _canvasGroupMarkedCommand ??= DocumentCommand(document => document.RequestGroupMarked());

    public System.Windows.Input.ICommand CanvasDeleteMarkedCommand =>
        _canvasDeleteMarkedCommand ??= DocumentCommand(document => document.CanvasDeleteMarked());

    public System.Windows.Input.ICommand CanvasColorMarkedCommand =>
        _canvasColorMarkedCommand ??= DocumentCommand(document => document.RequestColorMarked());

    public System.Windows.Input.ICommand CanvasToggleMarkCommand =>
        _canvasToggleMarkCommand ??= DocumentCommand(document => document.ToggleMark());

    public System.Windows.Input.ICommand CanvasClearMarksCommand =>
        _canvasClearMarksCommand ??= DocumentCommand(document => document.ClearMarks());

    // §G2 TG2-0 (G2-1/G2-2): the front doors over §E's shipped verbs.
    // Each resolves to a document seam whose OPENER speaks the refusal
    // (G2-3's table); the verbs that mint an operation take the invoking
    // TAB as owner (TE-1's initiating surface, IG2-34).
    public System.Windows.Input.ICommand CanvasDeleteCommand =>
        _canvasDeleteCommand ??= DocumentCommand(document => document.CanvasDeleteSelection());

    public System.Windows.Input.ICommand CanvasEditCardCommand =>
        _canvasEditCardCommand ??= DocumentCommand(
            document => document.RequestCardEditorForSelection());

    public System.Windows.Input.ICommand CanvasRenameGroupCommand =>
        _canvasRenameGroupCommand ??= DocumentCommand(
            document => document.RequestGroupRenameForSelection());

    public System.Windows.Input.ICommand CanvasSetColorCommand =>
        _canvasSetColorCommand ??= DocumentCommand(document => document.RequestSetColor());

    public System.Windows.Input.ICommand CanvasClearColorCommand =>
        _canvasClearColorCommand ??= DocumentCommand(
            (document, owner) => document.CanvasSetColor(null, owner: owner));

    public System.Windows.Input.ICommand CanvasNewGroupCommand =>
        _canvasNewGroupCommand ??= DocumentCommand(
            (document, owner) => document.RequestNewGroup(owner));

    public System.Windows.Input.ICommand CanvasAddLinkCommand =>
        _canvasAddLinkCommand ??= DocumentCommand(
            (document, owner) => document.RequestAddLink(owner));

    public System.Windows.Input.ICommand CanvasMoveIntoGroupCommand =>
        _canvasMoveIntoGroupCommand ??= DocumentCommand(
            (document, owner) => document.RequestMoveIntoGroup(owner));

    public System.Windows.Input.ICommand CanvasEditConnectionCommand =>
        _canvasEditConnectionCommand ??= DocumentCommand(
            (document, owner) => document.RequestEditConnection(owner));

    public System.Windows.Input.ICommand CanvasDeleteConnectionCommand =>
        _canvasDeleteConnectionCommand ??= DocumentCommand(
            (document, owner) => document.RequestDeleteConnection(owner));

    public System.Windows.Input.ICommand CanvasAddNoteCommand =>
        _canvasAddNoteCommand ??= DocumentCommand(
            (document, owner) => document.RequestVaultPick(CanvasVaultPickPurpose.Note, owner));

    public System.Windows.Input.ICommand CanvasAddMediaCommand =>
        _canvasAddMediaCommand ??= DocumentCommand(
            (document, owner) => document.RequestVaultPick(CanvasVaultPickPurpose.Media, owner));

    public System.Windows.Input.ICommand CanvasLocateFileCommand =>
        _canvasLocateFileCommand ??= DocumentCommand(
            (document, owner) => document.RequestVaultPick(CanvasVaultPickPurpose.Locate, owner));

    public System.Windows.Input.ICommand CanvasRemoveFromGroupCommand =>
        _canvasRemoveFromGroupCommand ??= DocumentCommand(
            (document, owner) => document.CanvasRemoveFromGroup(owner: owner));

    public System.Windows.Input.ICommand CanvasCreateConnectedCardCommand =>
        _canvasCreateConnectedCardCommand ??= DocumentCommand(
            (document, owner) => document.CanvasCreateConnectedCard(owner: owner));

    /// <summary>§G2 TG2-6: the sidebar as the canvas's note creator — set by
    /// the vault lifecycle when both exist.</summary>
    public ICanvasNoteCreator? CanvasNoteCreator { get; set; }

    public System.Windows.Input.ICommand CanvasConvertToNoteCommand =>
        _canvasConvertToNoteCommand ??= DocumentCommand(
            (document, owner) => document.RequestConvertToNote(owner));

    public System.Windows.Input.ICommand CanvasDuplicateCommand =>
        _canvasDuplicateCommand ??= DocumentCommand(
            (document, owner) => document.CanvasDuplicate(owner: owner));

    public System.Windows.Input.ICommand CanvasCreateConnectedCardDirectionalCommand =>
        _canvasCreateConnectedCardDirectionalCommand ??= DocumentCommand(
            (document, owner) => document.RequestCreateConnectedDirection(owner));

    private RelayCommand? _canvasDeleteCommand;
    private RelayCommand? _canvasEditCardCommand;
    private RelayCommand? _canvasRenameGroupCommand;
    private RelayCommand? _canvasSetColorCommand;
    private RelayCommand? _canvasClearColorCommand;
    private RelayCommand? _canvasNewGroupCommand;
    private RelayCommand? _canvasAddLinkCommand;
    private RelayCommand? _canvasMoveIntoGroupCommand;
    private RelayCommand? _canvasEditConnectionCommand;
    private RelayCommand? _canvasDeleteConnectionCommand;
    private RelayCommand? _canvasAddNoteCommand;
    private RelayCommand? _canvasAddMediaCommand;
    private RelayCommand? _canvasLocateFileCommand;
    private RelayCommand? _canvasRemoveFromGroupCommand;
    private RelayCommand? _canvasCreateConnectedCardCommand;
    private RelayCommand? _canvasCreateConnectedCardDirectionalCommand;
    private RelayCommand? _canvasDuplicateCommand;
    private RelayCommand? _canvasConvertToNoteCommand;

    public System.Windows.Input.ICommand CanvasConnectModeCommand =>
        _canvasConnectModeCommand ??= NavigatorCommand(
            navigator => navigator.EnterConnectMode());

    public System.Windows.Input.ICommand CanvasConnectToCommand =>
        _canvasConnectToCommand ??= DocumentCommand(
            document => document.OpenCardPicker(CanvasCardPickerPurpose.ConnectTo));

    /// <summary>§F TF-7 (F5/F6): the picker-opening verbs — the
    /// document owns every refusal and the request.</summary>
    public System.Windows.Input.ICommand CanvasPlaceBelowCommand =>
        _canvasPlaceBelowCommand ??= DocumentCommand(
            document => document.OpenCardPicker(CanvasCardPickerPurpose.PlaceBelow));

    public System.Windows.Input.ICommand CanvasPlaceRightOfCommand =>
        _canvasPlaceRightOfCommand ??= DocumentCommand(
            document => document.OpenCardPicker(CanvasCardPickerPurpose.PlaceRightOf));

    public System.Windows.Input.ICommand CanvasPlaceAboveCommand =>
        _canvasPlaceAboveCommand ??= DocumentCommand(
            document => document.OpenCardPicker(CanvasCardPickerPurpose.PlaceAbove));

    public System.Windows.Input.ICommand CanvasPlaceLeftOfCommand =>
        _canvasPlaceLeftOfCommand ??= DocumentCommand(
            document => document.OpenCardPicker(CanvasCardPickerPurpose.PlaceLeftOf));

    public System.Windows.Input.ICommand CanvasAlignWithCommand =>
        _canvasAlignWithCommand ??= DocumentCommand(
            document => document.OpenCardPicker(CanvasCardPickerPurpose.AlignWith));

    private RelayCommand DocumentCommand(
        Action<CanvasDocumentViewModel> run) =>
        new RelayCommand(
            _ => { if (ActiveCanvasDocument is { } document) { run(document); } },
            _ => ActiveCanvasDocument is not null);

    /// <summary>§G2 TG2-0 (G2-2, IG2-34): the owner-carrying shape —
    /// the invoking TAB rides into the operation as TE-1's initiating
    /// surface, captured at execution, never at construction.</summary>
    private RelayCommand DocumentCommand(
        Action<CanvasDocumentViewModel, object?> run) =>
        new RelayCommand(
            _ =>
            {
                if (ActiveCanvasDocument is { } document)
                {
                    run(document, ActiveGroup.ActiveTab);
                }
            },
            _ => ActiveCanvasDocument is not null);

    /// <summary>§F TF-4 (F9/M6): the spatial mode verbs for the
    /// palette and the chord resolver. Gated only on a canvas being
    /// active — the entry preflight and the modes own every other
    /// refusal and speak it.</summary>
    public System.Windows.Input.ICommand CanvasMoveModeCommand =>
        _canvasMoveModeCommand ??= NavigatorCommand(
            navigator => navigator.EnterMoveMode());

    public System.Windows.Input.ICommand CanvasResizeModeCommand =>
        _canvasResizeModeCommand ??= NavigatorCommand(
            navigator => navigator.CommitOrEnterResize());

    public System.Windows.Input.ICommand CanvasResizeDefaultSizeCommand =>
        _canvasResizeDefaultSizeCommand ??= NavigatorCommand(
            navigator => navigator.ResizeDefaultSize());

    public System.Windows.Input.ICommand CanvasResizeFitContentCommand =>
        _canvasResizeFitContentCommand ??= NavigatorCommand(
            navigator => navigator.ResizeFitContent());

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
