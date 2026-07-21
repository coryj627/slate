// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace SlateWindows.Tests;

public sealed class W1ShellAccessibilityContractTests
{
    [Fact]
    public void WeightedSplitPanel_RearrangesWhenAChildWeightChanges()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
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
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        Assert.True(thread.Join(TimeSpan.FromSeconds(10)), "Split layout test timed out.");
        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    [Fact]
    public void WeightedSplitPanel_DragHandleResizesAdjacentPanes()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
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
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        Assert.True(thread.Join(TimeSpan.FromSeconds(10)), "Split handle test timed out.");
        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
