// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Reflection;

namespace SlateWindows.Tests;

/// <summary>
/// Plan 47 CB-17b (#1866, CD11): the Files sidebar a keys-first or stays-on witness attaches
/// to its <see cref="ShownShell"/>, so the window it measures on is the app's: attached
/// through the lifecycle's own setter, which the shell observes, as
/// <c>SheetOpenListKeyTests.AttachASidebar</c> does. Dispose it before the shell.
/// </summary>
internal sealed class ShownShellSidebar : IDisposable
{
    private readonly ShownShell _host;

    public ShownShellSidebar(ShownShell host)
    {
        _host = host;
        string root = host.Root;
        Sidebar = new FilesSidebarViewModel(
            host.Session,
            _ => { },
            vaultRoot: root,
            localAppDataRoot: Path.Combine(root, "device-state"));
        PumpedDispatcher.PumpUntilDrained(Sidebar.TreeRefreshCompletion);
        SetFileSidebar(host, Sidebar);
        host.Settle();
    }

    public FilesSidebarViewModel Sidebar { get; }

    public void Dispose()
    {
        SetFileSidebar(_host, null);
        PumpedDispatcher.PumpUntilDrained(Sidebar.BeginSessionShutdownAndCaptureWork().SessionWork);
    }

    private static void SetFileSidebar(ShownShell host, FilesSidebarViewModel? sidebar) =>
        (typeof(VaultLifecycleViewModel).GetProperty(nameof(VaultLifecycleViewModel.FileSidebar), BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException(
                $"VaultLifecycleViewModel.{nameof(VaultLifecycleViewModel.FileSidebar)} is gone; a witness attaches its sidebar through that property's setter."))
        .SetValue(host.Lifecycle, sidebar);
}
