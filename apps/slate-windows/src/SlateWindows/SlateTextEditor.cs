// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows.Automation.Peers;
using ICSharpCode.AvalonEdit;

namespace SlateWindows;

/// <summary>
/// Keeps AvalonEdit's Document/Value/Text automation contract while making
/// its public peer focus the internal TextArea that owns keyboard input.
/// </summary>
internal sealed class SlateTextEditor : TextEditor
{
    internal bool FocusInputOwner() => TextArea.Focus();

    protected override AutomationPeer OnCreateAutomationPeer() =>
        new SlateTextEditorAutomationPeer(this);
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