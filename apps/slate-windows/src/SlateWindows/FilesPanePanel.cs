// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Controls;

namespace SlateWindows;

/// <summary>#1795: the Files pane's toolbar, actions and tree, in that order, which is
/// their tab order too. The toolbar is at the top and the actions at the bottom. The
/// actions take at most half of what the toolbar leaves, so a short window can't take
/// all of it from the tree, and they scroll inside that.</summary>
internal sealed class FilesPanePanel : Panel
{
    private const double ActionsTallest = 210;

    protected override Size MeasureOverride(Size availableSize)
    {
        UIElement toolbar = InternalChildren[0];
        UIElement actions = InternalChildren[1];
        UIElement tree = InternalChildren[2];
        toolbar.Measure(availableSize);
        double room = Math.Max(0, availableSize.Height - toolbar.DesiredSize.Height);
        actions.Measure(new Size(availableSize.Width, Math.Min(ActionsTallest, room / 2)));
        tree.Measure(new Size(availableSize.Width, Math.Max(0, room - actions.DesiredSize.Height)));
        return new Size(
            Math.Max(toolbar.DesiredSize.Width, Math.Max(actions.DesiredSize.Width, tree.DesiredSize.Width)),
            toolbar.DesiredSize.Height + actions.DesiredSize.Height + tree.DesiredSize.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        UIElement toolbar = InternalChildren[0];
        UIElement actions = InternalChildren[1];
        UIElement tree = InternalChildren[2];
        double toolbarHeight = toolbar.DesiredSize.Height;
        double actionsTop = Math.Max(toolbarHeight, finalSize.Height - actions.DesiredSize.Height);
        toolbar.Arrange(new Rect(0, 0, finalSize.Width, toolbarHeight));
        tree.Arrange(new Rect(0, toolbarHeight, finalSize.Width, actionsTop - toolbarHeight));
        actions.Arrange(new Rect(0, actionsTop, finalSize.Width, actions.DesiredSize.Height));
        return finalSize;
    }
}
