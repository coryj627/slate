// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SlateWindows;

internal sealed record WindowPlacementState(
    [property: JsonPropertyName("left")] int Left,
    [property: JsonPropertyName("top")] int Top,
    [property: JsonPropertyName("width")] int Width,
    [property: JsonPropertyName("height")] int Height,
    [property: JsonPropertyName("maximized")] bool IsMaximized,
    [property: JsonPropertyName("monitorDeviceName")] string MonitorDeviceName,
    [property: JsonPropertyName("monitorLeft")] int MonitorLeft,
    [property: JsonPropertyName("monitorTop")] int MonitorTop,
    [property: JsonPropertyName("monitorWidth")] int MonitorWidth,
    [property: JsonPropertyName("monitorHeight")] int MonitorHeight,
    [property: JsonPropertyName("dpi")] uint Dpi)
{
    private const int MaximumCoordinateMagnitude = 1_000_000;
    private const int MaximumDimension = 100_000;

    public bool IsValid =>
        Math.Abs((long)Left) <= MaximumCoordinateMagnitude
        && Math.Abs((long)Top) <= MaximumCoordinateMagnitude
        && Width is > 0 and <= MaximumDimension
        && Height is > 0 and <= MaximumDimension
        && Math.Abs((long)MonitorLeft) <= MaximumCoordinateMagnitude
        && Math.Abs((long)MonitorTop) <= MaximumCoordinateMagnitude
        && MonitorWidth is > 0 and <= MaximumDimension
        && MonitorHeight is > 0 and <= MaximumDimension
        && Dpi is >= 48 and <= 960
        && MonitorDeviceName is not null
        && MonitorDeviceName.Length <= 128;
}

internal readonly record struct PixelRect(int Left, int Top, int Width, int Height)
{
    public int Right => Left + Width;
    public int Bottom => Top + Height;

    public long IntersectionArea(PixelRect other)
    {
        long width = Math.Max(0L, Math.Min((long)Right, other.Right) - Math.Max((long)Left, other.Left));
        long height = Math.Max(0L, Math.Min((long)Bottom, other.Bottom) - Math.Max((long)Top, other.Top));
        return width * height;
    }
}

internal sealed record MonitorSnapshot(
    string DeviceName,
    PixelRect WorkArea,
    uint Dpi,
    bool IsPrimary)
{
    public bool IsValid =>
        WorkArea.Width > 0
        && WorkArea.Height > 0
        && Dpi is >= 48 and <= 960;
}

internal sealed record WindowPlacementPlan(
    PixelRect Bounds,
    bool IsMaximized,
    string MonitorDeviceName);

internal static class WindowPlacementPlanner
{
    private const double DefaultDpi = 96.0;

    public static WindowPlacementPlan? Plan(
        WindowPlacementState? state,
        IEnumerable<MonitorSnapshot> availableMonitors,
        double minimumWidthDip,
        double minimumHeightDip)
    {
        if (state is null
            || !state.IsValid
            || !double.IsFinite(minimumWidthDip)
            || !double.IsFinite(minimumHeightDip)
            || minimumWidthDip < 0
            || minimumHeightDip < 0)
        {
            return null;
        }

        MonitorSnapshot[] monitors = availableMonitors.Where(monitor => monitor.IsValid).ToArray();
        if (monitors.Length == 0)
        {
            return null;
        }

        PixelRect savedBounds = new(state.Left, state.Top, state.Width, state.Height);
        MonitorSnapshot? target = monitors.FirstOrDefault(monitor =>
            !string.IsNullOrEmpty(state.MonitorDeviceName)
            && StringComparer.OrdinalIgnoreCase.Equals(monitor.DeviceName, state.MonitorDeviceName));

        if (target is null)
        {
            MonitorSnapshot overlapTarget = monitors
                .OrderByDescending(monitor => monitor.WorkArea.IntersectionArea(savedBounds))
                .First();
            target = overlapTarget.WorkArea.IntersectionArea(savedBounds) > 0
                ? overlapTarget
                : monitors.FirstOrDefault(monitor => monitor.IsPrimary) ?? monitors[0];
        }

        double scale = target.Dpi / (double)state.Dpi;
        int minimumWidth = Math.Min(
            target.WorkArea.Width,
            Math.Max(1, RoundToInt(minimumWidthDip * target.Dpi / DefaultDpi)));
        int minimumHeight = Math.Min(
            target.WorkArea.Height,
            Math.Max(1, RoundToInt(minimumHeightDip * target.Dpi / DefaultDpi)));
        int width = Math.Clamp(RoundToInt(state.Width * scale), minimumWidth, target.WorkArea.Width);
        int height = Math.Clamp(RoundToInt(state.Height * scale), minimumHeight, target.WorkArea.Height);

        int left = target.WorkArea.Left + RoundToInt((state.Left - (double)state.MonitorLeft) * scale);
        int top = target.WorkArea.Top + RoundToInt((state.Top - (double)state.MonitorTop) * scale);
        left = Math.Clamp(left, target.WorkArea.Left, target.WorkArea.Right - width);
        top = Math.Clamp(top, target.WorkArea.Top, target.WorkArea.Bottom - height);

        return new WindowPlacementPlan(
            new PixelRect(left, top, width, height),
            state.IsMaximized,
            target.DeviceName);
    }

    private static int RoundToInt(double value) =>
        checked((int)Math.Round(value, MidpointRounding.AwayFromZero));
}

/// <summary>Bounded device-local storage for the main window's normal placement.</summary>
internal sealed class WindowStateStore
{
    public const int MaxFileBytes = 1 << 14;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private readonly string _filePath;

    public WindowStateStore(string? filePath = null)
    {
        _filePath = filePath ?? DefaultFilePath;
    }

    public static string DefaultFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Slate",
        "window-state.json");

    public WindowPlacementState? Load()
    {
        try
        {
            byte[] buffer = SafeFile.ReadAllBytesBounded(
                _filePath,
                MaxFileBytes,
                FileShare.ReadWrite | FileShare.Delete);

            WindowPlacementState? state = JsonSerializer.Deserialize<WindowPlacementState>(
                buffer,
                JsonOptions);
            return state?.IsValid == true ? state : null;
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public void Save(WindowPlacementState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!state.IsValid)
        {
            throw new ArgumentException("Window placement is outside the supported bounds.", nameof(state));
        }

        string? directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        byte[] json = JsonSerializer.SerializeToUtf8Bytes(state, JsonOptions);
        string temporaryPath = $"{_filePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllBytes(temporaryPath, json);
            File.Move(temporaryPath, _filePath, overwrite: true);
        }
        finally
        {
            SafeFile.TryDelete(temporaryPath);
        }
    }
}
