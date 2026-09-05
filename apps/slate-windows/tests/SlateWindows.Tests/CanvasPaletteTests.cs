// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows.Media;
using SlateWindows.Canvas;

namespace SlateWindows.Tests;

/// <summary>
/// §D D13's arithmetic edges (the review round): the hex domain's
/// contract is exactly '#RGB/#RRGGBB or null', and the parser must
/// not let the framework's whitespace tolerance widen it.
/// </summary>
public sealed class CanvasPaletteTests
{
    /// <summary>Whitespace-padded bodies are NOT colors — the
    /// HexNumber composite admitted "# FF00 " as green.</summary>
    [Fact]
    public void WhitespacePaddedHexIsRefused()
    {
        Assert.Null(CanvasPalette.Hex("# FF00 "));
        Assert.Null(CanvasPalette.Hex("#FF 00A"));
        Assert.Null(CanvasPalette.Hex("# F0"));
    }

    /// <summary>The two documented shapes parse; the shorthand
    /// expands per channel.</summary>
    [Fact]
    public void TheTwoDocumentedShapesParse()
    {
        Assert.Equal(Color.FromRgb(0xFF, 0x00, 0xFF), CanvasPalette.Hex("#FF00FF"));
        Assert.Equal(Color.FromRgb(0xAA, 0xBB, 0xCC), CanvasPalette.Hex("#ABC"));
        Assert.Null(CanvasPalette.Hex("FF00FF"));
        Assert.Null(CanvasPalette.Hex("#GGHHII"));
    }

    /// <summary>The engine's teardown guard (the review round): after
    /// Shutdown, neither a commit nor a queued intake installs into
    /// the dying view.</summary>
    [Fact]
    public void AShutDownEngineInstallsNothing()
    {
        var engine = new CanvasPresentationEngine(synchronousForTests: true);
        engine.OnPublicationApplied(CanvasPublication.Seed());
        var installs = 0;
        engine.StateInstalled += (_, _) => installs++;
        engine.Shutdown();
        engine.CommitViewport(v => v.ZoomedIn(0, 0));
        engine.OnPublicationApplied(CanvasPublication.Seed().WithNeedleIntent("n"));
        Assert.True(
            installs == 0,
            "a shut-down engine installed a state: the dying-UI publish "
            + "race the panel scheduler closed is reopened.");
    }
}
