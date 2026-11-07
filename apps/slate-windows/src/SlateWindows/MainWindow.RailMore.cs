// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;

namespace SlateWindows;

/// <summary>DS-16 (DSD8): the rail's More panels button and its menu, which holds the
/// twelve leaves past the strip's five, and the rail's one landing.</summary>
public partial class MainWindow
{
    internal const string MorePanelsItemIdPrefix = "RightPaneMorePanels.";

    /// <summary>The menu while it's open, for the facts.</summary>
    internal ContextMenu? MorePanelsMenu { get; private set; }

    /// <summary>Opens More panels on the left of its button, built from the workspace
    /// that's live now, its open panel checked and holding the keys.</summary>
    private void RightPaneMorePanels_Click(object sender, RoutedEventArgs e) => OpenMorePanels();

    internal void OpenMorePanels()
    {
        // The keys go to the button first, so the menu's own restore brings them back
        // there however it was opened: a click, the keys, or a reader's Invoke from
        // elsewhere (Codex round 1).
        if (_viewModel.Workspace is not { } workspace
            || !RightPaneMorePanelsButton.IsVisible
            || !RightPaneMorePanelsButton.Focus())
        {
            return;
        }

        var menu = new ContextMenu
        {
            Style = (Style)FindResource("Slate.ContextMenuStyle"),
            PlacementTarget = RightPaneMorePanelsButton,
            Placement = PlacementMode.Left,
        };
        AutomationProperties.SetName(menu, "More panels");
        foreach (WorkspaceLeafOption leaf in WorkspaceViewModel.MoreLeaves)
        {
            var item = new CheckMenuItem
            {
                Header = leaf.Title,
                Icon = new LeafIcon { Leaf = leaf, IconSize = 16 },
                IsCheckable = true,
                InputGestureText = string.Equals(leaf.Id, "tasksReview", StringComparison.Ordinal) ? "Ctrl+R" : string.Empty,
            };
            _ = item.SetBinding(
                MenuItem.IsCheckedProperty,
                new Binding(nameof(WorkspaceViewModel.ActiveLeaf))
                {
                    Source = workspace,
                    Mode = BindingMode.OneWay,
                    Converter = IsLeaf.Instance,
                    ConverterParameter = leaf,
                });
            AutomationProperties.SetAutomationId(item, MorePanelsItemIdPrefix + leaf.Id);
            // WPF runs a menu's Click after it closes: a workspace replaced meanwhile
            // (a vault opened or closed) takes no pick meant for the old one.
            item.Click += (_, _) =>
            {
                if (ReferenceEquals(_viewModel.Workspace, workspace))
                {
                    PickMorePanel(workspace, leaf, menu);
                }
            };
            _ = menu.Items.Add(item);
        }

        menu.Opened += (_, _) =>
        {
            MenuItem? open = menu.Items.OfType<MenuItem>().FirstOrDefault(row => row.IsChecked)
                ?? menu.Items.OfType<MenuItem>().FirstOrDefault();
            _ = open?.Focus();
        };
        // Built fresh at each open and dropped at close; its rows' bindings watch the
        // workspace weakly, so nothing outlives it.
        menu.Closed += (_, _) =>
        {
            if (ReferenceEquals(MorePanelsMenu, menu))
            {
                MorePanelsMenu = null;
            }

            // The keys go back to the button by the menu's own restore, as any menu's
            // do (PickingFromMoreSwitchesThePanelOnceAndReturnsFocus).
        };
        MorePanelsMenu = menu;
        menu.IsOpen = true;
    }

    /// <summary>The same leaf switch as an arrow on the strip, without the hold to
    /// settle, and the menu closes, by a toggle as by a click. A pick of the open
    /// panel changes nothing and keeps its check.</summary>
    private static void PickMorePanel(WorkspaceViewModel workspace, WorkspaceLeafOption leaf, ContextMenu menu)
    {
        workspace.ActiveLeaf = leaf;
        foreach (MenuItem row in menu.Items.OfType<MenuItem>())
        {
            BindingOperations.GetBindingExpression(row, MenuItem.IsCheckedProperty)?.UpdateTarget();
        }

        menu.IsOpen = false;
    }

    /// <summary>The rail's one landing (F6, the boundary routes, the guards): the open
    /// panel's row, or More panels when the open panel lives in its menu.</summary>
    internal bool LandOnRail() =>
        _viewModel.Workspace is { StripLeaf: null } && RightPaneMorePanelsButton.IsVisible
            ? RightPaneMorePanelsButton.Focus()
            : SelectorFocus.FocusFirstOrSelectedItem(RightPaneLeavesList);

    /// <summary>Whether the bound leaf is the parameter's.</summary>
    private sealed class IsLeaf : IValueConverter
    {
        internal static readonly IsLeaf Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            Equals(value, parameter);

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            Binding.DoNothing;
    }
}
