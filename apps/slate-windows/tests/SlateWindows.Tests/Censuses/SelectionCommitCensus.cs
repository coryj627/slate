// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// W7-7 PR 4 (#1247, contract R-5; the owner's S4): every container whose
// SELECTION commits something — opens a note, applies a tag, switches the
// leaf, seats the canvas selection, moves the graph's row — owns its
// landing (SelectorFocus.SetOwnLanding), because a row of it takes the keys
// by selecting itself (a tree row, a tab) or by moving the grid's currency,
// and the commit follows: a palette restore onto a Tags row re-applied the
// tag the user had just cleared, a recycled Files row opened another note,
// a canvas table cell moved and narrated the seat (the completeness sweep's
// G6 and G16). LandOnStop sends a row token to its container's own landing.
// The census finds every selection wiring — in the shell's XAML (a
// selection handler, a TwoWay selection binding, a row style binding
// IsSelected TwoWay) and in its code (a subscription to a selection or
// currency event) — and holds each to an own landing, or to an exemption
// whose reason is written here: a site added with neither fails.

using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SlateWindows.Canvas;
using SlateWindows.Graph;

namespace SlateWindows.Tests.Censuses;

[Trait("census", "selection-commit")]
public sealed class SelectionCommitCensus
{
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    /// <summary>The window's selection-wired containers that do not commit
    /// on selection, and why.</summary>
    private static readonly Dictionary<string, string> XamlExempt = new(StringComparer.Ordinal)
    {
        ["QuickSwitcherResultsList"] = "the current result of a modal overlay's search; Enter commits",
        ["SearchOverlayResults"] = "the current result of a modal overlay's search; Enter commits",
        ["PanelCitationsList"] = "the selection is only remembered, to be re-seated after a publication",
    };

    /// <summary>The code's selection subscriptions that do not commit on
    /// selection, and why — by declaring class and the field
    /// subscribed.</summary>
    private static readonly Dictionary<string, string> CodeExempt = new(StringComparer.Ordinal)
    {
        ["SlateWindows.Bases.BaseSurfaceView._viewPicker"] = "a combo box, its own stop",
        ["SlateWindows.Bases.BaseSurfaceView._list"] = "the base's current row for its commands; nothing opens or speaks",
        ["SlateWindows.Bases.BaseSurfaceView._grid"] = "the base's current row for its commands; nothing opens or speaks",
        ["SlateWindows.CommandPaletteResultsPresenter._list"] = "the current command of a modal overlay's search; Enter commits",
        ["SlateWindows.Graph.ConnectionsLeafView._depth"] = "a combo box, its own stop",
        ["SlateWindows.Graph.GraphInspectorView.ring"] = "a combo box, its own stop",
        ["SlateWindows.Graph.GraphInspectorView.colour"] = "a combo box, its own stop",
        ["SlateWindows.Grids.AccessibleDataGrid._grid"] = "the grid's own currency, which its owners subscribe to",
    };

    /// <summary>The code's selection-committing containers: the field that
    /// holds each, and — for an AccessibleDataGrid — its DataGrid.</summary>
    private static readonly Dictionary<string, (Type View, Func<object, UIElement> Container)> CodeRequired = new(StringComparer.Ordinal)
    {
        ["SlateWindows.Canvas.CanvasOutlineView._tree"] = (typeof(CanvasOutlineView), view => ((CanvasOutlineView)view).TreeForTests),
        ["SlateWindows.Canvas.CanvasTableView._grid"] = (typeof(CanvasTableView), view => Field<Grids.AccessibleDataGrid>(view, "_grid").Grid),
        // The selection names the occurrence the leaf's verbs act on; its
        // own landing keeps every landing on ONE row — the shown selection,
        // else the first row unselected (codex PR 4 round 7 finding 1).
        ["SlateWindows.Graph.ConnectionsLeafView._tree"] = (typeof(ConnectionsLeafView), view => ((ConnectionsLeafView)view).TreeForTests),
        ["SlateWindows.Graph.GraphTableView._grid"] = (typeof(GraphTableView), view => Field<Grids.AccessibleDataGrid>(view, "_grid").Grid),
    };

    [Fact]
    public void EverySelectionCommittingListInTheWindowOwnsItsLanding()
    {
        XDocument window = XDocument.Load(Path.Combine(SourceText.ShellSourceRoot(), "MainWindow.xaml"));
        List<string> wired = window.Descendants()
            .Where(IsSelectionWired)
            .Select(element => (string?)element.Attribute(Xaml + "Name")
                ?? (string?)element.Attribute("AutomationProperties.AutomationId")
                ?? throw new Xunit.Sdk.XunitException($"a selection-wired <{element.Name.LocalName}> has neither x:Name nor AutomationId"))
            .ToList();
        Assert.True(wired.Count >= 8, $"only {wired.Count} selection-wired containers were found; the scrape is broken.");

        var offenders = new List<string>();
        RunSta(() =>
        {
            var shell = new MainWindow();
            try
            {
                foreach (string id in wired.Where(id => !XamlExempt.ContainsKey(id)))
                {
                    UIElement container = shell.FindName(id) as UIElement
                        ?? ByAutomationId(shell, id)
                        ?? throw new Xunit.Sdk.XunitException($"{id} is not in the constructed window.");
                    if (!SelectorFocus.HasOwnLanding(container))
                    {
                        offenders.Add($"{id}: commits on selection and has no own landing");
                    }
                }
            }
            finally
            {
                shell.Close();
            }
        });

        Assert.All(XamlExempt.Keys, id => Assert.Contains(id, wired));
        Assert.True(
            offenders.Count == 0,
            "Selection-committing containers a row token would re-select (S4):\n  " + string.Join("\n  ", offenders));
    }

    [Fact]
    public void EverySelectionCommittingContainerTheCodeBuildsOwnsItsLanding()
    {
        string[] events = ["SelectionChanged", "SelectedItemChanged", "CurrentRowChanged", "CurrentCellChanged"];
        var subscribed = new HashSet<string>(StringComparer.Ordinal);
        foreach ((string _, CSharpSource source) in ShellCompilation.Sources)
        {
            SemanticModel model = ShellCompilation.ModelFor(source);
            foreach (AssignmentExpressionSyntax assignment in source.Root.DescendantNodes().OfType<AssignmentExpressionSyntax>())
            {
                if (assignment.OperatorToken.ValueText != "+="
                    || assignment.Left is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: { } eventName } access
                    || !events.Contains(eventName)
                    || model.GetEnclosingSymbol(assignment.SpanStart)?.ContainingType is not { } declaring)
                {
                    continue;
                }

                string receiver = access.Expression switch
                {
                    IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
                    MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
                    _ => access.Expression.ToString(),
                };
                subscribed.Add($"{declaring.ToDisplayString()}.{receiver}");
            }
        }

        Assert.True(subscribed.Count >= 8, $"only {subscribed.Count} selection subscriptions were found; the scrape is broken.");
        string[] unclassified = subscribed
            .Where(site => !CodeExempt.ContainsKey(site) && !CodeRequired.ContainsKey(site))
            .OrderBy(site => site, StringComparer.Ordinal)
            .ToArray();
        Assert.True(
            unclassified.Length == 0,
            "Selection subscriptions neither owning a landing nor exempted with a reason (S4):\n  "
            + string.Join("\n  ", unclassified));
        Assert.All(CodeRequired.Keys.Concat(CodeExempt.Keys), site => Assert.Contains(site, subscribed));

        var offenders = new List<string>();
        RunSta(() =>
        {
            foreach ((string site, (Type view, Func<object, UIElement> container)) in CodeRequired)
            {
                object instance = Activator.CreateInstance(view)!;
                if (!SelectorFocus.HasOwnLanding(container(instance)))
                {
                    offenders.Add($"{site}: commits on selection and has no own landing");
                }
            }
        });
        Assert.True(
            offenders.Count == 0,
            "Selection-committing containers a row token would re-select (S4):\n  " + string.Join("\n  ", offenders));
    }

    /// <summary>A selection handler, a TwoWay selection binding, or a row
    /// style binding IsSelected TwoWay — on anything but a combo box, which
    /// is its own stop.</summary>
    private static bool IsSelectionWired(XElement element)
    {
        if (element.Name.LocalName is "ComboBox" || element.Name.LocalName.Contains('.', StringComparison.Ordinal))
        {
            return false;
        }

        bool TwoWay(string attribute) =>
            ((string?)element.Attribute(attribute))?.Contains("TwoWay", StringComparison.Ordinal) == true;

        return element.Attribute("SelectionChanged") is not null
            || element.Attribute("SelectedItemChanged") is not null
            || TwoWay("SelectedItem")
            || TwoWay("SelectedIndex")
            || TwoWay("SelectedValue")
            || element.Elements()
                .Where(child => child.Name.LocalName.EndsWith(".ItemContainerStyle", StringComparison.Ordinal))
                .Descendants()
                .Any(setter => setter.Name.LocalName == "Setter"
                    && (string?)setter.Attribute("Property") == "IsSelected"
                    && ((string?)setter.Attribute("Value"))?.Contains("TwoWay", StringComparison.Ordinal) == true);
    }

    private static UIElement? ByAutomationId(DependencyObject root, string id)
    {
        foreach (object child in LogicalTreeHelper.GetChildren(root))
        {
            if (child is not DependencyObject element)
            {
                continue;
            }

            if (element is UIElement ui && AutomationProperties.GetAutomationId(ui) == id)
            {
                return ui;
            }

            if (ByAutomationId(element, id) is { } nested)
            {
                return nested;
            }
        }

        return null;
    }

    private static T Field<T>(object instance, string name) =>
        (T)(instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(instance)
            ?? throw new Xunit.Sdk.XunitException($"{instance.GetType().Name}.{name} is gone"));

    private static void RunSta(Action body)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                PumpedDispatcher.Run(body);
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(60)), "the selection census timed out.");
        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
