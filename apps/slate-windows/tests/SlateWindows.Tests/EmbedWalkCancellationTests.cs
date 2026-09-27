// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Reading;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// #1279 (locked decision 05 §4): the embeds leaf and the reading view's
/// embed cards carry a cancellation into core, retired with the load that
/// owns it. A walk whose load was retired — the note changed, the refresh
/// was superseded — stops at core's next boundary with <c>Cancelled</c>
/// instead of finishing a result nobody will publish.
/// </summary>
public sealed class EmbedWalkCancellationTests
{
    /// <summary>A note switch while the embeds leaf's batch is inside core:
    /// the retired batch's walk ends <c>Cancelled</c> at the key it was
    /// resolving, resolves nothing more, and publishes nothing — the leaf
    /// shows the new note's (empty) embeds.</summary>
    [Fact]
    public void ANoteSwitchCancelsTheEmbedsLeafsCoreWalk()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "embed-leaf-cancel");
        File.WriteAllText(
            Path.Combine(fixture.Root, "host.md"),
            "# Host\n\n![[target]]\n\n![[second]]\n");
        File.WriteAllText(Path.Combine(fixture.Root, "target.md"), "# Target\n\nTarget body.\n");
        File.WriteAllText(Path.Combine(fixture.Root, "second.md"), "# Second\n\nSecond body.\n");
        File.WriteAllText(Path.Combine(fixture.Root, "other.md"), "# Other\n\nNo embeds here.\n");
        PumpedDispatcher.Run(() =>
        {
            using VaultSession session = ScannedSession(fixture.Root);
            var panels = new SlateWindows.Panels.RightPanePanelsViewModel(
                session,
                _ => { },
                (_, _) => true,
                _ => true,
                (_, _) => { },
                (_, _) => true,
                (_, _) => { });
            using var parked = new ManualResetEventSlim();
            using var release = new ManualResetEventSlim();
            int resolves = 0;
            panels.EmbedResolveHookForTests = () =>
            {
                if (Interlocked.Increment(ref resolves) == 1)
                {
                    parked.Set();
                    release.Wait(TimeSpan.FromSeconds(20));
                }
            };
            try
            {
                panels.NoteChanged("host.md");
                Assert.True(
                    PumpedDispatcher.PumpUntil(() => parked.IsSet),
                    "the embed batch never reached core");

                panels.NoteChanged("other.md");
                release.Set();

                Assert.True(
                    PumpedDispatcher.PumpUntil(() => panels.EmbedResolvesCancelledForTests == 1),
                    "the retired batch's core walk was not cancelled");
                Assert.True(
                    PumpedDispatcher.PumpUntil(() => !panels.IsLoadingLinks && !panels.IsResolvingEmbeds),
                    "the new note never finished loading");
                Assert.Equal(1, Volatile.Read(ref resolves));
                Assert.Equal("other.md", panels.NotePath);
                Assert.Empty(panels.Embeds);
            }
            finally
            {
                release.Set();
                panels.Shutdown();
            }
        });
    }

    /// <summary>A reading refresh superseded while its embed cards are inside
    /// core: the old walk ends <c>Cancelled</c> — not retried, not a terminal
    /// failure, not announced — and the newer refresh publishes.</summary>
    [Fact]
    public void ASupersededReadingRefreshCancelsItsEmbedWalk()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "reading-embed-cancel");
        File.WriteAllText(Path.Combine(fixture.Root, "host.md"), "# Host\n\n![[target]]\n");
        File.WriteAllText(Path.Combine(fixture.Root, "target.md"), "# Target\n\nTarget body.\n");
        PumpedDispatcher.Run(() =>
        {
            using VaultSession session = ScannedSession(fixture.Root);
            var announcements = new List<A11yEvent>();
            using var tab = new WorkspaceTabViewModel(
                session,
                new WorkspaceTabState(
                    Guid.NewGuid(),
                    new WorkspaceItemState(WorkspaceItemKind.Markdown, "host.md")),
                announce: announcements.Add,
                startInteractionBackgroundWork: true);
            tab.ToggleViewMode();
            ReadingContentViewModel reading = Assert.IsType<ReadingContentViewModel>(tab.Reading);
            Assert.True(
                PumpedDispatcher.PumpUntil(() => !reading.IsLoading && reading.Document is not null),
                "the first projection never landed");

            using var parked = new ManualResetEventSlim();
            using var release = new ManualResetEventSlim();
            int keys = 0;
            reading.EmbedFaultForTests = () =>
            {
                if (Interlocked.Increment(ref keys) == 1)
                {
                    parked.Set();
                    release.Wait(TimeSpan.FromSeconds(20));
                }
                return null;
            };
            try
            {
                reading.Refresh();
                Assert.True(
                    PumpedDispatcher.PumpUntil(() => parked.IsSet),
                    "the refresh never reached its embed walk");

                reading.Refresh();
                release.Set();

                Assert.True(
                    PumpedDispatcher.PumpUntil(() => reading.FetchesCancelledForTests == 1),
                    "the superseded refresh's core walk was not cancelled");
                Assert.True(
                    PumpedDispatcher.PumpUntil(() => !reading.IsLoading),
                    "the newer refresh never published");
                Assert.DoesNotContain(
                    announcements,
                    item => item is A11yEvent.HostComposed composed
                        && composed.Text.Contains("could not load", StringComparison.Ordinal));
            }
            finally
            {
                release.Set();
            }
        });
    }

    /// <summary>Codex round 2a: a `.base` embed card's query runs under its
    /// refresh's cancellation, not a token of its own. A refresh retired
    /// while the card's query is parked — superseded, its surface detached,
    /// or the model disposed — ends <c>Cancelled</c> at the query: it builds
    /// no card and no error card, announces nothing, and its worker returns.
    /// A superseding refresh shows the real summary card.</summary>
    [Theory]
    [InlineData("supersede")]
    [InlineData("detach")]
    [InlineData("dispose")]
    public void ARetiredRefreshCancelsItsBaseCardsQuery(string retirement)
    {
        using FixtureVault fixture = FixtureVault.Create(2, $"reading-base-cancel-{retirement}");
        File.WriteAllText(
            Path.Combine(fixture.Root, "Summary.base"),
            "filters: 'file.ext == \"md\"'\n" +
            "views:\n" +
            "  - type: table\n" +
            "    name: Main\n" +
            "    order:\n" +
            "      - file.name\n");
        File.WriteAllText(Path.Combine(fixture.Root, "host.md"), "# Host\n\n![[Summary.base]]\n");
        PumpedDispatcher.Run(() =>
        {
            using VaultSession session = ScannedSession(fixture.Root);
            var announcements = new List<A11yEvent>();
            using var tab = new WorkspaceTabViewModel(
                session,
                new WorkspaceTabState(
                    Guid.NewGuid(),
                    new WorkspaceItemState(WorkspaceItemKind.Markdown, "host.md")),
                announce: announcements.Add,
                startInteractionBackgroundWork: true);
            tab.ToggleViewMode();
            ReadingContentViewModel reading = Assert.IsType<ReadingContentViewModel>(tab.Reading);
            Assert.True(
                PumpedDispatcher.PumpUntil(() => !reading.IsLoading && reading.Document is not null),
                "the first projection never landed");
            Assert.Contains("Embedded base: Summary", DocumentText(reading), StringComparison.Ordinal);

            using var parked = new ManualResetEventSlim();
            using var release = new ManualResetEventSlim();
            int queries = 0;
            reading.BaseProjectionHookForTests = () =>
            {
                if (Interlocked.Increment(ref queries) == 1)
                {
                    parked.Set();
                    release.Wait(TimeSpan.FromSeconds(20));
                }
            };
            try
            {
                reading.Refresh();
                Assert.True(
                    PumpedDispatcher.PumpUntil(() => parked.IsSet),
                    "the refresh never reached its base card's query");

                switch (retirement)
                {
                    case "supersede": reading.Refresh(); break;
                    case "detach": reading.OnSurfaceDetached(); break;
                    case "dispose": reading.Dispose(); break;
                    default: throw new ArgumentOutOfRangeException(nameof(retirement), retirement, null);
                }
                release.Set();

                // The retired walk ended Cancelled at the base query — with
                // its own token the query would run to completion (or turn
                // Cancelled into an error card) and the walk would return a
                // result instead.
                Assert.True(
                    PumpedDispatcher.PumpUntil(() => reading.FetchesCancelledForTests == 1),
                    "the retired refresh's base query was not cancelled");
                if (retirement == "supersede")
                {
                    Assert.True(
                        PumpedDispatcher.PumpUntil(() => !reading.IsLoading),
                        "the newer refresh never published");
                    string shown = DocumentText(reading);
                    Assert.Contains("Embedded base: Summary", shown, StringComparison.Ordinal);
                    Assert.DoesNotContain("could not be executed", shown, StringComparison.Ordinal);
                }
                Assert.Equal(1, reading.FetchesCancelledForTests);
                Assert.Null(reading.LastTerminalFailureForTests);
                Assert.DoesNotContain(
                    announcements,
                    item => item is A11yEvent.HostComposed composed
                        && composed.Text.Contains("could not", StringComparison.Ordinal));
            }
            finally
            {
                release.Set();
            }
        });
    }

    /// <summary>Codex round 3: a refresh superseded AFTER its fetch finished
    /// — its publication already queued on the dispatcher, too late for any
    /// cancellation — publishes nothing and moves no state. The newer
    /// refresh, parked in its own walk, stays loading while the stale
    /// publication runs, and loads only when it publishes itself.</summary>
    [Fact]
    public void ASupersededPublicationLeavesTheNewerRefreshLoading()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "reading-stale-publication");
        File.WriteAllText(Path.Combine(fixture.Root, "host.md"), "# Host\n\n![[target]]\n");
        File.WriteAllText(Path.Combine(fixture.Root, "target.md"), "# Target\n\nTarget body.\n");
        PumpedDispatcher.Run(() =>
        {
            using VaultSession session = ScannedSession(fixture.Root);
            var announcements = new List<A11yEvent>();
            using var tab = new WorkspaceTabViewModel(
                session,
                new WorkspaceTabState(
                    Guid.NewGuid(),
                    new WorkspaceItemState(WorkspaceItemKind.Markdown, "host.md")),
                announce: announcements.Add,
                startInteractionBackgroundWork: true);
            tab.ToggleViewMode();
            ReadingContentViewModel reading = Assert.IsType<ReadingContentViewModel>(tab.Reading);
            Assert.True(
                PumpedDispatcher.PumpUntil(() => !reading.IsLoading && reading.Document is not null),
                "the first projection never landed");

            using var queued = new ManualResetEventSlim();
            using var parked = new ManualResetEventSlim();
            using var release = new ManualResetEventSlim();
            int walks = 0;
            reading.PublicationQueuedHookForTests = queued.Set;
            reading.EmbedFaultForTests = () =>
            {
                // The first refresh's walk runs through; the superseding
                // refresh's walk parks.
                if (Interlocked.Increment(ref walks) == 2)
                {
                    parked.Set();
                    release.Wait(TimeSpan.FromSeconds(20));
                }
                return null;
            };
            try
            {
                reading.Refresh();
                // The first fetch has finished and queued its publication;
                // this thread — the dispatcher — has not run it.
                Assert.True(queued.Wait(TimeSpan.FromSeconds(20)), "the first fetch never queued its publication");
                reading.Refresh();
                Assert.True(reading.IsLoading);

                // The stale publication runs in these pumps, while the newer
                // refresh is parked in its walk.
                Assert.True(
                    PumpedDispatcher.PumpUntil(() => parked.IsSet),
                    "the newer refresh never reached its walk");
                PumpedDispatcher.Drain();
                Assert.True(reading.IsLoading, "a superseded publication cleared the newer refresh's loading state");

                release.Set();
                Assert.True(
                    PumpedDispatcher.PumpUntil(() => !reading.IsLoading),
                    "the newer refresh never published");
                Assert.Null(reading.LastTerminalFailureForTests);
            }
            finally
            {
                release.Set();
            }
        });
    }

    private static string DocumentText(ReadingContentViewModel reading)
    {
        System.Windows.Documents.FlowDocument document =
            Assert.IsType<System.Windows.Documents.FlowDocument>(reading.Document);
        return new System.Windows.Documents.TextRange(document.ContentStart, document.ContentEnd).Text;
    }

    private static VaultSession ScannedSession(string root)
    {
        VaultSession session = VaultSession.OpenFilesystem(root);
        using var cancel = new CancelToken();
        session.ScanInitial(cancel);
        return session;
    }
}
