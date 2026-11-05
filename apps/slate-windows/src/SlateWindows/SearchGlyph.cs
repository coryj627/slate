// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace SlateWindows;

/// <summary>
/// DS-9: a search or filter field shows it searches, with the design's glyph at its
/// leading edge. A behaviour, not a style: a based-on style can't add a template part,
/// and a style in window XAML can't reach Fluent's.
///
/// The glyph is presentation-only, so the field's peer, name, value and selection are
/// what they'd be without it. The field gives up some padding for it, and text and
/// prompt start after it.
/// </summary>
internal static class SearchGlyph
{
    /// <summary>The glyph's size, which text size doesn't change.</summary>
    private const double GlyphSize = 16;

    /// <summary>The glyph and the gap after it.</summary>
    private const double Room = 24;

    /// <summary>Segoe Fluent Icons' Search.</summary>
    private const string Glyph = "\uE721";

    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled", typeof(bool), typeof(SearchGlyph), new PropertyMetadata(false, OnIsEnabledChanged));

    /// <summary>The padding this set, so it can tell a padding it has already taken room
    /// from from one that a new style has put back.</summary>
    private static readonly DependencyProperty ShiftedPaddingProperty = DependencyProperty.RegisterAttached(
        "ShiftedPadding", typeof(Thickness?), typeof(SearchGlyph));

    public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (element is not TextBox box || e.NewValue is not true)
        {
            return;
        }

        // A theme change can swap the field's template, which takes the glyph with it.
        void Reinstall(object? sender, EventArgs args) =>
            box.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => Install(box));
        box.Loaded += (_, _) =>
        {
            ThemeManager.ResourcesChanged -= Reinstall;
            ThemeManager.ResourcesChanged += Reinstall;
            Install(box);
        };
        box.Unloaded += (_, _) => ThemeManager.ResourcesChanged -= Reinstall;
        box.IsEnabledChanged += (_, _) => Dim(box);
        if (box.IsLoaded)
        {
            Install(box);
        }
    }

    /// <summary>What a theme change does, for a fact that can't raise one.</summary>
    internal static void Reinstall(TextBox box) => Install(box);

    private static void Install(TextBox box)
    {
        box.ApplyTemplate();
        if (box.Template?.FindName("PART_ContentHost", box) is not FrameworkElement host
            || VisualTreeHelper.GetParent(host) is not Panel panel)
        {
            return;
        }

        Thickness padding = box.Padding;
        if (box.GetValue(ShiftedPaddingProperty) as Thickness? != padding)
        {
            padding = new Thickness(padding.Left + Room, padding.Top, padding.Right, padding.Bottom);
            box.SetCurrentValue(Control.PaddingProperty, padding);
            box.SetValue(ShiftedPaddingProperty, padding);
            FieldPrompt.Update(box);
        }

        if (Find(panel) is null)
        {
            var glyph = new AutomationPresentationTextBlock
            {
                Text = Glyph,
                FontSize = GlyphSize,
                IsHitTestVisible = false,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                // The 2px is the text view's own inset, as the prompt's is.
                Margin = new Thickness(host.Margin.Left + padding.Left - Room + 2, 0, 0, 0),
            };
            glyph.SetResourceReference(TextBlock.FontFamilyProperty, "SymbolThemeFontFamily");
            glyph.SetResourceReference(TextBlock.ForegroundProperty, "Slate.SecondaryTextBrush");
            Grid.SetRow(glyph, Grid.GetRow(host));
            Grid.SetColumn(glyph, Grid.GetColumn(host));
            panel.Children.Add(glyph);
        }

        Dim(box);
    }

    private static void Dim(TextBox box)
    {
        if (box.Template?.FindName("PART_ContentHost", box) is FrameworkElement host
            && VisualTreeHelper.GetParent(host) is Panel panel
            && Find(panel) is { } glyph)
        {
            glyph.Opacity = box.IsEnabled ? 1 : 0.6;
        }
    }

    private static TextBlock? Find(Panel panel) =>
        panel.Children.OfType<AutomationPresentationTextBlock>().FirstOrDefault(text => text.Text == Glyph);
}
