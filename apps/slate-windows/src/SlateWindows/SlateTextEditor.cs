// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Automation.Peers;
using ICSharpCode.AvalonEdit;

namespace SlateWindows;

/// <summary>
/// Keeps AvalonEdit's Document/Value/Text automation contract while making
/// its public peer focus the internal TextArea that owns keyboard input.
/// </summary>
internal sealed class SlateTextEditor : TextEditor
{
    public static readonly DependencyProperty HighlightSessionProperty =
        DependencyProperty.Register(
            nameof(HighlightSession),
            typeof(AvalonDocumentBufferSession),
            typeof(SlateTextEditor),
            new PropertyMetadata(null, HighlightSession_Changed));

    private AvalonHighlightingCoordinator? _highlighting;

    public SlateTextEditor()
    {
        Loaded += SlateTextEditor_Loaded;
        Unloaded += SlateTextEditor_Unloaded;
    }

    public AvalonDocumentBufferSession? HighlightSession
    {
        get => (AvalonDocumentBufferSession?)GetValue(HighlightSessionProperty);
        set => SetValue(HighlightSessionProperty, value);
    }

    internal bool FocusInputOwner() => TextArea.Focus();

    internal AvalonHighlightingCoordinator? HighlightingForCensus => _highlighting;

    protected override AutomationPeer OnCreateAutomationPeer() =>
        new SlateTextEditorAutomationPeer(this);

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == DocumentProperty && IsLoaded)
        {
            AttachHighlighting();
        }
    }

    private static void HighlightSession_Changed(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs eventArgs)
    {
        var editor = (SlateTextEditor)dependencyObject;
        if (editor.IsLoaded)
        {
            editor.AttachHighlighting();
        }
    }

    private void SlateTextEditor_Loaded(object sender, RoutedEventArgs e) => AttachHighlighting();

    private void SlateTextEditor_Unloaded(object sender, RoutedEventArgs e)
    {
        _highlighting?.Dispose();
        _highlighting = null;
    }

    private void AttachHighlighting()
    {
        _highlighting?.Dispose();
        _highlighting = null;
        if (HighlightSession is not null && ReferenceEquals(Document, HighlightSession.Document))
        {
            _highlighting = new AvalonHighlightingCoordinator(this, HighlightSession);
        }
    }
}

internal sealed class SlateTextEditorAutomationPeer : TextEditorAutomationPeer
{
    private readonly SlateTextEditor _owner;
    private readonly AutomationPeer _textAreaPeer;

    internal SlateTextEditorAutomationPeer(SlateTextEditor owner)
        : base(owner)
    {
        _owner = owner;
        _textAreaPeer = UIElementAutomationPeer.CreatePeerForElement(owner.TextArea)
            ?? throw new InvalidOperationException("AvalonEdit did not create a TextArea automation peer.");
        _textAreaPeer.EventsSource = this;
    }

    protected override bool HasKeyboardFocusCore() =>
        _owner.TextArea.IsKeyboardFocusWithin;

    protected override bool IsKeyboardFocusableCore() =>
        _owner.TextArea.Focusable
        && _owner.TextArea.IsEnabled
        && _owner.TextArea.IsVisible;

    protected override List<AutomationPeer>? GetChildrenCore() => null;

    public override object? GetPattern(PatternInterface patternInterface)
    {
        if (patternInterface == PatternInterface.Text)
        {
            return base.GetPattern(patternInterface);
        }
        else if (patternInterface == PatternInterface.Scroll
            && _owner.Template?.FindName("PART_ScrollViewer", _owner)
                is System.Windows.Controls.ScrollViewer scrollViewer)
        {
            AutomationPeer? scrollPeer = UIElementAutomationPeer.CreatePeerForElement(scrollViewer);
            if (scrollPeer is not null)
            {
                scrollPeer.EventsSource = this;
            }
        }

        return base.GetPattern(patternInterface);
    }

    protected override void SetFocusCore()
    {
        if (!_owner.FocusInputOwner())
        {
            throw new InvalidOperationException("The editor TextArea could not receive keyboard focus.");
        }
    }
}