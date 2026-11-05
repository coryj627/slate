// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.IO;

namespace SlateWindows;

/// <summary>
/// DS-10 (#1811): the theme the user chose: remembered on this device and applied at once.
/// One for the app's life, on the lifecycle, because a theme outlives a vault.
/// </summary>
internal sealed class ThemePreference
{
    private readonly AppPreferencesStore _store;
    private readonly Action<string> _apply;
    private string? _current;

    /// <param name="store">Where the choice is remembered.</param>
    /// <param name="apply">What makes it show: <see cref="ThemeManager.SetPreference"/> in
    /// the app, and a recording in a fact, where there's no manager.</param>
    internal ThemePreference(AppPreferencesStore store, Action<string> apply)
    {
        _store = store;
        _apply = apply;
    }

    internal string Current => _current ??= _store.Load().Theme;

    /// <summary>A choice shows even if it couldn't be saved, as every other preference does:
    /// the failure is logged, and the next launch starts from what's on disk.</summary>
    internal void Choose(string value)
    {
        string chosen = ThemeChoice.Normalize(value);
        if (chosen == Current)
        {
            return;
        }

        _current = chosen;
        try
        {
            _store.Save(_store.Load() with { Theme = chosen });
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
}
