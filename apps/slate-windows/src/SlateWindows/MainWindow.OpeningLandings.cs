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
    /// <para>
    /// <paramref name="landsLater"/> says the surface's own observer lands it later in the
    /// same call (the template picker, opened loading and filled before its opening call
    /// returns): it is not held meanwhile, so the keys go from what held them straight to
    /// its stop. A hold of an instant is a focus event NVDA reads only after the keys have
    /// moved on, and ignores, so it keeps the holder as its focus and speaks the holder
    /// going off (the NVDA spot on PR 2's head, A1).
    /// </para>
    /// </summary>
    private void LandInOpeningSurface(
        ModalSurface which,
        FrameworkElement surface,
        UIElement? stop,
        Func<bool> land,
        Func<bool> stillOpen,
        Func<bool>? lateLand = null,
        Func<bool>? landsLater = null)
    {
        Func<bool> late = lateLand ?? land;
        _openingLandings[which] = new OpeningLanding(
            surface, stillOpen, () => LandOpeningNow(which, surface, stop, land, late, stillOpen, landsLater));
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
    /// one if it opened beneath the surface that has just closed. When a surface closes over
    /// one that had the keys before it with the keys still in it, the one left on top lands
    /// them at once, in the close's own notification: a closer with no restore (the canvas
    /// sheets) leaves them to WPF, which hands them to the window's element beneath it, and
    /// every key already queued would reach that element first (the merge judge's minor 1).
    /// Any other close leaves the one on top checked once the closing surface's own restore
    /// and WPF's hand-up have run (red team on the diff, F1).</summary>
    private void LandAnOpeningThatWaited()
    {
        List<KeyValuePair<ModalSurface, OpeningLanding>> closed = [.. _openingLandings.Where(entry => !entry.Value.StillOpen())];
        foreach (KeyValuePair<ModalSurface, OpeningLanding> entry in closed)
        {
            _ = _openingLandings.Remove(entry.Key);
        }

        if (OpenModalSurface is not { } top || !_openingLandings.TryGetValue(top, out OpeningLanding? opening))
        {
            return;
        }

        if (opening.Awaiting)
        {
            TryLandOpening(top);
        }
        else if (closed.Any(entry => entry.Value.Surface.IsKeyboardFocusWithin))
        {
            _ = LandTheTopmostOpeningAgain();
        }
        else if (closed.Count > 0)
        {
            _ = Dispatcher.InvokeAsync(() => LandTheTopmostOpeningAgain(), DispatcherPriority.Background);
        }
    }

    /// <summary>Red team F4: the topmost surface whose stops do not hold the keys takes them
    /// when the window comes back or a box over it closes: one presented while the window
    /// was away, or one that held the keys itself as it went. The element its scope held
    /// last lands first, when it held the keys and is still a stop, so the reader keeps their
    /// place (red team on the diff, F4); else its opening landing, which also takes a dead
    /// element's place (the merge judge's nit: its scopes' landing would be the sheet's first
    /// focusable element, Settings' Close). Says whether the keys are in the surface now;
    /// false when nothing was to be done.</summary>
    private bool LandTheTopmostOpeningAgain()
    {
        if (!IsActive
            || OpenModalSurface is not { } top
            || !_openingLandings.TryGetValue(top, out OpeningLanding? opening)
            || !opening.StillOpen()
            || KeysAreOnAStopIn(opening.Surface))
        {
            return false;
        }

        opening.Awaiting = false;
        if (FocusManager.GetFocusedElement(opening.Surface) is DependencyObject last and IInputElement token
            && !ReferenceEquals(last, opening.Surface)
            && IsWithin(last, opening.Surface)
            && RegionFocusGuard.HeldTheKeys(token)
            && TryFocus(token)
            && KeysAreOnAStopIn(opening.Surface))
        {
            return true;
        }

        opening.LandNow();
        return opening.Surface.IsKeyboardFocusWithin;
    }

    /// <summary>Plan 47 CB-17b PR 2 (red team on the diff, F1): a closing sheet's restore
    /// never lands beneath the topmost surface still open. A token inside that surface lands
    /// as it is (a citation sheet's return into the details sheet under it); one outside it
    /// gives way to the surface's own landing, which runs only if its stops do not hold the
    /// keys. Says whether the restore is to stand down.</summary>
    private bool RestoreGivesWayToTheTopmost(IInputElement? token)
    {
        if (OpenModalSurface is not { } top
            || !_openingLandings.TryGetValue(top, out OpeningLanding? opening)
            || !opening.StillOpen())
        {
            return false;
        }

        if (token is DependencyObject target && IsWithin(target, opening.Surface))
        {
            return false;
        }

        _ = LandTheTopmostOpeningAgain();
        return true;
    }

    private void LandOpeningNow(
        ModalSurface which,
        FrameworkElement surface,
        UIElement? stop,
        Func<bool> land,
        Func<bool> lateLand,
        Func<bool> stillOpen,
        Func<bool>? landsLater)
    {
        BringUpToDate(surface, stop);
        if (!surface.IsKeyboardFocusWithin)
        {
            // A hold its keys left without passing through it, or one it closed in, ends.
            SetHoldsTheKeys(surface, false);
            FocusManager.SetFocusedElement(surface, null);
        }

        surface.UpdateLayout();
        if (land() && KeysAreOnAStopIn(surface))
        {
            return;
        }

        if (!surface.IsKeyboardFocusWithin
            && (IsKeyboardFocusWithin || MainMenu.IsKeyboardFocusWithin)
            && landsLater?.Invoke() != true)
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
    /// inside it, because it closed, or because the window went away: the window's return
    /// lands the topmost surface again (<see cref="LandTheTopmostOpeningAgain"/>), so a hold
    /// is never kept for it, and never outlives its keys (red team on the diff, F6).</summary>
    private void SurfaceLetsTheKeysGo(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is FrameworkElement surface)
        {
            SetHoldsTheKeys(surface, false);
        }
    }
}
