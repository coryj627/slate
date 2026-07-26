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
                HostLog.Write(
                    HostDiagnosticEvent.ReadingRefreshTerminalFailure, exception);
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
                HostLog.Write(
                    HostDiagnosticEvent.ReadingRefreshTerminalFailure, exception);
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

    /// <summary>Background-safe: FFI only, no WPF objects.</summary>
    private static FetchResult Fetch(VaultSession session, string path, string text)
    {
        // Records ownership is inherent here — they are fetched for the
        // captured path inside the gated refresh, and publication
        // re-verifies the tab still shows that exact path (ordinal).
        OutgoingLink[] records = session.OutgoingLinks(path);
        RenderedCitation[] citations = RenderCitations(session, path);
        TaskItem[] tasks = session.TasksForFile(path).ToArray();
        ReadingBlock[] blocks = SlateUniffiMethods.ReadingBlocksSource(text);
        ReadingBlockInlines[] inlines = SlateUniffiMethods.ReadingInlineSegmentsSource(
            text, citations, records);
        return new FetchResult(text, citations, records, tasks, blocks, inlines);
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

        var key = new MemoKey(fetched.Text, fetched.Citations, fetched.Records);
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
        ReadingDocumentModel built = ReadingDocumentBuilder.Build(
            model.GetRange(0, firstEnd), context);

        _publishedRecords = fetched.Records;
        _publishedTasks = fetched.Tasks;
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
                AppendFragment(model, index, end, context);
                index = end;
            }
            FinishPublish(key, degraded, renderLimit, streamed: true);
            return;
        }
        _ = _dispatcher!.InvokeAsync(
            () => RunPublishStep(
                generation,
                () => ContinueBuild(
                    generation, model, firstEnd, renderLimit, key, degraded, context)),
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
            HostLog.Write(
                HostDiagnosticEvent.ReadingRefreshTerminalFailure, exception);
            PublishTerminalFailure(generation);
        }
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
        ReadingListBuildContext context)
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
        AppendFragment(model, index, end, context);
        if (end < renderLimit)
        {
            _ = _dispatcher!.InvokeAsync(
                () => RunPublishStep(
                    generation,
                    () => ContinueBuild(
                        generation, model, end, renderLimit, key, degraded, context)),
                DispatcherPriority.Background);
            return;
        }
        FinishPublish(key, degraded, renderLimit, streamed: true);
    }

    private void AppendFragment(
        List<(ReadingBlock, ReadingBlockInlines)> model,
        int index,
        int end,
        ReadingListBuildContext context) =>
        BlocksAppended?.Invoke(
            ReadingDocumentBuilder.Build(model.GetRange(index, end - index), context).Document);

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
    }

    private sealed record FetchResult(
        string Text,
        RenderedCitation[] Citations,
        OutgoingLink[] Records,
        TaskItem[] Tasks,
        ReadingBlock[] Blocks,
        ReadingBlockInlines[] Inlines);

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

        public MemoKey(string text, RenderedCitation[] citations, OutgoingLink[] records)
        {
            _text = text;
            _citations = citations;
            _records = records;
        }

        public bool Matches(MemoKey other) =>
            string.Equals(_text, other._text, StringComparison.Ordinal)
            && _citations.SequenceEqual(other._citations)
            && _records.SequenceEqual(other._records);
    }
}
