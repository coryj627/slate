// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows.Input;

namespace SlateWindows.Commands;

/// <summary>
/// A command that can say WHY it cannot run. W7-7 PR 7 (#1252; codex PR 7
/// round 1, finding 11): the palette's one availability resolver (contract
/// P8, <see cref="SlateCommandRegistrar.DisabledReason(ISlateCommandHost, string)"/>)
/// speaks this reason instead of the generic one, so a command the
/// palette lists as unavailable says what to wait for.
/// </summary>
internal interface IUnavailableReason
{
    /// <summary>Why the command cannot run right now, or
    /// <see langword="null"/> when it can.</summary>
    string? UnavailableReason { get; }
}

/// <summary>
/// An <see cref="ICommand"/> whose availability IS its reason: it can run
/// exactly when <see cref="UnavailableReason"/> is <see langword="null"/>,
/// so the enabled state, the palette's row state and the reason it speaks
/// cannot disagree. Its owner raises <see cref="RaiseCanExecuteChanged"/>
/// whenever a blocker changes.
/// </summary>
internal sealed class ReasonedCommand : ICommand, IUnavailableReason
{
    private readonly Action _execute;
    private readonly Func<string?> _unavailableReason;

    public ReasonedCommand(Action execute, Func<string?> unavailableReason)
    {
        _execute = execute;
        _unavailableReason = unavailableReason;
    }

    public event EventHandler? CanExecuteChanged;

    public string? UnavailableReason => _unavailableReason();

    public bool CanExecute(object? parameter) => _unavailableReason() is null;

    /// <summary>Runs only when available: a caller that bypasses
    /// <see cref="CanExecute"/> (a routed command does) cannot run it
    /// against a blocker.</summary>
    public void Execute(object? parameter)
    {
        if (CanExecute(parameter))
        {
            _execute();
        }
    }

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
