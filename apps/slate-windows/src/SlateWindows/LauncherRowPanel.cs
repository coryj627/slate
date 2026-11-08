// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Controls;

namespace SlateWindows;

/// <summary>DS-19: a launcher row's label and chord (its first and second child). Side by
/// side, the chord at the right edge, when both fit on one line; otherwise the chord goes
/// under the label, so a narrow pane or the largest text size cuts neither. The row grows
/// with what it holds, as DS-5's rows do.</summary>
internal sealed class LauncherRowPanel : Panel
{
    public static readonly DependencyProperty GapProperty = DependencyProperty.Register(
        nameof(Gap), typeof(double), typeof(LauncherRowPanel),
        new FrameworkPropertyMetadata(16.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    /// <summary>The space between the label and the chord when they share a line.</summary>
    public double Gap
    {
        get => (double)GetValue(GapProperty);
        set => SetValue(GapProperty, value);
    }

    /// <summary>A launcher action's icon (a Segoe Fluent Icons code point), set on its
    /// button and drawn before its label as presentation: the button's name is its label.</summary>
    public static readonly DependencyProperty GlyphProperty = DependencyProperty.RegisterAttached(
        "Glyph", typeof(string), typeof(LauncherRowPanel), new FrameworkPropertyMetadata(string.Empty));

    public static string GetGlyph(DependencyObject element) => (string)element.GetValue(GlyphProperty);

    public static void SetGlyph(DependencyObject element, string value) => element.SetValue(GlyphProperty, value);

    /// <summary>Whether the last measure put the chord under the label.</summary>
    internal bool IsStacked { get; private set; }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (InternalChildren.Count != 2)
        {
            Size union = default;
            foreach (UIElement child in InternalChildren)
            {
                child.Measure(availableSize);
                union = new Size(Math.Max(union.Width, child.DesiredSize.Width), Math.Max(union.Height, child.DesiredSize.Height));
            }

            return union;
        }

        UIElement label = InternalChildren[0];
        UIElement chord = InternalChildren[1];
        var unbounded = new Size(double.PositiveInfinity, double.PositiveInfinity);
        label.Measure(unbounded);
        chord.Measure(unbounded);
        double inline = label.DesiredSize.Width + Gap + chord.DesiredSize.Width;
        IsStacked = inline > availableSize.Width;
        if (!IsStacked)
        {
            return new Size(inline, Math.Max(label.DesiredSize.Height, chord.DesiredSize.Height));
        }

        var column = new Size(availableSize.Width, double.PositiveInfinity);
        label.Measure(column);
        chord.Measure(column);
        return new Size(
            Math.Max(label.DesiredSize.Width, chord.DesiredSize.Width),
            label.DesiredSize.Height + chord.DesiredSize.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (InternalChildren.Count != 2)
        {
            foreach (UIElement child in InternalChildren)
            {
                child.Arrange(new Rect(finalSize));
            }

            return finalSize;
        }

        UIElement label = InternalChildren[0];
        UIElement chord = InternalChildren[1];
        Size labelSize = label.DesiredSize;
        Size chordSize = chord.DesiredSize;
        if (IsStacked)
        {
            label.Arrange(new Rect(0, 0, finalSize.Width, labelSize.Height));
            chord.Arrange(new Rect(0, labelSize.Height, Math.Min(chordSize.Width, finalSize.Width), chordSize.Height));
        }
        else
        {
            double chordLeft = Math.Max(0, finalSize.Width - chordSize.Width);
            label.Arrange(new Rect(0, (finalSize.Height - labelSize.Height) / 2, Math.Max(0, chordLeft - Gap), labelSize.Height));
            chord.Arrange(new Rect(chordLeft, (finalSize.Height - chordSize.Height) / 2, chordSize.Width, chordSize.Height));
        }

        return finalSize;
    }
}
