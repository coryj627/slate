// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.IO;
using System.Threading;
using System.Windows.Threading;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// Plan 44 WB-29: unsaved notes survive closing, quitting and a crash on
/// Windows. Each fact runs a real vault through the real lifecycle over its own
/// store host on a temp device root (the real core store). A store call is held
/// through <see cref="HotExitStoreHost.BeforeCallForTests"/>. "A new process" is
/// the old host's simulated death then a fresh host; a restore assertion also
/// proves the new open held the vault and read it (<see cref="AssertRead"/>), so
/// a refused or inert open can never pass for an empty store.
/// </summary>
public sealed class HotExitTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(20);

    [Fact]
    public void ADirtyTabClaimsAndWritesAtOnce()
    {
        using var host = new HotExitHost();
        host.Open();
        WorkspaceTabViewModel tab = host.OpenNote("a.md");
        var calls = new List<HotExitCallKind>();
        host.Store.BeforeCallForTests = call =>
        {
            lock (calls)
            {
                calls.Add(call.Kind);
            }
            return null;
        };

        tab.Text += "typed";

        host.PumpUntil(() => host.StoredText(tab) == tab.Text, "the first edit was never kept");
        lock (calls)
        {
            Assert.Equal(HotExitCallKind.Claim, calls[0]);
            Assert.Equal(HotExitCallKind.Write, calls[1]);
        }
    }

    [Fact]
    public void AChangedSnapshotIsWrittenAfterTheDebounce()
    {
        using var host = new HotExitHost();
        host.Open();
        WorkspaceTabViewModel tab = host.OpenNote("a.md");
        tab.Text += "one";
        host.PumpUntil(() => host.StoredText(tab) == tab.Text, "the first edit was never kept");

        tab.Text += "two";
        host.PumpFor(TimeSpan.FromMilliseconds(200));
        Assert.DoesNotContain("two", host.StoredText(tab), StringComparison.Ordinal);
        host.PumpUntil(() => host.StoredText(tab) == tab.Text, "the debounce never wrote");
        HotExitEntry entry = host.StoredEntry(tab)!;
        Assert.Equal("a.md", entry.Path);
        Assert.Equal(tab.SavedContentHash, entry.BaselineHash);
        Assert.Equal(tab.FileIdentity, entry.BaselineFileId);
        Assert.NotNull(entry.BaselineFileId);
    }

    [Fact]
    public void ACleanTabRetiresItsEntry()
    {
        using var host = new HotExitHost();
        host.Open();
        WorkspaceTabViewModel tab = host.OpenNote("a.md");
        tab.Text += "typed";
        host.PumpUntil(() => host.StoredText(tab) == tab.Text, "never kept");

        Assert.True(tab.Save());
        host.PumpUntil(() => !tab.HasPendingSaves, "the save never settled");

        host.PumpUntil(() => host.StoredEntry(tab) is null, "the clean tab kept its entry");
        Assert.Empty(host.Snapshot().Claims);
    }

    [Theory]
    [InlineData("ApplicationClose")]
    [InlineData("Switch")]
    public void AKeptCloseClosesWithoutAPrompt(string routeName)
    {
        var route = Enum.Parse<TeardownRoute>(routeName);
        using var host = new HotExitHost();
        host.Open();
        WorkspaceTabViewModel tab = host.OpenNote("a.md");
        // Typed after any debounce could have run.
        tab.Text += "kept";
        string final = tab.Text;

        Assert.Equal(WorkspaceTeardown.ClosedKept, host.Close(route));

        Assert.Equal(0, host.ClosePrompts);
        Assert.Equal("# a\n", host.Disk("a.md"));
        host.Crash();
        host.Open();
        host.AssertRead();
        WorkspaceTabViewModel restored = host.TabOn("a.md");
        Assert.True(restored.IsDirty);
        Assert.Equal(final, restored.Text);
        Assert.Equal("# a\n", host.Disk("a.md"));
        Assert.Single(host.Announced, item => item is A11yEvent.UnsavedNotesRestored { Count: 1 });
    }

    [Fact]
    public void ASettleThatCannotKeepFallsBackToThePromptWithItsReason()
    {
        using var host = new HotExitHost { ClosePrompt = () => VaultCloseDecision.Cancel };
        host.Open();
        WorkspaceTabViewModel tab = host.OpenNote("a.md");
        host.Store.BeforeCallForTests = call =>
            call.Kind == HotExitCallKind.Write ? new HotExitFault.Refused(HotExitRefusal.VaultFull) : null;
        tab.Text += "typed";

        Assert.Equal(WorkspaceTeardown.Refused, host.Close(TeardownRoute.ApplicationClose));

        Assert.Equal(1, host.ClosePrompts);
        A11yEvent.UnsavedNotesNotKept notKept =
            Assert.IsType<A11yEvent.UnsavedNotesNotKept>(
                host.Announced.Single(item => item is A11yEvent.UnsavedNotesNotKept));
        Assert.Equal(SlateUniffiMethods.HotExitRefusalReason(HotExitRefusal.VaultFull), notKept.Reason);
        Assert.Contains(notKept.Reason, host.Lifecycle.StatusText, StringComparison.Ordinal);
        Assert.Equal("# a\ntyped", tab.Text);
    }

    [Fact]
    public void CloseVaultStillAsksAndDiscardAllFencesTheStore()
    {
        using var host = new HotExitHost { ClosePrompt = () => VaultCloseDecision.Discard };
        host.Open();
        WorkspaceTabViewModel tab = host.OpenNote("a.md");
        tab.Text += "typed";
        host.PumpUntil(() => host.StoredText(tab) == tab.Text, "never kept");

        Assert.Equal(WorkspaceTeardown.ClosedChangesDiscarded, host.Close(TeardownRoute.Close));

        Assert.Equal(1, host.ClosePrompts);
        host.Crash();
        host.Open();
        host.AssertRead();
        Assert.False(host.TabOn("a.md").IsDirty);
        Assert.DoesNotContain(host.Announced, item => item is A11yEvent.UnsavedNotesRestored);
    }

    [Theory]
    [InlineData("Cancel")]
    [InlineData("CloseKeepingCopies")]
    [InlineData("TryAgain")]
    public void AnUnconfirmedRetirementOpensTheDiscardPendingPrompt(string answerName)
    {
        var answer = Enum.Parse<DiscardPendingDecision>(answerName);
        using var host = new HotExitHost();
        host.ShortenBudgets();
        host.Open();
        WorkspaceTabViewModel tab = host.OpenNote("a.md");
        tab.Text += "typed";
        host.PumpUntil(() => host.StoredText(tab) == tab.Text, "never kept");
        using var gate = new ManualResetEventSlim();
        host.Store.BeforeCallForTests = call =>
        {
            if (call.Kind == HotExitCallKind.Retire)
            {
                Assert.True(gate.Wait(Patience), "the retirement was never released");
            }
            return null;
        };
        Assert.True(tab.Save());
        host.PumpUntil(() => !tab.HasPendingSaves, "the save never settled");
        host.DiscardPrompt = () =>
        {
            if (answer == DiscardPendingDecision.TryAgain)
            {
                gate.Set();
            }
            return answer;
        };

        WorkspaceTeardown outcome = host.Close(TeardownRoute.ApplicationClose);

        Assert.Equal(1, host.DiscardPrompts);
        switch (answer)
        {
            case DiscardPendingDecision.Cancel:
                Assert.Equal(WorkspaceTeardown.Refused, outcome);
                gate.Set();
                break;
            case DiscardPendingDecision.TryAgain:
                Assert.Equal(WorkspaceTeardown.Closed, outcome);
                host.Crash();
                host.Open();
                host.AssertRead();
                Assert.False(host.TabOn("a.md").IsDirty);
                break;
            case DiscardPendingDecision.CloseKeepingCopies:
                Assert.Equal(WorkspaceTeardown.ClosedCopiesKept, outcome);
                host.NewProcess();
                gate.Set();
                host.Open();
                host.AssertRead();
                Assert.True(host.TabOn("a.md").IsDirty, "the copy did not come back");
                break;
        }
    }

    [Fact]
    public void CloseAndKeepTheCopiesAfterAFailedDiscard()
    {
        using var host = new HotExitHost
        {
            ClosePrompt = () => VaultCloseDecision.Discard,
            DiscardPrompt = () => DiscardPendingDecision.CloseKeepingCopies,
        };
        host.ShortenBudgets();
        host.Open();
        WorkspaceTabViewModel tab = host.OpenNote("a.md");
        tab.Text += "typed";
        string final = tab.Text;
        host.PumpUntil(() => host.StoredText(tab) == final, "never kept");
        using var gate = new ManualResetEventSlim();
        host.Store.BeforeCallForTests = call =>
        {
            if (call.Kind == HotExitCallKind.Discard)
            {
                return new HotExitFault.Io();
            }
            if (call.Kind == HotExitCallKind.Retire)
            {
                Assert.True(gate.Wait(Patience), "the retirement was never released");
            }
            return null;
        };

        Assert.Equal(WorkspaceTeardown.ClosedCopiesKept, host.Close(TeardownRoute.Close));

        Assert.Equal(1, host.DiscardPrompts);
        host.NewProcess();
        gate.Set();
        host.Open();
        host.AssertRead();
        WorkspaceTabViewModel restored = host.TabOn("a.md");
        Assert.True(restored.IsDirty);
        Assert.Equal(final, restored.Text);
    }

    [Theory]
    [InlineData("ApplicationClose")]
    [InlineData("Switch")]
    public void ALateDiscardNeverReKeepsTheTextItDiscarded(string routeName)
    {
        var route = Enum.Parse<TeardownRoute>(routeName);
        using var host = new HotExitHost
        {
            ClosePrompt = () => VaultCloseDecision.Discard,
            DiscardPrompt = () => DiscardPendingDecision.Cancel,
        };
        host.ShortenBudgets();
        host.Open();
        WorkspaceTabViewModel tab = host.OpenNote("a.md");
        tab.Text += "typed";
        host.PumpUntil(() => host.StoredText(tab) == tab.Text, "never kept");
        using var gate = new ManualResetEventSlim();
        // A close asks only when the settle cannot keep: the store refuses the
        // next write, so the fallback prompt's Discard All runs, and is held.
        host.Store.BeforeCallForTests = call =>
        {
            if (call.Kind == HotExitCallKind.Write)
            {
                return new HotExitFault.Refused(HotExitRefusal.VaultFull);
            }
            if (call.Kind == HotExitCallKind.Discard)
            {
                Assert.True(gate.Wait(Patience), "the discard was never released");
            }
            return null;
        };
        tab.Text += "more";
        Assert.Equal(WorkspaceTeardown.Refused, host.Close(route));
        int asked = host.ClosePrompts;
        Assert.Equal(1, asked);
        Assert.Equal(1, host.DiscardPrompts);

        gate.Set();
        host.PumpUntil(() => !host.Store.HasUnresolved(host.Key), "the discard never resolved");
        host.PumpFor(TimeSpan.FromMilliseconds(100));

        // The next close asks, with core's discarded reason, never ClosedKept.
        host.ClosePrompt = () => VaultCloseDecision.Cancel;
        Assert.Equal(WorkspaceTeardown.Refused, host.Close(route));
        Assert.Equal(asked + 1, host.ClosePrompts);
        Assert.Contains(host.Announced, item => item is A11yEvent.UnsavedNotesNotKept notKept
            && notKept.Reason == SlateUniffiMethods.HotExitReasonDiscarded());
        host.Crash();
        host.Open();
        host.AssertRead();
        Assert.False(host.TabOn("a.md").IsDirty, "the discarded text came back");
    }

    [Fact]
    public void EveryStoreCallIsRecordedBeforeItIsIssued()
    {
        using var host = new HotExitHost();
        host.Open();
        WorkspaceTabViewModel tab = host.OpenNote("a.md");
        int checkedCalls = 0;
        host.Store.BeforeCallForTests = _ =>
        {
            Assert.True(host.Store.HasUnresolved(host.Key), "a call ran with no obligation");
            Interlocked.Increment(ref checkedCalls);
            return null;
        };
        tab.Text += "typed";
        host.PumpUntil(() => host.StoredText(tab) == tab.Text, "never kept");
        Assert.True(Volatile.Read(ref checkedCalls) >= 2);
    }

    [Fact]
    public void ALateClaimNeverDisplacesANewerEpisode()
    {
        using var host = new HotExitHost();
        host.Open();
        WorkspaceTabViewModel tab = host.OpenNote("a.md");
        string first = tab.Text;
        using var gate = new ManualResetEventSlim();
        var order = new List<HotExitCallKind>();
        int claims = 0;
        host.Store.BeforeCallForTests = call =>
        {
            lock (order)
            {
                order.Add(call.Kind);
            }
            if (call.Kind == HotExitCallKind.Claim && Interlocked.Increment(ref claims) == 1)
            {
                Assert.True(gate.Wait(Patience), "C1 was never released");
            }
            return null;
        };
        tab.Text = first + "x";
        host.PumpUntil(() => Volatile.Read(ref claims) == 1, "C1 never started");
        tab.Text = first;
        host.PumpUntil(() => !tab.IsDirty, "the tab never turned clean");
        host.PumpFor(TimeSpan.FromMilliseconds(50));
        tab.Text = first + "second";
        host.PumpFor(TimeSpan.FromMilliseconds(100));
        Assert.Equal(1, Volatile.Read(ref claims));

        gate.Set();
        string final = tab.Text;
        host.PumpUntil(() => host.StoredText(tab) == final, "C2 never kept");
        lock (order)
        {
            int retire = order.IndexOf(HotExitCallKind.Retire);
            int secondClaim = order.LastIndexOf(HotExitCallKind.Claim);
            Assert.True(retire > 0 && retire < secondClaim, string.Join(",", order));
        }
        Assert.Equal(WorkspaceTeardown.ClosedKept, host.Close(TeardownRoute.ApplicationClose));
        host.Crash();
        host.Open();
        host.AssertRead();
        Assert.Equal(final, host.TabOn("a.md").Text);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AClaimForAReaderEndedEpisodeIsRetired(bool claimErrorsAfterTheDisk)
    {
        using var host = new HotExitHost();
        host.Open();
        WorkspaceTabViewModel tab = host.OpenNote("a.md");
        string baseline = tab.Text;
        using var gate = new ManualResetEventSlim();
        int claims = 0;
        host.Store.BeforeCallForTests = call =>
        {
            if (call.Kind == HotExitCallKind.Claim && Interlocked.Increment(ref claims) == 1)
            {
                Assert.True(gate.Wait(Patience), "the claim was never released");
            }
            return null;
        };
        host.Store.AfterCallForTests = call =>
            claimErrorsAfterTheDisk && call.Kind == HotExitCallKind.Claim ? new HotExitFault.Io() : null;
        tab.Text = baseline + "x";
        host.PumpUntil(() => Volatile.Read(ref claims) == 1, "the claim never started");
        tab.Text = baseline;
        host.PumpFor(TimeSpan.FromMilliseconds(100));
        gate.Set();
        host.PumpUntil(() => !host.Store.HasUnresolved(host.Key), "never resolved");
        Assert.Empty(host.Snapshot().Claims);
        host.Crash();
        host.Open();
        host.AssertRead();
        Assert.False(host.TabOn("a.md").IsDirty);
    }

    [Fact]
    public void AClaimForAnAbandonedEpisodeIsKept()
    {
        using var host = new HotExitHost();
        host.Open();
        WorkspaceTabViewModel tab = host.OpenNote("a.md");
        tab.Text += "typed";
        string typed = tab.Text;
        host.PumpUntil(() => host.StoredText(tab) == typed, "never kept");
        host.Crash();
        // The adopting claim lands; the crash shape comes before its write.
        host.Store.BeforeCallForTests = call =>
            call.Kind == HotExitCallKind.Write ? new HotExitFault.Io() : null;
        host.Open();
        host.AssertRead();
        host.PumpUntil(
            () => host.Snapshot().Claims.Length == 1
                && host.Snapshot().Claims[0].Incarnation != host.Snapshot().Entries[0].Incarnation,
            "the adopting claim never landed");
        host.Store.BeforeCallForTests = null;
        host.NewProcess();
        host.Open();
        host.AssertRead();
        WorkspaceTabViewModel again = host.TabOn("a.md");
        Assert.Equal(typed, again.Text);

        // Adopted again and discarded by the reader: nothing comes back.
        host.Workspace.OpenPath("b.md", WorkspaceOpenTarget.NewTab);
        host.TabPrompt = _ => WorkspaceDirtyNavigationDecision.Discard;
        host.CloseTab(again);
        host.PumpUntil(() => !host.Store.HasUnresolved(host.Key), "never resolved");
        host.NewProcess();
        host.Open();
        host.AssertRead();
        Assert.Empty(host.Snapshot().Entries);
        Assert.DoesNotContain(host.Announced, item => item is A11yEvent.UnsavedNotesRestored);
    }

    [Fact]
    public void ARetriedRetirementNeverDisplacesASuccessor()
    {
        using var host = new HotExitHost();
        host.Store.RetirementRetryDelay = TimeSpan.FromHours(1);
        host.Open();
        WorkspaceTabViewModel tab = host.OpenNote("a.md");
        tab.Text += "restored";
        string restored = tab.Text;
        host.PumpUntil(() => host.StoredText(tab) == restored, "never kept");
        host.Crash();
        host.Store.RetirementRetryDelay = TimeSpan.FromHours(1);
        host.Open();
        WorkspaceTabViewModel back = host.TabOn("a.md");
        host.PumpUntil(() => !host.Store.HasUnresolved(host.Key), "the adoption never resolved");
        // The outage: the retirement of a Discard by navigation errors.
        bool outage = true;
        host.Store.BeforeCallForTests = call =>
            Volatile.Read(ref outage) && call.Kind == HotExitCallKind.Retire ? new HotExitFault.Io() : null;
        host.TabPrompt = _ => WorkspaceDirtyNavigationDecision.Discard;
        host.Workspace.OpenPath("b.md");
        host.PumpUntil(() => back.Path == "b.md", "the tab never re-pointed");
        host.PumpFor(TimeSpan.FromMilliseconds(100));
        Assert.True(host.Store.HasUnresolved(host.Key), "premise: the retirement is parked");
        Volatile.Write(ref outage, false);

        // The next call for the entry retries it first; C is then kept.
        back.Text += "successor";
        string successor = back.Text;
        host.PumpUntil(() => host.StoredText(back) == successor, "the successor was never kept");
        host.Crash();
        host.Open();
        host.AssertRead();
        Assert.Equal(successor, host.TabOn("b.md").Text);
        Assert.DoesNotContain(
            host.Workspace.Groups.SelectMany(group => group.Tabs),
            candidate => candidate.Text == restored);
    }

    [Fact]
    public void AnErroredClaimIsRetriedWithItsOwnIncarnation()
    {
        using var host = new HotExitHost();
        host.Open();
        WorkspaceTabViewModel tab = host.OpenNote("a.md");
        var incarnations = new List<string>();
        int claims = 0;
        host.Store.AfterCallForTests = call =>
        {
            if (call.Kind != HotExitCallKind.Claim)
            {
                return null;
            }
            lock (incarnations)
            {
                incarnations.Add(call.Incarnations[0]);
            }
            return Interlocked.Increment(ref claims) == 1 ? new HotExitFault.Io() : null;
        };
        tab.Text += "one";
        host.PumpUntil(() => Volatile.Read(ref claims) == 1, "the claim never ran");
        host.PumpFor(TimeSpan.FromMilliseconds(100));
        tab.Text += "two";
        string final = tab.Text;
        host.PumpUntil(() => host.StoredText(tab) == final, "never kept after the retry");
        lock (incarnations)
        {
            Assert.Equal(2, incarnations.Count);
            Assert.Equal(incarnations[0], incarnations[1]);
        }
    }

    [Fact]
    public void AnAdoptingClaimTakenByAnotherClaimIsNotKept()
    {
        using var host = new HotExitHost { ClosePrompt = () => VaultCloseDecision.Cancel };
        host.Open();
        WorkspaceTabViewModel tab = host.OpenNote("a.md");
        tab.Text += "typed";
        string typed = tab.Text;
        host.PumpUntil(() => host.StoredText(tab) == typed, "never kept");
        string id = tab.RecoveryEntryId;
        host.Crash();
        // Between the reopen's read and its adopting claim, another claim.
        int claims = 0;
        host.Store.BeforeCallForTests = call =>
        {
            if (call.Kind == HotExitCallKind.Claim && Interlocked.Increment(ref claims) == 1)
            {
                using HotExitStore direct = HotExitStore.Open(host.DeviceRoot);
                HotExitSnapshot now = direct.ReadBuffers(host.Key);
                _ = direct.ClaimEntry(
                    host.Key, now.Epoch, id, now.Claims[0].Incarnation, SlateUniffiMethods.NewHotExitIncarnation());
            }
            return null;
        };
        host.Open();
        host.PumpUntil(() => !host.Store.HasUnresolved(host.Key), "never resolved");
        host.PumpFor(TimeSpan.FromMilliseconds(50));

        Assert.Equal(WorkspaceTeardown.Refused, host.Close(TeardownRoute.ApplicationClose));
        Assert.Contains(host.Announced, item => item is A11yEvent.UnsavedNotesNotKept notKept
            && notKept.Reason == SlateUniffiMethods.HotExitReasonTakenOver());
        Assert.Single(host.Snapshot().Claims);
        Assert.Equal(typed, host.Snapshot().Entries[0].Text);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AClaimWithNoPayloadNeverWedgesItsTab(bool aTabHoldsTheId)
    {
        using var host = new HotExitHost();
        host.Open();
        WorkspaceTabViewModel tab = host.OpenNote("a.md");
        string id = tab.RecoveryEntryId;
        host.Store.BeforeCallForTests = call =>
            call.Kind == HotExitCallKind.Write ? new HotExitFault.Io() : null;
        tab.Text += "lost";
        host.PumpUntil(() => host.Snapshot().Claims.Length == 1, "the claim never landed");
        Assert.Empty(host.Snapshot().Entries);
        host.Crash();
        host.Store.BeforeCallForTests = null;
        if (!aTabHoldsTheId)
        {
            host.ForgetLayout();
        }
        host.Open();
        host.AssertRead();
        if (aTabHoldsTheId)
        {
            WorkspaceTabViewModel back = host.TabOn("a.md");
            Assert.Equal(id, back.RecoveryEntryId);
            back.Text += "again";
            string final = back.Text;
            host.PumpUntil(() => host.StoredText(back) == final, "the tab stayed wedged");
        }
        else
        {
            host.PumpUntil(() => host.Snapshot().Claims.Length == 0, "the orphan was never retired");
        }
    }

    [Fact]
    public void AnOldClaimsLateOutcomeNeverCountsForANewOne()
    {
        using var host = new HotExitHost();
        host.Open();
        WorkspaceTabViewModel tab = host.OpenNote("a.md");
        string baseline = tab.Text;
        using var gate = new ManualResetEventSlim();
        int writes = 0;
        host.Store.BeforeCallForTests = call =>
        {
            if (call.Kind == HotExitCallKind.Write && Interlocked.Increment(ref writes) == 1)
            {
                Assert.True(gate.Wait(Patience), "the old write was never released");
            }
            return null;
        };
        tab.Text = baseline + "old";
        host.PumpUntil(() => Volatile.Read(ref writes) == 1, "the old write never started");
        tab.Text = baseline;
        host.PumpFor(TimeSpan.FromMilliseconds(100));
        tab.Text = baseline + "new";
        string final = tab.Text;
        host.PumpUntil(() => host.StoredText(tab) == final, "the new claim's write never landed");
        gate.Set();
        host.PumpUntil(() => !host.Store.HasUnresolved(host.Key), "never resolved");
        host.PumpFor(TimeSpan.FromMilliseconds(100));
        Assert.Equal(final, host.StoredText(tab));
        Assert.Equal(WorkspaceTeardown.ClosedKept, host.Close(TeardownRoute.ApplicationClose));
    }

    [Fact]
    public void ASaveNeverWaitsOnARecoveryWrite()
    {
        using var host = new HotExitHost();
        host.Open();
        WorkspaceTabViewModel tab = host.OpenNote("a.md");
        using var gate = new ManualResetEventSlim();
        int writes = 0;
        host.Store.BeforeCallForTests = call =>
        {
            if (call.Kind == HotExitCallKind.Write && Interlocked.Increment(ref writes) == 1)
            {
                Assert.True(gate.Wait(Patience), "the write was never released");
            }
            return null;
        };
        tab.Text += "typed";
        host.PumpUntil(() => Volatile.Read(ref writes) == 1, "the write never started");
        Assert.True(tab.Save());
        host.PumpUntil(() => !tab.HasPendingSaves, "the save waited on the recovery write");
        Assert.Equal(tab.Text, host.Disk("a.md"));
        gate.Set();
        host.PumpUntil(() => !host.Store.HasUnresolved(host.Key), "never resolved");
    }

    [Theory]
    [InlineData("Committed")]
    [InlineData("FailedUnchanged")]
    [InlineData("FailedAdvanced")]
    public void ARefusedDiscardKeepsTheWriterFencedUntilItResolves(string resolution)
    {
        using var host = new HotExitHost
        {
            ClosePrompt = () => VaultCloseDecision.Discard,
            DiscardPrompt = () => DiscardPendingDecision.Cancel,
        };
        host.ShortenBudgets();
        host.Open();
        WorkspaceTabViewModel covered = host.OpenNote("a.md");
        covered.Text += "covered";
        host.Workspace.OpenPath("b.md", WorkspaceOpenTarget.NewTab);
        PumpedDispatcher.Drain();
        WorkspaceTabViewModel edited = host.TabOn("b.md");
        edited.Text += "edited";
        host.PumpUntil(() => host.StoredText(edited) == edited.Text, "never kept");
        using var gate = new ManualResetEventSlim();
        host.Store.BeforeCallForTests = call =>
        {
            if (call.Kind != HotExitCallKind.Discard)
            {
                return null;
            }
            Assert.True(gate.Wait(Patience), "the discard was never released");
            return resolution == "FailedUnchanged" ? new HotExitFault.Io() : null;
        };
        host.Store.AfterCallForTests = call =>
            resolution == "FailedAdvanced" && call.Kind == HotExitCallKind.Discard ? new HotExitFault.Io() : null;

        Assert.Equal(WorkspaceTeardown.Refused, host.Close(TeardownRoute.Close));
        Assert.Equal(1, host.DiscardPrompts);
        edited.Text += "more";
        string final = edited.Text;
        host.PumpFor(TimeSpan.FromMilliseconds(100));
        Assert.NotEqual(final, host.StoredText(edited));

        gate.Set();
        host.PumpUntil(() => !host.Store.HasUnresolved(host.Key), "never resolved");
        host.PumpUntil(() => host.StoredText(edited) == final, "the later edit was never kept");
        Assert.Null(host.StoredEntry(covered));
    }

    [Fact]
    public void ASecondDiscardAllWaitsForTheOneInFlight()
    {
        using var host = new HotExitHost
        {
            ClosePrompt = () => VaultCloseDecision.Discard,
            DiscardPrompt = () => DiscardPendingDecision.Cancel,
        };
        host.ShortenBudgets();
        host.Open();
        WorkspaceTabViewModel tab = host.OpenNote("a.md");
        tab.Text += "typed";
        using var gate = new ManualResetEventSlim();
        int discards = 0;
        host.Store.BeforeCallForTests = call =>
        {
            if (call.Kind == HotExitCallKind.Discard)
            {
                Interlocked.Increment(ref discards);
                Assert.True(gate.Wait(Patience), "the discard was never released");
            }
            return null;
        };
        Assert.Equal(WorkspaceTeardown.Refused, host.Close(TeardownRoute.Close));
        Assert.Equal(WorkspaceTeardown.Refused, host.Close(TeardownRoute.Close));
        Assert.Equal(1, Volatile.Read(ref discards));
        gate.Set();
        host.PumpUntil(() => !host.Store.HasUnresolved(host.Key), "never resolved");
    }

    [Fact]
    public void TryAgainAfterADiscardThatAdvancedAndFailed()
    {
        using var host = new HotExitHost
        {
            ClosePrompt = () => VaultCloseDecision.Discard,
            DiscardPrompt = () => DiscardPendingDecision.TryAgain,
        };
        host.Open();
        WorkspaceTabViewModel tab = host.OpenNote("a.md");
        tab.Text += "typed";
        host.PumpUntil(() => host.StoredText(tab) == tab.Text, "never kept");
        int discards = 0;
        host.Store.AfterCallForTests = call =>
            call.Kind == HotExitCallKind.Discard && Interlocked.Increment(ref discards) == 1
                ? new HotExitFault.Io()
                : null;

        Assert.Equal(WorkspaceTeardown.ClosedChangesDiscarded, host.Close(TeardownRoute.Close));

        Assert.Equal(2, Volatile.Read(ref discards));
        Assert.Equal(1, host.DiscardPrompts);
        host.Crash();
        host.Open();
        host.AssertRead();
        Assert.False(host.TabOn("a.md").IsDirty);
    }

    [Fact]
    public void ADiscardWaitNeverDeadlocksTheDispatcher()
    {
        using var host = new HotExitHost { ClosePrompt = () => VaultCloseDecision.Discard };
        host.Open();
        WorkspaceTabViewModel tab = host.OpenNote("a.md");
        tab.Text += "typed";
        DateTime began = DateTime.UtcNow;
        Assert.Equal(WorkspaceTeardown.ClosedChangesDiscarded, host.Close(TeardownRoute.Close));
        Assert.True(DateTime.UtcNow - began < TimeSpan.FromSeconds(3), "the wait ran to its budget");
    }

    [Fact]
    public void TwoDirtyPeersAreTwoEntries()
    {
        using var host = new HotExitHost();
        host.Open();
        WorkspaceTabViewModel left = host.OpenNote("a.md");
        host.Workspace.OpenPath("a.md", WorkspaceOpenTarget.SplitRight);
        PumpedDispatcher.Drain();
        WorkspaceTabViewModel right = host.Workspace.Groups.SelectMany(group => group.Tabs)
            .First(tab => tab.Path == "a.md" && !ReferenceEquals(tab, left));
        left.Text += "typed";
        host.PumpUntil(() => host.Snapshot().Entries.Length == 2, "peers are not two entries");
        host.TabPrompt = _ => WorkspaceDirtyNavigationDecision.Discard;
        host.CloseTab(right);
        host.PumpUntil(() => host.Snapshot().Entries.Length == 1, "the discard kept the peer's entry");
        Assert.Equal(left.RecoveryEntryId, host.Snapshot().Entries[0].EntryId);
    }

    [Fact]
    public void AWriteThatLandsAfterItsTabTurnedCleanIsRetired()
    {
        using var host = new HotExitHost();
        host.Open();
        WorkspaceTabViewModel tab = host.OpenNote("a.md");
        tab.Text += "one";
        host.PumpUntil(() => host.StoredText(tab) == tab.Text, "never kept");
        using var gate = new ManualResetEventSlim();
        int writes = 0;
        host.Store.BeforeCallForTests = call =>
        {
            if (call.Kind == HotExitCallKind.Write && Interlocked.Increment(ref writes) == 1)
            {
                Assert.True(gate.Wait(Patience), "the write was never released");
            }
            return null;
        };
        tab.Text += "two";
        host.PumpUntil(() => Volatile.Read(ref writes) == 1, "the write never started");
        Assert.True(tab.Save());
        host.PumpUntil(() => !tab.HasPendingSaves && !tab.IsDirty, "the save never landed");
        host.PumpUntil(() => host.StoredEntry(tab) is null, "the retirement never confirmed");
        gate.Set();
        host.PumpFor(TimeSpan.FromMilliseconds(200));
        host.Crash();
        host.Open();
        host.AssertRead();
        Assert.False(host.TabOn("a.md").IsDirty);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SigningOutFlushesWithoutAPrompt(bool storeWorks)
    {
        using var host = new HotExitHost { ClosePrompt = () => VaultCloseDecision.Cancel };
        host.Open();
        WorkspaceTabViewModel tab = host.OpenNote("a.md");
        tab.Text += "one";
        host.PumpUntil(() => host.StoredText(tab) == tab.Text, "never kept");
        if (!storeWorks)
        {
            host.Store.BeforeCallForTests = call =>
                call.Kind == HotExitCallKind.Write ? new HotExitFault.Io() : null;
        }
        tab.Text += "inside the debounce";
        string final = tab.Text;

        host.Lifecycle.FlushHotExitForSessionEnd();
        Assert.Equal(0, host.ClosePrompts);
        if (storeWorks)
        {
            Assert.Equal(final, host.StoredText(tab));
        }

        // WPF's own shutdown then raises the window's Closing.
        WorkspaceTeardown closing = host.Close(TeardownRoute.ApplicationClose);
        Assert.Equal(storeWorks ? WorkspaceTeardown.ClosedKept : WorkspaceTeardown.Refused, closing);
        Assert.Equal(storeWorks ? 0 : 1, host.ClosePrompts);
    }

    [Fact]
    public void ABaselineChangeIsWrittenAtOnce()
    {
        using var host = new HotExitHost();
        var delay = HotExitWriter.DebounceDelay;
        host.Open();
        WorkspaceTabViewModel tab = host.OpenNote("a.md");
        tab.Text += "typed";
        host.PumpUntil(() => host.StoredText(tab) == tab.Text, "never kept");
        File.Move(Path.Combine(host.Root, "a.md"), Path.Combine(host.Root, "c.md"));
        tab.RetargetPath("c.md");
        host.PumpUntil(
            () => host.StoredEntry(tab)?.Path == "c.md",
            "the new path was not written");
        Assert.Equal(delay, HotExitWriter.DebounceDelay);
    }

    [Fact]
    public void CloseVaultThatKeepsTheCopiesSpeaksOneCloseLine()
    {
        using var host = new HotExitHost { DiscardPrompt = () => DiscardPendingDecision.CloseKeepingCopies };
        host.ShortenBudgets();
        host.Open();
        WorkspaceTabViewModel tab = host.OpenNote("a.md");
        tab.Text += "typed";
        host.PumpUntil(() => host.StoredText(tab) == tab.Text, "never kept");
        using var gate = new ManualResetEventSlim();
        host.Store.BeforeCallForTests = call =>
        {
            if (call.Kind == HotExitCallKind.Retire)
            {
                Assert.True(gate.Wait(Patience), "the retirement was never released");
            }
            return null;
        };
        Assert.True(tab.Save());
        host.PumpUntil(() => !tab.HasPendingSaves, "the save never settled");
        host.Announced.Clear();

        host.Lifecycle.CloseVault();

        Assert.Single(host.Announced, item => item is A11yEvent.VaultClosed);
        gate.Set();
    }

    [Fact]
    public void AVaultHeldByAnotherProcessHasNoHotExit()
    {
        using var host = new HotExitHost { ClosePrompt = () => VaultCloseDecision.Cancel };
        host.Open();
        WorkspaceTabViewModel tab = host.OpenNote("a.md");
        tab.Text += "held";
        string held = tab.Text;
        host.PumpUntil(() => host.StoredText(tab) == held, "never kept");
        host.ForgetLifecycle();
        HotExitStoreHost holder = host.Store;

        // A second process's host: the first still holds the vault.
        host.UseStore(HotExitStoreHost.Install(host.DeviceRoot, static _ => true));
        host.Open();
        Assert.True(host.Workspace.HotExit!.IsInertForTests);
        Assert.DoesNotContain(host.Announced, item => item is A11yEvent.UnsavedNotesRestored);
        WorkspaceTabViewModel second = host.TabOn("a.md");
        second.Text += "unkept";
        Assert.Equal(WorkspaceTeardown.Refused, host.Close(TeardownRoute.ApplicationClose));
        Assert.Contains(host.Announced, item => item is A11yEvent.UnsavedNotesNotKept notKept
            && notKept.Reason == SlateUniffiMethods.HotExitReasonHeldElsewhere());
        Assert.Equal(held, host.Snapshot().Entries[0].Text);

        // Once the holder dies a reopen holds it and restores the text.
        host.DiscardEverything();
        holder.SimulateProcessDeathForTests();
        host.UseStore(HotExitStoreHost.Install(host.DeviceRoot, static _ => true));
        host.Open();
        host.AssertRead();
        Assert.Equal(held, host.TabOn("a.md").Text);
    }

    [Fact]
    public void AnErroredRetirementLeftByCloseAndKeepNeverDisablesTheNextOpen()
    {
        using var host = new HotExitHost { DiscardPrompt = () => DiscardPendingDecision.CloseKeepingCopies };
        host.ShortenBudgets();
        host.Store.RetirementRetryDelay = TimeSpan.FromMilliseconds(50);
        host.Open();
        WorkspaceTabViewModel tab = host.OpenNote("a.md");
        tab.Text += "typed";
        host.PumpUntil(() => host.StoredText(tab) == tab.Text, "never kept");
        bool outage = true;
        host.Store.BeforeCallForTests = call =>
            Volatile.Read(ref outage) && call.Kind == HotExitCallKind.Retire ? new HotExitFault.Io() : null;
        Assert.True(tab.Save());
        host.PumpUntil(() => !tab.HasPendingSaves, "the save never settled");
        Assert.Equal(WorkspaceTeardown.ClosedCopiesKept, host.Close(TeardownRoute.ApplicationClose));
        host.Crash();
        Volatile.Write(ref outage, false);
        // With no workspace the host's own retry confirms it.
        host.PumpUntil(() => !host.Store.HasUnresolved(host.Key), "the retirement never retried itself");
        host.Open();
        host.AssertRead();
        Assert.False(host.TabOn("a.md").IsDirty);
    }

    [Fact]
    public void ADiscardedUnkeptTabEditedLaterIsKept()
    {
        using var host = new HotExitHost
        {
            ClosePrompt = () => VaultCloseDecision.Discard,
            DiscardPrompt = () => DiscardPendingDecision.Cancel,
        };
        host.ShortenBudgets();
        host.Open();
        WorkspaceTabViewModel tab = host.OpenNote("a.md");
        tab.Text += "typed";
        using var gate = new ManualResetEventSlim();
        host.Store.BeforeCallForTests = call =>
        {
            if (call.Kind == HotExitCallKind.Discard)
            {
                Assert.True(gate.Wait(Patience), "the discard was never released");
            }
            return null;
        };
        Assert.Equal(WorkspaceTeardown.Refused, host.Close(TeardownRoute.Close));
        gate.Set();
        host.PumpUntil(() => !host.Store.HasUnresolved(host.Key), "never resolved");
        host.PumpFor(TimeSpan.FromMilliseconds(100));
        Assert.Null(host.StoredEntry(tab));

        tab.Text += "later";
        string final = tab.Text;
        host.PumpUntil(() => host.StoredText(tab) == final, "the later edit was never kept");
    }

    [Fact]
    public void ACoveredTabSavedWhileTheDiscardIsPendingStaysRetired()
    {
        using var host = new HotExitHost
        {
            ClosePrompt = () => VaultCloseDecision.Discard,
            DiscardPrompt = () => DiscardPendingDecision.Cancel,
        };
        host.ShortenBudgets();
        host.Open();
        WorkspaceTabViewModel tab = host.OpenNote("a.md");
        tab.Text += "typed";
        host.PumpUntil(() => host.StoredText(tab) == tab.Text, "never kept");
        using var gate = new ManualResetEventSlim();
        host.Store.BeforeCallForTests = call =>
        {
            if (call.Kind != HotExitCallKind.Discard)
            {
                return null;
            }
            Assert.True(gate.Wait(Patience), "the discard was never released");
            return new HotExitFault.Io();
        };
        Assert.Equal(WorkspaceTeardown.Refused, host.Close(TeardownRoute.Close));
        Assert.True(tab.Save());
        host.PumpUntil(() => !tab.HasPendingSaves, "the save never settled");
        gate.Set();
        host.PumpUntil(() => !host.Store.HasUnresolved(host.Key), "never resolved");
        host.NewProcess();
        host.Open();
        host.AssertRead();
        Assert.False(host.TabOn("a.md").IsDirty, "the saved note's old copy came back");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AnEpochMovedElsewhereNeverReKeepsUnchangedText(bool writeInFlight)
    {
        using var host = new HotExitHost { ClosePrompt = () => VaultCloseDecision.Cancel };
        host.Open();
        WorkspaceTabViewModel tab = host.OpenNote("a.md");
        tab.Text += "typed";
        host.PumpUntil(() => host.StoredText(tab) == tab.Text, "never kept");
        using var gate = new ManualResetEventSlim();
        if (writeInFlight)
        {
            int writes = 0;
            host.Store.BeforeCallForTests = call =>
            {
                if (call.Kind == HotExitCallKind.Write && Interlocked.Increment(ref writes) == 1)
                {
                    Assert.True(gate.Wait(Patience), "the write was never released");
                }
                return null;
            };
            tab.Text += "more";
            host.PumpUntil(() => Volatile.Read(ref writes) == 1, "the write never started");
        }
        using (HotExitStore direct = HotExitStore.Open(host.DeviceRoot))
        {
            _ = direct.DiscardVault(host.Key);
        }
        gate.Set();

        Assert.Equal(WorkspaceTeardown.Refused, host.Close(TeardownRoute.ApplicationClose));
        Assert.Contains(host.Announced, item => item is A11yEvent.UnsavedNotesNotKept notKept
            && notKept.Reason == SlateUniffiMethods.HotExitReasonTakenOver());
        Assert.Null(host.StoredEntry(tab));

        tab.Text += "after";
        string final = tab.Text;
        host.PumpUntil(() => host.StoredText(tab) == final, "the edit after it was never kept");
    }

    [Fact]
    public void AReopenWaitsForTheLastSessionsObligations()
    {
        using var host = new HotExitHost
        {
            DiscardPrompt = () => DiscardPendingDecision.CloseKeepingCopies,
            ClosePrompt = () => VaultCloseDecision.Cancel,
        };
        host.ShortenBudgets();
        host.Open();
        WorkspaceTabViewModel tab = host.OpenNote("a.md");
        tab.Text += "typed";
        host.PumpUntil(() => host.StoredText(tab) == tab.Text, "never kept");
        using var gate = new ManualResetEventSlim();
        host.Store.BeforeCallForTests = call =>
        {
            if (call.Kind == HotExitCallKind.Retire)
            {
                Assert.True(gate.Wait(Patience), "the retirement was never released");
            }
            return null;
        };
        Assert.True(tab.Save());
        host.PumpUntil(() => !tab.HasPendingSaves, "the save never settled");
        Assert.Equal(WorkspaceTeardown.ClosedCopiesKept, host.Close(TeardownRoute.ApplicationClose));
        host.Crash();

        // Held past the wait: no hot exit that session.
        host.Open();
        Assert.True(host.Workspace.HotExit!.IsInertForTests);
        Assert.DoesNotContain(host.Announced, item => item is A11yEvent.UnsavedNotesRestored);
        WorkspaceTabViewModel inert = host.TabOn("a.md");
        inert.Text += "x";
        Assert.Equal(WorkspaceTeardown.Refused, host.Close(TeardownRoute.ApplicationClose));
        Assert.Contains(host.Announced, item => item is A11yEvent.UnsavedNotesNotKept notKept
            && notKept.Reason == SlateUniffiMethods.HotExitReasonEarlierWorkPending());
        host.DiscardEverything();

        // The retirement confirms; a further reopen restores nothing of it.
        gate.Set();
        host.PumpUntil(() => !host.Store.HasUnresolved(host.Key), "never resolved");
        host.Open();
        host.AssertRead();
        Assert.False(host.TabOn("a.md").IsDirty);
    }

    [Fact]
    public void AnUninstalledHostKeepsTodaysPromptSilently()
    {
        using var host = new HotExitHost();
        host.UseStore(new HotExitStoreHost(null, null));
        host.ClosePrompt = () => VaultCloseDecision.Cancel;
        host.OpenWithoutKey();
        WorkspaceTabViewModel tab = host.OpenNote("a.md");
        tab.Text += "typed";
        Assert.Equal(WorkspaceTeardown.Refused, host.Close(TeardownRoute.ApplicationClose));
        Assert.Equal(1, host.ClosePrompts);
        Assert.DoesNotContain(host.Announced, item => item is A11yEvent.UnsavedNotesNotKept);
    }

    [Fact]
    public void AStoreThatCannotOpenKeepsThePrompt()
    {
        using var host = new HotExitHost();
        host.UseStore(new HotExitStoreHost(null, SlateUniffiMethods.HotExitReasonStoreUnwritable()));
        host.ClosePrompt = () => VaultCloseDecision.Cancel;
        host.OpenWithoutKey();
        WorkspaceTabViewModel tab = host.OpenNote("a.md");
        tab.Text += "typed";
        Assert.Equal(WorkspaceTeardown.Refused, host.Close(TeardownRoute.ApplicationClose));
        Assert.Equal(1, host.ClosePrompts);
        Assert.Contains(host.Announced, item => item is A11yEvent.UnsavedNotesNotKept notKept
            && notKept.Reason == SlateUniffiMethods.HotExitReasonStoreUnwritable());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void InstallationSyncsTheDeviceRootIntoItsParent(bool rootExists)
    {
        string root = Path.Combine(Path.GetTempPath(), $"slate-hot-exit-install-{Guid.NewGuid():N}");
        if (rootExists)
        {
            Directory.CreateDirectory(root);
        }
        try
        {
            string? synced = null;
            HotExitStoreHost installed = HotExitStoreHost.Install(root, path =>
            {
                Assert.True(Directory.Exists(path), "the parent was synced before the root existed");
                synced = path;
                return true;
            });
            Assert.True(installed.HasStore);
            Assert.Equal(root, synced);
            Assert.False(HotExitStoreHost.Install(root, static _ => false).HasStore);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void HostCodeNeverPairsAHashWithASeparateIdentity()
    {
        string source = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "SlateWindows"));
        Assert.True(Directory.Exists(source), source);
        foreach (string file in Directory.EnumerateFiles(source, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }
            string text = File.ReadAllText(file);
            Assert.DoesNotContain(".NoteFileIdentity(", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ReopeningRestoresIntoTheTabsTheLayoutBuilds()
    {
        using var host = new HotExitHost();
        host.Open();
        WorkspaceTabViewModel tab = host.OpenNote("a.md");
        Guid id = tab.Id;
        tab.Text += "typed";
        string final = tab.Text;
        Assert.Equal(WorkspaceTeardown.ClosedKept, host.Close(TeardownRoute.ApplicationClose));
        host.Crash();
        var ready = new List<A11yEvent>();
        host.Open(onReady: announced => ready.AddRange(announced));
        host.AssertRead();
        WorkspaceTabViewModel back = Assert.Single(
            host.Workspace.Groups.SelectMany(group => group.Tabs), candidate => candidate.Id == id);
        Assert.True(back.IsDirty);
        Assert.Equal(final, back.Text);
        Assert.Same(back, host.Workspace.ActiveGroup.ActiveTab);
        Assert.DoesNotContain(ready, item => item is A11yEvent.UnsavedNotesRestored);
    }

    [Theory]
    [InlineData("canonical")]
    [InlineData("upper")]
    [InlineData("other")]
    public void AnEntryWithNoLayoutTabOpensANewDirtyTab(string kind)
    {
        using var host = new HotExitHost();
        host.Open();
        host.Crash();
        Guid guid = Guid.NewGuid();
        string entryId = kind switch
        {
            "canonical" => guid.ToString("D"),
            "upper" => guid.ToString("D").ToUpperInvariant(),
            _ => "not-a-guid",
        };
        string hash = SlateUniffiMethods.EditorTextContentHash("# a\n");
        host.Keep(entryId, "a.md", "# a\nkept", hash, null);
        host.Open();
        host.AssertRead();
        WorkspaceTabViewModel tab = host.Workspace.Groups.SelectMany(group => group.Tabs)
            .First(candidate => candidate.RecoveryEntryId == entryId);
        Assert.True(tab.IsDirty);
        Assert.Equal("# a\nkept", tab.Text);
        if (kind == "canonical")
        {
            Assert.Equal(guid, tab.Id);
        }
        else
        {
            Assert.NotEqual(entryId, tab.Id.ToString("D"));
        }
        host.TabPrompt = _ => WorkspaceDirtyNavigationDecision.Discard;
        host.CloseTab(tab);
        host.PumpUntil(() => host.Snapshot().Entries.Length == 0, "the discard did not retire it");
    }

    [Fact]
    public void EachRestoreDecisionShapesItsTab()
    {
        using var host = new HotExitHost();
        host.Open();
        WorkspaceTabViewModel same = host.OpenNote("a.md");
        same.Text += "same";
        host.Workspace.OpenPath("b.md", WorkspaceOpenTarget.NewTab);
        PumpedDispatcher.Drain();
        WorkspaceTabViewModel stale = host.TabOn("b.md");
        stale.Text += "stale";
        File.WriteAllText(Path.Combine(host.Root, "c.md"), "# c\n");
        File.WriteAllText(Path.Combine(host.Root, "d.md"), "# d\n");
        host.Workspace.OpenPath("c.md", WorkspaceOpenTarget.NewTab);
        PumpedDispatcher.Drain();
        WorkspaceTabViewModel replaced = host.TabOn("c.md");
        replaced.Text += "replaced";
        host.Workspace.OpenPath("d.md", WorkspaceOpenTarget.NewTab);
        PumpedDispatcher.Drain();
        WorkspaceTabViewModel gone = host.TabOn("d.md");
        gone.Text += "gone";
        Assert.Equal(WorkspaceTeardown.ClosedKept, host.Close(TeardownRoute.ApplicationClose));
        host.Crash();
        // While Slate was away.
        File.WriteAllText(Path.Combine(host.Root, "b.md"), "# b changed\n");
        File.Delete(Path.Combine(host.Root, "c.md"));
        File.WriteAllText(Path.Combine(host.Root, "c.md"), "# c\n");
        File.Delete(Path.Combine(host.Root, "d.md"));

        host.Open();
        host.AssertRead();

        WorkspaceTabViewModel a = host.TabOn("a.md");
        Assert.True(a.IsDirty);
        Assert.False(a.IsExternallyStale);
        Assert.NotNull(a.SavedContentHash);
        Assert.True(a.EnforcesFileIdentity);
        WorkspaceTabViewModel b = host.TabOn("b.md");
        Assert.True(b.IsDirty);
        Assert.True(b.IsExternallyStale);
        Assert.NotEqual(SlateUniffiMethods.EditorTextContentHash("# b changed\n"), b.SavedContentHash);
        WorkspaceTabViewModel c = host.TabOn("c.md");
        Assert.True(c.IsDirty);
        Assert.Null(c.SavedContentHash);
        Assert.Null(c.LoadFailure);
        WorkspaceTabViewModel d = host.TabOn("d.md");
        Assert.True(d.IsDirty);
        Assert.Null(d.SavedContentHash);
        Assert.Null(d.LoadFailure);
        Assert.Contains(host.Announced, item => item is A11yEvent.UnsavedNotesRestored { Count: 4 });
        Assert.Contains(host.Announced, item => item is A11yEvent.RestoredNoteChangedOnDisk { Name: "b.md" });
        Assert.Contains(host.Announced, item => item is A11yEvent.RestoredNoteExistsOnDisk { Name: "c.md" });
        Assert.Contains(host.Announced, item => item is A11yEvent.RestoredNoteMissingOnDisk { Name: "d.md" });
    }

    [Fact]
    public void ARestoredStaleTabsFirstSaveConflicts()
    {
        using var host = new HotExitHost();
        host.Open();
        WorkspaceTabViewModel tab = host.OpenNote("a.md");
        tab.Text += "typed";
        Assert.Equal(WorkspaceTeardown.ClosedKept, host.Close(TeardownRoute.ApplicationClose));
        host.Crash();
        File.WriteAllText(Path.Combine(host.Root, "a.md"), "# changed\n");
        host.Open();
        host.AssertRead();
        WorkspaceTabViewModel back = host.TabOn("a.md");

        _ = back.Save();
        host.PumpUntil(() => !back.HasPendingSaves, "the save never settled");

        Assert.Equal("# changed\n", host.Disk("a.md"));
        Assert.True(back.IsDirty);
    }

    [Theory]
    [InlineData("restored")]
    [InlineData("duplicate")]
    public void AReplacedFileIsNeverOverwritten(string route)
    {
        using var host = new HotExitHost();
        host.Open();
        WorkspaceTabViewModel tab = host.OpenNote("a.md");
        tab.Text += "typed";
        Assert.Equal(WorkspaceTeardown.ClosedKept, host.Close(TeardownRoute.ApplicationClose));
        host.Crash();
        host.Open();
        host.AssertRead();
        WorkspaceTabViewModel back = host.TabOn("a.md");
        Assert.True(back.EnforcesFileIdentity);
        if (route == "duplicate")
        {
            host.Workspace.OpenPath("a.md", WorkspaceOpenTarget.SplitRight);
            PumpedDispatcher.Drain();
            back = host.Workspace.ActiveGroup.ActiveTab!;
            Assert.True(back.EnforcesFileIdentity, "the copy lost the baseline's identity");
        }
        // Another file with the same bytes, after the restore classified.
        string path = Path.Combine(host.Root, "a.md");
        string bytes = File.ReadAllText(path);
        File.Delete(path);
        File.WriteAllText(path, bytes);

        _ = back.Save();
        host.PumpUntil(() => !back.HasPendingSaves, "the save never settled");

        Assert.Equal(bytes, host.Disk("a.md"));
    }

    [Fact]
    public void RestoreThenEditThenReopenGetsTheEdit()
    {
        using var host = new HotExitHost();
        host.Open();
        WorkspaceTabViewModel tab = host.OpenNote("a.md");
        tab.Text += "typed";
        Assert.Equal(WorkspaceTeardown.ClosedKept, host.Close(TeardownRoute.ApplicationClose));
        host.Crash();
        host.Open();
        WorkspaceTabViewModel back = host.TabOn("a.md");
        back.Text += "more";
        string final = back.Text;
        host.PumpUntil(() => host.StoredText(back) == final, "the restored tab stopped writing");
        host.Crash();
        host.Open();
        host.AssertRead();
        Assert.Equal(final, host.TabOn("a.md").Text);
    }

    [Fact]
    public void RestoreThenSaveThenReopenGetsNothing()
    {
        using var host = new HotExitHost();
        host.Open();
        WorkspaceTabViewModel tab = host.OpenNote("a.md");
        tab.Text += "typed";
        string final = tab.Text;
        Assert.Equal(WorkspaceTeardown.ClosedKept, host.Close(TeardownRoute.ApplicationClose));
        host.Crash();
        host.Open();
        WorkspaceTabViewModel back = host.TabOn("a.md");
        host.PumpUntil(() => !host.Store.HasUnresolved(host.Key), "the adoption never resolved");
        Assert.True(back.Save());
        host.PumpUntil(() => !back.HasPendingSaves && !back.IsDirty, "the save never landed");
        Assert.Equal(final, host.Disk("a.md"));
        host.PumpUntil(() => host.Snapshot().Entries.Length == 0, "the entry stayed");
        host.Crash();
        host.Open();
        host.AssertRead();
        Assert.False(host.TabOn("a.md").IsDirty);
    }

    [Theory]
    [InlineData("succeeded")]
    [InlineData("failedBefore")]
    [InlineData("erroredAfter")]
    public void RestoreThenDiscardThenReopenGetsNothing(string claim)
    {
        using var host = new HotExitHost { TabPrompt = _ => WorkspaceDirtyNavigationDecision.Discard };
        host.Open();
        WorkspaceTabViewModel tab = host.OpenNote("a.md");
        tab.Text += "typed";
        Assert.Equal(WorkspaceTeardown.ClosedKept, host.Close(TeardownRoute.ApplicationClose));
        host.Crash();
        if (claim == "failedBefore")
        {
            host.Store.BeforeCallForTests = call =>
                call.Kind == HotExitCallKind.Claim ? new HotExitFault.Io() : null;
        }
        else if (claim == "erroredAfter")
        {
            host.Store.AfterCallForTests = call =>
                call.Kind == HotExitCallKind.Claim ? new HotExitFault.Io() : null;
        }
        host.Open();
        WorkspaceTabViewModel back = host.TabOn("a.md");
        host.PumpUntil(() => !host.Store.HasUnresolved(host.Key), "the adoption never resolved");
        host.PumpFor(TimeSpan.FromMilliseconds(50));
        host.Workspace.OpenPath("b.md", WorkspaceOpenTarget.NewTab);
        PumpedDispatcher.Drain();
        host.CloseTab(back);
        host.PumpUntil(() => !host.Store.HasUnresolved(host.Key), "the retirement never resolved");
        host.Store.BeforeCallForTests = null;
        host.Store.AfterCallForTests = null;
        host.NewProcess();
        host.Open();
        host.AssertRead();
        Assert.Empty(host.Snapshot().Entries);
        Assert.Empty(host.Snapshot().Claims);
    }

    [Theory]
    [InlineData("orphan")]
    [InlineData("restored")]
    public void AClosedTabRetiresWhatItInherited(string kind)
    {
        using var host = new HotExitHost { TabPrompt = _ => WorkspaceDirtyNavigationDecision.Discard };
        host.Open();
        WorkspaceTabViewModel tab = host.OpenNote("a.md");
        if (kind == "orphan")
        {
            host.Store.BeforeCallForTests = call =>
                call.Kind == HotExitCallKind.Write ? new HotExitFault.Io() : null;
            tab.Text += "lost";
            host.PumpUntil(() => host.Snapshot().Claims.Length == 1, "the claim never landed");
            host.Store.BeforeCallForTests = null;
        }
        else
        {
            tab.Text += "typed";
            string typed = tab.Text;
            host.PumpUntil(() => host.StoredText(tab) == typed, "never kept");
        }
        host.Crash();
        host.Open();
        host.AssertRead();
        WorkspaceTabViewModel back = host.TabOn("a.md");
        host.Workspace.OpenPath("b.md", WorkspaceOpenTarget.NewTab);
        PumpedDispatcher.Drain();
        host.CloseTab(back);
        host.PumpUntil(() => !host.Store.HasUnresolved(host.Key), "never resolved");
        Assert.Empty(host.Snapshot().Claims);
        Assert.Empty(host.Snapshot().Entries);
    }

    [Fact]
    public void DivergentPeersAreReconciledIntoARecoveredNote()
    {
        using var host = new HotExitHost();
        host.Open();
        WorkspaceTabViewModel left = host.OpenNote("a.md");
        host.Workspace.OpenPath("a.md", WorkspaceOpenTarget.SplitRight);
        PumpedDispatcher.Drain();
        WorkspaceTabViewModel right = host.Workspace.ActiveGroup.ActiveTab!;
        Assert.NotSame(left, right);
        string leftId = left.RecoveryEntryId;
        string rightId = right.RecoveryEntryId;
        Assert.Equal(WorkspaceTeardown.Closed, host.Close(TeardownRoute.ApplicationClose));
        host.Crash();
        string hash = SlateUniffiMethods.EditorTextContentHash("# a\n");
        host.Keep(leftId, "a.md", "# a\nolder", hash, null);
        host.PumpFor(TimeSpan.FromMilliseconds(20));
        host.Keep(rightId, "a.md", "# a\nlatest", hash, null);
        host.Open();
        host.AssertRead();
        WorkspaceTabViewModel[] tabs = [.. host.Workspace.Groups.SelectMany(group => group.Tabs)];
        WorkspaceTabViewModel recovered = tabs.Single(tab => tab.Path == "a (recovered).md");
        Assert.Equal(leftId, recovered.RecoveryEntryId);
        Assert.Equal("# a\nolder", recovered.Text);
        Assert.All(tabs.Where(tab => tab.Path == "a.md"), tab => Assert.Equal("# a\nlatest", tab.Text));
        Assert.Equal(WorkspaceTeardown.ClosedKept, host.Close(TeardownRoute.ApplicationClose));
        int count = tabs.Length;
        host.Crash();
        host.Open();
        host.AssertRead();
        Assert.Equal(count, host.Workspace.Groups.SelectMany(group => group.Tabs).Count());
    }

    [Fact]
    public void ARestoreSpeaksCoresSentencesOnceAfterTheLanding()
    {
        using var host = new HotExitHost();
        host.Open();
        WorkspaceTabViewModel tab = host.OpenNote("a.md");
        tab.Text += "typed";
        Assert.Equal(WorkspaceTeardown.ClosedKept, host.Close(TeardownRoute.ApplicationClose));
        host.Crash();
        File.WriteAllText(Path.Combine(host.Root, "a.md"), "# changed\n");
        var atReady = new List<A11yEvent>();
        host.Open(onReady: announced => atReady.AddRange(announced));
        Assert.DoesNotContain(atReady, item => item is A11yEvent.UnsavedNotesRestored);
        Assert.Single(host.Announced, item => item is A11yEvent.UnsavedNotesRestored { Count: 1 });
        Assert.Single(host.Announced, item => item is A11yEvent.RestoredNoteChangedOnDisk);
        int restored = host.Announced.FindIndex(item => item is A11yEvent.UnsavedNotesRestored);
        int changed = host.Announced.FindIndex(item => item is A11yEvent.RestoredNoteChangedOnDisk);
        Assert.True(restored < changed);
    }

    [Fact]
    public void ADamagedReadKeepsThePromptAndDiscardAllRepairs()
    {
        using var host = new HotExitHost { ClosePrompt = () => VaultCloseDecision.Discard };
        host.Open();
        WorkspaceTabViewModel tab = host.OpenNote("a.md");
        tab.Text += "typed";
        host.PumpUntil(() => host.StoredText(tab) == tab.Text, "never kept");
        host.Crash();
        host.DamageClaims();
        host.Open();
        Assert.True(host.Workspace.HotExit!.IsInertForTests);
        Assert.NotNull(host.Workspace.HotExit!.Key);
        WorkspaceTabViewModel back = host.TabOn("a.md");
        Assert.False(back.IsDirty);
        back.Text += "again";
        Assert.Equal(WorkspaceTeardown.ClosedChangesDiscarded, host.Close(TeardownRoute.ApplicationClose));
        Assert.Contains(host.Announced, item => item is A11yEvent.UnsavedNotesNotKept);
        host.Crash();
        host.Open();
        host.AssertRead();
        WorkspaceTabViewModel repaired = host.TabOn("a.md");
        repaired.Text += "written";
        string final = repaired.Text;
        host.PumpUntil(() => host.StoredText(repaired) == final, "the repaired store did not write");
    }

    [Fact]
    public void AFailedReadNeverDiscardsUnreadEntries()
    {
        using var host = new HotExitHost { ClosePrompt = () => VaultCloseDecision.Discard };
        host.Open();
        WorkspaceTabViewModel tab = host.OpenNote("a.md");
        tab.Text += "typed";
        string typed = tab.Text;
        host.PumpUntil(() => host.StoredText(tab) == typed, "never kept");
        host.Crash();
        host.Store.BeforeCallForTests = call =>
            call.Kind == HotExitCallKind.Read ? new HotExitFault.Io() : null;
        int discards = 0;
        host.Store.AfterCallForTests = call =>
        {
            if (call.Kind == HotExitCallKind.Discard)
            {
                Interlocked.Increment(ref discards);
            }
            return null;
        };
        host.Open();
        Assert.True(host.Workspace.HotExit!.IsInertForTests);
        host.TabOn("a.md").Text += "x";
        Assert.Equal(WorkspaceTeardown.ClosedChangesDiscarded, host.Close(TeardownRoute.Close));
        Assert.Equal(0, Volatile.Read(ref discards));
        Assert.Equal(typed, host.Snapshot().Entries[0].Text);
    }

    [Fact]
    public void AVaultWithNoKeyKeepsThePromptAndKeepsNothing()
    {
        using var host = new HotExitHost { ClosePrompt = () => VaultCloseDecision.Cancel };
        host.WeakRoot = true;
        int calls = 0;
        host.Store.BeforeCallForTests = _ =>
        {
            Interlocked.Increment(ref calls);
            return null;
        };
        host.OpenWithoutKey();
        Assert.True(host.Workspace.HotExit!.IsInertForTests);
        WorkspaceTabViewModel tab = host.OpenNote("a.md");
        tab.Text += "typed";
        Assert.Equal(WorkspaceTeardown.Refused, host.Close(TeardownRoute.ApplicationClose));
        Assert.Equal(1, host.ClosePrompts);
        Assert.Equal(0, Volatile.Read(ref calls));
        Assert.Contains(host.Announced, item => item is A11yEvent.UnsavedNotesNotKept);
    }

    /// <summary>A real vault and lifecycle over a private store host.</summary>
    private sealed class HotExitHost : IDisposable
    {
        private readonly string _stateDir;
        private readonly TimeSpan _settle = VaultLifecycleViewModel.HotExitSettleBudget;
        private readonly TimeSpan _discard = VaultLifecycleViewModel.HotExitDiscardBudget;
        private readonly TimeSpan _reopen = VaultLifecycleViewModel.HotExitReopenWait;
        private bool _shortened;

        internal HotExitHost()
        {
            Dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher));
            Root = Path.Combine(Path.GetTempPath(), $"slate-hot-exit-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Root);
            File.WriteAllText(Path.Combine(Root, "a.md"), "# a\n");
            File.WriteAllText(Path.Combine(Root, "b.md"), "# b\n");
            DeviceRoot = Path.Combine(Path.GetTempPath(), $"slate-hot-exit-device-{Guid.NewGuid():N}");
            Directory.CreateDirectory(DeviceRoot);
            _stateDir = Path.Combine(Path.GetTempPath(), $"slate-hot-exit-state-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_stateDir);
            Store = HotExitStoreHost.Install(DeviceRoot, static _ => true);
            Assert.True(Store.HasStore);
        }

        internal Dispatcher Dispatcher { get; }
        internal string Root { get; }
        internal string DeviceRoot { get; }
        internal HotExitStoreHost Store { get; private set; }
        internal VaultLifecycleViewModel Lifecycle { get; private set; } = null!;
        internal HotExitVaultKey Key { get; private set; } = null!;
        internal List<A11yEvent> Announced { get; } = [];
        internal Func<VaultCloseDecision> ClosePrompt { get; set; } = () => VaultCloseDecision.Cancel;
        internal Func<DiscardPendingDecision> DiscardPrompt { get; set; } = () => DiscardPendingDecision.Cancel;
        internal int ClosePrompts { get; private set; }
        internal int DiscardPrompts { get; private set; }
        internal Func<WorkspaceTabViewModel, WorkspaceDirtyNavigationDecision> TabPrompt { get; set; } =
            _ => WorkspaceDirtyNavigationDecision.Cancel;

        internal WorkspaceViewModel Workspace => Assert.IsType<WorkspaceViewModel>(Lifecycle.Workspace);

        /// <summary>Facts that hold a call past a budget keep the wait short.</summary>
        internal void ShortenBudgets()
        {
            _shortened = true;
            VaultLifecycleViewModel.HotExitSettleBudget = TimeSpan.FromMilliseconds(400);
            VaultLifecycleViewModel.HotExitDiscardBudget = TimeSpan.FromMilliseconds(400);
            VaultLifecycleViewModel.HotExitReopenWait = TimeSpan.FromMilliseconds(400);
        }

        /// <summary>WB-28's seam: this vault's root has only a weak identity.</summary>
        internal bool WeakRoot
        {
            get;
            set
            {
                field = value;
                Environment.SetEnvironmentVariable(
                    WeakRootSeam,
                    value ? Path.GetFileName(Root) : _priorWeakRoot);
            }
        }

        private const string WeakRootSeam = "SLATE_TEST_ROOT_FILE_ID_INFO_UNSUPPORTED";
        private readonly string? _priorWeakRoot = Environment.GetEnvironmentVariable(WeakRootSeam);

        internal void Open(Action<IReadOnlyList<A11yEvent>>? onReady = null) =>
            OpenCore(onReady, expectKey: true);

        /// <summary>The same open, for a vault or host that has no key.</summary>
        internal void OpenWithoutKey() => OpenCore(null, expectKey: false);

        private void OpenCore(Action<IReadOnlyList<A11yEvent>>? onReady, bool expectKey)
        {
            Announced.Clear();
            Lifecycle = new VaultLifecycleViewModel(
                pickVault: () => Task.FromResult<string?>(Root),
                enqueueUi: action => Dispatcher.BeginInvoke(action),
                recentVaultsStore: new RecentVaultsStore(Path.Combine(_stateDir, "recent-vaults.json")),
                announce: Announced.Add,
                confirmUnsavedClose: () =>
                {
                    ClosePrompts++;
                    return ClosePrompt();
                },
                confirmDiscardPending: () =>
                {
                    DiscardPrompts++;
                    return DiscardPrompt();
                },
                confirmDirtyNavigation: (tab, _) => TabPrompt(tab),
                confirmDirtyClose: tab => TabPrompt(tab))
            {
                HotExitHostForTests = Store,
            };
            if (onReady is not null)
            {
                Lifecycle.WorkspaceReady += (_, _) => onReady([.. Announced]);
            }
            Task open = Lifecycle.OpenVaultAsync(Root);
            Assert.True(PumpedDispatcher.PumpUntil(() => open.IsCompleted, TimeSpan.FromSeconds(60)), "never opened");
            open.GetAwaiter().GetResult();
            Assert.True(
                PumpedDispatcher.PumpUntil(
                    () => Lifecycle.FileSidebar is { } sidebar
                        && sidebar.TreeRefreshCompletion.IsCompleted
                        && !sidebar.IsLoadingChildren
                        && !sidebar.IsExpandingLoaded,
                    TimeSpan.FromSeconds(30)),
                "the sidebar never settled");
            if (expectKey)
            {
                Key = Assert.IsType<HotExitKeyResult.Key>(Lifecycle.SessionForTests!.HotExitKey()).KeyValue;
            }
            PumpedDispatcher.Drain();
        }

        /// <summary>An entry as an earlier process left it.</summary>
        internal void Keep(string entryId, string path, string text, string? baselineHash, string? baselineFileId)
        {
            using HotExitStore store = HotExitStore.Open(DeviceRoot);
            ulong epoch = store.CurrentEpoch(Key);
            var claimed = Assert.IsType<HotExitClaimOutcome.Claimed>(
                store.ClaimEntry(Key, epoch, entryId, null, SlateUniffiMethods.NewHotExitIncarnation()));
            _ = store.WriteBuffer(
                Key,
                claimed.Claim,
                1,
                new HotExitEntryWrite(path, text, baselineHash, baselineFileId));
        }

        /// <summary>Makes the vault's claim records unparsable.</summary>
        internal void DamageClaims()
        {
            string vaults = Path.Combine(DeviceRoot, "hot-exit", "v1", "vaults");
            string vault = Assert.Single(Directory.GetDirectories(vaults));
            foreach (string claim in Directory.GetFiles(Path.Combine(vault, "claims")))
            {
                File.WriteAllText(claim, "{ not json");
            }
        }

        internal void UseStore(HotExitStoreHost store) => Store = store;

        /// <summary>This lifecycle's process goes on holding the vault; the
        /// fact opens another beside it.</summary>
        internal void ForgetLifecycle() => _forgotten.Add(Lifecycle);

        private readonly List<VaultLifecycleViewModel> _forgotten = [];

        /// <summary>Closes an open lifecycle, its dirty tabs thrown away.</summary>
        internal void DiscardEverything()
        {
            foreach (WorkspaceTabViewModel tab in Workspace.Groups.SelectMany(group => group.Tabs))
            {
                tab.HotExitChanged = null;
            }
            Workspace.HotExit?.Detach();
            Lifecycle.Dispose();
            PumpedDispatcher.Drain();
        }

        internal void CloseTab(WorkspaceTabViewModel tab)
        {
            Workspace.CloseTabCommand.Execute(tab);
            PumpedDispatcher.Drain();
        }

        /// <summary>The next open has no layout to restore.</summary>
        internal void ForgetLayout()
        {
            string slate = Path.Combine(Root, ".slate");
            foreach (string file in Directory.EnumerateFiles(slate, "workspace*", SearchOption.AllDirectories))
            {
                File.Delete(file);
            }
        }

        internal WorkspaceTabViewModel OpenNote(string path)
        {
            Workspace.OpenPath(path);
            PumpedDispatcher.Drain();
            return TabOn(path);
        }

        internal WorkspaceTabViewModel TabOn(string path) =>
            Workspace.Groups.SelectMany(group => group.Tabs).First(tab => tab.Path == path);

        internal WorkspaceTeardown Close(TeardownRoute route) => Lifecycle.TryCloseForTests(route);

        /// <summary>The crash shape: disposal with no teardown route.</summary>
        internal void Crash()
        {
            Lifecycle.Dispose();
            PumpedDispatcher.Drain();
        }

        /// <summary>Calls still held never issue; a fresh host owns the store.</summary>
        internal void NewProcess()
        {
            Lifecycle.Dispose();
            PumpedDispatcher.Drain();
            Store.SimulateProcessDeathForTests();
            Store = HotExitStoreHost.Install(DeviceRoot, static _ => true);
        }

        /// <summary>The open held the vault and its read succeeded.</summary>
        internal void AssertRead()
        {
            Assert.True(Store.HoldsVaultForTests(Key), "the open does not hold the vault");
            Assert.NotNull(Workspace.HotExit);
            Assert.False(Workspace.HotExit!.IsInertForTests, "the open's read did not succeed");
        }

        internal string Disk(string path) => File.ReadAllText(Path.Combine(Root, path));

        /// <summary>The store as a fresh reader sees it, through no hold.</summary>
        internal HotExitSnapshot Snapshot()
        {
            using HotExitStore store = HotExitStore.Open(DeviceRoot);
            return store.ReadBuffers(Key);
        }

        internal HotExitEntry? StoredEntry(WorkspaceTabViewModel tab) =>
            Snapshot().Entries.FirstOrDefault(entry => entry.EntryId == tab.RecoveryEntryId);

        internal string? StoredText(WorkspaceTabViewModel tab) => StoredEntry(tab)?.Text;

        internal void PumpUntil(Func<bool> condition, string message) =>
            Assert.True(PumpedDispatcher.PumpUntil(condition, Patience), message);

        internal void PumpFor(TimeSpan span)
        {
            DateTime until = DateTime.UtcNow + span;
            _ = PumpedDispatcher.PumpUntil(() => DateTime.UtcNow >= until, span + TimeSpan.FromSeconds(5));
        }

        public void Dispose()
        {
            if (_shortened)
            {
                VaultLifecycleViewModel.HotExitSettleBudget = _settle;
                VaultLifecycleViewModel.HotExitDiscardBudget = _discard;
                VaultLifecycleViewModel.HotExitReopenWait = _reopen;
            }
            Store.BeforeCallForTests = null;
            Store.AfterCallForTests = null;
            if (WeakRoot)
            {
                WeakRoot = false;
            }
            foreach (VaultLifecycleViewModel forgotten in _forgotten)
            {
                forgotten.Dispose();
            }
            Lifecycle?.Dispose();
            PumpedDispatcher.Drain();
            foreach (string directory in new[] { Root, DeviceRoot, _stateDir })
            {
                try
                {
                    Directory.Delete(directory, recursive: true);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    // A hold or a held call may still have the file open.
                }
            }
        }
    }
}
