// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows.Input;

namespace SlateWindows.Commands;

/// <summary>
/// #1866 (CD11): the one seam every command raises <see cref="ICommand.CanExecuteChanged"/>
/// through. A stub for the facts' first commit: it raises as each command did on its own.
/// </summary>
internal static class CommandKeysFirst
{
    /// <summary>Raises <paramref name="command"/>'s change through
    /// <paramref name="raise"/>.</summary>
    internal static void Raise(ICommand command, Action<ICommand> raise) => raise(command);

    /// <summary>A scope in which raises are held. The stub holds nothing.</summary>
    internal static IDisposable Hold() => new Released();

    private sealed class Released : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
