// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace SlateWindows;

internal sealed record EditorEmbedPreviewPart(
    string? Text,
    EditorEmbedPreviewNode? Nested);

internal sealed record EditorEmbedPreviewNode(
    string Title,
    IReadOnlyList<EditorEmbedPreviewPart> Parts,
    ImageSource? Image,
    string? SourcePath,
    bool IsDisclosure,
    bool InitiallyExpanded,
    bool IsWarning);

/// <summary>
/// Recursive native disclosure renderer for the core EmbedResolution tree.
/// </summary>
internal sealed class EditorEmbedPreviewView : ContentControl
{
    public static readonly DependencyProperty RootProperty =
        DependencyProperty.Register(
            nameof(Root),
            typeof(EditorEmbedPreviewNode),
            typeof(EditorEmbedPreviewView),
            new PropertyMetadata(null, PreviewProperty_Changed));

    public static readonly DependencyProperty InteractionSessionProperty =
        DependencyProperty.Register(
            nameof(InteractionSession),
            typeof(EditorInteractionCoordinator),
            typeof(EditorEmbedPreviewView),
            new PropertyMetadata(null));

    public EditorEmbedPreviewNode? Root
    {
        get => (EditorEmbedPreviewNode?)GetValue(RootProperty);
        set => SetValue(RootProperty, value);
    }

    public EditorInteractionCoordinator? InteractionSession
    {
        get => (EditorInteractionCoordinator?)GetValue(InteractionSessionProperty);
        set => SetValue(InteractionSessionProperty, value);
    }

    private static void PreviewProperty_Changed(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs eventArgs) =>
        ((EditorEmbedPreviewView)dependencyObject).Rebuild();

    private void Rebuild() =>
        Content = Root is null ? null : BuildNode(Root);

    private FrameworkElement BuildNode(EditorEmbedPreviewNode node)
    {
        FrameworkElement content = BuildNodeContent(node);
        if (!node.IsDisclosure)
        {
            AutomationProperties.SetName(content, node.Title);
            return content;
        }

        var expander = new Expander
        {
            Header = node.Title,
            IsExpanded = node.InitiallyExpanded,
            Content = content,
            Margin = new Thickness(0, 4, 0, 4),
        };
        AutomationProperties.SetName(expander, node.Title);
        return expander;
    }

    private FrameworkElement BuildNodeContent(EditorEmbedPreviewNode node)
    {
        var panel = new StackPanel
        {
            Margin = node.IsDisclosure
                ? new Thickness(12, 4, 0, 4)
                : new Thickness(0, 4, 0, 4),
        };
        if (node.IsWarning)
        {
            var warning = new TextBlock
            {
                Text = node.Title,
                TextWrapping = TextWrapping.Wrap,
            };
            AutomationProperties.SetName(warning, node.Title);
            panel.Children.Add(warning);
        }

        if (node.Image is not null)
        {
            var image = new Image
            {
                Source = node.Image,
                MaxWidth = 560,
                MaxHeight = 240,
                Stretch = Stretch.Uniform,
            };
            AutomationProperties.SetName(image, node.Title);
            panel.Children.Add(image);
        }

        foreach (EditorEmbedPreviewPart part in node.Parts)
        {
            if (!string.IsNullOrEmpty(part.Text))
            {
                var text = new TextBox
                {
                    Text = part.Text,
                    IsReadOnly = true,
                    AcceptsReturn = true,
                    TextWrapping = TextWrapping.Wrap,
                    BorderThickness = new Thickness(0),
                    Background = Brushes.Transparent,
                    Padding = new Thickness(0),
                    Margin = new Thickness(0, 2, 0, 2),
                };
                AutomationProperties.SetName(text, "Embedded content");
                panel.Children.Add(text);
            }
            if (part.Nested is not null)
            {
                panel.Children.Add(BuildNode(part.Nested));
            }
        }

        if (node.SourcePath is { Length: > 0 } sourcePath)
        {
            var open = new Button
            {
                Content = "Jump to source",
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 4, 0, 0),
            };
            AutomationProperties.SetName(open, $"Jump to source: {sourcePath}");
            open.Click += (_, _) => InteractionSession?.OpenEmbedSource(sourcePath);
            panel.Children.Add(open);
        }
        return panel;
    }
}
