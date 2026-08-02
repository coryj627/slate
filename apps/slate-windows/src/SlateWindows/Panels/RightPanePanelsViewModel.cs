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
internal sealed class RightPanePanelsViewModel : PanelWorkScheduler
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

    /// <summary>Round 7: core returns outgoing links UNPAGED, and the
    /// publish ran one row VM + one collection notification per link
    /// on the UI thread — a link-dense note produced tens of
    /// thousands of dispatcher mutations before any cap applied.
    /// Display caps here with an explicit notice; the header keeps
    /// the TRUE total, and embeds still derive from the full set
    /// (their own budgets bound that work).</summary>
    internal const int MaxOutgoingRows = 512;

    /// <summary>Round 8: heading extraction is count-unbounded in
    /// core, so the outline needed the same display cap — a
    /// generated note with tens of thousands of headings produced
    /// the outgoing-list fan-out all over again (including on every
    /// save-refresh). The OutlineCount announcement keeps the TRUE
    /// total.</summary>
    internal const int MaxOutlineRows = 512;

    /// <summary>W4-3: the note-tasks display cap — same posture as
    /// the outline (task extraction is count-unbounded per note);
    /// core bounds the read in SQL and the header speaks the true
    /// totals past the cap.</summary>
    internal const int MaxTaskRows = 512;

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
    private readonly Func<TaskItem, string, bool> _toggleTask;
    private readonly Action<TaskItem, string> _scrollToTask;

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
    private int _totalOutgoingLinks;
    private int _totalOutlineHeadings;
    private int _totalBacklinks;
    private int _totalEmbeds;
    private int _totalTasks;
    private int _openTaskTotal;
    private int _tasksRequestId;
    private bool _isLoadingTasks;
    private string? _tasksLoadError;

    private readonly TaskIndexRepairCoordinator _repairs;

    public RightPanePanelsViewModel(
        VaultSession session,
        Action<A11yEvent> announce,
        Func<string, WorkspaceOpenTarget, bool> openInternal,
        Func<string, bool> openExternal,
        Action<LinkAnchor, string?> scrollToAnchor,
        Func<TaskItem, string, bool> toggleTask,
        Action<TaskItem, string> scrollToTask,
        TaskIndexRepairCoordinator? repairs = null,
        bool synchronousForTests = false)
        : base(synchronousForTests)
    {
        _session = session;
        _announce = announce;
        _openInternal = openInternal;
        _openExternal = openExternal;
        _scrollToAnchor = scrollToAnchor;
        _toggleTask = toggleTask;
        _scrollToTask = scrollToTask;
        _repairs = repairs ?? new TaskIndexRepairCoordinator(session);
    }

    internal int LoadGenerationForTests => _loadGeneration;

    internal int OutlineRequestIdForTests => _outlineRequestId;

    public ObservableCollection<BacklinkRowViewModel> Backlinks { get; } = [];

    public ObservableCollection<OutgoingLinkRowViewModel> OutgoingLinks { get; } = [];

    public ObservableCollection<OutlineRowViewModel> Outline { get; } = [];

    public ObservableCollection<EmbedRowViewModel> Embeds { get; } = [];

    /// <summary>Note tasks, grouped mac-style: open first, done
    /// second, document order within each (grouping is by the
    /// COMPLETED derivation only — a cancelled '[-]' task lists as
    /// open unless its char is x/X, the shipped mac semantics).</summary>
    public ObservableCollection<NoteTaskRowViewModel> OpenTasks { get; } = [];

    public ObservableCollection<NoteTaskRowViewModel> DoneTasks { get; } = [];

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

    /// <summary>The TRUE inbound count (round 11): the page request
    /// is bounded at 200, and the header must not present the cap as
    /// the whole story.</summary>
    public string BacklinksHeader => Header(
        "Backlinks", Math.Max(_totalBacklinks, Backlinks.Count));

    public string? BacklinksTruncationNotice =>
        _totalBacklinks > Backlinks.Count && Backlinks.Count > 0
            ? $"Showing {Backlinks.Count} of {_totalBacklinks} backlinks."
            : null;

    /// <summary>The TRUE link count — the list itself caps display
    /// at MaxOutgoingRows.</summary>
    public string OutgoingLinksHeader => Header(
        "Outgoing links", Math.Max(_totalOutgoingLinks, OutgoingLinks.Count));

    /// <summary>Non-null when the outgoing list was display-capped:
    /// the truncation is spoken, never silent.</summary>
    public string? OutgoingLinksTruncationNotice =>
        _totalOutgoingLinks > OutgoingLinks.Count && OutgoingLinks.Count > 0
            ? $"Showing {OutgoingLinks.Count} of {_totalOutgoingLinks} "
                + "outgoing links."
            : null;

    public string? OutlineTruncationNotice =>
        _totalOutlineHeadings > Outline.Count && Outline.Count > 0
            ? $"Showing {Outline.Count} of {_totalOutlineHeadings} headings."
            : null;

    // ---- Tasks leaf (W4-3; mac TasksPanel strings, verbatim) ----

    /// <summary>"Tasks, none" / "Tasks, N open of M task|tasks" —
    /// TRUE totals, not the display-capped collections.</summary>
    public string TasksHeader => _totalTasks == 0
        ? "Tasks, none"
        : $"Tasks, {_openTaskTotal} open of {_totalTasks} "
            + (_totalTasks == 1 ? "task" : "tasks");

    public string OpenTasksGroupHeader => $"Open ({OpenTasks.Count})";

    public string DoneTasksGroupHeader => $"Done ({DoneTasks.Count})";

    public string? TasksEmptyMessage =>
        NotePath is null ? "Select a note to see its tasks."
        : _tasksLoadError is { Length: > 0 } error
            ? $"Could not load tasks: {error}"
        : _isLoadingTasks ? "Loading tasks…"
        : _totalTasks == 0 ? "No tasks in this note."
        : null;

    public string? TasksTruncationNotice
    {
        get
        {
            int shown = OpenTasks.Count + DoneTasks.Count;
            return _totalTasks > shown && shown > 0
                ? $"Showing {shown} of {_totalTasks} tasks."
                : null;
        }
    }

    /// <summary>The TRUE embed count (round 17): the collection caps
    /// at 256 cards plus one synthetic summary row, so counting it
    /// would tell AT "257 entries" on a 2000-embed note.</summary>
    public string EmbedsHeader => Header("Embeds", _totalEmbeds);

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
        if (IsShutDown
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
        OpenTasks.Clear();
        DoneTasks.Clear();
        LinksLoadError = null;
        OutlineLoadError = null;
        EmbedsLoadError = null;
        _tasksLoadError = null;
        _totalOutgoingLinks = 0;
        _totalOutlineHeadings = 0;
        _totalBacklinks = 0;
        _totalEmbeds = 0;
        _totalTasks = 0;
        _openTaskTotal = 0;
        RaiseHeaderChanges();
        if (path is null)
        {
            IsLoadingLinks = false;
            IsLoadingOutline = false;
            IsResolvingEmbeds = false;
            _isLoadingTasks = false;
            return;
        }
        LoadLinks(path, generation);
        LoadOutline(path, generation, announceCount: true);
        LoadTasks(path, generation);
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
        LoadTasks(path, _loadGeneration);
    }

    private void LoadLinks(string path, int generation)
    {
        IsLoadingLinks = true;
        IsResolvingEmbeds = true;
        StartWork(() =>
        {
            NoteLinkPanels? bundle = null;
            string? failure = null;
            try
            {
                // The BOUNDED feed (round 10): display, candidate,
                // and outline caps apply in core SQL — a link-dense
                // note never materializes an unbounded vector on
                // either side of the FFI boundary.
                bundle = _session.NoteLinkPanels(
                    path,
                    new Paging(null, BacklinksLimit),
                    MaxOutgoingRows,
                    MaxEmbedRows + 1);
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
                _totalBacklinks = checked((int)bundle.Backlinks.TotalFiltered);
                foreach (Backlink backlink in bundle.Backlinks.Items)
                {
                    Backlinks.Add(new BacklinkRowViewModel(backlink));
                }
                // Rows arrive pre-capped from core (rounds 7 + 10);
                // the true total labels what the cap hides.
                _totalOutgoingLinks = checked((int)bundle.OutgoingTotal);
                foreach (OutgoingLink link in bundle.OutgoingLinks)
                {
                    OutgoingLinks.Add(new OutgoingLinkRowViewModel(link));
                }
                IsLoadingLinks = false;
                RaiseHeaderChanges();
                ResolveEmbeds(
                    path,
                    generation,
                    bundle.EmbedLinks,
                    checked((int)bundle.EmbedTotal));
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
    private void ResolveEmbeds(
        string path, int generation, OutgoingLink[] embedLinks, int totalEmbeds)
    {
        if (embedLinks.Length == 0)
        {
            _totalEmbeds = totalEmbeds;
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
            bool ReserveDecoded(long cost) =>
                EditorInteractionCoordinator.TryReserveDecodedBytes(
                    ref decodedBytes, cost, MaxEmbedDecodedImageBytes);
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
                    // The candidate array is itself capped (round 8),
                    // so the tail size comes from the TRUE total.
                    rows.Add(EmbedRowViewModel.RowLimit(
                        link, totalEmbeds - index));
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
                    // Pool-clamped (round 11): the remaining note-wide
                    // image pool rides INTO core, so payloads past it
                    // are refused before any FFI record carries them —
                    // the post-arrival check below can no longer be
                    // reached by images, but stays as the final wall.
                    EmbedPreviewResolution preview =
                        _session.ResolveEmbedPreviewPooled(
                            path,
                            target,
                            link.DisplayText,
                            (ulong)Math.Max(0, MaxEmbedImageBytes - imageBytes));
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

                // A pool-refused image comes back Unresolved with
                // truncation marked (round 11) — rebuild the loud
                // budget card so Jump to the KNOWN target survives
                // the refusal (round 12: the generic unresolved node
                // has no source path).
                if (truncated
                    && resolution is EmbedResolution.Unresolved
                    && link.TargetPath is not null)
                {
                    cache[key] = null;
                    rows.Add(EmbedRowViewModel.OverBudget(link));
                    continue;
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
                _totalEmbeds = totalEmbeds;
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
            Heading[] headings = [];
            int total = 0;
            string? failure = null;
            try
            {
                // Bounded in core SQL (round 10): an unindexed path
                // is an empty page, not a fault.
                OutlinePage page = _session.NoteOutline(path, MaxOutlineRows);
                headings = page.Headings;
                total = checked((int)page.Total);
            }
            catch (Exception exception) when (
                exception is not OutOfMemoryException
                    and not StackOverflowException
                    and not AccessViolationException)
            {
                failure = exception.Message;
            }
            Post(() => PublishOutline(
                path, generation, requestId, headings, total,
                announceCount, failure));
        });
    }

    /// <summary>Internal so the stale-publish guard is testable with
    /// deterministic ordering (real completion races are timing).</summary>
    internal void PublishOutline(
        string path,
        int generation,
        int requestId,
        Heading[] headings,
        int total,
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
        // sentence on screen (adversarial round 3). Rows arrive
        // pre-capped from core (rounds 8 + 10) — the .Take is a
        // belt-and-suspenders bound on this dispatcher fan-out; the
        // notice and announcement carry the TRUE total.
        _totalOutlineHeadings = total;
        Outline.Clear();
        foreach (Heading heading in headings.Take(MaxOutlineRows))
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
            _announce(new A11yEvent.OutlineCount(
                (uint)_totalOutlineHeadings));
        }
    }

    private void LoadTasks(string path, int generation)
    {
        // Save-refreshes reuse the note generation (the outline
        // pattern): ordering among task reads needs its own token.
        int requestId = ++_tasksRequestId;
        _isLoadingTasks = true;
        RaiseHeaderChanges();
        StartWork(() =>
        {
            NoteTasksPage? page = null;
            string? failure = null;
            // The shared repair quarantine gates THIS surface too
            // (adversarial rounds 15-19): after a post-write failure
            // whose repair also failed, the index is known stale —
            // querying it would republish the rolled-back row and
            // its ghost hash. The panel SWEEPS like the review (a
            // pending path must not bar this surface forever when
            // the review is closed), then takes the atomic
            // clean-state ticket; while repairs keep failing, the
            // honest read-fault surface shows instead of the ghost.
            RepairSweep sweep = _repairs.Retry();
            if (!TryAwaitCleanTicket(out long quarantineEpoch))
            {
                Post(() => PublishTasks(
                    generation,
                    requestId,
                    page: null,
                    sweep.LastError ?? "The vault index needs repair."));
                return;
            }
            TasksInterleaveForTests?.Invoke();
            try
            {
                page = _session.NoteTasks(path, MaxTaskRows);
            }
            catch (Exception exception) when (
                exception is not OutOfMemoryException
                    and not StackOverflowException
                    and not AccessViolationException)
            {
                failure = exception.Message;
            }
            // Epoch, not HasPendingFor, re-checked AFTER the query
            // (adversarial rounds 16-17): a post-write failure that
            // registered between the gate and the read moved no
            // revision the read could see (its index transaction
            // rolled back) — and a register-repair-remove completing
            // DURING the read leaves nothing pending while the page
            // still holds the pre-repair ghost. Only the epoch sees
            // both.
            if (failure is null && _repairs.Epoch != quarantineEpoch)
            {
                page = null;
                failure = "The vault index needs repair.";
            }
            Post(() => PublishTasks(generation, requestId, page, failure));
        });
    }

    /// <summary>Worker-side clean-ticket acquisition (round 19):
    /// lease-only blocks are live writes milliseconds from settling
    /// — retry briefly; pending repairs refuse immediately.</summary>
    private bool TryAwaitCleanTicket(out long epoch)
    {
        for (int attempt = 0; ; attempt++)
        {
            if (_repairs.TryBeginCleanQuery(out epoch, out bool leasesOnly))
            {
                return true;
            }
            // Leases are transient (one bounded file write), but a
            // loaded CI runner can hold one past a short budget —
            // and a barred FIRST page has no auto-retry, so patience
            // here beats a stuck banner (10s total).
            if (!leasesOnly || attempt >= 200)
            {
                return false;
            }
            Thread.Sleep(50);
        }
    }

    /// <summary>Test seam (round 16): runs between the quarantine
    /// gate and the note-tasks query, where a concurrent post-write
    /// failure is otherwise impossible to schedule.</summary>
    internal Action? TasksInterleaveForTests { get; set; }

    /// <summary>Internal so the stale-publish guard is testable with
    /// deterministic ordering (the PublishOutline pattern).</summary>
    internal void PublishTasks(
        int generation, int requestId, NoteTasksPage? page, string? failure)
    {
        if (generation != _loadGeneration || requestId != _tasksRequestId)
        {
            return;
        }
        if (failure is not null || page is null)
        {
            // A read fault is NOT a task-free note (the W4-2 honesty
            // posture; the mac panel's silent-error quirk is a
            // recorded divergence).
            _tasksLoadError = failure ?? "The note could not be read.";
            _isLoadingTasks = false;
            RaiseHeaderChanges();
            return;
        }
        // Mutations before notifications (round 3): every raise must
        // read final state.
        OpenTasks.Clear();
        DoneTasks.Clear();
        foreach (TaskItem task in page.Tasks)
        {
            var row = new NoteTaskRowViewModel(task, page.ContentHash);
            if (task.Completed)
            {
                DoneTasks.Add(row);
            }
            else
            {
                OpenTasks.Add(row);
            }
        }
        _totalTasks = checked((int)page.Total);
        _openTaskTotal = checked((int)page.OpenTotal);
        _tasksLoadError = null;
        _isLoadingTasks = false;
        RaiseHeaderChanges();
    }

    /// <summary>Checkbox / Space: route the toggle through the
    /// workspace seam (the active tab's guarded ToggleTask — dirty
    /// refusal, conflict detection, and the canonical announcements
    /// all live there), carrying the row's snapshot hash so a row
    /// read before a save can't toggle whichever task inherited its
    /// ordinal (adversarial round 2). The refresh arrives via the
    /// save funnel.</summary>
    public void ToggleTask(NoteTaskRowViewModel row) =>
        _ = _toggleTask(row.Task, row.ContentHash);

    /// <summary>Re-snapshot only the task rows (adversarial round 2):
    /// a toggle that failed its snapshot-hash check needs fresh rows
    /// without disturbing the outline.</summary>
    public void ReloadTasks()
    {
        if (_notePath is { } path)
        {
            LoadTasks(path, _loadGeneration);
        }
    }

    /// <summary>Row activation scrolls the editor to the task's line
    /// (mac: silent scroll; the caret move is the observable). The
    /// row's snapshot hash rides along (adversarial round 7): a byte
    /// offset only means anything against the content it was read
    /// from, so the workspace refuses the scroll — silently, the
    /// panel's activation posture — when the saved note has moved on.</summary>
    public void OpenTask(NoteTaskRowViewModel row) =>
        _scrollToTask(row.Task, row.ContentHash);

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
        OnPropertyChanged(nameof(OutgoingLinksTruncationNotice));
        OnPropertyChanged(nameof(OutlineTruncationNotice));
        OnPropertyChanged(nameof(BacklinksTruncationNotice));
        OnPropertyChanged(nameof(TasksHeader));
        OnPropertyChanged(nameof(OpenTasksGroupHeader));
        OnPropertyChanged(nameof(DoneTasksGroupHeader));
        OnPropertyChanged(nameof(TasksEmptyMessage));
        OnPropertyChanged(nameof(TasksTruncationNotice));
    }

    /// <summary>Workspace teardown: invalidate every in-flight load
    /// so nothing publishes into a dying UI, and refuse new work. The
    /// broadened worker catches make any still-running core call
    /// against a subsequently disposed session degrade instead of
    /// faulting (adversarial round 3).</summary>
    internal override void Shutdown()
    {
        base.Shutdown();
        _ = Interlocked.Increment(ref _loadGeneration);
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
        // Display strings are BOUNDED; Link keeps the exact record
        // for activation and resolution (round 14: a truncated URL
        // opens a different address, so exactness lives on the data
        // and the ceiling lives on the rendering).
        Link = link;
        DisplayTarget = link.TargetPath is { Length: > 0 } path
            ? System.IO.Path.GetFileName(path)
            : EditorInteractionCoordinator.BoundDisplayText(link.TargetRaw);
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

    /// <summary>Mac per-state labels, verbatim — over display-bounded
    /// targets (round 14).</summary>
    public string AutomationName =>
        Link.IsExternal
            ? "External link: "
                + EditorInteractionCoordinator.BoundDisplayText(Link.TargetRaw)
        : Link.IsUnresolved
            ? "Unresolved link: "
                + EditorInteractionCoordinator.BoundDisplayText(Link.TargetRaw)
        : $"Link to {DisplayTarget}";

    /// <summary>Mac per-state hints, verbatim.</summary>
    public string AutomationHelpText =>
        Link.IsExternal ? "Opens in the default browser."
        : Link.IsUnresolved ? "Cannot open. Target file is not in the vault."
        : "Opens the linked note.";
}

/// <summary>One note-task row (W4-3): the mac TasksPanel row shape.
/// The record stays EXACT for toggling/scrolling; rendered text and
/// the UIA name are display-bounded (the W4-2 round-14 split). The
/// row also pins the note's content hash AT READ TIME (adversarial
/// round 2): rows are snapshots, and a toggle must prove the note
/// wasn't rewritten underneath it — a stale ordinal against newer
/// content could name a different task.</summary>
internal sealed class NoteTaskRowViewModel
{
    public NoteTaskRowViewModel(TaskItem task, string contentHash)
    {
        Task = task;
        ContentHash = contentHash;
        DisplayText = EditorInteractionCoordinator.BoundDisplayText(task.Text);
        MetadataCaption = string.Join(
            " · ",
            TaskStatusPhrase.MetadataParts(task)
                .Select(EditorInteractionCoordinator.BoundDisplayText));
    }

    public TaskItem Task { get; }

    /// <summary>The note's content hash when this row was read.</summary>
    public string ContentHash { get; }

    public string DisplayText { get; }

    public bool Completed => Task.Completed;

    /// <summary>"Due … · Priority … · Repeats …" (empty when the
    /// task carries no metadata; the caption row collapses).</summary>
    public string MetadataCaption { get; }

    public bool HasMetadata => MetadataCaption.Length > 0;

    /// <summary>The mac note-panel row label, verbatim shape:
    /// "&lt;statusWord&gt;. &lt;text&gt;. Due &lt;date&gt;.
    /// Priority &lt;level&gt;. Repeats &lt;rec&gt;.
    /// &lt;statusPhrase&gt;" joined by ". ".</summary>
    public string AutomationName => string.Join(
        ". ",
        new[] { TaskStatusPhrase.StatusWord(Task), DisplayText }
            .Concat(TaskStatusPhrase.MetadataParts(Task)
                .Select(EditorInteractionCoordinator.BoundDisplayText))
            .Append(TaskStatusPhrase.StatusPhrase(Task)));

    public string AutomationHelpText => "Scrolls the editor to this task's line.";

    /// <summary>Mac checkbox labels, verbatim.</summary>
    public string CheckboxLabel =>
        Completed ? "Mark incomplete" : "Mark complete";

    public string CheckboxHelpText => "Toggles the task between open and done.";
}

/// <summary>One flat outline row (mac's OutlineSidebar list is flat —
/// heading NAVIGATION belongs to W3-1's chords, this leaf is the
/// nav-utility feature).</summary>
internal sealed class OutlineRowViewModel
{
    public OutlineRowViewModel(Heading heading)
    {
        // The exact-data/bounded-display split (round 15): AnchorId
        // stays verbatim — activation resolves by it — while Text is
        // what the row RENDERS and the landing announcement speaks,
        // so a megabyte heading cannot become a megabyte TextBlock
        // or UIA name.
        Level = heading.Level;
        Text = EditorInteractionCoordinator.BoundDisplayText(heading.Text);
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
