// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Controls;

namespace SlateWindows;

/// <summary>
/// #1781: a row style with no base falls back to WPF's classic template, whose
/// selection is a fixed blue. A style built in code can't be based on the shell's
/// row styles, because it's built before it can reach them, so it names the same
/// templates and setters by key.
/// </summary>
internal static class ClassicRows
{
    internal static Style Apply(Style style)
    {
        if (style.TargetType == typeof(ComboBoxItem))
        {
            Add(style, "Slate.ComboRowTemplate", new Thickness(10, 4, 10, 4));
        }
        else if (style.TargetType == typeof(ListBoxItem))
        {
            Add(style, "Slate.PanelRowTemplate", new Thickness(10, 3, 10, 3));
        }

        return style;
    }

    /// <summary>A key it can't find would leave a row with no template at all, so
    /// the row takes the shell's template only once it can reach it.</summary>
    internal static void TakeTreeRow(TreeViewItem row)
    {
        if (row.TryFindResource("Slate.TreeRowTemplate") is ControlTemplate template)
        {
            row.Template = template;
        }
    }

    private static void Add(Style style, string templateKey, Thickness padding)
    {
        style.Setters.Add(new Setter(Control.PaddingProperty, padding));
        style.Setters.Add(new Setter(Control.BackgroundProperty, System.Windows.Media.Brushes.Transparent));
        style.Setters.Add(new Setter(Control.BorderBrushProperty, System.Windows.Media.Brushes.Transparent));
        style.Setters.Add(new Setter(FrameworkElement.FocusVisualStyleProperty, new DynamicResourceExtension("Slate.RowFocusVisual")));
        style.Setters.Add(new Setter(Control.TemplateProperty, new DynamicResourceExtension(templateKey)));
    }
}
