// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Markup;

namespace SlateWindows;

/// <summary>DS-17 (DS-18 reuses it): a pane's toolbar, `PanelToolbar.prompt.md`. At least
/// 40 tall: a title in BodyStrong at the given heading level, an optional count in
/// Caption, then its buttons, DS-9's 32 subtle icon buttons, at the trailing edge. The
/// title wraps rather than clips, so a long name pushes the buttons down a line instead
/// of hiding them.</summary>
[ContentProperty(nameof(Buttons))]
internal sealed class PanelToolbarView : Border
{
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(PanelToolbarView), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty TitleToolTipProperty = DependencyProperty.Register(
        nameof(TitleToolTip), typeof(object), typeof(PanelToolbarView), new PropertyMetadata(null));

    public static readonly DependencyProperty CountProperty = DependencyProperty.Register(
        nameof(Count), typeof(string), typeof(PanelToolbarView), new PropertyMetadata(null));

    public static readonly DependencyProperty TitleHeadingLevelProperty = DependencyProperty.Register(
        nameof(TitleHeadingLevel), typeof(AutomationHeadingLevel), typeof(PanelToolbarView),
        new PropertyMetadata(AutomationHeadingLevel.Level2));

    /// <summary>The least room the title keeps beside the buttons before they go under it.</summary>
    private const double TitleRoom = 96;

    private readonly WrapPanel _buttons = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };

    private readonly Grid _grid;

    public PanelToolbarView()
    {
        MinHeight = 40;
        Buttons.CollectionChanged += OnButtonsChanged;

        var title = new TextBlock { TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
        title.SetResourceReference(StyleProperty, "Slate.BodyStrongTextBlockStyle");
        _ = title.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(nameof(Title)) { Source = this });
        _ = title.SetBinding(ToolTipProperty, new System.Windows.Data.Binding(nameof(TitleToolTip)) { Source = this });
        _ = title.SetBinding(
            AutomationProperties.HeadingLevelProperty,
            new System.Windows.Data.Binding(nameof(TitleHeadingLevel)) { Source = this });

        var count = new AutomationPresentationTextBlock { Margin = new Thickness(6, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        count.SetResourceReference(StyleProperty, "Slate.CaptionTextBlockStyle");
        count.SetResourceReference(TextBlock.ForegroundProperty, "Slate.SecondaryTextBrush");
        _ = count.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(nameof(Count)) { Source = this });
        _ = count.SetBinding(
            VisibilityProperty,
            new System.Windows.Data.Binding(nameof(Count)) { Source = this, Converter = HasText.Instance });

        // The title stands 8 off the buttons beside it (#1790).
        var heading = new DockPanel { VerticalAlignment = VerticalAlignment.Center, LastChildFill = true, Margin = new Thickness(0, 0, 8, 0) };
        DockPanel.SetDock(count, Dock.Right);
        _ = heading.Children.Add(count);
        _ = heading.Children.Add(title);

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetColumn(_buttons, 1);
        _ = grid.Children.Add(heading);
        _ = grid.Children.Add(_buttons);
        grid.SizeChanged += (_, _) => Place();
        Child = grid;
        _grid = grid;
        TitleBlock = title;
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public object? TitleToolTip
    {
        get => GetValue(TitleToolTipProperty);
        set => SetValue(TitleToolTipProperty, value);
    }

    public string? Count
    {
        get => (string?)GetValue(CountProperty);
        set => SetValue(CountProperty, value);
    }

    public AutomationHeadingLevel TitleHeadingLevel
    {
        get => (AutomationHeadingLevel)GetValue(TitleHeadingLevelProperty);
        set => SetValue(TitleHeadingLevelProperty, value);
    }

    /// <summary>The toolbar's buttons, in the order they stand.</summary>
    public ObservableCollection<UIElement> Buttons { get; } = [];

    /// <summary>The title's text block, for the facts.</summary>
    internal TextBlock TitleBlock { get; }

    /// <summary>The buttons sit beside the title while it keeps its room (its own width,
    /// up to <see cref="TitleRoom"/>), and otherwise go under it, at the trailing edge,
    /// wrapping if even the full width is too narrow. Nothing is cut off at the pane's
    /// narrowest.</summary>
    private void Place()
    {
        if (_grid.ActualWidth <= 0)
        {
            return;
        }

        _buttons.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        TitleBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double titleRoom = Math.Min(TitleBlock.DesiredSize.Width, TitleRoom);
        bool under = _grid.ActualWidth - _buttons.DesiredSize.Width < titleRoom;
        if (Grid.GetRow(_buttons) != (under ? 1 : 0))
        {
            Grid.SetRow(_buttons, under ? 1 : 0);
            Grid.SetColumn(_buttons, under ? 0 : 1);
            Grid.SetColumnSpan(_buttons, under ? 2 : 1);
        }
    }

    /// <summary>The buttons are set once, from XAML or as the toolbar is built: each one    /// takes its place as it's added. Nothing takes one away, so nothing here can take
    /// the keys from under a reader.</summary>
    private void OnButtonsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Add || e.NewItems is null)
        {
            throw new InvalidOperationException("A toolbar's buttons are only ever added.");
        }

        int index = e.NewStartingIndex;
        foreach (UIElement button in e.NewItems)
        {
            _buttons.Children.Insert(index++, button);
        }
    }

    private sealed class HasText : System.Windows.Data.IValueConverter
    {
        internal static readonly HasText Instance = new();

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) =>
            string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) =>
            System.Windows.Data.Binding.DoNothing;
    }
}
