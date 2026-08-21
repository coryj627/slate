// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Templates;
using uniffi.slate_uniffi;

namespace SlateWindows;

/// <summary>
/// W5-3 (#743): the create-from-template coordinator — the picker
/// sheet, the prompt/name flow sheet, and the create itself (render +
/// exclusive write + open + caret park). Contracts:
/// docs/plans/30_templates_contracts.md. Everything here is
/// synchronous on the dispatcher (finding 3): the mac async-race
/// machinery — availability generations, selection supersession,
/// deferred cursor landing, destination owner-generation guards — is
/// deleted by construction, not omitted. What survives is the
/// stale-tab caret guard (T8) and vault-transition teardown via
/// workspace disposal (T13).
/// </summary>
internal sealed partial class WorkspaceViewModel
{
    private TemplatePickerViewModel? _templatePickerSheet;
    private TemplateFlowViewModel? _templateFlowSheet;
    private string _templateCreationDestination = string.Empty;
    private System.Windows.Input.ICommand? _newFromTemplateCommand;

    /// <summary>Raised after a template note is written, with its
    /// vault-relative path — the lifecycle layer refreshes the files
    /// sidebar on it (the sidebar's own creates refresh inline; this
    /// one happens outside it).</summary>
    internal event EventHandler<string>? TemplateNoteWritten;

    /// <summary>Window-supplied modal admission (the SearchOpenAdmission
    /// shape): consults <c>ModalSurfaces.DecideTemplateOpen</c> and
    /// performs any dismissal it calls for. Null (headless tests)
    /// admits — the #1118 rule lives in the window layer where the
    /// surface state is.</summary>
    internal Func<bool>? TemplateOpenAdmission { get; set; }

    /// <summary>Window-supplied creation parent (T12): the sidebar's
    /// selected directory, else the selection's parent, else the vault
    /// root. Frozen at picker open. Null (headless tests) means the
    /// vault root.</summary>
    internal Func<string>? TemplateCreationParentProvider { get; set; }

    /// <summary>Window-supplied vault root basename for `{{vault}}`.
    /// Null (headless tests) renders the marker against the empty
    /// string.</summary>
    internal Func<string>? TemplateVaultNameProvider { get; set; }

    /// <summary>Non-null while the template picker sheet is open.</summary>
    public TemplatePickerViewModel? TemplatePickerSheet
    {
        get => _templatePickerSheet;
        private set => SetField(ref _templatePickerSheet, value);
    }

    /// <summary>Non-null while the prompt/name flow sheet is open.</summary>
    public TemplateFlowViewModel? TemplateFlowSheet
    {
        get => _templateFlowSheet;
        private set => SetField(ref _templateFlowSheet, value);
    }

    public System.Windows.Input.ICommand NewFromTemplateCommand =>
        _newFromTemplateCommand ??= new RelayCommand(
            _ => OpenTemplatePicker(), _ => true);

    /// <summary>
    /// Open the picker (T3). Admission first — a refusal has no state
    /// or presentation side effects (mac's rule); past it, the
    /// destination freezes (T12) and the enumeration lands its
    /// terminal state synchronously.
    /// </summary>
    internal void OpenTemplatePicker()
    {
        if (TemplateOpenAdmission?.Invoke() == false)
        {
            return;
        }

        // Re-entry: the chord under an already-open flow is refused by
        // the window admission; headless callers get the same refusal
        // here so the flow can never be restarted out from under its
        // own sheets.
        if (TemplatePickerSheet is not null || TemplateFlowSheet is not null)
        {
            return;
        }

        _templateCreationDestination =
            TemplateCreationParentProvider?.Invoke() ?? string.Empty;
        var picker = new TemplatePickerViewModel(
            EnumerateTemplates,
            TemplateNameRules.DestinationDescription(_templateCreationDestination),
            TemplateActivated,
            CancelTemplateFlow,
            _announce);
        TemplatePickerSheet = picker;
        picker.Load();
    }

    /// <summary>Cancel from any step (T7): sheets close, state
    /// resets, and nothing has been written — the only write in the
    /// flow is the explicit Create.</summary>
    internal void CancelTemplateFlow()
    {
        TemplatePickerSheet = null;
        TemplateFlowSheet = null;
        _templateCreationDestination = string.Empty;
    }

    private IReadOnlyList<TemplateSummary> EnumerateTemplates()
    {
        using var cancel = new CancelToken();
        return _session.ListTemplates(cancel);
    }

    /// <summary>
    /// A picker row activated (T3): read the source, extract prompt
    /// metadata, close the picker, present the flow sheet at the
    /// prompt step — or straight at the name step for a promptless
    /// template (T2's fast path). A read failure here (template
    /// deleted between list and select) cancels the whole flow — mac's
    /// terminal reset, not an error sheet.
    /// </summary>
    private void TemplateActivated(TemplateSummary summary)
    {
        string source;
        try
        {
            source = _session.ReadText(summary.Path);
        }
        catch (VaultException)
        {
            CancelTemplateFlow();
            return;
        }

        TemplateMetadata metadata = SlateUniffiMethods.ExtractTemplateMetadata(source);
        // Flow first, THEN the picker goes away: the window's restore
        // runs when BOTH sheets read null, so this order keeps the
        // picker→flow transition from restoring focus mid-flow.
        TemplateFlowSheet = new TemplateFlowViewModel(
            summary,
            metadata.Prompts,
            TemplateNameRules.DestinationDescription(_templateCreationDestination),
            CreateFromTemplate,
            CancelTemplateFlow);
        TemplatePickerSheet = null;
    }

    /// <summary>
    /// The create (T7): render against the context, write with the
    /// no-clobber primitive, announce, open, park the caret (T8). A
    /// failure re-presents the name step with core's message verbatim
    /// and the user's exact name — the sheet stays up, so Cancel
    /// remains a full T7 cancel.
    /// </summary>
    private void CreateFromTemplate(TemplateFlowViewModel flow)
    {
        string trimmed = flow.NoteName.Trim();
        // The flow validated before requesting; re-check for totality
        // (a future caller must not skip the gate by calling directly).
        if (TemplateNameRules.Validate(trimmed) is { } problem)
        {
            flow.PresentCreateFailure(trimmed, problem);
            return;
        }

        string normalized = TemplateNameRules.NormalizeNoteName(trimmed);
        string path = TemplateNameRules.CreationPath(
            _templateCreationDestination, normalized);
        // `title` is the file stem of the NEW note, never the
        // template's name (§8.2; mac's titleStem).
        string titleStem = System.IO.Path.GetFileNameWithoutExtension(normalized);
        var context = new TemplateContext(
            NowMs: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Title: titleStem,
            VaultName: TemplateVaultNameProvider?.Invoke() ?? string.Empty,
            PromptValues: flow.PromptValues());

        RenderedTemplate rendered;
        try
        {
            rendered = _session.RenderTemplate(flow.Template.Path, context);
            _ = _session.CreateExclusive(path, rendered.Body);
        }
        catch (VaultException exception)
        {
            // Core's message, relayed verbatim — the recorded refusal
            // presentation (T7); inline and focusable, never announced.
            flow.PresentCreateFailure(trimmed, exception.Message);
            return;
        }

        // Announce BEFORE the open (mac's order): the created event is
        // High so it outlives the tab-switch announcement that follows.
        _announce(new A11yEvent.TemplateNoteCreated(
            System.IO.Path.GetFileName(path), flow.Template.Name));
        TemplateNoteWritten?.Invoke(this, path);

        // The open runs while the flow sheet is STILL UP (red team,
        // correctness finding 2): a dirty current tab makes OpenPath
        // show the owner-modal Save/Discard prompt, whose nested pump
        // drains the dispatcher — a restore queued by an earlier sheet
        // close would execute against a disabled window, spending the
        // pre-flow focus token and speaking a spurious pane
        // announcement mid-dialog. Closing the sheet AFTER the open
        // resolves means the restore queues into an enabled window,
        // after any editor focus claim, so the SD-2 stand-down probe
        // evaluates the create's real focus outcome.
        OpenPath(path, WorkspaceOpenTarget.CurrentTab);
        WorkspaceTabViewModel? tab = ActiveGroup.ActiveTab;
        if (tab is { IsMarkdown: true }
            && string.Equals(tab.Path, path, StringComparison.Ordinal))
        {
            // The create-specific landing is EDITOR mode (codex round
            // 3): a CurrentTab open inherits the landed tab's persisted
            // mode, and a Reading-mode landing collapses the editor —
            // the caret would park into a hidden control and T8's
            // focus-follows-content could not be delivered. Normalized
            // BEFORE the reload sweep so ReplaceItem does not
            // reconstruct the reading projection it is about to drop.
            // Same-path PEERS deliberately keep their mode: a reading
            // peer re-projecting the fresh bytes is correct.
            if (tab.IsReadingMode)
            {
                tab.ToggleViewMode();
            }

            // T8's fresh-read rule (red team, correctness finding 1): a
            // same-path tab can land the open WITHOUT a disk read — the
            // same-group arm just activates the existing tab, and the
            // cross-group arm mirrors the peer's buffer over the fresh
            // read — so a parked tab of a previously deleted file (a
            // persistence restore, or the mid-session InvalidatePath
            // sweep) would display its stale buffer over the note just
            // created. EVERY clean same-path tab reloads, not just the
            // landed one (verification round, finding 1): the workspace
            // mirrors same-path documents edit-by-edit with cross-
            // document offsets, so reloading one and leaving a stale
            // peer arms a divergence that the first keystroke turns
            // into an out-of-range replay or the refresh funnel's
            // divergence trap. A DIRTY tab is never reloaded — the
            // user's unsaved buffer outranks the render (mirroring
            // keeps same-path dirtiness in step, so a dirty peer
            // implies a dirty landed tab, which the caret guard below
            // stands down for). The IsMissingFromDisk arm also reloads
            // a byte-IDENTICAL ghost (verification finding 3): its
            // buffer needs no change, but its missing-status and saved
            // content hash are stale bookkeeping that would raise a
            // spurious WriteConflict on the first save.
            foreach (WorkspaceTabViewModel samePath in Groups
                .SelectMany(group => group.Tabs)
                .Where(candidate => candidate.IsMarkdown
                    && string.Equals(candidate.Path, path, StringComparison.Ordinal)
                    && !candidate.IsDirty
                    && (!string.Equals(
                            candidate.Text, rendered.Body, StringComparison.Ordinal)
                        || candidate.IsMissingFromDisk)))
            {
                samePath.ReplaceItem(samePath.Item);
            }

            // The Search_OpenRequested posture: a dirty or externally
            // stale buffer no longer matches the bytes the offset was
            // rendered from, so the caret stays put.
            if (!tab.IsDirty && !tab.IsExternallyStale)
            {
                tab.EditorInteractions?.RequestCaret(
                    TemplateCursor.CaretIndex(rendered.Body, rendered.CursorByteOffset));
            }
        }

        // else: the dirty-navigation prompt refused the open (TD-3) —
        // the note exists and was announced; no caret is parked and no
        // deferred landing is retained (the S9 posture). The sheet
        // still closes, and the queued restore returns focus to the
        // pre-flow element.
        TemplateFlowSheet = null;
        _templateCreationDestination = string.Empty;
    }
}
