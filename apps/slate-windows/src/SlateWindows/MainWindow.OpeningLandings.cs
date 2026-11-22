// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace SlateWindows;

/// <summary>
/// Plan 47 CB-17b (#1866, CD11; #1690 for the overlays): every modal surface, the three
/// overlays and the fourteen sheets, takes the keys in the turn it opens, so nothing under it
/// that holds them goes off before they have left it, and NVDA never hears it go
/// unavailable. One routine for all seventeen.
/// </summary>
public partial class MainWindow
{
    /// <summary>The opening landing of each surface that is open: what it lands on, how,
    /// and whether it is still open. A surface that opened beneath another waits here until
    /// it is the topmost (<see cref="ModalSource_PropertyChanged"/>); the activation step
    /// lands the topmost one again when it does not hold the keys.</summary>
    private readonly Dictionary<ModalSurface, OpeningLanding> _openingLandings = [];

    /// <summary>One surface's opening landing. <see cref="Awaiting"/> while it is open
    /// beneath another surface.</summary>
    private sealed record OpeningLanding(FrameworkElement Surface, Func<bool> StillOpen, Action LandNow)
    {
        public bool Awaiting { get; set; }
    }

    /// <summary>
    /// The keys go to an opening surface in the turn it opens. <paramref name="land"/> gives
    /// a stop inside it the keys and says whether one has them; <paramref name="lateLand"/>,
    /// the landing queued as the fallback, may take a stop the same-turn landing does not
    /// (Settings' Close, the template picker's Cancel); it defaults to <paramref name="land"/>.
    /// <para>
    /// A surface that opens beneath another (a sheet a continuation presents while Settings
    /// is up, search opened under the palette) leaves the keys where the surface above put
    /// them, and lands once it is the topmost (red team F3). Before it lands, the surface
    /// forgets the element its scope last held: asked to take the keys itself, a focus scope
    /// hands them on to that element, which can be a Cancel or an Apply from an earlier
    /// opening (red team F1). Its bindings are brought up to date and it is laid out first,
    /// since the window can hear the opening before they have run.
    /// </para>
    /// <para>
    /// When no stop can take the keys in that turn, the surface itself holds them, named and
    /// a dialog, whatever held them in this window outside it (the workspace, the welcome
    /// view, the menu, the status bar, a dismissed overlay's field; red team F2): nothing
    /// under the surface gets a key, text or a click meanwhile, and Escape reaches its route.
    /// Only if the surface cannot take them either are they cleared. The fallback runs only
    /// while the surface is open and the topmost and the keys are not on a stop inside it:
    /// a second landing would select what the reader has typed since.
    /// </para>
    /// </summary>
    private void LandInOpeningSurface(
        ModalSurface which,
        FrameworkElement surface,
        UIElement? stop,
        Func<bool> land,
        Func<bool> stillOpen,
        Func<bool>? lateLand = null)
    {
        Func<bool> late = lateLand ?? land;
        _openingLandings[which] = new OpeningLanding(surface, stillOpen, () => LandOpeningNow(which, surface, stop, land, late, stillOpen));
        TryLandOpening(which);
    }

    /// <summary>Lands <paramref name="which"/>'s opening when it is the topmost open
    /// surface, else marks it as waiting; forgets it once it has closed.</summary>
    private void TryLandOpening(ModalSurface which)
    {
        if (!_openingLandings.TryGetValue(which, out OpeningLanding? opening))
        {
            return;
        }

        if (!opening.StillOpen())
        {
            _ = _openingLandings.Remove(which);
            return;
        }

        if (OpenModalSurface != which)
        {
            opening.Awaiting = true;
            return;
        }

        opening.Awaiting = false;
        opening.LandNow();
    }

    /// <summary>Forgets the openings of surfaces that have closed, and lands the topmost
    /// one if it opened beneath the surface that has just closed.</summary>
    private void LandAnOpeningThatWaited()
    {
        foreach (ModalSurface closed in _openingLandings.Where(entry => !entry.Value.StillOpen()).Select(entry => entry.Key).ToList())
        {
            _ = _openingLandings.Remove(closed);
        }

        if (OpenModalSurface is { } top && _openingLandings.TryGetValue(top, out OpeningLanding? opening) && opening.Awaiting)
        {
            TryLandOpening(top);
        }
    }

    /// <summary>Red team F4: the topmost surface that does not hold the keys takes them by
    /// its opening landing when the window comes back or a box over it closes: one presented
    /// while the window was away, or one that held the keys itself as it went.</summary>
    private bool LandTheTopmostOpeningAgain()
    {
        if (OpenModalSurface is not { } top
            || !_openingLandings.TryGetValue(top, out OpeningLanding? opening)
            || !opening.StillOpen()
            || opening.Surface.IsKeyboardFocusWithin)
        {
            return false;
        }

        opening.Awaiting = false;
        opening.LandNow();
        return true;
    }

    private void LandOpeningNow(
        ModalSurface which,
        FrameworkElement surface,
        UIElement? stop,
        Func<bool> land,
        Func<bool> lateLand,
        Func<bool> stillOpen)
    {
        BringUpToDate(surface, stop);
        if (!surface.IsKeyboardFocusWithin)
        {
            FocusManager.SetFocusedElement(surface, null);
        }

        surface.UpdateLayout();
        if (land() && KeysAreOnAStopIn(surface))
        {
            return;
        }

        if (!surface.IsKeyboardFocusWithin && (IsKeyboardFocusWithin || MainMenu.IsKeyboardFocusWithin))
        {
            // A stop only while it holds the keys: it goes back once they leave, for a stop
            // inside it or because it closed.
            SetHoldsTheKeys(surface, true);
            if (!surface.IsVisible || !SelectorFocus.LandOnStop(surface))
            {
                // Neither a stop nor the surface can take the keys: a surface that isn't on
                // screen. Nothing holds them, so nothing under it gets a key, text or a
                // click; Escape is lost too, until the queued landing below. It's the least
                // bad state: the alternative is a focused, enabled view taking input under an
                // open surface.
                SetHoldsTheKeys(surface, false);
                Keyboard.ClearFocus();
            }
        }

        _ = Dispatcher.InvokeAsync(
            () =>
            {
                if (stillOpen() && OpenModalSurface == which && !KeysAreOnAStopIn(surface))
                {
                    _ = lateLand();
                }
            },
            DispatcherPriority.Input);
    }

    /// <summary>Whether the keys are on an element inside <paramref name="surface"/> other
    /// than the surface itself.</summary>
    private static bool KeysAreOnAStopIn(FrameworkElement surface) =>
        surface.IsKeyboardFocusWithin && !ReferenceEquals(Keyboard.FocusedElement, surface);

    /// <summary>The bindings an opening surface reads before its landing, brought up to
    /// date: every DataContext and Visibility inside it, top down, then each Visibility from
    /// its stop up to the window, then every binding on the stop itself, since what the stop
    /// says as it takes the keys is what a reader hears first and its name and help can
    /// change in notifications after the opening's (the palette's choices).</summary>
    private static void BringUpToDate(FrameworkElement surface, UIElement? stop)
    {
        foreach (DependencyProperty property in new[] { DataContextProperty, VisibilityProperty })
        {
            var pending = new Stack<DependencyObject>();
            pending.Push(surface);
            while (pending.Count > 0)
            {
                DependencyObject at = pending.Pop();
                BindingOperations.GetBindingExpressionBase(at, property)?.UpdateTarget();
                foreach (object child in LogicalTreeHelper.GetChildren(at))
                {
                    if (child is DependencyObject inner)
                    {
                        pending.Push(inner);
                    }
                }
            }
        }

        if (stop is null)
        {
            return;
        }

        for (DependencyObject? up = stop; up is not null; up = (up is Visual ? VisualTreeHelper.GetParent(up) : null) ?? LogicalTreeHelper.GetParent(up))
        {
            BindingOperations.GetBindingExpressionBase(up, VisibilityProperty)?.UpdateTarget();
        }

        LocalValueEnumerator values = stop.GetLocalValueEnumerator();
        while (values.MoveNext())
        {
            BindingOperations.GetBindingExpressionBase(stop, values.Current.Property)?.UpdateTarget();
        }
    }

    /// <summary>Makes <paramref name="overlay"/>, a sheet or an overlay, a stop that can hold
    /// the keys while its landing waits, and no stop again once it has let them go. Never a
    /// Tab stop.</summary>
    private void SetHoldsTheKeys(FrameworkElement overlay, bool holds)
    {
        overlay.LostKeyboardFocus -= SurfaceLetsTheKeysGo;
        if (holds)
        {
            KeyboardNavigation.SetIsTabStop(overlay, false);
            overlay.LostKeyboardFocus += SurfaceLetsTheKeysGo;
        }

        overlay.Focusable = holds;
    }

    /// <summary>A surface holding the keys lets them go once they leave it, for a stop
    /// inside it or because it closed. A loss to nothing while it is on screen (the window
    /// went away) keeps it a stop, so the window's return can give them back to it (red team
    /// F4).</summary>
    private void SurfaceLetsTheKeysGo(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is FrameworkElement surface && !(e.NewFocus is null && surface.IsVisible))
        {
            SetHoldsTheKeys(surface, false);
        }
    }
}
