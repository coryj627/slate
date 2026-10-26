// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.IO;

namespace SlateWindows;

/// <summary>
/// The one root of Slate's device-local state (#1331): recent vaults, window
/// placement, preferences, palette recents, per-vault file recents, the
/// sidebar write journal and the logs. <see cref="Variable"/>, when set,
/// points it elsewhere — the test hosts and the field-pass launcher set it,
/// so neither writes the developer's own profile — the way
/// <c>SLATE_LOG_DIR</c> already moves the logs alone
/// (<see cref="HostLog.LogDirectory"/>). Unset, it is
/// <c>%LOCALAPPDATA%\Slate</c>, as it always was.
/// </summary>
internal static class SlateDeviceState
{
    internal const string Variable = "SLATE_LOCAL_APP_DATA";

    /// <summary>Read at each use (a store keeps the path it was built
    /// with).</summary>
    internal static string Root => Resolve(Environment.GetEnvironmentVariable(Variable));

    /// <summary>An override that holds anything but white space, made
    /// absolute; else the profile's <c>Slate</c> folder.</summary>
    internal static string Resolve(string? overrideValue) =>
        string.IsNullOrWhiteSpace(overrideValue)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Slate")
            : Path.GetFullPath(overrideValue);
}
