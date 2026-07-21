// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace SlateWindows.Tests;

public sealed class W1ShellAccessibilityContractTests
{
    [Fact]
    public void WorkspaceLandmarks_CreateNamedPanePeersInTheControlTree() =>
        RunOnStaThread(
            () =>
            {
                (FrameworkElement Element, string Id, string Name)[] landmarks =
                [
                    (new AutomationLandmarkGrid(), "WelcomeView", "Welcome"),
                    (new AutomationLandmarkGrid(), "WorkspaceView", "Workspace"),
                    (new AutomationLandmarkBorder(), "FilesPane", "Files"),
                    (new AutomationLandmarkBorder(), "ContentPane", "Editor workspace"),
                    (new AutomationLandmarkBorder(), "InspectorPane", "Inspector"),
                    (new AutomationLandmarkBorder(), "QuickSwitcher", "Quick Open"),
                ];

                foreach ((FrameworkElement element, string id, string name) in landmarks)
                {
                    AutomationProperties.SetAutomationId(element, id);
                    AutomationProperties.SetName(element, name);

                    AutomationPeer peer = Assert.IsType<AutomationLandmarkPeer>(
                        UIElementAutomationPeer.CreatePeerForElement(element));
                    Assert.Equal(AutomationControlType.Pane, peer.GetAutomationControlType());
                    Assert.Equal(id, peer.GetAutomationId());
                    Assert.Equal(name, peer.GetName());
                    Assert.True(peer.IsControlElement());
                    Assert.False(peer.IsContentElement());

                    element.Visibility = Visibility.Collapsed;
                    Assert.False(peer.IsControlElement());
                }
            },
            "Landmark peer test timed out.");

    [Fact]
    public void WeightedSplitPanel_RearrangesWhenAChildWeightChanges() =>
        RunOnStaThread(
            () =>
            {
                var firstNode = new WorkspacePaneNodeViewModel("horizontal");
                var secondNode = new WorkspacePaneNodeViewModel("horizontal");
                var first = new Border { DataContext = firstNode };
                var second = new Border { DataContext = secondNode };
                var panel = new WeightedSplitPanel
                {
                    Orientation = Orientation.Horizontal,
                };
                panel.Children.Add(first);
                panel.Children.Add(second);

                var size = new Size(300, 100);
                panel.Measure(size);
                panel.Arrange(new Rect(size));
                Assert.Equal(150, first.RenderSize.Width, precision: 5);
                Assert.Equal(150, second.RenderSize.Width, precision: 5);

                firstNode.Weight = 0.25;
                Assert.False(panel.IsArrangeValid);
                panel.Arrange(new Rect(size));
                Assert.Equal(60, first.RenderSize.Width, precision: 5);
                Assert.Equal(240, second.RenderSize.Width, precision: 5);
            },
            "Split layout test timed out.");

    [Fact]
    public void WeightedSplitPanel_DragHandleResizesAdjacentPanes() =>
        RunOnStaThread(
            () =>
            {
                var firstNode = new WorkspacePaneNodeViewModel("horizontal");
                var secondNode = new WorkspacePaneNodeViewModel("horizontal");
                firstNode.Weight = 0.5;
                secondNode.Weight = 0.5;
                var first = new ContentControl { DataContext = firstNode };
                var handle = new Thumb { DataContext = secondNode };
                var second = new ContentControl
                {
                    DataContext = secondNode,
                    Content = handle,
                };
                var panel = new WeightedSplitPanel
                {
                    Orientation = Orientation.Horizontal,
                };
                panel.Children.Add(first);
                panel.Children.Add(second);

                var size = new Size(300, 100);
                panel.Measure(size);
                panel.Arrange(new Rect(size));
                handle.RaiseEvent(new DragDeltaEventArgs(30, 0)
                {
                    RoutedEvent = Thumb.DragDeltaEvent,
                });
                panel.Arrange(new Rect(size));

                Assert.Equal(180, first.RenderSize.Width, precision: 5);
                Assert.Equal(120, second.RenderSize.Width, precision: 5);
            },
            "Split handle test timed out.");

    private static void RunOnStaThread(Action action, string timeoutMessage)
    {
        ExceptionDispatchInfo? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                failure = ExceptionDispatchInfo.Capture(exception);
            }
        })
        {
            IsBackground = true,
            Name = "w1-shell-sta-test",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), timeoutMessage);
        failure?.Throw();
    }
}
