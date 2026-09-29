// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using uniffi.slate_uniffi;

namespace SlateWindows.Graph;

/// <summary>
/// W6-2 PR E (#746), rule I Terms I1, I5, I7; rules X, Y, Z, K; contracts
/// E-11, E-15; ED-10: the inspector's view — code-built like every graph
/// view; ONE Group named T37 (<c>GraphInspector</c>) over the two notices
/// and four Groups named T38, T43, T50, T55. Every control is a standard
/// WPF control with its own peer: its Name the inventory's title, its
/// HelpText the hint, a slider's value the RangeValue pattern with the
/// mac's <c>%.2f</c> text as a sibling (Term Z3). The controls are enabled
/// only while the graph is EFFECTIVE, under the inactive notice (Term I7,
/// E-D8); the read-only notice comes second (Term Y6, E-D6; IGX-3). Every
/// write is one call into the view model's seam; nothing is announced
/// here; Escape bubbles to the shell (Term I5).
/// </summary>
internal sealed class GraphInspectorView : UserControl
{
    public static readonly DependencyProperty ModelProperty =
        DependencyProperty.Register(
            nameof(Model),
            typeof(GraphInspectorViewModel),
            typeof(GraphInspectorView),
            new PropertyMetadata(null, OnModelChanged));

    /// <summary>Term K6: the sliders' keys — Left/Right one step, PageUp/PageDown one large step.</summary>
    internal const double SliderSmallChange = 0.01;

    internal const double SliderLargeChange = 0.1;

    private readonly AutomationNamedGroupPanel _root;
    private readonly TextBlock _inactive;
    private readonly TextBlock _readOnly;
    private readonly AutomationNamedGroupPanel _filters;
    private readonly AutomationNamedGroupPanel _groups;
    private readonly AutomationNamedGroupPanel _display;
    private readonly AutomationNamedGroupPanel _forces;
    private readonly TextBox _nameQuery;
    private readonly CheckBox _attachments;
    private readonly CheckBox _ghosts;
    private readonly CheckBox _orphans;
    private readonly StackPanel _rows;
    private readonly TextBlock _noGroups;
    private readonly Button _addGroup;
    private readonly CheckBox _arrows;
    private readonly Slider _textFade;
    private readonly Slider _nodeSize;
    private readonly Slider _linkThickness;
    private readonly Slider _center;
    private readonly Slider _repel;
    private readonly Slider _link;
    private readonly Slider _linkDistance;
    private readonly Dictionary<Slider, TextBlock> _valueTexts = [];
    private GraphInspectorViewModel? _observed;
    private bool _synchronizing;
    private int? _pendingRowFocus;
    private bool _pendingAddFocus;

    public GraphInspectorView()
    {
        _inactive = Notice("GraphInspectorInactive", GraphPhrase.InspectorInactiveText);
        _readOnly = Notice("GraphInspectorReadOnly", string.Empty);

        // --- Filters (rule X) -----------------------------------------------------
        _filters = Section(GraphPhrase.InspectorFiltersSection, "GraphInspectorFilters");
        var nameLabel = new TextBlock { Text = GraphPhrase.InspectorNameFieldLabel, Margin = new Thickness(0, 0, 0, 2) };
        _nameQuery = new TextBox { Margin = new Thickness(0, 0, 0, 8) };
        AutomationProperties.SetAutomationId(_nameQuery, "GraphInspectorNameQuery");
        AutomationProperties.SetName(_nameQuery, GraphPhrase.FilterFieldName);
        AutomationProperties.SetLabeledBy(_nameQuery, nameLabel);
        _nameQuery.TextChanged += (_, _) =>
        {
            if (!_synchronizing)
            {
                Model?.SetNameQuery(_nameQuery.Text);
            }
        };
        _attachments = Flag("GraphInspectorAttachments", GraphPhrase.InspectorAttachmentsLabel, GraphPhrase.InspectorAttachmentsHint);
        _ghosts = Flag("GraphInspectorGhosts", GraphPhrase.InspectorUnresolvedLabel, GraphPhrase.InspectorUnresolvedHint);
        _orphans = Flag("GraphInspectorOrphans", GraphPhrase.InspectorOrphansLabel, GraphPhrase.InspectorOrphansHint);
        foreach (CheckBox flag in new[] { _attachments, _ghosts, _orphans })
        {
            // Checked/Unchecked, not Click: UIA's Toggle pattern (a screen
            // reader's activation) flips the box without a Click.
            flag.Checked += (_, _) => OnFlagChanged();
            flag.Unchecked += (_, _) => OnFlagChanged();
        }
        _filters.Children.Add(nameLabel);
        _filters.Children.Add(_nameQuery);
        _filters.Children.Add(_attachments);
        _filters.Children.Add(_ghosts);
        _filters.Children.Add(_orphans);

        // --- Groups (rule Y) -----------------------------------------------------------
        _groups = Section(GraphPhrase.InspectorGroupsSection, "GraphInspectorGroups");
        _noGroups = new TextBlock { Text = GraphPhrase.InspectorNoGroupsText, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) };
        _noGroups.SetResourceReference(TextBlock.ForegroundProperty, "Slate.SecondaryTextBrush");
        AutomationProperties.SetAutomationId(_noGroups, "GraphInspectorNoGroups");
        _rows = new StackPanel();
        _addGroup = new Button { Content = GraphPhrase.InspectorAddGroupLabel, HorizontalAlignment = HorizontalAlignment.Left, Padding = new Thickness(8, 2, 8, 2) };
        AutomationProperties.SetAutomationId(_addGroup, "GraphInspectorAddGroup");
        AutomationProperties.SetHelpText(_addGroup, GraphPhrase.InspectorAddGroupHint);
        _addGroup.Click += (_, _) => OnAddGroup();
        _groups.Children.Add(_noGroups);
        _groups.Children.Add(_rows);
        _groups.Children.Add(_addGroup);

        // --- Display (rule Z) -------------------------------------------------------------
        _display = Section(GraphPhrase.InspectorDisplaySection, "GraphInspectorDisplay");
        _arrows = Flag("GraphInspectorArrows", GraphPhrase.InspectorArrowsLabel, GraphPhrase.InspectorArrowsHint);
        _arrows.Checked += (_, _) => OnArrowsChanged();
        _arrows.Unchecked += (_, _) => OnArrowsChanged();
        _display.Children.Add(_arrows);
        _textFade = SliderRow(_display, "GraphInspectorTextFade", GraphPhrase.InspectorTextFadeLabel, GraphPhrase.InspectorTextFadeHint, 0.1, 2.0, value => Model?.SetTextFadeZoom(value));
        _nodeSize = SliderRow(_display, "GraphInspectorNodeSize", GraphPhrase.InspectorNodeSizeLabel, GraphPhrase.InspectorNodeSizeHint, 0.5, 2.0, value => Model?.SetNodeSizeMultiplier(value));
        _linkThickness = SliderRow(_display, "GraphInspectorLinkThickness", GraphPhrase.InspectorLinkThicknessLabel, GraphPhrase.InspectorLinkThicknessHint, 0.5, 4.0, value => Model?.SetLinkThickness(value));

        // --- Forces (rule K) ---------------------------------------------------------------
        _forces = Section(GraphPhrase.InspectorForcesSection, "GraphInspectorForces");
        _center = SliderRow(_forces, "GraphInspectorCenter", GraphPhrase.InspectorCenterLabel, GraphPhrase.InspectorCenterHint, 0, 1, value => Model?.SetCenter(value));
        _repel = SliderRow(_forces, "GraphInspectorRepel", GraphPhrase.InspectorRepelLabel, GraphPhrase.InspectorRepelHint, 0, 1, value => Model?.SetRepel(value));
        _link = SliderRow(_forces, "GraphInspectorLink", GraphPhrase.InspectorLinkForceLabel, GraphPhrase.InspectorLinkForceHint, 0, 1, value => Model?.SetLink(value));
        _linkDistance = SliderRow(_forces, "GraphInspectorLinkDistance", GraphPhrase.InspectorLinkDistanceLabel, GraphPhrase.InspectorLinkDistanceHint, 0, 1, value => Model?.SetLinkDistance(value));

        // --- The root (Term I1): the notices first, then the four sections ------------------
        _root = new AutomationNamedGroupPanel { Orientation = Orientation.Vertical };
        AutomationProperties.SetAutomationId(_root, "GraphInspector");
        AutomationProperties.SetName(_root, GraphPhrase.InspectorName);
        _root.Children.Add(_inactive);
        _root.Children.Add(_readOnly);
        _root.Children.Add(_filters);
        _root.Children.Add(_groups);
        _root.Children.Add(_display);
        _root.Children.Add(_forces);
        Content = new ScrollViewer { Content = _root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        Loaded += (_, _) => Observe(Model);
        Unloaded += (_, _) => StopObserving();
        RenderAll();
    }

    public GraphInspectorViewModel? Model
    {
        get => (GraphInspectorViewModel?)GetValue(ModelProperty);
        set => SetValue(ModelProperty, value);
    }

    /// <summary>Term I5 (E-D1): the pane's FIRST stop is the name field —
    /// the shell's right-pane boundary lands here when the inspector is the
    /// shown leaf (rule C's Term 9 twin), so a reader who toggled is IN the
    /// pane, not on the rail; false while the field cannot take the keys
    /// (the graph not effective, Term I7), and the boundary falls back to
    /// the leaves list. (A disabled field refuses the keys on its own — no
    /// second guard.)</summary>
    internal bool FocusFirstStop() => _nameQuery.IsVisible && _nameQuery.Focus();

    /// <summary>The boundary's deferred landing (IPI-1-1): nothing when the
    /// inspector is no longer the shown leaf — a hide or another leaf's
    /// reveal that interleaved owns the keys now — else the first stop, or
    /// the rail when the field cannot take the keys.</summary>
    internal static void LandBoundary(bool stillShown, Func<bool> focusFirstStop, Action focusRail)
    {
        ArgumentNullException.ThrowIfNull(focusFirstStop);
        ArgumentNullException.ThrowIfNull(focusRail);
        if (!stillShown)
        {
            return;
        }
        if (!focusFirstStop())
        {
            focusRail();
        }
    }

    // --- Test seams ------------------------------------------------------------------------

    internal FrameworkElement RootForTests => _root;

    internal TextBlock InactiveNoticeForTests => _inactive;

    internal TextBlock ReadOnlyNoticeForTests => _readOnly;

    internal IReadOnlyList<FrameworkElement> SectionsForTests => [_filters, _groups, _display, _forces];

    internal TextBox NameQueryForTests => _nameQuery;

    internal IReadOnlyList<CheckBox> FlagsForTests => [_attachments, _ghosts, _orphans];

    internal CheckBox ArrowsForTests => _arrows;

    internal IReadOnlyList<Slider> SlidersForTests => [_textFade, _nodeSize, _linkThickness, _center, _repel, _link, _linkDistance];

    internal TextBlock ValueTextOf(Slider slider) => _valueTexts[slider];

    internal Panel RowsForTests => _rows;

    internal TextBlock NoGroupsForTests => _noGroups;

    internal Button AddGroupForTests => _addGroup;

    // --- The builders ----------------------------------------------------------------------

    private static TextBlock Notice(string id, string text)
    {
        var notice = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8), Visibility = Visibility.Collapsed };
        notice.SetResourceReference(TextBlock.ForegroundProperty, "Slate.SecondaryTextBrush");
        AutomationProperties.SetAutomationId(notice, id);
        return notice;
    }

    private static AutomationNamedGroupPanel Section(string title, string id)
    {
        var section = new AutomationNamedGroupPanel { Orientation = Orientation.Vertical, Margin = new Thickness(0, 0, 0, 12) };
        AutomationProperties.SetAutomationId(section, id);
        AutomationProperties.SetName(section, title);
        var heading = new TextBlock { Text = title, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 4) };
        section.Children.Add(heading);
        return section;
    }

    private static CheckBox Flag(string id, string label, string hint)
    {
        var flag = new CheckBox { Content = label, Margin = new Thickness(0, 2, 0, 2) };
        AutomationProperties.SetAutomationId(flag, id);
        AutomationProperties.SetHelpText(flag, hint);
        return flag;
    }

    /// <summary>Term Z3 (ED-10; T60): the title as the slider's Name, the
    /// hint its HelpText, the value the RangeValue pattern's — and the
    /// mac's <c>%.2f</c> text beside the title, its own peer.</summary>
    private Slider SliderRow(Panel section, string id, string title, string hint, double minimum, double maximum, Action<double> write)
    {
        var header = new DockPanel { Margin = new Thickness(0, 4, 0, 0) };
        var valueText = new TextBlock { HorizontalAlignment = HorizontalAlignment.Right };
        valueText.SetResourceReference(TextBlock.ForegroundProperty, "Slate.SecondaryTextBrush");
        AutomationProperties.SetAutomationId(valueText, id + "Value");
        DockPanel.SetDock(valueText, Dock.Right);
        header.Children.Add(valueText);
        header.Children.Add(new TextBlock { Text = title });
        var slider = new Slider
        {
            Minimum = minimum,
            Maximum = maximum,
            SmallChange = SliderSmallChange,
            LargeChange = SliderLargeChange,
            IsMoveToPointEnabled = true,
            Margin = new Thickness(0, 0, 0, 4),
        };
        AutomationProperties.SetAutomationId(slider, id);
        AutomationProperties.SetName(slider, title);
        AutomationProperties.SetHelpText(slider, hint);
        _valueTexts[slider] = valueText;
        // The value text is RENDERED from the model (SetSlider) — a user's
        // step writes the seam, the model's event renders it back; nothing
        // is shown that the sources do not hold.
        slider.ValueChanged += (_, e) =>
        {
            if (!_synchronizing)
            {
                write(e.NewValue);
            }
        };
        section.Children.Add(header);
        section.Children.Add(slider);
        return slider;
    }

    // --- The model --------------------------------------------------------------------------

    private static void OnModelChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        var view = (GraphInspectorView)sender;
        view.StopObserving();
        if (view.IsLoaded)
        {
            view.Observe(e.NewValue as GraphInspectorViewModel);
        }
        view.RenderAll();
    }

    private void Observe(GraphInspectorViewModel? model)
    {
        if (model is null || ReferenceEquals(_observed, model))
        {
            return;
        }
        StopObserving();
        _observed = model;
        model.PropertyChanged += OnModelPropertyChanged;
        RenderAll();
    }

    private void StopObserving()
    {
        if (_observed is { } observed)
        {
            observed.PropertyChanged -= OnModelPropertyChanged;
            _observed = null;
        }
    }

    private void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(GraphInspectorViewModel.IncludeAttachments):
            case nameof(GraphInspectorViewModel.IncludeGhosts):
            case nameof(GraphInspectorViewModel.OrphansOnly):
                RenderFilters();
                break;
            case nameof(GraphInspectorViewModel.NameQuery):
                RenderNameQuery();
                break;
            case nameof(GraphInspectorViewModel.Groups):
                RenderGroups();
                break;
            case nameof(GraphInspectorViewModel.HasNoGroups):
                break;
            case nameof(GraphInspectorViewModel.Arrows):
            case nameof(GraphInspectorViewModel.TextFadeZoom):
            case nameof(GraphInspectorViewModel.NodeSizeMultiplier):
            case nameof(GraphInspectorViewModel.LinkThickness):
                RenderDisplay();
                break;
            case nameof(GraphInspectorViewModel.Center):
            case nameof(GraphInspectorViewModel.Repel):
            case nameof(GraphInspectorViewModel.Link):
            case nameof(GraphInspectorViewModel.LinkDistance):
                RenderForces();
                break;
            case nameof(GraphInspectorViewModel.IsGraphEffective):
                RenderGate();
                break;
            default:
                break;
        }
    }

    // --- The renders: every control reads through the view model ---------------------------

    private void RenderAll()
    {
        RenderGate();
        RenderFilters();
        RenderNameQuery();
        RenderGroups();
        RenderDisplay();
        RenderForces();
    }

    /// <summary>Term I7 (E-D8) and Term Y6 (E-D6; IGX-3): the effectiveness
    /// gate disables the four sections whatever the writability and shows
    /// the inactive notice FIRST; the read-only state alone disables nothing
    /// and shows its notice second.</summary>
    private void RenderGate()
    {
        bool effective = Model?.IsGraphEffective == true;
        foreach (FrameworkElement section in SectionsForTests)
        {
            section.IsEnabled = effective;
        }
        _inactive.Visibility = effective ? Visibility.Collapsed : Visibility.Visible;
        bool writable = Model?.IsWritable ?? true;
        _readOnly.Text = writable ? string.Empty : GraphPhrase.InspectorReadOnlyPrefix + Model?.LoadFailure;
        _readOnly.Visibility = writable ? Visibility.Collapsed : Visibility.Visible;
    }

    private void RenderFilters()
    {
        _synchronizing = true;
        try
        {
            _attachments.IsChecked = Model?.IncludeAttachments == true;
            _ghosts.IsChecked = Model?.IncludeGhosts == true;
            _orphans.IsChecked = Model?.OrphansOnly == true;
        }
        finally
        {
            _synchronizing = false;
        }
    }

    private void RenderNameQuery()
    {
        string text = Model?.NameQuery ?? string.Empty;
        if (string.Equals(_nameQuery.Text, text, StringComparison.Ordinal))
        {
            return;
        }
        _synchronizing = true;
        try
        {
            _nameQuery.Text = text;
        }
        finally
        {
            _synchronizing = false;
        }
    }

    private void RenderDisplay()
    {
        _synchronizing = true;
        try
        {
            _arrows.IsChecked = Model?.Arrows == true;
            SetSlider(_textFade, Model?.TextFadeZoom);
            SetSlider(_nodeSize, Model?.NodeSizeMultiplier);
            SetSlider(_linkThickness, Model?.LinkThickness);
        }
        finally
        {
            _synchronizing = false;
        }
    }

    private void RenderForces()
    {
        _synchronizing = true;
        try
        {
            SetSlider(_center, Model?.Center);
            SetSlider(_repel, Model?.Repel);
            SetSlider(_link, Model?.Link);
            SetSlider(_linkDistance, Model?.LinkDistance);
        }
        finally
        {
            _synchronizing = false;
        }
    }

    private void SetSlider(Slider slider, double? value)
    {
        double next = value ?? slider.Minimum;
        if (slider.Value != next)
        {
            slider.Value = next;
        }
        _valueTexts[slider].Text = GraphPhrase.InspectorSliderValue(slider.Value);
    }

    /// <summary>Term Y1: one row per group in order, the labels composed with
    /// the 1-based index; a rebuild only when the count changed — an edit
    /// to a row syncs its controls in place, so the field being typed into
    /// keeps the keys; Term Y3's and Y5's landings delivered after.</summary>
    private void RenderGroups()
    {
        IReadOnlyList<GraphInspectorGroupRow> rows = Model?.Groups ?? [];
        _noGroups.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        _synchronizing = true;
        try
        {
            if (_rows.Children.Count != rows.Count)
            {
                _rows.Children.Clear();
                foreach (GraphInspectorGroupRow row in rows)
                {
                    _rows.Children.Add(BuildRow(row));
                }
            }
            else
            {
                for (int i = 0; i < rows.Count; i++)
                {
                    SyncRow((Panel)_rows.Children[i], rows[i]);
                }
            }
        }
        finally
        {
            _synchronizing = false;
        }
        DeliverPendingFocus();
    }

    private Panel BuildRow(GraphInspectorGroupRow row)
    {
        int index = row.Index - 1;
        var panel = new DockPanel { Margin = new Thickness(0, 2, 0, 2), LastChildFill = true };
        var remove = new Button { Content = "\u2715", Padding = new Thickness(6, 0, 6, 0), Margin = new Thickness(4, 0, 0, 0) };
        // The ids are literal prefixes + the 1-based index (the evidence
        // census reads the prefixes as the shell's literals).
        AutomationProperties.SetAutomationId(remove, "GraphInspectorRemoveGroup:" + row.Index.ToString(System.Globalization.CultureInfo.InvariantCulture));
        AutomationProperties.SetName(remove, GraphPhrase.InspectorRemoveGroupName(row.Index));
        remove.Click += (_, _) =>
        {
            _pendingRowFocus = index - 1;
            Model?.RemoveGroup(index);
        };
        var ring = new ComboBox { ItemsSource = Model?.RingStyles, DisplayMemberPath = nameof(GraphRingStyleSpec.Title), ItemContainerStyle = PickerItemStyle(), Margin = new Thickness(4, 0, 0, 0), MinWidth = 72 };
        AutomationProperties.SetAutomationId(ring, "GraphInspectorGroupRing:" + row.Index.ToString(System.Globalization.CultureInfo.InvariantCulture));
        SiblingNames.SetNamePath(ring, nameof(GraphRingStyleSpec.Title));
        SiblingNames.SetNoun(ring, "style");
        AutomationProperties.SetName(ring, GraphPhrase.InspectorGroupRingName(row.Index));
        ring.SelectionChanged += (_, _) =>
        {
            if (!_synchronizing && ring.SelectedItem is GraphRingStyleSpec spec)
            {
                Model?.SetGroupRing(index, spec.Style);
            }
        };
        var colour = new ComboBox { ItemsSource = Model?.ColorTokens, DisplayMemberPath = nameof(GraphColorTokenSpec.Title), ItemContainerStyle = PickerItemStyle(), Margin = new Thickness(4, 0, 0, 0), MinWidth = 72 };
        AutomationProperties.SetAutomationId(colour, "GraphInspectorGroupColour:" + row.Index.ToString(System.Globalization.CultureInfo.InvariantCulture));
        SiblingNames.SetNamePath(colour, nameof(GraphColorTokenSpec.Title));
        SiblingNames.SetNoun(colour, "colour");
        AutomationProperties.SetName(colour, GraphPhrase.InspectorGroupColourName(row.Index));
        colour.SelectionChanged += (_, _) =>
        {
            if (!_synchronizing && colour.SelectedItem is GraphColorTokenSpec spec)
            {
                Model?.SetGroupColor(index, spec.Token);
            }
        };
        var query = new TextBox { MinWidth = 60 };
        AutomationProperties.SetAutomationId(query, "GraphInspectorGroupQuery:" + row.Index.ToString(System.Globalization.CultureInfo.InvariantCulture));
        AutomationProperties.SetName(query, GraphPhrase.InspectorGroupQueryName(row.Index));
        AutomationProperties.SetHelpText(query, GraphPhrase.InspectorGroupQueryLabel);
        query.TextChanged += (_, _) =>
        {
            if (!_synchronizing)
            {
                Model?.SetGroupQuery(index, query.Text);
            }
        };
        DockPanel.SetDock(remove, Dock.Right);
        DockPanel.SetDock(ring, Dock.Right);
        DockPanel.SetDock(colour, Dock.Right);
        panel.Children.Add(remove);
        panel.Children.Add(ring);
        panel.Children.Add(colour);
        panel.Children.Add(query);
        SyncRow(panel, row);
        return panel;
    }

    /// <summary>Term Y4 (0bD-12): a picker item's ACCESSIBLE name is core's
    /// Title — without it WPF names a data item by its ToString (the spec
    /// record's shape, which the inspector journey heard). The visible text
    /// is the same Title through DisplayMemberPath. W7-7 PR 3 (#1246, R-4):
    /// read under the sibling rule the picker declares, so two specs that
    /// share a Title still read apart.</summary>
    private static Style PickerItemStyle()
    {
        var style = new Style(typeof(ComboBoxItem));
        style.Setters.Add(new Setter(AutomationProperties.NameProperty, SiblingNames.ContainerNameBinding()));
        return style;
    }

    private void SyncRow(Panel panel, GraphInspectorGroupRow row)
    {
        foreach (UIElement child in panel.Children)
        {
            switch (child)
            {
                case TextBox query when !string.Equals(query.Text, row.Query, StringComparison.Ordinal):
                    query.Text = row.Query;
                    break;
                case ComboBox combo when AutomationProperties.GetAutomationId(combo).StartsWith("GraphInspectorGroupColour", StringComparison.Ordinal):
                    combo.SelectedItem = Model?.ColorTokens.FirstOrDefault(spec => spec.Token == row.ColorToken);
                    break;
                case ComboBox combo:
                    combo.SelectedItem = Model?.RingStyles.FirstOrDefault(spec => spec.Style == row.RingStyle);
                    break;
                default:
                    break;
            }
        }
    }

    /// <summary>Term X1: a flag's own flip — a click, Space, the Toggle
    /// pattern — is ONE SetBackendFilter over the three boxes' values; the
    /// programmatic renders run under the guard. A refused write (Term X4)
    /// leaves the boxes as the view state has them.</summary>
    private void OnFlagChanged()
    {
        if (_synchronizing)
        {
            return;
        }
        Model?.SetBackendFilter(new GraphFilter(_attachments.IsChecked == true, _ghosts.IsChecked == true, _orphans.IsChecked == true));
        RenderFilters();
    }

    /// <summary>Term Z1: the Arrows flip — the preferences' trigger through the view model.</summary>
    private void OnArrowsChanged()
    {
        if (_synchronizing)
        {
            return;
        }
        Model?.SetArrows(_arrows.IsChecked == true);
        RenderDisplay();
    }

    /// <summary>Term Y3: the new row's query field takes the keys.</summary>
    private void OnAddGroup()
    {
        _pendingRowFocus = Model?.Groups.Count ?? 0;
        Model?.AddGroup();
    }

    /// <summary>Terms Y3, Y5: after a rebuild the keys land on the pending
    /// row's query field — the added row, or the row before the removed one
    /// — or on Add Group when the list is empty.</summary>
    private void DeliverPendingFocus()
    {
        if (_pendingRowFocus is not { } wanted && !_pendingAddFocus)
        {
            return;
        }
        int index = _pendingRowFocus ?? -1;
        _pendingRowFocus = null;
        _pendingAddFocus = false;
        if (_rows.Children.Count == 0)
        {
            _ = _addGroup.Focus();
            return;
        }
        index = Math.Clamp(index, 0, _rows.Children.Count - 1);
        if (_rows.Children[index] is Panel panel && panel.Children.OfType<TextBox>().FirstOrDefault() is { } query)
        {
            _ = query.Focus();
        }
    }
}
