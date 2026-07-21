// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace SlateWindows;

/// <summary>
/// Layout containers do not create WPF automation peers by default. These
/// variants preserve Grid/Border layout behavior while exposing named panes
/// as stable landmarks in the UI Automation control tree.
/// </summary>
internal sealed class AutomationLandmarkGrid : Grid
{
    protected override AutomationPeer OnCreateAutomationPeer() =>
        new AutomationLandmarkPeer(this);
}

internal sealed class AutomationLandmarkBorder : Border
{
    protected override AutomationPeer OnCreateAutomationPeer() =>
        new AutomationLandmarkPeer(this);
}

/// <summary>
/// Keeps a collapsed list out of the UI Automation control tree even when a
/// provider retains its peer after the surrounding popup has closed.
/// </summary>
internal sealed class AutomationVisibilityListBox : ListBox
{
    protected override AutomationPeer OnCreateAutomationPeer() =>
        new AutomationVisibilityListBoxPeer(this);
}

internal sealed class AutomationVisibilityListBoxPeer(AutomationVisibilityListBox owner)
    : ListBoxAutomationPeer(owner)
{
    protected override bool IsControlElementCore() =>
        Owner is UIElement { Visibility: Visibility.Visible }
        && base.IsControlElementCore();

    protected override bool IsContentElementCore() =>
        Owner is UIElement { Visibility: Visibility.Visible }
        && base.IsContentElementCore();
}

/// <summary>
/// Visual text whose accessible name and help text are supplied by its parent
/// control. Excluding the duplicate peer also prevents detached item-template
/// text from lingering in the UI Automation control tree.
/// </summary>
internal sealed class AutomationPresentationTextBlock : TextBlock
{
    protected override AutomationPeer OnCreateAutomationPeer() =>
        new AutomationPresentationTextBlockPeer(this);
}

internal sealed class AutomationPresentationTextBlockPeer(AutomationPresentationTextBlock owner)
    : TextBlockAutomationPeer(owner)
{
    protected override bool IsControlElementCore() => false;

    protected override bool IsContentElementCore() => false;
}

internal sealed class AutomationLandmarkPeer(FrameworkElement owner)
    : FrameworkElementAutomationPeer(owner)
{
    protected override AutomationControlType GetAutomationControlTypeCore() =>
        AutomationControlType.Pane;

    protected override string GetClassNameCore() => "SlateLandmark";

    protected override bool IsControlElementCore() =>
        Owner is UIElement { Visibility: Visibility.Visible };

    protected override bool IsContentElementCore() => false;
}
