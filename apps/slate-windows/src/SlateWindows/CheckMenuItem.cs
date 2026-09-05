// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;

namespace SlateWindows;

/// <summary>
/// A checkable menu item whose UIA Toggle IS its click. WPF's
/// <see cref="MenuItemAutomationPeer"/> answers <c>Toggle</c> by flipping
/// <see cref="MenuItem.IsChecked"/> locally and running nothing — so an
/// assistive client that toggles a preference item through the pattern
/// changed the check and never the preference (m1's second half, found
/// 2026-09-03 while fixing the first). Every checkable item in the shell
/// is one of these: the click path and the pattern path now run the same
/// command, and a radio group's re-selection re-asserts its check
/// through the view model (the first half).
/// </summary>
public sealed class CheckMenuItem : MenuItem
{
    protected override AutomationPeer OnCreateAutomationPeer() => new CheckMenuItemAutomationPeer(this);

    /// <summary>The click, for the peer: WPF toggles the local check and
    /// runs the command, exactly as Enter or a mouse click does.</summary>
    internal void ClickFromAutomation() => OnClick();
}

/// <summary>The stock menu-item peer with one pattern re-answered: Toggle
/// clicks. Control type, name, class name and the other patterns are the
/// base's.</summary>
public sealed class CheckMenuItemAutomationPeer(CheckMenuItem owner) : MenuItemAutomationPeer(owner)
{
    public override object? GetPattern(PatternInterface patternInterface) =>
        patternInterface == PatternInterface.Toggle && ((CheckMenuItem)Owner).IsCheckable
            ? new ClickToggleProvider((CheckMenuItem)Owner)
            : base.GetPattern(patternInterface);

    private sealed class ClickToggleProvider(CheckMenuItem owner) : IToggleProvider
    {
        public ToggleState ToggleState => owner.IsChecked ? ToggleState.On : ToggleState.Off;

        /// <summary>The UIA provider contract the stock peer keeps: a
        /// disabled element refuses the pattern rather than running a
        /// command its CanExecute disabled (codoki, PR #1177).</summary>
        public void Toggle()
        {
            if (!owner.IsEnabled)
            {
                throw new ElementNotEnabledException();
            }
            owner.ClickFromAutomation();
        }
    }
}
