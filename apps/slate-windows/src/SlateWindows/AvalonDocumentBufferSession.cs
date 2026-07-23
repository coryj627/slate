// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows.Threading;
using ICSharpCode.AvalonEdit.Document;
using uniffi.slate_uniffi;

namespace SlateWindows;

/// <summary>
/// One Windows note-editor session. AvalonEdit owns the native view/undo
/// surface while every committed <see cref="TextDocument"/> mutation is fed
/// to the canonical Rust <see cref="DocumentBuffer"/> as a UTF-16 delta.
/// </summary>
internal sealed class AvalonDocumentBufferSession : IDisposable
{
    internal static readonly TimeSpan DefaultIntegrityDelay = TimeSpan.FromMilliseconds(300);

    private readonly object _gate = new();
    private readonly Action<EditorDocumentSyncEvent> _documentEvent;
    private readonly DocumentBuffer _buffer;
    private readonly Dispatcher _dispatcher;
    private readonly DispatcherTimer _integrityTimer;
    private bool _disposed;
    private bool _suppressSyncNotifications;
    private bool _peerUpdateOpen;
    private bool _resetAfterCurrentChange;
    private Exception? _integrityFailure;
    private long _revision;
    private long _appliedDeltaCount;
    private long _lastVerifiedRevision;
    private long _driftRecoveryCount;
    private uint _savedLengthUtf16;
    private string _savedContentHash;
    private string _savedBaselineText;
    private EditorHighlightWindow? _latestHighlightWindow;

    public AvalonDocumentBufferSession(
        string text,
        Action<EditorDocumentSyncEvent> documentEvent,
        TimeSpan? integrityDelay = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(documentEvent);

        _documentEvent = documentEvent;
        _dispatcher = Dispatcher.CurrentDispatcher;
        _buffer = new DocumentBuffer(text);
        _savedLengthUtf16 = _buffer.LenUtf16();
        _savedContentHash = _buffer.ContentHash();
        _savedBaselineText = text;
        Document = new TextDocument(text);
        Document.Changing += Document_Changing;
        Document.Changed += Document_Changed;
        Document.UpdateStarted += Document_UpdateStarted;
        Document.UpdateFinished += Document_UpdateFinished;
        Document.UndoStack.MarkAsOriginalFile();

        _integrityTimer = new DispatcherTimer(DispatcherPriority.Background, _dispatcher)
        {
            Interval = integrityDelay ?? DefaultIntegrityDelay,
        };
        _integrityTimer.Tick += IntegrityTimer_Tick;
    }

    public TextDocument Document { get; }

    public event EventHandler? HighlightInvalidated;

    /// <summary>
    /// The last canonical semantic span window accepted for this document
    /// revision. W7-1's UIA peer consumes this same immutable window instead
    /// of running a second classifier.
    /// </summary>
    public EditorHighlightWindow? LatestHighlightWindow
    {
        get
        {
            ThrowIfDisposed();
            lock (_gate)
            {
                return _latestHighlightWindow;
            }
        }
    }

    public EditorSavedBaseline SavedBaseline
    {
        get
        {
            ThrowIfDisposed();
            lock (_gate)
            {
                return new EditorSavedBaseline(
                    _savedBaselineText,
                    _savedLengthUtf16,
                    _savedContentHash);
            }
        }
    }
    public bool IsAtSavedBaseline
    {
        get
        {
            ThrowIfDisposed();
            Document.VerifyAccess();
            if (Document.UndoStack.IsOriginalFile)
            {
                return true;
            }

            string savedBaselineText;
            uint savedLengthUtf16;
            lock (_gate)
            {
                savedBaselineText = _savedBaselineText;
                savedLengthUtf16 = _savedLengthUtf16;
            }

            return Document.TextLength == savedLengthUtf16
                && string.Equals(Document.Text, savedBaselineText, StringComparison.Ordinal);
        }
    }

    public long Revision
    {
        get
        {
            lock (_gate)
            {
                return _revision;
            }
        }
    }

    public long AppliedDeltaCount
    {
        get
        {
            lock (_gate)
            {
                return _appliedDeltaCount;
            }
        }
    }

    public long DriftRecoveryCount
    {
        get
        {
            lock (_gate)
            {
                return _driftRecoveryCount;
            }
        }
    }

    /// <summary>
    /// Census seam invoked after the first compare but before save snapshot
    /// acquisition. Production leaves this null; a test can inject a reentrant
    /// document mutation to prove the monotonic revision gate retries.
    /// </summary>
    internal Action<AvalonDocumentBufferSession>? BeforeSaveSnapshotAcquired { get; set; }

    /// <summary>
    /// Census-only direct access for flag-free drift injection. Mutating this
    /// buffer does not set a suspect bit; automatic idle/save checks must find
    /// the divergence by comparing content hashes.
    /// </summary>
    internal DocumentBuffer BufferForCensus => _buffer;

    /// <summary>
    /// Computes one canonical span window over UTF-16 editor coordinates and
    /// maps its UTF-8 byte offsets back to Avalon coordinates. This is the sole
    /// Windows highlight classifier boundary: all Markdown semantics come from
    /// <see cref="DocumentBuffer.HighlightInRange"/>.
    /// </summary>
    public EditorHighlightWindow HighlightInRange(int startUtf16, int endUtf16)
    {
        ThrowIfDisposed();
        Document.VerifyAccess();

        lock (_gate)
        {
            int clampedStart = Math.Clamp(startUtf16, 0, Document.TextLength);
            int clampedEnd = Math.Clamp(endUtf16, clampedStart, Document.TextLength);
            long revision = _revision;
            RangedHighlight ranged = _buffer.HighlightInRange(
                checked((uint)clampedStart),
                checked((uint)clampedEnd));
            int appliedStart = checked((int)_buffer.ByteToUtf16(ranged.AppliedStart));
            int appliedEnd = checked((int)_buffer.ByteToUtf16(ranged.AppliedEnd));
            if (appliedStart < 0
                || appliedEnd < appliedStart
                || appliedEnd > Document.TextLength)
            {
                throw new InvalidOperationException(
                    "Canonical highlight returned an invalid applied range.");
            }

            string appliedText = Document.GetText(appliedStart, appliedEnd - appliedStart);
            EditorSemanticSpan[] spans = EditorSpanMapper.MapWindow(
                appliedText,
                appliedStart,
                ranged.AppliedStart,
                ranged.Spans);
            var window = new EditorHighlightWindow(
                revision,
                appliedStart,
                appliedEnd - appliedStart,
                spans);
            _latestHighlightWindow = window;
            return window;
        }
    }

    public void ReplaceAll(string text)
    {
        ThrowIfDisposed();
        Document.VerifyAccess();
        if (!string.Equals(Document.Text, text, StringComparison.Ordinal))
        {
            Document.Replace(0, Document.TextLength, text);
        }
    }

    /// <summary>
    /// Initializes or reloads a mirrored tab as one native undo group. A
    /// dirty source stays undoable back to the disk baseline; a clean source
    /// advances this view's saved marker after synchronization.
    /// </summary>
    public void SynchronizeFromPeer(
        string text,
        EditorSavedBaseline savedBaseline,
        bool reconstructUndoHistory)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(savedBaseline);
        ThrowIfDisposed();
        Document.VerifyAccess();

        _suppressSyncNotifications = true;
        try
        {
            bool baselineChanged;
            lock (_gate)
            {
                baselineChanged = _savedLengthUtf16 != savedBaseline.Utf16Length
                    || !string.Equals(
                        _savedContentHash,
                        savedBaseline.ContentHash,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        _savedBaselineText,
                        savedBaseline.Text,
                        StringComparison.Ordinal);
            }

            if (!reconstructUndoHistory
                && !string.Equals(Document.Text, text, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "An existing peer diverged from the source document before baseline synchronization.");
            }

            if (baselineChanged && reconstructUndoHistory)
            {
                ReplacePeerDocument(savedBaseline.Text);
                Document.UndoStack.ClearAll();
                AdoptSavedBaseline(savedBaseline);
            }
            else if (baselineChanged)
            {
                if (!string.Equals(text, savedBaseline.Text, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Only a clean existing peer can advance its saved baseline without reconstruction.");
                }

                AdoptSavedBaseline(savedBaseline);
            }

            if (reconstructUndoHistory)
            {
                ReplacePeerDocument(text);
            }

            if (string.Equals(text, savedBaseline.Text, StringComparison.Ordinal))
            {
                AdoptSavedBaseline(savedBaseline);
            }
        }
        finally
        {
            _suppressSyncNotifications = false;
        }
    }

    private void ReplacePeerDocument(string text)
    {
        if (string.Equals(Document.Text, text, StringComparison.Ordinal))
        {
            return;
        }

        using (Document.RunUpdate())
        {
            Document.Replace(0, Document.TextLength, text);
        }
    }
    /// <summary>
    /// Opens the peer's matching outer Avalon update/undo group.
    /// </summary>
    public void BeginPeerUpdate()
    {
        ThrowIfDisposed();
        Document.VerifyAccess();
        if (_peerUpdateOpen)
        {
            throw new InvalidOperationException("A peer document update is already open.");
        }

        _suppressSyncNotifications = true;
        try
        {
            Document.BeginUpdate();
            _peerUpdateOpen = true;
        }
        finally
        {
            _suppressSyncNotifications = false;
        }
    }

    /// <summary>
    /// Applies one mutation inside the peer's matching outer update group.
    /// </summary>
    public void ApplyPeerEdit(EditorDocumentChange change)
    {
        ArgumentNullException.ThrowIfNull(change);
        ThrowIfDisposed();
        Document.VerifyAccess();
        if (!_peerUpdateOpen)
        {
            throw new InvalidOperationException("A peer edit requires an open document update.");
        }

        _suppressSyncNotifications = true;
        try
        {
            Document.Replace(change.Offset, change.RemovalLength, change.InsertedText);
        }
        finally
        {
            _suppressSyncNotifications = false;
        }
    }

    /// <summary>
    /// Closes the peer's matching outer Avalon update/undo group.
    /// </summary>
    public void EndPeerUpdate()
    {
        ThrowIfDisposed();
        Document.VerifyAccess();
        if (!_peerUpdateOpen)
        {
            throw new InvalidOperationException("No peer document update is open.");
        }

        _suppressSyncNotifications = true;
        try
        {
            Document.EndUpdate();
        }
        finally
        {
            _peerUpdateOpen = false;
            _suppressSyncNotifications = false;
        }
    }

    /// <summary>
    /// Runs the automatic idle/highlight-cadence integrity tier immediately.
    /// Returns true when the buffer already matched and false when a reset was
    /// required to reconverge.
    /// </summary>
    public bool VerifyIdleIntegrity()
    {
        ThrowIfDisposed();
        Document.VerifyAccess();
        lock (_gate)
        {
            bool matched = VerifyAndReconverge(Document.Text);
            _lastVerifiedRevision = _revision;
            return matched;
        }
    }

    /// <summary>
    /// Acquires a save snapshot under one serialized gate. A change injected
    /// after comparison advances <see cref="Revision"/> and forces a retry;
    /// repeated movement or any hash/reset error fails the save closed.
    /// </summary>
    public EditorSaveSnapshot PrepareSaveSnapshot()
    {
        ThrowIfDisposed();
        Document.VerifyAccess();
        lock (_gate)
        {
            for (int attempt = 0; attempt < 4; attempt++)
            {
                ThrowIntegrityFailure();
                long verifiedRevision = _revision;
                string verifiedText = Document.Text;
                VerifyAndReconverge(verifiedText);

                BeforeSaveSnapshotAcquired?.Invoke(this);
                if (_revision != verifiedRevision)
                {
                    continue;
                }

                string saveText = Document.Text;
                if (_revision == verifiedRevision
                    && string.Equals(
                        _buffer.ContentHash(),
                        SlateUniffiMethods.EditorTextContentHash(saveText),
                        StringComparison.Ordinal))
                {
                    _lastVerifiedRevision = verifiedRevision;
                    return new EditorSaveSnapshot(saveText, verifiedRevision);
                }
            }
        }

        throw new InvalidOperationException(
            "Editor changed repeatedly while acquiring a verified save snapshot.");
    }

    public void MarkSaved(string savedText)
    {
        ArgumentNullException.ThrowIfNull(savedText);
        ThrowIfDisposed();
        Document.VerifyAccess();
        if (!string.Equals(Document.Text, savedText, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Saved text does not match the editor document.");
        }

        string contentHash;
        uint length;
        lock (_gate)
        {
            length = _buffer.LenUtf16();
            contentHash = _buffer.ContentHash();
        }

        AdoptSavedBaseline(new EditorSavedBaseline(savedText, length, contentHash));
    }

    public void MarkSaved(EditorSavedBaseline savedBaseline)
    {
        ArgumentNullException.ThrowIfNull(savedBaseline);
        ThrowIfDisposed();
        Document.VerifyAccess();
        if (!string.Equals(Document.Text, savedBaseline.Text, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Peer saved baseline does not match the editor document.");
        }

        AdoptSavedBaseline(savedBaseline);
    }

    private void AdoptSavedBaseline(EditorSavedBaseline savedBaseline)
    {
        lock (_gate)
        {
            _savedBaselineText = savedBaseline.Text;
            _savedLengthUtf16 = savedBaseline.Utf16Length;
            _savedContentHash = savedBaseline.ContentHash;
        }

        Document.UndoStack.MarkAsOriginalFile();
    }
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _integrityTimer.Stop();
        _integrityTimer.Tick -= IntegrityTimer_Tick;
        Document.Changing -= Document_Changing;
        Document.Changed -= Document_Changed;
        Document.UpdateStarted -= Document_UpdateStarted;
        Document.UpdateFinished -= Document_UpdateFinished;
        _buffer.Dispose();
    }

    private void Document_Changing(object? sender, DocumentChangeEventArgs change)
    {
        lock (_gate)
        {
            try
            {
                _buffer.ApplyEdit(
                    checked((uint)change.Offset),
                    checked((uint)change.RemovalLength),
                    change.InsertedText.Text);
                _appliedDeltaCount++;
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                _resetAfterCurrentChange = true;
                _integrityFailure = exception;
            }
        }
    }

    private void Document_Changed(object? sender, DocumentChangeEventArgs change)
    {
        EditorDocumentChange? notification = null;
        lock (_gate)
        {
            _revision++;
            _latestHighlightWindow = null;
            if (_resetAfterCurrentChange
                || _buffer.LenUtf16() != checked((uint)Document.TextLength))
            {
                TryResetAfterChange();
            }

            _integrityTimer.Stop();
            _integrityTimer.Start();
            if (!_suppressSyncNotifications)
            {
                notification = new EditorDocumentChange(
                    change.Offset,
                    change.RemovalLength,
                    change.InsertedText.Text);
            }
        }

        if (notification is not null)
        {
            _documentEvent(notification);
        }

        HighlightInvalidated?.Invoke(this, EventArgs.Empty);
    }

    private void Document_UpdateStarted(object? sender, EventArgs e)
    {
        if (!_suppressSyncNotifications)
        {
            _documentEvent(new EditorDocumentUpdateStarted());
        }
    }

    private void Document_UpdateFinished(object? sender, EventArgs e)
    {
        if (!_suppressSyncNotifications)
        {
            _documentEvent(new EditorDocumentUpdateFinished());
        }
    }

    private bool VerifyAndReconverge(string text)
    {
        ThrowIntegrityFailure();
        string bufferHash = _buffer.ContentHash();
        string editorHash = SlateUniffiMethods.EditorTextContentHash(text);
        if (string.Equals(bufferHash, editorHash, StringComparison.Ordinal))
        {
            return true;
        }

        _buffer.Reset(text);
        _driftRecoveryCount++;
        _integrityFailure = null;
        return false;
    }

    private void TryResetAfterChange()
    {
        try
        {
            _buffer.Reset(Document.Text);
            _driftRecoveryCount++;
            _integrityFailure = null;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            _integrityFailure = exception;
        }
        finally
        {
            _resetAfterCurrentChange = false;
        }
    }

    private void IntegrityTimer_Tick(object? sender, EventArgs e)
    {
        _integrityTimer.Stop();
        lock (_gate)
        {
            if (_revision == _lastVerifiedRevision)
            {
                return;
            }
        }

        try
        {
            VerifyIdleIntegrity();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            lock (_gate)
            {
                _integrityFailure = exception;
            }
        }
    }

    private void ThrowIntegrityFailure()
    {
        if (_integrityFailure is not null)
        {
            throw new InvalidOperationException(
                "The editor buffer integrity check failed; save was blocked.",
                _integrityFailure);
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}

internal sealed record EditorSaveSnapshot(string Text, long Revision);

internal sealed record EditorSavedBaseline(string Text, uint Utf16Length, string ContentHash);

internal sealed record EditorSemanticSpan(
    int StartUtf16,
    int LengthUtf16,
    uint StartByte,
    uint EndByte,
    EditorSpanKind Kind);

internal sealed record EditorHighlightWindow(
    long Revision,
    int AppliedStartUtf16,
    int AppliedLengthUtf16,
    IReadOnlyList<EditorSemanticSpan> Spans)
{
    public int AppliedEndUtf16 => AppliedStartUtf16 + AppliedLengthUtf16;
}

internal abstract record EditorDocumentSyncEvent;

internal sealed record EditorDocumentUpdateStarted : EditorDocumentSyncEvent;

internal sealed record EditorDocumentChange(int Offset, int RemovalLength, string InsertedText)
    : EditorDocumentSyncEvent;

internal sealed record EditorDocumentUpdateFinished : EditorDocumentSyncEvent;

internal static class EditorOffsetMapper
{
    public static uint Utf16ToByte(string text, int utf16Offset) =>
        SlateUniffiMethods.TextUtf16ToByte(text, checked((uint)Math.Max(0, utf16Offset)));

    public static uint ByteToUtf16(DocumentBuffer buffer, uint byteOffset) =>
        buffer.ByteToUtf16(byteOffset);
}
