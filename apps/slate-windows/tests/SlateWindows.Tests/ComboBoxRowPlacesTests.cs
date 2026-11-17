// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
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
        Assert.True(combo.IsDropDownOpen, $"{label}: the list is not open.");
        Popup popup = Assert.IsType<Popup>(combo.Template?.FindName("PART_Popup", combo));
        Assert.True(popup.IsOpen, $"{label}: the list says it is open and its popup is not.");
        UIElement? root = popup.Child is { } child && PresentationSource.FromVisual(child) is HwndSource { RootVisual: UIElement visual }
            ? visual
            : null;
        Assert.True(root is not null, $"{label}: the open list has no window of its own.");

        var rows = new List<AutomationPeer>();
        CollectListItems(UIElementAutomationPeer.CreatePeerForElement(root!), rows);
        int realized = Enumerable.Range(0, combo.Items.Count)
            .Count(index => combo.ItemContainerGenerator.ContainerFromIndex(index) is not null);
        string seen = string.Join(", ", rows.Select(row => $"'{row.GetName()}' {row.GetPositionInSet()} of {row.GetSizeOfSet()}"));
        Assert.True(
            rows.Count >= 1 && rows.Count == realized && realized == combo.Items.Count,
            $"{label}: {rows.Count} ListItem peers under the open list's window, for {realized} realized rows of {combo.Items.Count}: {seen}");

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

        Assert.True(
            wrong.Count == 0,
            $"{label}: rows under the open list's window that do not say their place as WPF's item peer does:\n  "
            + string.Join("\n  ", wrong));
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
