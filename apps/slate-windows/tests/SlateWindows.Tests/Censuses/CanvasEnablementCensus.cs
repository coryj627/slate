// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using Microsoft.CodeAnalysis;

namespace SlateWindows.Tests.Censuses;

/// <summary>
/// §D D14's enablement census (obligation the round-3 majors named):
/// the visual-surface FLIP is complete when THIS census says so, not
/// when a list does. It derives every source file that consumes the
/// visual-disabled state — the `showVisual` id, the ships-later
/// phrase, or the §A registration fact's name — and requires one
/// disposition per consumer: what flips there when the renderer
/// lands. It fails BOTH ways: a consumer with no disposition, and a
/// disposition whose consumer is gone.
/// </summary>
[Trait("census", "canvas-enablement")]
public sealed class CanvasEnablementCensus
{
    private static readonly string[] Markers =
    [
        "CanvasShowVisual",
        "VisualShipsLater",
        "ShowVisualRegistersAndStaysDisabledUntilItsProjectionShips",
    ];

    /// <summary>File name → what the flip does there. The census walks
    /// the shell and this battery's own sources; the FlaUI project is
    /// out for the citation census's recorded reason.</summary>
    private static readonly (string File, string Disposition)[] Dispositions =
    [
        ("ChordTable.cs",
            "the row's IsRegistered stands; the flip touches nothing here"),
        ("SlateCommandRegistrar.cs",
            "the resolver returns the ENABLED workspace command; the "
            + "§D-staged comment retires"),
        ("WorkspaceViewModel.Canvas.cs",
            "CanvasShowVisualCommand gains a body and CanExecute; the "
            + "§D-staged comment retires"),
        ("CanvasSurfaceView.cs",
            "the visual radio enables, its ships-later help text goes, "
            + "and the surface switch grows the third arm"),
        ("CanvasTableTests.cs",
            "the switcher fact asserting the visual radio is disabled "
            + "becomes the enabled third-arm fact"),
        ("CanvasDocumentTests.cs",
            "the registration fact becomes the enabled-and-drives fact"),
        ("CanvasEnablementCensus.cs",
            "this census's own markers — the census retires WITH the "
            + "flip, its job done"),
    ];

    [Fact]
    public void EveryVisualDisabledConsumerCarriesADisposition()
    {
        string root = SourceText.RepoRoot();
        string shell = Path.Combine(
            root, "apps", "slate-windows", "src", "SlateWindows");
        string tests = Path.Combine(
            root, "apps", "slate-windows", "tests", "SlateWindows.Tests");
        var consumers = new SortedSet<string>(StringComparer.Ordinal);
        foreach (string dir in new[] { shell, tests })
        {
            foreach (string file in Directory.EnumerateFiles(
                dir, "*.cs", SearchOption.AllDirectories))
            {
                if (file.Contains(
                        $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                        StringComparison.Ordinal)
                    || file.Contains(
                        $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                        StringComparison.Ordinal))
                {
                    continue;
                }
                string text = File.ReadAllText(file);
                if (Markers.Any(marker => text.Contains(marker, StringComparison.Ordinal)))
                {
                    _ = consumers.Add(Path.GetFileName(file));
                }
            }
        }
        Assert.True(
            consumers.Count >= 4,
            $"only {consumers.Count} consumers found — the derivation is "
            + "scanning almost nothing, so the census would bless any flip.");
        string[] dispositioned = [.. Dispositions.Select(row => row.File)];
        string[] missing = [.. consumers.Except(dispositioned, StringComparer.Ordinal)];
        string[] stale = [.. dispositioned.Except(consumers, StringComparer.Ordinal)];
        Assert.True(
            missing.Length == 0,
            "a file consumes the visual-disabled state with NO disposition "
            + "for the flip — the flip inventory is incomplete by "
            + $"derivation, not by list: {string.Join(", ", missing)}");
        Assert.True(
            stale.Length == 0,
            "a disposition names a consumer that no longer exists: "
            + string.Join(", ", stale));
    }
}
