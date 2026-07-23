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
    protected override AutomationPeer OnCreateAutomationPeer() =>
        new SlateTextEditorAutomationPeer(this);
}

internal sealed class SlateTextEditorAutomationPeer : TextEditorAutomationPeer
{
    private readonly SlateTextEditor _owner;

    internal SlateTextEditorAutomationPeer(SlateTextEditor owner)
        : base(owner)
    {
        _owner = owner;
    }

    protected override bool HasKeyboardFocusCore() =>
        _owner.TextArea.IsKeyboardFocusWithin;

    protected override bool IsKeyboardFocusableCore() =>
        _owner.TextArea.Focusable
        && _owner.TextArea.IsEnabled
        && _owner.TextArea.IsVisible;

    protected override void SetFocusCore()
    {
        if (!_owner.TextArea.Focus())
        {
            throw new InvalidOperationException("The editor TextArea could not receive keyboard focus.");
        }
    }
}