// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
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
    private static readonly ConditionalWeakTable<UIElement, Func<bool>> GoneLandings = new();
    private static readonly ConditionalWeakTable<IInputElement, UIElement[]> ScopesAtFocus = new();

    /// <summary>The list or tree whose row took the keys, recorded as it
    /// took them: stranded keys come back to the rows they were in.</summary>
    private static readonly ConditionalWeakTable<IInputElement, ItemsControl> RowsAtFocus = new();

    private static bool _registered;

    /// <summary>A landing the guard made is not guarded again.</summary>
    [ThreadStatic]
    private static bool _landing;

    /// <summary>The element whose keys a running landing is placing — the
    /// stranded holder, or a restore's dead token — for a landing that lands
    /// by what went away (the Properties header's rebuilt lists,
    /// <see cref="PropertiesLanding"/>); null outside a landing.</summary>
    [ThreadStatic]
    private static IInputElement? _holder;

    internal static IInputElement? Holder => _holder;

    /// <summary>Whether the keys are on a live element other than the
    /// <see cref="Holder"/> — never the window. A landing tells from it that
    /// it placed them: an element that went away still answers its own
    /// <c>Focus()</c> true while it holds them, and a pending landing may have
    /// moved them nowhere yet.</summary>
    internal static bool HolderLanded => Landed(_holder);

    /// <summary>What each element that took the keys was showing as it took
    /// them. An items control hands the elements of a container it removes
    /// the disconnected-item sentinel for a data context, and a landing by
    /// what went away needs the item.</summary>
    private static readonly ConditionalWeakTable<IInputElement, object> ContextsAtFocus = new();

    /// <summary>The <see cref="Holder"/>'s data context — as it is, else, its
    /// container removed, as it was when it took the keys.</summary>
    internal static object? HolderContext =>
        _holder switch
        {
            null => null,
            FrameworkElement { DataContext: { } live } when !ReferenceEquals(live, BindingOperations.DisconnectedSource) => live,
            _ => ContextsAtFocus.TryGetValue(_holder, out object? recorded) ? recorded : null,
        };

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

    /// <summary>
    /// Where the keys go when <paramref name="scope"/> ITSELF goes away under
    /// them, every scope inside it with it: the view that replaced it (W7-7 PR
    /// 4b round 2 — the welcome view and the workspace, swapped by the vault's
    /// open and close). A scope whose going has no replacement (a sheet: its
    /// own dismissal restores the keys) registers none.
    /// </summary>
    internal static void SetGoneLanding(UIElement scope, Func<bool> land)
    {
        Register();
        GoneLandings.AddOrUpdate(scope, land);
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
            if (current is UIElement scope && (Landings.TryGetValue(scope, out _) || GoneLandings.TryGetValue(scope, out _)))
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
            if (((focused as FrameworkElement)?.DataContext ?? (focused as FrameworkContentElement)?.DataContext) is { } context)
            {
                ContextsAtFocus.AddOrUpdate(input, context);
            }
            else
            {
                ContextsAtFocus.Remove(input);
            }

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
        // The holder may be a content element — a reading view's Hyperlink
        // takes the keys by Tab — as well as a visual (PR 4b codex round 2,
        // F1: Ctrl+W on a pane's last tab, the keys on a link).
        if (_landing
            || e.Handled
            || e.OldFocus is not IInputElement old
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

        if (direct
            && proposed is ItemsControl { HasItems: true } container
            && old is UIElement row
            && PresentationSource.FromVisual(row) is null)
        {
            // A removed row's own hand-over to its list, inside the layout
            // that removed it: declined — the keys stay on the removed row —
            // and landed once the rows are laid out, ahead of WPF's
            // re-evaluation at Input.
            e.Handled = true;
            _ = row.Dispatcher.InvokeAsync(
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
    /// cell on the surface's current row. A dead ROW's token comes back to
    /// the rows it was in first — its list's selected (re-seated) or first
    /// row, R-5 (h) (codex PR 4b r1 F3: a Queries row the builder's save
    /// rebuilt) — while that list still has rows; an emptied one gives way
    /// to the scopes, whose landing prefers a populated list (S5).
    /// </summary>
    /// <returns>Whether the keys landed on a live element.</returns>
    internal static bool LandInScopesOf(IInputElement token)
    {
        bool outer = _landing;
        IInputElement? outerHolder = _holder;
        _landing = true;
        _holder = token;
        try
        {
            if (token is UIElement row
                && RowsAtFocus.TryGetValue(row, out ItemsControl? rows)
                && IsLive(rows)
                && rows.HasItems
                && SelectorFocus.LandOnStop(rows)
                && Landed(row))
            {
                return true;
            }

            if (!ScopesAtFocus.TryGetValue(token, out UIElement[]? scopes))
            {
                return false;
            }

            foreach (UIElement scope in scopes)
            {
                Func<bool>? land = IsLive(scope)
                    ? Landings.TryGetValue(scope, out Func<bool>? own) ? own : null
                    : GoneLandings.TryGetValue(scope, out Func<bool>? replaced) ? replaced : null;
                if (land is not null && land() && Landed(token))
                {
                    return true;
                }
            }

            return false;
        }
        finally
        {
            _landing = outer;
            _holder = outerHolder;
        }
    }

    /// <summary>Lands keys stranded on <paramref name="old"/> the guard's way
    /// — its own landing, else its recorded scopes' — for a keeper that finds
    /// them stranded first (a publication keeper resolves at Loaded, ahead
    /// of WPF's re-evaluation).</summary>
    /// <returns>Whether they are now on a live element.</returns>
    internal static bool LandStranded(IInputElement old) =>
        ScopesAtFocus.TryGetValue(old, out UIElement[]? scopes)
            ? Land(old, proposed: null, scopes)
            : old is UIElement element && StrandedLandings.TryGetValue(element, out Func<bool>? own) && own() && Landed(old);

    /// <summary>Lands the keys stranded on <paramref name="old"/>; true when
    /// they are now on a live element other than it.</summary>
    private static bool Land(IInputElement old, UIElement? proposed, UIElement[] scopes)
    {
        bool outer = _landing;
        IInputElement? outerHolder = _holder;
        _landing = true;
        _holder = old;
        try
        {
            if (old is UIElement element
                && StrandedLandings.TryGetValue(element, out Func<bool>? own)
                && own()
                && Landed(old))
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
                if (!IsLive(scope))
                {
                    // A scope gone with everything in it: the view that
                    // replaced it, if it names one.
                    if (GoneLandings.TryGetValue(scope, out Func<bool>? replaced) && replaced() && Landed(old))
                    {
                        return true;
                    }

                    continue;
                }

                if (!Landings.TryGetValue(scope, out Func<bool>? land))
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
            _holder = outerHolder;
        }
    }

    /// <summary>The keys are on a live element other than the stranded
    /// one.</summary>
    private static bool Landed(IInputElement? old) =>
        Keyboard.FocusedElement is { } now && !ReferenceEquals(now, old) && !IsStranded(now) && now is not Window;

    /// <summary>Whether <paramref name="element"/> can no longer hold the
    /// keys: a visual out of the tree, hidden, disabled or unfocusable; a
    /// content element (a Hyperlink) disabled or unfocusable, or whose host —
    /// its nearest visual — is out of the tree, hidden or disabled.</summary>
    /// <remarks>WPF re-evaluates a content element's keys when it is
    /// disabled, made unfocusable or taken out of the tree with its host;
    /// a host merely COLLAPSED under them raises nothing on the element, so
    /// no guard hears it (contract 40, PR 4b's record).</remarks>
    private static bool IsStranded(IInputElement element) => element switch
    {
        UIElement visual => PresentationSource.FromVisual(visual) is null
            || !visual.IsVisible
            || !visual.IsEnabled
            || !visual.Focusable,
        ContentElement content => !content.IsEnabled
            || !content.Focusable
            || HostOf(content) is not { } host
            || PresentationSource.FromVisual(host) is null
            || !host.IsVisible
            || !host.IsEnabled,
        _ => false,
    };

    /// <summary>The nearest visual above <paramref name="content"/>.</summary>
    private static UIElement? HostOf(ContentElement content)
    {
        for (DependencyObject? current = ParentOf(content); current is not null; current = ParentOf(current))
        {
            if (current is UIElement host)
            {
                return host;
            }
        }

        return null;
    }

    private static bool IsLive(UIElement scope) =>
        scope.IsVisible && scope.IsEnabled && PresentationSource.FromVisual(scope) is not null;

    /// <summary>Whether <paramref name="proposed"/> is a stop the keys may
    /// rest on: anything but the window, a scroll viewer, a tab control
    /// (WPF's climb out of a tab's content reaches it — out of the content
    /// region, toward the tab strip), a bare populated container, or an
    /// element that holds one of the scopes the keys were in.</summary>
    private static bool IsAStop(UIElement proposed, UIElement[] scopes) =>
        proposed is not (Window or ScrollViewer or TabControl)
        && !IsBareContainer(proposed)
        && !scopes.Any(scope => ReferenceEquals(scope, proposed) || proposed.IsAncestorOf(scope));

    /// <summary>
    /// A populated list, tree or grid ITSELF — never one of its rows. A tree's
    /// row is an items control with rows of its own, and a stop: #1318's shell
    /// gate caught the guard reading the window's landing in the Connections
    /// leaf — a direct request to the tree's group row, made after the
    /// re-root's open took the graph table's cell holding the keys out of the
    /// tree — as that cell's hand-over to its bare list, declining it, so the
    /// boundary fell back to the rail.
    /// </summary>
    private static bool IsBareContainer(UIElement element) =>
        element is ItemsControl { HasItems: true } && ItemsControl.ItemsControlFromItemContainer(element) is null;

    private static DependencyObject? ParentOf(DependencyObject current) =>
        current is Visual or System.Windows.Media.Media3D.Visual3D
            ? VisualTreeHelper.GetParent(current) ?? LogicalTreeHelper.GetParent(current)
            : LogicalTreeHelper.GetParent(current);
}
