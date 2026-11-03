// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;

namespace SlateWindows;

/// <summary>
/// #1774: says on screen what an empty field is for, in the words its accessible
/// name already gives a screen reader. WPF's TextBox has no placeholder.
///
/// The prompt goes inside the field's own template rather than in an adorner: the
/// adorner layer sits above the whole window, so a prompt there would draw over an
/// open overlay. It's presentation-only and never part of the field's value.
/// </summary>
internal static class FieldPrompt
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled", typeof(bool), typeof(FieldPrompt), new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (element is not TextBox box || e.NewValue is not true)
        {
            return;
        }

        // A theme change can swap the field's template, which takes the prompt with it.
        void Reinstall(object? sender, EventArgs args) =>
            box.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => Install(box));
        box.Loaded += (_, _) =>
        {
            ThemeManager.ResourcesChanged -= Reinstall;
            ThemeManager.ResourcesChanged += Reinstall;
            Install(box);
        };
        box.Unloaded += (_, _) => ThemeManager.ResourcesChanged -= Reinstall;
        box.TextChanged += (_, _) => Update(box);
        box.IsEnabledChanged += (_, _) => Update(box);
        if (box.IsLoaded)
        {
            Install(box);
        }
    }

    private static void Install(TextBox box)
    {
        box.ApplyTemplate();
        if (box.Template?.FindName("PART_ContentHost", box) is not FrameworkElement host
            || VisualTreeHelper.GetParent(host) is not Panel panel)
        {
            return;
        }

        if (Prompt(box) is null)
        {
            var text = new AutomationPresentationTextBlock
            {
                // A multi-line field's text starts at its top.
                VerticalAlignment = box.AcceptsReturn ? VerticalAlignment.Top : VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                // The 2px is the text view's own inset, so the prompt starts where
                // typed text will.
                Margin = new Thickness(host.Margin.Left + box.Padding.Left + 2, 0, host.Margin.Right + box.Padding.Right, 0),
            };
            text.SetResourceReference(TextBlock.ForegroundProperty, "Slate.SecondaryTextBrush");
            // A bound name can arrive after the field loads.
            text.SetBinding(TextBlock.TextProperty, new Binding
            {
                Source = box,
                Path = new PropertyPath(AutomationProperties.NameProperty),
            });
            var prompt = new PromptHost { IsHitTestVisible = false, Child = text };
            Grid.SetRow(prompt, Grid.GetRow(host));
            Grid.SetColumn(prompt, Grid.GetColumn(host));
            panel.Children.Add(prompt);
        }

        Update(box);
    }

    private static void Update(TextBox box)
    {
        if (Prompt(box) is { } prompt)
        {
            prompt.Visibility = box.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            prompt.Opacity = box.IsEnabled ? 1 : 0.6;
        }
    }

    private static PromptHost? Prompt(TextBox box) =>
        box.Template?.FindName("PART_ContentHost", box) is FrameworkElement host
        && VisualTreeHelper.GetParent(host) is Panel panel
            ? panel.Children.OfType<PromptHost>().FirstOrDefault()
            : null;

    /// <summary>Asks for no width, so a long prompt trims to the field rather than
    /// making the field wider.</summary>
    private sealed class PromptHost : Decorator
    {
        private double _fieldWidth = double.PositiveInfinity;

        protected override Size MeasureOverride(Size constraint)
        {
            // A TextBlock trims to the width it's measured at, not the one it's
            // arranged at, so it measures at the field's width once that's known.
            Child?.Measure(new Size(Math.Min(constraint.Width, _fieldWidth), constraint.Height));
            return new Size(0, Child?.DesiredSize.Height ?? 0);
        }

        protected override Size ArrangeOverride(Size arrangeSize)
        {
            if (arrangeSize.Width != _fieldWidth)
            {
                _fieldWidth = arrangeSize.Width;
                InvalidateMeasure();
            }

            return base.ArrangeOverride(arrangeSize);
        }
    }
}
