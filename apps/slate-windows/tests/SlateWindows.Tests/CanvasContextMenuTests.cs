// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Canvas;

namespace SlateWindows.Tests;

/// <summary>
/// W6-1 §E TE-8 (IE-31): the ONE applicability plan, the outline
/// menu's derived equality against it, and the staged rows' visible
/// reasons — never a dead click, never a hand-list.
/// </summary>
public sealed class CanvasContextMenuTests
{
    /// <summary>The plan's tables per kind: mac's leading pair, the
    /// kind's own verbs, staged rows carrying their why.</summary>
    [Fact]
    public void ThePlanDerivesEachKindsRows()
    {
        var text = CanvasContextMenuPlan.RowsFor("text");
        Assert.Equal(
            [
                CanvasPhrase.OpenRowAction,
                CanvasPhrase.ToggleMarkRowAction,
                CanvasPhrase.EditCardRowAction,
                CanvasPhrase.SetColorRowAction,
                CanvasPhrase.DeleteRowAction,
            ],
            text.Select(row => row.Name));
        Assert.True(text.Single(r => r.Name == CanvasPhrase.DeleteRowAction).Enabled);
        Assert.False(text.Single(r => r.Name == CanvasPhrase.ToggleMarkRowAction).Enabled);
        // §F TF-8 (FD-4): the prompt machinery landed — the row is
        // LIVE on the §E commit path.
        Assert.True(
            text.Single(r => r.Name == CanvasPhrase.SetColorRowAction).Enabled);

        var group = CanvasContextMenuPlan.RowsFor("group");
        Assert.Equal(
            [
                CanvasPhrase.OpenRowAction,
                CanvasPhrase.ToggleMarkRowAction,
                CanvasPhrase.RenameGroupRowAction,
                CanvasPhrase.UngroupRowAction,
            ],
            group.Select(row => row.Name));
        Assert.True(group.Single(r => r.Name == CanvasPhrase.UngroupRowAction).Enabled);
        Assert.DoesNotContain(group, r => r.Name == CanvasPhrase.DeleteRowAction);

        var file = CanvasContextMenuPlan.RowsFor("file");
        Assert.DoesNotContain(file, r => r.Name == CanvasPhrase.EditCardRowAction);
        Assert.True(file.Single(r => r.Name == CanvasPhrase.DeleteRowAction).Enabled);
    }

    /// <summary>Every staged row carries a WHY — the mac contract's
    /// visible-with-reason shape has no silent arm in any kind.</summary>
    [Fact]
    public void EveryStagedRowCarriesItsReason()
    {
        foreach (string kind in (string[])["text", "file", "link", "image", "group"])
        {
            foreach (CanvasContextMenuRow row in CanvasContextMenuPlan.RowsFor(kind))
            {
                Assert.True(
                    row.Enabled == (row.DisabledReason is null),
                    $"{kind}/{row.Name}: a staged row without a reason — or a "
                    + "live row carrying one — breaks the visible-why contract.");
            }
        }
    }

    /// <summary>IE-31's census: the outline's built menu equals the
    /// plan — headers, enabled flags and reasons verbatim, per kind —
    /// so no surface can hand-list its own subset.</summary>
    [Fact]
    public void TheOutlineMenuEqualsThePlan() => RunSta(() =>
    {
        foreach (string kind in (string[])["text", "group", "file"])
        {
            // The census drives the ONE plan-to-menu mapping the
            // opening handler itself uses.
            System.Windows.Controls.ContextMenu menu =
                CanvasOutlineView.BuildMenuFromPlan(kind, (_, _) => { }, "n1");
            var planned = CanvasContextMenuPlan.RowsFor(kind);
            Assert.Equal(planned.Length, menu.Items.Count);
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
