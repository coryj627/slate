// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using SlateWindows.Commands;

namespace SlateWindows;

/// <summary>
/// DS-15 (#1816): the bar above a note (<c>design/windows/components/patterns/NoteHeader.prompt.md</c>):
/// its folders and name, the Source and Reading switch, and ⋯. A sighted reader sees where
/// they are and which mode they're in; nothing a screen reader relies on moves, and the bar
/// adds its stops before Properties (DSD11). Back and Forward wait for a note history (DSD15).
/// </summary>
/// <remarks>
/// <para>Each control acts on its own note, whichever pane is active. The switch names its own
/// tab (<see cref="WorkspaceViewModel.SetViewMode"/>) and a segment carries its own path, so
/// neither activates its pane: a pane made active with the keys left in another would send the
/// next chord to the wrong note. ⋯ runs the active pane's commands, so it makes its own pane
/// active as it opens, as focus entering the pane would.</para>
/// <para>The bar is a <see cref="Grid"/> with no peer: it adds stops, not a group to announce.
/// The template reuses it across a pane's tabs, so it follows its DataContext.</para>
/// </remarks>
internal sealed class NoteHeaderView : Grid
{
    /// <summary>Below this the switch drops its labels and the folders collapse into "…".</summary>
    internal const double CompactBelow = 520;

    private const double GlyphSize = 16;
    private const double ChevronSize = 12;
    private const string SourceGlyph = "\uE943";
    private const string ReadingGlyph = "\uE736";
    private const string ChevronGlyph = "\uE76C";
    private const string MoreGlyph = "\uE712";
    private const string FolderGlyph = "\uE8B7";

    private readonly StackPanel _folders = new() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _name = new()
    {
        Margin = new Thickness(6, 0, 6, 0),
        VerticalAlignment = VerticalAlignment.Center,
        TextTrimming = TextTrimming.CharacterEllipsis,
    };

    private readonly RadioButton _source;
    private readonly RadioButton _reading;
    private readonly TextBlock _sourceLabel;
    private readonly TextBlock _readingLabel;
    private readonly Button _more;
    private readonly MenuItem _splitRight;
    private readonly MenuItem _addProperty;
    private readonly MenuItem _bulkRename;
    private WorkspaceTabViewModel? _tab;
    private bool _synchronizing;

    public NoteHeaderView()
    {
        MinHeight = 40;
        SetResourceReference(BackgroundProperty, "Slate.SurfaceBrush");
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // The path: the folders take what the name leaves, so they're cut first, then the name.
        _name.SetResourceReference(StyleProperty, "Slate.BodyStrongTextBlockStyle");
        var path = new DockPanel
        {
            Margin = new Thickness(8, 0, 4, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            LastChildFill = true,
            ClipToBounds = true,
        };
        DockPanel.SetDock(_name, Dock.Right);
        path.Children.Add(_name);
        path.Children.Add(_folders);
        Children.Add(path);

        // The switch: one Tab stop, arrows move the choice (RadioGroupArrows), as the canvas's.
        string group = "NoteHeaderMode" + Guid.NewGuid().ToString("N");
        (_source, _sourceLabel) = Choice("NoteHeaderSource", "Source", SourceGlyph, group);
        (_reading, _readingLabel) = Choice("NoteHeaderReading", "Reading", ReadingGlyph, group);
        var choices = new StackPanel { Orientation = Orientation.Horizontal };
        choices.Children.Add(_source);
        choices.Children.Add(_reading);
        KeyboardNavigation.SetTabNavigation(choices, KeyboardNavigationMode.Once);
        KeyboardNavigation.SetDirectionalNavigation(choices, KeyboardNavigationMode.Cycle);
        RadioGroupArrows.SetIsEnabled(choices, true);
        var switcher = new Border
        {
            Margin = new Thickness(4, 0, 2, 0),
            Padding = new Thickness(2),
            CornerRadius = new CornerRadius(6),
            VerticalAlignment = VerticalAlignment.Center,
            Child = choices,
        };
        switcher.SetResourceReference(Border.BackgroundProperty, "Slate.RaisedSurfaceBrush");
        SetColumn(switcher, 1);
        Children.Add(switcher);

        // ⋯: the note's actions, the same commands as the menus.
        _splitRight = Row("NoteHeaderSplitRight", "Split Right", ChordTable.Ids.SplitRight);
        _addProperty = Row("NoteHeaderAddProperty", "Add Property…", ChordTable.Ids.AddProperty);
        _bulkRename = Row("NoteHeaderBulkRename", "Bulk Rename Properties…", ChordTable.Ids.BulkRenameProperties);
        var menu = new ContextMenu();
        menu.Items.Add(_splitRight);
        menu.Items.Add(new Separator());
        menu.Items.Add(_addProperty);
        menu.Items.Add(_bulkRename);
        // A row's Click comes before its command, so the row acts on this note however the
        // menu was reached: a click, the keyboard, or a peer that never moved focus.
        menu.Opened += (_, _) => ActivateOwnPane();
        foreach (MenuItem row in new[] { _splitRight, _addProperty, _bulkRename })
        {
            row.Click += (_, _) => ActivateOwnPane();
        }
        _more = new Button
        {
            Content = MoreGlyph,
            Margin = new Thickness(2, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center,
            ContextMenu = menu,
        };
        _more.SetResourceReference(StyleProperty, "Slate.SubtleIconButtonStyle");
        AutomationProperties.SetAutomationId(_more, "NoteHeaderMore");
        AutomationProperties.SetName(_more, "More note actions");
        _more.Click += (_, _) => Open(_more);
        SetColumn(_more, 2);
        Children.Add(_more);

        DataContextChanged += (_, eventArgs) => Follow(eventArgs.NewValue as WorkspaceTabViewModel);
        Loaded += (_, _) => BindCommands();
    }

    internal bool IsCompact { get; private set; }

    private WorkspaceViewModel? Workspace => (Window.GetWindow(this)?.DataContext as VaultLifecycleViewModel)?.Workspace;

    private WorkspaceGroupViewModel? OwnGroup
    {
        get
        {
            for (DependencyObject? node = this; node is not null; node = VisualTreeHelper.GetParent(node))
            {
                if (node is TabControl { DataContext: WorkspaceGroupViewModel group })
                {
                    return group;
                }
            }

            return null;
        }
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        bool compact = sizeInfo.NewSize.Width < CompactBelow;
        if (compact != IsCompact)
        {
            IsCompact = compact;
            ShowPath();
            ShowChoices();
        }
    }

    private void Follow(WorkspaceTabViewModel? tab)
    {
        if (_tab is not null)
        {
            _tab.PropertyChanged -= Tab_PropertyChanged;
        }

        _tab = tab;
        if (_tab is not null)
        {
            _tab.PropertyChanged += Tab_PropertyChanged;
        }

        ShowPath();
        ShowChoices();
        BindCommands();
    }

    private void Tab_PropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName is nameof(WorkspaceTabViewModel.IsReadingMode))
        {
            ShowChoices();
        }
        else if (eventArgs.PropertyName is nameof(WorkspaceTabViewModel.Path))
        {
            ShowPath();
        }
    }

    /// <summary>The folders as segments, or behind "…" when compact, and the note's name,
    /// which is text, not a stop: the tab's name already says it. A rebuild under the keys
    /// leaves them on the rebuilt path's first button, not on nothing.</summary>
    private void ShowPath()
    {
        bool keysOnThePath = _folders.IsKeyboardFocusWithin;
        try
        {
            BuildPath();
        }
        finally
        {
            if (keysOnThePath && _folders.Children.OfType<Button>().FirstOrDefault() is { } first)
            {
                first.Focus();
            }
        }
    }

    private void BuildPath()
    {
        _folders.Children.Clear();
        string notePath = _tab?.Path ?? string.Empty;
        string[] parts = notePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        _name.Text = parts.Length == 0 ? string.Empty : System.IO.Path.GetFileNameWithoutExtension(parts[^1]);
        (string Name, string Path)[] folders = [.. parts.SkipLast(1).Select((name, index) => (name, string.Join('/', parts.Take(index + 1))))];
        if (folders.Length == 0)
        {
            return;
        }

        if (IsCompact)
        {
            var menu = new ContextMenu();
            foreach ((string name, string folder) in folders)
            {
                // Named by its path: a note's folders can repeat a name (notes/notes/x.md).
                var row = new MenuItem { Header = name, Icon = Glyph(FolderGlyph, GlyphSize) };
                AutomationProperties.SetName(row, folder);
                row.Click += (_, _) => Workspace?.RevealFolderFromHeader(folder);
                menu.Items.Add(row);
            }

            var collapsed = Segment("…", string.Join(" › ", folders.Select(folder => folder.Name)));
            AutomationProperties.SetAutomationId(collapsed, "NoteHeaderFolders");
            AutomationProperties.SetName(collapsed, "Folders");
            collapsed.ContextMenu = menu;
            collapsed.Click += (_, _) => Open(collapsed);
            _folders.Children.Add(collapsed);
            _folders.Children.Add(Chevron());
            return;
        }

        foreach ((string name, string folder) in folders)
        {
            Button segment = Segment(name, name);
            AutomationProperties.SetAutomationId(segment, "NoteHeaderFolder");
            AutomationProperties.SetName(segment, name);
            segment.Click += (_, _) => Workspace?.RevealFolderFromHeader(folder);
            _folders.Children.Add(segment);
            _folders.Children.Add(Chevron());
        }
    }

    /// <summary>The checked choice follows the tab's mode, under a guard so following it
    /// isn't taken for a choice. Compact, the labels go and tooltips say them.</summary>
    private void ShowChoices()
    {
        bool reading = _tab?.IsReadingMode == true;
        _synchronizing = true;
        try
        {
            _source.IsChecked = !reading;
            _reading.IsChecked = reading;
        }
        finally
        {
            _synchronizing = false;
        }

        _sourceLabel.Visibility = IsCompact ? Visibility.Collapsed : Visibility.Visible;
        _readingLabel.Visibility = _sourceLabel.Visibility;
        string? chord = ChordTable.WindowsChordFor(ChordTable.Ids.ToggleViewMode);
        _source.ToolTip = IsCompact ? (reading ? Switch("source", chord) : "Source view") : null;
        _reading.ToolTip = IsCompact ? (reading ? "Reading view" : Switch("reading", chord)) : null;
    }

    private static string Switch(string view, string? chord) =>
        chord is null ? $"Switch to {view} view" : $"Switch to {view} view ({chord})";

    private void Choice_Checked(object sender, RoutedEventArgs eventArgs)
    {
        if (_synchronizing || _tab is not { } tab || Workspace is not { } workspace)
        {
            return;
        }

        bool reading = ReferenceEquals(sender, _reading);
        workspace.SetViewMode(tab, reading, landFocus: KeysAreOnTheSurfaceGoingAway(reading));
    }

    /// <summary>Whether the keys are in the surface this flip hides: then they land in the
    /// note, as Ctrl+Shift+E's do. A collapse under a content element reaches no guard, so
    /// a reading-view link is walked up to its surface here (MainWindow's content walk).</summary>
    private bool KeysAreOnTheSurfaceGoingAway(bool toReading)
    {
        if (Parent is not Panel content || Keyboard.FocusedElement is not DependencyObject keys)
        {
            return false;
        }

        UIElement? leaving = toReading
            ? content.Children.OfType<SlateTextEditor>().FirstOrDefault()
            : content.Children.OfType<Reading.ReadingSurface>().FirstOrDefault();
        return leaving is not null && IsWithin(keys, leaving);
    }

    internal static bool IsWithin(DependencyObject? node, DependencyObject ancestor)
    {
        while (node is not null)
        {
            if (ReferenceEquals(node, ancestor))
            {
                return true;
            }

            node = node switch
            {
                FrameworkElement element => element.Parent ?? element.TemplatedParent ?? VisualTreeHelper.GetParent(node),
                FrameworkContentElement content => content.Parent ?? LogicalTreeHelper.GetParent(content),
                Visual or System.Windows.Media.Media3D.Visual3D => VisualTreeHelper.GetParent(node),
                _ => LogicalTreeHelper.GetParent(node),
            };
        }

        return false;
    }

    /// <summary>⋯ runs the active pane's commands, so its pane goes active first, as focus
    /// entering it would: nothing when it already is, else the one pane announcement.</summary>
    private void ActivateOwnPane()
    {
        if (OwnGroup is { } group)
        {
            Workspace?.SelectGroupFromKeyboardFocus(group);
        }
    }

    private void BindCommands()
    {
        if (Workspace is not { } workspace)
        {
            return;
        }

        _splitRight.Command = workspace.SplitRightCommand;
        _addProperty.Command = workspace.OpenAddPropertySheetCommand;
        _bulkRename.Command = workspace.OpenBulkRenameSheetCommand;
    }

    private static void Open(Button owner)
    {
        if (owner.ContextMenu is { } menu)
        {
            menu.PlacementTarget = owner;
            menu.Placement = PlacementMode.Bottom;
            menu.IsOpen = true;
        }
    }

    private (RadioButton Choice, TextBlock Label) Choice(string automationId, string label, string glyph, string group)
    {
        var text = new TextBlock { Text = label, Margin = new Thickness(6, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        text.SetResourceReference(StyleProperty, "Slate.BodyTextBlockStyle");
        var content = new StackPanel { Orientation = Orientation.Horizontal };
        content.Children.Add(Glyph(glyph, GlyphSize));
        content.Children.Add(text);
        var choice = new RadioButton
        {
            Content = content,
            GroupName = group,
            VerticalAlignment = VerticalAlignment.Center,
        };
        choice.SetResourceReference(StyleProperty, "Slate.SegmentRadioStyle");
        AutomationProperties.SetAutomationId(choice, automationId);
        AutomationProperties.SetName(choice, label);
        choice.Checked += Choice_Checked;
        return (choice, text);
    }

    private static MenuItem Row(string automationId, string label, string chordId)
    {
        var row = new MenuItem { Header = label, InputGestureText = ChordTable.WindowsChordFor(chordId) ?? string.Empty };
        AutomationProperties.SetAutomationId(row, automationId);
        AutomationProperties.SetName(row, label);
        return row;
    }

    private static Button Segment(string text, string tip)
    {
        var label = new TextBlock { Text = text, TextTrimming = TextTrimming.CharacterEllipsis };
        label.SetResourceReference(StyleProperty, "Slate.BodyTextBlockStyle");
        label.SetResourceReference(TextBlock.ForegroundProperty, "Slate.SecondaryTextBrush");
        var button = new Button { Content = label, ToolTip = tip == text ? null : tip };
        button.SetResourceReference(StyleProperty, "Slate.CrumbButtonStyle");
        return button;
    }

    private static UIElement Chevron()
    {
        AutomationPresentationTextBlock chevron = Glyph(ChevronGlyph, ChevronSize);
        chevron.Margin = new Thickness(2, 0, 2, 0);
        chevron.SetResourceReference(TextBlock.ForegroundProperty, "Slate.TertiaryTextBrush");
        return chevron;
    }

    private static AutomationPresentationTextBlock Glyph(string glyph, double size)
    {
        var text = new AutomationPresentationTextBlock
        {
            Text = glyph,
            FontSize = size,
            VerticalAlignment = VerticalAlignment.Center,
        };
        text.SetResourceReference(TextBlock.FontFamilyProperty, "SymbolThemeFontFamily");
        return text;
    }
}
