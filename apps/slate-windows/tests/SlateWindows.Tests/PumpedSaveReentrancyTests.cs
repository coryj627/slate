// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows.Threading;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// #1280 round 2 (contract 38 D-10 as amended — the pumped-wait invariant).
/// The callers that need a save's yes/no — Save All, close tab, close pane,
/// the replace gate and vault teardown — wait for its worker in a nested
/// dispatcher frame, and anything the dispatcher can run may run inside it;
/// the Save command does not wait at all (codex round 2a). Each caller must
/// survive every mutation of the tab set or a tab's identity that can land
/// while its save's write is in flight. Each cell of the matrix parks the
/// first save's worker inside the write, runs the mutation while it is
/// parked, then releases the write; the theory asserts what every cell
/// shares, and the named facts pin the exact outcomes the reviews called out.
/// No fact sleeps: every interleaving is placed by a parked worker, a queued
/// dispatcher callback or a join seam.
/// </summary>
/// <remarks>
/// The fixture is a real vault opened through the vault lifecycle, so
/// renames and deletes arrive as the production file events do and teardown
/// is the real close: pane 1 holds the dirty target (note0) and a clean
/// sibling (note1); pane 2 holds the target's same-path peer and a clean note
/// (note2). Unless a fact says otherwise every dirty-tab prompt answers Save,
/// so no typed text may vanish.
/// </remarks>
public sealed class PumpedSaveReentrancyTests
{
    private static readonly string[] Sites =
        ["save", "save-all", "close-tab", "close-pane", "replace", "teardown"];

    private static readonly string[] Mutations =
    [
        "close-same", "close-sibling", "close-pane", "open-edit", "navigate",
        "duplicate", "move", "rename", "delete", "save-all", "save-peer",
        "teardown", "type", "type-peer", "select",
    ];

    public static TheoryData<string, string> Matrix()
    {
        var data = new TheoryData<string, string>();
        foreach (string site in Sites)
        {
            foreach (string mutation in Mutations)
            {
                data.Add(site, mutation);
            }
        }
        return data;
    }

    /// <summary>Every cell of the site × mutation matrix: no exception, no
    /// typed text lost, no false conflict, no duplicated confirmation, every
    /// announcement on the dispatcher, a consistent workspace, and no save
    /// still admitted or writing afterwards. The Save command's column also
    /// proves it returned without entering a frame.</summary>
    [Theory]
    [MemberData(nameof(Matrix))]
    public void EveryPumpedCallerSurvivesEveryMutation(string site, string mutation)
    {
        using var host = new Host();
        host.RunCell(site, mutation);
        host.AssertInvariants(site, mutation);
    }

    /// <summary>Codex round 1's critical finding: the same tab closed from
    /// inside its own save-before-close. The inner close completes; the outer
    /// finds the tab gone and stops — one TabClosed, never a stale index.</summary>
    [Fact]
    public void ACloseDuringItsOwnSavesPumpClosesOnce()
    {
        using var host = new Host();
        host.RunCell("close-tab", "close-same");
        host.AssertInvariants("close-tab", "close-same");

        Assert.Single(host.Announced, item => item.Event is A11yEvent.TabClosed);
        Assert.True(host.T.IsDisposed);
        Assert.DoesNotContain(host.T, host.LiveTabs());
        Assert.Contains("Marker-A", host.Disk("note0.md"), StringComparison.Ordinal);
    }

    /// <summary>A tab opened into the pane and typed into while the pane's
    /// close is saving: the close admits it — saves it — before disposing
    /// the pane, instead of disposing it past its dirty gate.</summary>
    [Fact]
    public void AClosePaneAdmitsATabOpenedDuringItsSave()
    {
        using var host = new Host();
        host.RunCell("close-pane", "open-edit");
        host.AssertInvariants("close-pane", "open-edit");

        Assert.DoesNotContain(host.G1, host.Workspace.Groups);
        WorkspaceTabViewModel opened = Assert.IsType<WorkspaceTabViewModel>(host.N);
        Assert.True(opened.IsDisposed);
        Assert.Contains("Marker-N", host.Disk("note4.md"), StringComparison.Ordinal);
        Assert.Contains("Marker-A", host.Disk("note0.md"), StringComparison.Ordinal);
    }

    /// <summary>Save All with a tab opened and typed into mid-way: every
    /// dirty tab is saved, each exactly once, and the answer is saved.</summary>
    [Fact]
    public void SaveAllReachesATabOpenedMidway()
    {
        using var host = new Host();
        host.RunCell("save-all", "open-edit");
        host.AssertInvariants("save-all", "open-edit");

        Assert.True(host.SiteAnswer);
        Assert.Contains("Marker-A", host.Disk("note0.md"), StringComparison.Ordinal);
        Assert.Contains("Marker-N", host.Disk("note4.md"), StringComparison.Ordinal);
        Assert.Equal(1, host.WritesTo("note0.md"));
        Assert.Equal(1, host.WritesTo("note4.md"));
        Assert.DoesNotContain(host.LiveTabs(), tab => tab.IsDirty);
    }

    /// <summary>Typing while a save with a same-path peer is writing: one
    /// confirmation, the snapshot on disk, and BOTH tabs dirty with the typed
    /// text — the peer adopts the saved baseline behind its edits instead of
    /// throwing out of the publication.</summary>
    [Fact]
    public void TypingDuringASaveWithAPeerKeepsBothDirty()
    {
        using var host = new Host();
        host.RunCell("save", "type");
        host.AssertInvariants("save", "type");

        Assert.Single(host.Announced, item => item.Event is A11yEvent.NoteSaved);
        Assert.True(host.T.IsDirty);
        Assert.True(host.P.IsDirty);
        Assert.Contains("Marker-T2", host.T.Text, StringComparison.Ordinal);
        Assert.Contains("Marker-T2", host.P.Text, StringComparison.Ordinal);
        string disk = host.Disk("note0.md");
        Assert.Contains("Marker-A", disk, StringComparison.Ordinal);
        Assert.DoesNotContain("Marker-T2", disk, StringComparison.Ordinal);
    }

    /// <summary>The file renamed or deleted under an in-flight save — after
    /// the write landed, or before it could: the old write's outcome is
    /// retired silently. A rename leaves the tab at its new path (a landed
    /// write adopted as its baseline) and its next save succeeds with no
    /// false conflict; a deletion keeps the missing-file status and the
    /// unsaved text.</summary>
    [Theory]
    [InlineData("rename", true)]
    [InlineData("rename", false)]
    [InlineData("delete", true)]
    [InlineData("delete", false)]
    public void ARenameOrDeleteRetiresTheInFlightPublication(string change, bool landed)
    {
        using var host = new Host();
        host.RunCell("save", change, parkAfterWrite: landed);
        host.AssertInvariants("save", change);

        Assert.DoesNotContain(
            host.Announced,
            item => item.Event is A11yEvent.NoteSaved
                or A11yEvent.NoteSaveConflict
                or A11yEvent.NoteSaveBlocked);
        if (change == "rename")
        {
            Assert.Equal("renamed0.md", host.T.Path);
            Assert.Equal(landed, !host.T.IsDirty);
            if (landed)
            {
                Assert.Contains("Marker-A", host.Disk("renamed0.md"), StringComparison.Ordinal);
            }

            host.Type(host.T, "Marker-B");
            host.Announced.Clear();
            host.Workspace.SaveActiveAndSettle();

            var saved = Assert.Single(host.Announced, item => item.Event is A11yEvent.NoteSaved);
            Assert.Equal("renamed0.md", Assert.IsType<A11yEvent.NoteSaved>(saved.Event).Filename);
            Assert.DoesNotContain(
                host.Announced,
                item => item.Event is A11yEvent.NoteSaveConflict or A11yEvent.NoteSaveBlocked);
            string disk = host.Disk("renamed0.md");
            Assert.Contains("Marker-A", disk, StringComparison.Ordinal);
            Assert.Contains("Marker-B", disk, StringComparison.Ordinal);
        }
        else
        {
            Assert.True(host.T.IsMissingFromDisk);
            Assert.Equal(
                "note0.md no longer exists on disk. Unsaved editor content is preserved.",
                host.T.Status);
            Assert.True(host.T.IsDirty);
            Assert.Contains("Marker-A", host.T.Text, StringComparison.Ordinal);
        }
    }

    /// <summary>Two tabs on one file, a save on each while the first is
    /// writing: the second waits for the first's publication and starts from
    /// the hash it recorded — two confirmations in order, one write, and no
    /// false "modified externally".</summary>
    [Fact]
    public void PeersSaveInOrderWithoutAFalseConflict()
    {
        using var host = new Host();
        host.RunCell("save", "save-peer");
        host.AssertInvariants("save", "save-peer");

        (A11yEvent Event, int Thread, string Disk)[] saved =
            [.. host.Announced.Where(item => item.Event is A11yEvent.NoteSaved)];
        Assert.Equal(2, saved.Length);
        Assert.All(saved, item => Assert.Contains("Marker-A", item.Disk, StringComparison.Ordinal));
        Assert.DoesNotContain(
            host.Announced,
            item => item.Event is A11yEvent.NoteSaveConflict or A11yEvent.NoteSaveBlocked);
        Assert.Equal(1, host.WritesTo("note0.md"));
        Assert.False(host.T.IsDirty);
        Assert.False(host.P.IsDirty);
    }

    /// <summary>Contract 35 A-1 and contract 38's VaultClosed-family row: a
    /// vault close while a save is writing — the user would choose Discard —
    /// settles the admitted save first. It publishes, so nothing is left to
    /// ask about: the prompt never appears, the text is on disk, exactly one
    /// close line is spoken, and the session goes only after the workers are
    /// joined.</summary>
    [Fact]
    public void TeardownSettlesAnAdmittedSaveBeforeItAsks()
    {
        using var host = new Host(VaultCloseDecision.Discard);
        host.RunCell("save", "teardown");
        host.AssertInvariants("save", "teardown");

        Assert.Null(host.Lifecycle.Workspace);
        Assert.Equal(0, host.ClosePrompts);
        Assert.Single(host.Announced, item => item.Event is A11yEvent.NoteSaved);
        Assert.Single(
            host.Announced,
            item => item.Event is A11yEvent.VaultClosed
                or A11yEvent.VaultClosedAllSaved
                or A11yEvent.VaultClosedChangesDiscarded);
        Assert.Single(host.Announced, item => item.Event is A11yEvent.VaultClosed);
        Assert.Contains("Marker-A", host.Disk("note0.md"), StringComparison.Ordinal);
        Assert.True(host.Workspace.SavesForTests.IsClosed);
        Assert.Equal(0, host.Workspace.SavesForTests.LiveWorkersForTests);
    }

    /// <summary>Contract 35 A-1, without the settle: the lifecycle disposed
    /// while a save's worker is still writing (the shutdown path). Disposal
    /// joins the worker — its write lands before the session is disposed —
    /// and no worker ever reaches a disposed session. The worker is released
    /// from the join seam, deterministically inside the join.</summary>
    [Fact]
    public void DisposalJoinsASaveWorkerBeforeTheSessionGoes()
    {
        using var host = new Host();
        host.ParkFirstWrite();
        host.Workspace.SavesForTests.BeforeJoinForTests = host.Release;

        host.Workspace.SaveActiveCommand.Execute(null);
        Assert.True(host.WaitParked(), "no write parked");
        host.DisposeLifecycle();

        // Read the moment disposal returned: the write landed inside it.
        Assert.Equal(1, host.WritesTo("note0.md"));
        Assert.Contains("Marker-A", host.Disk("note0.md"), StringComparison.Ordinal);
        Assert.True(host.Workspace.SavesForTests.IsClosed);
        Assert.Null(host.Lifecycle.Workspace);
        host.Settle();
        Assert.Equal(0, host.Workspace.SavesForTests.LiveWorkersForTests);
        Assert.True(host.Faults.Count == 0, string.Join("\n---\n", host.Faults));
        // The publication found its tab disposed and said nothing.
        Assert.DoesNotContain(
            host.Announced,
            item => item.Event is A11yEvent.NoteSaved
                or A11yEvent.NoteSaveConflict
                or A11yEvent.NoteSaveBlocked);
    }

    /// <summary>Codex round 2a (model hole 1): a Ctrl+S still writing when
    /// the user closes or replaces the tab and chooses Discard. The admission
    /// settles the tab's admitted save FIRST — the write lands before the
    /// prompt, never after the choice — and the edit typed after Ctrl+S is
    /// what is discarded.</summary>
    [Theory]
    [InlineData("close-tab")]
    [InlineData("close-pane")]
    [InlineData("replace")]
    public void ADiscardNeverRacesAnAdmittedSave(string site)
    {
        using var host = new Host();
        (int Writes, string Disk)? atPrompt = null;
        host.TabPrompt = _ =>
        {
            atPrompt = (host.WritesTo("note0.md"), host.Disk("note0.md"));
            return WorkspaceDirtyNavigationDecision.Discard;
        };
        host.ParkFirstWrite();
        host.Workspace.SaveActiveCommand.Execute(null);
        host.Type(host.T, "Marker-Late");
        // Released from inside the admission's settle, the only frame that
        // can run it.
        host.Dispatcher.BeginInvoke(
            DispatcherPriority.Background,
            new Action(() =>
            {
                Assert.True(host.WaitParked(), "no write parked");
                host.Release();
            }));

        host.RunSite(site);
        host.Settle();

        Assert.True(host.Faults.Count == 0, string.Join("\n---\n", host.Faults));
        Assert.Equal(1, host.TabPrompts);
        (int writesAtPrompt, string diskAtPrompt) = Assert.IsType<(int, string)>(atPrompt);
        Assert.Equal(1, writesAtPrompt);
        Assert.Contains("Marker-A", diskAtPrompt, StringComparison.Ordinal);
        Assert.Equal(1, host.WritesTo("note0.md"));
        string disk = host.Disk("note0.md");
        Assert.Contains("Marker-A", disk, StringComparison.Ordinal);
        Assert.DoesNotContain("Marker-Late", disk, StringComparison.Ordinal);
        switch (site)
        {
            case "close-tab": Assert.True(host.T.IsDisposed); break;
            case "close-pane": Assert.DoesNotContain(host.G1, host.Workspace.Groups); break;
            case "replace": Assert.Equal("note3.md", host.T.Path); break;
        }
    }

    /// <summary>Codex round 2a (model hole 2): Discard approves exactly what
    /// the prompt asked about. An edit that lands while the prompt is up is
    /// asked about again — for a tab, a pane and the vault — never discarded
    /// unasked.</summary>
    [Theory]
    [InlineData("close-tab")]
    [InlineData("close-pane")]
    [InlineData("replace")]
    [InlineData("teardown")]
    public void AnEditThatLandsWhileThePromptIsUpIsAskedAboutAgain(string site)
    {
        using var host = new Host(VaultCloseDecision.Discard);
        int prompts = 0;
        bool secondPromptSawTheEdit = false;
        WorkspaceDirtyNavigationDecision Ask()
        {
            if (++prompts == 1)
            {
                host.Type(host.T, "Marker-During");
            }
            else
            {
                secondPromptSawTheEdit = host.T.Text.Contains("Marker-During", StringComparison.Ordinal);
            }
            return WorkspaceDirtyNavigationDecision.Discard;
        }
        host.TabPrompt = _ => Ask();
        host.ClosePrompt = () => Ask() == WorkspaceDirtyNavigationDecision.Discard
            ? VaultCloseDecision.Discard
            : VaultCloseDecision.Cancel;

        host.RunSite(site);
        host.Settle();

        Assert.True(host.Faults.Count == 0, string.Join("\n---\n", host.Faults));
        Assert.Equal(2, prompts);
        Assert.True(secondPromptSawTheEdit, "the second prompt did not ask about the edit");
        Assert.DoesNotContain("Marker-During", host.Disk("note0.md"), StringComparison.Ordinal);
        if (site == "teardown")
        {
            Assert.Null(host.Lifecycle.Workspace);
            Assert.Single(host.Announced, item => item.Event is A11yEvent.VaultClosedChangesDiscarded);
        }
    }

    /// <summary>Codex round 2a (design change): holding Ctrl+S never nests a
    /// frame. Presses made while a write is in flight return at once and join
    /// ONE queued save, which captures the editor when it STARTS — text typed
    /// between the presses included: at most two writes and two
    /// confirmations, however many presses. With nothing typed the queued
    /// save finds the note clean and confirms without writing, as a Save of
    /// a clean note always has.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void HoldingSaveCoalescesIntoOneQueuedSave(bool typing)
    {
        using var host = new Host();
        host.ParkFirstWrite();
        int frames = PumpedWait.FramesEnteredForTests;

        host.Workspace.SaveActiveCommand.Execute(null);
        for (int press = 0; press < 4; press++)
        {
            if (typing && press % 2 == 0)
            {
                host.Type(host.T, $"Marker-B{press}");
            }
            host.Workspace.SaveActiveCommand.Execute(null);
        }

        Assert.Equal(frames, PumpedWait.FramesEnteredForTests);
        Assert.False(host.Released, "a Save press waited for the parked write");
        host.Release();
        host.Settle();

        Assert.True(host.Faults.Count == 0, string.Join("\n---\n", host.Faults));
        Assert.Equal(typing ? 2 : 1, host.WritesTo("note0.md"));
        Assert.Equal(2, host.Announced.Count(item => item.Event is A11yEvent.NoteSaved));
        string disk = host.Disk("note0.md");
        Assert.Contains("Marker-A", disk, StringComparison.Ordinal);
        if (typing)
        {
            Assert.Contains("Marker-B0", disk, StringComparison.Ordinal);
            Assert.Contains("Marker-B2", disk, StringComparison.Ordinal);
        }
        Assert.False(host.T.IsDirty);
        Assert.True(host.Workspace.SavesIdle);
    }

    /// <summary>Codex round 2a: a Save nobody waits on still has its failure
    /// handled. A refused write (a conflict) is spoken exactly once; a fault
    /// past the D-10 outcomes is observed and logged, never an unobserved
    /// task — and neither path waits.</summary>
    [Theory]
    [InlineData("conflict")]
    [InlineData("fault")]
    public void ASaveFailureIsObservedAndSpokenOnce(string failure)
    {
        using var host = new Host();
        if (failure == "conflict")
        {
            host.ParkFirstWrite();
        }
        else
        {
            host.T.SaveWriteHookForTests = () => throw new InvalidOperationException("injected fault");
        }
        int frames = PumpedWait.FramesEnteredForTests;

        host.Workspace.SaveActiveCommand.Execute(null);

        Assert.Equal(frames, PumpedWait.FramesEnteredForTests);
        if (failure == "conflict")
        {
            Assert.True(host.WaitParked(), "no write parked");
            File.WriteAllText(Path.Combine(host.Root, "note0.md"), "# Changed elsewhere\n");
            host.Release();
        }
        host.Settle();

        Assert.True(host.T.IsDirty);
        Assert.DoesNotContain(host.Announced, item => item.Event is A11yEvent.NoteSaved);
        if (failure == "conflict")
        {
            Assert.Single(host.Announced, item => item.Event is A11yEvent.NoteSaveConflict);
            Assert.Equal(0, host.Workspace.SaveFailuresObservedForTests);
        }
        else
        {
            // The observer runs off the dispatcher once the save faults.
            Assert.True(
                PumpedDispatcher.PumpUntil(() => host.Workspace.SaveFailuresObservedForTests > 0),
                "the faulted save was never observed");
            Assert.Equal(1, host.Workspace.SaveFailuresObservedForTests);
            Assert.DoesNotContain(
                host.Announced,
                item => item.Event is A11yEvent.NoteSaveConflict or A11yEvent.NoteSaveBlocked);
        }
    }

    /// <summary>Codex round 2a (medium): a rename carries the file's save
    /// chain. The target's write lands, the file is renamed before its
    /// publication, and its same-path peer saves new text: the peer's save
    /// waits for the pre-rename write to publish and starts from the hash it
    /// recorded — no false conflict, both texts on disk, in order.</summary>
    [Fact]
    public void APeerSaveAfterARenameContinuesTheChain()
    {
        using var host = new Host();
        host.ParkFirstWrite(afterWrite: true);

        host.Workspace.SaveActiveCommand.Execute(null);
        Assert.True(host.WaitParked(), "the target's write never landed");
        host.Rename("note0.md", "renamed0.md");
        Assert.True(
            PumpedDispatcher.PumpUntil(() => host.P.Path == "renamed0.md"),
            "the rename never reached the tabs");
        host.Type(host.P, "Marker-P");
        host.Workspace.SelectGroupFromKeyboardFocus(host.G2);
        host.G2.ActiveTab = host.P;
        host.Workspace.SaveActiveCommand.Execute(null);
        host.Release();
        host.Settle();

        Assert.True(host.Faults.Count == 0, string.Join("\n---\n", host.Faults));
        Assert.DoesNotContain(
            host.Announced,
            item => item.Event is A11yEvent.NoteSaveConflict or A11yEvent.NoteSaveBlocked);
        // The target's write was retired silently by the rename; the peer's
        // confirmation names the new file.
        var saved = Assert.Single(host.Announced, item => item.Event is A11yEvent.NoteSaved);
        Assert.Equal("renamed0.md", Assert.IsType<A11yEvent.NoteSaved>(saved.Event).Filename);
        Assert.Equal(1, host.WritesTo("renamed0.md"));
        string disk = host.Disk("renamed0.md");
        Assert.Contains("Marker-A", disk, StringComparison.Ordinal);
        Assert.Contains("Marker-P", disk, StringComparison.Ordinal);
        Assert.False(host.T.IsDirty);
        Assert.False(host.P.IsDirty);
    }

    /// <summary>Coalescing is per item: a Save request is for the note the
    /// tab shows when it is made. A tab re-pointed at another note while its
    /// write is landing and a second save waits its turn: the landed write
    /// and the waiting save both belong to the old note and say nothing (the
    /// waiting one never writes), and a Save made after the re-point queues
    /// its own save — it does not join the retired one — which confirms the
    /// new note once. Driven through the tab directly: every workspace route
    /// that re-points a tab settles its saves first (rule 1), or re-points
    /// only a transient tab, which is never dirty.</summary>
    [Fact]
    public void AQueuedSaveServesOnlyTheItemItWasRequestedFor()
    {
        using var host = new Host();
        host.G1.ActiveTab = host.S;
        host.Type(host.S, "Marker-S1");
        host.ParkFirstWrite(afterWrite: true);

        host.Workspace.SaveActiveCommand.Execute(null);
        Assert.True(host.WaitParked(), "the first write never landed");
        host.Type(host.S, "Marker-S2");
        host.Workspace.SaveActiveCommand.Execute(null);
        host.S.ReplaceItem(new WorkspaceItemState(WorkspaceItemKind.Markdown, "note3.md"));
        host.Workspace.SaveActiveCommand.Execute(null);
        host.Release();
        host.Settle();

        Assert.True(host.Faults.Count == 0, string.Join("\n---\n", host.Faults));
        var saved = Assert.Single(host.Announced, item => item.Event is A11yEvent.NoteSaved);
        Assert.Equal("note3.md", Assert.IsType<A11yEvent.NoteSaved>(saved.Event).Filename);
        Assert.DoesNotContain(
            host.Announced,
            item => item.Event is A11yEvent.NoteSaveConflict or A11yEvent.NoteSaveBlocked);
        Assert.Equal(1, host.WritesTo("note1.md"));
        Assert.Equal(0, host.WritesTo("note3.md"));
        string disk = host.Disk("note1.md");
        Assert.Contains("Marker-S1", disk, StringComparison.Ordinal);
        Assert.DoesNotContain("Marker-S2", disk, StringComparison.Ordinal);
        Assert.Equal("note3.md", host.S.Path);
        Assert.False(host.S.IsDirty);
    }

    internal sealed class Host : IDisposable
    {
        private readonly SynchronizationContext? _previousContext;
        private readonly string _stateDir;
        private readonly ManualResetEventSlim _parked = new();
        private readonly ManualResetEventSlim _release = new();
        private readonly Lock _writesGate = new();
        private readonly Dictionary<string, int> _writes = new(StringComparer.Ordinal);
        private int _parkCandidates;
        private bool _parkAfterWrite;
        private bool _lifecycleDisposed;

        internal Host(VaultCloseDecision closeDecision = VaultCloseDecision.SaveAll)
        {
            _previousContext = SynchronizationContext.Current;
            Dispatcher = Dispatcher.CurrentDispatcher;
            DispatcherThread = Environment.CurrentManagedThreadId;
            SynchronizationContext.SetSynchronizationContext(
                new DispatcherSynchronizationContext(Dispatcher));
            ClosePrompt = () => closeDecision;

            Root = Path.Combine(Path.GetTempPath(), $"slate-pumped-save-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Root);
            for (int n = 0; n < 6; n++)
            {
                File.WriteAllText(Path.Combine(Root, $"note{n}.md"), $"# Note {n}\n\nBody {n}.\n");
            }
            _stateDir = Path.Combine(Path.GetTempPath(), $"slate-pumped-save-state-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_stateDir);

            Lifecycle = new VaultLifecycleViewModel(
                pickVault: () => Task.FromResult<string?>(Root),
                enqueueUi: action => Dispatcher.BeginInvoke(action),
                recentVaultsStore: new RecentVaultsStore(Path.Combine(_stateDir, "recent-vaults.json")),
                announce: item => Announced.Add(
                    (item, Environment.CurrentManagedThreadId, DiskOrEmpty("note0.md"))),
                confirmUnsavedClose: () =>
                {
                    ClosePrompts++;
                    return ClosePrompt();
                },
                confirmDirtyNavigation: (tab, _) => AskTab(tab),
                confirmDirtyClose: AskTab);
            Task open = Lifecycle.OpenVaultAsync(Root);
            Assert.True(
                PumpedDispatcher.PumpUntil(() => open.IsCompleted, TimeSpan.FromSeconds(60)),
                "the vault never opened");
            open.GetAwaiter().GetResult();
            Workspace = Assert.IsType<WorkspaceViewModel>(Lifecycle.Workspace);
            Assert.True(
                PumpedDispatcher.PumpUntil(
                    () => Lifecycle.FileSidebar is { } sidebar
                        && sidebar.TreeRefreshCompletion.IsCompleted
                        && !sidebar.IsLoadingChildren
                        && !sidebar.IsExpandingLoaded,
                    TimeSpan.FromSeconds(30)),
                "the sidebar never settled");

            Workspace.OpenPath("note0.md");
            T = ActiveTab();
            Workspace.OpenPath("note1.md", WorkspaceOpenTarget.NewTab);
            S = ActiveTab();
            Workspace.OpenPath("note0.md");
            Assert.Same(T, ActiveTab());
            G1 = Workspace.ActiveGroup;
            Workspace.OpenPath("note0.md", WorkspaceOpenTarget.SplitRight);
            G2 = Workspace.ActiveGroup;
            Assert.NotSame(G1, G2);
            P = ActiveTab();
            Workspace.OpenPath("note2.md", WorkspaceOpenTarget.NewTab);
            U = ActiveTab();
            Workspace.SelectGroupFromKeyboardFocus(G1);
            Assert.Same(G1, Workspace.ActiveGroup);
            Assert.Same(T, G1.ActiveTab);
            Assert.Equal(new[] { T, S }, G1.Tabs);
            Assert.Equal(new[] { P, U }, G2.Tabs);
            PumpedDispatcher.Drain();

            Type(T, "Marker-A");
            Assert.True(T.IsDirty, "the target is not dirty");
            Assert.True(P.IsDirty, "the peer did not mirror the edit");
            Announced.Clear();
        }

        internal Dispatcher Dispatcher { get; }
        internal int DispatcherThread { get; }
        internal string Root { get; }
        internal VaultLifecycleViewModel Lifecycle { get; }

        /// <summary>The workspace the fixture opened — kept after a teardown
        /// disposes it, so its save coordinator can be inspected.</summary>
        internal WorkspaceViewModel Workspace { get; }

        internal WorkspaceGroupViewModel G1 { get; }
        internal WorkspaceGroupViewModel G2 { get; }
        internal WorkspaceTabViewModel T { get; }
        internal WorkspaceTabViewModel S { get; }
        internal WorkspaceTabViewModel P { get; }
        internal WorkspaceTabViewModel U { get; }

        /// <summary>The tab the open-edit mutation opened.</summary>
        internal WorkspaceTabViewModel? N { get; private set; }

        internal List<(A11yEvent Event, int Thread, string Disk)> Announced { get; } = [];
        internal List<Exception> Faults { get; } = [];
        internal HashSet<string> Markers { get; } = new(StringComparer.Ordinal);
        internal bool MutationRanWhileParked { get; private set; }
        internal bool SaveReturnedWithoutPumping { get; private set; }
        internal bool SiteAnswer { get; private set; }

        /// <summary>The dirty-tab prompt (close and navigation); Save unless
        /// a fact says otherwise.</summary>
        internal Func<WorkspaceTabViewModel, WorkspaceDirtyNavigationDecision> TabPrompt { get; set; } =
            _ => WorkspaceDirtyNavigationDecision.Save;

        internal int TabPrompts { get; private set; }

        /// <summary>The vault close prompt.</summary>
        internal Func<VaultCloseDecision> ClosePrompt { get; set; }

        internal int ClosePrompts { get; private set; }

        internal bool Released => _release.IsSet;

        private VaultSession Session =>
            Lifecycle.SessionForTests ?? throw new InvalidOperationException("the vault is closed");

        /// <summary>One cell: park the first write, run the mutation while it
        /// is parked — from the site's frame, or right after the Save
        /// command, which does not pump — release the write, then settle.</summary>
        internal void RunCell(string site, string mutation, bool parkAfterWrite = false)
        {
            ParkFirstWrite(parkAfterWrite);
            Dispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new Action(() =>
                {
                    try
                    {
                        if (!WaitParked())
                        {
                            throw new TimeoutException($"{site}: no write parked");
                        }
                        MutationRanWhileParked = !_release.IsSet;
                        // Released from the frame the mutation runs in, or
                        // right after it when the mutation does not pump.
                        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(Release));
                        Mutate(mutation);
                        HookAll();
                    }
                    catch (Exception exception)
                    {
                        Faults.Add(exception);
                        Release();
                    }
                }));

            int frames = PumpedWait.FramesEnteredForTests;
            try
            {
                RunSite(site);
                if (site == "save")
                {
                    // The Save command returned while its write could not
                    // have finished (the release runs only when pumped), and
                    // it entered no frame.
                    SaveReturnedWithoutPumping =
                        !_release.IsSet && PumpedWait.FramesEnteredForTests == frames;
                }
            }
            catch (Exception exception)
            {
                Faults.Add(exception);
            }
            finally
            {
                if (site != "save")
                {
                    Release();
                }
            }
            Settle();
            Release();
            Settle();
        }

        internal void AssertInvariants(string site, string mutation)
        {
            string cell = $"{site} × {mutation}";
            Assert.True(Faults.Count == 0, $"{cell}:\n" + string.Join("\n---\n", Faults));
            Assert.True(MutationRanWhileParked, $"{cell}: the mutation did not run while the write was parked");
            if (site == "save")
            {
                Assert.True(SaveReturnedWithoutPumping, $"{cell}: the Save command waited in a frame");
            }
            Assert.All(Announced, item => Assert.Equal(DispatcherThread, item.Thread));

            // A deletion is a real conflict for a save that still names the
            // file; nothing else in the matrix changes the file behind a tab.
            if (mutation != "delete")
            {
                Assert.DoesNotContain(
                    Announced,
                    item => item.Event is A11yEvent.NoteSaveConflict or A11yEvent.NoteSaveBlocked);
            }
            int saveCommands = (site == "save" ? 1 : 0) + (mutation == "save-peer" ? 1 : 0);
            int confirmations = Announced.Count(item => item.Event is A11yEvent.NoteSaved);
            Assert.True(
                confirmations <= saveCommands,
                $"{cell}: {confirmations} save confirmations for {saveCommands} Save commands");
            Assert.True(
                Announced.Count(item => item.Event is A11yEvent.VaultClosed
                    or A11yEvent.VaultClosedAllSaved
                    or A11yEvent.VaultClosedChangesDiscarded) <= 1,
                $"{cell}: more than one close line");

            // Every decision was Save: every typed marker is on disk or in a
            // tab that is still open.
            string disk = AllDiskText();
            WorkspaceTabViewModel[] live = LiveTabs();
            foreach (string marker in Markers)
            {
                Assert.True(
                    disk.Contains(marker, StringComparison.Ordinal)
                        || live.Any(tab => tab.Text.Contains(marker, StringComparison.Ordinal)),
                    $"{cell}: {marker} is on neither disk nor an open tab");
            }

            if (Lifecycle.Workspace is WorkspaceViewModel open)
            {
                Assert.Contains(open.ActiveGroup, open.Groups);
                foreach (WorkspaceGroupViewModel group in open.Groups)
                {
                    Assert.True(
                        group.ActiveTab is null || group.Tabs.Contains(group.ActiveTab),
                        $"{cell}: a group's active tab is not one of its tabs");
                    Assert.DoesNotContain(group.Tabs, tab => tab.IsDisposed);
                }
                Assert.True(
                    open.Groups.Count == 1 || open.Groups.All(group => group.Tabs.Count > 0),
                    $"{cell}: an empty pane was left beside others");
                Assert.True(open.SavesIdle, $"{cell}: a save is still admitted");
            }
            else
            {
                Assert.True(Workspace.SavesForTests.IsClosed, $"{cell}: the vault closed with its saves open");
            }
            Assert.Equal(0, Workspace.SavesForTests.LiveWorkersForTests);
        }

        internal bool WaitParked() => _parked.Wait(TimeSpan.FromSeconds(10));

        internal void Release() => _release.Set();

        /// <summary>The first write anywhere parks — before the core write,
        /// or after it when <paramref name="afterWrite"/> asks for a write
        /// that has landed.</summary>
        internal void ParkFirstWrite(bool afterWrite = false)
        {
            _parkAfterWrite = afterWrite;
            HookAll();
        }

        /// <summary>Every tab's save counts its landed writes by path and may
        /// park (see <see cref="ParkFirstWrite"/>).</summary>
        internal void HookAll()
        {
            foreach (WorkspaceTabViewModel tab in Lifecycle.Workspace?.Groups.SelectMany(group => group.Tabs) ?? [])
            {
                WorkspaceTabViewModel hooked = tab;
                hooked.SaveWriteHookForTests = () =>
                {
                    if (!_parkAfterWrite)
                    {
                        MaybePark();
                    }
                };
                hooked.SaveWrittenHookForTests = () =>
                {
                    lock (_writesGate)
                    {
                        _writes[hooked.Path] = _writes.GetValueOrDefault(hooked.Path) + 1;
                    }
                    if (_parkAfterWrite)
                    {
                        MaybePark();
                    }
                };
            }
        }

        internal int WritesTo(string path)
        {
            lock (_writesGate)
            {
                return _writes.GetValueOrDefault(path);
            }
        }

        internal void Type(WorkspaceTabViewModel tab, string marker)
        {
            Markers.Add(marker);
            tab.EditorDocument!.Insert(tab.EditorDocument.TextLength, $"\n{marker}.\n");
        }

        internal void Rename(string from, string to) => _ = Session.RenameFile(from, to);

        internal WorkspaceTabViewModel[] LiveTabs() =>
            [.. Lifecycle.Workspace?.Groups.SelectMany(group => group.Tabs).Where(tab => !tab.IsDisposed) ?? []];

        internal string Disk(string path) => File.ReadAllText(Path.Combine(Root, path));

        /// <summary>Pump until no save is admitted or writing, then drain what
        /// the publications queued.</summary>
        internal void Settle()
        {
            PumpedDispatcher.PumpUntil(
                () => Workspace.SavesIdle && Workspace.SavesForTests.LiveWorkersForTests == 0,
                TimeSpan.FromSeconds(30));
            for (int drain = 0; drain < 3; drain++)
            {
                PumpedDispatcher.Drain();
            }
        }

        internal void DisposeLifecycle()
        {
            _lifecycleDisposed = true;
            Lifecycle.Dispose();
        }

        internal void RunSite(string site)
        {
            switch (site)
            {
                case "save": Workspace.SaveActiveCommand.Execute(null); break;
                case "save-all": SiteAnswer = Workspace.SaveAll(); break;
                case "close-tab": Workspace.CloseTabCommand.Execute(T); break;
                case "close-pane": Workspace.ClosePaneCommand.Execute(null); break;
                case "replace": Workspace.OpenPath("note3.md"); break;
                case "teardown": Lifecycle.CloseVault(); break;
                default: throw new ArgumentOutOfRangeException(nameof(site), site, null);
            }
        }

        public void Dispose()
        {
            Release();
            try
            {
                Settle();
            }
            finally
            {
                if (!_lifecycleDisposed)
                {
                    DisposeLifecycle();
                }
                SynchronizationContext.SetSynchronizationContext(_previousContext);
                _parked.Dispose();
                _release.Dispose();
                TryDelete(Root);
                TryDelete(_stateDir);
            }
        }

        private WorkspaceDirtyNavigationDecision AskTab(WorkspaceTabViewModel tab)
        {
            TabPrompts++;
            return TabPrompt(tab);
        }

        private void Mutate(string mutation)
        {
            switch (mutation)
            {
                case "close-same": Workspace.CloseTabCommand.Execute(T); break;
                case "close-sibling": Workspace.CloseTabCommand.Execute(S); break;
                case "close-pane": Workspace.ClosePaneCommand.Execute(null); break;
                case "open-edit":
                    Workspace.OpenPath("note4.md", WorkspaceOpenTarget.NewTab);
                    N = ActiveTab();
                    HookAll();
                    Type(N, "Marker-N");
                    break;
                case "navigate": Workspace.OpenPath("note5.md"); break;
                case "duplicate": Workspace.DuplicateTabCommand.Execute(null); break;
                case "move": Workspace.MoveTabRightCommand.Execute(null); break;
                case "rename": Rename("note0.md", "renamed0.md"); break;
                case "delete": DeleteOnSta("note0.md"); break;
                case "save-all": _ = Workspace.SaveAll(); break;
                case "save-peer":
                    Workspace.SelectGroupFromKeyboardFocus(G2);
                    G2.ActiveTab = P;
                    Workspace.SaveActiveCommand.Execute(null);
                    break;
                case "teardown": Lifecycle.CloseVault(); break;
                case "type": Type(T, "Marker-T2"); break;
                case "type-peer": Type(P, "Marker-P"); break;
                // PR 2 (#1245): a Files selection shows its note in the pane's
                // transient tab — a tab created, replaced or activated in place.
                case "select": Workspace.OpenPath("note4.md", WorkspaceOpenTarget.CurrentTab, fromSelection: true); break;
                default: throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
            }
        }

        /// <summary>DeleteFile routes through the system trash, whose COM
        /// init needs an STA thread (the app's UI thread is one; the xunit
        /// thread is not — the HistoryPanelTests precedent). The Deleted
        /// event still arrives through the lifecycle's listener.</summary>
        private void DeleteOnSta(string path)
        {
            VaultSession session = Session;
            Exception? failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    session.DeleteFile(path);
                }
                catch (Exception exception)
                {
                    failure = exception;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (failure is not null)
            {
                throw failure;
            }
        }

        private void MaybePark()
        {
            if (Interlocked.Increment(ref _parkCandidates) == 1)
            {
                _parked.Set();
                _release.Wait(TimeSpan.FromSeconds(20));
            }
        }

        private WorkspaceTabViewModel ActiveTab() =>
            Assert.IsType<WorkspaceTabViewModel>(Workspace.ActiveGroup.ActiveTab);

        private string DiskOrEmpty(string path)
        {
            try
            {
                return File.ReadAllText(Path.Combine(Root, path));
            }
            catch (IOException)
            {
                return string.Empty;
            }
        }

        private string AllDiskText() =>
            string.Join(
                "\n",
                Directory.EnumerateFiles(Root, "*.md", SearchOption.AllDirectories)
                    .Select(File.ReadAllText));

        private static void TryDelete(string directory)
        {
            try
            {
                Directory.Delete(directory, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
