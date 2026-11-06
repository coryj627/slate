// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.IO;

namespace SlateWindows;

/// <summary>
/// A setting the user chose: remembered on this device and applied at once. One for the app's
/// life, on the lifecycle, because how Slate looks outlives a vault.
/// </summary>
internal abstract class DevicePreference
{
    private readonly AppPreferencesStore _store;
    private readonly Action<string> _apply;
    private string? _current;

    /// <param name="store">Where the choice is remembered.</param>
    /// <param name="apply">What makes it show: the <see cref="ThemeManager"/> in the app, and
    /// a recording in a fact, where there's no manager.</param>
    protected DevicePreference(AppPreferencesStore store, Action<string> apply)
    {
        _store = store;
        _apply = apply;
    }

    internal string Current => _current ??= Read(_store.Load());

    /// <summary>A choice shows even if it couldn't be saved, as every other preference does:
    /// the failure is logged, and the next launch starts from what's on disk.</summary>
    internal void Choose(string value)
    {
        string chosen = Normalize(value);
        if (chosen == Current)
        {
            return;
        }

        _current = chosen;
        try
        {
            _store.Save(Written(_store.Load(), chosen));
        }
        catch (IOException exception)
        {
            HostLog.Write(HostDiagnosticEvent.AppPreferencesPersistFailed, exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            HostLog.Write(HostDiagnosticEvent.AppPreferencesPersistFailed, exception);
        }

        _apply(chosen);
    }

    protected abstract string Normalize(string value);

    protected abstract string Read(AppPreferencesState state);

    protected abstract AppPreferencesState Written(AppPreferencesState state, string value);
}

/// <summary>DS-10 (#1811): the theme the user chose.</summary>
internal sealed class ThemePreference(AppPreferencesStore store, Action<string> apply)
    : DevicePreference(store, apply)
{
    protected override string Normalize(string value) => ThemeChoice.Normalize(value);

    protected override string Read(AppPreferencesState state) => state.Theme;

    protected override AppPreferencesState Written(AppPreferencesState state, string value) =>
        state with { Theme = value };
}

/// <summary>DS-11 (#1812): the accent the user chose.</summary>
internal sealed class AccentPreference(AppPreferencesStore store, Action<string> apply)
    : DevicePreference(store, apply)
{
    protected override string Normalize(string value) => SlateAccent.Normalize(value);

    protected override string Read(AppPreferencesState state) => state.AccentColor;

    protected override AppPreferencesState Written(AppPreferencesState state, string value) =>
        state with { AccentColor = value };
}
