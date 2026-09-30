// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace SlateWindows.Tests;

/// <summary>
/// #1280 (codex round 2a): the Save command requests the write and returns
/// without pumping — its publication lands on the dispatcher later. A fact
/// that reads a save's outcome saves through here, which pumps until every
/// admitted save has published and every save worker has finished.
/// </summary>
internal static class WorkspaceSaveTestExtensions
{
    internal static void SaveActiveAndSettle(this WorkspaceViewModel workspace)
    {
        workspace.SaveActiveCommand.Execute(null);
        workspace.SettleSavesForTests();
    }

    internal static void SettleSavesForTests(this WorkspaceViewModel workspace) =>
        Assert.True(
            PumpedDispatcher.PumpUntil(
                () => workspace.SavesIdle && workspace.SavesForTests.LiveWorkersForTests == 0,
                TimeSpan.FromSeconds(20)),
            "a save never published");
}
