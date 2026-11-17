// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using SlateWindows.Bases;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// CB-19 (#1783, #1836): WPF publishes an open combo box's rows twice. Under the
/// combo box, each row is WPF's item peer, which works its place out. Under the
/// open list's own window, each row is the row container's own peer, which knows
/// a place only when one is set on the container. A screen reader that walks or
/// hit-tests the popup, and axe-windows' <c>ControlShouldSupportSetInfoWPF</c>,
/// read that second copy. Every combo box the census lists is opened here, in the
/// shipped window, and every row under its popup must say what WPF's item peer
/// for the same row says.
/// </summary>
public sealed class ComboBoxRowPlacesTests
{
    /// <summary>Fact 1: one data row for each combo box in
    /// <c>ComboBoxCensus</c>'s table (the census reads these rows). The
    /// combo boxes the census calls code-built are reached in their real
    /// views inside the shell.</summary>
    [Theory]
    [InlineData("SettingsTheme")]
    [InlineData("SettingsAccent")]
    [InlineData("AddPropertyType")]
    [InlineData("BulkRenameOldKeyType")]
    [InlineData("DashboardEditorQueryPicker")]
    [InlineData("View override")]
    [InlineData("BuilderCombinator")]
    [InlineData("BaseViewPicker")]
    [InlineData("ConnectionsDepth")]
    [InlineData("GraphInspectorGroupRing")]
    [InlineData("GraphInspectorGroupColour")]
    public void EveryRowOfAnOpenComboBoxSaysItsPlaceInBothTrees(string key) => OpenLists.RunSta(() =>
    {
        using ShownShell host = OpenLists.NewShell();
        ComboBox combo = OpenLists.Reach(host, key);
        OpenLists.Open(host, combo, key);

        OpenLists.AssertPopupRule(combo, key);
    });

    // ---- Facts 2 to 7: the tracker, on a bench of its own ------------------------------

    /// <summary>Fact 2, the rule every stamp is held to: a stamped row says
    /// exactly what WPF's item peer would have said with no stamp. Each arm
    /// runs twice on the bench: on a tracked combo box, and on a twin whose
    /// stamps are cleared before each read, which reads WPF's own answer. At
    /// every mark the tracked item peers say what the twin's say, and every
    /// tracked row's own peer (the one under the open list's window) says what
    /// its item peer says.</summary>
    [Theory]
    [MemberData(nameof(Operations))]
    public void AStampedRowSaysWhatWpfsItemPeerSays(string arm) => OpenLists.RunSta(() =>
    {
        using var bench = new Bench();
        Arm chosen = Arms[arm];
        TwinRun tracked = TwinRun.Run(bench, chosen, tracked: true);
        TwinRun twin = TwinRun.Run(bench, chosen, tracked: false);

        Assert.Equal(twin.Marks.Select(mark => mark.Label), tracked.Marks.Select(mark => mark.Label));
        var wrong = new List<string>();
        for (int index = 0; index < tracked.Marks.Count; index++)
        {
            (string label, List<RowRead> rows) = tracked.Marks[index];
            List<RowRead> wpf = twin.Marks[index].Rows;
            string said = string.Join(" ", rows.Select(row => row.Item));
            string wpfSaid = string.Join(" ", wpf.Select(row => row.Item));
            if (said != wpfSaid)
            {
                wrong.Add($"[{label}] the tracked item peers say {said}; WPF alone says {wpfSaid}");
            }

            foreach (RowRead row in rows.Where(row => row.Own is not null && row.Own != row.Item))
            {
                wrong.Add($"[{label}] '{row.Name}': its own peer says {row.Own}; its item peer says {row.Item}");
            }
        }

        Assert.True(wrong.Count == 0, $"{arm}:\n  " + string.Join("\n  ", wrong));
    });

    public static TheoryData<string> Operations()
    {
        var data = new TheoryData<string>();
        foreach (string arm in Arms.Keys)
        {
            data.Add(arm);
        }

        return data;
    }

    /// <summary>Fact 3: under grouping WPF's item peer gives each row its place
    /// within its group, so the tracker stamps nothing and leaves WPF's answer,
    /// through a change that restamps while the grouped rows are realized; the
    /// open list's window then holds the item peers themselves, and the popup
    /// rule passes. With grouping off again the rows are restamped.</summary>
    [Fact]
    public void AGroupedListKeepsWpfsOwnPlaces() => OpenLists.RunSta(() =>
    {
        using var bench = new Bench();
        var fruit = new ObservableCollection<Fruit>
        {
            new("Apple"), new("Avocado"), new("Banana"), new("Blueberry"), new("Cherry"),
        };
        var view = new ListCollectionView(fruit);
        var combo = new ComboBox { ItemsSource = view, DisplayMemberPath = nameof(Fruit.Title), ItemContainerStyle = RowStyle() };
        combo.GroupStyle.Add(GroupStyle.Default);
        bench.Panel.Children.Add(combo);
        bench.Settle();
        bench.Open(combo);
        view.GroupDescriptions!.Add(new PropertyGroupDescription(nameof(Fruit.Group)));
        bench.Settle();
        // A change while grouped and open restamps with the grouped rows realized.
        fruit.Add(new Fruit("Cranberry"));
        bench.Settle();

        Assert.True(combo.IsGrouping);
        AssertNoRowIsStamped(combo, "grouped");
        Assert.Equal(
            "Apple:1/2 Avocado:2/2 Banana:1/2 Blueberry:2/2 Cherry:1/2 Cranberry:2/2",
            string.Join(" ", RowRead.All(combo).Select(row => $"{row.Name}:{row.Item}")));
        OpenLists.AssertPopupRule(combo, "grouped");

        view.GroupDescriptions.Clear();
        bench.Settle();
        Assert.False(combo.IsGrouping);
        Assert.Equal(
            "Apple:1/6 Avocado:2/6 Banana:3/6 Blueberry:4/6 Cherry:5/6 Cranberry:6/6",
            string.Join(" ", RowRead.All(combo).Select(row => $"{row.Name}:{row.Own}")));
    });

    /// <summary>Fact 4: a list that virtualizes and recycles its rows is left
    /// to WPF: no row holds a stamp, and the item peers are right after a
    /// scroll. Its copies under the open list's window keep today's defect,
    /// which is why the census refuses this shape.</summary>
    [Fact]
    public void ARecyclingListIsLeftToWpf() => OpenLists.RunSta(() =>
    {
        using var bench = new Bench();
        string[] rows = [.. Enumerable.Range(1, 300).Select(number => "row " + number)];
        var combo = new ComboBox
        {
            ItemsSource = rows,
            ItemContainerStyle = RowStyle(),
            ItemsPanel = new ItemsPanelTemplate(new FrameworkElementFactory(typeof(VirtualizingStackPanel))),
        };
        VirtualizingPanel.SetIsVirtualizing(combo, true);
        VirtualizingPanel.SetVirtualizationMode(combo, VirtualizationMode.Recycling);
        ScrollViewer.SetCanContentScroll(combo, true);
        bench.Panel.Children.Add(combo);
        bench.Settle();
        bench.Open(combo);
        Assert.True(ComboBoxRowPlaces.LeavesItToWpf(combo));
        AssertNoRowIsStamped(combo, "recycling, open");

        ScrollViewer scroller = Assert.Single(
            Descendants(Assert.IsType<Popup>(combo.Template!.FindName("PART_Popup", combo)).Child).OfType<ScrollViewer>());
        scroller.ScrollToVerticalOffset(150);
        bench.Settle();

        AssertNoRowIsStamped(combo, "recycling, scrolled");
        List<RowRead> realized = [.. RowRead.All(combo).Where(row => row.Own is not null)];
        Assert.NotEmpty(realized);
        Assert.All(realized, row => Assert.Equal($"{row.Index + 1}/300", row.Item));
        List<string> failures = OpenLists.PopupRuleFailures(OpenLists.PopupRows(combo, "recycling"));
        Assert.True(
            failures.Count == realized.Count,
            $"the recycling list's popup copies were expected to keep today's defect, every one: {failures.Count} of {realized.Count} do");
    });

    /// <summary>Fact 5: on every route that opens a list, the rows are stamped
    /// inside the open, so no dispatcher operation (and so no UI Automation
    /// call) starts with a realized row unstamped.</summary>
    [Theory]
    [InlineData("Alt+Down")]
    [InlineData("F4")]
    [InlineData("the toggle")]
    [InlineData("IsDropDownOpen")]
    [InlineData("UIA Expand")]
    public void EveryOpenRouteStampsBeforeARowCanBeRead(string route) => OpenLists.RunSta(() =>
    {
        using var bench = new Bench();
        (ComboBox combo, _) = Plain();
        bench.Panel.Children.Add(combo);
        bench.Settle();
        using var scan = new BoundaryScan(combo);
        switch (route)
        {
            case "Alt+Down":
                Assert.True(combo.Focus());
                CommandPaletteTests.ThreadModifiers.Hold(ModifierKeys.Alt, () => bench.Press(Key.Down));
                break;
            case "F4":
                Assert.True(combo.Focus());
                bench.Press(Key.F4);
                break;
            case "the toggle":
                // A raised MouseLeftButtonDown does not open the list; a press
                // checks the template's toggle, which is bound to the list.
                Descendants(combo).OfType<ToggleButton>().First().IsChecked = true;
                break;
            case "IsDropDownOpen":
                combo.IsDropDownOpen = true;
                break;
            case "UIA Expand":
                ((IExpandCollapseProvider)UIElementAutomationPeer.CreatePeerForElement(combo).GetPattern(PatternInterface.ExpandCollapse)).Expand();
                break;
        }

        bench.Settle();
        Assert.True(combo.IsDropDownOpen, $"{route} did not open the list.");
        Assert.True(scan.Boundaries > 0, $"{route}: no dispatcher operation started while the list was watched.");
        Assert.True(scan.Unstamped == 0, $"{route}: {scan}");
        OpenLists.AssertPopupRule(combo, route);
    });

    /// <summary>Fact 6: a combo box with no size never raises its own
    /// SizeChanged, and can still be opened through UI Automation. Its rows
    /// raise theirs inside the open, so it is tracked and stamped all the same.</summary>
    [Fact]
    public void AComboBoxWithNoSizeStillStampsItsRows() => OpenLists.RunSta(() =>
    {
        using var bench = new Bench();
        (ComboBox combo, _) = Plain();
        combo.Width = 0;
        combo.Height = 0;
        combo.MinWidth = 0;
        combo.MinHeight = 0;
        bench.Panel.Children.Add(combo);
        bench.Settle();
        Assert.True(combo.ActualWidth == 0 && combo.ActualHeight == 0, $"the combo box has a size: {combo.ActualWidth} by {combo.ActualHeight}");
        Assert.Null(ComboBoxRowPlaces.For(combo));
        using var scan = new BoundaryScan(combo);

        ((IExpandCollapseProvider)UIElementAutomationPeer.CreatePeerForElement(combo).GetPattern(PatternInterface.ExpandCollapse)).Expand();
        bench.Settle();

        Assert.True(combo.IsDropDownOpen, "UI Automation did not open the list.");
        Assert.True(scan.Unstamped == 0, scan.ToString());
        OpenLists.AssertPopupRule(combo, "no size");
    });

    /// <summary>Fact 7: a tracker keeps neither its combo box nor itself
    /// alive. Six combo boxes are attached and dropped; after a collection
    /// all six and their trackers are gone. WPF itself holds the last combo
    /// boxes opened, tracked or not, so where the six were opened four more
    /// are opened and closed after them.</summary>
    [Theory]
    [InlineData("never opened, a static source")]
    [InlineData("opened and closed, a static source")]
    [InlineData("opened and closed, a list of their own")]
    public void ATrackerDoesNotKeepItsComboBoxAlive(string arm) => OpenLists.RunSta(() =>
    {
        using var bench = new Bench();
        bool open = !arm.StartsWith("never", StringComparison.Ordinal);
        bool own = arm.EndsWith("of their own", StringComparison.Ordinal);
        (WeakReference Combo, WeakReference Tracker)[] dropped =
            [.. Enumerable.Range(0, 6).Select(_ => AttachAndDrop(bench, own, open))];
        if (open)
        {
            for (int more = 0; more < 4; more++)
            {
                _ = AttachAndDrop(bench, own: true, open: true);
            }
        }

        for (int round = 0; round < 6 && dropped.Any(pair => pair.Combo.IsAlive || pair.Tracker.IsAlive); round++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            bench.Settle();
        }

        Assert.Equal(
            "0 combo boxes and 0 trackers alive",
            $"{dropped.Count(pair => pair.Combo.IsAlive)} combo boxes and {dropped.Count(pair => pair.Tracker.IsAlive)} trackers alive");
    });

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference Combo, WeakReference Tracker) AttachAndDrop(Bench bench, bool own, bool open)
    {
        var combo = new ComboBox
        {
            ItemsSource = own ? new ObservableCollection<string> { "p", "q", "r" } : SlateWindows.Graph.ConnectionsPhrase.DepthTags,
            ItemContainerStyle = RowStyle(),
        };
        bench.Panel.Children.Add(combo);
        bench.Settle();
        ComboBoxRowPlaces tracker = Assert.IsType<ComboBoxRowPlaces>(ComboBoxRowPlaces.For(combo));
        if (open)
        {
            bench.Open(combo);
            combo.IsDropDownOpen = false;
            bench.Settle();
        }

        bench.Panel.Children.Remove(combo);
        bench.Settle();
        return (new WeakReference(combo), new WeakReference(tracker));
    }

    // ---- the bench and its reads ------------------------------------------------------

    /// <summary>The style the app's code-built rows take: named by their
    /// item, on the shell's row template by key.</summary>
    private static Style RowStyle() => ItemContainerNames.BySelf(typeof(ComboBoxItem));

    private static (ComboBox Combo, ObservableCollection<string> Rows) Plain()
    {
        var rows = new ObservableCollection<string> { "b", "d", "a", "c", "e" };
        return (new ComboBox { ItemsSource = rows, ItemContainerStyle = RowStyle(), SelectedIndex = 0 }, rows);
    }

    private sealed record Fruit(string Title)
    {
        public string Group => Title[..1];
    }

    private static void AssertNoRowIsStamped(ComboBox combo, string label)
    {
        var stamped = new List<string>();
        for (int index = 0; index < combo.Items.Count; index++)
        {
            if (OpenLists.ContainerAt(combo, index) is { } container
                && (container.ReadLocalValue(AutomationProperties.PositionInSetProperty) != DependencyProperty.UnsetValue
                    || container.ReadLocalValue(AutomationProperties.SizeOfSetProperty) != DependencyProperty.UnsetValue))
            {
                stamped.Add($"row {index + 1}: {AutomationProperties.GetPositionInSet(container)} of {AutomationProperties.GetSizeOfSet(container)}");
            }
        }

        Assert.True(stamped.Count == 0, $"{label}: rows that hold a stamp: {string.Join(", ", stamped)}");
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, index);
            yield return child;
            foreach (DependencyObject nested in Descendants(child))
            {
                yield return nested;
            }
        }
    }

    /// <summary>A window of the app's theme layers, shown off-screen and never
    /// activated, with the tracker registered as the window registers it.</summary>
    private sealed class Bench : IDisposable
    {
        private ResourceDictionary[] _layers;

        public Bench()
        {
            ComboBoxRowPlaces.Register();
            RuntimeHelpers.RunClassConstructor(typeof(Application).TypeHandle);
            Window = new Window
            {
                Width = 800,
                Height = 600,
                ShowActivated = false,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = -20_000,
                Top = -20_000,
            };
            _layers = ThemeManager.DictionariesFor(SlateTheme.Light, highContrast: false);
            foreach (ResourceDictionary layer in _layers)
            {
                Window.Resources.MergedDictionaries.Add(layer);
            }

            Window.Content = new StackPanel { Children = { Elsewhere, Panel, Other } };
            Window.Show();
            Settle();
        }

        public Window Window { get; }

        public Button Elsewhere { get; } = new() { Content = "elsewhere" };

        public StackPanel Panel { get; } = new();

        public StackPanel Other { get; } = new();

        public void Settle()
        {
            Window.UpdateLayout();
            PumpedDispatcher.Drain();
            Window.UpdateLayout();
            PumpedDispatcher.Drain();
        }

        public void Open(ComboBox combo)
        {
            combo.IsDropDownOpen = true;
            Settle();
            Assert.True(combo.IsDropDownOpen, "the list did not open.");
        }

        /// <summary>The app's theme layers swapped for a new set, as a theme
        /// change from Settings swaps them.</summary>
        public void Theme(SlateTheme theme)
        {
            foreach (ResourceDictionary layer in _layers)
            {
                _ = Window.Resources.MergedDictionaries.Remove(layer);
            }

            _layers = ThemeManager.DictionariesFor(theme, highContrast: false);
            foreach (ResourceDictionary layer in _layers)
            {
                Window.Resources.MergedDictionaries.Add(layer);
            }
        }

        public void Press(Key key)
        {
            InputManager.Current.ProcessInput(new KeyEventArgs(
                Keyboard.PrimaryDevice, PresentationSource.FromVisual(Window)!, Environment.TickCount, key)
            {
                RoutedEvent = Keyboard.PreviewKeyDownEvent,
            });
            Settle();
        }

        public void Dispose() => Window.Close();
    }

    /// <summary>One row as UI Automation reads it: its index in the items, its
    /// name, what its item peer says, and what its own peer (its copy under the
    /// open list's window) says, or null for a row with no container.</summary>
    private sealed record RowRead(int Index, string Name, string Item, string? Own)
    {
        public static List<RowRead> All(ComboBox combo)
        {
            AutomationPeer comboPeer = UIElementAutomationPeer.CreatePeerForElement(combo);
            comboPeer.ResetChildrenCache();
            ItemAutomationPeer[] itemPeers = [.. Flatten(comboPeer.GetChildren()).OfType<ItemAutomationPeer>()];
            var rows = new List<RowRead>();
            for (int index = 0; index < combo.Items.Count; index++)
            {
                object item = combo.Items[index];
                string name = item switch
                {
                    string text => text,
                    Fruit fruit => fruit.Title,
                    _ => item?.ToString() ?? "(null)",
                };
                AutomationPeer? own = OpenLists.ContainerAt(combo, index) is UIElement container
                    ? UIElementAutomationPeer.CreatePeerForElement(container)
                    : null;
                AutomationPeer? peer = own?.EventsSource as ItemAutomationPeer
                    ?? own as ItemAutomationPeer
                    ?? itemPeers.FirstOrDefault(candidate => Equals(candidate.Item, item));
                rows.Add(new RowRead(index, name, Said(peer), own is null ? null : Said(own)));
            }

            return rows;
        }

        private static string Said(AutomationPeer? peer) =>
            peer is null ? "none" : $"{peer.GetPositionInSet()}/{peer.GetSizeOfSet()}";

        private static IEnumerable<AutomationPeer> Flatten(IEnumerable<AutomationPeer>? peers)
        {
            foreach (AutomationPeer peer in peers ?? [])
            {
                yield return peer;
                if (peer is GroupItemAutomationPeer)
                {
                    peer.ResetChildrenCache();
                    foreach (AutomationPeer nested in Flatten(peer.GetChildren()))
                    {
                        yield return nested;
                    }
                }
            }
        }
    }

    private sealed record Arm(Func<(ComboBox Combo, ObservableCollection<string> Rows)> Make, Action<TwinRun> Script);

    /// <summary>One run of an arm: the combo box on the bench, the script, and
    /// a read of every row at each mark. The twin's stamps are cleared before
    /// each read, so it reads what WPF says with no stamp.</summary>
    private sealed class TwinRun(Bench bench, ComboBox combo, ObservableCollection<string> rows, bool tracked)
    {
        public Bench Bench => bench;

        public ComboBox Combo => combo;

        public ObservableCollection<string> Rows => rows;

        public List<(string Label, List<RowRead> Rows)> Marks { get; } = [];

        public static TwinRun Run(Bench bench, Arm arm, bool tracked)
        {
            bench.Theme(SlateTheme.Light);
            bench.Settle();
            (ComboBox combo, ObservableCollection<string> rows) = arm.Make();
            bench.Panel.Children.Add(combo);
            bench.Settle();
            Assert.NotNull(ComboBoxRowPlaces.For(combo));
            var run = new TwinRun(bench, combo, rows, tracked);
            arm.Script(run);
            combo.IsDropDownOpen = false;
            bench.Settle();
            if (combo.Parent is Panel parent)
            {
                parent.Children.Remove(combo);
            }

            bench.Settle();
            return run;
        }

        public void Settle() => bench.Settle();

        public void Open(bool open = true)
        {
            combo.IsDropDownOpen = open;
            bench.Settle();
        }

        public void Mark(string label)
        {
            if (!tracked)
            {
                for (int index = 0; index < combo.Items.Count; index++)
                {
                    if (OpenLists.ContainerAt(combo, index) is { } container)
                    {
                        container.ClearValue(AutomationProperties.PositionInSetProperty);
                        container.ClearValue(AutomationProperties.SizeOfSetProperty);
                    }
                }
            }

            Marks.Add((label, RowRead.All(combo)));
        }
    }

    private static readonly Dictionary<string, Arm> Arms = BuildArms();

    private static Dictionary<string, Arm> BuildArms()
    {
        var arms = new Dictionary<string, Arm>(StringComparer.Ordinal);
        (string Name, Action<TwinRun> Do)[] operations =
        [
            ("insert", run => run.Rows.Insert(0, "NEW")),
            ("remove", run => run.Rows.RemoveAt(2)),
            ("move", run => run.Rows.Move(0, run.Rows.Count - 1)),
            ("replace", run => run.Rows[1] = "REPL"),
            ("clear", run => run.Rows.Clear()),
            ("sort", run => run.Combo.Items.SortDescriptions.Add(new SortDescription(string.Empty, ListSortDirection.Ascending))),
            ("filter", run => run.Combo.Items.Filter = row => (string)row != "d"),
            ("source swap", run => run.Combo.ItemsSource = new ObservableCollection<string> { "x", "y", "z" }),
        ];
        foreach ((string name, Action<TwinRun> operation) in operations)
        {
            arms[$"{name}, while open"] = new(Plain, run =>
            {
                run.Open();
                run.Mark("open before");
                operation(run);
                run.Mark("after, same turn");
                run.Settle();
                run.Mark("after, settled");
                run.Open(false);
                run.Mark("closed");
                run.Open();
                run.Mark("reopened");
            });
            arms[$"{name}, while closed after an open"] = new(Plain, run =>
            {
                run.Open();
                run.Open(false);
                run.Mark("closed before");
                operation(run);
                run.Mark("after, same turn");
                run.Settle();
                run.Mark("after, settled, still closed");
                run.Combo.IsDropDownOpen = true;
                run.Mark("opened, same turn");
                run.Settle();
                run.Mark("opened, settled");
            });
            arms[$"{name}, before the first open"] = new(Plain, run =>
            {
                operation(run);
                run.Settle();
                run.Mark("after, never opened");
                run.Combo.IsDropDownOpen = true;
                run.Mark("first open, same turn");
                run.Settle();
                run.Mark("first open, settled");
            });
        }

        arms["first open, close and reopen"] = new(Plain, run =>
        {
            run.Mark("never opened");
            run.Combo.IsDropDownOpen = true;
            run.Mark("opened, same turn");
            run.Settle();
            run.Mark("opened, settled");
            run.Open(false);
            run.Mark("closed");
            run.Combo.IsDropDownOpen = true;
            run.Mark("reopened, same turn");
            run.Settle();
            run.Mark("reopened, settled");
        });
        arms["a collapsed row"] = new(Plain, run =>
        {
            run.Open();
            run.Mark("open before");
            ((UIElement)run.Combo.ItemContainerGenerator.ContainerFromIndex(1)).Visibility = Visibility.Collapsed;
            run.Mark("row 2 collapsed, same turn");
            run.Settle();
            run.Mark("row 2 collapsed, settled");
            run.Open(false);
            run.Open();
            run.Mark("reopened");
        });
        arms["two equal items"] = new(
            () =>
            {
                var rows = new ObservableCollection<string> { "a", "b", "a", "c" };
                return (new ComboBox { ItemsSource = rows, ItemContainerStyle = RowStyle() }, rows);
            },
            run =>
            {
                run.Open();
                run.Mark("open");
            });
        arms["a changed selection"] = new(Plain, run =>
        {
            run.Open();
            run.Mark("open, the first chosen");
            run.Combo.SelectedIndex = 3;
            run.Mark("the fourth chosen while open, same turn");
            run.Settle();
            run.Mark("the fourth chosen, settled");
            run.Open(false);
            run.Combo.SelectedIndex = 1;
            run.Settle();
            run.Mark("the second chosen while closed");
            run.Open();
            run.Mark("opened");
        });
        arms["out of the tree and back"] = new(Plain, run =>
        {
            run.Open();
            run.Open(false);
            run.Mark("opened once, closed");
            run.Bench.Panel.Children.Remove(run.Combo);
            run.Settle();
            run.Mark("out of the tree");
            run.Rows.Insert(0, "NEW");
            run.Rows.RemoveAt(3);
            run.Settle();
            run.Mark("changed while out of the tree");
            run.Bench.Panel.Children.Add(run.Combo);
            run.Settle();
            run.Mark("back in the tree, closed");
            run.Combo.IsDropDownOpen = true;
            run.Mark("opened, same turn");
            run.Settle();
            run.Mark("opened, settled");
        });
        arms["another parent"] = new(Plain, run =>
        {
            run.Open();
            run.Open(false);
            run.Bench.Panel.Children.Remove(run.Combo);
            run.Bench.Other.Children.Add(run.Combo);
            run.Settle();
            run.Mark("in the other parent, closed");
            run.Open();
            run.Mark("opened in the other parent");
            run.Rows.Insert(1, "NEW");
            run.Settle();
            run.Mark("a row inserted there");
        });
        arms["the theme swapped while open"] = new(Plain, run =>
        {
            run.Open();
            run.Mark("open, light");
            run.Bench.Theme(SlateTheme.Dark);
            run.Mark("dark, same turn");
            run.Settle();
            run.Mark("dark, settled");
            run.Open(false);
            run.Open();
            run.Mark("reopened");
        });
        arms["the theme swapped while closed after an open"] = new(Plain, run =>
        {
            run.Open();
            run.Open(false);
            run.Bench.Theme(SlateTheme.Dark);
            run.Settle();
            run.Mark("dark while closed");
            run.Combo.IsDropDownOpen = true;
            run.Mark("opened, same turn");
            run.Settle();
            run.Mark("opened, settled");
        });
        arms["the row template swapped"] = new(Plain, run =>
        {
            run.Open();
            run.Mark("open");
            object fresh = ThemeManager.DictionariesFor(SlateTheme.Light, highContrast: false)
                .First(layer => layer.Contains("Slate.ComboRowTemplate"))["Slate.ComboRowTemplate"];
            run.Bench.Window.Resources["Slate.ComboRowTemplate"] = fresh;
            run.Mark("template swapped, same turn");
            run.Settle();
            run.Mark("template swapped, settled");
            run.Open(false);
            run.Open();
            run.Mark("reopened");
            run.Bench.Window.Resources.Remove("Slate.ComboRowTemplate");
        });
        arms["a deferral"] = new(Plain, run =>
        {
            // A combo box on a static array, whose default view every control on
            // that array shares, is laid out (and so attached, and restamped)
            // while that view is held in DeferRefresh, which refuses Count and
            // IndexOf. WPF itself throws under a deferral when the source changes
            // (its view reads CurrentPosition) and when a layout pass updates a
            // peer of an items control on that view (the peer reads Count),
            // tracked or not; so the combo box is laid out on its own, out of
            // the window, and the source does not change during the deferral.
            string[] shared = ["p", "q", "r"];
            var late = new ComboBox { ItemsSource = shared, ItemContainerStyle = RowStyle() };
            using (CollectionViewSource.GetDefaultView(shared).DeferRefresh())
            {
                late.Measure(new Size(200, 40));
                late.Arrange(new Rect(0, 0, 200, 40));
                late.UpdateLayout();
                Assert.NotNull(ComboBoxRowPlaces.For(late));
            }

            run.Bench.Other.Children.Add(late);
            run.Settle();
            run.Bench.Open(late);
            OpenLists.AssertPopupRule(late, "a combo box attached during a deferral");
            late.IsDropDownOpen = false;
            run.Bench.Other.Children.Remove(late);
            run.Settle();
            run.Open();
            run.Mark("opened");
        });
        return arms;
    }

    /// <summary>Counts the dispatcher operations that start while the combo
    /// box has a realized row whose stamp is missing or wrong. A UI Automation
    /// call is such an operation, so each is a moment a client could have read
    /// a row with no place.</summary>
    private sealed class BoundaryScan : IDisposable
    {
        private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
        private readonly DispatcherHookEventHandler _handler;
        private readonly ComboBox _combo;
        private string? _first;

        public BoundaryScan(ComboBox combo)
        {
            _combo = combo;
            _handler = (_, _) => Check();
            _dispatcher.Hooks.OperationStarted += _handler;
        }

        public int Boundaries { get; private set; }

        public int Unstamped { get; private set; }

        private void Check()
        {
            if (ComboBoxRowPlaces.LeavesItToWpf(_combo))
            {
                return;
            }

            Boundaries++;
            ItemCollection items = _combo.Items;
            for (int index = 0; index < items.Count; index++)
            {
                if (_combo.ItemContainerGenerator.ContainerFromIndex(index) is { } container
                    && (AutomationProperties.GetPositionInSet(container) != items.IndexOf(_combo.ItemContainerGenerator.ItemFromContainer(container)) + 1
                        || AutomationProperties.GetSizeOfSet(container) != items.Count))
                {
                    Unstamped++;
                    _first ??= $"row {index + 1} '{items[index]}' said {AutomationProperties.GetPositionInSet(container)} of {AutomationProperties.GetSizeOfSet(container)}, open={_combo.IsDropDownOpen}";
                    return;
                }
            }
        }

        public override string ToString() =>
            $"{Boundaries} dispatcher operations started; {Unstamped} of them with a realized row whose stamp was missing or wrong{(_first is null ? string.Empty : " (first: " + _first + ")")}";

        public void Dispose() => _dispatcher.Hooks.OperationStarted -= _handler;
    }
}

/// <summary>CB-19: the shell, the keys and the reads the open-list facts share.</summary>
internal static class OpenLists
{
    internal const string BaseYaml =
        "filters: 'file.ext == \"md\"'\nviews:\n  - type: table\n    name: Main\n    order:\n      - file.name\n"
        + "  - type: list\n    name: Rows\n    order:\n      - file.name\n";

    internal static void RunSta(Action body) =>
        StaThread.RunPumped(body, TimeSpan.FromSeconds(120), "The open-list fact timed out.");

    /// <summary>The shipped window over a vault with a linked note, two notes
    /// that carry the key Bulk rename previews, and a base with two views.</summary>
    internal static ShownShell NewShell() => new(
        ("alpha.md", "---\nstatus: todo\n---\n\n# Alpha\n\n[[beta]]\n"),
        ("beta.md", "---\nkind: x\n---\n\n# Beta\n"),
        ("gamma.md", "---\nkind: y\n---\n\n# Gamma\n"),
        ("Notes.base", BaseYaml));

    internal static void Settle(ShownShell host)
    {
        host.Settle();
        host.Settle();
    }

    /// <summary>The combo box <paramref name="key"/> names, its surface opened
    /// in the shell and the combo box shown, ready to open.</summary>
    internal static ComboBox Reach(ShownShell host, string key)
    {
        switch (key)
        {
            case "SettingsTheme":
                host.Lifecycle.OpenSettingsCommand.Execute(null);
                Settle(host);
                return host.Shell.SettingsThemeComboBox;
            case "SettingsAccent":
                host.Lifecycle.OpenSettingsCommand.Execute(null);
                Settle(host);
                return host.Shell.SettingsAccentComboBox;
            case "AddPropertyType":
                host.Workspace.OpenAddPropertySheet(synchronousForTests: true);
                return Shown(host, key, _ => true);
            case "BulkRenameOldKeyType":
                host.Workspace.OpenBulkRenameSheet(synchronousForTests: true);
                return Shown(host, key, _ => true);
            case "DashboardEditorQueryPicker":
                SeedASavedQuery(host);
                host.Workspace.OpenDashboardEditor(null);
                return Shown(host, key, combo => combo.Items.Count > 0);
            case "View override":
                host.Workspace.OpenDashboardEditor(null);
                Settle(host);
                host.Workspace.DashboardEditorSheet!.Sections.Add(new DashboardEditorSection("q1", "First query"));
                return Shown(
                    host,
                    combo => AutomationProperties.GetName(combo).StartsWith("View override for", StringComparison.Ordinal),
                    key);
            case "BuilderCombinator":
                host.Workspace.BasesNewQueryCommand.Execute(null);
                Settle(host);
                return host.Shell.BuilderCombinatorBox;
            case "BaseViewPicker":
                host.Workspace.OpenPath("Notes.base", WorkspaceOpenTarget.NewTab);
                return Shown(host, key, combo => combo.Items.Count > 1);
            case "ConnectionsDepth":
                host.Workspace.OpenPath("alpha.md", WorkspaceOpenTarget.CurrentTab);
                host.Workspace.ActiveLeaf = Leaf("connections");
                host.Workspace.IsRightPaneVisible = true;
                return Shown(host, key, combo => combo.IsEnabled);
            case "GraphInspectorGroupRing":
            case "GraphInspectorGroupColour":
                // The inspector's sections are enabled only while a graph is in effect.
                host.Workspace.OpenGraph();
                host.Workspace.ActiveLeaf = Leaf("inspector");
                host.Workspace.IsRightPaneVisible = true;
                Assert.True(
                    PumpedDispatcher.PumpUntil(() => host.Workspace.Inspector.IsGraphEffective, TimeSpan.FromSeconds(15)),
                    $"{key}: the graph never came into effect for the inspector.");
                host.Workspace.Inspector.AddGroup();
                return Shown(host, key + ":1", combo => combo.IsEnabled && combo.Items.Count > 0);
            default:
                throw new ArgumentOutOfRangeException(nameof(key), key, "no combo box is reached by this key");
        }
    }

    internal static WorkspaceLeafOption Leaf(string id) =>
        WorkspaceViewModel.Leaves.Single(leaf => leaf.Id == id);

    /// <summary>A saved query in the session's registry, and in the list the
    /// dashboard editor's picker binds to (the workspace's own refresh publishes
    /// it from a pool thread).</summary>
    internal static void SeedASavedQuery(ShownShell host)
    {
        ulong scratch = host.Session.OpenBase("Notes.base");
        string queryJson;
        try
        {
            queryJson = host.Session.BaseViewQueryJson(scratch, 0);
        }
        finally
        {
            host.Session.CloseBase(scratch);
        }

        _ = host.Session.SaveQuery("Open notes", null, queryJson, SavedQuerySourceSyntax.Builder);
        foreach (SavedQuerySummary summary in host.Session.ListSavedQueries())
        {
            host.Workspace.SavedQueries.Add(summary);
        }

        Settle(host);
    }

    internal static ComboBox Shown(ShownShell host, string automationId, Func<ComboBox, bool> ready) =>
        Shown(host, combo => AutomationProperties.GetAutomationId(combo) == automationId && ready(combo), automationId);

    internal static ComboBox Shown(ShownShell host, Func<ComboBox, bool> which, string label)
    {
        ComboBox? found = null;
        bool shown = PumpedDispatcher.PumpUntil(
            () =>
            {
                host.Shell.UpdateLayout();
                found = Combos(host.Shell).FirstOrDefault(combo => combo.IsVisible && which(combo));
                return found is not null;
            },
            TimeSpan.FromSeconds(15));
        Assert.True(shown, $"{label}: no such combo box was shown in the shell.");
        return found!;
    }

    /// <summary>Every combo box under <paramref name="root"/>, by the visual
    /// and the logical tree both (a collapsed sheet's are in the logical one).</summary>
    internal static ComboBox[] Combos(DependencyObject root) => [.. Everything(root).OfType<ComboBox>()];

    internal static ComboBox ById(ShownShell host, string automationId) =>
        Assert.Single(Combos(host.Shell), combo => AutomationProperties.GetAutomationId(combo) == automationId);

    private static IEnumerable<DependencyObject> Everything(DependencyObject root)
    {
        var seen = new HashSet<DependencyObject>(ReferenceEqualityComparer.Instance);
        var pending = new Stack<DependencyObject>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            DependencyObject node = pending.Pop();
            if (!seen.Add(node))
            {
                continue;
            }

            yield return node;
            if (node is Visual or System.Windows.Media.Media3D.Visual3D)
            {
                for (int index = 0; index < VisualTreeHelper.GetChildrenCount(node); index++)
                {
                    pending.Push(VisualTreeHelper.GetChild(node, index));
                }
            }

            foreach (DependencyObject child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>())
            {
                pending.Push(child);
            }
        }
    }

    /// <summary>The keys put on the combo box and its list opened, as a keyboard
    /// user's Alt+Down leaves it: the keys move into the list.</summary>
    internal static void Open(ShownShell host, ComboBox combo, string label)
    {
        Assert.True(combo.Focus(), $"{label}: the combo box took no keys.");
        combo.IsDropDownOpen = true;
        Settle(host);
        Assert.True(combo.IsDropDownOpen, $"{label}: the list did not open.");
    }

    /// <summary>A key pressed where the keys are, through WPF's input manager:
    /// its preview tunnels from the window through every handler on the way,
    /// and an unhandled one is promoted to KeyDown, as the keyboard does.</summary>
    internal static void Press(ShownShell host, Key key)
    {
        Assert.True(
            PumpedDispatcher.PumpUntil(() => Keyboard.Modifiers == ModifierKeys.None, TimeSpan.FromSeconds(5)),
            $"A real modifier key is held on this desktop ({Keyboard.Modifiers}); these facts press unmodified keys.");
        InputManager.Current.ProcessInput(new KeyEventArgs(
            Keyboard.PrimaryDevice, PresentationSource.FromVisual(host.Shell)!, Environment.TickCount, key)
        {
            RoutedEvent = Keyboard.PreviewKeyDownEvent,
        });
        Settle(host);
    }

    /// <summary>The popup rule. Walking the peers from the open list's own
    /// window reaches one ListItem for each realized row, and at least one;
    /// each is paired with its row's item peer through its events source, not
    /// by name; and each says a place from 1 to its set's size, the place WPF's
    /// item peer says for the same row.</summary>
    internal static void AssertPopupRule(ComboBox combo, string label)
    {
        List<AutomationPeer> rows = PopupRows(combo, label);
        int realized = Enumerable.Range(0, combo.Items.Count).Count(index => ContainerAt(combo, index) is not null);
        string seen = string.Join(", ", rows.Select(row => $"'{row.GetName()}' {row.GetPositionInSet()} of {row.GetSizeOfSet()}"));
        Assert.True(
            rows.Count >= 1 && rows.Count == realized && realized == combo.Items.Count,
            $"{label}: {rows.Count} ListItem peers under the open list's window, for {realized} realized rows of {combo.Items.Count}: {seen}");

        List<string> wrong = PopupRuleFailures(rows);
        Assert.True(
            wrong.Count == 0,
            $"{label}: rows under the open list's window that do not say their place as WPF's item peer does:\n  "
            + string.Join("\n  ", wrong));
    }

    /// <summary>A realized row's container, by its index in the items (under
    /// grouping the generator's own indexes are the groups').</summary>
    internal static DependencyObject? ContainerAt(ComboBox combo, int index) =>
        combo.IsGrouping
            ? combo.ItemContainerGenerator.ContainerFromItem(combo.Items[index])
            : combo.ItemContainerGenerator.ContainerFromIndex(index);

    /// <summary>Every ListItem peer under the open list's own window.</summary>
    internal static List<AutomationPeer> PopupRows(ComboBox combo, string label)
    {
        Assert.True(combo.IsDropDownOpen, $"{label}: the list is not open.");
        Popup popup = Assert.IsType<Popup>(combo.Template?.FindName("PART_Popup", combo));
        Assert.True(popup.IsOpen, $"{label}: the list says it is open and its popup is not.");
        UIElement? root = popup.Child is { } child && PresentationSource.FromVisual(child) is HwndSource { RootVisual: UIElement visual }
            ? visual
            : null;
        Assert.True(root is not null, $"{label}: the open list has no window of its own.");
        var rows = new List<AutomationPeer>();
        CollectListItems(UIElementAutomationPeer.CreatePeerForElement(root!), rows);
        return rows;
    }

    /// <summary>The rows of <paramref name="rows"/> that are paired with no
    /// item peer, or say a place other than their item peer's, or none.</summary>
    internal static List<string> PopupRuleFailures(List<AutomationPeer> rows)
    {
        var paired = new HashSet<AutomationPeer>(ReferenceEqualityComparer.Instance);
        var wrong = new List<string>();
        foreach (AutomationPeer row in rows)
        {
            if ((row as ItemAutomationPeer ?? row.EventsSource as ItemAutomationPeer) is not { } item)
            {
                wrong.Add($"'{row.GetName()}' is paired with no item peer");
                continue;
            }

            if (!paired.Add(item))
            {
                wrong.Add($"'{row.GetName()}' is paired with an item peer another row already has");
            }

            int position = row.GetPositionInSet();
            int size = row.GetSizeOfSet();
            int itemPosition = item.GetPositionInSet();
            int itemSize = item.GetSizeOfSet();
            if (position < 1 || position > size || position != itemPosition || size != itemSize)
            {
                wrong.Add($"'{row.GetName()}': the popup's copy says {position} of {size}; WPF's item peer says {itemPosition} of {itemSize}");
            }
        }

        return wrong;
    }

    private static void CollectListItems(AutomationPeer peer, List<AutomationPeer> rows)
    {
        if (peer.GetAutomationControlType() == AutomationControlType.ListItem)
        {
            rows.Add(peer);
        }

        peer.ResetChildrenCache();
        foreach (AutomationPeer child in peer.GetChildren() ?? [])
        {
            CollectListItems(child, rows);
        }
    }
}
