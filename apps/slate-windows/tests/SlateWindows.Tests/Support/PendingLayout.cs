// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace SlateWindows.Tests;

/// <summary>
/// Plan 47 CB-35 (#1837): what WPF still has to lay out, read two ways.
/// </summary>
/// <remarks>
/// <para>
/// The global answer is the dispatcher's own layout queue. TSF's <c>GetTextExt</c> begins
/// with <c>UiScope.UpdateLayout()</c>, and <c>ContextLayoutManager.UpdateLayout</c> loops
/// while anything in the dispatcher is dirty or post-layout events are queued, raising the
/// queued automation events inside that loop. So what TSF finds pending when it is granted
/// its lock is the whole dispatcher's queue, not one list's subtree. WPF keeps that queue
/// internal: it is read here by reflection, and <see cref="Premise"/> answers why not when
/// WPF renames what it reads.
/// </para>
/// <para>
/// The subtree answer is DS-10's walker (contract 28): every element under a root whose
/// measure or arrange is not valid. A viewer's arrangement can be pending while the list's
/// own flags read valid, so the walk goes to the leaves. A collapsed element is skipped:
/// nothing lays out what is collapsed, so it never reads valid.
/// </para>
/// </remarks>
internal static class PendingLayout
{
    private const BindingFlags Internal = BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

    private static readonly Type? Manager =
        typeof(UIElement).Assembly.GetType("System.Windows.ContextLayoutManager", throwOnError: false);

    private static readonly MethodInfo? From = Manager?.GetMethod("From", Internal, [typeof(Dispatcher)]);

    private static readonly PropertyInfo? Dirtiness = Manager?.GetProperty("hasDirtiness", Internal);

    private static readonly FieldInfo? PostLayoutEvents = Manager?.GetField("_firePostLayoutEvents", Internal);

    /// <summary>Why the reader cannot read WPF's queue, or null when it can: the type, its
    /// lookup by dispatcher, its dirtiness and its queued post-layout events are all where
    /// WPF 10 keeps them.</summary>
    internal static string? Premise()
    {
        if (Manager is null)
        {
            return "WPF has no System.Windows.ContextLayoutManager";
        }

        if (From is null || From.ReturnType != Manager)
        {
            return "ContextLayoutManager has no static From(Dispatcher) answering itself";
        }

        if (Dirtiness is null || Dirtiness.PropertyType != typeof(bool))
        {
            return "ContextLayoutManager has no bool hasDirtiness";
        }

        if (PostLayoutEvents is null || PostLayoutEvents.FieldType != typeof(bool))
        {
            return "ContextLayoutManager has no bool _firePostLayoutEvents";
        }

        return null;
    }

    /// <summary>The global answer: what the dispatcher's layout queue still holds. Empty
    /// when an <c>UpdateLayout</c> from TSF would find nothing to lay out and no automation
    /// event to raise.</summary>
    internal static string[] InTheDispatcher(Dispatcher dispatcher)
    {
        if (Premise() is { } broken)
        {
            throw new InvalidOperationException("The layout queue reader cannot read WPF's queue: " + broken);
        }

        object manager = From!.Invoke(null, [dispatcher])
            ?? throw new InvalidOperationException("ContextLayoutManager.From answered null");
        var pending = new List<string>();
        if ((bool)Dirtiness!.GetValue(manager)!)
        {
            pending.Add("elements wait in the measure or arrange queue");
        }

        if ((bool)PostLayoutEvents!.GetValue(manager)!)
        {
            pending.Add("post-layout events wait to be raised");
        }

        return [.. pending];
    }

    /// <summary>The subtree answer (DS-10's walker): every visible element under
    /// <paramref name="root"/> still waiting on a layout pass, by its type.</summary>
    internal static string[] Under(DependencyObject root)
    {
        var pending = new List<string>();
        void Walk(DependencyObject parent)
        {
            // Nothing lays out what's collapsed, so it never reads valid.
            if (parent is UIElement { Visibility: not Visibility.Visible })
            {
                return;
            }

            if (parent is UIElement element && !(element.IsMeasureValid && element.IsArrangeValid))
            {
                pending.Add(element.GetType().Name);
            }

            for (int index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
            {
                Walk(VisualTreeHelper.GetChild(parent, index));
            }
        }

        Walk(root);
        return [.. pending];
    }

    /// <summary>Both answers in one line for a fact's message: empty when nothing is
    /// pending in the dispatcher or under <paramref name="root"/>.</summary>
    internal static string Describe(DependencyObject root)
    {
        string[] global = InTheDispatcher(root.Dispatcher);
        string[] under = Under(root);
        if (global.Length == 0 && under.Length == 0)
        {
            return string.Empty;
        }

        string subtree = under.Length == 0
            ? "none under the list"
            : $"{under.Length} under the list ("
                + string.Join(", ", under.GroupBy(name => name).Select(group => $"{group.Key} x{group.Count()}"))
                + ")";
        return (global.Length == 0 ? "the dispatcher's queue is clean" : string.Join(" and ", global)) + "; " + subtree;
    }
}
