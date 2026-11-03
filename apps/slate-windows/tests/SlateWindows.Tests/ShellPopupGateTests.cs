// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Controls;

namespace SlateWindows.Tests;

/// <summary>#1790: the gate answers for the window as it is now, whatever it did before the
/// owner arrived. A vault can finish opening after the reader has switched applications.</summary>
public sealed class ShellPopupGateTests
{
    private sealed class Hosted : IDisposable
    {
        internal Hosted(bool active)
        {
            Owner = new Border();
            Gate = new ShellPopupGate(Owner, () => Asked++);
            Window = new Window
            {
                Content = Owner,
                Width = 300,
                Height = 200,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
                ShowActivated = false,
            };
            Window.Show();
            if (active)
            {
                WindowActivation.Activate(Window);
            }

            Settle();
        }

        internal Window Window { get; }

        internal Border Owner { get; }

        internal ShellPopupGate Gate { get; }

        internal int Asked { get; private set; }

        internal void Settle()
        {
            Window.UpdateLayout();
            PumpedDispatcher.Drain();
        }

        public void Dispose() => Window.Close();
    }

    [Fact]
    public void AnOwnerInAnActiveWindowIsAllowed() => StaThread.Run(() =>
    {
        using var host = new Hosted(active: true);

        Assert.True(host.Gate.Allows);
    });

    [Fact]
    public void AnOwnerLoadedIntoAnInactiveWindowIsNotAllowedUntilItComesBack() => StaThread.Run(() =>
    {
        using var host = new Hosted(active: false);

        Assert.False(host.Window.IsActive, "premise: the window isn't active");
        Assert.False(host.Gate.Allows);

        int asked = host.Asked;
        WindowActivation.Activate(host.Window);

        Assert.True(host.Gate.Allows);
        Assert.True(host.Asked > asked, "the window coming back didn't ask the owner again.");
    });

    [Fact]
    public void AnOwnerReloadedWhileTheWindowIsInactiveIsNotAllowed() => StaThread.Run(() =>
    {
        using var host = new Hosted(active: true);
        WindowActivation.Deactivate(host.Window);

        host.Window.Content = null;
        host.Settle();
        host.Window.Content = host.Owner;
        host.Settle();

        Assert.False(host.Gate.Allows);
    });

    [Fact]
    public void LeavingTheWindowAsksTheOwnerAgain() => StaThread.Run(() =>
    {
        using var host = new Hosted(active: true);
        int asked = host.Asked;

        WindowActivation.Deactivate(host.Window);

        Assert.False(host.Gate.Allows);
        Assert.True(host.Asked > asked, "leaving the window didn't ask the owner again.");
    });

    /// <summary>A trigger can fire between the owner showing and its Loaded, before the gate
    /// hears the window's events, so arriving asks too.</summary>
    [Fact]
    public void AnOwnerArrivingAsksAgain() => StaThread.Run(() =>
    {
        using var host = new Hosted(active: false);

        Assert.True(host.Asked > 0, "loading into the window didn't ask the owner.");
    });
}
