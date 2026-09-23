// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Runtime.ExceptionServices;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Data;
using System.Xml.Linq;
using SlateWindows.Commands;
using SlateWindows.Reading;

namespace SlateWindows.Tests;

public sealed class NavigationHelpTests
{
    [Fact]
    public void QuickOpenHelpNamesEveryModifiedEnterRouteFromItsOwnRow()
    {
        Assert.Equal("Up and Down move selection. Enter opens. "
            + $"{ChordTable.WindowsSpokenFor("windows.quickOpen.openNewTab")} opens a new tab. "
            + $"{ChordTable.WindowsSpokenFor("windows.quickOpen.openSplitRight")} opens a right split. "
            + $"{ChordTable.WindowsSpokenFor("windows.quickOpen.openSplitDown")} opens a down split. "
            + "Escape closes Quick Open.", NavigationHelp.QuickOpen);
    }

    /// <summary>W7-7 (R-2, OD-2): the Files tree's help speaks its three
    /// row gestures from their own rows (N-1), and the shipped tree
    /// carries it.</summary>
    [Fact]
    public void FilesTreeHelpSpeaksItsThreeRowGesturesFromTheirRows()
    {
        Assert.Equal("Up and Down move through files and folders, and a selected note is shown. "
            + $"{ChordTable.WindowsSpokenFor("windows.filesTree.openSelected")} opens it and moves focus into it. "
            + $"{ChordTable.WindowsSpokenFor("windows.filesTree.openSelectedInNewTab")} opens it in a new tab. "
            + $"{ChordTable.WindowsSpokenFor("windows.filesTree.toggleBatchSelection")} checks or unchecks it for batch actions.",
            NavigationHelp.FilesTree);
        XElement tree = Assert.Single(
            XDocument.Load(Path.Combine(SourceText.ShellSourceRoot(), "MainWindow.xaml")).Descendants(),
            element => (string?)element.Attribute("AutomationProperties.AutomationId") == "FilesTree");
        Assert.Equal(
            "{x:Static cmd:NavigationHelp.FilesTree}",
            (string?)tree.Attribute("AutomationProperties.HelpText"));
    }

    [Fact]
    public void ReadingPeerPublishesTheTableDerivedNavigationEntryPoints()
        => OnSta(() =>
        {
            var surface = new ReadingSurface();
            string help = AutomationProperties.GetHelpText(surface);
            Assert.Equal(NavigationHelp.Reading, help);
            foreach (string id in new[] { "nextH", "previousH", "nextK", "nextL", "nextT" })
            {
                Assert.Contains(ChordTable.WindowsSpokenFor("windows.reading." + id)!, help);
            }
        });

    [Fact]
    public void NativeMenuAcceleratorFollowsItsDisplayedChord()
        => OnSta(() =>
        {
            var menu = new MenuItem { InputGestureText = ChordTable.WindowsChordFor("slate.vault.open") };
            var peer = new MenuItemAutomationPeer(menu);
            Assert.Equal("", peer.GetAcceleratorKey());
            menu.SetBinding(AutomationProperties.AcceleratorKeyProperty,
                new Binding(nameof(MenuItem.InputGestureText)) { Source = menu });
            Assert.Equal(menu.InputGestureText, peer.GetAcceleratorKey());
            menu.InputGestureText = ChordTable.WindowsChordFor("slate.workspace.quickOpen");
            Assert.Equal(menu.InputGestureText, peer.GetAcceleratorKey());
        });

    [Fact]
    public void EveryAuthoredMenuAcceleratorBindsItsTableDerivedDisplay()
    {
        var declarations = Directory.EnumerateFiles(SourceText.ShellSourceRoot(), "*.xaml", SearchOption.AllDirectories)
            .Where(path => !Path.GetRelativePath(SourceText.ShellSourceRoot(), path).Split(Path.DirectorySeparatorChar)
                .Any(part => part is "obj" or "bin"))
            .SelectMany(path => XDocument.Load(path).Descendants())
            .Where(element => element.Attribute("InputGestureText") is not null).ToArray();
        Assert.NotEmpty(declarations);
        foreach (XElement element in declarations)
        {
            string display = element.Attribute("InputGestureText")!.Value;
            Assert.StartsWith("{cmd:ChordText ", display);
            Assert.NotNull(ChordTable.WindowsChordFor(display[15..^1]));
            Assert.Equal("{Binding InputGestureText, RelativeSource={RelativeSource Self}}",
                element.Attribute("AutomationProperties.AcceleratorKey")?.Value);
        }
    }

    [Fact]
    public void SplitterHelpUsesTheFourDeliveredArrowRows()
    {
        Assert.Contains(ChordTable.WindowsSpokenFor("windows.splitter.growLeading")!, NavigationHelp.HorizontalSplitter);
        Assert.Contains(ChordTable.WindowsSpokenFor("windows.splitter.growTrailing")!, NavigationHelp.HorizontalSplitter);
        Assert.Contains(ChordTable.WindowsSpokenFor("windows.splitter.growAbove")!, NavigationHelp.VerticalSplitter);
        Assert.Contains(ChordTable.WindowsSpokenFor("windows.splitter.growBelow")!, NavigationHelp.VerticalSplitter);
    }

    [Fact]
    public void ReadingListNavigationPublishesItsCurrentSetAndDistinctName()
        => OnSta(() =>
        {
            var list = new System.Windows.Documents.List();
            var first = new System.Windows.Documents.ListItem(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run("First")));
            var second = new System.Windows.Documents.ListItem(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run("Second")));
            list.ListItems.Add(first);
            list.ListItems.Add(second);
            var listPeer = new ReadingListPeer(list);
            var itemPeer = new ReadingListItemPeer(second);
            Assert.Equal("2 entries", listPeer.GetName());
            Assert.Equal(2, itemPeer.GetPositionInSet());
            Assert.Equal(2, itemPeer.GetSizeOfSet());
            list.ListItems.Remove(first);
            Assert.Equal("1 entry", listPeer.GetName());
            Assert.Equal(1, itemPeer.GetPositionInSet());
            Assert.Equal(1, itemPeer.GetSizeOfSet());
        });

    private static void OnSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception) { failure = exception; }
        });
        // A background thread: a hung action fails this test at Join and
        // must not keep the test host alive until the lane's timeout.
        thread.IsBackground = true;
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)), "The STA editor operation did not finish.");
        if (failure is not null) { ExceptionDispatchInfo.Capture(failure).Throw(); }
    }

    [Theory]
    [InlineData("missing.navigation.command")]
    [InlineData("windows.grid.routedCommands")]
    public void MissingOrChordlessRowsCannotSilentlyEraseHelp(string id) =>
        Assert.Contains(id, Assert.Throws<InvalidOperationException>(() => NavigationHelp.Spoken(id)).Message);
}
