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
/// original request. The first time that element loses keyboard focus while
/// it is still shown, the reader (or another route) moved them: the owner is
/// told at once, and coming back never revives the landing. Two moves are
/// nobody's choice and are followed instead of latched: a loss from an
/// element that has stopped being shown (hidden, collapsed, taken out of the
/// tree — WPF's own recovery, a closing overlay's restore), and a move into
/// the landing's own target. Nothing is sampled later, on a timer or at a
/// dispatcher priority, so no move queued behind the request can be taken
/// for the place the request found the reader.
/// </summary>
internal sealed class FocusDepartureWatch : IDisposable
{
    private readonly DependencyObject _target;
    private readonly Action _departed;
    private DependencyObject? _watched;
    private IInputElement? _origin;
    private bool _stopped;

    /// <param name="target">The element the landing will seat; focus
    /// arriving inside it is the landing's, not a departure.</param>
    /// <param name="departed">Called once, when the reader leaves.</param>
    public FocusDepartureWatch(DependencyObject target, Action departed)
    {
        _target = target;
        _departed = departed;
        Watch(Keyboard.FocusedElement);
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

        if (IsWithin(e.NewFocus as DependencyObject, _target) || !IsShown(_origin))
        {
            Watch(e.NewFocus);
            return;
        }

        _stopped = true;
        Unwatch();
        _departed();
    }

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
