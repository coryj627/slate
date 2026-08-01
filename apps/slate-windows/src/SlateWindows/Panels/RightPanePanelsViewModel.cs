// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.ObjectModel;
using System.ComponentModel;
using uniffi.slate_uniffi;

namespace SlateWindows.Panels;

/// <summary>
/// W4-2 (#734): the data model behind the four link-and-structure
/// leaves — backlinks, outgoing links, outline, embeds.
///
/// The mac leaf-context matrix, ported: collections rebind on active-
/// tab changes (including in-place path replacement on current-tab
/// navigation) and empty out when no markdown note is active. The
/// collections live HERE, keyed on the note — the WPF leaf templates
/// may be re-instantiated on every leaf switch, so retention (mac's
/// mounted-ZStack rationale: no refetch, no re-announcement) is a
/// view-model property, never a view lifetime one.
///
/// Refresh policy is mac-verbatim: links, backlinks, and embeds load
/// once per note selection (one <c>NoteLoadBundle</c> lock); the
/// OUTLINE also refreshes after a save (headings move under edits;
/// link rows deliberately do not chase the buffer).
/// </summary>
internal sealed class RightPanePanelsViewModel : BindableBase
{
    /// <summary>Mac parity: backlinks page bound (limit 200).</summary>
    private const uint BacklinksLimit = 200;

    /// <summary>Note-wide embed budgets (adversarial rounds 1-2): the
    /// core budgets bound each SINGLE preview, so a note with
    /// hundreds of embed links — or a handful of huge images — still
    /// multiplied that bound without limit. Resolution slots are
    /// charged per occurrence KEY (anchored target + display text,
    /// since display text is per-occurrence image alt), so alt-text
    /// variation cannot multiply core calls past the cap either.
    /// A resolution whose image payload would push the cumulative
    /// budget past the cap is dropped, not retained (round 2: the
    /// pre-check let the final target blow through it). Duplicate
    /// occurrences share one resolution AND one built card — the
    /// round-2 hole was per-row re-decode of the same image. Rows
    /// themselves cap at MaxEmbedRows (the embeds ItemsControl is
    /// not virtualized); the tail degrades to one summary warning.
    /// Degradation is always a loud warning card, never a silent
    /// drop, and Jump survives wherever the target is known. The
    /// image figure matches the W3-5 reading-card host budget.</summary>
    internal const int MaxResolvedEmbedTargets = 128;
    internal const long MaxEmbedImageBytes = 16L * 1024 * 1024;
    internal const int MaxEmbedRows = 256;

    /// <summary>Rounds 4-5: encoded size says nothing about pixel
    /// allocation — a few-KB PNG decodes to ~5 MB at the 1120px
    /// decode bound, so 128 tiny images could pass the encoded
    /// budget while allocating ~640 MB of bitmaps. The bound is
    /// enforced INSIDE the decode (each image reserves its bounded
    /// cost from its header before pixels are allocated — a
    /// post-build check would let one many-image card allocate it
    /// all first); refused images are elided with a loud warning
    /// body while the rest of the card survives. NOTE-wide here;
    /// the popover applies the same figure per card.</summary>
    internal const long MaxEmbedDecodedImageBytes =
        EditorInteractionCoordinator.MaxDecodedImageBytesPerCard;

    private readonly VaultSession _session;
    private readonly Action<A11yEvent> _announce;
    private readonly Func<string, WorkspaceOpenTarget, bool> _openInternal;
    private readonly Func<string, bool> _openExternal;
    private readonly Action<LinkAnchor, string?> _scrollToAnchor;
    private readonly SynchronizationContext? _uiContext;

    private readonly object _workLock = new();
    private readonly HashSet<Task> _pendingWork = [];

    private string? _notePath;
    private string? _announcedOutlinePath;
    private int _loadGeneration;
    private int _outlineRequestId;
    private bool _isLoadingLinks;
    private bool _isLoadingOutline;
    private bool _isResolvingEmbeds;
    private string? _linksLoadError;
    private string? _outlineLoadError;
    private string? _embedsLoadError;
    private volatile bool _isShutDown;
    private readonly bool _synchronous;

    public RightPanePanelsViewModel(
        VaultSession session,
        Action<A11yEvent> announce,
        Func<string, WorkspaceOpenTarget, bool> openInternal,
        Func<string, bool> openExternal,
        Action<LinkAnchor, string?> scrollToAnchor,
        bool synchronousForTests = false)
    {
        _session = session;
        _announce = announce;
        _openInternal = openInternal;
        _openExternal = openExternal;
        _scrollToAnchor = scrollToAnchor;
        _uiContext = SynchronizationContext.Current;
        _synchronous = synchronousForTests;
    }

    internal int LoadGenerationForTests => _loadGeneration;

    internal int OutlineRequestIdForTests => _outlineRequestId;

    public ObservableCollection<BacklinkRowViewModel> Backlinks { get; } = [];

    public ObservableCollection<OutgoingLinkRowViewModel> OutgoingLinks { get; } = [];

    public ObservableCollection<OutlineRowViewModel> Outline { get; } = [];

    public ObservableCollection<EmbedRowViewModel> Embeds { get; } = [];

    /// <summary>Null when no markdown note is active — the leaves show
    /// their "Select a note …" empty states.</summary>
    public string? NotePath
    {
        get => _notePath;
        private set
        {
            if (SetField(ref _notePath, value))
            {
                RaiseHeaderChanges();
            }
        }
    }

    public bool IsLoadingLinks
    {
        get => _isLoadingLinks;
        private set
        {
            if (SetField(ref _isLoadingLinks, value))
            {
                RaiseHeaderChanges();
            }
        }
    }

    public bool IsLoadingOutline
    {
        get => _isLoadingOutline;
        private set
        {
            if (SetField(ref _isLoadingOutline, value))
            {
                RaiseHeaderChanges();
            }
        }
    }

    public bool IsResolvingEmbeds
    {
        get => _isResolvingEmbeds;
        private set
        {
            if (SetField(ref _isResolvingEmbeds, value))
            {
                RaiseHeaderChanges();
            }
        }
    }

    /// <summary>A core read failure loading the bundle: a database or
    /// filesystem fault must never masquerade as a legitimately empty
    /// note (adversarial round 3).</summary>
    public string? LinksLoadError
    {
        get => _linksLoadError;
        private set
        {
            if (SetField(ref _linksLoadError, value))
            {
                RaiseHeaderChanges();
            }
        }
    }

    public string? OutlineLoadError
    {
        get => _outlineLoadError;
        private set
        {
            if (SetField(ref _outlineLoadError, value))
            {
                RaiseHeaderChanges();
            }
        }
    }

    /// <summary>Whole-batch embed failure only — per-embed failures
    /// synthesize unresolved rows instead (mac audit #202).</summary>
    public string? EmbedsLoadError
    {
        get => _embedsLoadError;
        private set
        {
            if (SetField(ref _embedsLoadError, value))
            {
                RaiseHeaderChanges();
            }
        }
    }

    // ---- Header labels (mac LeafSection strings, verbatim) ----

    public string BacklinksHeader => Header("Backlinks", Backlinks.Count);

    public string OutgoingLinksHeader => Header("Outgoing links", OutgoingLinks.Count);

    public string EmbedsHeader => Header("Embeds", Embeds.Count);

    private static string Header(string title, int count) =>
        $"{title}, {count} {(count == 1 ? "entry" : "entries")}";

    // ---- Empty/loading states (mac LeafEmptyState sentences,
    // verbatim — LeafPortTests pins them there; ours pin here). Null
    // means the list has content and the message row collapses. ----

    public string? BacklinksEmptyMessage =>
        NotePath is null ? "Select a note to see its backlinks."
        : LinksLoadError is { Length: > 0 } error
            ? $"Could not load links: {error}"
        : IsLoadingLinks ? "Loading backlinks…"
        : Backlinks.Count == 0 ? "No notes link here yet."
        : null;

    public string? OutgoingLinksEmptyMessage =>
        NotePath is null ? "Select a note to see its outgoing links."
        : LinksLoadError is { Length: > 0 } error
            ? $"Could not load links: {error}"
        : IsLoadingLinks ? "Loading outgoing links…"
        : OutgoingLinks.Count == 0 ? "This note has no outgoing links."
        : null;

    public string? OutlineEmptyMessage =>
        NotePath is null ? "Select a note to see its outline."
        : OutlineLoadError is { Length: > 0 } error
            ? $"Could not load outline: {error}"
        : IsLoadingOutline ? "Loading outline…"
        : Outline.Count == 0 ? "This note has no headings."
        : null;

    public string? EmbedsEmptyMessage =>
        NotePath is null ? "Select a note to see its embeds."
        : EmbedsLoadError is { Length: > 0 } error
            ? $"Could not resolve embeds: {error}"
        : IsResolvingEmbeds ? "Resolving embeds…"
        : Embeds.Count == 0 ? "This note has no embeds."
        : null;

    /// <summary>
    /// The active markdown note changed (or went away). Mac's
    /// fireCollectionLoads: one bundle lock for links + backlinks,
    /// then the embeds resolution chain, plus the outline read.
    /// Passing the SAME path again is a no-op (leaf switches and tab
    /// re-activations must not refetch — the retention contract).
    /// </summary>
    public void NoteChanged(string? path)
    {
        if (_isShutDown
            || string.Equals(path, _notePath, StringComparison.Ordinal))
        {
            return;
        }
        NotePath = path;
        // Interlocked so the embed-resolve worker's mid-loop
        // Volatile.Read observes the bump promptly and abandons.
        int generation = Interlocked.Increment(ref _loadGeneration);
        Backlinks.Clear();
        OutgoingLinks.Clear();
        Outline.Clear();
        Embeds.Clear();
        LinksLoadError = null;
        OutlineLoadError = null;
        EmbedsLoadError = null;
        RaiseHeaderChanges();
        if (path is null)
        {
            IsLoadingLinks = false;
            IsLoadingOutline = false;
            IsResolvingEmbeds = false;
            return;
        }
        LoadLinks(path, generation);
        LoadOutline(path, generation, announceCount: true);
    }

    /// <summary>The active note was saved: the OUTLINE re-reads
    /// (headings move under edits); link rows deliberately stay
    /// (mac parity — they refresh on the next selection).</summary>
    public void NoteSaved(string path)
    {
        if (!string.Equals(path, _notePath, StringComparison.Ordinal))
        {
            return;
        }
        LoadOutline(path, _loadGeneration, announceCount: false);
    }

    private void LoadLinks(string path, int generation)
    {
        IsLoadingLinks = true;
        IsResolvingEmbeds = true;
        StartWork(() =>
        {
            NoteLoadBundle? bundle = null;
            string? failure = null;
            try
            {
                bundle = _session.NoteLoadBundle(
                    path, new Paging(null, BacklinksLimit));
            }
            catch (Exception exception) when (
                exception is not OutOfMemoryException
                    and not StackOverflowException
                    and not AccessViolationException)
            {
                // Broadened past VaultException (round 3): a session
                // torn down mid-flight throws the binding's disposal
                // guard, and an uncaught worker fault is unobserved.
                failure = exception.Message;
            }
            Post(() =>
            {
                if (generation != _loadGeneration)
                {
                    return;
                }
                if (bundle is null)
                {
                    // A read fault is NOT an empty note (round 3):
                    // publish it so the leaves say so instead of
                    // "no links here yet".
                    LinksLoadError = failure ?? "The note could not be read.";
                    EmbedsLoadError = LinksLoadError;
                    IsLoadingLinks = false;
                    IsResolvingEmbeds = false;
                    RaiseHeaderChanges();
                    return;
                }
                // Mutations before the loading-flag flip (round 3):
                // every binding notification must read final state.
                foreach (Backlink backlink in bundle.Backlinks.Items)
                {
                    Backlinks.Add(new BacklinkRowViewModel(backlink));
                }
                foreach (OutgoingLink link in bundle.OutgoingLinks)
                {
                    OutgoingLinks.Add(new OutgoingLinkRowViewModel(link));
                }
                IsLoadingLinks = false;
                RaiseHeaderChanges();
                ResolveEmbeds(
                    path,
                    generation,
                    bundle.OutgoingLinks.Where(link => link.IsEmbed).ToArray());
            });
        });
    }

    /// <summary>
    /// The embeds leaf is the outgoing list filtered to embeds (mac
    /// parity — no dedicated core API), each resolved through the
    /// BOUNDED preview path. G24's posture extends to this panel: mac
    /// resolves panel embeds unbounded; Windows keeps the core-owned
    /// preview budgets, degrading over-budget content with an explicit
    /// truncation notice rather than silently.
    /// </summary>
    private void ResolveEmbeds(string path, int generation, OutgoingLink[] embedLinks)
    {
        if (embedLinks.Length == 0)
        {
            IsResolvingEmbeds = false;
            RaiseHeaderChanges();
            return;
        }
        StartWork(() =>
        {
            // One core call AND one built card per occurrence key
            // (raw target + per-occurrence alt): the same [[target]]
            // embedded five times renders five rows off one shared
            // resolution and one shared card — no duplicate image
            // decodes. A null cache entry marks a budget-degraded
            // key so its duplicates degrade without re-resolving.
            var cache = new Dictionary<
                (string Target, string? Alt), EmbedRowViewModel.Shared?>();
            long imageBytes = 0;
            long decodedBytes = 0;
            // The note-wide decoded budget, reserved image-by-image
            // INSIDE the decode (single worker thread — no locking).
            bool ReserveDecoded(long cost)
            {
                if (decodedBytes + cost > MaxEmbedDecodedImageBytes)
                {
                    return false;
                }
                decodedBytes += cost;
                return true;
            }
            var rows = new List<EmbedRowViewModel>(
                Math.Min(embedLinks.Length, MaxEmbedRows + 1));
            for (int index = 0; index < embedLinks.Length; index++)
            {
                OutgoingLink link = embedLinks[index];

                // Mid-loop staleness check: a note switch during a
                // large batch must abandon, not keep resolving into
                // a publish that will be discarded anyway.
                if (Volatile.Read(ref _loadGeneration) != generation)
                {
                    return;
                }

                // The materialization cap: every row becomes a full
                // card visual in a non-virtualized ItemsControl, so
                // even cheap cache hits are bounded — the tail is
                // one loud summary, never a silent drop.
                if (rows.Count >= MaxEmbedRows)
                {
                    rows.Add(EmbedRowViewModel.RowLimit(
                        link, embedLinks.Length - index));
                    break;
                }

                // The ANCHORED target (round 6): TargetRaw is anchor-
                // stripped, so resolving by it renders ![[note#S]] as
                // the whole note and collides every anchored embed of
                // one note in the cache.
                string target =
                    EditorInteractionCoordinator.ComposeAnchoredTarget(link);
                (string Target, string? DisplayText) key =
                    (target, link.DisplayText);
                if (cache.TryGetValue(key, out EmbedRowViewModel.Shared? hit))
                {
                    rows.Add(hit is null
                        ? EmbedRowViewModel.OverBudget(link)
                        : EmbedRowViewModel.FromShared(link, hit));
                    continue;
                }

                if (cache.Count >= MaxResolvedEmbedTargets)
                {
                    cache[key] = null;
                    rows.Add(EmbedRowViewModel.OverBudget(link));
                    continue;
                }

                EmbedResolution resolution;
                bool truncated = false;
                try
                {
                    EmbedPreviewResolution preview = _session.ResolveEmbedPreview(
                        path, target, link.DisplayText);
                    resolution = preview.Resolution;
                    truncated = preview.Truncated;
                }
                catch (Exception exception) when (
                    exception is not OutOfMemoryException
                        and not StackOverflowException
                        and not AccessViolationException)
                {
                    // Per-embed failure synthesizes an unresolved row —
                    // the batch never discards partial success (mac
                    // audit #202). Broadened past VaultException so a
                    // session torn down mid-batch degrades instead of
                    // faulting the worker (round 3).
                    resolution = new EmbedResolution.Unresolved(
                        new EmbedUnresolvedReason.ReadError(
                            "The embed could not be resolved."));
                }

                // Post-resolution accounting: a payload that would
                // push the cumulative image budget past the cap is
                // DROPPED — the bound is on retained bytes, so no
                // single target may overshoot it.
                long cost = CountImageBytes(resolution);
                if (cost > 0 && imageBytes + cost > MaxEmbedImageBytes)
                {
                    cache[key] = null;
                    rows.Add(EmbedRowViewModel.OverBudget(link));
                    continue;
                }

                imageBytes += cost;
                var shared = new EmbedRowViewModel.Shared(
                    resolution,
                    truncated,
                    EditorInteractionCoordinator.BuildEmbedPreviewNode(
                        resolution, ReserveDecoded));
                cache[key] = shared;
                rows.Add(EmbedRowViewModel.FromShared(link, shared));
            }
            Post(() =>
            {
                if (generation != _loadGeneration)
                {
                    return;
                }
                foreach (EmbedRowViewModel row in rows)
                {
                    Embeds.Add(row);
                }
                IsResolvingEmbeds = false;
                RaiseHeaderChanges();
            });
        });
    }

    /// <summary>Every image payload anywhere in the resolved tree —
    /// nested embeds resolve inline, so their images spend the same
    /// note-wide budget as root ones.</summary>
    internal static long CountImageBytes(EmbedResolution resolution) =>
        resolution switch
        {
            EmbedResolution.Image image => image.Bytes.LongLength,
            EmbedResolution.FullNote full =>
                full.Nested.Sum(n => CountImageBytes(n.Resolution)),
            EmbedResolution.Section section =>
                section.Nested.Sum(n => CountImageBytes(n.Resolution)),
            _ => 0,
        };

    /// <summary>The RETAINED pixel cost of a built card: every decoded
    /// bitmap in the node tree at 4 bytes per pixel, nested cards
    /// included (round 4).</summary>
    internal static long CountDecodedImageBytes(EditorEmbedPreviewNode node)
    {
        long total = node.Image is System.Windows.Media.Imaging.BitmapSource
            bitmap
            ? (long)bitmap.PixelWidth * bitmap.PixelHeight
                * Math.Max(4, ((long)bitmap.Format.BitsPerPixel + 7) / 8)
            : 0;
        foreach (EditorEmbedPreviewPart part in node.Parts)
        {
            if (part.Nested is { } nested)
            {
                total += CountDecodedImageBytes(nested);
            }
        }
        return total;
    }

    private void LoadOutline(string path, int generation, bool announceCount)
    {
        // Save-refreshes reuse the note generation, so ordering among
        // outline requests needs its own token: without it, an older
        // in-flight read completing LAST would overwrite the newest
        // save's headings (adversarial round 2).
        int requestId = ++_outlineRequestId;
        IsLoadingOutline = true;
        StartWork(() =>
        {
            Heading[]? headings = null;
            string? failure = null;
            try
            {
                // Null for a path the scanner has not indexed (a file
                // created moments ago): an empty outline, not a fault.
                headings = _session.GetFileMetadata(path)?.Headings;
            }
            catch (Exception exception) when (
                exception is not OutOfMemoryException
                    and not StackOverflowException
                    and not AccessViolationException)
            {
                failure = exception.Message;
            }
            Post(() => PublishOutline(
                path, generation, requestId, headings, announceCount, failure));
        });
    }

    /// <summary>Internal so the stale-publish guard is testable with
    /// deterministic ordering (real completion races are timing).</summary>
    internal void PublishOutline(
        string path,
        int generation,
        int requestId,
        Heading[]? headings,
        bool announceCount,
        string? failure = null)
    {
        if (generation != _loadGeneration || requestId != _outlineRequestId)
        {
            return;
        }
        if (failure is not null)
        {
            // A read fault keeps the last-known outline rather than
            // masquerading as a note with no headings (round 3).
            OutlineLoadError = failure;
            IsLoadingOutline = false;
            RaiseHeaderChanges();
            return;
        }
        // Mutations FIRST: the loading-flag setter notifies the
        // empty-message binding, and WPF re-reads it synchronously —
        // notifying before the rows land froze the previous state's
        // sentence on screen (adversarial round 3).
        Outline.Clear();
        foreach (Heading heading in headings ?? [])
        {
            Outline.Add(new OutlineRowViewModel(heading));
        }
        OutlineLoadError = null;
        IsLoadingOutline = false;
        RaiseHeaderChanges();
        // Once per file, never for empty outlines, never again
        // on save-refresh (mac announcedFilePath guard — the
        // guard lives in the VIEW MODEL so template
        // re-instantiation on leaf switches cannot re-announce).
        if (announceCount
            && Outline.Count > 0
            && !string.Equals(
                _announcedOutlinePath, path, StringComparison.Ordinal))
        {
            _announcedOutlinePath = path;
            _announce(new A11yEvent.OutlineCount((uint)Outline.Count));
        }
    }

    // ---- Activation (mac AppState.openBacklink / openLink twins) ----

    public void OpenBacklink(
        BacklinkRowViewModel row,
        WorkspaceOpenTarget target = WorkspaceOpenTarget.CurrentTab)
    {
        // Announce only what HAPPENED: a dirty-tab cancel or failed
        // save refuses the navigation, and telling a reader the
        // target opened while the editor stayed put is a false
        // success (adversarial round 1).
        if (_openInternal(row.SourcePath, target))
        {
            _announce(new A11yEvent.InternalNavigated(
                "Opened backlink to", row.FileName));
        }
    }

    public void OpenOutgoingLink(
        OutgoingLinkRowViewModel row,
        WorkspaceOpenTarget target = WorkspaceOpenTarget.CurrentTab)
    {
        OutgoingLink link = row.Link;
        if (link.IsExternal)
        {
            OpenExternal(link.TargetRaw);
            return;
        }
        if (link.IsUnresolved || link.TargetPath is not { Length: > 0 } targetPath)
        {
            // Defensive branch included (mac parity): a non-external
            // link without a resolved path is treated as unresolved.
            _announce(new A11yEvent.LinkUnresolved(link.TargetRaw));
            return;
        }
        if (_openInternal(targetPath, target))
        {
            _announce(new A11yEvent.InternalNavigated(
                "Opened", System.IO.Path.GetFileName(targetPath)));
        }
    }

    public void OpenEmbedSource(string targetPath)
    {
        if (_openInternal(targetPath, WorkspaceOpenTarget.CurrentTab))
        {
            _announce(new A11yEvent.InternalNavigated(
                "Opened embed source", System.IO.Path.GetFileName(targetPath)));
        }
    }

    /// <summary>Outline activation scrolls the CURRENT note to the
    /// heading (the editor anchor path announces ScrolledToHeading on
    /// an actual landing — mac #431 parity). The anchor carries the
    /// UNIQUE slug, not the display text: the core resolver matches
    /// text against the first occurrence, so duplicate headings would
    /// all land on the topmost one. The display text rides along so
    /// the landing announcement speaks prose, not the slug.</summary>
    public void OpenHeading(OutlineRowViewModel row) =>
        _scrollToAnchor(new LinkAnchor("heading", row.AnchorId), row.Text);

    private void OpenExternal(string target)
    {
        // The mac allowlist, verbatim: anything else — file:,
        // javascript:, custom schemes — is refused loudly.
        bool allowed = Uri.TryCreate(target, UriKind.Absolute, out Uri? uri)
            && uri.Scheme is "http" or "https" or "mailto";
        if (!allowed)
        {
            _announce(new A11yEvent.ExternalLinkUnsupported(target));
            return;
        }
        _announce(_openExternal(target)
            ? new A11yEvent.ExternalLinkOpened()
            : new A11yEvent.ExternalLinkFailed(target));
    }

    private void RaiseHeaderChanges()
    {
        OnPropertyChanged(nameof(BacklinksHeader));
        OnPropertyChanged(nameof(OutgoingLinksHeader));
        OnPropertyChanged(nameof(EmbedsHeader));
        OnPropertyChanged(nameof(BacklinksEmptyMessage));
        OnPropertyChanged(nameof(OutgoingLinksEmptyMessage));
        OnPropertyChanged(nameof(OutlineEmptyMessage));
        OnPropertyChanged(nameof(EmbedsEmptyMessage));
    }

    /// <summary>Workspace teardown: invalidate every in-flight load
    /// so nothing publishes into a dying UI, and refuse new work. The
    /// broadened worker catches make any still-running core call
    /// against a subsequently disposed session degrade instead of
    /// faulting (adversarial round 3).</summary>
    internal void Shutdown()
    {
        _isShutDown = true;
        _ = Interlocked.Increment(ref _loadGeneration);
    }

    /// <summary>All load work funnels through here. Synchronous mode
    /// (the ReadingContentViewModel test pattern) runs the body
    /// inline: without a UI SynchronizationContext, worker publishes
    /// would land on background threads and race every list the test
    /// thread is reading — the CI-only "Collection was modified"
    /// failure in unrelated workspace tests.</summary>
    private void StartWork(Action body)
    {
        if (_synchronous)
        {
            body();
            return;
        }
        TrackWork(Task.Run(body));
    }

    /// <summary>Every worker is tracked so tests (and shutdown
    /// diagnostics) can drain deterministically; bodies catch their
    /// own failures, so tracked tasks never fault.</summary>
    private void TrackWork(Task work)
    {
        lock (_workLock)
        {
            _ = _pendingWork.Add(work);
        }
        _ = work.ContinueWith(
            completed =>
            {
                lock (_workLock)
                {
                    _ = _pendingWork.Remove(completed);
                }
            },
            TaskScheduler.Default);
    }

    internal Task DrainForTests()
    {
        Task[] snapshot;
        lock (_workLock)
        {
            snapshot = [.. _pendingWork];
        }
        return Task.WhenAll(snapshot);
    }

    private void Post(Action action)
    {
        if (_uiContext is null)
        {
            action();
        }
        else
        {
            _uiContext.Post(_ => action(), null);
        }
    }
}

/// <summary>One backlink row — always resolved by construction (the
/// core query joins on resolved targets).</summary>
internal sealed class BacklinkRowViewModel
{
    public BacklinkRowViewModel(Backlink backlink)
    {
        SourcePath = backlink.SourcePath;
        FileName = System.IO.Path.GetFileName(backlink.SourcePath);
        Snippet = backlink.Snippet;
    }

    public string SourcePath { get; }

    public string FileName { get; }

    public string Snippet { get; }

    /// <summary>Mac label, verbatim.</summary>
    public string AutomationName => $"Backlink from {FileName}, context: {Snippet}";

    public string AutomationHelpText => "Opens the source note.";
}

/// <summary>One outgoing-link row with the three-state contract:
/// resolved internal, unresolved internal, external.</summary>
internal sealed class OutgoingLinkRowViewModel
{
    public OutgoingLinkRowViewModel(OutgoingLink link)
    {
        Link = link;
        DisplayTarget = link.TargetPath is { Length: > 0 } path
            ? System.IO.Path.GetFileName(path)
            : link.TargetRaw;
    }

    public OutgoingLink Link { get; }

    public string DisplayTarget { get; }

    public string Snippet => Link.Snippet;

    public bool HasSnippet => Link.Snippet.Length > 0;

    public bool IsUnresolved => Link.IsUnresolved;

    /// <summary>The state badge chip text; the role is folded into the
    /// row label, so the chip itself stays out of the UIA name.</summary>
    public string? Badge =>
        Link.IsExternal ? "External"
        : Link.IsUnresolved ? "Unresolved"
        : Link.IsEmbed ? "Embed"
        : null;

    /// <summary>Mac per-state labels, verbatim.</summary>
    public string AutomationName =>
        Link.IsExternal ? $"External link: {Link.TargetRaw}"
        : Link.IsUnresolved ? $"Unresolved link: {Link.TargetRaw}"
        : $"Link to {DisplayTarget}";

    /// <summary>Mac per-state hints, verbatim.</summary>
    public string AutomationHelpText =>
        Link.IsExternal ? "Opens in the default browser."
        : Link.IsUnresolved ? "Cannot open. Target file is not in the vault."
        : "Opens the linked note.";
}

/// <summary>One flat outline row (mac's OutlineSidebar list is flat —
/// heading NAVIGATION belongs to W3-1's chords, this leaf is the
/// nav-utility feature).</summary>
internal sealed class OutlineRowViewModel
{
    public OutlineRowViewModel(Heading heading)
    {
        Level = heading.Level;
        Text = heading.Text;
        AnchorId = heading.AnchorId;
    }

    public byte Level { get; }

    public string Text { get; }

    public string AnchorId { get; }

    /// <summary>Indentation carries the level visually; the label
    /// carries it for AT (mac verbatim).</summary>
    public double Indent => (Level - 1) * 14.0;

    public string AutomationName => $"Level {Level} heading: {Text}";
}

/// <summary>One embeds-leaf row: the resolved card tree (the shared
/// EditorEmbedPreviewNode renderer) for one embed link.</summary>
internal sealed class EmbedRowViewModel
{
    internal const string OverBudgetMessage =
        "Embed limit reached for this note. Open source to view this content.";

    /// <summary>One resolution + one built card, shared across every
    /// duplicate occurrence of the same (target, alt) key — the
    /// round-2 hole was a fresh card (and image decode) per row.</summary>
    internal sealed record Shared(
        EmbedResolution Resolution,
        bool Truncated,
        EditorEmbedPreviewNode Node);

    public EmbedRowViewModel(
        OutgoingLink link, EmbedResolution resolution, bool truncated)
        : this(
            link,
            resolution,
            truncated,
            EditorInteractionCoordinator.BuildEmbedPreviewNode(resolution))
    {
    }

    private EmbedRowViewModel(
        OutgoingLink link,
        EmbedResolution resolution,
        bool truncated,
        EditorEmbedPreviewNode node)
    {
        Link = link;
        Resolution = resolution;
        Truncated = truncated;
        Node = node;
    }

    public static EmbedRowViewModel FromShared(
        OutgoingLink link, Shared shared) => new(
        link, shared.Resolution, shared.Truncated, shared.Node);

    /// <summary>The note-wide budget refused to resolve this target:
    /// a warning card that never silently drops the row, and keeps
    /// Jump to source alive when the target is known.</summary>
    public static EmbedRowViewModel OverBudget(OutgoingLink link) => new(
        link,
        new EmbedResolution.Unresolved(
            new EmbedUnresolvedReason.ReadError(OverBudgetMessage)),
        truncated: false,
        new EditorEmbedPreviewNode(
            OverBudgetMessage,
            [],
            null,
            link.TargetPath,
            IsDisclosure: false,
            InitiallyExpanded: false,
            IsWarning: true));

    /// <summary>The materialized-row cap: one summary warning covers
    /// the whole hidden tail (no Jump — it stands for many targets).</summary>
    public static EmbedRowViewModel RowLimit(
        OutgoingLink link, int hiddenCount)
    {
        string message =
            $"Embed limit reached for this note. {hiddenCount} more "
            + $"embed{(hiddenCount == 1 ? " is" : "s are")} not shown.";
        return new EmbedRowViewModel(
            link,
            new EmbedResolution.Unresolved(
                new EmbedUnresolvedReason.ReadError(message)),
            truncated: false,
            new EditorEmbedPreviewNode(
                message,
                [],
                null,
                null,
                IsDisclosure: false,
                InitiallyExpanded: false,
                IsWarning: true));
    }

    public OutgoingLink Link { get; }

    public EmbedResolution Resolution { get; }

    /// <summary>Core preview budgets clipped the content (G24: the
    /// bound is explicit, never silent).</summary>
    public bool Truncated { get; }

    public EditorEmbedPreviewNode Node { get; }
}
