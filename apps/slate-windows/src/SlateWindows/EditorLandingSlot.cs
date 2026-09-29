// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;

namespace SlateWindows;

/// <summary>
/// W7-7 PR 8 (#1253, contract R-10; owner decision OD-12): the ONE owner of the
/// editor landing a window holds. A window holds at most one, whoever asked for
/// it — the F6 ring or a route (the focus funnel behind every open, the close
/// fallbacks, the switcher's committed dismissal, a canvas jump) — and whether
/// or not a surface is realized for its tab yet. The arms seat and refuse (the
/// reading surface's token, a canvas or graph document's request); this slot
/// alone CANCELS, silently, and every cancellation comes from one of four
/// places:
/// <list type="bullet">
/// <item>ONE departure observer. It sees every keyboard-focus transition on this
/// thread — class handlers on every input element, handled events included, so a
/// popup's or a context menu's focus is seen, and a transition FROM nowhere (focus
/// null) is seen like one to nowhere. While a landing is held, a transition is the
/// reader (or another route) moving them, and cancels it, unless it is a loss from
/// an element that can no longer hold the keys — no longer shown, disabled or
/// made unfocusable (WPF's recovery and the focus guard's landing in its place,
/// W7-7 PR 4b; a closing overlay's restore); the ONE move that enters the
/// landing's target (the reader stepping in, a graph's provisional seat — a
/// landing that found focus already inside has had its entry); the target's
/// own TERMINAL seat, the move that completes the landing, which the surface
/// declares (<see cref="SeatTerminally"/>); or the window's activation restore
/// (WPF putting focus back when the window comes forward: a landing raised while
/// the window was away was not declined by it).
/// The target is resolved when a transition is read, so a landing whose surface is
/// realized only later still recognises its own entry. The moves a landing makes
/// before it is held (the reading park on the tab item, a synchronous fallback)
/// run before <see cref="Hold"/>; the ones after its arm ended it (a late
/// refusal's fallback) find nothing held.</item>
/// <item>ONE modal hook: a modal surface or a modal loop over the shell (a
/// message box it owns, a common dialog, a WPF <c>ShowDialog</c>) OPENING — the
/// edge, never the level, so a modal's own later changes do not withdraw what
/// its commit raised (<see cref="ModalOpened"/>).</item>
/// <item>The window deactivating (<see cref="WindowDeactivated"/>): a dialog or
/// another application has the keys.</item>
/// <item>The active group, or its active tab, changing away from the landing's
/// own — a surface-less landing has no surface to rebind.</item>
/// </list>
/// A newer landing, the funnel and the ring's repeated press withdraw it
/// through <see cref="Withdraw"/>. What the arm ends itself — seated, refused,
/// or let go of by its surface (hidden, rebound, unloaded) — the slot lets go
/// of on its next read.
/// </summary>
internal sealed class EditorLandingSlot : IDisposable
{
    /// <summary>The slots on this thread (the dispatcher's): one per window.</summary>
    [ThreadStatic]
    private static List<EditorLandingSlot>? t_slots;

    /// <summary>The surface seating its document's terminal landing right
    /// now, on this thread.</summary>
    [ThreadStatic]
    private static DependencyObject? t_seating;

    private readonly Dispatcher _dispatcher;
    private HeldEditorLanding? _held;
    private bool _restoringActivation;
    private int _activations;

    static EditorLandingSlot()
    {
        // Class handlers, not a window's: a popup, a context menu and a
        // rehosted pane each have a root of their own, and a transition from
        // nowhere raises nothing on the window it arrives in until focus is
        // inside it.
        foreach (Type owner in new[] { typeof(UIElement), typeof(ContentElement), typeof(UIElement3D) })
        {
            EventManager.RegisterClassHandler(
                owner, Keyboard.GotKeyboardFocusEvent, new KeyboardFocusChangedEventHandler(OnGotKeyboardFocus), handledEventsToo: true);
            EventManager.RegisterClassHandler(
                owner, Keyboard.LostKeyboardFocusEvent, new KeyboardFocusChangedEventHandler(OnLostKeyboardFocus), handledEventsToo: true);
        }
    }

    /// <summary>A window's slot; created on its dispatcher's thread.</summary>
    internal EditorLandingSlot(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
        (t_slots ??= []).Add(this);
    }

    /// <summary>The landing this window holds, or null. A landing its arm has
    /// ended (seated, refused, or let go of by its surface) is let go of here.</summary>
    internal HeldEditorLanding? Held
    {
        get
        {
            if (_held is { } held && !held.IsLive())
            {
                Release(held);
            }

            return _held;
        }
    }

    /// <summary>Hold <paramref name="landing"/>, the arm's pending request,
    /// once its synchronous part has run. Whatever this slot held before is
    /// withdrawn: one landing per window.</summary>
    internal void Hold(HeldEditorLanding landing)
    {
        ArgumentNullException.ThrowIfNull(landing);
        _ = Withdraw();
        landing.Entered = landing.Target() is { } target
            && IsWithin(Keyboard.FocusedElement as DependencyObject, target);
        _held = landing;
        landing.Attach(this);
    }

    /// <summary>Let go of the held landing, silently: its arm neither seats nor
    /// refuses it later. Answers whether it was still held.</summary>
    internal bool Withdraw()
    {
        if (_held is not { } held)
        {
            return false;
        }

        Release(held);
        return held.Withdraw();
    }

    /// <summary>A modal surface OPENED, or a modal loop began over the shell:
    /// it owns the keys, so nothing may seat beneath it. The edge alone — a modal's later changes while it stays open
    /// leave a landing its own commit raised to the route that follows.</summary>
    internal void ModalOpened() => _ = Withdraw();

    /// <summary>The window lost activation: a dialog or another application has
    /// the keys, and a landing held until the window returns would land on a
    /// reader who went elsewhere.</summary>
    internal void WindowDeactivated() => _ = Withdraw();

    /// <summary>The window came forward: WPF puts focus back where it was, and
    /// that restore — the transitions from nowhere before the input queued
    /// behind it — is not the reader moving.</summary>
    internal void WindowActivated()
    {
        _restoringActivation = true;
        int activation = ++_activations;
        _ = _dispatcher.BeginInvoke(
            DispatcherPriority.Input,
            () =>
            {
                if (activation == _activations)
                {
                    _restoringActivation = false;
                }
            });
    }

    public void Dispose()
    {
        _ = Withdraw();
        _ = t_slots?.Remove(this);
    }

    /// <summary>Run <paramref name="seat"/> as a surface's TERMINAL seat — the
    /// move that completes its document's landing: focus moving inside
    /// <paramref name="surface"/> meanwhile is the landing arriving, not the
    /// reader moving on within it. A provisional seat, which leaves the request
    /// pending, is never declared.</summary>
    internal static T SeatTerminally<T>(DependencyObject surface, Func<T> seat)
    {
        ArgumentNullException.ThrowIfNull(seat);
        DependencyObject? outer = t_seating;
        t_seating = surface;
        try
        {
            return seat();
        }
        finally
        {
            t_seating = outer;
        }
    }

    internal void ScopeChanged(HeldEditorLanding landing)
    {
        if (ReferenceEquals(Held, landing) && !landing.StillWhereAsked())
        {
            _ = Withdraw();
        }
    }

    private void Release(HeldEditorLanding held)
    {
        if (ReferenceEquals(_held, held))
        {
            _held = null;
        }

        held.Detach();
    }

    private void OnTransition(IInputElement? from, IInputElement? to)
    {
        if (Held is not { } held)
        {
            return;
        }

        // Nobody's leaving: WPF's own recovery off an element that can no
        // longer hold the keys — no longer shown, disabled or made unfocusable —
        // and the focus guard's landing that takes its place (W7-7 PR 4b, R-5
        // (h)); and WPF's restore when the window comes forward.
        if (from is null ? _restoringActivation : !CanHoldKeys(from))
        {
            return;
        }

        if (held.Target() is { } target
            && IsWithin(to as DependencyObject, target)
            && (!held.Entered || IsTerminalSeatInto(target)))
        {
            held.Entered = true;
            return;
        }

        _ = Withdraw();
    }

    private static void OnGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        // Raised on every element of the route: the transition once, from the
        // element that took focus.
        if (ReferenceEquals(sender, e.NewFocus))
        {
            Transition(e.OldFocus, e.NewFocus);
        }
    }

    private static void OnLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        // A move to NOWHERE raises no GotKeyboardFocus: read from the element
        // that lost focus. Every other transition is read from its Got side.
        if (e.NewFocus is null && ReferenceEquals(sender, e.OldFocus))
        {
            Transition(e.OldFocus, null);
        }
    }

    private static void Transition(IInputElement? from, IInputElement? to)
    {
        if (t_slots is not { Count: > 0 } slots)
        {
            return;
        }

        foreach (EditorLandingSlot slot in slots.ToArray())
        {
            slot.OnTransition(from, to);
        }
    }

    private static bool IsTerminalSeatInto(DependencyObject target) =>
        t_seating is { } seating && IsWithin(seating, target);

    /// <summary>Whether <paramref name="element"/> can still hold the keys:
    /// shown, enabled and focusable (a content element: itself enabled and
    /// focusable, its host shown and enabled). A move off one that cannot is
    /// WPF's recovery — or the focus guard's landing in its place — never the
    /// reader leaving (W7-7 PR 4b on OD-12: the guard lands keys stranded on a
    /// disabled element as it does on a collapsed one).</summary>
    private static bool CanHoldKeys(IInputElement element) => element switch
    {
        UIElement visual => visual.IsVisible && visual.IsEnabled && visual.Focusable,
        UIElement3D visual3D => visual3D.IsVisible && visual3D.IsEnabled && visual3D.Focusable,
        ContentElement content => content.IsEnabled
            && content.Focusable
            && HostOf(content) is { IsVisible: true, IsEnabled: true },
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

    internal static bool IsWithin(DependencyObject? node, DependencyObject ancestor)
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

/// <summary>
/// W7-7 PR 8 (R-10, OD-12): one editor landing, as the window's
/// <see cref="EditorLandingSlot"/> holds it — how its arm is asked whether it
/// is still pending and is let go of, where it will seat, and where it was
/// asked for.
/// </summary>
internal sealed class HeldEditorLanding
{
    private readonly Func<DependencyObject?> _target;
    private readonly Func<bool> _isLive;
    private readonly Func<bool> _withdraw;
    private readonly Func<bool> _stillWhereAsked;
    private readonly IReadOnlyList<INotifyPropertyChanged> _scope;
    private EditorLandingSlot? _slot;

    /// <param name="target">The element the landing seats — resolved each
    /// time it is read, so a surface realized after the request is found; null
    /// while none is.</param>
    /// <param name="isLive">Whether the arm still holds the request.</param>
    /// <param name="withdraw">Let go of the arm's request; answers whether it
    /// was still held.</param>
    /// <param name="stillWhereAsked">Whether the group and tab it was asked for
    /// are still the active ones.</param>
    /// <param name="scope">What announces a change of the active group or tab
    /// (the workspace and the landing's group).</param>
    /// <param name="ringRegion">The ring position a press asked for, or null
    /// when a route asked.</param>
    internal HeldEditorLanding(
        Func<DependencyObject?> target,
        Func<bool> isLive,
        Func<bool> withdraw,
        Func<bool> stillWhereAsked,
        IReadOnlyList<INotifyPropertyChanged> scope,
        ShellRegionKind? ringRegion)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(isLive);
        ArgumentNullException.ThrowIfNull(withdraw);
        ArgumentNullException.ThrowIfNull(stillWhereAsked);
        ArgumentNullException.ThrowIfNull(scope);
        _target = target;
        _isLive = isLive;
        _withdraw = withdraw;
        _stillWhereAsked = stillWhereAsked;
        _scope = scope;
        RingRegion = ringRegion;
    }

    /// <summary>The ring position the F6 press that asked for it stands on, or
    /// null when a route asked.</summary>
    internal ShellRegionKind? RingRegion { get; }

    /// <summary>Whether its one entry into the target has been made.</summary>
    internal bool Entered { get; set; }

    internal DependencyObject? Target() => _target();

    internal bool IsLive() => _isLive();

    internal bool Withdraw() => _withdraw();

    internal bool StillWhereAsked() => _stillWhereAsked();

    internal void Attach(EditorLandingSlot slot)
    {
        _slot = slot;
        foreach (INotifyPropertyChanged source in _scope)
        {
            source.PropertyChanged += ScopeChanged;
        }
    }

    internal void Detach()
    {
        if (_slot is null)
        {
            return;
        }

        _slot = null;
        foreach (INotifyPropertyChanged source in _scope)
        {
            source.PropertyChanged -= ScopeChanged;
        }
    }

    private void ScopeChanged(object? sender, PropertyChangedEventArgs e) => _slot?.ScopeChanged(this);
}
