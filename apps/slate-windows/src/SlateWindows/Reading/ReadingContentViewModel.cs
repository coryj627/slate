// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.IO;
using System.Windows.Documents;
using System.Windows.Threading;
using uniffi.slate_uniffi;

namespace SlateWindows.Reading;

/// <summary>
/// The reading view's projection pipeline — `w3_inline_runs_spec.md`
/// §10.1 as shipped code.
///
/// One parse per refresh, never per render: the FFI pair
/// (<c>ReadingBlocksSource</c> + <c>ReadingInlineSegmentsSource</c>) and
/// the citation/record fetches run off the dispatcher on
/// <see cref="Task.Run(Action)"/>; only the WPF tree construction
/// happens on the dispatcher. The result is memoized on
/// <b>(text, citations, records)</b> — §10.1 names keying on text alone
/// as the mac-learned defect (`testParseCacheInvalidatesWhenRecordsChange`):
/// it freezes a note's link styling at whatever the first render saw.
///
/// Publication is gated on the standard tuple — disposed, refresh
/// generation, tab path (ordinal), editor revision, and the session's
/// interaction generation — the same shape
/// <see cref="EditorInteractionCoordinator"/> uses, so a stale
/// background result can never publish over a newer state.
/// </summary>
internal sealed class ReadingContentViewModel : BindableBase, IDisposable
{
    private const int MaximumBackgroundRefreshAttempts = 3;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan EditDebounce = TimeSpan.FromMilliseconds(300);

    /// <summary>
    /// §W3-1 item 9 (spike-measured): build cost is linear at ~0.2 ms
    /// per block, so the ceiling render is kept stall-free by CHUNKED
    /// construction — one dispatcher pass per chunk at Background
    /// priority — never by a faster inner loop.
    /// </summary>
    internal const int BuildChunkBlocks = 250;

    /// <summary>
    /// The documented ceiling (≈0.5 MB / 2–3k blocks without
    /// virtualization). Mac logs its perf note at the same 2,000
    /// (`perfNoteBlockThreshold`); Windows additionally enters the
    /// deliberate degraded mode the spike obligated: the first 2,000
    /// blocks render, a terminal notice says so, and the announcement
    /// narrates it.
    /// </summary>
    internal const int MaximumRenderedBlocks = 2_000;

    private readonly VaultSession _session;
    private readonly WorkspaceTabViewModel _tab;
    private readonly Action<A11yEvent> _announce;
    private readonly bool _synchronousForTests;
    private readonly Dispatcher? _dispatcher;

    private bool _disposed;
    private int _generation;

    /// <summary>The generation whose refresh is still live (its
    /// publish or terminal-failure state can still land). -1 when the
    /// current generation has no refresh — the state a surface detach
    /// leaves behind, which a rebind must repair by refreshing even
    /// before the first publication.</summary>
    private int _liveRefreshGeneration = -1;
    private FlowDocument? _document;
    private bool _isLoading;
    private DispatcherTimer? _editDebounce;
    private ICSharpCode.AvalonEdit.Document.TextDocument? _observedDocument;

    /// <summary>Last published memo key; see <see cref="MemoKey"/>.</summary>
    private MemoKey? _memo;

    /// <summary>
    /// True only when <see cref="Document"/> references a projection
    /// that was FULLY built and delivered (single-shot, or a stream
    /// that completed into a subscriber). False for torsos: canceled
    /// streams, streams nobody heard, detached surfaces. The rebind
    /// skip and the terminal-failure preserve decision both consult
    /// it — Document being non-null proves nothing on its own, since
    /// the surface merge drains published documents.
    /// </summary>
    private bool _projectionComplete;

    /// <summary>
    /// Set while a rebind-triggered re-projection is in flight: the
    /// surface that just bound cannot trust what it shows, so a
    /// terminal failure during recovery must publish the visible
    /// notice instead of "preserving" content that may not exist.
    /// </summary>
    private bool _rebindRecovery;

    /// <summary>The records the published document was built against —
    /// activation must match against exactly these (§10.1 coherence:
    /// styling and activation share one snapshot).</summary>
    private OutgoingLink[] _publishedRecords = Array.Empty<OutgoingLink>();

    /// <summary>The task records fetched with the published snapshot —
    /// checkbox activation matches against exactly these (same §10.1
    /// coherence rule as <see cref="_publishedRecords"/>).</summary>
    private TaskItem[] _publishedTasks = Array.Empty<TaskItem>();

    private ReadingActivation? _activation;

    /// <summary>Vault-relative paths the PUBLISHED embed cards were
    /// resolved from (root + nested targets) — the reverse-dependency
    /// filter for target-note saves (W3-5 round 1: a target saved
    /// after publication scheduled nothing, leaving cards stale
    /// indefinitely).</summary>
    private readonly HashSet<string> _publishedEmbedDependencies =
        new(StringComparer.Ordinal);

    /// <summary>True when any published card is unresolved or
    /// degraded — a CREATED/renamed file might resolve it, so those
    /// events refresh even off the dependency set.</summary>
    private bool _publishedHasUnresolvedEmbeds;

    private DispatcherTimer? _dependencyDebounce;

    public ReadingContentViewModel(
        VaultSession session,
        WorkspaceTabViewModel tab,
        Action<A11yEvent> announce,
        bool synchronousForTests = false)
    {
        _session = session;
        _tab = tab;
        _announce = announce;
        _synchronousForTests = synchronousForTests;
        _dispatcher = synchronousForTests ? null : Dispatcher.CurrentDispatcher;
    }

    public FlowDocument? Document
    {
        get => _document;
        private set => SetField(ref _document, value);
    }

    /// <summary>
    /// Raised once per streamed chunk after the first publish: the
    /// surface moves the fragment's blocks into its persistent document
    /// and re-collects landmarks. Fragments are dispatcher-built, so
    /// the handler runs on the same thread that owns the document.
    /// </summary>
    public event Action<FlowDocument>? BlocksAppended;


    public bool IsLoading
    {
        get => _isLoading;
        private set => SetField(ref _isLoading, value);
    }

    public Action<A11yEvent> Announce => _announce;

    /// <summary>
    /// Re-project for a (re)binding surface. The surface's merge
    /// CONSUMES the published document's blocks, and one surface
    /// instance is shared across a group's tabs — so a model that
    /// rebinds after another reading tab was displayed holds an
    /// emptied projection nothing can re-apply (measured as a blank
    /// surface on switching back between two reading-mode tabs).
    /// Dropping the memo forces the next publish to rebuild; streamed
    /// chunks that fired before any surface was attached are recovered
    /// the same way. No-op before the first publish — the in-flight
    /// refresh will reach the new binding through PropertyChanged.
    /// </summary>
    public void EnsureProjected()
    {
        if (_disposed)
        {
            return;
        }
        // Rebinding also resumes buffer observation a tab deactivation
        // paused (Deactivate detaches; the surface's bind is the
        // "shown again" signal).
        AttachObserver();
        if (_document is null)
        {
            // Nothing published yet. If a refresh is still live its
            // publication reaches this binding through PropertyChanged
            // — but a detach-canceled first refresh left NOTHING
            // pending, and returning here stranded the surface on its
            // loading placeholder forever (A→B→A before A published).
            if (_liveRefreshGeneration != _generation)
            {
                Refresh();
            }
            return;
        }
        _memo = null;
        _rebindRecovery = true;
        Refresh();
    }

    /// <summary>True when the published document is a complete,
    /// delivered projection — the only state a rebinding surface may
    /// display without re-projecting.</summary>
    internal bool ProjectionComplete => _projectionComplete;

    /// <summary>
    /// Preference-change invalidation (W3-2 round 1): drop the memo so
    /// the NEXT projection re-renders with the new prefs, and refresh
    /// NOW only when a surface is actually attached (the
    /// BlocksAppended subscription is the attachment signal). Unlike
    /// <see cref="EnsureProjected"/> this attaches no observers and
    /// sets no rebind-recovery state — hidden models must not start
    /// discarded background builds, and a later terminal failure must
    /// not replace valid displayed content with the failure notice.
    /// </summary>
    public void InvalidateForPrefsChange()
    {
        if (_disposed)
        {
            return;
        }
        _memo = null;
        if (BlocksAppended is not null)
        {
            Refresh();
            return;
        }
        // Unbound, but a refresh may be IN FLIGHT (round 2: a restored
        // tab's constructor projection races the prefs change, and
        // set_math_prefs does not advance the session generation, so a
        // stale-prefs publish would pass every gate and memoize).
        // Poison the generation so nothing rendered with the old prefs
        // can land; retiring the live marker makes the next bind's
        // EnsureProjected restart — the pre-publication rebind
        // machinery already handles exactly this shape.
        _generation++;
        _liveRefreshGeneration = -1;
    }

    /// <summary>
    /// The surface unbound this model (tab switch, template rebind).
    /// The stream MUST die here: chunk continuations append into list
    /// objects that now belong to whatever the surface shows next —
    /// without this, note A's stream grows a list mounted under note
    /// B. The generation bump aborts every pending continuation and
    /// in-flight publish at its gate (dispatcher-serial, so no chunk
    /// can slip between detach and the check); the memo drops so the
    /// next binding re-projects instead of trusting a torso.
    /// </summary>
    internal void OnSurfaceDetached()
    {
        if (_disposed)
        {
            return;
        }
        // This is the true "no longer visible" signal — the shared
        // surface just rebound away — so buffer observation pauses
        // HERE, not on tab Deactivate (which also fires on split-pane
        // focus moves while the tab stays visible). EnsureProjected
        // re-attaches on the next bind.
        Deactivate();
        _generation++;
        _liveRefreshGeneration = -1;
        _memo = null;
        // _projectionComplete is deliberately untouched: it is already
        // false for any in-flight stream (the case detach must poison),
        // and a COMPLETED projection stays valid — its blocks remain
        // mounted untouched across a reading→editor→back switch, which
        // is exactly the rebind the completeness-gated skip keeps free.
        IsLoading = false;
    }

    /// <summary>Activate one run kind (surface click/Enter path).</summary>
    public void Activate(ReadingInlineRunKind kind)
    {
        _activation ??= new ReadingActivation(
            _tab, _announce, () => _publishedRecords, _openExternalForTests);
        _activation.Activate(kind);
    }

    /// <summary>Open an embed card's ALREADY-RESOLVED source (W3-5) —
    /// see <see cref="ReadingActivation.ActivateResolvedEmbedSource"/>.</summary>
    public void ActivateResolvedEmbedSource(string path, LinkAnchor? anchor)
    {
        _activation ??= new ReadingActivation(
            _tab, _announce, () => _publishedRecords, _openExternalForTests);
        _activation.ActivateResolvedEmbedSource(path, anchor);
    }

    /// <summary>
    /// Copy a code block's source - the Copy button route (W3-4). The
    /// source travels on the button itself (a plain string Tag); the
    /// announcement is core's canonical "Code copied.".
    /// </summary>
    public void CopyCode(string source)
    {
        try
        {
            (ClipboardForTests ?? System.Windows.Clipboard.SetText)(source);
        }
        catch (System.Runtime.InteropServices.ExternalException exception)
        {
            // WPF surfaces clipboard contention as ExternalException
            // (COMException is only its subtype) — the MainWindow
            // CopyText precedent. Log + say so instead of letting a
            // busy clipboard crash the UI thread out of a click.
            HostLog.Write(HostDiagnosticEvent.ClipboardCopyFailed, exception);
            _announce(new A11yEvent.HostComposed(
                "Could not copy code. Try again.",
                A11yPriority.High));
            return;
        }
        _announce(new A11yEvent.CodeCopied());
    }

    /// <summary>Clipboard injection seam for tests.</summary>
    internal Action<string>? ClipboardForTests { get; set; }

    /// <summary>
    /// Toggle the task whose checkbox lives inside the given source
    /// block range (the range the builder stamped on the checkbox) —
    /// the byte-containment analog of mac's line-matched `taskRow`.
    /// Routes through the tab's core task command, so in-flight
    /// gating, write-conflict detection, and the canonical
    /// announcements are all the Tasks panel's.
    /// </summary>
    public void ToggleTaskAt(ulong blockByteStart, ulong blockByteEnd)
    {
        TaskItem? task = _publishedTasks.FirstOrDefault(t =>
            t.CheckboxStartByte >= blockByteStart && t.CheckboxStartByte < blockByteEnd);
        if (task is null)
        {
            // The snapshot predates the checkbox the user clicked —
            // mid-transition, same interim wording as the citation
            // cache's not-ready path.
            _announce(new A11yEvent.HostComposed(
                "Tasks are still loading; try again.",
                A11yPriority.Medium));
            return;
        }
        if (_tab.IsDirty)
        {
            // The editor's exact refusal (#158): the toggle's
            // post-save reload would overwrite unsaved edits.
            _announce(new A11yEvent.TaskToggleUnsaved(
                Path.GetFileName(_tab.Path)));
            return;
        }
        _ = _tab.ToggleTask(task, _announce);
    }

    private Func<string, bool>? _openExternalForTests;

    internal void SetExternalOpenerForTests(Func<string, bool> opener)
    {
        _openExternalForTests = opener;
        _activation = null;
    }

    /// <summary>
    /// Begin observing the live buffer while reading mode is active:
    /// undo, external sync, and any other text mutation re-projects
    /// after a short debounce. Idempotent; detached on
    /// <see cref="Deactivate"/> so a hidden reading view costs nothing.
    /// </summary>
    public void Activate()
    {
        if (_disposed || _synchronousForTests)
        {
            return;
        }
        AttachObserver();
        Refresh();
    }

    private void AttachObserver()
    {
        if (_synchronousForTests)
        {
            return;
        }
        if (_tab.EditorDocument is { } document
            && !ReferenceEquals(_observedDocument, document))
        {
            Detach();
            _observedDocument = document;
            document.TextChanged += Document_TextChanged;
        }
    }

    public void Deactivate()
    {
        Detach();
        _editDebounce?.Stop();
    }

    private void Detach()
    {
        if (_observedDocument is { } observed)
        {
            observed.TextChanged -= Document_TextChanged;
            _observedDocument = null;
        }
    }

    private void Document_TextChanged(object? sender, EventArgs e)
    {
        if (_disposed)
        {
            return;
        }
        _editDebounce ??= new DispatcherTimer { Interval = EditDebounce };
        _editDebounce.Stop();
        _editDebounce.Tick -= EditDebounce_Tick;
        _editDebounce.Tick += EditDebounce_Tick;
        _editDebounce.Start();
    }

    private void EditDebounce_Tick(object? sender, EventArgs e)
    {
        _editDebounce?.Stop();
        Refresh();
    }

    /// <summary>Project the live buffer. Dispatcher thread only.</summary>
    public void Refresh()
    {
        if (_disposed || !_tab.IsMarkdown)
        {
            return;
        }

        int generation = ++_generation;
        _liveRefreshGeneration = generation;
        string text = _tab.Text;
        string path = _tab.Path;
        long revision = _tab.EditorSession?.Revision ?? -1;
        ulong sessionGeneration = _session.InteractionGeneration();

        if (_synchronousForTests)
        {
            try
            {
                FetchResult fetched = FetchGuarded(_session, path, text);
                Publish(generation, path, revision, sessionGeneration, fetched);
            }
            catch (Exception exception)
            {
                RecordTerminalFailure(exception);
                PublishTerminalFailure(generation);
            }
            return;
        }

        IsLoading = true;
        _ = Task.Run(async () =>
        {
            // The outer boundary exists because this task is
            // fire-and-forget: any exception the retry policy does not
            // recognize would otherwise fault a discarded task —
            // no diagnostic, no announcement, and (combined with a
            // pending model switch) the PREVIOUS note left on screen.
            try
            {
                FetchResult? fetched = null;
                for (int attempt = 1; attempt <= MaximumBackgroundRefreshAttempts; attempt++)
                {
                    try
                    {
                        fetched = FetchGuarded(_session, path, text);
                        break;
                    }
                    catch (Exception exception) when (
                        exception is VaultException or IOException
                        && attempt < MaximumBackgroundRefreshAttempts)
                    {
                        // Known-transient only; the last attempt and
                        // every other exception fall through to the
                        // terminal boundary.
                        await Task.Delay(RetryDelay).ConfigureAwait(false);
                    }
                }
                if (fetched is { } result)
                {
                    _ = _dispatcher!.InvokeAsync(() => RunPublishStep(
                        generation,
                        () => Publish(generation, path, revision, sessionGeneration, result)));
                }
            }
            catch (Exception exception)
            {
                // Terminal: an unconditional host diagnostic (event +
                // exception TYPE only, never payload text — W1-RT-01),
                // then a generation-gated user-visible failure state.
                RecordTerminalFailure(exception);
                _ = _dispatcher!.InvokeAsync(
                    () => PublishTerminalFailure(generation));
            }
        });
    }

    /// <summary>Terminal-failure injection seam for tests.</summary>
    internal Func<Exception?>? FetchFaultForTests { get; set; }

    /// <summary>Dispatcher-side (publish/chunk-build) fault injection
    /// seam for tests.</summary>
    internal Func<Exception?>? PublishFaultForTests { get; set; }

    /// <summary>The exception behind the most recent terminal failure —
    /// the log stores only the type (W1-RT-01), tests need the stack.</summary>
    internal Exception? LastTerminalFailureForTests { get; private set; }

    internal bool IsDisposedForTests => _disposed;

    internal bool ObservesEditorForTests => _observedDocument is not null;

    private FetchResult FetchGuarded(VaultSession session, string path, string text)
    {
        if (FetchFaultForTests?.Invoke() is { } fault)
        {
            throw fault;
        }
        return Fetch(session, path, text);
    }

    /// <summary>
    /// The user-visible half of a terminal refresh failure: loading
    /// state clears, the failure is announced with a recovery route,
    /// and — only when there is nothing already on screen — a notice
    /// document is published so the surface is never silently blank.
    /// Existing content is preserved (stale beats empty); the memo is
    /// untouched, so the next refresh retries the projection in full.
    /// </summary>
    private void PublishTerminalFailure(int generation)
    {
        if (_disposed || generation != _generation)
        {
            return;
        }
        _liveRefreshGeneration = -1;
        IsLoading = false;
        _announce(new A11yEvent.HostComposed(
            "Reading view could not load this note. Switch to the editor to "
            + "keep working, then toggle reading mode to retry.",
            A11yPriority.High));
        // Preserve only what is PROVABLY on screen: a complete,
        // delivered projection outside a rebind. A non-null Document
        // proves nothing by itself — the surface merge drains it, a
        // heard-by-nobody stream leaves a torso, and a rebinding
        // surface may have had this model's blocks cleared by another
        // tab entirely.
        bool preserve = Document is not null && _projectionComplete && !_rebindRecovery;
        _rebindRecovery = false;
        if (preserve)
        {
            return;
        }
        var document = new FlowDocument();
        var paragraph = new Paragraph(new Run(
            "Reading view could not load this note. Switch to the editor to "
            + "keep working, then toggle reading mode to retry."))
        {
            FontStyle = System.Windows.FontStyles.Italic,
        };
        System.Windows.Automation.AutomationProperties.SetAutomationId(
            paragraph, "ReadingRefreshFailedNotice");
        document.Blocks.Add(paragraph);
        Document = document;
    }

    /// <summary>Tokens-only fault seam: exercises the degraded code
    /// fetch (empty artifact, digest "degraded") without failing the
    /// projection itself.</summary>
    internal Func<Exception?>? CodeTokenFaultForTests { get; set; }

    /// <summary>Math-only fault seam (W3-2), same contract.</summary>
    internal Func<Exception?>? MathFaultForTests { get; set; }

    /// <summary>Diagram-fetch fault seam (W3-3), mirroring
    /// <see cref="MathFaultForTests"/>.</summary>
    internal Func<Exception?>? DiagramFaultForTests { get; set; }

    /// <summary>Embed-resolution fault seam (W3-5), mirroring the
    /// other artifact seams. Fired once per resolved key.</summary>
    internal Func<Exception?>? EmbedFaultForTests { get; set; }

    /// <summary>Per-note embed-resolution budgets (W3-5): each
    /// preview call is already core-bounded (64 KiB text / 128 nodes
    /// / 8 MiB image, cumulative per key), but a note can hold
    /// thousands of embed paragraphs — the count cap bounds FFI round
    /// trips and the image pool bounds bytes held by the fetch
    /// result. Over-budget keys degrade to header-only cards that
    /// still activate; nothing is silently absent.</summary>
    internal const int MaximumResolvedEmbedsPerNote = 128;

    /// <summary>See <see cref="MaximumResolvedEmbedsPerNote"/> —
    /// cumulative image-byte pool across the note's resolved
    /// embeds.</summary>
    internal const int FetchedEmbedImageByteBudget = 16 * 1024 * 1024;

    /// <summary>Background-safe: FFI only, no WPF objects.</summary>
    private FetchResult Fetch(VaultSession session, string path, string text)
    {
        // Records ownership is inherent here — they are fetched for the
        // captured path inside the gated refresh, and publication
        // re-verifies the tab still shows that exact path (ordinal).
        OutgoingLink[] records = session.OutgoingLinks(path);
        RenderedCitation[] citations = RenderCitations(session, path);
        TaskItem[] tasks = session.TasksForFile(path).ToArray();
        // Code blocks degrade per-fetch, mac-style: a failure here
        // renders plain un-highlighted fences with correct preambles
        // rather than failing the whole projection.
        CodeBlock[] codeBlocks;
        bool codeFetchDegraded = false;
        try
        {
            if (CodeTokenFaultForTests?.Invoke() is { } fault)
            {
                throw fault;
            }
            codeBlocks = session.GetSyntaxTokens(path);
        }
        catch (VaultException)
        {
            codeBlocks = Array.Empty<CodeBlock>();
            codeFetchDegraded = true;
        }
        // Math degrades identically (W3-2): a MathCAT failure renders
        // source-in-range fallbacks with nav still working, never a
        // failed projection.
        MathBlock[] mathBlocks;
        bool mathFetchDegraded = false;
        try
        {
            if (MathFaultForTests?.Invoke() is { } mathFault)
            {
                throw mathFault;
            }
            mathBlocks = session.GetMathBlocks(path);
        }
        catch (VaultException)
        {
            mathBlocks = Array.Empty<MathBlock>();
            mathFetchDegraded = true;
        }
        // Diagrams degrade identically (W3-3): a renderer failure
        // renders source-in-range fallbacks with nav still working,
        // never a failed projection.
        DiagramBlock[] diagramBlocks;
        bool diagramFetchDegraded = false;
        try
        {
            if (DiagramFaultForTests?.Invoke() is { } diagramFault)
            {
                throw diagramFault;
            }
            diagramBlocks = session.GetDiagramBlocks(path);
        }
        catch (VaultException)
        {
            diagramBlocks = Array.Empty<DiagramBlock>();
            diagramFetchDegraded = true;
        }
        ReadingBlock[] blocks = SlateUniffiMethods.ReadingBlocksSource(text);
        ReadingBlockInlines[] inlines = SlateUniffiMethods.ReadingInlineSegmentsSource(
            text, citations, records);
        ReadingEmbedArtifact[] embeds = FetchEmbedResolutions(
            session, path, records, inlines);
        // The artifact digest hashes the COMPLETE artifact sets, so
        // it belongs here on the fetch task, not on the dispatcher at
        // publication (round 5: a dense note made the memo key itself
        // a large dispatcher allocation).
        string artifactDigest =
            CodeArtifactDigest(codeBlocks, codeFetchDegraded)
            + "|" + MathArtifactDigest(mathBlocks, mathFetchDegraded)
            + "|" + DiagramArtifactDigest(diagramBlocks, diagramFetchDegraded)
            + "|" + EmbedArtifactDigest(embeds);
        return new FetchResult(
            text,
            citations,
            records,
            tasks,
            codeBlocks,
            codeFetchDegraded,
            mathBlocks,
            mathFetchDegraded,
            diagramBlocks,
            diagramFetchDegraded,
            embeds,
            blocks,
            inlines,
            artifactDigest);
    }

    /// <summary>
    /// W3-5: block-embed cards resolve their content HERE, on the
    /// fetch task — the projection pipeline IS the async layer, so
    /// the card never needs mac's loading state machine. Resolution
    /// is BOUNDED (core preview budgets per key + the per-note count
    /// and image pools) and degrades PER KEY: an FFI failure or an
    /// over-budget key yields a header-only card that still
    /// activates, never a failed projection. Alt text threads from
    /// the embed's own link record (the mac batch precedent — its
    /// per-key fallback loses the alt; this path never does), last
    /// same-key record winning as on mac.
    /// </summary>
    private ReadingEmbedArtifact[] FetchEmbedResolutions(
        VaultSession session,
        string path,
        OutgoingLink[] records,
        ReadingBlockInlines[] inlines)
    {
        var keys = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (ReadingBlockInlines blockInlines in inlines)
        {
            if (blockInlines.BlockEmbedKey is { Length: > 0 } key && seen.Add(key))
            {
                keys.Add(key);
            }
        }
        if (keys.Count == 0)
        {
            return Array.Empty<ReadingEmbedArtifact>();
        }
        var altByKey = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (OutgoingLink record in records)
        {
            if (record.IsEmbed)
            {
                altByKey[SlateUniffiMethods.ReadingEmbedKey(
                    record.TargetRaw, record.TargetAnchor)] = record.DisplayText;
            }
        }
        var artifacts = new List<ReadingEmbedArtifact>(keys.Count);
        int attempts = 0;
        long imagePool = FetchedEmbedImageByteBudget;
        foreach (string key in keys)
        {
            altByKey.TryGetValue(key, out string? alt);
            // Attempts are counted BEFORE the FFI call (round 1
            // [high]: counting successes let persistent failures make
            // one call per distinct key, unbounded).
            if (attempts >= MaximumResolvedEmbedsPerNote)
            {
                artifacts.Add(new ReadingEmbedArtifact(key, alt, null));
                continue;
            }
            attempts++;
            EmbedPreviewResolution? resolution;
            try
            {
                if (EmbedFaultForTests?.Invoke() is { } fault)
                {
                    throw fault;
                }
                resolution = session.ResolveEmbedPreview(path, key, alt);
            }
            catch (VaultException)
            {
                resolution = null;
            }
            bool imageRefused = false;
            if (resolution is not null)
            {
                // NESTED image payloads are stripped outright: nested
                // cards render header-only (G25), so their bytes are
                // pure retention — and they bypassed the pool (round 1
                // [high]: 128 trees × the core 8 MiB per-key allowance
                // is ~1 GiB the old accounting never saw).
                EmbedResolution stripped =
                    StripNestedImagePayloads(resolution.Resolution);
                if (stripped is EmbedResolution.Image image
                    && image.Bytes.Length > 0)
                {
                    if (image.Bytes.Length > imagePool)
                    {
                        imageRefused = true;
                        stripped = new EmbedResolution.Image(
                            image.TargetPath,
                            Array.Empty<byte>(),
                            image.Mime,
                            image.Alt);
                    }
                    else
                    {
                        imagePool -= image.Bytes.Length;
                    }
                }
                resolution = new EmbedPreviewResolution(
                    stripped, resolution.Truncated);
            }
            artifacts.Add(new ReadingEmbedArtifact(key, alt, resolution, imageRefused));
        }
        return artifacts.ToArray();
    }

    /// <summary>
    /// W3-5 round 1 [high]: a TARGET-note save after publication must
    /// re-project the cards built from it. The vault event stream
    /// (session write paths; external edits surface at the next scan)
    /// reaches every open reading model through the workspace, and
    /// this filter keeps it cheap: refresh only when the changed path
    /// is one the published cards were resolved from, or when a
    /// create/rename might resolve a card that is currently
    /// unresolved or degraded. Debounced (saves arrive in bursts);
    /// the artifact digest makes a no-op refresh a memo hit. Hidden
    /// models do nothing — a rebind always re-projects (W3-2 rule).
    /// </summary>
    public void NotifyVaultFileChanged(FileChangeKind kind, string path)
    {
        if (_disposed
            || BlocksAppended is null
            || string.Equals(path, _tab.Path, StringComparison.Ordinal))
        {
            return;
        }
        bool relevant = _publishedEmbedDependencies.Contains(path)
            || (_publishedHasUnresolvedEmbeds
                && kind is FileChangeKind.Created or FileChangeKind.Renamed);
        if (!relevant)
        {
            return;
        }
        if (_synchronousForTests)
        {
            Refresh();
            return;
        }
        _dependencyDebounce ??= new DispatcherTimer
        {
            Interval = EditDebounce,
        };
        _dependencyDebounce.Stop();
        _dependencyDebounce.Tick -= DependencyDebounceElapsed;
        _dependencyDebounce.Tick += DependencyDebounceElapsed;
        _dependencyDebounce.Start();
    }

    private void DependencyDebounceElapsed(object? sender, EventArgs e)
    {
        _dependencyDebounce?.Stop();
        if (!_disposed)
        {
            Refresh();
        }
    }

    private void CollectEmbedDependencies(ReadingEmbedArtifact[] embeds)
    {
        _publishedEmbedDependencies.Clear();
        _publishedHasUnresolvedEmbeds = false;
        foreach (ReadingEmbedArtifact embed in embeds)
        {
            if (embed.Resolution is null)
            {
                _publishedHasUnresolvedEmbeds = true;
                continue;
            }
            AddDependencyPaths(embed.Resolution.Resolution);
        }
    }

    private void AddDependencyPaths(EmbedResolution resolution)
    {
        switch (resolution)
        {
            case EmbedResolution.FullNote fullNote:
                _publishedEmbedDependencies.Add(fullNote.TargetPath);
                foreach (NestedEmbed nested in fullNote.Nested)
                {
                    AddDependencyPaths(nested.Resolution);
                }
                break;
            case EmbedResolution.Section section:
                _publishedEmbedDependencies.Add(section.TargetPath);
                foreach (NestedEmbed nested in section.Nested)
                {
                    AddDependencyPaths(nested.Resolution);
                }
                break;
            case EmbedResolution.Block block:
                _publishedEmbedDependencies.Add(block.TargetPath);
                break;
            case EmbedResolution.Image image:
                _publishedEmbedDependencies.Add(image.TargetPath);
                break;
            case EmbedResolution.Unresolved:
                _publishedHasUnresolvedEmbeds = true;
                break;
        }
    }

    /// <summary>Rebuilds a resolution tree with every NESTED image
    /// payload emptied — collapsed child cards never render bytes.
    /// The root image is left intact (its pool charge happens at the
    /// call site).</summary>
    internal static EmbedResolution StripNestedImagePayloads(EmbedResolution resolution)
    {
        switch (resolution)
        {
            case EmbedResolution.FullNote fullNote:
                return new EmbedResolution.FullNote(
                    fullNote.TargetPath,
                    fullNote.Text,
                    StripNestedArray(fullNote.Nested));
            case EmbedResolution.Section section:
                return new EmbedResolution.Section(
                    section.TargetPath,
                    section.Heading,
                    section.Text,
                    StripNestedArray(section.Nested));
            default:
                return resolution;
        }
    }

    private static NestedEmbed[] StripNestedArray(NestedEmbed[] nested)
    {
        if (nested.Length == 0)
        {
            return nested;
        }
        var stripped = new NestedEmbed[nested.Length];
        for (int i = 0; i < nested.Length; i++)
        {
            EmbedResolution child = nested[i].Resolution switch
            {
                EmbedResolution.Image image when image.Bytes.Length > 0 =>
                    new EmbedResolution.Image(
                        image.TargetPath, Array.Empty<byte>(), image.Mime, image.Alt),
                var other => StripNestedImagePayloads(other),
            };
            stripped[i] = new NestedEmbed(
                nested[i].RawTarget,
                nested[i].ByteOffsetInParent,
                nested[i].ByteEndInParent,
                child);
        }
        return stripped;
    }

    /// <summary>
    /// The active-style render, following the editor popover's
    /// precedent: no configured style, or a per-reference render
    /// failure, degrades that citation to its authored raw text — which
    /// core renders verbatim for an unmatched raw (the documented
    /// mid-transition behavior), so degradation is per-citation, never
    /// note-wide.
    /// </summary>
    private static RenderedCitation[] RenderCitations(VaultSession session, string path)
    {
        string? styleId = null;
        try
        {
            string? defaultStyle = session.CitationsPrefs().DefaultStyle;
            if (!string.IsNullOrWhiteSpace(defaultStyle))
            {
                styleId = Path.GetFileNameWithoutExtension(defaultStyle);
            }
        }
        catch (VaultException)
        {
        }
        if (styleId is null)
        {
            return Array.Empty<RenderedCitation>();
        }

        IReadOnlyList<CitationReference> references;
        try
        {
            references = session.ListCitationsInFile(path);
        }
        catch (VaultException)
        {
            return Array.Empty<RenderedCitation>();
        }

        var rendered = new List<RenderedCitation>(references.Count);
        foreach (CitationReference reference in references)
        {
            try
            {
                rendered.Add(session.RenderCitation(reference, styleId));
            }
            catch (VaultException)
            {
                // Per-citation degradation to authored raw.
            }
        }
        return rendered.ToArray();
    }

    private void Publish(
        int generation,
        string path,
        long revision,
        ulong sessionGeneration,
        FetchResult fetched)
    {
        if (PublishFaultForTests?.Invoke() is { } fault)
        {
            throw fault;
        }
        IsLoading = false;
        if (_disposed || generation != _generation)
        {
            // Superseded or dead: a newer refresh owns the pipeline
            // (and re-marked itself live); nothing to repair here.
            return;
        }
        // Whatever happens below, THIS generation's refresh has landed.
        _liveRefreshGeneration = -1;
        if (!string.Equals(_tab.Path, path, StringComparison.Ordinal)
            || (_tab.EditorSession?.Revision ?? -1) != revision
            || _session.InteractionGeneration() != sessionGeneration)
        {
            // The captured tuple drifted while the fetch ran — and the
            // session generation is VAULT-WIDE, so an unrelated save
            // lands here with nothing else scheduled to re-project
            // this tab. Silently returning stranded the surface on
            // its placeholder (or on stale content) until a mode
            // cycle; retry immediately with the latest tuple instead.
            // Converges when vault activity settles; a same-text
            // retry is one parse ending in a memo hit.
            Refresh();
            return;
        }

        var key = new MemoKey(
            fetched.Text,
            fetched.Citations,
            fetched.Records,
            fetched.ArtifactDigest);
        if (Document is not null && _memo is { } memo && memo.Matches(key))
        {
            return;
        }

        var model = new List<(ReadingBlock, ReadingBlockInlines)>(fetched.Blocks.Length);
        for (int i = 0; i < fetched.Blocks.Length && i < fetched.Inlines.Length; i++)
        {
            model.Add((fetched.Blocks[i], fetched.Inlines[i]));
        }

        // §W3-1 item 9: the ceiling is ABSOLUTE — an all-list note must
        // not bypass it (adversarial input would otherwise render in
        // one synchronous pass). Chunk boundaries may fall anywhere:
        // the shared build context carries open-list state across
        // chunks, so a list is never split by where a cut landed.
        int renderLimit = Math.Min(model.Count, MaximumRenderedBlocks);
        bool degraded = model.Count > renderLimit;

        var context = new ReadingListBuildContext();
        int firstEnd = Math.Min(BuildChunkBlocks, renderLimit);
        // Excluded from the memo on purpose: CodeBlock carries arrays
        // (reference equality would defeat every memo hit), and its
        // content derives from the SAVED file - saved-state changes
        // arrive through the session-generation drift retry.
        CodeBlock[] codeBlocks = fetched.CodeBlocks;
        MathBlock[] mathBlocks = fetched.MathBlocks;
        DiagramBlock[] diagramBlocks = fetched.DiagramBlocks;
        ReadingEmbedArtifact[] embeds = fetched.Embeds;
        ReadingDocumentModel built = ReadingDocumentBuilder.Build(
            model.GetRange(0, firstEnd), context, codeBlocks, mathBlocks, diagramBlocks,
            embeds);

        _publishedRecords = fetched.Records;
        _publishedTasks = fetched.Tasks;
        CollectEmbedDependencies(fetched.Embeds);
        _projectionComplete = false;
        // Only the DOCUMENT is published. The built model's landmarks
        // point into a container the surface's merge empties — the
        // surface re-collects over the live container, and a second
        // landmark source here would be a dangling-pointer trap.
        Document = built.Document;

        if (firstEnd >= renderLimit)
        {
            FinishPublish(key, degraded, renderLimit, streamed: false);
            return;
        }

        // The memo is set only when the stream COMPLETES — a memo hit
        // on a half-built document would freeze the truncation forever.
        _memo = null;
        if (_synchronousForTests)
        {
            int index = firstEnd;
            while (index < renderLimit)
            {
                int end = Math.Min(index + BuildChunkBlocks, renderLimit);
                AppendFragment(
                    model, index, end, context, codeBlocks, mathBlocks, diagramBlocks,
                    embeds);
                index = end;
            }
            FinishPublish(key, degraded, renderLimit, streamed: true);
            return;
        }
        _ = _dispatcher!.InvokeAsync(
            () => RunPublishStep(
                generation,
                () => ContinueBuild(
                    generation, model, firstEnd, renderLimit, key, degraded, context,
                    codeBlocks, mathBlocks, diagramBlocks, embeds)),
            DispatcherPriority.Background);
    }

    /// <summary>
    /// Terminal boundary for DISPATCHER-side projection work. The
    /// fetch task's boundary cannot see these: InvokeAsync delegates
    /// fault their discarded DispatcherOperation, not the task that
    /// queued them — a WPF build/merge exception would strand the
    /// surface on the loading placeholder with no notice, no
    /// announcement, and no diagnostic.
    /// </summary>
    private void RunPublishStep(int generation, Action step)
    {
        try
        {
            step();
        }
        catch (Exception exception)
        {
            RecordTerminalFailure(exception);
            PublishTerminalFailure(generation);
        }
    }

    /// <summary>
    /// One recorder for every terminal boundary: the unconditional log
    /// carries the exception TYPE only (W1-RT-01); under
    /// SLATE_UIA_DIAGNOSTICS the STACK (never the message — messages
    /// can carry payload text, frames cannot) is written too, so a
    /// field recurrence pinpoints its origin.
    /// </summary>
    private void RecordTerminalFailure(Exception exception)
    {
        LastTerminalFailureForTests = exception;
        HostLog.Write(
            HostDiagnosticEvent.ReadingRefreshTerminalFailure, exception);
        HostLog.WriteUiAutomationDiagnostic(
            HostDiagnosticEvent.ReadingRefreshTerminalFailure,
            $"{exception.GetType().Name}: {exception.StackTrace}");
    }

    /// <summary>
    /// One streamed chunk per dispatcher pass; a newer refresh bumps
    /// the generation and the orphaned stream stops silently.
    /// </summary>
    private void ContinueBuild(
        int generation,
        List<(ReadingBlock, ReadingBlockInlines)> model,
        int index,
        int renderLimit,
        MemoKey key,
        bool degraded,
        ReadingListBuildContext context,
        CodeBlock[] codeBlocks,
        MathBlock[] mathBlocks,
        DiagramBlock[] diagramBlocks,
        ReadingEmbedArtifact[] embeds)
    {
        if (_disposed || generation != _generation)
        {
            return;
        }
        if (PublishFaultForTests?.Invoke() is { } fault)
        {
            throw fault;
        }
        int end = Math.Min(index + BuildChunkBlocks, renderLimit);
        AppendFragment(
            model, index, end, context, codeBlocks, mathBlocks, diagramBlocks, embeds);
        if (end < renderLimit)
        {
            _ = _dispatcher!.InvokeAsync(
                () => RunPublishStep(
                    generation,
                    () => ContinueBuild(
                        generation, model, end, renderLimit, key, degraded, context,
                        codeBlocks, mathBlocks, diagramBlocks, embeds)),
                DispatcherPriority.Background);
            return;
        }
        FinishPublish(key, degraded, renderLimit, streamed: true);
    }

    private void AppendFragment(
        List<(ReadingBlock, ReadingBlockInlines)> model,
        int index,
        int end,
        ReadingListBuildContext context,
        CodeBlock[] codeBlocks,
        MathBlock[] mathBlocks,
        DiagramBlock[] diagramBlocks,
        ReadingEmbedArtifact[] embeds) =>
        BlocksAppended?.Invoke(
            ReadingDocumentBuilder.Build(
                model.GetRange(index, end - index), context, codeBlocks, mathBlocks,
                diagramBlocks, embeds)
                .Document);

    private void FinishPublish(MemoKey key, bool degraded, int renderedBlocks, bool streamed)
    {
        // A stream nobody heard delivered nothing past chunk 1: leave
        // the memo empty so the next binding's EnsureProjected (or any
        // refresh) rebuilds instead of memo-matching a torso, and say
        // nothing — the announcement narrates a surface that isn't
        // showing this projection.
        if (streamed && BlocksAppended is null)
        {
            _memo = null;
            _projectionComplete = false;
            return;
        }
        _memo = key;
        _projectionComplete = true;
        _rebindRecovery = false;
        if (!degraded)
        {
            return;
        }
        string rendered = renderedBlocks.ToString(
            "N0", System.Globalization.CultureInfo.InvariantCulture);
        string notice =
            $"Reading view shows the first {rendered} blocks of this note. "
            + "Switch to the editor for the full text.";
        var document = new FlowDocument();
        var paragraph = new Paragraph(new Run(notice))
        {
            FontStyle = System.Windows.FontStyles.Italic,
        };
        System.Windows.Automation.AutomationProperties.SetAutomationId(
            paragraph, "ReadingDegradedNotice");
        document.Blocks.Add(paragraph);
        BlocksAppended?.Invoke(document);
        _announce(new A11yEvent.HostComposed(notice, A11yPriority.High));
    }


    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        Deactivate();
        _editDebounce = null;
        _dependencyDebounce?.Stop();
        _dependencyDebounce = null;
    }

    private sealed record FetchResult(
        string Text,
        RenderedCitation[] Citations,
        OutgoingLink[] Records,
        TaskItem[] Tasks,
        CodeBlock[] CodeBlocks,
        bool CodeFetchDegraded,
        MathBlock[] MathBlocks,
        bool MathFetchDegraded,
        DiagramBlock[] DiagramBlocks,
        bool DiagramFetchDegraded,
        ReadingEmbedArtifact[] Embeds,
        ReadingBlock[] Blocks,
        ReadingBlockInlines[] Inlines,
        string ArtifactDigest);

    /// <summary>
    /// The §10.1 memo triple. Citations and records are uniffi records
    /// with value equality, so sequence comparison is exact — not a
    /// fingerprint that could collide.
    /// </summary>
    private sealed class MemoKey
    {
        private readonly string _text;
        private readonly RenderedCitation[] _citations;
        private readonly OutgoingLink[] _records;
        private readonly string _codeDigest;

        public MemoKey(
            string text,
            RenderedCitation[] citations,
            OutgoingLink[] records,
            string codeDigest)
        {
            _text = text;
            _citations = citations;
            _records = records;
            _codeDigest = codeDigest;
        }

        public bool Matches(MemoKey other) =>
            string.Equals(_text, other._text, StringComparison.Ordinal)
            && _citations.SequenceEqual(other._citations)
            && _records.SequenceEqual(other._records)
            && string.Equals(_codeDigest, other._codeDigest, StringComparison.Ordinal);
    }

    /// <summary>
    /// A CONTENT-COMPLETE identity for the saved code artifact, folded
    /// into the memo: CodeBlock carries arrays (reference equality
    /// would defeat every hit), but the memo must see a save that
    /// changed source or token CONTENT with the live text unchanged —
    /// offsets/lengths/counts alone collide on same-length edits
    /// (41 -> 42 froze un-highlighted rendering across its save). A
    /// degraded fetch is its own identity so recovery always rebuilds.
    /// </summary>
    private static string CodeArtifactDigest(CodeBlock[] blocks, bool degraded)
    {
        if (degraded)
        {
            return "degraded";
        }
        using var hash = System.Security.Cryptography.IncrementalHash.CreateHash(
            System.Security.Cryptography.HashAlgorithmName.SHA256);
        foreach (CodeBlock block in blocks)
        {
            DigestField(hash, block.ByteOffset.ToString());
            DigestField(hash, block.Language ?? string.Empty);
            DigestField(hash, block.Source);
            foreach (SyntaxToken token in block.Tokens)
            {
                DigestField(hash, $"{token.StartByte}:{token.EndByte}:{token.Kind}");
            }
            hash.AppendData(BlockSeparator);
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    /// <summary>The math analog of <see cref="CodeArtifactDigest"/>:
    /// content-complete (source, MathML, speech, braille all shift the
    /// digest — a MathPrefs change re-renders speech with identical
    /// live text), degraded is its own identity. Incremental (round
    /// 5): the artifact set streams into the hash field by field,
    /// never materialized into one canonical buffer.</summary>
    private static string MathArtifactDigest(MathBlock[] blocks, bool degraded)
    {
        if (degraded)
        {
            return "math-degraded";
        }
        using var hash = System.Security.Cryptography.IncrementalHash.CreateHash(
            System.Security.Cryptography.HashAlgorithmName.SHA256);
        foreach (MathBlock block in blocks)
        {
            DigestField(hash, block.ByteOffset.ToString());
            DigestField(hash, block.DisplayStyle.ToString());
            DigestField(hash, block.Source);
            DigestField(hash, block.Mathml);
            DigestField(hash, block.Speech);
            hash.AppendData(block.Braille);
            hash.AppendData(BlockSeparator);
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    /// <summary>The diagram analog (W3-3): content-complete —
    /// source, description, status, and SVG bytes all shift the
    /// digest — with degraded as its own identity, streamed
    /// incrementally like the others.</summary>
    private static string DiagramArtifactDigest(DiagramBlock[] blocks, bool degraded)
    {
        if (degraded)
        {
            return "diagram-degraded";
        }
        using var hash = System.Security.Cryptography.IncrementalHash.CreateHash(
            System.Security.Cryptography.HashAlgorithmName.SHA256);
        foreach (DiagramBlock block in blocks)
        {
            DigestField(hash, block.ByteOffset.ToString());
            DigestField(hash, block.Source);
            DigestField(hash, block.StructuredDescription);
            DigestField(hash, block.RenderStatus switch
            {
                DiagramRenderStatus.UnsupportedDialect u => "unsupported:" + u.Reason,
                DiagramRenderStatus.RenderFailed f => "failed:" + f.Message,
                _ => "ok",
            });
            hash.AppendData(block.Svg ?? Array.Empty<byte>());
            hash.AppendData(BlockSeparator);
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    /// <summary>The embed analog (W3-5): content-complete over each
    /// key's resolution tree (kind, targets, text, truncation, nested
    /// raw targets, image bytes); a per-key degraded resolution is
    /// its own identity so recovery always rebuilds.</summary>
    private static string EmbedArtifactDigest(ReadingEmbedArtifact[] embeds)
    {
        using var hash = System.Security.Cryptography.IncrementalHash.CreateHash(
            System.Security.Cryptography.HashAlgorithmName.SHA256);
        foreach (ReadingEmbedArtifact embed in embeds)
        {
            DigestField(hash, embed.Key);
            DigestField(hash, embed.Alt ?? string.Empty);
            DigestField(hash, embed.ImageBudgetRefused ? "image-refused" : "image-ok");
            if (embed.Resolution is null)
            {
                DigestField(hash, "embed-degraded");
            }
            else
            {
                DigestField(hash, embed.Resolution.Truncated ? "truncated" : "complete");
                DigestResolution(hash, embed.Resolution.Resolution);
            }
            hash.AppendData(BlockSeparator);
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static void DigestResolution(
        System.Security.Cryptography.IncrementalHash hash, EmbedResolution resolution)
    {
        switch (resolution)
        {
            case EmbedResolution.FullNote fullNote:
                DigestField(hash, "note:" + fullNote.TargetPath);
                DigestField(hash, fullNote.Text);
                foreach (NestedEmbed nested in fullNote.Nested)
                {
                    DigestField(hash, nested.RawTarget);
                    DigestResolution(hash, nested.Resolution);
                }
                break;
            case EmbedResolution.Section section:
                DigestField(hash, "section:" + section.TargetPath + "#" + section.Heading);
                DigestField(hash, section.Text);
                foreach (NestedEmbed nested in section.Nested)
                {
                    DigestField(hash, nested.RawTarget);
                    DigestResolution(hash, nested.Resolution);
                }
                break;
            case EmbedResolution.Block block:
                DigestField(hash, "block:" + block.TargetPath + "^" + block.BlockId);
                DigestField(hash, block.Text);
                break;
            case EmbedResolution.Image image:
                DigestField(hash, "image:" + image.TargetPath + ":" + image.Mime);
                hash.AppendData(image.Bytes);
                hash.AppendData(FieldSeparator);
                break;
            case EmbedResolution.Unresolved unresolved:
                DigestField(hash, "unresolved:" + unresolved.Reason.GetType().Name);
                break;
        }
    }

    private static readonly byte[] FieldSeparator = { 0x01 };
    private static readonly byte[] BlockSeparator = { 0x02 };

    private static void DigestField(
        System.Security.Cryptography.IncrementalHash hash, string value)
    {
        hash.AppendData(System.Text.Encoding.UTF8.GetBytes(value));
        hash.AppendData(FieldSeparator);
    }
}
