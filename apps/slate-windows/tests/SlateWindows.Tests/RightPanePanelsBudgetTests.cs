// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Panels;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W4-2 adversarial round 2: the cumulative image budget is a bound
/// on RETAINED bytes — a resolution whose payload would cross the
/// cap is dropped, not admitted (the round-1 pre-check let the last
/// target overshoot arbitrarily). Isolated from the main panel suite
/// because its fixture writes ~21 MiB of image payloads per test.
/// </summary>
public sealed class RightPanePanelsBudgetTests : IDisposable
{
    // Three payloads sized so two fit inside MaxEmbedImageBytes
    // (14 MiB) and the third would cross it (21 MiB) while every
    // individual payload stays inside the CORE per-preview budget.
    private const int PayloadBytes = 7 * 1024 * 1024;

    private readonly FixtureVault _fixture;
    private readonly VaultSession _session;

    public RightPanePanelsBudgetTests()
    {
        _fixture = FixtureVault.Create(0, "right-pane-budget");
        File.WriteAllBytes(
            Path.Combine(_fixture.Root, "big1.png"), new byte[PayloadBytes]);
        File.WriteAllBytes(
            Path.Combine(_fixture.Root, "big2.png"), new byte[PayloadBytes]);
        File.WriteAllBytes(
            Path.Combine(_fixture.Root, "big3.png"), new byte[PayloadBytes]);
        File.WriteAllText(
            Path.Combine(_fixture.Root, "budget.md"),
            "![[big1.png]]\n\n![[big2.png]]\n\n![[big3.png]]\n");
        _session = VaultSession.OpenFilesystem(_fixture.Root);
        using var cancel = new CancelToken();
        _session.ScanInitial(cancel);
    }

    public void Dispose()
    {
        _session.Dispose();
        _fixture.Dispose();
    }

    [Fact]
    public void ImageBudgetBoundsRetainedBytesPostResolution()
    {
        var panels = new RightPanePanelsViewModel(
            _session,
            _ => { },
            (_, _) => true,
            _ => true,
            (_, _) => { });
        panels.NoteChanged("budget.md");
        Assert.True(
            SpinWait.SpinUntil(
                () => !panels.IsLoadingLinks && !panels.IsResolvingEmbeds,
                TimeSpan.FromSeconds(30)),
            "the budget note never finished resolving");

        Assert.Equal(3, panels.Embeds.Count);

        // The first two payloads fit and resolve as images.
        Assert.IsType<EmbedResolution.Image>(panels.Embeds[0].Resolution);
        Assert.IsType<EmbedResolution.Image>(panels.Embeds[1].Resolution);

        // The third would push retained bytes past the cap: dropped
        // AFTER resolution, degraded loudly, Jump kept alive.
        EmbedRowViewModel degraded = panels.Embeds[2];
        Assert.Equal(
            EmbedRowViewModel.OverBudgetMessage, degraded.Node.Title);
        Assert.True(degraded.Node.IsWarning);
        Assert.Equal("big3.png", degraded.Node.SourcePath);
    }
}
