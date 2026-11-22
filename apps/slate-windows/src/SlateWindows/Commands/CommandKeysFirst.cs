// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace SlateWindows.Commands;

/// <summary>
/// #1866 (CD11), keys first for a command: the one seam <see cref="RelayCommand"/>,
/// <see cref="AsyncRelayCommand"/> and <see cref="ReasonedCommand"/> raise
/// <see cref="ICommand.CanExecuteChanged"/> through.
/// </summary>
/// <remarks>
/// <para>
/// A button bound to a command goes off inside the raise, when it asks the command again.
/// Off with the keys on it, NVDA says "unavailable" of it and the keys fall to whatever WPF
/// finds above. So before the event is raised the seam asks: is the element holding the
/// keys a command source bound to this command, enabled, whose parameter the command now
/// refuses? If so the keys move first (<see cref="RegionFocusGuard.LandBeforeLeaving(UIElement)"/>),
/// to where they go today after the off. If nothing takes them, the button goes off as it
/// did and the guard lands the keys after.
/// </para>
/// <para>
/// It sees a command button whenever it goes off while its window holds the keys, whoever
/// raises: the press itself (a busy flag), a later publication, work settling, another
/// driver. A row of a menu is left alone: a menu row is no site, and the menu owns its own
/// keys.
/// </para>
/// <para>
/// A raise moves the keys at once. A command that refuses and admits again within one turn
/// would move them on the refusal, where WPF, looking later, left them; such a change raises
/// inside <see cref="Hold"/>, as a Files publication and an <see cref="AsyncRelayCommand"/>'s
/// press do, so the button is raised once, for the state the turn ends in.
/// </para>
/// <para>
/// Only an STA thread that has a dispatcher is asked, and both are looked for before any
/// <see cref="Keyboard"/> member is read: reading one gives the asking thread an input
/// manager, and a worker's completion raises too.
/// </para>
/// </remarks>
internal static class CommandKeysFirst
{
    /// <summary>The raises this thread is holding; null outside every
    /// <see cref="Hold"/>.</summary>
    [ThreadStatic]
    private static Held? t_held;

    /// <summary>Raises <paramref name="command"/>'s change through
    /// <paramref name="raise"/>, the keys moved first; inside a <see cref="Hold"/>, at the
    /// scope's end.</summary>
    internal static void Raise(ICommand command, Action<ICommand> raise)
    {
        if (t_held is { } held)
        {
            held.Add(command, raise);
            return;
        }

        if (HoldsKeys(Thread.CurrentThread))
        {
            MoveTheKeysFirst(command);
        }

        raise(command);
    }

    /// <summary>
    /// Opens a scope in which raises on this thread are put off. When the outermost scope
    /// ends, each command raised inside is raised once, through the same keys-first check,
    /// in the order of its first raise. A scope opened inside another joins it. For a
    /// publication that turns a command off and on again, or raises it many times: its
    /// button then never goes off for a state the publication did not end in. Use it with
    /// <c>using</c>: it ends on an exception too, and on the thread that opened it.
    /// </summary>
    internal static IDisposable Hold()
    {
        Held held = t_held ??= new Held();
        held.Depth++;
        return new Scope(held);
    }

    /// <summary>Whether <paramref name="thread"/> can hold the keys at all: an STA thread
    /// with a running dispatcher. A worker has no dispatcher. A thread that is not STA can
    /// have one (a view model built there captured it) and still hosts no window, and WPF
    /// refuses to make it an input manager.</summary>
    private static bool HoldsKeys(Thread thread) =>
        thread.GetApartmentState() == ApartmentState.STA
        && Dispatcher.FromThread(thread) is { HasShutdownStarted: false };

    /// <summary>In a method of its own, so no <see cref="Keyboard"/> member is touched on a
    /// thread that was not asked.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void MoveTheKeysFirst(ICommand command)
    {
        if (Keyboard.FocusedElement is ICommandSource source
            && ReferenceEquals(source.Command, command)
            && source is UIElement { IsEnabled: true } holder
            && !IsAMenuRow(holder)
            && !command.CanExecute(source.CommandParameter))
        {
            _ = RegionFocusGuard.LandBeforeLeaving(holder);
        }
    }

    private static bool IsAMenuRow(UIElement holder)
    {
        if (holder is not MenuItem)
        {
            return false;
        }

        for (DependencyObject? at = holder; at is not null; at = LogicalTreeHelper.GetParent(at) ?? System.Windows.Media.VisualTreeHelper.GetParent(at))
        {
            if (at is Menu or ContextMenu)
            {
                return true;
            }
        }

        return false;
    }

    private static void Deliver(List<(ICommand Command, Action<ICommand> Raise)> raises, int from)
    {
        for (int index = from; index < raises.Count; index++)
        {
            try
            {
                Raise(raises[index].Command, raises[index].Raise);
            }
            catch
            {
                // One listener's failure does not keep the commands after it from raising.
                Deliver(raises, index + 1);
                throw;
            }
        }
    }

    private sealed class Held
    {
        private readonly HashSet<ICommand> _seen = new(ReferenceEqualityComparer.Instance);

        public int Depth { get; set; }

        public List<(ICommand Command, Action<ICommand> Raise)> Raises { get; } = [];

        public void Add(ICommand command, Action<ICommand> raise)
        {
            if (_seen.Add(command))
            {
                Raises.Add((command, raise));
            }
        }
    }

    private sealed class Scope(Held held) : IDisposable
    {
        private bool _ended;

        public void Dispose()
        {
            if (_ended)
            {
                return;
            }

            _ended = true;
            if (--held.Depth > 0)
            {
                return;
            }

            if (ReferenceEquals(t_held, held))
            {
                t_held = null;
            }

            Deliver(held.Raises, 0);
        }
    }
}
