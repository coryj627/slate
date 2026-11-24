// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
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

    /// <summary>Jump seam for hosts WITHOUT an editor interaction
    /// coordinator (the W4-2 embeds leaf): used when
    /// <see cref="InteractionSession"/> is null.</summary>
    public Action<string>? JumpToSource { get; set; }

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

        // Plan 47 CB-22b: the title as a TextBlock, not a string. Fluent's header button
        // reads an underscore in a string as an access-key marker: "my_notes.md" painted
        // and named "mynotes.md", and its letter taken on the window (Alt+N, Alt+B). DS-2:
        // the title wraps, so at 14 point in a narrow pane it doesn't run under the chevron.
        // Fluent's template ignores HeaderTemplate, and Fluent's TextBlock style doesn't
        // wrap, so the wrap is set here.
        var expander = new Expander
        {
            Header = new TextBlock { Text = node.Title, TextWrapping = TextWrapping.Wrap },
            IsExpanded = node.InitiallyExpanded,
            Content = content,
            Margin = new Thickness(0, 4, 0, 4),
        };
        TakesItsPresentersColour((TextBlock)expander.Header);
        AutomationProperties.SetName(expander, node.Title);
        return expander;
    }

    /// <summary>Plan 47 CB-22b: a TextBlock set as a Header or Content is the logical child of
    /// its Expander or Button and would inherit their Foreground, but Fluent's templates put
    /// their state colours (pointer over, pressed, disabled; High Contrast's Highlight and
    /// GrayText) on the ContentPresenter that draws it. So the text takes the presenter's
    /// colour, as a string's generated TextBlock does.</summary>
    private static void TakesItsPresentersColour(TextBlock text) =>
        text.SetBinding(TextBlock.ForegroundProperty, new Binding
        {
            Path = new PropertyPath(System.Windows.Documents.TextElement.ForegroundProperty),
            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(ContentPresenter), 1),
        });

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
                    // W7-7 (#1251, R-8): WPF disables caret navigation in a
                    // read-only TextBox whose caret is hidden, so Down fell
                    // through to the scroll host and a reader heard line 1
                    // again. Reading/ReadingSurface is the precedent.
                    IsReadOnlyCaretVisible = true,
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
                // CB-22b: a TextBlock, so the button's peer keeps the path's underscores in
                // the name below; with a string content it strips the first from that name.
                Content = new TextBlock { Text = "Jump to source" },
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 4, 0, 0),
            };
            TakesItsPresentersColour((TextBlock)open.Content);
            AutomationProperties.SetName(open, $"Jump to source: {sourcePath}");
            open.Click += (_, _) =>
            {
                if (InteractionSession is { } session)
                {
                    session.OpenEmbedSource(sourcePath);
                }
                else
                {
                    JumpToSource?.Invoke(sourcePath);
                }
            };
            panel.Children.Add(open);
        }
        return panel;
    }
}
