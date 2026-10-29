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
/// PR-6, 2026-10-29) and the guard is #1694. Each fact pins the recorded
/// behaviour: one pattern call on the shipped shell while the seal holds
/// acts.
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

    /// <summary>The recorded behaviour: the call acts while the seal holds.
    /// When #1694 guards the controls this fails, and the fact flips to
    /// asserting the refusal (and the register row closes).</summary>
    private static void AssertActsUnderTheSealAsRecorded(
        CommandPaletteTests.ShippedShellHost host, Action call, Func<string> observe)
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
                after != before,
                $"the pattern call no longer acts under the seal ({before}) — contract 28 PR-6 records that it does; "
                + "if #1694 guarded it, assert the refusal here and close the row");
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
