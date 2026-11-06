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

    /// <summary>The bar's own stops' names, which a folder's segment mustn't sound like.</summary>
    private static readonly string[] ControlNames = ["Source", "Reading", "More note actions", "Folders"];

    private readonly PathPanel _path;
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
    private readonly Border _switcher;
    private readonly Button _more;
    private readonly MenuItem _splitRight;
    private readonly MenuItem _addProperty;
    private readonly MenuItem _bulkRename;
    private WorkspaceTabViewModel? _tab;
    private (string Name, string Path)[] _shownFolders = [];
    private bool _shownCollapsed;

    // Folders that don't fit the bar collapse into "…" as when compact, until their room is
    // as wide as they needed.
    private bool _overflowing;
    private double _foldersNeed;

    // The widths the switch, ⋯ and the collapsed folders need to be whole, kept while each is
    // hidden, since a hidden element measures nothing.
    private double _switcherNeeds;
    private double _moreNeeds;
    private double _foldersNeeds;
    private bool _synchronizing;

    public NoteHeaderView()
    {
        MinHeight = 40;
        SetResourceReference(BackgroundProperty, "Slate.SurfaceBrush");
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // The path: the folders give way first, into "…", then the name is cut.
        _name.SetResourceReference(StyleProperty, "Slate.BodyStrongTextBlockStyle");
        _path = new PathPanel(_folders, _name)
        {
            Margin = new Thickness(8, 0, 4, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            ClipToBounds = true,
        };
        Children.Add(_path);

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
        _switcher = new Border
        {
            Margin = new Thickness(4, 0, 2, 0),
            Padding = new Thickness(2),
            CornerRadius = new CornerRadius(6),
            VerticalAlignment = VerticalAlignment.Center,
            Child = choices,
        };
        _switcher.SetResourceReference(Border.BackgroundProperty, "Slate.RaisedSurfaceBrush");
        SetColumn(_switcher, 1);
        Children.Add(_switcher);

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

    private bool PathCollapsed => IsCompact || _overflowing;

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

    /// <summary>The bar asks for no more width than it's offered. A grid asks for its Auto
    /// columns whatever it's offered, and an element that asks for more is arranged at that
    /// and clipped, so the stops would be judged at a width the bar isn't shown at.</summary>
    protected override Size MeasureOverride(Size constraint)
    {
        Size desired = base.MeasureOverride(constraint);
        return new Size(Math.Min(desired.Width, constraint.Width), desired.Height);
    }

    /// <summary>A segment the bar can't show is a stop a reader can Tab to and nobody can see,
    /// so folders that don't fit beside the whole name collapse into "…", as a breadcrumb does
    /// when space runs out, and come back when their room is as wide as they needed (a wider
    /// bar, a shorter name). Both are read from the path's last measure, which offers the
    /// folders and the name all the room they want.</summary>
    protected override Size ArrangeOverride(Size arrangeSize)
    {
        Size arranged = base.ArrangeOverride(arrangeSize);
        double room = _path.Offered - _path.NameNeeds;
        if (!PathCollapsed && _folders.Children.Count > 0)
        {
            double needed = _folders.DesiredSize.Width;
            if (needed - room > 0.5)
            {
                _overflowing = true;
                _foldersNeed = needed;
                Dispatcher.BeginInvoke(ShowPath);
            }
        }
        else if (_overflowing && !IsCompact && room >= _foldersNeed)
        {
            _overflowing = false;
            Dispatcher.BeginInvoke(ShowPath);
        }

        if (Fits(arrangeSize.Width) != Shown)
        {
            Dispatcher.BeginInvoke(FitStops);
        }

        return arranged;
    }

    private (bool Folders, bool More, bool Switcher) Shown =>
        (_folders.Visibility == Visibility.Visible, _more.Visibility == Visibility.Visible, _switcher.Visibility == Visibility.Visible);

    /// <summary>Which of the bar's stops are whole at this width. A pane can be narrower than
    /// they are (a split has no floor), so they give way in order when one can't be: the
    /// folders first, then ⋯, then the switch. A stop's width is taken while it shows (the
    /// folders' once they're built collapsed, since segments that don't fit collapse first)
    /// and kept while it's hidden. The bar's width doesn't depend on its stops, so one comes
    /// back at the width it left.</summary>
    private (bool Folders, bool More, bool Switcher) Fits(double width)
    {
        if (_switcher.Visibility == Visibility.Visible)
        {
            _switcherNeeds = _switcher.DesiredSize.Width;
        }

        if (_more.Visibility == Visibility.Visible)
        {
            _moreNeeds = _more.DesiredSize.Width;
        }

        if (_folders.Visibility == Visibility.Visible && _shownCollapsed && _folders.Children.Count > 0)
        {
            _foldersNeeds = _folders.DesiredSize.Width + _path.Margin.Left + _path.Margin.Right;
        }

        bool switcher = width >= _switcherNeeds - 0.5;
        bool more = switcher && width >= _switcherNeeds + _moreNeeds - 0.5;
        bool folders = more && width >= _switcherNeeds + _moreNeeds + _foldersNeeds - 0.5;
        return (folders, more, switcher);
    }

    /// <summary>Shows the stops that are whole and hides the rest. Keys on a stop that goes
    /// move first to the bar's first stop that stays, which can only be the switch, else into
    /// the note, so they're never left on nothing.</summary>
    private void FitStops()
    {
        (bool folders, bool more, bool switcher) = Fits(ActualWidth);
        bool keysGo = (!folders && _folders.IsKeyboardFocusWithin)
            || (!more && _more.IsKeyboardFocusWithin)
            || (!switcher && _switcher.IsKeyboardFocusWithin);
        if (keysGo)
        {
            RadioButton checkedChoice = _reading.IsChecked == true ? _reading : _source;
            if (!switcher || !checkedChoice.Focus())
            {
                Workspace?.RequestActiveEditorFocus();
            }
        }

        _folders.Visibility = folders ? Visibility.Visible : Visibility.Collapsed;
        _more.Visibility = more ? Visibility.Visible : Visibility.Collapsed;
        _switcher.Visibility = switcher ? Visibility.Visible : Visibility.Collapsed;
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
    /// which is text, not a stop: the tab's name already says it. A rename that leaves the
    /// folders as they were leaves their segments, and the keys, alone. A rebuild under the
    /// keys puts them on the segment in the same place, else the first: never on nothing.
    /// (A folder's path changes whenever one above it does, so its place is as good a
    /// match as its path.)</summary>
    private void ShowPath()
    {
        (string Name, string Path)[] folders = FoldersOf(_tab?.Path, out string name);
        _name.Text = name;
        if (!folders.SequenceEqual(_shownFolders))
        {
            // Another path gets the bar's whole width to try.
            _overflowing = false;
        }

        if (folders.SequenceEqual(_shownFolders) && PathCollapsed == _shownCollapsed)
        {
            return;
        }

        Button[] before = [.. _folders.Children.OfType<Button>()];
        int focused = Array.FindIndex(before, button => button.IsKeyboardFocusWithin);
        try
        {
            BuildPath(folders);
        }
        finally
        {
            _shownFolders = folders;
            _shownCollapsed = PathCollapsed;
            Button[] after = [.. _folders.Children.OfType<Button>()];
            if (focused >= 0 && (after.ElementAtOrDefault(focused) ?? after.FirstOrDefault()) is { } target)
            {
                target.Focus();
            }
        }
    }

    private static (string Name, string Path)[] FoldersOf(string? notePath, out string name)
    {
        string[] parts = (notePath ?? string.Empty).Split('/', StringSplitOptions.RemoveEmptyEntries);
        name = parts.Length == 0 ? string.Empty : System.IO.Path.GetFileNameWithoutExtension(parts[^1]);
        return [.. parts.SkipLast(1).Select((folder, index) => (folder, string.Join('/', parts.Take(index + 1))))];
    }

    private void BuildPath((string Name, string Path)[] folders)
    {
        _folders.Children.Clear();
        if (folders.Length == 0)
        {
            return;
        }

        if (PathCollapsed)
        {
            var menu = new ContextMenu();
            string?[] rowNames = SiblingNames.Compose(
                [.. folders.Select(folder => (string?)folder.Name)],
                [.. folders.Select(folder => (string?)folder.Path)],
                "folder");
            foreach (((string name, string folder), string? spoken) in folders.Zip(rowNames))
            {
                // The sibling rule, as the segments': a note's folders can repeat a name
                // (notes/notes/x.md) or have none a reader hears (notes/_/x.md).
                var row = new MenuItem { Header = name, Icon = Glyph(FolderGlyph, GlyphSize) };
                AutomationProperties.SetName(row, spoken);
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

        string[] spokenNames = SegmentNames(folders);
        foreach (((string name, string folder), string spoken) in folders.Zip(spokenNames))
        {
            Button segment = Segment(name, spoken);
            AutomationProperties.SetAutomationId(segment, "NoteHeaderFolder");
            AutomationProperties.SetName(segment, spoken);
            segment.Click += (_, _) => Workspace?.RevealFolderFromHeader(folder);
            _folders.Children.Add(segment);
            _folders.Children.Add(Chevron());
        }
    }

    /// <summary>The segments' names by the shell's one sibling rule (SiblingNames.Compose,
    /// R-4): a folder's name as it is, unless it reads like another stop in the bar (a folder
    /// of the same name but for case, or one of the bar's own controls); then its path tells
    /// it apart, "notes, notes/notes". The controls go in beside the folders and keep their
    /// own names.</summary>
    private static string[] SegmentNames((string Name, string Path)[] folders)
    {
        string?[] names = [.. folders.Select(folder => (string?)folder.Name), .. ControlNames];
        string?[] distinguishers = [.. folders.Select(folder => (string?)folder.Path), .. ControlNames.Select(_ => (string?)null)];
        return [.. SiblingNames.Compose(names, distinguishers, "folder").Take(folders.Length)];
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

    /// <summary>The path's layout: the folders first, at their whole width, then the note's
    /// name in what's left, cut with an ellipsis. So "…" is never what's cut, and the bar
    /// can tell whether the folders' segments would fit beside the whole name.</summary>
    private sealed class PathPanel : Panel
    {
        private readonly UIElement _folders;
        private readonly UIElement _name;

        internal PathPanel(UIElement folders, UIElement name)
        {
            _folders = folders;
            _name = name;
            Children.Add(folders);
            Children.Add(name);
        }

        /// <summary>The width the last measure offered the path.</summary>
        internal double Offered { get; private set; }

        /// <summary>The whole name's width, uncut.</summary>
        internal double NameNeeds { get; private set; }

        protected override Size MeasureOverride(Size availableSize)
        {
            var unbounded = new Size(double.PositiveInfinity, availableSize.Height);
            _folders.Measure(unbounded);
            _name.Measure(unbounded);
            Offered = availableSize.Width;
            NameNeeds = _name.DesiredSize.Width;
            double folders = Math.Min(_folders.DesiredSize.Width, availableSize.Width);
            _name.Measure(new Size(Math.Max(0, availableSize.Width - folders), availableSize.Height));
            return new Size(folders + _name.DesiredSize.Width, Math.Max(_folders.DesiredSize.Height, _name.DesiredSize.Height));
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            double folders = Math.Min(_folders.DesiredSize.Width, finalSize.Width);
            _folders.Arrange(new Rect(0, 0, folders, finalSize.Height));
            _name.Arrange(new Rect(folders, 0, Math.Max(0, finalSize.Width - folders), finalSize.Height));
            return finalSize;
        }
    }
}
