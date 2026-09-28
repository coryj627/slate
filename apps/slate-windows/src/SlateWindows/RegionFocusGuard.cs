// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace SlateWindows;

/// <summary>
/// W7-7 PR 4b (#1247, contract R-5; the owner's S3): one focus-integrity
/// guard for every region — the keys never fall out of a region because
/// the element holding them went away.
/// </summary>
/// <remarks>
/// <para>
/// A focused element that is disabled, collapsed, made unfocusable or taken
/// out of the tree does not keep the keys: WPF re-evaluates them at Input
/// priority and hands them to the nearest focusable element ABOVE — the
/// workspace tab control under a Bases view switched from table to list
/// (the NVDA pass heard "Workspace tabs, tab control"), a standalone scroll
/// viewer above a rebuilt dashboard, or the window itself under a disabled
/// "Load more" (the completeness sweep's G3, G4, G10, G12, G13). A removed
/// list row hands its keys to its bare list itself (G9, G12). Each site
/// used to need a keeper of its own; most had none.
/// </para>
/// <para>
/// <b>Scopes.</b> Every region root registers its landing
/// (<see cref="SetLanding"/>) — the Files pane, the editor, each right-pane
/// leaf, the rail, the status bar, the welcome view, every sheet
/// (<c>RegionGuardCensus</c>) — and a surface inside one may register a
/// finer one (a Bases view, a dashboard). The scopes around each element
/// that takes the keys are recorded as it takes them, innermost first, so
/// they are known even after the element has left the tree.
/// </para>
/// <para>
/// <b>The guard.</b> When the keys are about to move off a STRANDED element —
/// detached, hidden, disabled or unfocusable — onto something that is no
/// stop (nothing, the window, a scroll viewer, a populated container, a
/// tab control, or an ancestor of the scope), the guard lands them itself:
/// the stranded element's own landing when it has one
/// (<see cref="SetStrandedLanding"/>), else the innermost recorded scope that
/// is still on screen — a container of that scope on its row first
/// (<see cref="SelectorFocus.LandOnStop"/>), else the scope's landing. WPF's
/// re-evaluation lands them in the same focus change (the nested landing
/// inside the declined request, which UI Automation sees once); a removed
/// row's own hand-over to its bare list is declined and landed once the
/// rows are laid out (Loaded, ahead of the re-evaluation), the
/// <see cref="SelectorFocus.KeepKeysThroughPublications"/> shape. A scope
/// whose landing takes nothing leaves the keys to WPF. Containers with a
/// landing of their own — a <see cref="LandingTreeView"/>, a list with a
/// publication keeper — keep handling their hand-overs.
/// </para>
/// </remarks>
internal static class RegionFocusGuard
{
    private static readonly ConditionalWeakTable<UIElement, Func<bool>> Landings = new();
    private static readonly ConditionalWeakTable<UIElement, Func<bool>> StrandedLandings = new();
    private static readonly ConditionalWeakTable<IInputElement, UIElement[]> ScopesAtFocus = new();

    /// <summary>The list or tree whose row took the keys, recorded as it
    /// took them: stranded keys come back to the rows they were in.</summary>
    private static readonly ConditionalWeakTable<IInputElement, ItemsControl> RowsAtFocus = new();

    private static bool _registered;

    /// <summary>A landing the guard made is not guarded again.</summary>
    [ThreadStatic]
    private static bool _landing;

    /// <summary>The last focus change asked of the element losing the keys
    /// (a direct request raises PreviewLostKeyboardFocus first; WPF's
    /// re-evaluation does not).</summary>
    [ThreadStatic]
    private static (WeakReference<IInputElement> Old, WeakReference<IInputElement> New)? _directRequest;

    /// <summary>The landing a region root — or a surface inside one — owns:
    /// where the keys go when the element holding them in it goes
    /// away.</summary>
    internal static void SetLanding(UIElement scope, Func<bool> land)
    {
        Register();
        Landings.AddOrUpdate(scope, land);
    }

    /// <summary>Whether <paramref name="scope"/> owns a landing
    /// (<c>RegionGuardCensus</c>).</summary>
    internal static bool HasLanding(UIElement scope) => Landings.TryGetValue(scope, out _);

    /// <summary>Where the keys go when <paramref name="element"/> itself goes
    /// away under them — ahead of its scopes' landings.</summary>
    internal static void SetStrandedLanding(UIElement element, Func<bool> land)
    {
        Register();
        StrandedLandings.AddOrUpdate(element, land);
    }

    /// <summary>The guard's class handlers, process-wide, registered once:
    /// every focus change inside a window routes through its root.</summary>
    internal static void Register()
    {
        if (_registered)
        {
            return;
        }

        _registered = true;
        EventManager.RegisterClassHandler(
            typeof(Window), Keyboard.GotKeyboardFocusEvent, new KeyboardFocusChangedEventHandler(Recorded), handledEventsToo: true);
        EventManager.RegisterClassHandler(
            typeof(Window), Keyboard.PreviewGotKeyboardFocusEvent, new KeyboardFocusChangedEventHandler(Guarded));
        EventManager.RegisterClassHandler(
            typeof(UIElement), Keyboard.PreviewLostKeyboardFocusEvent, new KeyboardFocusChangedEventHandler(Asked), handledEventsToo: true);
    }

    /// <summary>The scopes around <paramref name="element"/>, innermost
    /// first.</summary>
    internal static UIElement[] ScopesOf(DependencyObject element)
    {
        var scopes = new List<UIElement>();
        for (DependencyObject? current = element; current is not null; current = ParentOf(current))
        {
            if (current is UIElement scope && Landings.TryGetValue(scope, out _))
            {
                scopes.Add(scope);
            }
        }

        return [.. scopes];
    }

    private static void Recorded(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (e.NewFocus is DependencyObject focused and IInputElement input && !ReferenceEquals(sender, e.NewFocus))
        {
            ScopesAtFocus.AddOrUpdate(input, ScopesOf(focused));
            if (focused is ListBoxItem or TreeViewItem && ItemsControl.ItemsControlFromItemContainer(focused) is { } rows)
            {
                for (ItemsControl level = rows; level is TreeViewItem row && ItemsControl.ItemsControlFromItemContainer(row) is { } up; level = up)
                {
                    rows = up;
                }

                RowsAtFocus.AddOrUpdate(input, rows);
            }
        }
    }

    private static void Asked(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (ReferenceEquals(sender, e.OriginalSource) && e.OldFocus is { } old && e.NewFocus is { } requested)
        {
            _directRequest = (new WeakReference<IInputElement>(old), new WeakReference<IInputElement>(requested));
        }
    }

    private static void Guarded(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (_landing
            || e.Handled
            || e.OldFocus is not UIElement old
            || !IsStranded(old)
            || e.NewFocus is not UIElement proposed
            || !ScopesAtFocus.TryGetValue(old, out UIElement[]? scopes)
            || IsAStop(proposed, scopes)
            || proposed is LandingTreeView)
        {
            return;
        }

        bool direct = _directRequest is { } request
            && request.Old.TryGetTarget(out IInputElement? asked) && ReferenceEquals(asked, old)
            && request.New.TryGetTarget(out IInputElement? requested) && ReferenceEquals(requested, proposed);
        _directRequest = null;
        if (direct && SelectorFocus.HasPublicationKeeper(proposed))
        {
            // The list's publication keeper declines its removed rows'
            // hand-overs and lands them on the fresh row of the same item.
            return;
        }

        if (direct && proposed is ItemsControl { HasItems: true } container && PresentationSource.FromVisual(old) is null)
        {
            // A removed row's own hand-over to its list, inside the layout
            // that removed it: declined — the keys stay on the removed row —
            // and landed once the rows are laid out, ahead of WPF's
            // re-evaluation at Input.
            e.Handled = true;
            _ = old.Dispatcher.InvokeAsync(
                () =>
                {
                    if (ReferenceEquals(Keyboard.FocusedElement, old) || Keyboard.FocusedElement is null or Window)
                    {
                        _ = Land(old, container, scopes);
                    }
                },
                DispatcherPriority.Loaded);
            return;
        }

        // WPF's re-evaluation (or a request climbing off a stranded element):
        // the landing is made inside it, in the same focus change.
        if (Land(old, proposed, scopes))
        {
            e.Handled = true;
        }
    }

    /// <summary>
    /// W7-7 PR 4b (#1247, R-5; the sweep's G20): a focus RESTORE whose token
    /// can no longer take the keys — its row republished, its results list
    /// collapsed, its cell rebuilt — lands in the scopes the token was in,
    /// innermost first, instead of every restore's old fallback, the editor:
    /// the Files filter's cleared results land in the Files pane, a Bases
    /// cell on the surface's current row.
    /// </summary>
    /// <returns>Whether the keys landed on a live element.</returns>
    internal static bool LandInScopesOf(IInputElement token)
    {
        if (!ScopesAtFocus.TryGetValue(token, out UIElement[]? scopes))
        {
            return false;
        }

        bool outer = _landing;
        _landing = true;
        try
        {
            foreach (UIElement scope in scopes)
            {
                if (IsLive(scope)
                    && Landings.TryGetValue(scope, out Func<bool>? land)
                    && land()
                    && Keyboard.FocusedElement is UIElement now
                    && !ReferenceEquals(now, token)
                    && !IsStranded(now)
                    && now is not Window)
                {
                    return true;
                }
            }

            return false;
        }
        finally
        {
            _landing = outer;
        }
    }

    /// <summary>Lands keys stranded on <paramref name="old"/> the guard's way
    /// — its own landing, else its recorded scopes' — for a keeper that finds
    /// them stranded first (a publication keeper resolves at Loaded, ahead
    /// of WPF's re-evaluation).</summary>
    /// <returns>Whether they are now on a live element.</returns>
    internal static bool LandStranded(UIElement old) =>
        ScopesAtFocus.TryGetValue(old, out UIElement[]? scopes)
            ? Land(old, proposed: null, scopes)
            : StrandedLandings.TryGetValue(old, out Func<bool>? own) && own() && Landed(old);

    /// <summary>Lands the keys stranded on <paramref name="old"/>; true when
    /// they are now on a live element other than it.</summary>
    private static bool Land(UIElement old, UIElement? proposed, UIElement[] scopes)
    {
        bool outer = _landing;
        _landing = true;
        try
        {
            if (StrandedLandings.TryGetValue(old, out Func<bool>? own) && own() && Landed(old))
            {
                return true;
            }

            // A removed row's keys come back to the rows they were in — its
            // list's selected or first row, its tree's landing.
            if (RowsAtFocus.TryGetValue(old, out ItemsControl? rows)
                && IsLive(rows)
                && SelectorFocus.LandOnStop(rows)
                && Landed(old))
            {
                return true;
            }

            foreach (UIElement scope in scopes)
            {
                if (!IsLive(scope) || !Landings.TryGetValue(scope, out Func<bool>? land))
                {
                    continue;
                }

                // A list the keys were handed to: its row. Never a tab control
                // — its "row" is a tab header, out of the content the keys
                // were in.
                if (proposed is ItemsControl container and not TabControl
                    && !ReferenceEquals(container, scope)
                    && scope.IsAncestorOf(container)
                    && container.IsVisible
                    && SelectorFocus.LandOnStop(container)
                    && Landed(old))
                {
                    return true;
                }

                if (land() && Landed(old))
                {
                    return true;
                }
            }

            return false;
        }
        finally
        {
            _landing = outer;
        }
    }

    /// <summary>The keys are on a live element other than the stranded
    /// one.</summary>
    private static bool Landed(UIElement old) =>
        Keyboard.FocusedElement is UIElement now && !ReferenceEquals(now, old) && !IsStranded(now) && now is not Window;

    /// <summary>Whether <paramref name="element"/> can no longer hold the
    /// keys: out of the tree, hidden, disabled or unfocusable.</summary>
    private static bool IsStranded(UIElement element) =>
        PresentationSource.FromVisual(element) is null
        || !element.IsVisible
        || !element.IsEnabled
        || !element.Focusable;

    private static bool IsLive(UIElement scope) =>
        scope.IsVisible && scope.IsEnabled && PresentationSource.FromVisual(scope) is not null;

    /// <summary>Whether <paramref name="proposed"/> is a stop the keys may
    /// rest on: anything but the window, a scroll viewer, a tab control or
    /// a tab's header (WPF's climb out of a tab's content reaches either —
    /// out of the content region, into the tab strip), a populated
    /// container, or an element that holds one of the scopes the keys were
    /// in.</summary>
    private static bool IsAStop(UIElement proposed, UIElement[] scopes) =>
        proposed is not (Window or ScrollViewer or TabControl or TabItem)
        && proposed is not ItemsControl { HasItems: true }
        && !scopes.Any(scope => ReferenceEquals(scope, proposed) || proposed.IsAncestorOf(scope));

    private static DependencyObject? ParentOf(DependencyObject current) =>
        current is Visual or System.Windows.Media.Media3D.Visual3D
            ? VisualTreeHelper.GetParent(current) ?? LogicalTreeHelper.GetParent(current)
            : LogicalTreeHelper.GetParent(current);
}
