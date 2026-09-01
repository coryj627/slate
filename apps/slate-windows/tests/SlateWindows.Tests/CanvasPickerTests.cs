// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Canvas;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W6-1 §E TE-6: the picker models — the generation cache's paging,
/// classification-before-cap, local refiltering (IE-37/IE-38), and
/// the card model's order preservation.
/// </summary>
public sealed class CanvasPickerTests
{
    private static FileSummary File(string path, bool markdown) => new(
        path, path, 0, 0, markdown, null, null, null, null, null, 0, 0);

    /// <summary>The walk joins every page, and classification runs
    /// over the WHOLE walk before the cap: a media file on the last
    /// page — beyond 200 markdown rows — is still admitted (ED-2).</summary>
    [Fact]
    public void PagingJoinsAndClassifiesBeforeTheCap()
    {
        var calls = 0;
        FileSummaryPage Page(string? cursor)
        {
            calls++;
            return cursor switch
            {
                null => new FileSummaryPage(
                    [.. Enumerable.Range(0, 150).Select(i => File($"note-{i}.md", true))],
                    "p2", 302),
                "p2" => new FileSummaryPage(
                    [.. Enumerable.Range(150, 150).Select(i => File($"note-{i}.md", true))],
                    "p3", 302),
                _ => new FileSummaryPage(
                    [File("deep/last-art.webp", false), File("deep/skip.exe", false)],
                    null, 302),
            };
        }
        CanvasVaultFilePickerModel media = CanvasVaultFilePickerModel.LoadAll(
            Page, f => SlateUniffiMethods.CanvasMediaClass(f.Path) is not null);
        Assert.Equal(3, calls);
        Assert.Single(media.Classified);
        Assert.Equal("deep/last-art.webp", media.Classified[0].Path);
        Assert.Single(media.Visible(""));
    }

    /// <summary>IE-38: a filter keystroke refilters the CACHE — the
    /// page seam is never called again — and the cap applies to the
    /// FILTERED view with the rest reachable by narrowing.</summary>
    [Fact]
    public void RefilteringNeverRepagesAndTheCapNarrows()
    {
        var calls = 0;
        FileSummaryPage Page(string? cursor)
        {
            calls++;
            return new FileSummaryPage(
                [.. Enumerable.Range(0, 250).Select(i => File($"note-{i:D3}.md", true))],
                null, 250);
        }
        CanvasVaultFilePickerModel notes = CanvasVaultFilePickerModel.LoadAll(
            Page, f => f.IsMarkdown);
        int afterLoad = calls;

        Assert.Equal(CanvasVaultFilePickerModel.DisplayCap, notes.Visible("").Length);
        Assert.Equal(250, notes.Classified.Length);
        Assert.Single(notes.Visible("note-249"));
        Assert.Equal(afterLoad, calls);
    }

    /// <summary>IE-38's supersession: a pick belongs to the model it
    /// was shown from; a superseded generation's pick is refused by
    /// reference.</summary>
    [Fact]
    public void AStalePickIsRefusedByGeneration()
    {
        FileSummaryPage Page(string? cursor) =>
            new([File("a.md", true)], null, 1);
        CanvasVaultFilePickerModel first =
            CanvasVaultFilePickerModel.LoadAll(Page, f => f.IsMarkdown);
        CanvasVaultFilePickerModel second =
            CanvasVaultFilePickerModel.LoadAll(Page, f => f.IsMarkdown);
        Assert.True(second.Admits(second));
        Assert.False(
            second.Admits(first),
            "a pick from a superseded generation was admitted (IE-38).");
    }

    /// <summary>The card model preserves core's order through the
    /// filter and never re-sorts.</summary>
    [Fact]
    public void TheCardModelFiltersWithoutReordering()
    {
        var model = new CanvasCardPickerModel(
        [
            new CanvasCardPickerRow("d", "Text card \"Delta\", in canvas"),
            new CanvasCardPickerRow("a", "Text card \"Alpha\", in canvas"),
            new CanvasCardPickerRow("c", "Group \"Alpine\", in canvas"),
        ]);
        Assert.Equal(["d", "a", "c"], model.Rows.Select(r => r.NodeId));
        Assert.Equal(["a", "c"], model.Visible("al").Select(r => r.NodeId));
    }
}
