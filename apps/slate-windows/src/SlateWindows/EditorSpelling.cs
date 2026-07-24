// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;

namespace SlateWindows;

internal readonly record struct EditorSpellingError(int StartUtf16, int LengthUtf16);

internal interface IEditorSpellingService : IDisposable
{
    bool IsAvailable { get; }
    IReadOnlyList<EditorSpellingError> Check(string text);
}

internal sealed class UnavailableEditorSpellingService : IEditorSpellingService
{
    public bool IsAvailable => false;
    public IReadOnlyList<EditorSpellingError> Check(string text) => [];
    public void Dispose()
    {
    }
}

internal sealed class WindowsEditorSpellingService : IEditorSpellingService
{
    private ISpellChecker? _checker;

    private WindowsEditorSpellingService(ISpellChecker checker)
    {
        _checker = checker;
    }

    public bool IsAvailable => _checker is not null;

    public static IEditorSpellingService CreateForCurrentUser()
    {
        ISpellCheckerFactory? factory = null;
        try
        {
            Type type = Type.GetTypeFromCLSID(
                new Guid("7AB36653-1796-484B-BDFA-E74F1DB7C1DC"),
                throwOnError: true)!;
            factory = (ISpellCheckerFactory)Activator.CreateInstance(type)!;
            foreach (string languageTag in CandidateLanguageTags())
            {
                if (factory.IsSupported(languageTag))
                {
                    return new WindowsEditorSpellingService(
                        factory.CreateSpellChecker(languageTag));
                }
            }
        }
        catch (Exception exception) when (
            exception is COMException
                or InvalidCastException
                or PlatformNotSupportedException
                or TypeLoadException)
        {
        }
        finally
        {
            Release(factory);
        }

        return new UnavailableEditorSpellingService();
    }

    public IReadOnlyList<EditorSpellingError> Check(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        ISpellChecker? checker = _checker;
        if (checker is null || text.Length == 0)
        {
            return [];
        }

        IEnumSpellingError? errors = null;
        var result = new List<EditorSpellingError>();
        try
        {
            errors = checker.Check(text);
            while (true)
            {
                int hresult = errors.Next(out ISpellingError? error);
                if (hresult == 1 || error is null)
                {
                    break;
                }

                Marshal.ThrowExceptionForHR(hresult);
                try
                {
                    uint start = error.StartIndex;
                    uint length = error.Length;
                    if (length > 0
                        && start <= text.Length
                        && length <= text.Length - start)
                    {
                        result.Add(new EditorSpellingError(
                            checked((int)start),
                            checked((int)length)));
                    }
                }
                finally
                {
                    Release(error);
                }
            }
        }
        catch (COMException)
        {
            return [];
        }
        finally
        {
            Release(errors);
        }

        return result;
    }

    public void Dispose()
    {
        Release(_checker);
        _checker = null;
    }

    private static IEnumerable<string> CandidateLanguageTags() =>
        new[]
        {
            CultureInfo.CurrentUICulture.Name,
            CultureInfo.CurrentCulture.Name,
            "en-US",
        }.Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Distinct(StringComparer.OrdinalIgnoreCase);

    private static void Release(object? value)
    {
        if (value is not null && Marshal.IsComObject(value))
        {
            _ = Marshal.FinalReleaseComObject(value);
        }
    }

    [ComImport]
    [Guid("8E018A9D-2415-4677-BF08-794EA61F94BB")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ISpellCheckerFactory
    {
        [return: MarshalAs(UnmanagedType.Interface)]
        IEnumString GetSupportedLanguages();

        [return: MarshalAs(UnmanagedType.Bool)]
        bool IsSupported([MarshalAs(UnmanagedType.LPWStr)] string languageTag);

        [return: MarshalAs(UnmanagedType.Interface)]
        ISpellChecker CreateSpellChecker(
            [MarshalAs(UnmanagedType.LPWStr)] string languageTag);
    }

    [ComImport]
    [Guid("B6FD0B71-E2BC-4653-8D05-F197E412770B")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ISpellChecker
    {
        [return: MarshalAs(UnmanagedType.LPWStr)]
        string GetLanguageTag();

        [return: MarshalAs(UnmanagedType.Interface)]
        IEnumSpellingError Check([MarshalAs(UnmanagedType.LPWStr)] string text);
    }

    [ComImport]
    [Guid("803E3BD4-2828-4410-8290-418D1D73C762")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IEnumSpellingError
    {
        [PreserveSig]
        int Next([MarshalAs(UnmanagedType.Interface)] out ISpellingError? value);
    }

    [ComImport]
    [Guid("B7C82D61-FBE8-4B47-9B27-6C0D2E0DE0A3")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ISpellingError
    {
        uint StartIndex { get; }
        uint Length { get; }
    }
}

internal sealed class AvalonSpellingCoordinator : DocumentColorizingTransformer, IDisposable
{
    private const int MaximumCheckLength = 65_536;

    private static readonly TextDecorationCollection ErrorDecorations =
        CreateErrorDecorations();

    private readonly TextEditor _editor;
    private readonly EditorPreferencesViewModel _preferences;
    private readonly DispatcherTimer _timer;
    private IReadOnlyList<EditorSpellingError> _errors = [];
    private int _windowStart;
    private bool _disposed;

    public AvalonSpellingCoordinator(
        TextEditor editor,
        EditorPreferencesViewModel preferences)
    {
        _editor = editor;
        _preferences = preferences;
        _timer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(250),
            DispatcherPriority.Background,
            (_, _) => Refresh(),
            editor.Dispatcher);
        _timer.Stop();
        _editor.TextArea.TextView.LineTransformers.Add(this);
        _editor.Document.Changed += Document_Changed;
        _editor.TextArea.TextView.ScrollOffsetChanged += Viewport_Changed;
        _editor.TextArea.TextView.SizeChanged += Viewport_Changed;
        _preferences.PropertyChanged += Preferences_PropertyChanged;
        Schedule();
    }

    internal int ErrorCountForTests => _errors.Count;

    internal void RefreshForTests() => Refresh();

    protected override void ColorizeLine(DocumentLine line)
    {
        foreach (EditorSpellingError error in _errors)
        {
            int start = _windowStart + error.StartUtf16;
            int end = start + error.LengthUtf16;
            int appliedStart = Math.Max(start, line.Offset);
            int appliedEnd = Math.Min(end, line.EndOffset);
            if (appliedStart < appliedEnd)
            {
                ChangeLinePart(
                    appliedStart,
                    appliedEnd,
                    element => element.TextRunProperties.SetTextDecorations(
                        ErrorDecorations));
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _timer.Stop();
        _editor.Document.Changed -= Document_Changed;
        _editor.TextArea.TextView.ScrollOffsetChanged -= Viewport_Changed;
        _editor.TextArea.TextView.SizeChanged -= Viewport_Changed;
        _preferences.PropertyChanged -= Preferences_PropertyChanged;
        _editor.TextArea.TextView.LineTransformers.Remove(this);
        _errors = [];
        _editor.TextArea.TextView.Redraw();
    }

    private void Schedule()
    {
        if (_disposed)
        {
            return;
        }

        _timer.Stop();
        _timer.Start();
    }

    private void Refresh()
    {
        _timer.Stop();
        if (_disposed)
        {
            return;
        }

        if (!_preferences.IsSpellCheckEnabled
            || !_preferences.SpellingService.IsAvailable)
        {
            Clear();
            return;
        }

        (int start, int length) = VisibleWindow();
        string text = _editor.Document.GetText(start, length);
        IReadOnlyList<EditorSpellingError> next =
            _preferences.SpellingService.Check(text);
        if (_windowStart == start && _errors.SequenceEqual(next))
        {
            return;
        }

        _windowStart = start;
        _errors = next;
        _editor.TextArea.TextView.Redraw();
    }

    private (int Start, int Length) VisibleWindow()
    {
        TextView view = _editor.TextArea.TextView;
        view.EnsureVisualLines();
        int start;
        int end;
        if (view.VisualLines.Count == 0)
        {
            int caret = Math.Clamp(
                _editor.CaretOffset,
                0,
                _editor.Document.TextLength);
            DocumentLine line = _editor.Document.GetLineByOffset(caret);
            start = line.Offset;
            end = line.EndOffset;
        }
        else
        {
            start = view.VisualLines[0].FirstDocumentLine.Offset;
            end = view.VisualLines[^1].LastDocumentLine.EndOffset;
        }

        if (end - start > MaximumCheckLength)
        {
            int center = Math.Clamp(_editor.CaretOffset, start, end);
            start = Math.Max(start, center - (MaximumCheckLength / 2));
            end = Math.Min(end, start + MaximumCheckLength);
            start = Math.Max(start, end - MaximumCheckLength);
        }

        return (start, Math.Max(0, end - start));
    }

    private void Clear()
    {
        if (_errors.Count == 0)
        {
            return;
        }

        _errors = [];
        _editor.TextArea.TextView.Redraw();
    }

    private void Document_Changed(object? sender, DocumentChangeEventArgs e) => Schedule();

    private void Viewport_Changed(object? sender, EventArgs e) => Schedule();

    private void Viewport_Changed(object? sender, SizeChangedEventArgs e) => Schedule();

    private void Preferences_PropertyChanged(
        object? sender,
        PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(EditorPreferencesViewModel.IsSpellCheckEnabled)
            or nameof(EditorPreferencesViewModel.FontSize))
        {
            Schedule();
        }
    }

    private static TextDecorationCollection CreateErrorDecorations()
    {
        var pen = new Pen(Brushes.Red, 1);
        pen.Freeze();
        var decoration = new TextDecoration
        {
            Location = TextDecorationLocation.Underline,
            Pen = pen,
            PenOffset = 2,
            PenOffsetUnit = TextDecorationUnit.Pixel,
            PenThicknessUnit = TextDecorationUnit.Pixel,
        };
        var decorations = new TextDecorationCollection { decoration };
        decorations.Freeze();
        return decorations;
    }
}
