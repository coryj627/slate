// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.ComponentModel;
using System.Windows;

namespace SlateWindows;

/// <summary>
/// DS-4: whether the shell's chrome animates: the system's animation preference, shaped
/// like <see cref="Graph.GraphMotionPolicy"/>. A menu's slide reads it through a binding,
/// so a flip of the preference reaches the next menu that opens. It's public because
/// XAML reaches it with <c>x:Static</c>, and one instance because that reads it once,
/// when a dictionary loads.
/// </summary>
public sealed class ShellMotion : INotifyPropertyChanged
{
    private Func<bool> _read = static () => SystemParameters.ClientAreaAnimation;

    private ShellMotion()
    {
        SystemParameters.StaticPropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SystemParameters.ClientAreaAnimation))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Animates)));
            }
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public static ShellMotion Current { get; } = new();

    public bool Animates => _read();

    /// <summary>A fact's read in place of the system's; the previous one comes back to
    /// restore.</summary>
    internal Func<bool> ReadForTests(Func<bool> read)
    {
        Func<bool> previous = _read;
        _read = read;
        return previous;
    }
}
