// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace SlateWindows;

/// <summary>Lightweight recursive layout panel for persisted workspace weights.</summary>
internal sealed class WeightedSplitPanel : Panel
{
    public WeightedSplitPanel()
    {
        AddHandler(Thumb.DragDeltaEvent, new DragDeltaEventHandler(Thumb_DragDelta));
        AddHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler(Thumb_DragCompleted));
        AddHandler(Keyboard.KeyDownEvent, new KeyEventHandler(Thumb_KeyDown));
    }

    public static readonly DependencyProperty OrientationProperty = DependencyProperty.Register(
        nameof(Orientation),
        typeof(Orientation),
        typeof(WeightedSplitPanel),
        new FrameworkPropertyMetadata(
            Orientation.Horizontal,
            FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));

    public Orientation Orientation
    {
        get => (Orientation)GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    protected override void OnVisualChildrenChanged(
        DependencyObject visualAdded,
        DependencyObject visualRemoved)
    {
        if (visualRemoved is FrameworkElement removed)
        {
            removed.DataContextChanged -= Child_DataContextChanged;
            ObserveNode(removed.DataContext, observe: false);
        }

        base.OnVisualChildrenChanged(visualAdded, visualRemoved);

        if (visualAdded is FrameworkElement added)
        {
            added.DataContextChanged += Child_DataContextChanged;
            ObserveNode(added.DataContext, observe: true);
        }

        InvalidateMeasure();
        InvalidateArrange();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (UIElement child in InternalChildren)
        {
            child.Measure(availableSize);
        }

        return availableSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (InternalChildren.Count == 0)
        {
            return finalSize;
        }

        double total = InternalChildren.Cast<FrameworkElement>()
            .Sum(child => EffectiveWeight(child.DataContext as WorkspacePaneNodeViewModel));
        double offset = 0;
        foreach (FrameworkElement child in InternalChildren.Cast<FrameworkElement>())
        {
            double weight = EffectiveWeight(child.DataContext as WorkspacePaneNodeViewModel) / total;
            if (Orientation == Orientation.Horizontal)
            {
                double width = finalSize.Width * weight;
                child.Arrange(new Rect(offset, 0, width, finalSize.Height));
                offset += width;
            }
            else
            {
                double height = finalSize.Height * weight;
                child.Arrange(new Rect(0, offset, finalSize.Width, height));
                offset += height;
            }
        }

        return finalSize;
    }

    private void Child_DataContextChanged(
        object sender,
        DependencyPropertyChangedEventArgs eventArgs)
    {
        ObserveNode(eventArgs.OldValue, observe: false);
        ObserveNode(eventArgs.NewValue, observe: true);
        InvalidateMeasure();
        InvalidateArrange();
    }

    private void ObserveNode(object? candidate, bool observe)
    {
        if (candidate is not INotifyPropertyChanged node)
        {
            return;
        }

        if (observe)
        {
            node.PropertyChanged += Node_PropertyChanged;
        }
        else
        {
            node.PropertyChanged -= Node_PropertyChanged;
        }
    }

    private void Node_PropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName == nameof(WorkspacePaneNodeViewModel.Weight))
        {
            InvalidateArrange();
        }
    }

    private void Thumb_DragDelta(object sender, DragDeltaEventArgs eventArgs)
    {
        if (eventArgs.OriginalSource is not Thumb { DataContext: WorkspacePaneNodeViewModel node })
        {
            return;
        }

        double pixels = Orientation == Orientation.Horizontal
            ? eventArgs.HorizontalChange
            : eventArgs.VerticalChange;
        AdjustBoundary(node, pixels);
        eventArgs.Handled = true;
    }

    private void Thumb_DragCompleted(object sender, DragCompletedEventArgs eventArgs)
    {
        WorkspacePaneNodeViewModel? node =
            (eventArgs.OriginalSource as Thumb)?.DataContext as WorkspacePaneNodeViewModel;
        PersistOwner(node);
        eventArgs.Handled = true;
    }

    private void Thumb_KeyDown(object sender, KeyEventArgs eventArgs)
    {
        if (eventArgs.OriginalSource is not Thumb { DataContext: WorkspacePaneNodeViewModel node }
            || Keyboard.Modifiers != ModifierKeys.None)
        {
            return;
        }

        double pixels = (Orientation, eventArgs.Key) switch
        {
            (Orientation.Horizontal, Key.Left) => -Math.Max(8, ActualWidth * 0.05),
            (Orientation.Horizontal, Key.Right) => Math.Max(8, ActualWidth * 0.05),
            (Orientation.Vertical, Key.Up) => -Math.Max(8, ActualHeight * 0.05),
            (Orientation.Vertical, Key.Down) => Math.Max(8, ActualHeight * 0.05),
            _ => 0,
        };
        if (pixels == 0)
        {
            return;
        }

        AdjustBoundary(node, pixels);
        PersistOwner(node);
        eventArgs.Handled = true;
    }

    private void AdjustBoundary(WorkspacePaneNodeViewModel current, double pixels)
    {
        WorkspacePaneNodeViewModel[] nodes = InternalChildren
            .Cast<FrameworkElement>()
            .Select(child => child.DataContext as WorkspacePaneNodeViewModel)
            .Where(node => node is not null)
            .Cast<WorkspacePaneNodeViewModel>()
            .ToArray();
        int index = Array.IndexOf(nodes, current);
        double extent = Orientation == Orientation.Horizontal ? ActualWidth : ActualHeight;
        if (index <= 0 || !double.IsFinite(extent) || extent <= 0)
        {
            return;
        }

        WorkspacePaneNodeViewModel previous = nodes[index - 1];
        double pairTotal = previous.Weight + current.Weight;
        if (pairTotal <= 0.30)
        {
            return;
        }

        double totalWeight = nodes.Sum(node => EffectiveWeight(node));
        double deltaWeight = pixels / extent * totalWeight;
        double previousWeight = Math.Clamp(
            previous.Weight + deltaWeight,
            0.15,
            pairTotal - 0.15);
        previous.Weight = previousWeight;
        current.Weight = pairTotal - previousWeight;
    }

    private void PersistOwner(WorkspacePaneNodeViewModel? resizedNode)
    {
        WorkspaceGroupViewModel? group = InternalChildren
            .Cast<FrameworkElement>()
            .Select(child => child.DataContext as WorkspacePaneNodeViewModel)
            .Where(node => node is not null)
            .SelectMany(EnumerateGroups)
            .FirstOrDefault();
        if (group?.Owner is WorkspaceViewModel owner)
        {
            owner.PersistLayoutWeights();
            if (resizedNode is not null)
            {
                owner.AnnouncePaneResize(resizedNode.Weight);
            }
        }
    }

    private static IEnumerable<WorkspaceGroupViewModel> EnumerateGroups(
        WorkspacePaneNodeViewModel? node)
    {
        if (node?.Group is WorkspaceGroupViewModel group)
        {
            yield return group;
            yield break;
        }

        if (node is null)
        {
            yield break;
        }

        foreach (WorkspacePaneNodeViewModel child in node.Children)
        {
            foreach (WorkspaceGroupViewModel descendant in EnumerateGroups(child))
            {
                yield return descendant;
            }
        }
    }

    private static double EffectiveWeight(WorkspacePaneNodeViewModel? node) =>
        node?.Weight is double weight && double.IsFinite(weight) && weight > 0
            ? weight
            : 1;
}
