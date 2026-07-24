// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Input;
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

    public static readonly DependencyProperty InteractionSessionProperty =
        DependencyProperty.Register(
            nameof(InteractionSession),
            typeof(EditorInteractionCoordinator),
            typeof(SlateTextEditor),
            new PropertyMetadata(null, InteractionSession_Changed));

    public static readonly DependencyProperty SpellingPreferencesProperty =
        DependencyProperty.Register(
            nameof(SpellingPreferences),
            typeof(EditorPreferencesViewModel),
            typeof(SlateTextEditor),
            new PropertyMetadata(null, SpellingPreferences_Changed));

    private AvalonHighlightingCoordinator? _highlighting;
    private AvalonSpellingCoordinator? _spelling;
    private EditorInteractionCoordinator? _attachedInteraction;

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

    public EditorInteractionCoordinator? InteractionSession
    {
        get => (EditorInteractionCoordinator?)GetValue(InteractionSessionProperty);
        set => SetValue(InteractionSessionProperty, value);
    }

    public EditorPreferencesViewModel? SpellingPreferences
    {
        get => (EditorPreferencesViewModel?)GetValue(SpellingPreferencesProperty);
        set => SetValue(SpellingPreferencesProperty, value);
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
            AttachSpelling();
        }
    }

    protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control
            && TryOffsetAt(e.GetPosition(this), out int offset))
        {
            CaretOffset = offset;
            if (InteractionSession?.ActivateAt(
                    offset,
                    EditorInteractionOrigin.Pointer) == true)
            {
                e.Handled = true;
                return;
            }
        }

        base.OnPreviewMouseLeftButtonDown(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (TryOffsetAt(e.GetPosition(this), out int offset))
        {
            InteractionSession?.HoverAt(offset);
        }
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        InteractionSession?.ClearCitationHover();
    }
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        ModifierKeys modifiers = Keyboard.Modifiers;
        if (modifiers == ModifierKeys.Control && e.Key == Key.E)
        {
            InteractionSession?.PreviewEmbedAt(CaretOffset);
            e.Handled = true;
            return;
        }

        if (modifiers == ModifierKeys.Control
            && e.Key == Key.Enter
            && InteractionSession?.ActivateAt(
                CaretOffset,
                EditorInteractionOrigin.Keyboard) == true)
        {
            e.Handled = true;
            return;
        }

        if (modifiers == ModifierKeys.None
            && e.Key == Key.Escape
            && InteractionSession?.IsPopoverOpen == true)
        {
            InteractionSession.ClosePopoverCommand.Execute(null);
            e.Handled = true;
            return;
        }

        base.OnPreviewKeyDown(e);
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

    private static void InteractionSession_Changed(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs eventArgs)
    {
        var editor = (SlateTextEditor)dependencyObject;
        if (editor.IsLoaded)
        {
            editor.AttachInteractions();
        }
    }

    private static void SpellingPreferences_Changed(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs eventArgs)
    {
        var editor = (SlateTextEditor)dependencyObject;
        if (editor.IsLoaded)
        {
            editor.AttachSpelling();
        }
    }

    private void SlateTextEditor_Loaded(object sender, RoutedEventArgs e)
    {
        AttachHighlighting();
        AttachSpelling();
        AttachInteractions();
    }

    private void SlateTextEditor_Unloaded(object sender, RoutedEventArgs e)
    {
        _highlighting?.Dispose();
        _highlighting = null;
        _spelling?.Dispose();
        _spelling = null;
        DetachInteractions();
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

    private void AttachSpelling()
    {
        _spelling?.Dispose();
        _spelling = null;
        if (SpellingPreferences is not null)
        {
            _spelling = new AvalonSpellingCoordinator(this, SpellingPreferences);
        }
    }

    private void AttachInteractions()
    {
        DetachInteractions();
        _attachedInteraction = InteractionSession;
        if (_attachedInteraction is null)
        {
            return;
        }

        _attachedInteraction.FocusRequested += Interaction_FocusRequested;
        _attachedInteraction.CaretNavigationRequested += Interaction_CaretNavigationRequested;
        ApplyPendingCaret();
    }

    private void DetachInteractions()
    {
        if (_attachedInteraction is null)
        {
            return;
        }

        _attachedInteraction.FocusRequested -= Interaction_FocusRequested;
        _attachedInteraction.CaretNavigationRequested -= Interaction_CaretNavigationRequested;
        _attachedInteraction = null;
    }

    private void Interaction_FocusRequested(object? sender, EventArgs e) => FocusInputOwner();

    private void Interaction_CaretNavigationRequested(object? sender, EventArgs e) =>
        ApplyPendingCaret();

    private void ApplyPendingCaret()
    {
        if (_attachedInteraction?.TryConsumePendingCaret(out int offset) != true)
        {
            return;
        }

        CaretOffset = Math.Clamp(offset, 0, Document.TextLength);
        ScrollToLine(Document.GetLineByOffset(CaretOffset).LineNumber);
        FocusInputOwner();
    }

    private bool TryOffsetAt(Point point, out int offset)
    {
        TextViewPosition? position = GetPositionFromPoint(point);
        if (position is null)
        {
            offset = 0;
            return false;
        }

        offset = Document.GetOffset(position.Value.Location);
        return true;
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