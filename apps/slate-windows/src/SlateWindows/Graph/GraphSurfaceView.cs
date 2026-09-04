// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using uniffi.slate_uniffi;

namespace SlateWindows.Graph;

/// <summary>
/// W6-2 PR A (#746), contracts A-4 and A-11: the graph tab body — the
/// header (the kind label, the mode switcher built from core's vector,
/// the Diagram item present and disabled until PR D), the four load
/// states with the mac's labels, and the table projection. A code-built
/// control, like every sibling surface view in this shell.
/// </summary>
internal sealed class GraphSurfaceView : UserControl
{
    public static readonly DependencyProperty ModelProperty =
        DependencyProperty.Register(
            nameof(Model),
            typeof(GraphDocumentViewModel),
            typeof(GraphSurfaceView),
            new PropertyMetadata(null, OnModelChanged));

    /// <summary>The mac's labels (contract A-4; view text, not templates).</summary>
    internal const string LoadingText = "Loading graph…";
    internal const string LoadingAccessibleName = "Loading graph.";
    internal const string EmptyText = "No notes match the current filters.";
    internal const string ErrorAccessiblePrefix = "Graph error: ";

    private readonly TextBlock _title;
    private readonly StackPanel _switcher;
    private readonly List<RadioButton> _modeChoices = [];
    private readonly TextBlock _stateText;
    private readonly GraphTableView _table;

    public GraphSurfaceView()
    {
        AutomationProperties.SetAutomationId(this, "GraphSurface");

        _title = new TextBlock
        {
            Text = "Graph",
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 8, 12, 8),
        };
        AutomationProperties.SetHeadingLevel(_title, AutomationHeadingLevel.Level2);

        _switcher = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(12, 4, 12, 4),
        };
        AutomationProperties.SetName(_switcher, "Graph surface");
        AutomationProperties.SetAutomationId(_switcher, "GraphSurfaceSwitcher");

        _stateText = new TextBlock
        {
            Margin = new Thickness(32),
            HorizontalAlignment = HorizontalAlignment.Center,
            Visibility = Visibility.Collapsed,
            TextWrapping = TextWrapping.Wrap,
        };
        _stateText.SetResourceReference(TextBlock.ForegroundProperty, "Slate.SecondaryTextBrush");
        AutomationProperties.SetAutomationId(_stateText, "GraphStateText");

        _table = new GraphTableView();

        var header = new DockPanel();
        DockPanel.SetDock(_switcher, Dock.Right);
        header.Children.Add(_switcher);
        header.Children.Add(_title);

        var layout = new DockPanel();
        DockPanel.SetDock(header, Dock.Top);
        DockPanel.SetDock(_stateText, Dock.Top);
        layout.Children.Add(header);
        layout.Children.Add(_stateText);
        layout.Children.Add(_table);
        Content = layout;
    }

    public GraphDocumentViewModel? Model
    {
        get => (GraphDocumentViewModel?)GetValue(ModelProperty);
        set => SetValue(ModelProperty, value);
    }

    internal GraphTableView TableForTests => _table;

    internal TextBlock StateTextForTests => _stateText;

    internal IReadOnlyList<RadioButton> ModeChoicesForTests => _modeChoices;

    private static void OnModelChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        var view = (GraphSurfaceView)sender;
        if (e.OldValue is GraphDocumentViewModel old)
        {
            old.PropertyChanged -= view.OnModelPropertyChanged;
            old.ViewState.PropertyChanged -= view.OnViewStateChanged;
        }
        view._table.Model = e.NewValue as GraphDocumentViewModel;
        if (e.NewValue is GraphDocumentViewModel model)
        {
            model.PropertyChanged += view.OnModelPropertyChanged;
            model.ViewState.PropertyChanged += view.OnViewStateChanged;
            view.BuildSwitcher(model);
            view.ApplyState(model.Publication);
        }
    }

    /// <summary>Contract A-11: the mode switcher's items are core's vector
    /// in order; only Table is selectable in PR A.</summary>
    private void BuildSwitcher(GraphDocumentViewModel model)
    {
        _switcher.Children.Clear();
        _modeChoices.Clear();
        foreach (GraphSurfaceModeSpec spec in model.SurfaceModes)
        {
            var choice = new RadioButton
            {
                Content = spec.Title,
                GroupName = "GraphSurfaceMode",
                Margin = new Thickness(0, 0, 8, 0),
                IsChecked = spec.Mode == model.ViewState.Mode,
                IsEnabled = spec.Mode == GraphSurfaceMode.Table,
                Tag = spec.Mode,
            };
            AutomationProperties.SetAutomationId(choice, "GraphMode." + spec.Tag);
            AutomationProperties.SetName(choice, spec.Title);
            _switcher.Children.Add(choice);
            _modeChoices.Add(choice);
        }
    }

    private void OnViewStateChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(GraphViewState.Mode) && Model is { } model)
        {
            foreach (RadioButton choice in _modeChoices)
            {
                choice.IsChecked = (GraphSurfaceMode)choice.Tag == model.ViewState.Mode;
            }
        }
    }

    private void OnModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(GraphDocumentViewModel.Publication) && Model is { } model)
        {
            ApplyState(model.Publication);
        }
    }

    /// <summary>Contract A-4: LOADING, ERROR, EMPTY, READY in that
    /// precedence — the label visible, the accessible name the mac's.</summary>
    private void ApplyState(GraphPublication publication)
    {
        switch (publication.State)
        {
            case GraphLoadState.Loading:
                ShowState(LoadingText, LoadingAccessibleName);
                break;
            case GraphLoadState.Error:
                string message = publication.Error ?? string.Empty;
                ShowState(message, ErrorAccessiblePrefix + message);
                break;
            case GraphLoadState.Empty:
                ShowState(EmptyText, EmptyText);
                break;
            default:
                _stateText.Visibility = Visibility.Collapsed;
                _table.Visibility = Visibility.Visible;
                break;
        }
    }

    private void ShowState(string text, string accessibleName)
    {
        _stateText.Text = text;
        AutomationProperties.SetName(_stateText, accessibleName);
        _stateText.Visibility = Visibility.Visible;
        _table.Visibility = Visibility.Collapsed;
    }
}
