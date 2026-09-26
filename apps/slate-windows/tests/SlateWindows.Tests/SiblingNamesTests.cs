// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.ObjectModel;
using System.Dynamic;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace SlateWindows.Tests;

/// <summary>
/// W7-7 PR 3 (#1246, R-4: a duplicate sibling is told apart; the spec
/// review, round 21): <see cref="SiblingNames"/>, the one collision rule
/// every named items host uses — pure, then hosted on a list, a tree and a
/// closed combo, as UIA reads the containers.
/// </summary>
public sealed class SiblingNamesTests
{
    [Fact]
    public void DistinctNamesReadBare() =>
        Assert.Equal(
            ["alpha", "beta"],
            SiblingNames.Compose(["alpha", "beta"], ["A", "B"], "item"));

    /// <summary>Namesakes take their distinguisher, and namesakes that share
    /// one — ignoring case, as speech does (one file in two tabs) — both
    /// take their ordinal; a namesake with no distinguisher keeps its name
    /// once the others read apart from it, and takes its ordinal while one
    /// still reads like it.</summary>
    [Fact]
    public void NamesakesReadTheirDistinguisherElseTheirOrdinal()
    {
        Assert.Equal(
            ["note.md, A", "note.md, tab 2", "Note.MD, tab 3", "other.md", "note.md"],
            SiblingNames.Compose(
                ["note.md", "note.md", "Note.MD", "other.md", "note.md"],
                ["A", "B", "B", "C", null],
                "tab"));
        Assert.Equal(
            ["note.md, tab 1", "note.md, tab 2"],
            SiblingNames.Compose(["note.md", "note.md"], [null, null], "tab"));
    }

    /// <summary>Uniqueness holds AFTER the suffixes (the spec review,
    /// rounds 21 and 22): a natural name that reads like a suffixed one sends
    /// the whole colliding group to its ordinal form. "note.md (2)", a copy's
    /// name in another tool's shape, is not this rule's shape and meets no
    /// one: it reads as itself.</summary>
    [Fact]
    public void ASuffixThatMeetsANaturalNameFallsBackToTheOrdinal() =>
        Assert.Equal(
            ["note.md, item 1", "note.md, A, item 2", "note.md, B", "note.md (2)"],
            SiblingNames.Compose(
                ["note.md", "note.md, A", "note.md", "note.md (2)"],
                ["A", null, "B", null],
                "item"));

    /// <summary>A blank name is never nothing, and a pathological natural
    /// name that equals an ordinal form still ends distinct.</summary>
    [Fact]
    public void BlankAndPathologicalNamesEndDistinct()
    {
        Assert.Equal(["Item 1", "Item 2", "x"], SiblingNames.Compose([" ", null, "x"], [], "item"));
        string[] names = SiblingNames.Compose(["a", "a", "a, item 1", "Item 1"], [], "item");
        Assert.Equal(names.Length, names.Distinct(StringComparer.CurrentCultureIgnoreCase).Count());
    }

    /// <summary>Hosted on a list: UIA reads the rule's names — two equal
    /// strings are two rows — and follows an add, a removal and a rename.</summary>
    [Fact]
    public void AListReadsItsItemsApartAndFollowsTheirChanges() => RunSta(() =>
    {
        var items = new ObservableCollection<object>
        {
            Row("note.md", "A"),
            Row("note.md", "B"),
            Row("other.md", "C"),
        };
        var host = new ListBox { ItemContainerStyle = SiblingNames.ContainerStyle(typeof(ListBoxItem)) };
        SiblingNames.SetNamePath(host, "Name");
        SiblingNames.SetDistinguisherPath(host, "Path");
        host.ItemsSource = items;
        Hosted(host, () =>
        {
            Assert.Equal(["note.md, A", "note.md, B", "other.md"], Names(host));

            items.RemoveAt(1);
            Assert.Equal(["note.md", "other.md"], Names(host));

            ((IDictionary<string, object?>)items[1])["Name"] = "note.md";
            Assert.Equal(["note.md, A", "note.md, C"], Names(host));

            items.Add(Row("fresh.md", "D"));
            Assert.Equal(["note.md, A", "note.md, C", "fresh.md"], Names(host));
        });

        // Two rows with no distinguisher read their ordinals, in the host's
        // own noun.
        var warnings = new ListBox { ItemContainerStyle = SiblingNames.ContainerStyle(typeof(ListBoxItem)) };
        SiblingNames.SetNamePath(warnings, "Name");
        SiblingNames.SetNoun(warnings, "warning");
        warnings.ItemsSource = new[] { Row("Missing file", null), Row("Missing file", null) };
        Hosted(warnings, () => Assert.Equal(["Missing file, warning 1", "Missing file, warning 2"], Names(warnings)));
    });

    /// <summary>A RECYCLING list hands a container that scrolled away to
    /// another item without re-applying its style, and the name binding's
    /// source — the container itself — never changes: every reused
    /// container must still read its new item's name. (The Files tree, the
    /// filter results and the dual pane recycle.)</summary>
    [Fact]
    public void ARecyclingListNamesEachReusedContainerForItsNewItem() => RunSta(() =>
    {
        var items = new ObservableCollection<object>();
        for (int index = 0; index < 300; index++)
        {
            items.Add(Row(index % 3 == 0 ? "shared.md" : $"note {index}.md", $"folder {index}"));
        }
        string[] expected = SiblingNames.Compose(
            [.. items.Select(item => (string?)((IDictionary<string, object?>)item)["Name"])],
            [.. items.Select(item => (string?)((IDictionary<string, object?>)item)["Path"])],
            "item");
        var host = new ListBox
        {
            ItemContainerStyle = SiblingNames.ContainerStyle(typeof(ListBoxItem)),
            Height = 150,
        };
        VirtualizingPanel.SetIsVirtualizing(host, true);
        VirtualizingPanel.SetVirtualizationMode(host, VirtualizationMode.Recycling);
        SiblingNames.SetNamePath(host, "Name");
        SiblingNames.SetDistinguisherPath(host, "Path");
        host.ItemsSource = items;
        Hosted(host, () =>
        {
            void Expect(string where)
            {
                host.UpdateLayout();
                PumpedDispatcher.Drain();
                int realized = 0;
                for (int index = 0; index < items.Count; index++)
                {
                    if (host.ItemContainerGenerator.ContainerFromIndex(index) is ListBoxItem container)
                    {
                        realized++;
                        string read = AutomationProperties.GetName(container);
                        Assert.True(
                            read == expected[index],
                            $"{where}: row {index} reads \"{read}\", not \"{expected[index]}\"");
                    }
                }
                Assert.True(realized is > 0 and < 100, $"{where}: {realized} rows realized — the list does not virtualize");
            }

            Expect("at the top");
            host.ScrollIntoView(items[150]);
            Expect("mid-way");
            host.ScrollIntoView(items[^1]);
            Expect("at the end");
            host.ScrollIntoView(items[0]);
            Expect("back at the top");
        });
    });

    /// <summary>A batch of changes — a folder resorted row by row, five
    /// hundred filter results added one at a time — is ONE refresh of the
    /// names, once the batch is done: each refresh reads every item, so one
    /// per change would be quadratic.</summary>
    [Fact]
    public void ABatchOfChangesIsOneRefresh() => RunSta(() =>
    {
        var items = new ObservableCollection<object>();
        var host = new ListBox
        {
            ItemContainerStyle = SiblingNames.ContainerStyle(typeof(ListBoxItem)),
            Height = 150,
        };
        SiblingNames.SetNamePath(host, "Name");
        host.ItemsSource = items;
        Hosted(host, () =>
        {
            host.UpdateLayout();
            PumpedDispatcher.Drain();
            int before = SiblingNames.RefreshesForTests(host);
            Assert.True(before >= 0, "the host declares no scope");
            for (int index = 0; index < 500; index++)
            {
                items.Add(Row($"note {index % 250}.md", null));
            }
            Assert.Equal(before, SiblingNames.RefreshesForTests(host));
            host.UpdateLayout();
            PumpedDispatcher.Drain();
            Assert.Equal(before + 1, SiblingNames.RefreshesForTests(host));
            // ...and that one refresh named them: each name twice, told apart
            // by place.
            var first = (ListBoxItem)host.ItemContainerGenerator.ContainerFromIndex(0);
            Assert.Equal("note 0.md, item 1", AutomationProperties.GetName(first));
        });
    });

    /// <summary>UIA names an item no container holds — a closed combo's
    /// selection, a row a virtualized list has not realized — through a
    /// throwaway wrapper container that sits in no panel, and it reuses that
    /// wrapper for the next such item. The wrapper must read its item's name
    /// under the rule (never the item's ToString, which the Bases and Graph
    /// inspector journeys heard as a record dump), and read again for every
    /// item it is handed.</summary>
    [Fact]
    public void AnUnrealizedItemIsNamedThroughTheRuleToo() => RunSta(() =>
    {
        var host = new ComboBox { ItemContainerStyle = SiblingNames.ContainerStyle(typeof(ComboBoxItem)) };
        SiblingNames.SetNamePath(host, "Name");
        SiblingNames.SetNoun(host, "view");
        host.ItemsSource = new[] { Row("Open tasks", null), Row("Open tasks", null), Row("Archive", null) };
        host.SelectedIndex = 1;
        Hosted(host, () =>
        {
            host.UpdateLayout();
            PumpedDispatcher.Drain();
            // Never opened: no item has a container of its own.
            Assert.Null(host.ItemContainerGenerator.ContainerFromIndex(1));
            AutomationPeer peer = UIElementAutomationPeer.CreatePeerForElement(host);
            // The peer UIA hands out for the selection (SelectorAutomationPeer
            // makes it the same way): an item peer with no container behind it.
            System.Reflection.MethodInfo create = typeof(ItemsControlAutomationPeer).GetMethod(
                "CreateItemAutomationPeer",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            string SelectedName() =>
                ((ItemAutomationPeer)create.Invoke(peer, [host.SelectedItem])!).GetName();

            Assert.Equal("Open tasks, view 2", SelectedName());
            host.SelectedIndex = 2;
            PumpedDispatcher.Drain();
            Assert.Equal("Archive", SelectedName());
            host.SelectedIndex = 0;
            PumpedDispatcher.Drain();
            Assert.Equal("Open tasks, view 1", SelectedName());
        });
    });

    /// <summary>A tree scopes per level: each tree item names its own
    /// children among themselves.</summary>
    [Fact]
    public void ATreeNamesEachLevelAmongItself() => RunSta(() =>
    {
        var tree = new TreeView
        {
            ItemContainerStyle = SiblingNames.ContainerStyle(typeof(TreeViewItem)),
            ItemTemplate = new HierarchicalDataTemplate { ItemsSource = new System.Windows.Data.Binding("Children") },
        };
        SiblingNames.SetNamePath(tree, "Name");
        SiblingNames.SetDistinguisherPath(tree, "Path");
        dynamic folder = Row("Notes", "Notes");
        folder.Children = new ObservableCollection<object> { Row("Untitled", "Notes/a.md"), Row("Untitled", "Notes/b.md") };
        tree.ItemsSource = new ObservableCollection<object> { folder, Row("Untitled", "c.md") };
        Hosted(tree, () =>
        {
            Assert.Equal(["Notes", "Untitled"], Names(tree));
            var first = (TreeViewItem)tree.ItemContainerGenerator.ContainerFromIndex(0);
            first.IsExpanded = true;
            first.UpdateLayout();
            PumpedDispatcher.Drain();
            Assert.Equal(["Untitled, Notes/a.md", "Untitled, Notes/b.md"], Names(first));
        });
    });

    /// <summary>A combo's items — their containers live in its drop-down —
    /// read the rule's names, the selected one included (the value NVDA
    /// speaks for the combo; the closed combo's is pinned by the FlaUI
    /// journeys).</summary>
    [Fact]
    public void AComboReadsItsItemsApart() => RunSta(() =>
    {
        var combo = new ComboBox { ItemContainerStyle = SiblingNames.ContainerStyle(typeof(ComboBoxItem)) };
        SiblingNames.SetNamePath(combo, "Name");
        SiblingNames.SetNoun(combo, "view");
        combo.ItemsSource = new[] { Row("Main", null), Row("Main", null) };
        combo.SelectedIndex = 1;
        Hosted(combo, () =>
        {
            combo.IsDropDownOpen = true;
            Assert.Equal(["Main, view 1", "Main, view 2"], Names(combo));
            AutomationPeer peer = UIElementAutomationPeer.CreatePeerForElement(combo);
            ItemAutomationPeer selected = Assert.Single(
                peer.GetChildren().OfType<ItemAutomationPeer>(),
                item => ReferenceEquals(item.Item, combo.SelectedItem));
            Assert.Equal("Main, view 2", selected.GetName());
        });
    });

    private static ExpandoObject Row(string name, string? path)
    {
        var row = new ExpandoObject();
        var fields = (IDictionary<string, object?>)row;
        fields["Name"] = name;
        fields["Path"] = path;
        return row;
    }

    private static string[] Names(ItemsControl host)
    {
        host.UpdateLayout();
        PumpedDispatcher.Drain();
        host.UpdateLayout();
        AutomationPeer peer = UIElementAutomationPeer.CreatePeerForElement(host);
        peer.ResetChildrenCache();
        return [.. (peer.GetChildren() ?? []).OfType<ItemAutomationPeer>().Select(item => item.GetName())];
    }

    private static void Hosted(FrameworkElement content, Action body)
    {
        var window = new Window
        {
            Content = content,
            Width = 480,
            Height = 360,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None,
        };
        window.Show();
        try
        {
            window.UpdateLayout();
            body();
        }
        finally
        {
            window.Close();
        }
    }

    private static void RunSta(Action body)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                body();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(60)), "STA test body timed out.");
        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
