// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.IO;
using System.Text;
using System.Threading;
using System.Xml;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SkiaSharp;
using Svg;
using Svg.Model;
using Svg.Model.Services;
using Svg.Skia;
using uniffi.slate_uniffi;

namespace SlateWindows;

internal sealed record EditorNavigationRequest(
    string Path,
    LinkAnchor? Anchor,
    string? ResolvedAnchorText);

internal enum EditorInteractionOrigin
{
    Pointer,
    Keyboard,
}

internal sealed class EditorPreferencesViewModel : BindableBase, IDisposable
{
    internal const double ActualFontSize = 14;
    internal const double MinimumFontSize = 8;
    internal const double MaximumFontSize = 40;

    private readonly Action<A11yEvent> _announce;
    private readonly IEditorSpellingService _spellingService;
    private double _fontSize = ActualFontSize;
    private bool _isSpellCheckEnabled;

    public EditorPreferencesViewModel(
        Action<A11yEvent>? announce = null,
        IEditorSpellingService? spellingService = null)
    {
        _announce = announce ?? (_ => { });

        _spellingService = spellingService ?? WindowsEditorSpellingService.CreateForCurrentUser();
        ActualSizeCommand = new RelayCommand(
            _ =>
            {
                if (FontSize == ActualFontSize)
                {
                    _announce(new A11yEvent.HostComposed(
                        $"Editor text size already {ActualFontSize:0} points.",
                        A11yPriority.Medium));
                }
                else
                {
                    FontSize = ActualFontSize;
                }
            },
            _ => true);
        ToggleSpellCheckCommand = new RelayCommand(
            _ =>
            {
                if (!_spellingService.IsAvailable)
                {
                    IsSpellCheckEnabled = false;
                    _announce(new A11yEvent.HostComposed(
                        "Windows spell checking is unavailable.",
                        A11yPriority.High));
                    return;
                }

                IsSpellCheckEnabled = !IsSpellCheckEnabled;
                _announce(new A11yEvent.SpellCheckToggled(IsSpellCheckEnabled));
            },
            _ => true);
        ZoomInCommand = new RelayCommand(
            _ => FontSize = Math.Min(MaximumFontSize, FontSize + 1),
            _ => FontSize < MaximumFontSize);
        ZoomOutCommand = new RelayCommand(
            _ => FontSize = Math.Max(MinimumFontSize, FontSize - 1),
            _ => FontSize > MinimumFontSize);
    }

    public double FontSize
    {
        get => _fontSize;
        set
        {
            double clamped = Math.Clamp(value, MinimumFontSize, MaximumFontSize);
            if (SetField(ref _fontSize, clamped))
            {
                ((RelayCommand)ZoomInCommand).RaiseCanExecuteChanged();
                ((RelayCommand)ZoomOutCommand).RaiseCanExecuteChanged();
                _announce(new A11yEvent.HostComposed(
                    $"Editor text size {clamped:0} points.",
                    A11yPriority.Medium));
            }
        }
    }

    public bool IsSpellCheckEnabled
    {
        get => _isSpellCheckEnabled;
        set => SetField(ref _isSpellCheckEnabled, value);
    }

    public ICommand ActualSizeCommand { get; }
    public ICommand ToggleSpellCheckCommand { get; }
    public ICommand ZoomInCommand { get; }
    public ICommand ZoomOutCommand { get; }

    internal IEditorSpellingService SpellingService => _spellingService;

    public void Dispose()
    {
        _spellingService.Dispose();
    }
}

/// <summary>
/// Discrete semantic actions for one Avalon editor. Classification, link
/// resolution, citations, embeds, tasks, and math-region boundaries all come
/// from slate-core. C# only maps those records to native input and popover UI.
/// </summary>
internal sealed class EditorInteractionCoordinator : BindableBase, IDisposable
{
    private readonly VaultSession _session;
    private readonly WorkspaceTabViewModel _tab;
    private readonly Action<EditorNavigationRequest> _navigate;
    private readonly Action<string> _activateTag;
    private readonly Action<A11yEvent> _announce;
    private bool _disposed;
    private bool _isPopoverOpen;
    private string _popoverTitle = string.Empty;
    private string _popoverBody = string.Empty;
    private string _popoverAutomationName = string.Empty;
    private ImageSource? _popoverImage;
    private string? _popoverSourcePath;
    private int? _hoveredCitationByteOffset;
    private uint? _pendingHoveredCitationByteOffset;
    private int? _pendingHoverUtf16;
    private int? _pendingCaretUtf16;
    private IReadOnlyList<EditorInteractionRange> _mathRanges = [];
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
    private readonly object _mathRefreshGate = new();
    private CancellationTokenSource? _mathRefreshDelay;
    private bool _mathWorkerRunning;
    private bool _mathRerunPending;
    private long _mathRangesRevision = -1;
    private bool _popoverFocusPending;
    private readonly object _artifactCacheGate = new();
    private bool _artifactCacheLoading;
    private bool _artifactCacheRerunPending;
    private int _artifactCacheGeneration;
    private ulong _artifactCacheSessionGeneration;
    private bool _artifactCacheSourceCurrent;
    private string? _artifactCachePath;
    private string? _artifactCacheHash;
    private string? _citationCachePath;
    private string? _citationCacheHash;
    private bool _citationCacheLoading;
    private bool _citationCacheRerunPending;
    private int _citationCacheGeneration;
    private ulong _citationCacheSessionGeneration;
    private bool _citationCacheSourceCurrent;
    private Dictionary<(uint Start, uint End, bool Embed), OutgoingLink> _linksBySpan = [];
    private Dictionary<uint, CitationPreview> _citationsByOffset = [];
    private Dictionary<uint, TaskItem> _tasksByLine = [];
    private TaskItem[] _tasksByCheckbox = [];
    private long _artifactCacheLoadCountForTests;
    private int _embedGeneration;
    private EditorEmbedPreviewNode? _popoverEmbedRoot;
    private string? _embedRequestKey;
    private string? _activeEmbedRequestKey;
    private int _pendingEmbedPreviewGeneration;
    private PendingEmbedPreview? _pendingEmbedPreview;
    private readonly DispatcherTimer _citationCloseTimer;

    public EditorInteractionCoordinator(
        VaultSession session,
        WorkspaceTabViewModel tab,
        Action<EditorNavigationRequest>? navigate = null,
        Action<string>? activateTag = null,
        Action<A11yEvent>? announce = null,
        bool startBackgroundWork = true)
    {
        _session = session;
        _tab = tab;
        _navigate = navigate ?? (_ => { });
        _activateTag = activateTag ?? (_ => { });
        _announce = announce ?? (_ => { });
        _citationCloseTimer = new DispatcherTimer(
            DispatcherPriority.Background,
            _dispatcher)
        {
            Interval = TimeSpan.FromSeconds(1),
        };
        _citationCloseTimer.Tick += CitationCloseTimer_Tick;
        ActivateAtCaretCommand = new RelayCommand(
            _ => ActivateAt(_tab.EditorCaretOffset, EditorInteractionOrigin.Keyboard),
            _ => _tab.IsMarkdown);
        PreviewEmbedCommand = new RelayCommand(
            _ => PreviewEmbedAt(_tab.EditorCaretOffset),
            _ => _tab.IsMarkdown);
        ClosePopoverCommand = new RelayCommand(
            _ => ClosePopover(requestFocus: true),
            _ => IsPopoverOpen);
        OpenPopoverSourceCommand = new RelayCommand(
            _ => OpenPopoverSource(),
            _ => IsPopoverOpen && !string.IsNullOrWhiteSpace(PopoverSourcePath));
        _tab.EditorSession!.HighlightInvalidated += EditorSession_HighlightInvalidated;
        if (startBackgroundWork)
        {
            QueueMathRefresh(TimeSpan.Zero);
            QueueArtifactCacheRefresh();
            QueueCitationCacheRefresh();
        }
    }

    public event EventHandler? FocusRequested;
    public event EventHandler? CaretNavigationRequested;
    public event EventHandler? PopoverFocusRequested;

    public bool IsPopoverOpen
    {
        get => _isPopoverOpen;
        set
        {
            if (SetField(ref _isPopoverOpen, value))
            {
                if (!value)
                {
                    _hoveredCitationByteOffset = null;
                    _embedGeneration++;
                    _embedRequestKey = null;
                    _activeEmbedRequestKey = null;
                    _popoverFocusPending = false;
                    PopoverEmbedRoot = null;
                }

                ((RelayCommand)ClosePopoverCommand).RaiseCanExecuteChanged();
                ((RelayCommand)OpenPopoverSourceCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public string PopoverTitle
    {
        get => _popoverTitle;
        private set => SetField(ref _popoverTitle, value);
    }

    public string PopoverBody
    {
        get => _popoverBody;
        private set => SetField(ref _popoverBody, value);
    }

    public string PopoverAutomationName
    {
        get => _popoverAutomationName;
        private set => SetField(ref _popoverAutomationName, value);
    }

    public ImageSource? PopoverImage
    {
        get => _popoverImage;
        private set
        {
            if (SetField(ref _popoverImage, value))
            {
                OnPropertyChanged(nameof(HasPopoverImage));
            }
        }
    }

    public bool HasPopoverImage => PopoverImage is not null;

    public EditorEmbedPreviewNode? PopoverEmbedRoot
    {
        get => _popoverEmbedRoot;
        private set
        {
            if (SetField(ref _popoverEmbedRoot, value))
            {
                OnPropertyChanged(nameof(HasPopoverEmbed));
            }
        }
    }

    public bool HasPopoverEmbed => PopoverEmbedRoot is not null;

    public string? PopoverSourcePath
    {
        get => _popoverSourcePath;
        private set
        {
            if (SetField(ref _popoverSourcePath, value))
            {
                OnPropertyChanged(nameof(CanOpenPopoverSource));
                ((RelayCommand)OpenPopoverSourceCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public bool CanOpenPopoverSource => !string.IsNullOrWhiteSpace(PopoverSourcePath);

    public ICommand ActivateAtCaretCommand { get; }
    public ICommand PreviewEmbedCommand { get; }
    public ICommand ClosePopoverCommand { get; }
    public ICommand OpenPopoverSourceCommand { get; }

    private long _mathRangeRefreshCountForTests;
    internal long MathRangeRefreshCountForTests =>
        Interlocked.Read(ref _mathRangeRefreshCountForTests);
    internal long ArtifactCacheLoadCountForTests =>
        Interlocked.Read(ref _artifactCacheLoadCountForTests);

    public bool ActivateAt(
        int utf16Offset,
        EditorInteractionOrigin origin = EditorInteractionOrigin.Keyboard)
    {
        ThrowIfDisposed();
        if (_tab.IsDirty && CachedTaskAt(utf16Offset, origin) is not null)
        {
            _announce(new A11yEvent.TaskToggleUnsaved(Path.GetFileName(_tab.Path)));
            return true;
        }
        if (_tab.IsDirty
            && TryGetActionableSpan(
                utf16Offset,
                includeRightEdge: origin is EditorInteractionOrigin.Keyboard,
                out EditorSemanticSpan? dirtySpan)
            && dirtySpan is
            {
                Kind: EditorSpanKind.Wikilink
                    or EditorSpanKind.Embed
                    or EditorSpanKind.Citation,
            })
        {
            AnnounceSaveBeforeInteraction();
            return true;
        }
        if (!EnsureMathRangesReady(announceWhenUnavailable:
                origin is EditorInteractionOrigin.Keyboard))
        {
            return true;
        }

        int clamped = Math.Clamp(utf16Offset, 0, _tab.EditorDocument!.TextLength);
        if (IsInsideMathRegion(clamped))
        {
            return false;
        }

        if (!TryGetActionableSpan(
                utf16Offset,
                includeRightEdge: origin is EditorInteractionOrigin.Keyboard,
                out EditorSemanticSpan? span)
            || span is null)
        {
            return TryToggleTaskAt(utf16Offset, origin);
        }

        if (span.Kind is EditorSpanKind.Tag)
        {
            string authored = _tab.EditorDocument!.GetText(span.StartUtf16, span.LengthUtf16);
            if (authored.Length <= 1 || authored[0] != '#')
            {
                return false;
            }

            // The core span owns recognition and boundaries. Removing its one
            // syntax marker is boundary marshalling, not a second tag parser.
            _activateTag(authored[1..]);
            return true;
        }

        if (span.Kind is EditorSpanKind.Citation)
        {
            _ = ShowCitation(
                span,
                announceWhenUnavailable: true,
                requestPopoverFocus: true);
            return true;
        }

        if (span.Kind is EditorSpanKind.Embed)
        {
            return PreviewEmbed(span);
        }

        if (span.Kind is EditorSpanKind.Wikilink)
        {
            return FollowWikilink(span);
        }

        return false;
    }

    public bool PreviewEmbedAt(int utf16Offset)
    {
        ThrowIfDisposed();
        if (_tab.IsDirty)
        {
            CancelPendingEmbedPreview();
            AnnounceSaveBeforeInteraction();
            return true;
        }

        if (_tab.EditorDocument is null
            || _tab.EditorSession is null
            || _tab.SavedContentHash is not { } savedHash)
        {
            CancelPendingEmbedPreview();
            AnnounceReloadBeforeInteraction();
            return true;
        }

        int clamped = Math.Clamp(utf16Offset, 0, _tab.EditorDocument.TextLength);
        int generation = ++_pendingEmbedPreviewGeneration;
        _pendingEmbedPreview = new PendingEmbedPreview(
            generation,
            clamped,
            _tab.Path,
            savedHash,
            _tab.EditorSession.Revision,
            _session.InteractionGeneration());
        return TryReplayPendingEmbedPreview() ?? true;
    }

    private bool? TryReplayPendingEmbedPreview()
    {
        PendingEmbedPreview? pending = _pendingEmbedPreview;
        if (pending is null || pending.Generation != _pendingEmbedPreviewGeneration)
        {
            return null;
        }

        if (_disposed
            || _tab.IsDirty
            || !string.Equals(_tab.Path, pending.Path, StringComparison.Ordinal)
            || !string.Equals(
                _tab.SavedContentHash,
                pending.SavedHash,
                StringComparison.Ordinal)
            || _tab.EditorSession?.Revision != pending.Revision
            || _session.InteractionGeneration() != pending.SessionGeneration)
        {
            CancelPendingEmbedPreview();
            return false;
        }

        if (_mathRangesRevision != pending.Revision)
        {
            QueueMathRefresh(TimeSpan.Zero);
            QueueArtifactCacheRefresh();
            return null;
        }

        bool artifactMatches =
            string.Equals(_artifactCachePath, pending.Path, StringComparison.Ordinal)
            && string.Equals(
                _artifactCacheHash,
                pending.SavedHash,
                StringComparison.Ordinal)
            && _artifactCacheSessionGeneration == pending.SessionGeneration;
        if (!artifactMatches)
        {
            QueueArtifactCacheRefresh();
            return null;
        }

        if (!_artifactCacheSourceCurrent)
        {
            CancelPendingEmbedPreview();
            AnnounceReloadBeforeInteraction();
            return true;
        }

        _pendingEmbedPreview = null;
        if (IsInsideMathRegion(pending.Utf16Offset))
        {
            _announce(new A11yEvent.NoEmbedAtCursor());
            return false;
        }

        if (TryGetActionableSpan(
                pending.Utf16Offset,
                includeRightEdge: true,
                out EditorSemanticSpan? span)
            && span is not null
            && span.Kind is EditorSpanKind.Embed)
        {
            return PreviewEmbed(span);
        }

        _announce(new A11yEvent.NoEmbedAtCursor());
        return false;
    }

    private void CancelPendingEmbedPreview()
    {
        _pendingEmbedPreviewGeneration++;
        _pendingEmbedPreview = null;
    }

    public void HoverAt(int utf16Offset)
    {
        ThrowIfDisposed();
        _pendingHoverUtf16 = utf16Offset;
        if (!EnsureMathRangesReady(announceWhenUnavailable: false))
        {
            return;
        }

        int clamped = Math.Clamp(utf16Offset, 0, _tab.EditorDocument!.TextLength);
        if (IsInsideMathRegion(clamped))
        {
            ClearCitationHover();
            return;
        }

        if (TryGetActionableSpan(
                utf16Offset,
                includeRightEdge: false,
                out EditorSemanticSpan? span)
            && span is not null
            && span.Kind is EditorSpanKind.Citation)
        {
            _pendingHoverUtf16 = null;
            _pendingHoveredCitationByteOffset = span.StartByte;
            _citationCloseTimer.Stop();
            if (_hoveredCitationByteOffset != checked((int)span.StartByte))
            {
                ShowCitation(
                    span,
                    announceWhenUnavailable: false,
                    requestPopoverFocus: false);
            }
            return;
        }

        ClearCitationHover();
    }

    internal void ClearCitationHover()
    {
        _pendingHoverUtf16 = null;
        _pendingHoveredCitationByteOffset = null;
        ReleaseCitationPopover();
    }

    private void CloseHoveredCitation()
    {
        _hoveredCitationByteOffset = null;
        if (IsPopoverOpen
            && PopoverAutomationName.StartsWith("Citation", StringComparison.Ordinal))
        {
            ClosePopover(requestFocus: false);
        }
    }

    private void CitationCloseTimer_Tick(object? sender, EventArgs e)
    {
        _citationCloseTimer.Stop();
        CloseHoveredCitation();
    }

    internal void HoldCitationPopoverOpen() => _citationCloseTimer.Stop();

    internal void ReleaseCitationPopover()
    {
        if (IsPopoverOpen
            && PopoverAutomationName.StartsWith("Citation", StringComparison.Ordinal))
        {
            _citationCloseTimer.Stop();
            _citationCloseTimer.Start();
        }
    }
    public bool TryConsumePendingCaret(out int utf16Offset)
    {
        if (_pendingCaretUtf16 is int pending)
        {
            _pendingCaretUtf16 = null;
            utf16Offset = pending;
            return true;
        }

        utf16Offset = 0;
        return false;
    }

    public void RequestCaret(int utf16Offset)
    {
        ThrowIfDisposed();
        int clamped = Math.Clamp(utf16Offset, 0, _tab.EditorDocument?.TextLength ?? 0);
        _pendingCaretUtf16 = clamped;
        _tab.EditorCaretOffset = clamped;
        CaretNavigationRequested?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _citationCloseTimer.Stop();
        lock (_mathRefreshGate)
        {
            _mathRefreshDelay?.Cancel();
            _mathRefreshDelay?.Dispose();
            _mathRefreshDelay = null;
            _mathRerunPending = false;
            _mathRanges = [];
            _mathRangesRevision = -1;
        }
        lock (_artifactCacheGate)
        {
            _artifactCacheGeneration++;
            _citationCacheGeneration++;
            _artifactCacheRerunPending = false;
            _citationCacheRerunPending = false;
            _artifactCachePath = null;
            _artifactCacheHash = null;
            _citationCachePath = null;
            _citationCacheHash = null;
            _artifactCacheSessionGeneration = 0;
            _artifactCacheSourceCurrent = false;
            _citationCacheSessionGeneration = 0;
            _citationCacheSourceCurrent = false;
            _linksBySpan = [];
            _citationsByOffset = [];
            _tasksByLine = [];
            _tasksByCheckbox = [];
        }
        _pendingHoverUtf16 = null;
        _pendingHoveredCitationByteOffset = null;
        CancelPendingEmbedPreview();
        _embedGeneration++;
        _embedRequestKey = null;
        _activeEmbedRequestKey = null;
        if (_tab.EditorSession is not null)
        {
            _tab.EditorSession.HighlightInvalidated -= EditorSession_HighlightInvalidated;
        }
    }

    public void CloseTransientUi()
    {
        CancelPendingEmbedPreview();
        ClosePopover(requestFocus: false);
    }

    internal void InvalidateExternalState()
    {
        lock (_artifactCacheGate)
        {
            _artifactCacheGeneration++;
            _citationCacheGeneration++;
            _artifactCacheRerunPending |= _artifactCacheLoading;
            _citationCacheRerunPending |= _citationCacheLoading;
            _artifactCachePath = null;
            _artifactCacheHash = null;
            _citationCachePath = null;
            _citationCacheHash = null;
            _artifactCacheSessionGeneration = 0;
            _artifactCacheSourceCurrent = false;
            _citationCacheSessionGeneration = 0;
            _citationCacheSourceCurrent = false;
            _linksBySpan = [];
            _citationsByOffset = [];
            _tasksByLine = [];
            _tasksByCheckbox = [];
        }
        _pendingHoverUtf16 = null;
        _pendingHoveredCitationByteOffset = null;
        CancelPendingEmbedPreview();
        _embedGeneration++;
        _embedRequestKey = null;
        _activeEmbedRequestKey = null;
        ClosePopover(requestFocus: false);
        QueueArtifactCacheRefresh();
        QueueCitationCacheRefresh();
    }
    private bool FollowWikilink(EditorSemanticSpan span)
    {
        if (_tab.IsDirty)
        {
            AnnounceSaveBeforeInteraction();
            return true;
        }
        if (!EnsureArtifactCacheReady(announceWhenUnavailable: true)
            || !RequireCurrentSavedNote())
        {
            return true;
        }

        OutgoingLink? link = LinkRecordFor(span, expectEmbed: false);
        if (link is null)
        {
            _announce(new A11yEvent.LinkUnresolved("link at cursor"));
            return true;
        }

        if (link.IsUnresolved || string.IsNullOrWhiteSpace(link.TargetPath))
        {
            _announce(new A11yEvent.LinkUnresolved(link.TargetRaw));
            return true;
        }

        _navigate(new EditorNavigationRequest(
            link.TargetPath,
            link.TargetAnchor,
            null));
        return true;
    }

    private bool PreviewEmbed(EditorSemanticSpan span)
    {
        if (_tab.IsDirty)
        {
            AnnounceSaveBeforeInteraction();
            return true;
        }
        if (!EnsureArtifactCacheReady(announceWhenUnavailable: true)
            || !RequireCurrentSavedNote())
        {
            return true;
        }

        OutgoingLink? link = LinkRecordFor(span, expectEmbed: true);
        if (link is null)
        {
            _announce(new A11yEvent.NoResolvedEmbedAtCursor());
            return true;
        }

        int sourceLine = _tab.EditorDocument!.GetLineByOffset(span.StartUtf16).LineNumber;
        string path = _tab.Path;
        string savedHash = _tab.SavedContentHash!;
        long revision = _tab.EditorSession!.Revision;
        ulong sessionGeneration = _session.InteractionGeneration();
        string requestKey =
            $"{path}:{savedHash}:{sessionGeneration}:{span.StartByte}:{span.EndByte}";
        if (string.Equals(_embedRequestKey, requestKey, StringComparison.Ordinal)
            || (IsPopoverOpen
                && string.Equals(
                    _activeEmbedRequestKey,
                    requestKey,
                    StringComparison.Ordinal)))
        {
            return true;
        }

        int generation = ++_embedGeneration;
        _embedRequestKey = requestKey;
        PopoverTitle = $"Loading embed preview — source line {sourceLine}";
        PopoverBody = "Resolving embedded content…";
        PopoverAutomationName =
            $"Loading embed preview for {link.TargetRaw}, source line {sourceLine}.";
        PopoverImage = null;
        PopoverEmbedRoot = null;
        PopoverSourcePath = null;
        OpenPopover();
        _ = Task.Run(() => ResolveEmbedPreview(
            generation,
            requestKey,
            path,
            savedHash,
            revision,
            sessionGeneration,
            sourceLine,
            link));
        return true;
    }
    private sealed record EmbedPreviewContent(
        string Title,
        string Body,
        string? SourcePath,
        ImageSource? Image,
        EditorEmbedPreviewNode Root);

    private void ResolveEmbedPreview(
        int generation,
        string requestKey,
        string path,
        string savedHash,
        long revision,
        ulong sessionGeneration,
        int sourceLine,
        OutgoingLink link)
    {
        EmbedPreviewContent? content = null;
        try
        {
            EmbedPreviewResolution preview = _session.ResolveEmbedPreview(
                path,
                ComposeAnchoredTarget(link),
                link.DisplayText);
            content = BuildEmbedPreview(preview.Resolution, preview.Truncated);
        }
        catch (Exception exception) when (
            exception is not OutOfMemoryException
                and not StackOverflowException
                and not AccessViolationException)
        {
        }

        if (_dispatcher.HasShutdownStarted || _dispatcher.HasShutdownFinished)
        {
            return;
        }

        _dispatcher.BeginInvoke(
            DispatcherPriority.Background,
            new Action(() => PublishEmbedPreview(
                generation,
                requestKey,
                path,
                savedHash,
                revision,
                sessionGeneration,
                sourceLine,
                link.TargetRaw,
                content)));
    }

    private void PublishEmbedPreview(
        int generation,
        string requestKey,
        string path,
        string savedHash,
        long revision,
        ulong sessionGeneration,
        int sourceLine,
        string targetRaw,
        EmbedPreviewContent? content)
    {
        if (_disposed
            || generation != _embedGeneration
            || !string.Equals(_embedRequestKey, requestKey, StringComparison.Ordinal)
            || !string.Equals(_tab.Path, path, StringComparison.Ordinal)
            || !string.Equals(_tab.SavedContentHash, savedHash, StringComparison.Ordinal)
            || _tab.EditorSession!.Revision != revision
            || _session.InteractionGeneration() != sessionGeneration)
        {
            return;
        }

        _embedRequestKey = null;
        _activeEmbedRequestKey = requestKey;
        if (content is null)
        {
            PopoverTitle = $"Embed preview unavailable — source line {sourceLine}";
            PopoverBody = "The embedded content could not be resolved.";
            PopoverAutomationName =
                $"Embed preview for {targetRaw}, source line {sourceLine}, unavailable.";
            PopoverImage = null;
            PopoverEmbedRoot = null;
            PopoverSourcePath = null;
            return;
        }

        PopoverTitle = $"{content.Title} — source line {sourceLine}";
        PopoverBody = content.Body;
        PopoverAutomationName =
            $"Embed preview for {targetRaw}, source line {sourceLine}. {content.Title}";
        PopoverImage = content.Image;
        PopoverEmbedRoot = content.Root;
        PopoverSourcePath = content.SourcePath;
    }

    private static EmbedPreviewContent BuildEmbedPreview(
        EmbedResolution resolution,
        bool truncated)
    {
        EditorEmbedPreviewNode root = BuildEmbedNode(resolution, depth: 0);
        string body = string.Empty;
        if (truncated)
        {
            body += PreviewTruncatedMessage;
            root = root with
            {
                Parts = root.Parts
                    .Append(new EditorEmbedPreviewPart(
                        PreviewTruncatedMessage.TrimStart(),
                        null))
                    .ToArray(),
            };
        }

        return new EmbedPreviewContent(
            root.Title,
            body,
            root.SourcePath,
            root.Image,
            root);
    }

    private static EditorEmbedPreviewNode BuildEmbedNode(
        EmbedResolution resolution,
        int depth)
    {
        if (depth >= 3)
        {
            const string message =
                "Embed depth limit reached. Open source to view deeper nested content.";
            return new EditorEmbedPreviewNode(
                message,
                [],
                null,
                null,
                IsDisclosure: false,
                InitiallyExpanded: false,
                IsWarning: true);
        }

        return resolution switch
        {
            EmbedResolution.FullNote full => new EditorEmbedPreviewNode(
                $"Embedded note: {full.TargetPath}",
                BuildEmbedParts(full.Text, full.Nested, depth + 1),
                null,
                full.TargetPath,
                IsDisclosure: true,
                InitiallyExpanded: depth == 0,
                IsWarning: false),
            EmbedResolution.Section section => new EditorEmbedPreviewNode(
                $"Embedded section: {section.Heading} from {section.TargetPath}",
                BuildEmbedParts(section.Text, section.Nested, depth + 1),
                null,
                section.TargetPath,
                IsDisclosure: true,
                InitiallyExpanded: depth == 0,
                IsWarning: false),
            EmbedResolution.Block block => new EditorEmbedPreviewNode(
                $"Embedded block from {block.TargetPath}",
                [new EditorEmbedPreviewPart(BoundPreviewText(block.Text), null)],
                null,
                block.TargetPath,
                IsDisclosure: true,
                InitiallyExpanded: depth == 0,
                IsWarning: false),
            EmbedResolution.Image image => BuildImageNode(image, depth),
            EmbedResolution.Unresolved unresolved => new EditorEmbedPreviewNode(
                Describe(unresolved.Reason),
                [],
                null,
                null,
                IsDisclosure: false,
                InitiallyExpanded: false,
                IsWarning: true),
            _ => new EditorEmbedPreviewNode(
                "The embed could not be resolved.",
                [],
                null,
                null,
                IsDisclosure: false,
                InitiallyExpanded: false,
                IsWarning: true),
        };
    }

    private static EditorEmbedPreviewNode BuildImageNode(
        EmbedResolution.Image image,
        int depth)
    {
        ImageSource? decoded = DecodeImage(image.Bytes, image.Mime);
        string body = decoded is null
            ? $"Could not decode image. MIME: {image.Mime}. "
                + "The file may be corrupt or use an unsupported codec."
            : image.Mime;
        return new EditorEmbedPreviewNode(
            ImageTitle(image.TargetPath, image.Alt),
            [new EditorEmbedPreviewPart(body, null)],
            decoded,
            image.TargetPath,
            IsDisclosure: true,
            InitiallyExpanded: depth == 0,
            IsWarning: decoded is null);
    }

    private static IReadOnlyList<EditorEmbedPreviewPart> BuildEmbedParts(
        string parentText,
        IReadOnlyList<NestedEmbed> nested,
        int childDepth)
    {
        if (nested.Count == 0)
        {
            return [new EditorEmbedPreviewPart(BoundPreviewText(parentText), null)];
        }

        var parts = new List<EditorEmbedPreviewPart>();
        byte[] source = Encoding.UTF8.GetBytes(parentText);
        int cursor = 0;
        foreach (NestedEmbed item in nested.OrderBy(item => item.ByteOffsetInParent))
        {
            int offset = checked((int)item.ByteOffsetInParent);
            if (offset < cursor || offset > source.Length)
            {
                continue;
            }
            if (offset > cursor)
            {
                parts.Add(new EditorEmbedPreviewPart(
                    Encoding.UTF8.GetString(source, cursor, offset - cursor),
                    null));
            }
            parts.Add(new EditorEmbedPreviewPart(
                null,
                BuildEmbedNode(item.Resolution, childDepth)));
            cursor = Math.Clamp(
                checked((int)item.ByteEndInParent),
                offset,
                source.Length);
        }
        if (cursor < source.Length)
        {
            parts.Add(new EditorEmbedPreviewPart(
                Encoding.UTF8.GetString(source, cursor, source.Length - cursor),
                null));
        }
        return parts;
    }
    private bool ShowCitation(
        EditorSemanticSpan span,
        bool announceWhenUnavailable,
        bool requestPopoverFocus)
    {
        if (_tab.IsDirty)
        {
            if (announceWhenUnavailable)
            {
                AnnounceSaveBeforeInteraction();
            }
            return false;
        }
        if (!EnsureCitationCacheReady(announceWhenUnavailable)
            || !RequireCurrentSavedNote(announceWhenUnavailable))
        {
            return false;
        }

        CitationPreview? preview = _citationsByOffset.GetValueOrDefault(span.StartByte);
        if (preview is null)
        {
            if (announceWhenUnavailable)
            {
                _announce(new A11yEvent.CitationNotLoaded());
            }
            return false;
        }

        _pendingHoveredCitationByteOffset = null;
        PresentCitation(span.StartByte, preview, requestPopoverFocus);
        return true;
    }

    private void PresentCitation(
        uint byteOffset,
        CitationPreview preview,
        bool requestPopoverFocus)
    {
        _hoveredCitationByteOffset = checked((int)byteOffset);
        PopoverTitle = "Citation";
        PopoverBody = preview.Body;
        PopoverAutomationName = preview.Speech.StartsWith(
            "Citation",
            StringComparison.OrdinalIgnoreCase)
                ? preview.Speech
                : $"Citation. {preview.Speech}";
        PopoverImage = null;
        PopoverEmbedRoot = null;
        PopoverSourcePath = null;
        OpenPopover(requestPopoverFocus);
    }

    private bool TryToggleTaskAt(int utf16Offset, EditorInteractionOrigin origin)
    {
        if (_tab.EditorDocument is null || _tab.EditorSession is null)
        {
            return false;
        }

        TaskItem? cachedTask = CachedTaskAt(utf16Offset, origin);
        if (_tab.IsDirty && cachedTask is not null)
        {
            _announce(new A11yEvent.TaskToggleUnsaved(Path.GetFileName(_tab.Path)));
            return true;
        }
        if (!EnsureArtifactCacheReady(announceWhenUnavailable: true))
        {
            return true;
        }

        TaskItem? task = cachedTask ?? CachedTaskAt(utf16Offset, origin);
        if (task is null)
        {
            return false;
        }
        if (!RequireCurrentSavedNote())
        {
            return true;
        }

        return _tab.ToggleTask(task, _announce);
    }

    private TaskItem? CachedTaskAt(int utf16Offset, EditorInteractionOrigin origin)
    {
        if (_tab.EditorDocument is null || _tab.EditorSession is null)
        {
            return null;
        }

        int clamped = Math.Clamp(utf16Offset, 0, _tab.EditorDocument.TextLength);
        if (origin is EditorInteractionOrigin.Pointer)
        {
            uint byteOffset = _tab.EditorSession.Utf16ToByte(clamped);
            return TaskAtCheckboxByte(byteOffset);
        }

        int line = _tab.EditorDocument.GetLineByOffset(clamped).LineNumber;
        return _tasksByLine.GetValueOrDefault(checked((uint)line));
    }
    private TaskItem? TaskAtCheckboxByte(uint byteOffset)
    {
        int low = 0;
        int high = _tasksByCheckbox.Length;
        while (low < high)
        {
            int middle = low + ((high - low) / 2);
            if (_tasksByCheckbox[middle].CheckboxStartByte <= byteOffset)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        int candidate = low - 1;
        return candidate >= 0
            && byteOffset < _tasksByCheckbox[candidate].CheckboxEndByte
                ? _tasksByCheckbox[candidate]
                : null;
    }
    private bool TryGetActionableSpan(
        int utf16Offset,
        bool includeRightEdge,
        out EditorSemanticSpan? actionable)
    {
        actionable = null;
        AvalonDocumentBufferSession? editorSession = _tab.EditorSession;
        if (editorSession is null || _tab.EditorDocument is null)
        {
            return false;
        }

        int clamped = Math.Clamp(utf16Offset, 0, _tab.EditorDocument.TextLength);
        EditorHighlightWindow window = WindowContaining(clamped, includeRightEdge);
        EditorSemanticSpan[] containing = window.Spans
            .Where(span => span.StartUtf16 <= clamped
                && (clamped < span.StartUtf16 + span.LengthUtf16
                    || (includeRightEdge
                        && span.LengthUtf16 > 0
                        && clamped == span.StartUtf16 + span.LengthUtf16)))
            .OrderByDescending(span => clamped < span.StartUtf16 + span.LengthUtf16)
            .ToArray();
        if (containing.Any(span => span.Kind is EditorSpanKind.CodeFence))
        {
            return false;
        }

        actionable = containing.FirstOrDefault(span =>
            span.Kind is EditorSpanKind.Wikilink
                or EditorSpanKind.Embed
                or EditorSpanKind.Tag
                or EditorSpanKind.Citation);
        return actionable is not null;
    }

    private EditorHighlightWindow WindowContaining(int utf16Offset, bool includeRightEdge)
    {
        AvalonDocumentBufferSession editorSession = _tab.EditorSession!;
        EditorHighlightWindow? retained = editorSession.LatestHighlightWindow;
        if (retained is not null
            && retained.Revision == editorSession.Revision
            && retained.AppliedStartUtf16 <= utf16Offset
            && (utf16Offset < retained.AppliedEndUtf16
                || (includeRightEdge
                    && retained.AppliedStartUtf16 < utf16Offset
                    && utf16Offset == retained.AppliedEndUtf16)))
        {
            return retained;
        }

        int start = includeRightEdge ? Math.Max(0, utf16Offset - 1) : utf16Offset;
        int end = Math.Min(_tab.EditorDocument!.TextLength, utf16Offset + 1);
        return editorSession.InspectInRange(start, end);
    }

    private bool IsInsideMathRegion(int utf16Offset)
    {
        AvalonDocumentBufferSession session = _tab.EditorSession!;
        uint byteOffset = session.Utf16ToByte(utf16Offset);
        int low = 0;
        int high = _mathRanges.Count;
        while (low < high)
        {
            int middle = low + ((high - low) / 2);
            if (_mathRanges[middle].StartByte <= byteOffset)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        int candidate = low - 1;
        return candidate >= 0 && byteOffset < _mathRanges[candidate].EndByte;
    }

    private bool EnsureMathRangesReady(bool announceWhenUnavailable)
    {
        if (_mathRangesRevision == _tab.EditorSession!.Revision)
        {
            return true;
        }

        QueueMathRefresh(TimeSpan.Zero);
        QueueArtifactCacheRefresh();
        QueueCitationCacheRefresh();
        if (announceWhenUnavailable)
        {
            _announce(new A11yEvent.HostComposed(
                "Editor interactions are still updating; try again.",
                A11yPriority.Medium));
        }

        return false;
    }

    private void QueueMathRefresh(TimeSpan delay)
    {
        CancellationTokenSource pending;
        lock (_mathRefreshGate)
        {
            if (_disposed)
            {
                return;
            }

            _mathRefreshDelay?.Cancel();
            _mathRefreshDelay?.Dispose();
            pending = new CancellationTokenSource();
            _mathRefreshDelay = pending;
        }

        _ = WaitAndStartMathRefreshAsync(delay, pending);
    }

    private async Task WaitAndStartMathRefreshAsync(
        TimeSpan delay,
        CancellationTokenSource pending)
    {
        try
        {
            await Task.Delay(delay, pending.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        lock (_mathRefreshGate)
        {
            if (_disposed || !ReferenceEquals(_mathRefreshDelay, pending))
            {
                return;
            }

            _mathRefreshDelay = null;
            if (_mathWorkerRunning)
            {
                _mathRerunPending = true;
                pending.Dispose();
                return;
            }

            _mathWorkerRunning = true;
        }

        pending.Dispose();
        _ = Task.Run(RunMathRefreshWorker);
    }

    private void RunMathRefreshWorker()
    {
        while (true)
        {
            AvalonDocumentBufferSession? session = _tab.EditorSession;
            long revisionBefore = -1;
            long revisionAfter = -1;
            IReadOnlyList<EditorInteractionRange>? ranges = null;
            try
            {
                if (session is not null)
                {
                    revisionBefore = session.Revision;
                    ranges = session.EditorInteractionMathRanges();
                    revisionAfter = session.Revision;
                    Interlocked.Increment(ref _mathRangeRefreshCountForTests);
                }
            }
            catch (Exception exception) when (
                exception is not OutOfMemoryException
                    and not StackOverflowException
                    and not AccessViolationException)
            {
            }

            if (ranges is not null
                && !_dispatcher.HasShutdownStarted
                && !_dispatcher.HasShutdownFinished)
            {
                _dispatcher.BeginInvoke(
                    DispatcherPriority.Background,
                    new Action(() => PublishMathRanges(
                        revisionBefore,
                        revisionAfter,
                        ranges)));
            }

            lock (_mathRefreshGate)
            {
                if (_disposed)
                {
                    _mathWorkerRunning = false;
                    return;
                }

                if (_mathRerunPending)
                {
                    _mathRerunPending = false;
                    continue;
                }

                _mathWorkerRunning = false;
                return;
            }
        }
    }

    private void PublishMathRanges(
        long revisionBefore,
        long revisionAfter,
        IReadOnlyList<EditorInteractionRange> ranges)
    {
        if (_disposed
            || revisionBefore != revisionAfter
            || revisionAfter != _tab.EditorSession!.Revision)
        {
            return;
        }

        _mathRanges = ranges;
        _mathRangesRevision = revisionAfter;
        if (_pendingHoverUtf16 is int pendingHover)
        {
            HoverAt(pendingHover);
        }
        _ = TryReplayPendingEmbedPreview();
    }

    internal void RefreshMathRangesForTests()
    {
        ThrowIfDisposed();
        _dispatcher.VerifyAccess();
        lock (_mathRefreshGate)
        {
            _mathRefreshDelay?.Cancel();
            _mathRefreshDelay?.Dispose();
            _mathRefreshDelay = null;
        }

        long revision = _tab.EditorSession!.Revision;
        IReadOnlyList<EditorInteractionRange> ranges =
            _tab.EditorSession.EditorInteractionMathRanges();
        Interlocked.Increment(ref _mathRangeRefreshCountForTests);
        PublishMathRanges(revision, _tab.EditorSession.Revision, ranges);
    }
    private sealed record CitationPreview(string Body, string Speech);

    private sealed record PendingEmbedPreview(
        int Generation,
        int Utf16Offset,
        string Path,
        string SavedHash,
        long Revision,
        ulong SessionGeneration);

    private sealed record InteractionArtifactSnapshot(
        Dictionary<(uint Start, uint End, bool Embed), OutgoingLink> Links,
        Dictionary<uint, TaskItem> TasksByLine,
        TaskItem[] TasksByCheckbox,
        ulong SessionGeneration,
        bool SourceCurrent);

    private sealed record CitationArtifactSnapshot(
        Dictionary<uint, CitationPreview> Previews,
        ulong SessionGeneration,
        bool SourceCurrent);

    private void QueueArtifactCacheRefresh()
    {
        string? savedHash = _tab.SavedContentHash;
        if (savedHash is null)
        {
            return;
        }

        string path = _tab.Path;
        ulong sessionGeneration = _session.InteractionGeneration();
        int generation;
        lock (_artifactCacheGate)
        {
            if (_disposed
                || (string.Equals(_artifactCachePath, path, StringComparison.Ordinal)
                    && string.Equals(_artifactCacheHash, savedHash, StringComparison.Ordinal)
                    && _artifactCacheSessionGeneration == sessionGeneration))
            {
                return;
            }
            if (_artifactCacheLoading)
            {
                _artifactCacheRerunPending = true;
                return;
            }

            _artifactCacheLoading = true;
            generation = _artifactCacheGeneration;
        }

        _ = Task.Run(() => LoadArtifactCache(generation, path, savedHash));
    }

    private void LoadArtifactCache(int generation, string path, string savedHash)
    {
        InteractionArtifactSnapshot? snapshot = null;
        try
        {
            ulong generationBefore = _session.InteractionGeneration();
            FileMetadata? metadata = _session.GetFileMetadata(path);
            IReadOnlyList<TaskItem> tasks = _session.TasksForFile(path);
            Dictionary<(uint Start, uint End, bool Embed), OutgoingLink> links =
                _session.OutgoingLinks(path).ToDictionary(
                    link => (link.SpanStart, link.SpanEnd, link.IsEmbed));
            ulong generationAfter = _session.InteractionGeneration();
            snapshot = new InteractionArtifactSnapshot(
                links,
                tasks.ToDictionary(task => task.Line),
                tasks.OrderBy(task => task.CheckboxStartByte).ToArray(),
                generationAfter,
                generationBefore == generationAfter
                    && metadata is not null
                    && string.Equals(metadata.ContentHash, savedHash, StringComparison.Ordinal));
            Interlocked.Increment(ref _artifactCacheLoadCountForTests);
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
                new Action(() => PublishArtifactCache(generation, path, savedHash, snapshot)));
        }
    }

    private void PublishArtifactCache(
        int generation,
        string path,
        string savedHash,
        InteractionArtifactSnapshot? snapshot)
    {
        bool rerun;
        bool published = false;
        lock (_artifactCacheGate)
        {
            _artifactCacheLoading = false;
            bool currentRequest = generation == _artifactCacheGeneration
                && !_disposed
                && snapshot is not null
                && string.Equals(_tab.Path, path, StringComparison.Ordinal)
                && string.Equals(_tab.SavedContentHash, savedHash, StringComparison.Ordinal)
                && snapshot.SessionGeneration == _session.InteractionGeneration();
            if (currentRequest)
            {
                _artifactCachePath = path;
                _artifactCacheHash = savedHash;
                _artifactCacheSessionGeneration = snapshot!.SessionGeneration;
                _artifactCacheSourceCurrent = snapshot.SourceCurrent;
                _linksBySpan = snapshot.SourceCurrent ? snapshot.Links : [];
                _tasksByLine = snapshot.SourceCurrent ? snapshot.TasksByLine : [];
                _tasksByCheckbox = snapshot.SourceCurrent ? snapshot.TasksByCheckbox : [];
                published = true;
            }

            rerun = !_disposed
                && (_artifactCacheRerunPending
                    || generation != _artifactCacheGeneration
                    || !string.Equals(_tab.Path, path, StringComparison.Ordinal)
                    || !string.Equals(_tab.SavedContentHash, savedHash, StringComparison.Ordinal)
                    || (snapshot is not null
                        && snapshot.SessionGeneration != _session.InteractionGeneration()));
            _artifactCacheRerunPending = false;
        }

        if (rerun)
        {
            QueueArtifactCacheRefresh();
        }
        if (published)
        {
            _ = TryReplayPendingEmbedPreview();
        }
    }

    private void QueueCitationCacheRefresh()
    {
        string? savedHash = _tab.SavedContentHash;
        if (savedHash is null)
        {
            return;
        }

        string path = _tab.Path;
        ulong sessionGeneration = _session.InteractionGeneration();
        int generation;
        lock (_artifactCacheGate)
        {
            if (_disposed
                || (string.Equals(_citationCachePath, path, StringComparison.Ordinal)
                    && string.Equals(_citationCacheHash, savedHash, StringComparison.Ordinal)
                    && _citationCacheSessionGeneration == sessionGeneration))
            {
                return;
            }
            if (_citationCacheLoading)
            {
                _citationCacheRerunPending = true;
                return;
            }

            _citationCacheLoading = true;
            generation = _citationCacheGeneration;
        }

        _ = Task.Run(() => LoadCitationCache(generation, path, savedHash));
    }

    private void LoadCitationCache(int generation, string path, string savedHash)
    {
        CitationArtifactSnapshot? snapshot = null;
        try
        {
            ulong generationBefore = _session.InteractionGeneration();
            FileMetadata? metadata = _session.GetFileMetadata(path);
            Dictionary<uint, CitationPreview> previews =
                BuildCitationPreviews(_session.ListCitationsInFile(path));
            ulong generationAfter = _session.InteractionGeneration();
            snapshot = new CitationArtifactSnapshot(
                previews,
                generationAfter,
                generationBefore == generationAfter
                    && metadata is not null
                    && string.Equals(metadata.ContentHash, savedHash, StringComparison.Ordinal));
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
                new Action(() => PublishCitationCache(generation, path, savedHash, snapshot)));
        }
    }

    private void PublishCitationCache(
        int generation,
        string path,
        string savedHash,
        CitationArtifactSnapshot? snapshot)
    {
        bool rerun;
        bool published = false;
        lock (_artifactCacheGate)
        {
            _citationCacheLoading = false;
            bool currentRequest = generation == _citationCacheGeneration
                && !_disposed
                && snapshot is not null
                && string.Equals(_tab.Path, path, StringComparison.Ordinal)
                && string.Equals(_tab.SavedContentHash, savedHash, StringComparison.Ordinal)
                && snapshot.SessionGeneration == _session.InteractionGeneration();
            if (currentRequest)
            {
                _citationCachePath = path;
                _citationCacheHash = savedHash;
                _citationCacheSessionGeneration = snapshot!.SessionGeneration;
                _citationCacheSourceCurrent = snapshot.SourceCurrent;
                _citationsByOffset = snapshot.SourceCurrent ? snapshot.Previews : [];
                published = snapshot.SourceCurrent;
            }

            rerun = !_disposed
                && (_citationCacheRerunPending
                    || generation != _citationCacheGeneration
                    || !string.Equals(_tab.Path, path, StringComparison.Ordinal)
                    || !string.Equals(_tab.SavedContentHash, savedHash, StringComparison.Ordinal)
                    || (snapshot is not null
                        && snapshot.SessionGeneration != _session.InteractionGeneration()));
            _citationCacheRerunPending = false;
        }

        if (published
            && _pendingHoveredCitationByteOffset is uint pending
            && _citationsByOffset.TryGetValue(pending, out CitationPreview? preview)
            && RequireCurrentSavedNote(announceWhenUnavailable: false))
        {
            _pendingHoveredCitationByteOffset = null;
            PresentCitation(pending, preview, requestPopoverFocus: false);
        }
        if (rerun)
        {
            QueueCitationCacheRefresh();
        }
    }
    private bool EnsureArtifactCacheReady(bool announceWhenUnavailable)
    {
        ulong sessionGeneration = _session.InteractionGeneration();
        bool matches = _tab.SavedContentHash is { } savedHash
            && string.Equals(_artifactCachePath, _tab.Path, StringComparison.Ordinal)
            && string.Equals(_artifactCacheHash, savedHash, StringComparison.Ordinal)
            && _artifactCacheSessionGeneration == sessionGeneration;
        if (matches)
        {
            if (_artifactCacheSourceCurrent)
            {
                return true;
            }
            if (announceWhenUnavailable)
            {
                AnnounceReloadBeforeInteraction();
            }
            return false;
        }

        QueueArtifactCacheRefresh();
        if (announceWhenUnavailable)
        {
            _announce(new A11yEvent.HostComposed(
                "Editor interactions are still loading; try again.",
                A11yPriority.Medium));
        }
        return false;
    }

    private bool EnsureCitationCacheReady(bool announceWhenUnavailable)
    {
        ulong sessionGeneration = _session.InteractionGeneration();
        bool matches = _tab.SavedContentHash is { } savedHash
            && string.Equals(_citationCachePath, _tab.Path, StringComparison.Ordinal)
            && string.Equals(_citationCacheHash, savedHash, StringComparison.Ordinal)
            && _citationCacheSessionGeneration == sessionGeneration;
        if (matches)
        {
            if (_citationCacheSourceCurrent)
            {
                return true;
            }
            if (announceWhenUnavailable)
            {
                AnnounceReloadBeforeInteraction();
            }
            return false;
        }

        QueueCitationCacheRefresh();
        if (announceWhenUnavailable)
        {
            _announce(new A11yEvent.HostComposed(
                "Citations are still loading; try again.",
                A11yPriority.Medium));
        }
        return false;
    }
    internal void RefreshArtifactCacheForTests()
    {
        ThrowIfDisposed();
        _dispatcher.VerifyAccess();
        string savedHash = _tab.SavedContentHash
            ?? throw new InvalidOperationException("The test tab has no saved hash.");
        ulong sessionGeneration = _session.InteractionGeneration();
        FileMetadata? metadata = _session.GetFileMetadata(_tab.Path);
        bool sourceCurrent = metadata is not null
            && string.Equals(metadata.ContentHash, savedHash, StringComparison.Ordinal);
        IReadOnlyList<TaskItem> tasks = _session.TasksForFile(_tab.Path);
        var snapshot = new InteractionArtifactSnapshot(
            _session.OutgoingLinks(_tab.Path).ToDictionary(
                link => (link.SpanStart, link.SpanEnd, link.IsEmbed)),
            tasks.ToDictionary(task => task.Line),
            tasks.OrderBy(task => task.CheckboxStartByte).ToArray(),
            sessionGeneration,
            sourceCurrent);
        Interlocked.Increment(ref _artifactCacheLoadCountForTests);
        PublishArtifactCache(_artifactCacheGeneration, _tab.Path, savedHash, snapshot);
        PublishCitationCache(
            _citationCacheGeneration,
            _tab.Path,
            savedHash,
            new CitationArtifactSnapshot(
                BuildCitationPreviews(_session.ListCitationsInFile(_tab.Path)),
                sessionGeneration,
                sourceCurrent));
    }
    private OutgoingLink? LinkRecordFor(EditorSemanticSpan selected, bool expectEmbed) =>
        _linksBySpan.GetValueOrDefault(
            (selected.StartByte, selected.EndByte, expectEmbed));
    private Dictionary<uint, CitationPreview> BuildCitationPreviews(
        IReadOnlyList<CitationReference> references)
    {
        string? styleId = null;
        try
        {
            string? defaultStyle = _session.CitationsPrefs().DefaultStyle;
            if (!string.IsNullOrWhiteSpace(defaultStyle))
            {
                styleId = Path.GetFileNameWithoutExtension(defaultStyle);
            }
        }
        catch (VaultException)
        {
        }

        var previews = new Dictionary<uint, CitationPreview>(references.Count);
        foreach (CitationReference reference in references)
        {
            string body = CitationFallback(reference);
            string speech = body;
            if (styleId is not null)
            {
                try
                {
                    RenderedCitation rendered = _session.RenderCitation(reference, styleId);
                    body = rendered.VisualText;
                    speech = string.IsNullOrWhiteSpace(rendered.SpeechText)
                        ? body
                        : rendered.SpeechText;
                }
                catch (VaultException)
                {
                }
            }

            previews[reference.ByteOffset] = new CitationPreview(body, speech);
        }
        return previews;
    }
    private bool RequireCurrentSavedNote(bool announceWhenUnavailable = true)
    {
        if (_tab.IsDirty)
        {
            if (announceWhenUnavailable)
            {
                AnnounceSaveBeforeInteraction();
            }
            return false;
        }

        ulong generation = _session.InteractionGeneration();
        bool artifactMatches = _tab.SavedContentHash is { } savedHash
            && string.Equals(_artifactCachePath, _tab.Path, StringComparison.Ordinal)
            && string.Equals(_artifactCacheHash, savedHash, StringComparison.Ordinal)
            && _artifactCacheSessionGeneration == generation;
        bool citationMatches = _tab.SavedContentHash is { } citationSavedHash
            && string.Equals(_citationCachePath, _tab.Path, StringComparison.Ordinal)
            && string.Equals(_citationCacheHash, citationSavedHash, StringComparison.Ordinal)
            && _citationCacheSessionGeneration == generation;
        if ((artifactMatches && _artifactCacheSourceCurrent)
            || (citationMatches && _citationCacheSourceCurrent))
        {
            return true;
        }

        if (announceWhenUnavailable)
        {
            AnnounceReloadBeforeInteraction();
        }
        return false;
    }

    private void AnnounceSaveBeforeInteraction() =>
        _announce(new A11yEvent.HostComposed(
            $"Save {Path.GetFileName(_tab.Path)} before using this interaction.",
            A11yPriority.High));

    private void AnnounceReloadBeforeInteraction() =>
        _announce(new A11yEvent.HostComposed(
            $"Reload {Path.GetFileName(_tab.Path)} before using this interaction.",
            A11yPriority.High));
    private static string ComposeAnchoredTarget(OutgoingLink link)
    {
        if (link.TargetAnchor is null)
        {
            return link.TargetRaw;
        }

        string marker = string.Equals(
            link.TargetAnchor.Kind,
            "block",
            StringComparison.Ordinal)
            ? "^"
            : "#";
        return link.TargetRaw + marker + link.TargetAnchor.Text;
    }

    private static string CitationFallback(CitationReference reference)
    {
        string keys = string.Join(", ", reference.Citations.Select(item => item.Key));
        return keys.Length == 0 ? reference.Raw : $"Citation: {keys}";
    }

    private static string Describe(EmbedUnresolvedReason reason) => reason switch
    {
        EmbedUnresolvedReason.TargetNotFound target =>
            $"Target not found: {target.Target}",
        EmbedUnresolvedReason.HeadingNotFound heading =>
            $"Heading not found: {heading.Heading} in {heading.TargetPath}",
        EmbedUnresolvedReason.BlockNotFound block =>
            $"Block not found: {block.BlockId} in {block.TargetPath}",
        EmbedUnresolvedReason.DepthLimitReached =>
            "Nested embed depth limit reached.",
        EmbedUnresolvedReason.ReadError read =>
            $"Could not read embed: {read.Message}",
        _ => "The embed could not be resolved.",
    };

    private const int MaxEmbedPreviewCharacters = 64 * 1024;
    private const int MaxNestedEmbedNodes = 128;
    private const string PreviewTruncatedMessage =
        "\n\n… Preview truncated. Open source to read the complete content.";

    private static string BoundPreviewText(string text)
    {
        if (text.Length <= MaxEmbedPreviewCharacters)
        {
            return text;
        }
        int end = MaxEmbedPreviewCharacters;
        if (char.IsHighSurrogate(text[end - 1])
            && char.IsLowSurrogate(text[end]))
        {
            end--;
        }
        return string.Concat(text.AsSpan(0, end), PreviewTruncatedMessage);
    }

    private const int MaxPreviewImageBytes = 8 * 1024 * 1024;
    private const int MaxPreviewImageDimension = 1120;

    internal static ImageSource? DecodeImage(byte[] bytes, string? mime = null)
    {
        if (bytes.Length == 0 || bytes.Length > MaxPreviewImageBytes)
        {
            return null;
        }

        bool isSvg = string.Equals(mime, "image/svg+xml", StringComparison.OrdinalIgnoreCase)
            || bytes.AsSpan(0, Math.Min(bytes.Length, 1024))
                .IndexOf("<svg"u8) >= 0;
        return isSvg ? DecodeSvgImage(bytes) : DecodeRasterImage(bytes);
    }

    private static ImageSource? DecodeSvgImage(byte[] bytes)
    {
        try
        {
            string source = new UTF8Encoding(
                encoderShouldEmitUTF8Identifier: false,
                throwOnInvalidBytes: true).GetString(bytes);
            if (!IsSafeSvg(source))
            {
                return null;
            }

            var document = ParseSecureSvg(source);
            if (document is null)
            {
                return null;
            }

            using var svg = new SKSvg();
            svg.Settings.EnableJavaScript = false;
            if (svg.FromSvgDocument(document) is null || svg.Picture is null)
            {
                return null;
            }

            SKRect bounds = svg.Picture.CullRect;
            if (!float.IsFinite(bounds.Width)
                || !float.IsFinite(bounds.Height)
                || bounds.Width <= 0
                || bounds.Height <= 0)
            {
                return null;
            }

            float scale = Math.Min(
                1f,
                MaxPreviewImageDimension / Math.Max(bounds.Width, bounds.Height));
            int width = Math.Max(1, (int)Math.Ceiling(bounds.Width * scale));
            int height = Math.Max(1, (int)Math.Ceiling(bounds.Height * scale));
            using var bitmap = new SKBitmap(new SKImageInfo(
                width,
                height,
                SKColorType.Bgra8888,
                SKAlphaType.Premul));
            using (var canvas = new SKCanvas(bitmap))
            {
                canvas.Clear(SKColors.Transparent);
                canvas.Scale(scale);
                canvas.Translate(-bounds.Left, -bounds.Top);
                canvas.DrawPicture(svg.Picture);
                canvas.Flush();
            }
            using SKImage image = SKImage.FromBitmap(bitmap);
            using SKData encoded = image.Encode(SKEncodedImageFormat.Png, 100);
            return DecodeRasterImage(encoded.ToArray());
        }
        catch (Exception exception) when (
            exception is not OutOfMemoryException
                and not StackOverflowException
                and not AccessViolationException)
        {
            return null;
        }
    }

    private static global::Svg.SvgDocument? ParseSecureSvg(string source)
    {
        var parameters = new SvgParameters(
            null,
            null,
            null,
            new SvgDocumentLoadOptions
            {
                ProcessingMode = SvgProcessingMode.SecureStatic,
                ExternalResources = SvgExternalResourcePolicy.Disabled,
                PreserveUnknownElements = false,
            });
        return SvgService.FromSvg(source, parameters);
    }

    internal static bool SecureSvgParsesForTests(string source) =>
        IsSafeSvg(source) && ParseSecureSvg(source) is not null;
    internal static bool SecureSvgAllowsResourceForTests(string resource)
    {
        const string minimal =
            "<svg xmlns='http://www.w3.org/2000/svg' width='1' height='1'/>";
        var document = ParseSecureSvg(minimal);
        return document is not null
            && SvgExternalResourceResolver.AllowsExternalResource(
                document,
                new Uri(resource, UriKind.RelativeOrAbsolute));
    }
    private static bool IsSafeSvg(string source)
    {
        string[] forbiddenTokens =
        [
            "<!doctype",
            "<!entity",
            "<script",
            "<foreignobject",
            "@import",
        ];
        if (forbiddenTokens.Any(token =>
                source.Contains(token, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = MaxPreviewImageBytes,
            IgnoreComments = true,
            IgnoreProcessingInstructions = false,
        };
        using var textReader = new StringReader(source);
        using XmlReader reader = XmlReader.Create(textReader, settings);
        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.ProcessingInstruction
                && reader.Name.Equals("xml-stylesheet", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            if (reader.NodeType != XmlNodeType.Element)
            {
                continue;
            }
            if (reader.LocalName.Equals("script", StringComparison.OrdinalIgnoreCase)
                || reader.LocalName.Equals("foreignObject", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!reader.HasAttributes)
            {
                continue;
            }
            while (reader.MoveToNextAttribute())
            {
                if (!reader.LocalName.Equals("href", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                string value = reader.Value.Trim();
                if (!value.StartsWith('#')
                    && !value.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }
            reader.MoveToElement();
        }
        return true;
    }

    private static ImageSource? DecodeRasterImage(byte[] bytes)
    {
        try
        {
            using var stream = new MemoryStream(bytes, writable: false);
            BitmapFrame frame = BitmapDecoder.Create(
                stream,
                BitmapCreateOptions.PreservePixelFormat,
                BitmapCacheOption.OnDemand).Frames[0];
            if (frame.PixelWidth <= 0 || frame.PixelHeight <= 0)
            {
                return null;
            }

            bool widthLimits = frame.PixelWidth >= frame.PixelHeight;
            int boundedDimension = Math.Min(
                MaxPreviewImageDimension,
                widthLimits ? frame.PixelWidth : frame.PixelHeight);
            stream.Position = 0;
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            if (widthLimits)
            {
                image.DecodePixelWidth = boundedDimension;
            }
            else
            {
                image.DecodePixelHeight = boundedDimension;
            }
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception exception) when (
            exception is not OutOfMemoryException
                and not StackOverflowException
                and not AccessViolationException)
        {
            return null;
        }
    }
    private static string ImageTitle(string targetPath, string? alt)
    {
        string trimmed = alt?.Trim() ?? string.Empty;
        string descriptor = trimmed.Length > 0
            ? trimmed
            : Path.GetFileName(targetPath);
        return $"Embedded image: {descriptor}";
    }
    private void OpenPopoverSource()
    {
        if (PopoverSourcePath is not { Length: > 0 } path)
        {
            return;
        }

        OpenEmbedSource(path);
    }

    internal void OpenEmbedSource(string path)
    {
        ClosePopover(requestFocus: false);
        _navigate(new EditorNavigationRequest(path, null, null));
    }

    private void ClosePopover(bool requestFocus)
    {
        _embedGeneration++;
        _embedRequestKey = null;
        _activeEmbedRequestKey = null;
        _popoverFocusPending = false;
        IsPopoverOpen = false;
        _hoveredCitationByteOffset = null;
        if (requestFocus)
        {
            FocusRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void EditorSession_HighlightInvalidated(object? sender, EventArgs e)
    {
        _hoveredCitationByteOffset = null;
        _embedGeneration++;
        _embedRequestKey = null;
        _activeEmbedRequestKey = null;
        CancelPendingEmbedPreview();
        _mathRangesRevision = -1;
        QueueMathRefresh(TimeSpan.FromMilliseconds(250));
        if (IsPopoverOpen)
        {
            ClosePopover(requestFocus: false);
        }
    }

    private void OpenPopover(bool requestFocus = true)
    {
        _popoverFocusPending = requestFocus;
        IsPopoverOpen = true;
        if (requestFocus)
        {
            PopoverFocusRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    internal bool ConsumePopoverFocusRequest()
    {
        bool pending = _popoverFocusPending;
        _popoverFocusPending = false;
        return pending;
    }
    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
