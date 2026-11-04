// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Globalization;
using System.Windows.Threading;
using Microsoft.Win32;
using SlateWindows.Canvas;

namespace SlateWindows;

/// <summary>
/// The ONE owner of Windows text scaling (DS-5; §D D11, DD-4): reads the
/// accessibility registry value, subscribes to the system preference change,
/// marshals to the dispatcher it was constructed on, and is DISPOSABLE — the W1-1
/// reactive-theme shape including its unsubscribe half, because WPF binds none of
/// this and a static subscription without a Dispose is the leak obligation ID-9's
/// sibling minor named. The app creates one and alone disposes it. Consumers read
/// <see cref="Factor"/> or ride <see cref="Revision"/>; a view attaches and detaches
/// with its life, and never disposes, or closing one board would stop scaling for
/// the whole app.
/// </summary>
internal sealed class TextScaleService : IDisposable
{
    private const string KeyPath = @"Software\Microsoft\Accessibility";
    private const string ValueName = "TextScaleFactor";

    /// <summary>A launched test's factor, read only when the census's instance is set,
    /// as the capture and census modes are.</summary>
    internal const string TestFactorVariable = "SLATE_TEXT_SCALE_FOR_TESTS";

    private const string CensusInstanceVariable = "SLATE_CENSUS_INSTANCE_ID";

    private readonly Dispatcher _dispatcher;
    private readonly Func<double> _read;
    private readonly bool _observesSystem;
    private bool _disposed;

    /// <summary>The system's service: the registry's value, observed until it's disposed.</summary>
    internal TextScaleService()
        : this(ReadFactor, observesSystem: true)
    {
    }

    /// <summary>A service over an injected read: a fact's, which refreshes it by hand.
    /// It leaves <c>SystemEvents</c> alone: that makes its hidden window on the first
    /// subscriber's thread when that thread is STA, and a fact's thread ends, which
    /// runs managed code on a dead thread and takes the test host down.</summary>
    internal TextScaleService(Func<double> read)
        : this(read, observesSystem: false)
    {
    }

    private TextScaleService(Func<double> read, bool observesSystem)
    {
        _dispatcher = Dispatcher.CurrentDispatcher;
        _read = read;
        _observesSystem = observesSystem;
        Factor = _read();
        if (observesSystem)
        {
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        }
    }

    /// <summary>The app's one service, or none in a hosted fact, where everything draws
    /// at 1. Per thread, because each is one dispatcher's: the app has one UI thread,
    /// and a fact's service on its own thread can't reach another fact's views.</summary>
    [field: ThreadStatic]
    internal static TextScaleService? Current { get; set; }

    /// <summary>The current factor, for a reader that has no service to hold.</summary>
    internal static double CurrentFactor => Current?.Factor ?? 1.0;

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
        if (_observesSystem)
        {
            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        }
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
        double factor = _read();
        if (factor == Factor)
        {
            return;
        }
        Factor = factor;
        Revision++;
        Changed?.Invoke();
    }

    internal static double ReadFactor()
    {
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(CensusInstanceVariable))
            && double.TryParse(Environment.GetEnvironmentVariable(TestFactorVariable), NumberStyles.Float, CultureInfo.InvariantCulture, out double forced))
        {
            return Math.Clamp(forced, 1.0, 2.25);
        }

        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(KeyPath);
            // Clamped BOTH ways (the review round): a policy or a
            // future slider can write past 225, and the documented
            // range is the contract consumers size text by.
            return key?.GetValue(ValueName) is int percent && percent >= 100
                ? Math.Clamp(percent / 100.0, 1.0, 2.25)
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
