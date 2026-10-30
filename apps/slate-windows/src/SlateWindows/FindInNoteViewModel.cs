// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows.Input;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Search;
using uniffi.slate_uniffi;

namespace SlateWindows;

/// <summary>
/// WB-22 (#1112): find in the open note — one per Markdown tab, owned by
/// <see cref="WorkspaceTabViewModel"/>. One engine (AvalonEdit's search
/// strategy) runs over whichever <see cref="IFindInNoteSurface"/> the bar
/// attached. Results are recomputed on a 150 ms debounce after a query or
/// option change (which reveals the first hit at or after the caret) and
/// after every surface text change (which recounts and re-highlights only,
/// never moving the caret). <see cref="Replace"/> acts only on the hit the
/// reader last heard, followed by anchors and validated before any write.
/// </summary>
internal sealed class FindInNoteViewModel : BindableBase, IDisposable
{
    internal static readonly TimeSpan DefaultDebounce = TimeSpan.FromMilliseconds(150);

    private readonly Action<A11yEvent> _announce;
    private readonly DispatcherTimer _debounce;
    private IFindInNoteSurface? _surface;
    private object? _restorePoint;
    private string _query = string.Empty;
    private bool _matchCase;
    private bool _wholeWord;
    private string _replaceText = string.Empty;
    private bool _isOpen;
    private FindInNoteMode _mode;
    private int _currentIndex;
    private int _totalCount;
    private string? _error;
    private string _countText = string.Empty;
    private int _optionsRevision;
    private Recompute _pending = Recompute.None;
    private ResultSet? _results;
    private TrackedHit? _tracked;
    private bool _disposed;

    public FindInNoteViewModel(Action<A11yEvent> announce, TimeSpan? debounce = null)
    {
        _announce = announce;
        _debounce = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = debounce ?? DefaultDebounce,
        };
        _debounce.Tick += (_, _) => RunPending();
        NextCommand = new RelayCommand(_ => Next(), _ => IsOpen);
        PreviousCommand = new RelayCommand(_ => Previous(), _ => IsOpen);
        ReplaceCommand = new RelayCommand(_ => Replace(), _ => CanReplace && _surface?.Document is not null);
        ReplaceAllCommand = new RelayCommand(_ => ReplaceAll(), _ => CanReplace && _surface?.Document is not null);
        CloseCommand = new RelayCommand(_ => Close(), _ => IsOpen);
    }

    public string Query
    {
        get => _query;
        set
        {
            if (SetField(ref _query, value ?? string.Empty))
            {
                OptionsChanged();
            }
        }
    }

    public bool MatchCase
    {
        get => _matchCase;
        set
        {
            if (SetField(ref _matchCase, value))
            {
                OptionsChanged();
            }
        }
    }

    public bool WholeWord
    {
        get => _wholeWord;
        set
        {
            if (SetField(ref _wholeWord, value))
            {
                OptionsChanged();
            }
        }
    }

    public string ReplaceText
    {
        get => _replaceText;
        set => SetField(ref _replaceText, value ?? string.Empty);
    }

    public bool IsOpen
    {
        get => _isOpen;
        private set => SetField(ref _isOpen, value);
    }

    /// <summary>Which surface the bar searches; follows the tab's mode.</summary>
    public FindInNoteMode Mode
    {
        get => _mode;
        set
        {
            if (SetField(ref _mode, value))
            {
                OnPropertyChanged(nameof(CanReplace));
                RaiseCommands();
            }
        }
    }

    /// <summary>Replace exists in the editor only.</summary>
    public bool CanReplace => Mode == FindInNoteMode.Editor;

    /// <summary>1-based; 0 when no match is current.</summary>
    public int CurrentIndex
    {
        get => _currentIndex;
        private set => SetField(ref _currentIndex, value);
    }

    public int TotalCount
    {
        get => _totalCount;
        private set => SetField(ref _totalCount, value);
    }

    /// <summary>The count readout's text; empty while there is no query.</summary>
    public string CountText
    {
        get => _countText;
        private set => SetField(ref _countText, value);
    }

    /// <summary>A pattern error, verbatim; null otherwise.</summary>
    public string? Error
    {
        get => _error;
        private set
        {
            if (SetField(ref _error, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => _error is not null;

    public ICommand NextCommand { get; }

    public ICommand PreviousCommand { get; }

    public ICommand ReplaceCommand { get; }

    public ICommand ReplaceAllCommand { get; }

    public ICommand CloseCommand { get; }

    /// <summary>Asks the bar to focus the query and select it all (the
    /// <c>BaseDocumentViewModel.QuickFilterFocusRequested</c> shape).</summary>
    internal event Action? FocusRequested;

    internal IFindInNoteSurface? Surface => _surface;

    /// <summary>Opens the bar, or — open already — focuses the query and
    /// selects it all (mac's <c>Reactivate</c>).</summary>
    public void Open()
    {
        if (!IsOpen)
        {
            IsOpen = true;
            RaiseCommands();
            if (_surface is not null)
            {
                _restorePoint = _surface.CaptureRestorePoint();
            }
        }
        FocusRequested?.Invoke();
    }

    /// <summary>
    /// Attaches the surface the bar shows (null detaches): the previous
    /// surface's highlights are cleared and its changes unsubscribed, the
    /// tracked hit is dropped, the restore point is taken on the new
    /// surface and the set is recounted over it — silently.
    /// </summary>
    public void AttachSurface(IFindInNoteSurface? surface)
    {
        if (ReferenceEquals(surface, _surface))
        {
            return;
        }
        DetachSurface();
        _surface = surface;
        if (surface is null)
        {
            return;
        }
        surface.TextChanged += OnSurfaceTextChanged;
        Mode = surface.Mode;
        // Taken once, at the open's first attach: a tab switch and back, or a
        // same-item reload, re-attaches without moving where Close returns.
        if (IsOpen && _restorePoint is null)
        {
            _restorePoint = surface.CaptureRestorePoint();
        }
        else if (_restorePoint is not null)
        {
            // A same-item reload swapped the document: the point moves onto
            // the live one, so later edits move it as they would have.
            _restorePoint = surface.RebaseRestorePoint(_restorePoint);
        }
        RaiseCommands();
        if (Query.Length > 0)
        {
            // A query asked just before a switch away is answered here.
            Recompute cause = _pending == Recompute.Options ? Recompute.Options : Recompute.Text;
            _debounce.Stop();
            _pending = Recompute.None;
            RecomputeNow(cause);
        }
    }

    public void Next() => Step(forward: true);

    public void Previous() => Step(forward: false);

    /// <summary>
    /// Replaces the tracked hit — the one the reader last heard — when it
    /// still validates, then steps to the next; otherwise writes nothing.
    /// With no tracked hit it acts as <see cref="Next"/>, so a hit is always
    /// heard before Replace can rewrite it.
    /// </summary>
    public void Replace()
    {
        if (!IsOpen || !CanReplace || _surface?.Document is not { } document)
        {
            return;
        }
        if (_tracked is not { } tracked)
        {
            Next();
            return;
        }
        // Validated BEFORE any recompute: the target is never taken from a
        // recomputation.
        if (!Validates(tracked, document))
        {
            _tracked = null;
            _debounce.Stop();
            _pending = Recompute.None;
            RecomputeNow(Recompute.Text);
            Speak(FindInNotePhrase.MatchChanged);
            return;
        }
        int start = tracked.Start.Offset;
        document.Replace(start, tracked.Length, ReplaceText);
        _tracked = null;
        _debounce.Stop();
        _pending = Recompute.None;
        RecomputeNow(Recompute.Text);
        if (_results is { Matches.Count: > 0 } results)
        {
            int after = start + ReplaceText.Length;
            int index = results.Matches.FindIndex(match => match.Offset >= after);
            MakeCurrent(index < 0 ? 0 : index);
            Speak(FindInNotePhrase.Step(CurrentIndex, TotalCount, index < 0 ? FindInNoteWrap.ToTop : FindInNoteWrap.None));
        }
        else
        {
            Speak(FindInNotePhrase.Count(0, 0));
        }
    }

    /// <summary>Replaces every match of a freshly recomputed set in one
    /// undo step.</summary>
    public void ReplaceAll()
    {
        if (!IsOpen || !CanReplace || _surface?.Document is not { } document)
        {
            return;
        }
        EnsureFresh();
        if (_results is not { Matches.Count: > 0 } results)
        {
            Speak(FindInNotePhrase.Count(0, 0));
            return;
        }
        int count = results.Matches.Count;
        document.BeginUpdate();
        try
        {
            for (int i = results.Matches.Count - 1; i >= 0; i--)
            {
                (int offset, int length) = results.Matches[i];
                document.Replace(offset, length, ReplaceText);
            }
        }
        finally
        {
            document.EndUpdate();
        }
        _tracked = null;
        _debounce.Stop();
        _pending = Recompute.None;
        RecomputeNow(Recompute.Text);
        Speak(FindInNotePhrase.ReplacedAll(count));
    }

    /// <summary>Closes the bar: the highlights cleared, the caret and
    /// selection restored where opening found them, focus returned to the
    /// surface.</summary>
    public void Close()
    {
        if (!IsOpen)
        {
            return;
        }
        _debounce.Stop();
        _pending = Recompute.None;
        _tracked = null;
        // Restored and refocused BEFORE IsOpen falls: the bar detaches its
        // surface as soon as it closes.
        if (_surface is { } surface)
        {
            surface.ClearHighlights();
            if (_restorePoint is not null)
            {
                surface.Restore(_restorePoint);
            }
            surface.ReturnFocus();
        }
        _restorePoint = null;
        IsOpen = false;
        RaiseCommands();
    }

    /// <summary>A mode switch or an in-place change of item with the bar
    /// open (PR 1): the bar closes without moving focus or the caret; PR 2
    /// keeps it open across a mode switch instead.</summary>
    internal void CloseForModeSwitch()
    {
        if (!IsOpen)
        {
            return;
        }
        IsOpen = false;
        _debounce.Stop();
        _pending = Recompute.None;
        _tracked = null;
        _restorePoint = null;
        RaiseCommands();
        DetachSurface();
    }

    /// <summary>Runs a pending debounced recompute now.</summary>
    internal void FlushPendingForTests() => RunPending();

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _debounce.Stop();
        DetachSurface();
    }

    private void DetachSurface()
    {
        if (_surface is not { } old)
        {
            return;
        }
        old.TextChanged -= OnSurfaceTextChanged;
        _debounce.Stop();
        old.ClearHighlights();
        if (old is EditorFindSurface editor)
        {
            editor.Detach();
        }
        _surface = null;
        _tracked = null;
        _results = null;
        TotalCount = 0;
        CurrentIndex = 0;
        CountText = string.Empty;
        RaiseCommands();
    }

    private void OptionsChanged()
    {
        _optionsRevision++;
        _pending = Recompute.Options;
        Restart();
    }

    private void OnSurfaceTextChanged()
    {
        if (_pending == Recompute.None)
        {
            _pending = Recompute.Text;
        }
        Restart();
    }

    private void Restart()
    {
        if (!IsOpen || _disposed)
        {
            return;
        }
        _debounce.Stop();
        _debounce.Start();
    }

    private void RunPending()
    {
        _debounce.Stop();
        if (_surface is null)
        {
            // Detached (a tab switch inside the window): the pending change
            // waits for the re-attach, which answers it.
            return;
        }
        Recompute cause = _pending;
        _pending = Recompute.None;
        if (cause != Recompute.None && IsOpen)
        {
            RecomputeNow(cause);
        }
    }

    /// <summary>Navigation and Replace All act on a fresh set: a pending
    /// change or a stale stamp recomputes synchronously first (silently,
    /// moving nothing — the action itself then moves).</summary>
    private void EnsureFresh()
    {
        bool stale = _pending != Recompute.None
            || _results is null
            || _surface is null
            || !ReferenceEquals(_results.Stamp, _surface.Stamp)
            || _results.OptionsRevision != _optionsRevision;
        if (!stale)
        {
            return;
        }
        _debounce.Stop();
        _pending = Recompute.None;
        RecomputeNow(Recompute.Text);
    }

    private void Step(bool forward)
    {
        if (!IsOpen || _surface is not { } surface)
        {
            return;
        }
        EnsureFresh();
        if (Error is { } error)
        {
            Speak(error);
            return;
        }
        if (_results is not { Matches.Count: > 0 } results)
        {
            Speak(FindInNotePhrase.Count(0, 0));
            return;
        }
        (int offset, int length) = surface.Selection;
        int index;
        FindInNoteWrap wrap = FindInNoteWrap.None;
        if (forward)
        {
            int from = offset + length;
            index = results.Matches.FindIndex(match => match.Offset >= from);
            if (index < 0)
            {
                index = 0;
                wrap = FindInNoteWrap.ToTop;
            }
        }
        else
        {
            index = results.Matches.FindLastIndex(match => match.Offset + match.Length <= offset);
            if (index < 0)
            {
                index = results.Matches.Count - 1;
                wrap = FindInNoteWrap.ToEnd;
            }
        }
        MakeCurrent(index);
        Speak(FindInNotePhrase.Step(CurrentIndex, TotalCount, wrap));
    }

    /// <summary>
    /// Recomputes over the attached surface. An option change reveals the
    /// first hit at or after the caret and speaks the count (typing is
    /// coalesced by the debounce); a text change recounts and re-highlights
    /// only — the tracked hit stays current while it validates.
    /// </summary>
    private void RecomputeNow(Recompute cause)
    {
        if (_surface is not { } surface)
        {
            return;
        }
        if (Query.Length == 0)
        {
            _results = null;
            _tracked = null;
            Error = null;
            surface.ClearHighlights();
            TotalCount = 0;
            CurrentIndex = 0;
            CountText = string.Empty;
            return;
        }
        ISearchStrategy? strategy = CreateStrategy();
        if (strategy is null)
        {
            _results = new ResultSet([], surface.Stamp, _optionsRevision);
            _tracked = null;
            surface.ClearHighlights();
            TotalCount = 0;
            CurrentIndex = 0;
            CountText = Error ?? string.Empty;
            if (cause == Recompute.Options && Error is { } error)
            {
                Speak(error);
            }
            return;
        }
        ITextSource text = surface.Text;
        List<(int Offset, int Length)> matches = strategy
            .FindAll(text, 0, text.TextLength)
            .Select(result => (result.Offset, result.Length))
            .ToList();
        _results = new ResultSet(matches, surface.Stamp, _optionsRevision);
        TotalCount = matches.Count;

        if (cause == Recompute.Options)
        {
            _tracked = null;
            if (matches.Count == 0)
            {
                CurrentIndex = 0;
                surface.Highlight(matches, -1);
                UpdateCountText();
                Speak(FindInNotePhrase.Count(0, 0));
                return;
            }
            int caret = surface.Selection.Offset;
            int index = matches.FindIndex(match => match.Offset >= caret);
            MakeCurrent(index < 0 ? 0 : index);
            Speak(FindInNotePhrase.Count(CurrentIndex, TotalCount));
            return;
        }

        // A text change: recount only. What stays current depends on the
        // surface: the editor's tracked hit while it validates; otherwise
        // the match the selection covers exactly.
        int current = -1;
        if (surface.Document is { } document)
        {
            if (_tracked is { } tracked && Validates(tracked, document))
            {
                current = matches.IndexOf((tracked.Start.Offset, tracked.Length));
            }
            if (current < 0)
            {
                _tracked = null;
            }
        }
        else
        {
            current = matches.IndexOf(surface.Selection);
        }
        CurrentIndex = current + 1;
        surface.Highlight(matches, current);
        UpdateCountText();
    }

    private void MakeCurrent(int index)
    {
        if (_surface is not { } surface || _results is not { } results)
        {
            return;
        }
        (int offset, int length) = results.Matches[index];
        surface.Reveal(offset, length);
        _tracked = surface.Document is { } document ? Track(document, offset, length) : null;
        CurrentIndex = index + 1;
        surface.Highlight(results.Matches, index);
        UpdateCountText();
    }

    /// <summary>The hit holds when its anchors still span its length and
    /// the live strategy still finds exactly it there.</summary>
    private bool Validates(TrackedHit tracked, TextDocument document)
    {
        int start = tracked.Start.Offset;
        if (tracked.End.Offset - start != tracked.Length || start + tracked.Length > document.TextLength)
        {
            return false;
        }
        if (CreateStrategy() is not { } strategy)
        {
            return false;
        }
        ISearchResult? found = strategy.FindNext(document, start, tracked.Length);
        return found is not null && found.Offset == start && found.Length == tracked.Length;
    }

    private ISearchStrategy? CreateStrategy()
    {
        try
        {
            ISearchStrategy strategy = SearchStrategyFactory.Create(Query, !MatchCase, WholeWord, SearchMode.Normal);
            Error = null;
            return strategy;
        }
        catch (SearchPatternException exception)
        {
            Error = exception.Message;
            return null;
        }
    }

    private static TrackedHit Track(TextDocument document, int offset, int length)
    {
        TextAnchor start = document.CreateAnchor(offset);
        start.MovementType = AnchorMovementType.AfterInsertion;
        start.SurviveDeletion = true;
        TextAnchor end = document.CreateAnchor(offset + length);
        end.MovementType = AnchorMovementType.BeforeInsertion;
        end.SurviveDeletion = true;
        return new TrackedHit(start, end, length);
    }

    private void UpdateCountText() => CountText = FindInNotePhrase.Count(CurrentIndex, TotalCount);

    private void Speak(string line) => _announce(new A11yEvent.HostComposed(line, A11yPriority.Medium));

    private void RaiseCommands()
    {
        foreach (ICommand command in new[] { NextCommand, PreviousCommand, ReplaceCommand, ReplaceAllCommand, CloseCommand })
        {
            ((RelayCommand)command).RaiseCanExecuteChanged();
        }
    }

    private enum Recompute
    {
        None,
        Text,
        Options,
    }

    private sealed record ResultSet(List<(int Offset, int Length)> Matches, object Stamp, int OptionsRevision);

    /// <summary>The hit last made current, followed by its anchors.</summary>
    private sealed record TrackedHit(TextAnchor Start, TextAnchor End, int Length);
}
