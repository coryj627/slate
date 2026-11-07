// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Concurrent;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;

namespace SlateWindows;

/// <summary>DS-16: a right-pane leaf's icon, the strip's at 20 and the More panels
/// menu's at 16. Most are a Segoe Fluent Icons glyph, some turned. The two the font
/// lacks (DSD19) are drawn: a filled outline on a 20 grid, cut where its strokes
/// cross, so nothing paints a gap colour over a hover or selection fill. Neither
/// names anything: the glyph is a presentation text block, and a shape has no
/// peer, so the row's own name is all a reader meets.</summary>
internal sealed class LeafIcon : Decorator
{
    /// <summary>The grid the drawn icons are laid out on, a glyph's em.</summary>
    internal const double Grid = 20;

    public static readonly DependencyProperty LeafProperty = DependencyProperty.Register(
        nameof(Leaf),
        typeof(WorkspaceLeafOption),
        typeof(LeafIcon),
        new PropertyMetadata(null, (element, _) => ((LeafIcon)element).Build()));

    public static readonly DependencyProperty IconSizeProperty = DependencyProperty.Register(
        nameof(IconSize),
        typeof(double),
        typeof(LeafIcon),
        new PropertyMetadata(Grid, (element, _) => ((LeafIcon)element).Build()));

    private static readonly ConcurrentDictionary<string, Geometry> Drawings = new(StringComparer.Ordinal);

    public WorkspaceLeafOption? Leaf
    {
        get => (WorkspaceLeafOption?)GetValue(LeafProperty);
        set => SetValue(LeafProperty, value);
    }

    public double IconSize
    {
        get => (double)GetValue(IconSizeProperty);
        set => SetValue(IconSizeProperty, value);
    }

    private void Build()
    {
        if (Leaf is not { } leaf)
        {
            Child = null;
            return;
        }

        double size = IconSize;
        if (leaf.Drawing is { } markup)
        {
            var path = new Path
            {
                Data = Drawings.GetOrAdd(markup, static text =>
                {
                    Geometry geometry = Geometry.Parse(text);
                    geometry.Freeze();
                    return geometry;
                }),
                Width = Grid,
                Height = Grid,
                Stretch = Stretch.None,
                LayoutTransform = new ScaleTransform(size / Grid, size / Grid),
                SnapsToDevicePixels = true,
            };
            _ = path.SetBinding(
                Shape.FillProperty,
                new Binding { Path = new PropertyPath(TextElement.ForegroundProperty), RelativeSource = RelativeSource.Self });
            Child = path;
            return;
        }

        var glyph = new AutomationPresentationTextBlock
        {
            Text = leaf.Glyph,
            FontSize = size,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            RenderTransformOrigin = new Point(0.5, 0.5),
        };
        glyph.SetResourceReference(TextBlock.FontFamilyProperty, "SymbolThemeFontFamily");
        if (leaf.GlyphTurn != 0)
        {
            glyph.RenderTransform = new RotateTransform(leaf.GlyphTurn);
        }

        Child = glyph;
    }
}

/// <summary>DSD19: the two icons drawn for Slate, as WPF path markup on a 20 grid.
/// They come from the owner's picks, built by widening the strokes at Segoe Fluent
/// Icons' 1.1 and combining them; the build is in the DS-16 PR.</summary>
internal static class LeafDrawings
{
    /// <summary>Two interlocking links on the diagonal, each passing over the other
    /// once, with an arrow coming in at the lower left.</summary>
    internal const string Backlinks =
        "F1M8.6,7.27L8.59,7.36 8.57,7.72 8.59,8.07 8.64,8.43 8.65,8.47 6.77,10.35 6.63,10.5 6.51,10.67 6.4,10.84 6.32,11.02 6.25,11.21 6.21,11.4 6.18,11.59 6.17,11.79 6.18,11.99 6.21,12.18 6.25,12.37 6.32,12.56 6.4,12.74 6.43,12.79 6.61,12.61C6.72,12.5 6.86,12.45 7,12.45 7.14,12.45 7.28,12.5 7.39,12.61 7.6,12.83 7.6,13.17 7.39,13.39L7.21,13.57 7.26,13.6 7.44,13.68 7.63,13.75 7.82,13.79 8.01,13.82 8.21,13.83 8.41,13.82 8.6,13.79 8.79,13.75 8.98,13.68 9.16,13.6 9.33,13.49 9.5,13.37 9.65,13.23 11.53,11.35 11.57,11.36 11.93,11.41 12.28,11.43 12.64,11.41 12.73,11.4 10.27,13.85 10.05,14.05 9.82,14.23 9.57,14.38 9.31,14.5 9.04,14.59 8.77,14.66 8.49,14.7 8.21,14.71 7.93,14.7 7.65,14.66 7.38,14.59 7.11,14.5 6.85,14.38 6.6,14.23 6.57,14.21 2.93,17.85 6.2,17.85C6.5,17.85 6.75,18.1 6.75,18.4 6.75,18.7 6.5,18.95 6.2,18.95L1.6,18.95 1.6,18.95C1.56,18.95,1.53,18.95,1.49,18.94L1.49,18.94 1.49,18.94 1.45,18.93 1.39,18.91 1.39,18.91 1.39,18.91 1.34,18.88 1.3,18.86 1.29,18.86 1.29,18.86C1.23,18.82,1.18,18.77,1.14,18.71L1.14,18.71 1.14,18.7 1.12,18.66 1.09,18.61 1.09,18.61 1.09,18.61 1.07,18.55 1.06,18.51 1.06,18.51 1.06,18.51 1.05,18.4 1.05,18.4 1.05,13.8C1.05,13.5 1.3,13.25 1.6,13.25 1.9,13.25 2.15,13.5 2.15,13.8L2.15,17.07 5.79,13.43 5.77,13.4 5.62,13.15 5.5,12.89 5.41,12.62 5.34,12.35 5.3,12.07 5.29,11.79 5.3,11.51 5.34,11.23 5.41,10.96 5.5,10.69 5.62,10.43 5.77,10.18 5.95,9.95 6.15,9.73 8.6,7.27z M15.79,1.29L16.07,1.3 16.35,1.34 16.62,1.41 16.89,1.5 17.15,1.62 17.4,1.77 17.63,1.95 17.85,2.15 18.05,2.37 18.23,2.6 18.38,2.85 18.5,3.11 18.59,3.38 18.66,3.65 18.7,3.93 18.71,4.21 18.7,4.49 18.66,4.77 18.59,5.04 18.5,5.31 18.38,5.57 18.23,5.82 18.05,6.05 17.85,6.27 15.4,8.73 15.41,8.64 15.43,8.28 15.42,8.02 15.14,7.75 17.23,5.65 17.37,5.5 17.49,5.33 17.6,5.16 17.68,4.98 17.75,4.79 17.79,4.6 17.82,4.41 17.83,4.21 17.82,4.01 17.79,3.82 17.75,3.63 17.68,3.44 17.6,3.26 17.49,3.09 17.37,2.92 17.23,2.77 17.08,2.63 16.91,2.51 16.74,2.4 16.56,2.32 16.37,2.25 16.18,2.21 15.99,2.18 15.79,2.17 15.59,2.18 15.4,2.21 15.21,2.25 15.02,2.32 14.84,2.4 14.67,2.51 14.5,2.63 14.35,2.77 10.84,6.27 10.7,6.43 10.58,6.6 10.48,6.77 10.39,6.95 10.33,7.14 10.28,7.33 10.25,7.52 10.24,7.72 10.25,7.91 10.28,8.11 10.33,8.3 10.39,8.49 10.48,8.67 10.58,8.84 10.7,9 10.84,9.16 11,9.3 11.16,9.42 11.33,9.52 11.51,9.61 11.7,9.67 11.89,9.72 12.09,9.75 12.28,9.76 12.48,9.75 12.67,9.72 12.86,9.67 13.05,9.61 13.12,9.57 13.21,9.67 13.3,9.57 13.42,9.4 13.52,9.23 13.61,9.05 13.67,8.86 13.72,8.67 13.75,8.48 13.76,8.28 13.75,8.09 13.72,7.89 13.71,7.87 13.67,7.7 13.61,7.51 13.52,7.33 13.42,7.16 13.3,7 13.16,6.84 13,6.7 12.84,6.58 12.67,6.48 12.49,6.39 12.3,6.33 12.11,6.28 11.97,6.26 12.7,5.54 12.82,5.58 13.08,5.7 13.32,5.84 13.56,6.02 13.78,6.22 13.98,6.44 14.16,6.68 14.3,6.92 14.42,7.17 14.42,7.18 14.52,7.45 14.58,7.73 14.62,8 14.64,8.28 14.62,8.56 14.58,8.84 14.52,9.11 14.42,9.38 14.3,9.64 14.16,9.89 13.98,10.13 13.84,10.29 13.77,10.23 13.64,10.3 13.38,10.42 13.11,10.52 12.84,10.58 12.56,10.62 12.28,10.64 12,10.62 11.73,10.58 11.45,10.52 11.18,10.42 10.92,10.3 10.68,10.16 10.44,9.98 10.22,9.78 10.02,9.56 9.84,9.32 9.7,9.08 9.58,8.82 9.48,8.55 9.42,8.27 9.38,8 9.36,7.72 9.38,7.44 9.42,7.16 9.48,6.89 9.58,6.62 9.7,6.36 9.84,6.11 10.02,5.87 10.22,5.65 13.73,2.15 13.95,1.95 14.18,1.77 14.43,1.62 14.69,1.5 14.96,1.41 15.23,1.34 15.51,1.3 15.79,1.29z";

    /// <summary>A flowchart: a step, a decision and its two outcomes.</summary>
    internal const string Diagrams =
        "F1M13.8,14.95L13.71,14.96 13.63,14.99 13.55,15.03 13.48,15.08 13.43,15.15 13.39,15.23 13.36,15.31 13.35,15.4 13.35,17.6 13.36,17.69 13.39,17.77 13.43,17.85 13.48,17.92 13.55,17.97 13.63,18.01 13.71,18.04 13.8,18.05 17.8,18.05 17.89,18.04 17.97,18.01 18.05,17.97 18.12,17.92 18.17,17.85 18.21,17.77 18.24,17.69 18.25,17.6 18.25,15.4 18.24,15.31 18.21,15.23 18.17,15.15 18.12,15.08 18.05,15.03 17.97,14.99 17.89,14.96 17.8,14.95 15.8,14.95 13.8,14.95z M2.2,14.95L2.11,14.96 2.03,14.99 1.95,15.03 1.88,15.08 1.83,15.15 1.79,15.23 1.76,15.31 1.75,15.4 1.75,17.6 1.76,17.69 1.79,17.77 1.83,17.85 1.88,17.92 1.95,17.97 2.03,18.01 2.11,18.04 2.2,18.05 6.2,18.05 6.29,18.04 6.37,18.01 6.45,17.97 6.52,17.92 6.57,17.85 6.61,17.77 6.64,17.69 6.65,17.6 6.65,15.4 6.64,15.31 6.61,15.23 6.57,15.15 6.52,15.08 6.45,15.03 6.37,14.99 6.29,14.96 6.2,14.95 4.2,14.95 2.2,14.95z M10,7.78L7.78,10 10,12.22 12.22,10 10,7.78z M7,1.75L6.91,1.76 6.83,1.79 6.75,1.83 6.68,1.88 6.63,1.95 6.59,2.03 6.56,2.11 6.55,2.2 6.55,4.4 6.56,4.49 6.59,4.57 6.63,4.65 6.68,4.72 6.75,4.77 6.83,4.81 6.91,4.84 7,4.85 10,4.85 13,4.85 13.09,4.84 13.17,4.81 13.25,4.77 13.32,4.72 13.37,4.65 13.41,4.57 13.44,4.49 13.45,4.4 13.45,2.2 13.44,2.11 13.41,2.03 13.37,1.95 13.32,1.88 13.25,1.83 13.17,1.79 13.09,1.76 13,1.75 7,1.75z M7,0.65L13,0.65 13.31,0.68 13.6,0.77 13.87,0.92 14.1,1.1 14.28,1.33 14.43,1.6 14.52,1.89 14.55,2.2 14.55,4.4 14.52,4.71 14.43,5 14.28,5.27 14.1,5.5 13.87,5.68 13.6,5.83 13.31,5.92 13,5.95 10.55,5.95 10.55,6.77 13.23,9.45 15.8,9.45C16.1,9.45,16.35,9.7,16.35,10L16.35,13.85 17.8,13.85 18.11,13.88 18.4,13.97 18.67,14.12 18.9,14.3 19.08,14.53 19.23,14.8 19.32,15.09 19.35,15.4 19.35,17.6 19.32,17.91 19.23,18.2 19.08,18.47 18.9,18.7 18.67,18.88 18.4,19.03 18.11,19.12 17.8,19.15 13.8,19.15 13.49,19.12 13.2,19.03 12.93,18.88 12.7,18.7 12.52,18.47 12.37,18.2 12.28,17.91 12.25,17.6 12.25,15.4 12.28,15.09 12.37,14.8 12.52,14.53 12.7,14.3 12.93,14.12 13.2,13.97 13.49,13.88 13.8,13.85 15.25,13.85 15.25,10.55 13.23,10.55 10.39,13.39C10.17,13.6,9.83,13.6,9.61,13.39L6.77,10.55 4.75,10.55 4.75,13.85 6.2,13.85 6.51,13.88 6.8,13.97 7.07,14.12 7.3,14.3 7.48,14.53 7.63,14.8 7.72,15.09 7.75,15.4 7.75,17.6 7.72,17.91 7.63,18.2 7.48,18.47 7.3,18.7 7.07,18.88 6.8,19.03 6.51,19.12 6.2,19.15 2.2,19.15 1.89,19.12 1.6,19.03 1.33,18.88 1.1,18.7 0.92,18.47 0.77,18.2 0.68,17.91 0.65,17.6 0.65,15.4 0.68,15.09 0.77,14.8 0.92,14.53 1.1,14.3 1.33,14.12 1.6,13.97 1.89,13.88 2.2,13.85 3.65,13.85 3.65,10C3.65,9.7,3.9,9.45,4.2,9.45L6.77,9.45 9.45,6.77 9.45,5.95 7,5.95 6.69,5.92 6.4,5.83 6.13,5.68 5.9,5.5 5.72,5.27 5.57,5 5.48,4.71 5.45,4.4 5.45,2.2 5.48,1.89 5.57,1.6 5.72,1.33 5.9,1.1 6.13,0.92 6.4,0.77 6.69,0.68 7,0.65z";
}