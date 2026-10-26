// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace SlateWindows.Tests;

/// <summary>
/// #1331, the fast local signal: nothing this process has run so far wrote
/// the developer's own <c>%LOCALAPPDATA%\Slate</c>.
/// </summary>
/// <remarks>
/// Partial by construction: it sees only what ran before it in this process,
/// and none of the shell gate's launches. The whole-run witness is
/// <c>scripts/windows/guard-device-state.ps1</c>, which snapshots the
/// profile around a whole <c>dotnet test</c> and its child app processes;
/// CI runs both test steps under it. A Slate the developer has open while
/// the suite runs writes the profile too, and this fact then names its
/// files.
/// </remarks>
public sealed class DeviceStateGuardTests
{
    [Fact]
    public void TheRealLocalAppDataIsUntouchedSoFar()
    {
        List<string> changes = DeviceStateForTests.Differences(
            DeviceStateForTests.ProfileAtLoad,
            DeviceStateForTests.Snapshot(DeviceStateForTests.Profile));
        Assert.True(
            changes.Count == 0,
            $"{changes.Count} change(s) in {DeviceStateForTests.Profile} since the test assembly loaded "
            + $"(a Slate left running writes there too): {string.Join("; ", changes.Take(20))}");
    }
}
