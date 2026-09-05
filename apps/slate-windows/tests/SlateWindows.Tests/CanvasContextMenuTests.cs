// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Canvas;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W6-1 §E TE-8 (IE-31), widened by §G2 TG2-7 (G2-12): the ONE
/// applicability plan over (surface, target), mac's order per
/// projection, the outline menu's bidirectional equality against it,
/// and no silent arm — never a dead click, never a hand-list.
/// </summary>
public sealed class CanvasContextMenuTests
{
    private static readonly CanvasNeighbor Captured =
        new("e1", "n2", "Other", default, null, null, true);

    /// <summary>Every target the censuses walk: the five node kinds in
    /// and out of a group, and a captured connection.</summary>
    private static IEnumerable<(string Name, CanvasContextTarget Target)> Targets()
    {
        foreach (string kind in CanvasContextMenuPlan.NodeKinds)
        {
            yield return (kind, new CanvasContextTarget.Node("n1", kind, false));
            yield return (kind + " in a group", new CanvasContextTarget.Node("n1", kind, true));
        }
        yield return ("connection", new CanvasContextTarget.Connection("n1", Captured));
    }

    private static IEnumerable<string> Names(CanvasContextSurface surface, CanvasContextTarget target) =>
        CanvasContextMenuPlan.RowsFor(surface, target).Select(row => row.Name);

    /// <summary>The plan's tables per (surface, target): mac's order,
    /// the kind-scoped rows present exactly where mac places them, Set
    /// Color on a group, Remove from Group only inside a group, a
    /// connection's three rows, and the grid's subset from the SAME
    /// plan.</summary>
    [Fact]
    public void ThePlanServesMacsOrderPerSurfaceAndTarget()
    {
        Assert.Equal(
            [
                CanvasPhrase.OpenRowAction,
                CanvasPhrase.EditCardRowAction,
                CanvasPhrase.ConvertToNoteRowAction,
                CanvasPhrase.CreateConnectedCardRowAction,
                CanvasPhrase.DuplicateRowAction,
                CanvasPhrase.ToggleMarkRowAction,
                CanvasPhrase.ConnectToRowAction,
                CanvasPhrase.SetColorRowAction,
                CanvasPhrase.MoveIntoGroupRowAction,
                CanvasPhrase.DeleteRowAction,
            ],
            Names(CanvasContextSurface.Outline, new CanvasContextTarget.Node("n1", "text", false)));

        Assert.Equal(
            [
                CanvasPhrase.OpenRowAction,
                CanvasPhrase.CreateConnectedCardRowAction,
                CanvasPhrase.DuplicateRowAction,
                CanvasPhrase.RenameGroupRowAction,
                CanvasPhrase.ToggleMarkRowAction,
                CanvasPhrase.ConnectToRowAction,
                CanvasPhrase.SetColorRowAction,
                CanvasPhrase.MoveIntoGroupRowAction,
                CanvasPhrase.RemoveFromGroupRowAction,
                CanvasPhrase.UngroupRowAction,
            ],
            Names(CanvasContextSurface.Outline, new CanvasContextTarget.Node("g1", "group", true)));

        // Locate File… on file and image cards, nowhere else; Edit Card
        // Text… and Convert to Note… on text cards only.
        foreach (string kind in (string[])["file", "image"])
        {
            IEnumerable<string> names =
                Names(CanvasContextSurface.Outline, new CanvasContextTarget.Node("n1", kind, false));
            Assert.Contains(CanvasPhrase.LocateFileRowAction, names);
            Assert.DoesNotContain(CanvasPhrase.EditCardRowAction, names);
            Assert.DoesNotContain(CanvasPhrase.ConvertToNoteRowAction, names);
        }
        IEnumerable<string> link =
            Names(CanvasContextSurface.Outline, new CanvasContextTarget.Node("n1", "link", false));
        Assert.DoesNotContain(CanvasPhrase.LocateFileRowAction, link);
        Assert.DoesNotContain(CanvasPhrase.EditCardRowAction, link);
        Assert.DoesNotContain(CanvasPhrase.RemoveFromGroupRowAction, link);

        Assert.Equal(
            [
                CanvasPhrase.JumpToCardRowAction,
                CanvasPhrase.EditConnectionRowAction,
                CanvasPhrase.DeleteConnectionRowAction,
            ],
            Names(CanvasContextSurface.Outline, new CanvasContextTarget.Connection("n1", Captured)));

        Assert.Equal(
            [CanvasPhrase.OpenRowAction, CanvasPhrase.ToggleMarkRowAction, CanvasPhrase.DeleteRowAction],
            Names(CanvasContextSurface.Grid, new CanvasContextTarget.Node("n1", "text", true)));
        Assert.Equal(
            [CanvasPhrase.OpenRowAction, CanvasPhrase.ToggleMarkRowAction, CanvasPhrase.UngroupRowAction],
            Names(CanvasContextSurface.Grid, new CanvasContextTarget.Node("g1", "group", false)));
        // The grid has no connection rows (G2-12).
        Assert.Empty(CanvasContextMenuPlan.RowsFor(
            CanvasContextSurface.Grid, new CanvasContextTarget.Connection("n1", Captured)));
    }

    /// <summary>No silent arm in any projection: a row is live, or it
    /// carries the WHY a reader can hear (the mac contract's
    /// visible-with-reason shape). Today every row is live — the last
    /// staged reason retired with the grid's Toggle Mark.</summary>
    [Fact]
    public void EveryRowIsLiveOrCarriesItsReason()
    {
        foreach (CanvasContextSurface surface in Enum.GetValues<CanvasContextSurface>())
        {
            foreach ((string name, CanvasContextTarget target) in Targets())
            {
                foreach (CanvasContextMenuRow row in CanvasContextMenuPlan.RowsFor(surface, target))
                {
                    Assert.True(
                        row.Enabled == (row.DisabledReason is null),
                        $"{surface}/{name}/{row.Name}: a staged row without a reason — or a "
                        + "live row carrying one — breaks the visible-why contract.");
                    Assert.True(row.Enabled, $"{surface}/{name}/{row.Name}: G2-12 retired the last staged row.");
                }
            }
        }
    }

    /// <summary>Every verb the enum can name is placed by SOME
    /// projection and carries mac's label through the one label table
    /// — no orphan verb, no row whose name is not its verb's.</summary>
    [Fact]
    public void EveryVerbIsPlacedSomewhereUnderItsOwnLabel()
    {
        var placed = new HashSet<CanvasContextVerb>();
        foreach (CanvasContextSurface surface in Enum.GetValues<CanvasContextSurface>())
        {
            foreach ((_, CanvasContextTarget target) in Targets())
            {
                foreach (CanvasContextMenuRow row in CanvasContextMenuPlan.RowsFor(surface, target))
                {
                    placed.Add(row.Verb);
                    Assert.Equal(CanvasContextMenuPlan.Label(row.Verb), row.Name);
                }
            }
        }
        foreach (CanvasContextVerb verb in Enum.GetValues<CanvasContextVerb>())
        {
            Assert.Contains(verb, placed);
            Assert.False(string.IsNullOrWhiteSpace(CanvasContextMenuPlan.Label(verb)));
        }
    }

    /// <summary>IE-31's census, per target (G2-12): the outline's built
    /// menu equals the plan's OUTLINE projection — headers, enabled
    /// flags and reasons verbatim, the same count both ways — so the
    /// surface can neither hand-list a subset nor carry a row the plan
    /// did not give it.</summary>
    [Fact]
    public void TheOutlineMenuEqualsThePlan() => RunSta(() =>
    {
        foreach ((string name, CanvasContextTarget target) in Targets())
        {
            // The census drives the ONE plan-to-menu mapping the
            // opening handler itself uses.
            System.Windows.Controls.ContextMenu menu =
                CanvasOutlineView.BuildMenuFromPlan(target, _ => { });
            var planned = CanvasContextMenuPlan.RowsFor(CanvasContextSurface.Outline, target);
            Assert.True(planned.Length == menu.Items.Count, $"{name}: the menu and the plan differ in count.");
            foreach ((CanvasContextMenuRow expected,
                object actual) in planned.Zip(menu.Items.Cast<object>()))
            {
                var item = Assert.IsType<System.Windows.Controls.MenuItem>(actual);
                Assert.Equal(expected.Name, item.Header);
                Assert.Equal(expected.Enabled, item.IsEnabled);
                Assert.Equal(expected.DisabledReason, item.ToolTip);
            }
        }
    });

    /// <summary>The outline row's target is DERIVED from the row: a
    /// node row's kind and group membership, a connection row's source
    /// and captured neighbor — no consumer decides its own target.</summary>
    [Fact]
    public void TheOutlineRowsTargetIsDerivedFromTheRow()
    {
        var inGroup = new CanvasOutlineRow("n1", 1, "file", "Paper", "Paper", ["Research"], 1, 2, 0, null);
        CanvasOutlineRowViewModel node = CanvasOutlineRowViewModel.ForNode(inGroup, marked: false, filtered: false);
        Assert.Equal(
            new CanvasContextTarget.Node("n1", "file", true),
            CanvasOutlineView.TargetOf(node));

        CanvasOutlineRowViewModel connection =
            CanvasOutlineRowViewModel.ForConnection(inGroup, Captured, "text", 1, 1);
        Assert.Equal(
            new CanvasContextTarget.Connection("n1", Captured),
            CanvasOutlineView.TargetOf(connection));
    }

    private static void RunSta(Action body)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                body();
            }
            catch (Exception e)
            {
                failure = e;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null)
        {
            throw new Xunit.Sdk.XunitException(failure.ToString());
        }
    }
}
