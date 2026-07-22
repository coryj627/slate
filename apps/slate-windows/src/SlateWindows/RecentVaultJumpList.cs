// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Shell;

namespace SlateWindows;

internal static class RecentVaultJumpList
{
    public static void Apply(IEnumerable<RecentVault> recentVaults)
    {
        if (Application.Current is null || string.IsNullOrEmpty(Environment.ProcessPath))
        {
            return;
        }

        try
        {
            var jumpList = new JumpList
            {
                ShowFrequentCategory = false,
                ShowRecentCategory = false,
            };
            foreach (RecentVault recent in recentVaults)
            {
                jumpList.JumpItems.Add(new JumpTask
                {
                    Title = recent.DisplayName,
                    Description = recent.Path,
                    CustomCategory = "Recent Vaults",
                    ApplicationPath = Environment.ProcessPath,
                    Arguments = ActivationArguments.QuoteForWindowsCommandLine(recent.Path),
                    IconResourcePath = Environment.ProcessPath,
                });
            }

            JumpList.SetJumpList(Application.Current, jumpList);
            jumpList.Apply();
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or UnauthorizedAccessException)
        {
            // Jump Lists may be disabled by group policy. Recents remain fully
            // available in the welcome screen and must not block startup.
            Console.Error.WriteLine($"Could not update recent-vault Jump List: {exception.Message}");
        }
    }
}
