// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text;
using SlateWindows;

namespace SlateWindows.Tests;

public sealed class WindowStateStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        $"slate-window-state-test-{Guid.NewGuid():N}");

    public WindowStateStoreTests()
    {
        Directory.CreateDirectory(_directory);
    }

    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public void MissingMalformedInvalidAndOversizedStateIsIgnored()
    {
        WindowStateStore store = CreateStore();
        Assert.Null(store.Load());

        File.WriteAllText(StorePath, "not json");
        Assert.Null(store.Load());

        File.WriteAllText(
            StorePath,
            """
            {"left":0,"top":0,"width":0,"height":700,"maximized":false,
             "monitorDeviceName":"DISPLAY1","monitorLeft":0,"monitorTop":0,
             "monitorWidth":1920,"monitorHeight":1040,"dpi":96}
            """);
        Assert.Null(store.Load());

        File.WriteAllText(
            StorePath,
            """
            {"left":0,"top":0,"width":1120,"height":700,"maximized":false,
             "monitorDeviceName":null,"monitorLeft":0,"monitorTop":0,
             "monitorWidth":1920,"monitorHeight":1040,"dpi":96}
            """);
        Assert.Null(store.Load());

        File.WriteAllBytes(StorePath, new byte[WindowStateStore.MaxFileBytes + 1]);
        Assert.Null(store.Load());
    }

    [Fact]
    public void SaveAndLoadRoundTripsDeviceLocalShape()
    {
        WindowPlacementState expected = CreateState(isMaximized: true);

        WindowStateStore store = CreateStore();
        store.Save(expected);

        Assert.Equal(expected, store.Load());
        string json = File.ReadAllText(StorePath);
        Assert.Contains("\"monitorDeviceName\"", json, StringComparison.Ordinal);
        Assert.Contains("\"maximized\": true", json, StringComparison.Ordinal);
    }

    [Fact]
    public void SaveRejectsUnboundedState()
    {
        WindowPlacementState invalid = CreateState() with { Width = 0 };

        Assert.Throws<ArgumentException>(() => CreateStore().Save(invalid));
    }

    [Fact]
    public void ExistingMonitorRestoresPositionAndMaximizedState()
    {
        WindowPlacementState state = CreateState(isMaximized: true);
        MonitorSnapshot monitor = new(
            @"\\.\DISPLAY1",
            new PixelRect(0, 0, 1920, 1040),
            96,
            true);

        WindowPlacementPlan plan = Assert.IsType<WindowPlacementPlan>(
            WindowPlacementPlanner.Plan(state, [monitor], 760, 480));

        Assert.Equal(new PixelRect(120, 80, 1120, 720), plan.Bounds);
        Assert.True(plan.IsMaximized);
        Assert.Equal(monitor.DeviceName, plan.MonitorDeviceName);
    }

    [Fact]
    public void OffscreenPlacementIsClampedIntoWorkArea()
    {
        WindowPlacementState state = CreateState() with
        {
            Left = 1800,
            Top = 1000,
        };
        MonitorSnapshot monitor = new(
            @"\\.\DISPLAY1",
            new PixelRect(0, 0, 1920, 1040),
            96,
            true);

        WindowPlacementPlan plan = Assert.IsType<WindowPlacementPlan>(
            WindowPlacementPlanner.Plan(state, [monitor], 760, 480));

        Assert.Equal(new PixelRect(800, 320, 1120, 720), plan.Bounds);
    }

    [Fact]
    public void MissingMonitorFallsBackToPrimaryAndPreservesLogicalSize()
    {
        WindowPlacementState state = new(
            2000,
            150,
            1200,
            900,
            false,
            @"\\.\DISPLAY2",
            1920,
            0,
            2560,
            1400,
            144);
        MonitorSnapshot primary = new(
            @"\\.\DISPLAY1",
            new PixelRect(0, 0, 1920, 1040),
            96,
            true);

        WindowPlacementPlan plan = Assert.IsType<WindowPlacementPlan>(
            WindowPlacementPlanner.Plan(state, [primary], 760, 480));

        Assert.Equal(new PixelRect(53, 100, 800, 600), plan.Bounds);
        Assert.Equal(primary.DeviceName, plan.MonitorDeviceName);
    }

    [Fact]
    public void ChangedDeviceNameUsesTheMonitorThatStillOverlaps()
    {
        WindowPlacementState state = CreateState() with { MonitorDeviceName = @"\\.\OLD" };
        MonitorSnapshot primary = new(
            @"\\.\DISPLAY1",
            new PixelRect(-1920, 0, 1920, 1040),
            96,
            true);
        MonitorSnapshot overlapping = new(
            @"\\.\DISPLAY9",
            new PixelRect(0, 0, 1920, 1040),
            96,
            false);

        WindowPlacementPlan plan = Assert.IsType<WindowPlacementPlan>(
            WindowPlacementPlanner.Plan(state, [primary, overlapping], 760, 480));

        Assert.Equal(overlapping.DeviceName, plan.MonitorDeviceName);
        Assert.Equal(new PixelRect(120, 80, 1120, 720), plan.Bounds);
    }

    [Fact]
    public void MinimumSizeIsScaledAndCappedToTheAvailableWorkArea()
    {
        WindowPlacementState state = CreateState() with { Width = 200, Height = 100 };
        MonitorSnapshot compact = new(
            @"\\.\DISPLAY1",
            new PixelRect(0, 0, 1000, 700),
            144,
            true);

        WindowPlacementPlan plan = Assert.IsType<WindowPlacementPlan>(
            WindowPlacementPlanner.Plan(state, [compact], 760, 480));

        Assert.Equal(1000, plan.Bounds.Width);
        Assert.Equal(700, plan.Bounds.Height);
        Assert.Equal(0, plan.Bounds.Left);
        Assert.Equal(0, plan.Bounds.Top);
    }

    [Fact]
    public void ExactlyMaximumFileSizeIsStillRead()
    {
        WindowPlacementState state = CreateState();
        CreateStore().Save(state);
        string json = File.ReadAllText(StorePath);
        int padding = WindowStateStore.MaxFileBytes - Encoding.UTF8.GetByteCount(json);
        string padded = json.Insert(json.Length - 1, new string(' ', padding));
        File.WriteAllBytes(StorePath, Encoding.UTF8.GetBytes(padded));

        Assert.Equal(state, CreateStore().Load());
    }

    private static WindowPlacementState CreateState(bool isMaximized = false) => new(
        120,
        80,
        1120,
        720,
        isMaximized,
        @"\\.\DISPLAY1",
        0,
        0,
        1920,
        1040,
        96);

    private string StorePath => Path.Combine(_directory, "window-state.json");
    private WindowStateStore CreateStore() => new(StorePath);
}
