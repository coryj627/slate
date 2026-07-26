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
    private FlowDocument? _document;
    private bool _isLoading;
    private DispatcherTimer? _editDebounce;
    private ICSharpCode.AvalonEdit.Document.TextDocument? _observedDocument;

    /// <summary>Last published memo key; see <see cref="MemoKey"/>.</summary>
    private MemoKey? _memo;

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
        if (_disposed || _document is null)
        {
            return;
        }
        _memo = null;
        Refresh();
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
        if (_tab.EditorDocument is { } document
            && !ReferenceEquals(_observedDocument, document))
        {
            Detach();
            _observedDocument = document;
            document.TextChanged += Document_TextChanged;
        }
        Refresh();
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
        string text = _tab.Text;
        string path = _tab.Path;
        long revision = _tab.EditorSession?.Revision ?? -1;
        ulong sessionGeneration = _session.InteractionGeneration();

        if (_synchronousForTests)
        {
            FetchResult fetched = Fetch(_session, path, text);
            Publish(generation, path, revision, sessionGeneration, fetched);
            return;
        }

        IsLoading = true;
        _ = Task.Run(async () =>
        {
            FetchResult? fetched = null;
            for (int attempt = 1; attempt <= MaximumBackgroundRefreshAttempts; attempt++)
            {
                try
                {
                    fetched = Fetch(_session, path, text);
                    break;
                }
                catch (Exception exception) when (exception is VaultException or IOException)
                {
                    if (attempt == MaximumBackgroundRefreshAttempts)
                    {
                        // Terminal: event + exception type only, never
                        // payload text (W1-RT-01).
                        HostLog.WriteUiAutomationDiagnostic(
                            HostDiagnosticEvent.ReadingRefreshTerminalFailure);
                        return;
                    }
                    await Task.Delay(RetryDelay).ConfigureAwait(false);
                }
            }
            if (fetched is { } result)
            {
                _ = _dispatcher!.InvokeAsync(() =>
                    Publish(generation, path, revision, sessionGeneration, result));
            }
        });
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
        IsLoading = false;
        if (_disposed
            || generation != _generation
            || !string.Equals(_tab.Path, path, StringComparison.Ordinal)
            || (_tab.EditorSession?.Revision ?? -1) != revision
            || _session.InteractionGeneration() != sessionGeneration)
        {
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

        // §W3-1 item 9: the render limit is the documented ceiling,
        // extended past any list run so deliberate truncation never
        // cuts mid-list (a split list is two nav stops that lie).
        int renderLimit = model.Count > MaximumRenderedBlocks
            ? NextChunkBoundary(model, MaximumRenderedBlocks - BuildChunkBlocks, model.Count)
            : model.Count;
        bool degraded = model.Count > renderLimit;

        int firstEnd = NextChunkBoundary(model, 0, renderLimit);
        ReadingDocumentModel built = ReadingDocumentBuilder.Build(
            model.GetRange(0, firstEnd));

        _publishedRecords = fetched.Records;
        _publishedTasks = fetched.Tasks;
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
                int end = NextChunkBoundary(model, index, renderLimit);
                AppendFragment(model, index, end);
                index = end;
            }
            FinishPublish(key, degraded, renderLimit, streamed: true);
            return;
        }
        _ = _dispatcher!.InvokeAsync(
            () => ContinueBuild(generation, model, firstEnd, renderLimit, key, degraded),
            DispatcherPriority.Background);
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
        bool degraded)
    {
        if (_disposed || generation != _generation)
        {
            return;
        }
        int end = NextChunkBoundary(model, index, renderLimit);
        AppendFragment(model, index, end);
        if (end < renderLimit)
        {
            _ = _dispatcher!.InvokeAsync(
                () => ContinueBuild(generation, model, end, renderLimit, key, degraded),
                DispatcherPriority.Background);
            return;
        }
        FinishPublish(key, degraded, renderLimit, streamed: true);
    }

    private void AppendFragment(
        List<(ReadingBlock, ReadingBlockInlines)> model, int index, int end) =>
        BlocksAppended?.Invoke(
            ReadingDocumentBuilder.Build(model.GetRange(index, end - index)).Document);

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
            return;
        }
        _memo = key;
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

    /// <summary>
    /// Chunk boundaries never fall immediately before a ListItem
    /// continuation: the builder tracks its open list within one Build
    /// call, so a cut inside a list run would split one authored list
    /// into two — wrong structure AND two quick-nav stops.
    /// </summary>
    private static int NextChunkBoundary(
        List<(ReadingBlock, ReadingBlockInlines)> model, int index, int limit)
    {
        int end = Math.Min(index + BuildChunkBlocks, limit);
        while (end < limit && model[end].Item1.Kind is ReadingBlockKind.ListItem)
        {
            end++;
        }
        return end;
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
