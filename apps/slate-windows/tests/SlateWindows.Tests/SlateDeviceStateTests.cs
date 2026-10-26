// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Commands;

namespace SlateWindows.Tests;

/// <summary>
/// #1331: one device-state root, <see cref="SlateDeviceState.Root"/>, which
/// honours <c>SLATE_LOCAL_APP_DATA</c>, and every store's default path and
/// the host log under it.
/// </summary>
public sealed class SlateDeviceStateTests
{
    private static string Profile => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Slate");

    [Fact]
    public void ResolveWithoutAnOverrideIsTheProfilesSlateFolder()
    {
        Assert.Equal(Profile, SlateDeviceState.Resolve(null));
        Assert.Equal(Profile, SlateDeviceState.Resolve(string.Empty));
        Assert.Equal(Profile, SlateDeviceState.Resolve("   "));
    }

    [Fact]
    public void ResolveWithAnOverrideIsThatFolderMadeAbsolute()
    {
        string absolute = Path.Combine(Path.GetTempPath(), "slate-device-state-override");
        Assert.Equal(absolute, SlateDeviceState.Resolve(absolute));

        string resolved = SlateDeviceState.Resolve("relative-device-state");
        Assert.True(Path.IsPathFullyQualified(resolved), $"\"{resolved}\" is not absolute.");
        Assert.Equal(Path.GetFullPath("relative-device-state"), resolved);
    }

    /// <summary>The six stores' default paths and the host log (with
    /// <c>SLATE_LOG_DIR</c> unset) start with the root, which in this process
    /// is the module initializer's per-run folder, never the profile.</summary>
    [Fact]
    public void EveryStoreDefaultIsUnderTheRoot()
    {
        using IDisposable gate = EnvFaultLock.Acquire();
        string? logDirectory = Environment.GetEnvironmentVariable("SLATE_LOG_DIR");
        Environment.SetEnvironmentVariable("SLATE_LOG_DIR", null);
        try
        {
            string root = SlateDeviceState.Root;
            Assert.Equal(DeviceStateForTests.Root, root);
            Assert.False(
                string.Equals(Path.TrimEndingDirectorySeparator(root), Profile, StringComparison.OrdinalIgnoreCase),
                $"the test process's device-state root is the developer's profile, {Profile}.");

            string[] defaults =
            [
                RecentVaultsStore.DefaultFilePath,
                WindowStateStore.DefaultFilePath,
                AppPreferencesStore.DefaultFilePath,
                CommandPaletteRecentsStore.DefaultFilePath,
                new FileRecentsStore(Path.GetTempPath()).FilePathForTests,
                SidebarWriteJournal.ProductionDirectory,
                HostLog.LogDirectory,
            ];
            string prefix = Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar;
            Assert.All(defaults, path => Assert.True(
                path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase),
                $"\"{path}\" is not under the device-state root \"{root}\"."));
        }
        finally
        {
            Environment.SetEnvironmentVariable("SLATE_LOG_DIR", logDirectory);
        }
    }
}
