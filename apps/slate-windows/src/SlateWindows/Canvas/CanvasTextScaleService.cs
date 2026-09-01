// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows.Threading;
using Microsoft.Win32;

namespace SlateWindows.Canvas;

/// <summary>
/// The ONE owner of Windows text scaling for the canvas (§D D11,
/// DD-4): reads the accessibility registry value, subscribes to the
/// system preference change, marshals to the dispatcher it was
/// constructed on, and is DISPOSABLE — the W1-1 reactive-theme shape
/// including its unsubscribe half, because WPF binds none of this and
/// a static subscription without a Dispose is the leak obligation
/// ID-9's sibling minor named. Consumers read
/// <see cref="Factor"/> or ride <see cref="Revision"/> through the
/// presentation engine's revision commit; a bumped revision is a new
/// installed state.
/// </summary>
internal sealed class CanvasTextScaleService : IDisposable
{
    private const string KeyPath = @"Software\Microsoft\Accessibility";
    private const string ValueName = "TextScaleFactor";
    private readonly Dispatcher _dispatcher;
    private bool _disposed;

    internal CanvasTextScaleService()
    {
        _dispatcher = Dispatcher.CurrentDispatcher;
        Factor = ReadFactor();
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    /// <summary>1.0 through 2.25 — the registry percent over 100, or
    /// 1.0 when the key is absent or unreadable (the documented
    /// fallback: a machine that never touched the slider has no
    /// key).</summary>
    internal double Factor { get; private set; }

    /// <summary>Bumped on every observed change — the value the
    /// presentation engine's text-scale commit consumes.</summary>
    internal int Revision { get; private set; }

    /// <summary>Raised ON THE DISPATCHER after Factor and Revision
    /// move together.</summary>
    internal event Action? Changed;

    /// <summary>The refresh body, callable by facts: re-read, and if
    /// the factor moved, bump and notify. Production reaches it only
    /// through the marshalled preference handler. Test seam by
    /// name.</summary>
    internal void RefreshForTests() => Refresh();

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
    }

    private void OnUserPreferenceChanged(
        object sender, UserPreferenceChangedEventArgs eventArgs)
    {
        // The system raises this on ITS thread; every consumer of the
        // factor is dispatcher-side, so the marshal happens here —
        // once, at the owner — rather than in each consumer (DD-4).
        if (_dispatcher.CheckAccess())
        {
            Refresh();
            return;
        }
        _ = _dispatcher.BeginInvoke(Refresh);
    }

    private void Refresh()
    {
        if (_disposed)
        {
            return;
        }
        double factor = ReadFactor();
        if (factor == Factor)
        {
            return;
        }
        Factor = factor;
        Revision++;
        Changed?.Invoke();
    }

    private static double ReadFactor()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(KeyPath);
            return key?.GetValue(ValueName) is int percent && percent >= 100
                ? percent / 100.0
                : 1.0;
        }
        catch (Exception exception) when (CanvasFaults.Survivable(exception))
        {
            // An unreadable key is a 1.0 machine, not a crash: the
            // factor is a rendering preference, and the honest
            // fallback is the unscaled default.
            return 1.0;
        }
    }
}
