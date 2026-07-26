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


    public bool IsLoading
    {
        get => _isLoading;
        private set => SetField(ref _isLoading, value);
    }

    public Action<A11yEvent> Announce => _announce;

    /// <summary>Activate one run kind (surface click/Enter path).</summary>
    public void Activate(ReadingInlineRunKind kind)
    {
        _activation ??= new ReadingActivation(
            _tab, _announce, () => _publishedRecords, _openExternalForTests);
        _activation.Activate(kind);
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
        ReadingBlock[] blocks = SlateUniffiMethods.ReadingBlocksSource(text);
        ReadingBlockInlines[] inlines = SlateUniffiMethods.ReadingInlineSegmentsSource(
            text, citations, records);
        return new FetchResult(text, citations, records, blocks, inlines);
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
        ReadingDocumentModel built = ReadingDocumentBuilder.Build(model);

        _memo = key;
        _publishedRecords = fetched.Records;
        // Only the DOCUMENT is published. The built model's landmarks
        // point into a container the surface's merge empties — the
        // surface re-collects over the live container, and a second
        // landmark source here would be a dangling-pointer trap.
        Document = built.Document;
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
