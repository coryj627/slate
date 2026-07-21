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
