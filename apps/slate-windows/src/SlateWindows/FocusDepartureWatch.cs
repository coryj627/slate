// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace SlateWindows;

/// <summary>
/// W7-7 PR 8 (#1253, contract R-10): whether the reader has left the element
/// a held focus landing's request found them on — a ONE-WAY latch from that
/// original request. The first time the watched element loses keyboard focus
/// while it is still shown, the reader (or another route) moved them: the
/// owner is told at once, and coming back never revives the landing. Three
/// moves are nobody's leaving and are followed instead of latched: a loss
/// from an element that has stopped being shown (hidden, collapsed, taken out
/// of the tree — WPF's own recovery, a closing overlay's restore); the ONE
/// move that enters the landing's own target (the reader stepping in, or a
/// document's provisional seat — a request that finds focus already inside
/// has had its entry); and the document's own terminal seat, the move that
/// completes its landing, which the document declares (<see
/// cref="SeatTerminally"/>). Any other move inside the target after the entry
/// is the reader moving on within it — a departure like any other, so a
/// landing that would later re-seat them is cancelled instead. Nothing is
/// sampled later, on a timer or at a dispatcher priority, so no move queued
/// behind the request can be taken for the place the request found the reader.
/// </summary>
internal sealed class FocusDepartureWatch : IDisposable
{
    /// <summary>The surface seating its document's terminal landing right
    /// now, on this thread (the dispatcher's).</summary>
    [ThreadStatic]
    private static DependencyObject? _seating;

    private readonly DependencyObject _target;
    private readonly Action _departed;
    private DependencyObject? _watched;
    private IInputElement? _origin;
    private bool _entered;
    private bool _stopped;

    /// <param name="target">The element the landing will seat; the one move
    /// into it, and the document's terminal seat inside it, are the
    /// landing's, not a departure.</param>
    /// <param name="departed">Called once, when the reader leaves.</param>
    public FocusDepartureWatch(DependencyObject target, Action departed)
    {
        _target = target;
        _departed = departed;
        _entered = IsWithin(Keyboard.FocusedElement as DependencyObject, target);
        Watch(Keyboard.FocusedElement);
    }

    /// <summary>Run <paramref name="seat"/> as a document's own TERMINAL
    /// seat — the move that completes its landing: focus moving inside
    /// <paramref name="surface"/> meanwhile is the landing arriving, not the
    /// reader moving on within it, so a held landing's watch follows it. A
    /// provisional seat, which leaves the request pending, is never declared.</summary>
    internal static bool SeatTerminally(DependencyObject surface, Func<bool> seat)
    {
        DependencyObject? outer = _seating;
        _seating = surface;
        try
        {
            return seat();
        }
        finally
        {
            _seating = outer;
        }
    }

    public void Dispose()
    {
        _stopped = true;
        Unwatch();
    }

    private void Watch(IInputElement? origin)
    {
        Unwatch();
        _origin = origin;
        if (origin is DependencyObject element)
        {
            _watched = element;
            Keyboard.AddLostKeyboardFocusHandler(element, OriginLostFocus);
        }
    }

    private void Unwatch()
    {
        if (_watched is { } element)
        {
            Keyboard.RemoveLostKeyboardFocusHandler(element, OriginLostFocus);
            _watched = null;
        }
    }

    private void OriginLostFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (_stopped || !ReferenceEquals(e.OldFocus, _origin))
        {
            return;
        }

        bool intoTarget = IsWithin(e.NewFocus as DependencyObject, _target);
        if (!IsShown(_origin) || (intoTarget && (!_entered || IsTerminalSeatInto(_target))))
        {
            _entered |= intoTarget;
            Watch(e.NewFocus);
            return;
        }

        _stopped = true;
        Unwatch();
        _departed();
    }

    private static bool IsTerminalSeatInto(DependencyObject target) =>
        _seating is { } seating && IsWithin(seating, target);

    private static bool IsShown(IInputElement? element) => element switch
    {
        UIElement visual => visual.IsVisible,
        UIElement3D visual3D => visual3D.IsVisible,
        ContentElement content => HostOf(content) is { IsVisible: true },
        _ => false,
    };

    private static UIElement? HostOf(ContentElement content)
    {
        for (DependencyObject? node = content; node is not null; node = ParentOf(node))
        {
            if (node is UIElement host)
            {
                return host;
            }
        }

        return null;
    }

    private static bool IsWithin(DependencyObject? node, DependencyObject ancestor)
    {
        for (; node is not null; node = ParentOf(node))
        {
            if (ReferenceEquals(node, ancestor))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The visual parent where there is one; a content element
    /// (a reading-view hyperlink) climbs through its logical parent —
    /// VisualTreeHelper throws for it.</summary>
    private static DependencyObject? ParentOf(DependencyObject node) => node is Visual or Visual3D
        ? VisualTreeHelper.GetParent(node) ?? LogicalTreeHelper.GetParent(node)
        : LogicalTreeHelper.GetParent(node);
}
