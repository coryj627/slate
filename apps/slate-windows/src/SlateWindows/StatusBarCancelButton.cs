// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace SlateWindows;

/// <summary>
/// Plan 47 CB-16 (#1830): a status bar Cancel, the trash's or the import's. When its work
/// ends, its command goes off and its item hides in the same turn. WPF raises the off for the
/// button's peer only afterwards, at Background priority, when the button is already hidden.
/// UI Automation then hands a hidden element's event to its nearest shown ancestor: the status
/// bar itself, where the keys have just gone back (the owner's "Off, keys go back"). Narrator
/// said "Status bar disabled." there, and NVDA's log showed the status bar going off.
/// While hidden, this button reports the enabled state it last showed, so a hide raises
/// nothing. Shown again, it reports its own. The style stays the stock button's.
/// </summary>
public sealed class StatusBarCancelButton : Button
{
    public StatusBarCancelButton() => SetResourceReference(StyleProperty, typeof(Button));

    protected override AutomationPeer OnCreateAutomationPeer() => new StatusBarCancelButtonAutomationPeer(this);
}

/// <summary>The stock button peer with one answer kept while its button is hidden: whether
/// it is enabled. Everything else is the base's.</summary>
public sealed class StatusBarCancelButtonAutomationPeer(StatusBarCancelButton owner) : ButtonAutomationPeer(owner)
{
    private bool _shownEnabled = owner.IsEnabled;

    protected override bool IsEnabledCore()
    {
        if (Owner.IsVisible)
        {
            _shownEnabled = base.IsEnabledCore();
        }

        return _shownEnabled;
    }
}
