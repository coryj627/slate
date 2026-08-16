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
/// A named, focusable row host. A plain Border creates NO automation
/// peer, so a composed row name set on one is silently dropped from
/// UIA (the W4-5 lesson that produced AutomationLandmarkBorder). This
/// variant exposes ONE Group element carrying the composed name and
/// raises focus events when the row takes keyboard focus; visual text
/// inside the row rides AutomationPresentationTextBlock so the host
/// stays the single accessible stop.
/// </summary>
internal sealed class AutomationNamedRowBorder : Border
{
    protected override AutomationPeer OnCreateAutomationPeer() =>
        new AutomationNamedRowPeer(this);
}

internal sealed class AutomationNamedRowPeer : FrameworkElementAutomationPeer
{
    internal AutomationNamedRowPeer(FrameworkElement owner)
        : base(owner)
    {
    }

    protected override AutomationControlType GetAutomationControlTypeCore() =>
        AutomationControlType.Group;

    protected override string GetClassNameCore() => "SlateRow";

    protected override bool IsControlElementCore() =>
        Owner is UIElement { Visibility: Visibility.Visible };

    protected override bool IsContentElementCore() =>
        Owner is UIElement { Visibility: Visibility.Visible };
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

internal sealed class AutomationVisibilityListBoxPeer : ListBoxAutomationPeer
{
    internal AutomationVisibilityListBoxPeer(AutomationVisibilityListBox owner)
        : base(owner)
    {
    }

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

internal sealed class AutomationPresentationTextBlockPeer : TextBlockAutomationPeer
{
    internal AutomationPresentationTextBlockPeer(AutomationPresentationTextBlock owner)
        : base(owner)
    {
    }

    protected override bool IsControlElementCore() => false;

    protected override bool IsContentElementCore() => false;
}

/// <summary>
/// An items host whose children are presentation-only (the search
/// result row's emphasis runs). WPF gives a plain <see cref="ItemsControl"/>
/// an automation peer that publishes an unnamed List element, which put
/// a nameless stop inside every search result row — the extra stop
/// contract S4 forbids and the W5-2 close-out journey caught.
/// Suppressing the wrapper peer removes the host from the control and
/// content views; its <see cref="AutomationPresentationTextBlock"/>
/// children were already suppressed.
/// </summary>
internal sealed class AutomationPresentationItemsControl : ItemsControl
{
    protected override AutomationPeer OnCreateAutomationPeer() =>
        new AutomationPresentationItemsControlPeer(this);
}

internal sealed class AutomationPresentationItemsControlPeer : FrameworkElementAutomationPeer
{
    internal AutomationPresentationItemsControlPeer(AutomationPresentationItemsControl owner)
        : base(owner)
    {
    }

    protected override bool IsControlElementCore() => false;

    protected override bool IsContentElementCore() => false;
}

internal sealed class AutomationLandmarkPeer : FrameworkElementAutomationPeer
{
    internal AutomationLandmarkPeer(FrameworkElement owner)
        : base(owner)
    {
    }

    protected override AutomationControlType GetAutomationControlTypeCore() =>
        AutomationControlType.Pane;

    protected override string GetClassNameCore() => "SlateLandmark";

    protected override bool IsControlElementCore() =>
        Owner is UIElement { Visibility: Visibility.Visible };

    protected override bool IsContentElementCore() => false;
}

/// <summary>
/// Focus-token ancestry that survives non-Visual focus (#742, codex
/// round 7). <see cref="System.Windows.Media.VisualTreeHelper"/> throws
/// for a <see cref="System.Windows.FrameworkContentElement"/> — a
/// focused reading-view Hyperlink is one — so content elements walk
/// their logical parent until the tree re-enters visuals.
/// </summary>
internal static class FocusAncestry
{
    internal static System.Windows.DependencyObject? Parent(
        System.Windows.DependencyObject node) =>
        node is System.Windows.Media.Visual
            or System.Windows.Media.Media3D.Visual3D
            ? System.Windows.Media.VisualTreeHelper.GetParent(node)
            : System.Windows.LogicalTreeHelper.GetParent(node);
}
