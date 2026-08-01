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
        // VALID, highly compressible images (round 4): a few-KB PNG
        // decodes to ~5 MB of pixels at the 1120px decode bound, so
        // encoded-byte accounting alone admits unbounded bitmaps.
        byte[] png = MakeCompressiblePng();
        var embeds = new System.Text.StringBuilder("# Decoded\n\n");
        for (int i = 1; i <= 20; i++)
        {
            File.WriteAllBytes(
                Path.Combine(_fixture.Root, $"pix{i}.png"), png);
            embeds.Append($"![[pix{i}.png]]\n");
        }
        File.WriteAllText(
            Path.Combine(_fixture.Root, "decoded.md"), embeds.ToString());
        _session = VaultSession.OpenFilesystem(_fixture.Root);
        using var cancel = new CancelToken();
        _session.ScanInitial(cancel);
    }

    private static byte[] MakeCompressiblePng()
    {
        const int size = 1120;
        const int stride = size * 4;
        var source = System.Windows.Media.Imaging.BitmapSource.Create(
            size,
            size,
            96,
            96,
            System.Windows.Media.PixelFormats.Bgra32,
            null,
            new byte[stride * size],
            stride);
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(
            System.Windows.Media.Imaging.BitmapFrame.Create(source));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
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

    [Fact]
    public void DecodedPixelBudgetBoundsRetainedBitmaps()
    {
        var panels = new RightPanePanelsViewModel(
            _session,
            _ => { },
            (_, _) => true,
            _ => true,
            (_, _) => { });
        panels.NoteChanged("decoded.md");
        Assert.True(
            SpinWait.SpinUntil(
                () => !panels.IsLoadingLinks && !panels.IsResolvingEmbeds,
                TimeSpan.FromSeconds(30)),
            "the decoded note never finished resolving");

        Assert.Equal(20, panels.Embeds.Count);

        // Tiny encoded payloads, ~5 MB each decoded: exactly the
        // rows that fit the decoded budget resolve, the rest degrade.
        long perImage = 1120L * 1120L * 4;
        int admitted = (int)(
            RightPanePanelsViewModel.MaxEmbedDecodedImageBytes / perImage);
        Assert.InRange(admitted, 1, 19);
        for (int i = 0; i < panels.Embeds.Count; i++)
        {
            if (i < admitted)
            {
                Assert.IsType<EmbedResolution.Image>(
                    panels.Embeds[i].Resolution);
            }
            else
            {
                Assert.Equal(
                    EmbedRowViewModel.OverBudgetMessage,
                    panels.Embeds[i].Node.Title);
            }
        }

        // The retained cards, re-measured, stay inside the budget.
        long retained = panels.Embeds.Sum(
            row => RightPanePanelsViewModel.CountDecodedImageBytes(row.Node));
        Assert.InRange(
            retained, 1, RightPanePanelsViewModel.MaxEmbedDecodedImageBytes);
    }
}
