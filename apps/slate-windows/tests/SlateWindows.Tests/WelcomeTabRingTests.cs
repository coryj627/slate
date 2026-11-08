// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;

namespace SlateWindows.Tests;

/// <summary>
/// #1677, #1678: after a vault opens and closes, the welcome view's recent vaults are each a
/// button a reader reaches: each one's own element is in the window's tree under the
/// "Recent vaults" group, and Tab cycles Open Vault and every recent vault, each holding the
/// keys as an element of that tree, never the bare window.
/// </summary>
public sealed class WelcomeTabRingTests
{
    [Fact]
    public void AfterAVaultOpensAndClosesEveryRecentVaultIsAnElement() => RunSta(() =>
    {
        using var beta = FixtureVault.Create(1, "ring-beta");
        using var gamma = FixtureVault.Create(1, "ring-gamma");
        using var shell = new WelcomeShell(gamma.Root, beta.Root, WelcomeShell.TheFixtureVault);
        shell.ReleaseParks();
        AssertTheRing(shell, "at launch");

        OpenAndCloseTheFixtureVault(shell);

        Assert.Equal(3, shell.Lifecycle.RecentVaults.Count);
        AssertTheRing(shell, "after an open and a close");
    });

    private static void OpenAndCloseTheFixtureVault(WelcomeShell shell)
    {
        RecentVault fixture = shell.Lifecycle.RecentVaults.Single(
            recent => string.Equals(recent.Path, shell.VaultRoot, StringComparison.OrdinalIgnoreCase));
        shell.Lifecycle.OpenRecentCommand.Execute(fixture);
        shell.WaitForTheOpenToEnd();
        Assert.True(shell.Lifecycle.IsVaultOpen, "premise: the fixture vault didn't open");
        shell.Lifecycle.CloseVault();
        shell.Settle();
    }

    /// <summary>What a UIA client sees, walking down from the window: every recent button's
    /// own element is in the tree, the group lists exactly those, and Tab from Open Vault
    /// visits each recent button and comes back, each stop an element of the tree.</summary>
    private static void AssertTheRing(WelcomeShell shell, string when)
    {
        Button[] recents = RecentButtons(shell);
        Assert.Equal(3, recents.Length);
        HashSet<AutomationPeer> tree = Walk(UIElementAutomationPeer.CreatePeerForElement(shell.Shell));
        AutomationPeer group = UIElementAutomationPeer.CreatePeerForElement(RecentHost(shell));
        HashSet<AutomationPeer> listed = Walk(group);
        Assert.All(recents, button =>
        {
            AutomationPeer peer = PeerOf(button);
            Assert.True(tree.Contains(peer), $"{when}: {Name(button)}'s element isn't in the window's tree.");
            Assert.True(listed.Contains(peer), $"{when}: {Name(button)} isn't listed under Recent vaults.");
        });
        Assert.Equal(3, listed.OfType<ButtonAutomationPeer>().Count(peer => peer.IsControlElement()));

        Button open = Assert.Single(ShownShell.Descendants(shell.Shell).OfType<Button>(), button => button.Name == "OpenVaultButton");
        Assert.True(open.Focus(), $"{when}: premise: Open Vault refused the keys");
        var visited = new List<IInputElement>();
        for (int press = 0; press < recents.Length + 1; press++)
        {
            Assert.True(((UIElement)Keyboard.FocusedElement).MoveFocus(new TraversalRequest(FocusNavigationDirection.Next)));
            shell.Settle();
            visited.Add(Keyboard.FocusedElement);
            var focused = Assert.IsAssignableFrom<UIElement>(Keyboard.FocusedElement);
            Assert.True(tree.Contains(PeerOf(focused)), $"{when}: Tab {press + 1} holds the keys on a {focused.GetType().Name} with no element in the tree.");
        }

        Assert.Equal([.. recents, open], visited);
    }

    private static AutomationPeer PeerOf(UIElement element) =>
        UIElementAutomationPeer.FromElement(element) ?? UIElementAutomationPeer.CreatePeerForElement(element);

    private static string Name(Button button) => ((RecentVault)button.DataContext).DisplayName;

    private static LayoutItemsControl RecentHost(WelcomeShell shell) =>
        Assert.Single(
            ShownShell.Descendants(shell.Shell).OfType<LayoutItemsControl>(),
            host => AutomationProperties.GetName(host) == "Recent vaults");

    private static Button[] RecentButtons(WelcomeShell shell) =>
        [.. ShownShell.Descendants(RecentHost(shell)).OfType<Button>().Where(button => button.IsVisible)];

    private static HashSet<AutomationPeer> Walk(AutomationPeer root)
    {
        var seen = new HashSet<AutomationPeer>(ReferenceEqualityComparer.Instance);
        var pending = new Stack<AutomationPeer>([root]);
        while (pending.Count > 0)
        {
            AutomationPeer peer = pending.Pop();
            if (!seen.Add(peer))
            {
                continue;
            }

            foreach (AutomationPeer child in peer.GetChildren() ?? [])
            {
                pending.Push(child);
            }
        }

        return seen;
    }

    private static void RunSta(Action body) =>
        StaThread.RunPumped(body, TimeSpan.FromSeconds(90), "STA test body timed out.");
}
