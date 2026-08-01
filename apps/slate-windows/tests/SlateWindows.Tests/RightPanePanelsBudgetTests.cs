// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows;
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
        // ONE card holding every image as a NESTED embed (round 5):
        // the decode itself must stay bounded — a post-build check
        // would let this single card allocate ~100 MB first.
        var gallery = new System.Text.StringBuilder("# Gallery\n\n");
        for (int i = 1; i <= 20; i++)
        {
            gallery.Append($"![[pix{i}.png]]\n");
        }
        File.WriteAllText(
            Path.Combine(_fixture.Root, "gallery.md"), gallery.ToString());
        File.WriteAllText(
            Path.Combine(_fixture.Root, "nested.md"), "![[gallery.md]]\n");
        // High-bit-depth PNGs (round 6): a fixed 4-bytes-per-pixel
        // multiplier undercounts Rgba64 sources by half.
        byte[] deepPng = MakeCompressiblePng(
            System.Windows.Media.PixelFormats.Rgba64, bytesPerPixel: 8);
        var deep = new System.Text.StringBuilder("# Deep\n\n");
        for (int i = 1; i <= 10; i++)
        {
            File.WriteAllBytes(
                Path.Combine(_fixture.Root, $"deep{i}.png"), deepPng);
            deep.Append($"![[deep{i}.png]]\n");
        }
        File.WriteAllText(
            Path.Combine(_fixture.Root, "deep.md"), deep.ToString());
        // A small compressed file with large decoded dimensions in a
        // codec that cannot downsample natively (round 6): GIF
        // decodes the full source frame before scaling, so the
        // reservation must charge source size and refuse before any
        // allocation.
        File.WriteAllBytes(
            Path.Combine(_fixture.Root, "huge.gif"), MakeLargeGif());
        File.WriteAllText(
            Path.Combine(_fixture.Root, "huge.md"), "![[huge.gif]]\n");
        // PNG has NO native scaler in WIC (round 9): an oversized
        // compressible PNG must be charged at source dimensions and
        // refused, exactly like the GIF.
        File.WriteAllBytes(
            Path.Combine(_fixture.Root, "hugepng.png"), MakeLargePng());
        File.WriteAllText(
            Path.Combine(_fixture.Root, "hugepng.md"), "![[hugepng.png]]\n");
        _session = VaultSession.OpenFilesystem(_fixture.Root);
        using var cancel = new CancelToken();
        _session.ScanInitial(cancel);
    }

    private static byte[] MakeCompressiblePng() => MakeCompressiblePng(
        System.Windows.Media.PixelFormats.Bgra32, bytesPerPixel: 4);

    private static byte[] MakeCompressiblePng(
        System.Windows.Media.PixelFormat format, int bytesPerPixel)
    {
        const int size = 1120;
        int stride = size * bytesPerPixel;
        var source = System.Windows.Media.Imaging.BitmapSource.Create(
            size,
            size,
            96,
            96,
            format,
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

    /// <summary>A VALID solid-black 4200x4200 GIF (compresses to a
    /// few KB): decoding it would allocate ~70 MB — past the budget
    /// — because GIF cannot downsample natively.</summary>
    private static byte[] MakeLargeGif() => EncodeLargeImage(
        new System.Windows.Media.Imaging.GifBitmapEncoder());

    /// <summary>The PNG twin (round 9): WIC has no native PNG
    /// scaler either, so the same source-size charge applies.</summary>
    private static byte[] MakeLargePng() => EncodeLargeImage(
        new System.Windows.Media.Imaging.PngBitmapEncoder());

    private static byte[] EncodeLargeImage(
        System.Windows.Media.Imaging.BitmapEncoder encoder)
    {
        const int size = 4200;
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

        // The third would cross the note-wide pool: refused IN CORE
        // (round 11 — the payload never crosses FFI), surfacing as a
        // loud unresolved card with truncation marked.
        EmbedRowViewModel degraded = panels.Embeds[2];
        Assert.IsType<EmbedResolution.Unresolved>(degraded.Resolution);
        Assert.True(degraded.Node.IsWarning);
        Assert.True(degraded.Truncated);
        Assert.Null(degraded.Node.Image);

        // Retained encoded bytes across all rows stay inside the cap.
        long retained = panels.Embeds.Sum(
            row => RightPanePanelsViewModel.CountImageBytes(row.Resolution));
        Assert.InRange(
            retained, 1, RightPanePanelsViewModel.MaxEmbedImageBytes);
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
        // images that fit the decoded budget decode; the rest are
        // ELIDED with a loud warning body while their rows survive.
        long perImage = 1120L * 1120L * 4;
        int admitted = (int)(
            RightPanePanelsViewModel.MaxEmbedDecodedImageBytes / perImage);
        Assert.InRange(admitted, 1, 19);
        for (int i = 0; i < panels.Embeds.Count; i++)
        {
            EmbedRowViewModel row = panels.Embeds[i];
            Assert.IsType<EmbedResolution.Image>(row.Resolution);
            if (i < admitted)
            {
                Assert.NotNull(row.Node.Image);
            }
            else
            {
                Assert.Null(row.Node.Image);
                Assert.True(row.Node.IsWarning);
                Assert.Contains(
                    row.Node.Parts,
                    part => part.Text == EditorInteractionCoordinator
                        .ImageBudgetSpentMessage);
            }
        }

        // The retained cards, re-measured, stay inside the budget.
        long retained = panels.Embeds.Sum(
            row => RightPanePanelsViewModel.CountDecodedImageBytes(row.Node));
        Assert.InRange(
            retained, 1, RightPanePanelsViewModel.MaxEmbedDecodedImageBytes);
    }

    [Fact]
    public void NestedImagesInsideOneCardReserveBeforeDecoding()
    {
        var panels = new RightPanePanelsViewModel(
            _session,
            _ => { },
            (_, _) => true,
            _ => true,
            (_, _) => { });
        panels.NoteChanged("nested.md");
        Assert.True(
            SpinWait.SpinUntil(
                () => !panels.IsLoadingLinks && !panels.IsResolvingEmbeds,
                TimeSpan.FromSeconds(30)),
            "the nested note never finished resolving");

        // One card, every image nested inside it: decode-time
        // reservation bounds the card WHILE it is built.
        EmbedRowViewModel row = Assert.Single(panels.Embeds);
        Assert.Equal("Embedded note: gallery.md", row.Node.Title);
        Assert.InRange(
            RightPanePanelsViewModel.CountDecodedImageBytes(row.Node),
            1,
            RightPanePanelsViewModel.MaxEmbedDecodedImageBytes);

        (int decoded, int elided) = CountImages(row.Node);
        long perImage = 1120L * 1120L * 4;
        int admitted = (int)(
            RightPanePanelsViewModel.MaxEmbedDecodedImageBytes / perImage);
        Assert.Equal(admitted, decoded);
        Assert.Equal(20 - admitted, elided);
    }

    [Fact]
    public void HighBitDepthImagesChargeTheirTruePixelCost()
    {
        var panels = new RightPanePanelsViewModel(
            _session,
            _ => { },
            (_, _) => true,
            _ => true,
            (_, _) => { });
        panels.NoteChanged("deep.md");
        Assert.True(
            SpinWait.SpinUntil(
                () => !panels.IsLoadingLinks && !panels.IsResolvingEmbeds,
                TimeSpan.FromSeconds(30)),
            "the deep note never finished resolving");

        // Rgba64 decodes at 8 bytes per pixel — a fixed 4 would
        // admit twice as many images as the budget allows.
        Assert.Equal(10, panels.Embeds.Count);
        long perImage = 1120L * 1120L * 8;
        int admitted = (int)(
            RightPanePanelsViewModel.MaxEmbedDecodedImageBytes / perImage);
        Assert.InRange(admitted, 1, 9);
        int decodedCount = panels.Embeds.Count(
            row => row.Node.Image is not null);
        Assert.Equal(admitted, decodedCount);
        long retained = panels.Embeds.Sum(
            row => RightPanePanelsViewModel.CountDecodedImageBytes(row.Node));
        Assert.InRange(
            retained, 1, RightPanePanelsViewModel.MaxEmbedDecodedImageBytes);
    }

    [Fact]
    public void DeclaredHugeDimensionsAreRefusedFromTheHeader()
    {
        var panels = new RightPanePanelsViewModel(
            _session,
            _ => { },
            (_, _) => true,
            _ => true,
            (_, _) => { });
        panels.NoteChanged("huge.md");
        Assert.True(
            SpinWait.SpinUntil(
                () => !panels.IsLoadingLinks && !panels.IsResolvingEmbeds,
                TimeSpan.FromSeconds(30)),
            "the huge note never finished resolving");

        // GIF cannot downsample natively: the 4200x4200 source must
        // be refused from its HEADER (a scaled-size reservation
        // would admit ~5 MB, then decode ~70 MB).
        EmbedRowViewModel row = Assert.Single(panels.Embeds);
        Assert.Null(row.Node.Image);
        Assert.True(row.Node.IsWarning);
        Assert.Contains(
            row.Node.Parts,
            part => part.Text
                == EditorInteractionCoordinator.ImageBudgetSpentMessage);
    }

    [Fact]
    public void OversizedCompressiblePngIsRefusedFromTheHeader()
    {
        var panels = new RightPanePanelsViewModel(
            _session,
            _ => { },
            (_, _) => true,
            _ => true,
            (_, _) => { });
        panels.NoteChanged("hugepng.md");
        Assert.True(
            SpinWait.SpinUntil(
                () => !panels.IsLoadingLinks && !panels.IsResolvingEmbeds,
                TimeSpan.FromSeconds(30)),
            "the huge-png note never finished resolving");

        // PNG has no native WIC scaler: the 4200x4200 source must be
        // charged at source size and refused before pixel decode (a
        // scaled-size reservation would admit ~5 MB, then allocate
        // ~70 MB).
        EmbedRowViewModel row = Assert.Single(panels.Embeds);
        Assert.Null(row.Node.Image);
        Assert.True(row.Node.IsWarning);
        Assert.Contains(
            row.Node.Parts,
            part => part.Text
                == EditorInteractionCoordinator.ImageBudgetSpentMessage);
    }

    [Fact]
    public void ReservationArithmeticCannotBePoisoned()
    {
        // Attacker-controlled header dimensions can overflow long:
        // the cost saturates and every reservation refuses it,
        // leaving the accumulator clean for later valid images
        // (round 7).
        Assert.Equal(
            long.MaxValue,
            EditorInteractionCoordinator.SaturatingDecodeCost(
                int.MaxValue, int.MaxValue, 8));
        Assert.Equal(
            long.MaxValue,
            EditorInteractionCoordinator.SaturatingDecodeCost(0, 100, 4));
        Assert.Equal(
            long.MaxValue,
            EditorInteractionCoordinator.SaturatingDecodeCost(-5, 100, 4));
        Assert.Equal(
            1120L * 1120 * 4,
            EditorInteractionCoordinator.SaturatingDecodeCost(1120, 1120, 4));

        long limit = 100;
        long spent = 0;
        // The poisoned cost is refused and leaves spent untouched...
        Assert.False(EditorInteractionCoordinator.TryReserveDecodedBytes(
            ref spent, long.MaxValue, limit));
        Assert.Equal(0, spent);
        Assert.False(EditorInteractionCoordinator.TryReserveDecodedBytes(
            ref spent, -50, limit));
        Assert.Equal(0, spent);
        // ...so later valid reservations still account correctly.
        Assert.True(EditorInteractionCoordinator.TryReserveDecodedBytes(
            ref spent, 60, limit));
        Assert.True(EditorInteractionCoordinator.TryReserveDecodedBytes(
            ref spent, 40, limit));
        Assert.False(EditorInteractionCoordinator.TryReserveDecodedBytes(
            ref spent, 1, limit));
        Assert.Equal(100, spent);
    }

    private static (int Decoded, int Elided) CountImages(
        SlateWindows.EditorEmbedPreviewNode node)
    {
        int decoded = 0;
        int elided = 0;
        if (node.Image is not null)
        {
            decoded++;
        }
        else if (node.Parts.Any(part => part.Text
            == EditorInteractionCoordinator.ImageBudgetSpentMessage))
        {
            elided++;
        }
        foreach (SlateWindows.EditorEmbedPreviewPart part in node.Parts)
        {
            if (part.Nested is { } nested)
            {
                (int d, int e) = CountImages(nested);
                decoded += d;
                elided += e;
            }
        }
        return (decoded, elided);
    }
}
