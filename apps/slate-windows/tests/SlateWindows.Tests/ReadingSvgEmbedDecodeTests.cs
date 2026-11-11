// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows.Documents;
using SlateWindows.Reading;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// #1634: an SVG embed is decoded with its resolution, on the reading
/// refresh's worker, never while the document is built on the dispatcher.
/// The decode can run up to its 5 s budget (#1581); a dispatcher blocked
/// that long when the reading view takes focus leaves NVDA on its
/// IAccessible fallback, and Down says nothing until focus moves.
/// </summary>
public sealed class ReadingSvgEmbedDecodeTests
{
    private const string Svg =
        "<svg xmlns='http://www.w3.org/2000/svg' width='8' height='8'>"
        + "<rect width='8' height='8' fill='red'/></svg>";

    [Fact]
    public void AnSvgEmbedIsDecodedOffTheDispatcherBeforeTheDocumentIsBuilt()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "reading-svg-embed-thread");
        File.WriteAllText(Path.Combine(fixture.Root, "pic.svg"), Svg);
        File.WriteAllText(Path.Combine(fixture.Root, "host.md"), "Before.\n\n![[pic.svg]]\n\nAfter.\n");
        var fetched = new List<int>();
        var built = new List<string>();
        ReadingContentViewModel.EmbedSvgDecodeProbeForTests = _ =>
        {
            lock (fetched)
            {
                fetched.Add(Environment.CurrentManagedThreadId);
            }
        };
        ReadingDocumentBuilder.EmbedImageDecodeProbeForTests = built.Add;
        try
        {
            StaThread.RunPumped(() =>
            {
                int dispatcher = Environment.CurrentManagedThreadId;
                using VaultSession session = Scanned(fixture.Root);
                using var tab = new WorkspaceTabViewModel(
                    session,
                    new WorkspaceTabState(
                        Guid.NewGuid(),
                        new WorkspaceItemState(WorkspaceItemKind.Markdown, "host.md")),
                    startInteractionBackgroundWork: true);
                tab.ToggleViewMode();
                ReadingContentViewModel reading = Assert.IsType<ReadingContentViewModel>(tab.Reading);
                Assert.True(
                    PumpedDispatcher.PumpUntil(() => !reading.IsLoading && reading.Document is not null),
                    "the projection never landed");

                Assert.Empty(built);
                lock (fetched)
                {
                    Assert.NotEmpty(fetched);
                    Assert.DoesNotContain(dispatcher, fetched);
                }

                FlowDocument document = reading.Document!;
                Assert.True(
                    BlocksOf(document.Blocks).Any(
                        block => block is BlockUIContainer { Child: ReadingDiagramImage { Source: not null } }),
                    "the decoded image was not placed: "
                    + new TextRange(document.ContentStart, document.ContentEnd).Text
                    + " / " + reading.LastTerminalFailureForTests);
            });
        }
        finally
        {
            ReadingContentViewModel.EmbedSvgDecodeProbeForTests = null;
            ReadingDocumentBuilder.EmbedImageDecodeProbeForTests = null;
        }
    }

    /// <summary>The decode's outcome is part of the projection's identity: a
    /// decode that ran past its budget once and succeeds on the next refresh
    /// rebuilds the card with the image, where an unchanged note alone would
    /// keep showing the timeout sentence.</summary>
    [Fact]
    public void ARefreshWhoseSvgDecodeNowSucceedsShowsTheImage()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "reading-svg-embed-retry");
        File.WriteAllText(Path.Combine(fixture.Root, "pic.svg"), Svg);
        File.WriteAllText(Path.Combine(fixture.Root, "host.md"), "![[pic.svg]]\n");
        StaThread.Run(() =>
        {
            using VaultSession session = Scanned(fixture.Root);
            using var release = new ManualResetEventSlim(false);
            EditorInteractionCoordinator.SvgDecodeSlotsForTests = new EditorInteractionCoordinator.SvgDecodeSlots();
            EditorInteractionCoordinator.SvgDecodeBodyForTests = (_, _) =>
            {
                release.Wait(TimeSpan.FromSeconds(10));
                return null;
            };
            EditorInteractionCoordinator.SvgDecodeWaitForTests = _ => 0x102;
            try
            {
                using var tab = new WorkspaceTabViewModel(
                    session,
                    new WorkspaceTabState(
                        Guid.NewGuid(),
                        new WorkspaceItemState(WorkspaceItemKind.Markdown, "host.md")),
                    startInteractionBackgroundWork: false);
                tab.ToggleViewMode();
                ReadingContentViewModel reading = Assert.IsType<ReadingContentViewModel>(tab.Reading);
                Assert.Contains(
                    "Image not shown: decoding took longer than 5 seconds.",
                    TextOf(reading.Document!),
                    StringComparison.Ordinal);

                EditorInteractionCoordinator.SvgDecodeWaitForTests = null;
                EditorInteractionCoordinator.SvgDecodeBodyForTests = null;
                release.Set();
                reading.Refresh();

                FlowDocument document = reading.Document!;
                Assert.DoesNotContain("Image not shown", TextOf(document), StringComparison.Ordinal);
                Assert.True(
                    BlocksOf(document.Blocks).Any(
                        block => block is BlockUIContainer { Child: ReadingDiagramImage { Source: not null } }),
                    "the refreshed card did not place the image: " + TextOf(document));
            }
            finally
            {
                release.Set();
                EditorInteractionCoordinator.SvgDecodeWaitForTests = null;
                EditorInteractionCoordinator.SvgDecodeBodyForTests = null;
                EditorInteractionCoordinator.SvgDecodeSlotsForTests = null;
            }
        });
    }

    private static string TextOf(FlowDocument document) =>
        new TextRange(document.ContentStart, document.ContentEnd).Text;

    private static IEnumerable<Block> BlocksOf(BlockCollection blocks)
    {
        foreach (Block block in blocks)
        {
            yield return block;
            if (block is Section section)
            {
                foreach (Block inner in BlocksOf(section.Blocks))
                {
                    yield return inner;
                }
            }
        }
    }

    private static VaultSession Scanned(string root)
    {
        VaultSession session = VaultSession.OpenFilesystem(root);
        using var cancel = new CancelToken();
        session.ScanInitial(cancel);
        return session;
    }
}
