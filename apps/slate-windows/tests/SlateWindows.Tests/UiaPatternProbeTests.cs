// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;

namespace SlateWindows.Tests;

/// <summary>
/// WB-15 (#1307 item 3, WD9): a screen reader acts through an element's
/// automation peer — Invoke, Select, Expand, SetValue — and no input route
/// carries that call, so the seal's admission never sees it (contract 40
/// AR-48 names the path). The probe found all four acting under the seal,
/// and stock WPF controls give no hook to refuse them short of a Slate
/// subclass for every control; the owner recorded the exclusion (contract 28
/// PR-6, 2026-10-29). The exclusion is standing by the owner's decision
/// (CD26, 2026-11-16): every pattern and range call, and UI Automation's
/// <c>SetFocus</c>, stay outside the seal, and #1694 ended as won't-do.
/// Each fact pins the recorded behaviour: one pattern call, or one
/// <c>SetFocus</c> (plan 47 CB-38), on the shipped shell while the seal
/// holds acts.
/// </summary>
public sealed class UiaPatternProbeTests
{
    [Fact]
    public void InvokeOnAShellButtonActsUnderTheSealAsRecorded() => RunSta(() =>
    {
        using var host = new CommandPaletteTests.ShippedShellHost(shown: true);
        Button button = host.Shell.CitationDetailsCloseButton;
        int clicks = 0;
        button.AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler((_, _) => clicks++));
        var invoke = Pattern<IInvokeProvider>(button, PatternInterface.Invoke);

        AssertActsUnderTheSealAsRecorded(host, invoke.Invoke, () => $"clicks {clicks}");
    });

    [Fact]
    public void SelectOnAFilesTreeRowActsUnderTheSealAsRecorded() => RunSta(() =>
    {
        using var host = new CommandPaletteTests.ShippedShellHost(shown: true, folder: true);
        FilesSidebarViewModel sidebar = host.AttachFilesSidebar();
        TreeView tree = host.Shell.FilesTree;
        TreeViewItem row = Row(tree, sidebar.RootNodes.Last(node => !node.IsDirectory));
        var select = Pattern<ISelectionItemProvider>(row, PatternInterface.SelectionItem);

        AssertActsUnderTheSealAsRecorded(host, select.Select, () => $"selected {row.IsSelected}");
    });

    [Fact]
    public void ExpandOnAFilesTreeFolderActsUnderTheSealAsRecorded() => RunSta(() =>
    {
        using var host = new CommandPaletteTests.ShippedShellHost(shown: true, folder: true);
        FilesSidebarViewModel sidebar = host.AttachFilesSidebar();
        TreeViewItem folder = Row(host.Shell.FilesTree, sidebar.RootNodes.First(node => node.IsDirectory));
        Assert.False(folder.IsExpanded, "premise: the folder starts collapsed");
        var expand = Pattern<IExpandCollapseProvider>(folder, PatternInterface.ExpandCollapse);

        AssertActsUnderTheSealAsRecorded(host, expand.Expand, () => $"expanded {folder.IsExpanded}");
    });

    [Fact]
    public void SetValueOnThePaletteFieldActsUnderTheSealAsRecorded() => RunSta(() =>
    {
        using var host = new CommandPaletteTests.ShippedShellHost(shown: true);
        TextBox field = host.Shell.CommandPaletteSearchTextBox;
        var value = Pattern<IValueProvider>(field, PatternInterface.Value);
        int calls = 0;

        AssertActsUnderTheSealAsRecorded(host, () => value.SetValue($"x{++calls}"), () => $"text \"{field.Text}\"");
    });

    /// <summary>
    /// Plan 47 CB-38: UI Automation's <c>SetFocus</c> on a Files tree row,
    /// through the item peer a client reaches, passes no input preview, so
    /// under the seal it focuses the row and selects it, as recorded (CD26).
    /// </summary>
    [Fact]
    public void SetFocusOnAFilesTreeRowActsUnderTheSealAsRecorded() => RunSta(() =>
    {
        using var host = new CommandPaletteTests.ShippedShellHost(shown: true, folder: true);
        FilesSidebarViewModel sidebar = host.AttachFilesSidebar();
        TreeView tree = host.Shell.FilesTree;
        TreeViewItem held = Row(tree, sidebar.RootNodes.First());
        object node = sidebar.RootNodes.Last(candidate => !candidate.IsDirectory);
        TreeViewItem row = Row(tree, node);
        Assert.True(held.Focus() && !ReferenceEquals(held, row), "premise: the keys rest on another row first");
        AutomationPeer item = Assert.Single(
            UIElementAutomationPeer.CreatePeerForElement(tree).GetChildren() ?? [],
            child => child is ItemAutomationPeer peer && ReferenceEquals(peer.Item, node));

        AssertActsUnderTheSealAsRecorded(
            host, item.SetFocus, () => $"focused {row.IsKeyboardFocused}, selected {row.IsSelected}", expected: "focused True, selected True");
    });

    /// <summary>
    /// Plan 47 CB-38: UI Automation's <c>SetFocus</c> on a shell button moves
    /// the keys to it under the seal, as recorded (CD26), and clicks
    /// nothing.
    /// </summary>
    [Fact]
    public void SetFocusOnAShellButtonActsUnderTheSealAsRecorded() => RunSta(() =>
    {
        using var host = new CommandPaletteTests.ShippedShellHost(shown: true, folder: true);
        FilesSidebarViewModel sidebar = host.AttachFilesSidebar();
        TreeViewItem held = Row(host.Shell.FilesTree, sidebar.RootNodes.First());
        Button button = ShownShell.Descendants(host.Shell).OfType<Button>().First(candidate =>
            AutomationProperties.GetAutomationId(candidate) == "SidebarCollapseAll");
        Assert.True(button.IsVisible && held.Focus(), "premise: the button shows and the keys rest on a tree row first");
        int clicks = 0;
        button.AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler((_, _) => clicks++), handledEventsToo: true);
        AutomationPeer peer = UIElementAutomationPeer.CreatePeerForElement(button);

        AssertActsUnderTheSealAsRecorded(
            host, peer.SetFocus, () => $"focused {button.IsKeyboardFocused}, clicks {clicks}", expected: "focused True, clicks 0");
    });

    /// <summary>The recorded behaviour: the call acts while the seal holds.
    /// The exclusion is standing by the owner's decision (CD26, contract 28
    /// PR-6), so a guard that refused the call would be a new decision, and
    /// this fact would change with it. <paramref name="expected"/>, when
    /// given, is what the call leaves.</summary>
    private static void AssertActsUnderTheSealAsRecorded(
        CommandPaletteTests.ShippedShellHost host, Action call, Func<string> observe, string? expected = null)
    {
        PumpedDispatcher.Drain();
        string before = observe();
        ComponentDispatcher.PushModal();
        try
        {
            Assert.True(host.Palette.IsSealed, "premise: the modal section sealed the shell");
            call();
            PumpedDispatcher.Drain();
            string after = observe();
            Assert.True(
                after != before && (expected is null || after == expected),
                $"the call no longer acts under the seal as recorded ({before} to {after}): contract 28 PR-6 records "
                + "that it does, and the owner's CD26 keeps it outside the seal");
        }
        finally
        {
            ComponentDispatcher.PopModal();
        }
    }

    private static T Pattern<T>(UIElement element, PatternInterface pattern)
        where T : class
    {
        AutomationPeer peer = UIElementAutomationPeer.CreatePeerForElement(element);
        return Assert.IsAssignableFrom<T>(peer.GetPattern(pattern));
    }

    private static TreeViewItem Row(TreeView tree, object node)
    {
        Assert.True(
            PumpedDispatcher.PumpUntil(() => tree.IsVisible && tree.HasItems, TimeSpan.FromSeconds(10)),
            "premise: the Files tree never showed its rows");
        tree.UpdateLayout();
        return Assert.IsAssignableFrom<TreeViewItem>(tree.ItemContainerGenerator.ContainerFromItem(node));
    }

    private static void RunSta(Action body) =>
        StaThread.Run(body, TimeSpan.FromSeconds(60), "STA test body timed out.");
}
