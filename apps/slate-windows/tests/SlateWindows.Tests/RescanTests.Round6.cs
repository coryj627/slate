// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Bases;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// W7-7 PR 7, codex PR 7 round 5 (the last check, with the owner's
/// decision): every terminal outcome of an open base settles the rescan
/// that awaits it, and a base whose open or execute fails is one counted
/// error. Each fact runs the production path, with no throw at the re-sync
/// seam.
/// </summary>
public sealed partial class RescanTests
{
    /// <summary>A definition the base parses to no views: empty, without
    /// <c>views:</c>, and YAML that does not parse.</summary>
    public static TheoryData<string> ViewlessDefinitions => new()
    {
        string.Empty,
        "filters: 'file.ext == \"md\"'\n",
        "views: [unclosed\n",
    };

    /// <summary>Round 5, finding 1 (the verifier's hang): with a view-less or
    /// unparseable base open, a rescan used to wait forever — its execute
    /// failure was published without settling the run, so no sentence came,
    /// later requests joined a follow-up that never ran, and Import and
    /// Delete stayed unavailable. Now the run ends, speaks once with the base
    /// counted, and releases the structural commands — through an explicit
    /// Refresh and then a foreground rescan.</summary>
    [Theory]
    [MemberData(nameof(ViewlessDefinitions))]
    public void AViewlessBaseIsCountedAndEveryRescanEnds(string definition) => RunSta(() =>
    {
        using var h = new Harness("viewless-base", ("broken.base", definition), ("alpha.md", "# Alpha\n"), ("beta.md", "# Beta\n"));
        WorkspaceTabViewModel tab = h.Open("broken.base");
        h.PumpUntil(() => tab.Base is { State: not BaseLoadState.Loading }, "the base's first load");
        h.Sidebar.SelectedNode = Assert.Single(h.Sidebar.RootNodes, node => node.Path == "beta.md");
        h.Events.Clear();

        Task explicitRun = h.Lifecycle.RescanAsync(RescanReason.Explicit);
        h.PumpUntil(() => explicitRun.IsCompleted, "the explicit rescan ending");

        A11yEvent.VaultRescanIncomplete explicitSentence =
            Assert.IsType<A11yEvent.VaultRescanIncomplete>(Assert.Single(h.Events));
        Assert.Equal(1UL, explicitSentence.Errors);
        Assert.False(h.Lifecycle.IsRescanActive);
        Assert.True(h.Sidebar.ImportCommand.CanExecute(null));
        Assert.True(h.Sidebar.DeleteCommand.CanExecute(null));

        h.Now += TimeSpan.FromMinutes(1);
        Task foregroundRun = h.Lifecycle.RescanAsync(RescanReason.Foreground);
        h.PumpUntil(() => foregroundRun.IsCompleted, "the foreground rescan ending");

        Assert.Equal(2, h.Events.Count);
        A11yEvent.VaultRescanIncomplete foregroundSentence =
            Assert.IsType<A11yEvent.VaultRescanIncomplete>(h.Events[1]);
        Assert.Equal(1UL, foregroundSentence.Errors);
        Assert.False(h.Lifecycle.IsRescanActive);
        Assert.True(h.Sidebar.ImportCommand.CanExecute(null));
        Assert.True(h.Sidebar.DeleteCommand.CanExecute(null));
    });

    /// <summary>Round 5, finding 1 (replacing round 4's seam-thrown fact):
    /// two open bases whose re-runs really fail are two counted
    /// operations — "2 errors".</summary>
    [Fact]
    public void TwoViewlessBasesAreTwoErrors() => RunSta(() =>
    {
        using var h = new Harness("two-viewless-bases", ("One.base", string.Empty), ("Two.base", string.Empty), ("a.md", "# A\n"));
        WorkspaceTabViewModel one = h.Open("One.base");
        WorkspaceTabViewModel two = h.Open("Two.base");
        h.PumpUntil(
            () => one.Base is { State: not BaseLoadState.Loading } && two.Base is { State: not BaseLoadState.Loading },
            "both bases' first loads");
        h.Events.Clear();

        Task run = h.Lifecycle.RescanAsync(RescanReason.Explicit);
        h.PumpUntil(() => run.IsCompleted, "the rescan ending");

        A11yEvent.VaultRescanIncomplete incomplete =
            Assert.IsType<A11yEvent.VaultRescanIncomplete>(Assert.Single(h.Events));
        Assert.Equal(2UL, incomplete.Errors);
    });

    /// <summary>Round 5, finding 1 (replacing the base arm of round 28's
    /// seam-thrown fact): a base whose definition changed outside Slate and
    /// whose reopen really fails (the open's own failure, injected where the
    /// production open runs) is one counted error; the next rescan reopens it
    /// and completes.</summary>
    [Fact]
    public void ABaseWhoseReopenFailsIsCountedAndTheNextRescanCompletesIt() => RunSta(() =>
    {
        using var h = new Harness("base-reopen-fails", ("Notes.base", MainBase), ("n.md", "# N\n"));
        WorkspaceTabViewModel tab = h.Open("Notes.base");
        h.PumpUntil(() => tab.Base?.State == BaseLoadState.Ready, "the base's first load");
        BaseDocumentViewModel document = Assert.IsType<BaseDocumentViewModel>(tab.Base);
        document.BeforeOpenForTests = () => throw new VaultException.Io("injected base open failure");
        h.Write("Notes.base", MainBase.Replace("name: Main", "name: Renamed", StringComparison.Ordinal));

        Task run = h.Lifecycle.RescanAsync(RescanReason.Explicit);
        h.PumpUntil(() => run.IsCompleted, "the rescan ending");

        Assert.Equal(BaseLoadState.Failed, document.State);
        A11yEvent.VaultRescanIncomplete incomplete =
            Assert.IsType<A11yEvent.VaultRescanIncomplete>(Assert.Single(h.Events));
        Assert.Equal(1UL, incomplete.Errors);

        document.BeforeOpenForTests = null;
        h.Now += TimeSpan.FromMinutes(1);
        run = h.Lifecycle.RescanAsync(RescanReason.Explicit);
        h.PumpUntil(() => run.IsCompleted, "the next rescan ending");

        Assert.Equal(2, h.Events.Count);
        Assert.IsType<A11yEvent.VaultRescanFinished>(h.Events[1]);
        Assert.Equal(BaseLoadState.Ready, document.State);
        Assert.Equal("Renamed", document.ActiveViewName);
    });

    /// <summary>Round 5, finding 1 (a declined run): a base left Failed with
    /// its handle closed, its definition unchanged, used to decline the
    /// Bases dependent's re-run and leave the rescan waiting. The rescan now
    /// reopens it, so it recovers, and the run ends.</summary>
    [Fact]
    public void AFailedBaseWithAnUnchangedDefinitionIsReopenedAndTheRunEnds() => RunSta(() =>
    {
        using var h = new Harness("failed-base-reopened", ("Notes.base", MainBase), ("n.md", "# N\n"));
        WorkspaceTabViewModel tab = h.Open("Notes.base");
        h.PumpUntil(() => tab.Base?.State == BaseLoadState.Ready, "the base's first load");
        BaseDocumentViewModel document = Assert.IsType<BaseDocumentViewModel>(tab.Base);
        document.BeforeOpenForTests = () => throw new VaultException.Io("injected base open failure");
        document.Load();
        h.PumpUntil(() => document.State == BaseLoadState.Failed, "the failed reopen");
        document.BeforeOpenForTests = null;
        h.Events.Clear();

        Task run = h.Lifecycle.RescanAsync(RescanReason.Explicit);
        h.PumpUntil(() => run.IsCompleted, "the rescan ending");

        Assert.Equal(["Files refreshed. No changes."], h.Spoken);
        Assert.Equal(BaseLoadState.Ready, document.State);
        Assert.Equal(["n.md"], BaseRows(tab));
    });

    /// <summary>Round 5, finding 1 (replacing the base arm of round 5's
    /// seam-thrown re-seat fact): a view-less base renamed outside Slate by
    /// case only re-seats, and its load — the run's, at the stored spelling —
    /// is one counted error.</summary>
    [Fact]
    public void ACaseOnlyReseatedViewlessBaseIsCounted() => RunSta(() =>
    {
        using var h = new Harness("reseat-viewless-base", ("broken.base", string.Empty), ("a.md", "# A\n"));
        if (!h.VolumeAliasesCase())
        {
            return;
        }

        WorkspaceTabViewModel tab = h.Open("broken.base");
        h.PumpUntil(() => tab.Base is { State: not BaseLoadState.Loading }, "the base's first load");
        File.Move(Path.Combine(h.Root, "broken.base"), Path.Combine(h.Root, "Broken.base"));
        h.Events.Clear();

        Task run = h.Lifecycle.RescanAsync(RescanReason.Explicit);
        h.PumpUntil(() => run.IsCompleted, "the rescan ending");

        BaseDocumentViewModel reseated = Assert.IsType<BaseDocumentViewModel>(tab.Base);
        Assert.Equal("Broken.base", reseated.Path);
        Assert.Equal(1, reseated.OpensForTests);
        A11yEvent.VaultRescanIncomplete incomplete =
            Assert.IsType<A11yEvent.VaultRescanIncomplete>(Assert.Single(h.Events));
        Assert.Equal(1UL, incomplete.Errors);
    });
}
