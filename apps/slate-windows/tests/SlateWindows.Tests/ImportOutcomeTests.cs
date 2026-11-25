// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows.Threading;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// Plan 47 CB-16 (#1830, CD10): an import says how it ended, in core's words. A cancel says
/// "Cancelling import…" at once on its press (the owner's wording, 2026-11-24), then the
/// summary of what was copied when the import settles; a completed import's summary stays on
/// the Files line after the refresh it starts; a close or a shutdown asks it to stop without
/// the press's sentence. Each fact runs a real sidebar on a pumped thread with the
/// dispatcher's context installed, so its tree refresh is posted as in the app, and imports
/// real files from outside the vault.
/// </summary>
public sealed class ImportOutcomeTests
{
    /// <summary>Step 0's P1a: a cancel after three of six files have been copied. On main
    /// nothing was said.</summary>
    [Fact]
    public void ACancelledImportSaysWhatItCopied() => RunSta(() =>
    {
        using var import = new ImportSidebar(sourceFiles: 6);
        Dispatcher ui = Dispatcher.CurrentDispatcher;
        int copies = 0;
        bool saidBeforeTheEnd = false;
        import.Sidebar.AfterImportFileCopiedForTesting = () =>
        {
            if (Interlocked.Increment(ref copies) == 3)
            {
                ui.Invoke(() =>
                {
                    import.Sidebar.CancelImportCommand.Execute(null);
                    saidBeforeTheEnd = !import.Sidebar.ImportCompletion.IsCompleted
                        && import.Said.OfType<A11yEvent.ImportCancelling>().Any();
                });
            }
        };

        import.Sidebar.ImportCommand.Execute(null);
        import.AwaitTheImportAndItsPublication();

        Assert.True(saidBeforeTheEnd, "\"Cancelling import…\" was not said at the press, before the import ended");
        Assert.Collection(
            import.Said,
            said => Assert.IsType<A11yEvent.ImportCancelling>(said),
            said =>
            {
                A11yEvent.ImportFinished finished = Assert.IsType<A11yEvent.ImportFinished>(said);
                Assert.True(finished.Cancelled, "the summary did not say the import was cancelled");
                Assert.Equal(3u, finished.CopiedFiles);
                Assert.Equal((uint)import.ImportedFilesOnDisk(), finished.CopiedFiles);
                Assert.Equal(string.Empty, finished.Destination);
            });
        Assert.Equal(
            "Copied 3 files to the vault root. Import cancelled; completed copies remain.",
            import.Sidebar.Status);
    });

    /// <summary>Step 0's P1b: the cancel lands after the pick chose sources and before the
    /// worker ran, so nothing was copied, and the import says so; an empty pick, by contrast,
    /// is no import and says nothing.</summary>
    [Fact]
    public void ACancelBeforeTheCopySaysNothingWasImported() => RunSta(() =>
    {
        using (var import = new ImportSidebar(sourceFiles: 2, worker: ImportSidebar.WaitForTheCancel))
        {
            import.Sidebar.ImportCommand.Execute(null);
            Assert.True(PumpedDispatcher.PumpUntil(() => import.Sidebar.IsImportRunning), "premise: the import never started copying");
            import.Sidebar.CancelImportCommand.Execute(null);
            import.AwaitTheImportAndItsPublication();

            Assert.Collection(
                import.Said,
                said => Assert.IsType<A11yEvent.ImportCancelling>(said),
                said => Assert.Equal(
                    "No items were imported to the vault root. Import cancelled; completed copies remain.",
                    SlateUniffiMethods.A11yRender(Assert.IsType<A11yEvent.ImportFinished>(said)).Text));
            Assert.Equal(0, import.ImportedFilesOnDisk());
        }

        using (var empty = new ImportSidebar(sourceFiles: 0))
        {
            empty.Sidebar.ImportCommand.Execute(null);
            empty.AwaitTheImportAndItsPublication();

            Assert.Empty(empty.Said);
        }
    });

    /// <summary>A close or a shutdown asks the import to stop while its pick is still up (the
    /// only routes that can, the pick being modal). A pick that then returns nothing is no
    /// import and says nothing; one that returns sources copies nothing and says so, in
    /// core's summary. On main both were silent.</summary>
    [Fact]
    public void ACancelDuringThePickReportsOnlyAPickThatChoseSources() => RunSta(() =>
    {
        foreach (bool chose in new[] { false, true })
        {
            var pick = new TaskCompletionSource<IReadOnlyList<string>>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var import = new ImportSidebar(sourceFiles: 2, pick: () => pick.Task);
            import.Sidebar.ImportCommand.Execute(null);
            Assert.True(PumpedDispatcher.PumpUntil(() => import.Sidebar.IsImporting), "premise: the import never asked for its sources");

            import.Sidebar.CancelImport();
            pick.SetResult(chose ? import.Sources : []);
            import.AwaitTheImportAndItsPublication();

            Assert.Equal(0, import.ImportedFilesOnDisk());
            if (!chose)
            {
                Assert.Empty(import.Said);
                continue;
            }

            A11yEvent.ImportFinished finished = Assert.IsType<A11yEvent.ImportFinished>(Assert.Single(import.Said));
            Assert.True(finished.Cancelled, "the summary did not say the import was cancelled");
            Assert.Equal(0u, finished.CopiedFiles);
        }
    });

    /// <summary>Step 0's P1c: on main the summary was said and the Files line went to
    /// "Loading files…" at once, and stayed there.</summary>
    [Fact]
    public void ACompletedImportsSentenceStaysOnTheFilesLine() => RunSta(() =>
    {
        using var import = new ImportSidebar(sourceFiles: 2);

        import.Sidebar.ImportCommand.Execute(null);
        import.AwaitTheImportAndItsPublication();

        A11yEvent.ImportFinished finished = Assert.IsType<A11yEvent.ImportFinished>(Assert.Single(import.Said));
        Assert.False(finished.Cancelled);
        Assert.Equal(2u, finished.CopiedFiles);
        Assert.Equal("Copied 2 files to the vault root.", import.Sidebar.Status);
        Assert.Equal(2, import.ImportedFilesOnDisk());
        Assert.Contains(import.Sidebar.RootNodes, node => node.Path == "n0000.md");
    });

    /// <summary>The summary stays on the Files line over the settings notice the import's own
    /// refresh writes as it publishes: the line is held for that publication, as every
    /// mutation's result is.</summary>
    [Fact]
    public void ACompletedImportsSentenceStaysOverTheSettingsNotice() => RunSta(() =>
    {
        using var import = new ImportSidebar(sourceFiles: 2, malformedSidebarSettings: true);
        Assert.NotNull(import.Sidebar.SettingsNotice);

        import.Sidebar.ImportCommand.Execute(null);
        import.AwaitTheImportAndItsPublication();

        Assert.Equal("Copied 2 files to the vault root.", import.Sidebar.Status);
    });

    /// <summary>A shutdown asks the import to stop and says nothing; a close says its own
    /// refusal, not the press's sentence, and the summary follows when the import settles.
    /// </summary>
    [Fact]
    public void AShutdownOrACloseCancelsWithoutTheCancelSentence() => RunSta(() =>
    {
        using (var import = new ImportSidebar(sourceFiles: 2, worker: ImportSidebar.WaitForTheCancel))
        {
            import.Sidebar.ImportCommand.Execute(null);
            Assert.True(PumpedDispatcher.PumpUntil(() => import.Sidebar.IsImportRunning), "premise: the import never started copying");
            Task drained = import.Sidebar.BeginSessionShutdownAndCaptureWork().SessionWork;
            PumpedDispatcher.PumpUntilDrained(import.Sidebar.ImportCompletion, TimeSpan.FromSeconds(30));
            PumpedDispatcher.PumpUntilDrained(drained, TimeSpan.FromSeconds(30));

            Assert.Empty(import.Said);
        }

        string root = ImportSidebar.NewVault();
        string source = ImportSidebar.NewSource(2);
        var said = new List<A11yEvent>();
        using var lifecycle = new VaultLifecycleViewModel(
            pickVault: () => Task.FromResult<string?>(root),
            enqueueUi: action => action(),
            recentVaultsStore: new RecentVaultsStore(Path.Combine(root + "-device", "recent-vaults.json")),
            announce: said.Add,
            pickImportSources: () => Task.FromResult<IReadOnlyList<string>>(Directory.GetFiles(source)),
            importWorker: ImportSidebar.WaitForTheCancel);
        PumpedDispatcher.PumpUntilDrained(lifecycle.OpenVaultAsync(root), TimeSpan.FromSeconds(30));
        FilesSidebarViewModel sidebar = Assert.IsType<FilesSidebarViewModel>(lifecycle.FileSidebar);
        PumpedDispatcher.PumpUntilDrained(sidebar.TreeRefreshCompletion, TimeSpan.FromSeconds(30));
        said.Clear();
        sidebar.ImportCommand.Execute(null);
        Assert.True(PumpedDispatcher.PumpUntil(() => sidebar.IsImportRunning), "premise: the import never started copying");

        Assert.False(lifecycle.PrepareForApplicationClose(), "the close was not refused while an import ran");
        // The diff red team's low 4: the close's cancel is silent, but the import is
        // stopping all the same: the status bar says so and Cancel import is off.
        Assert.True(sidebar.IsImportRunning, "premise: the import ended before the close returned");
        Assert.Equal(SlateUniffiMethods.ImportStatusLine(ImportPhase.Cancelling), sidebar.ImportStatusLine);
        Assert.False(sidebar.CancelImportCommand.CanExecute(null), "Cancel import stayed on while the close's cancel drained");
        PumpedDispatcher.PumpUntilDrained(sidebar.ImportCompletion, TimeSpan.FromSeconds(30));
        PumpedDispatcher.Drain();

        Assert.DoesNotContain(said, heard => heard is A11yEvent.ImportCancelling);
        int refusal = said.FindIndex(heard =>
            SlateUniffiMethods.A11yRender(heard).Text.StartsWith("Import cancellation requested", StringComparison.Ordinal));
        int summary = said.FindIndex(heard => heard is A11yEvent.ImportFinished);
        Assert.True(refusal >= 0, "the close did not say its refusal: " + string.Join(" | ", said.Select(heard => SlateUniffiMethods.A11yRender(heard).Text)));
        Assert.True(summary > refusal, "the import's summary did not follow the close's refusal: " + string.Join(" | ", said.Select(heard => SlateUniffiMethods.A11yRender(heard).Text)));
        Assert.True(((A11yEvent.ImportFinished)said[summary]).Cancelled, "the summary after the close did not say the import was cancelled");
        Assert.Single(said, heard => heard is A11yEvent.ImportFinished);
    });

    /// <summary>The press says "Cancelling import…" once, at once; the status bar's line
    /// says the same; Cancel import is off; and a second press (Escape's route, which
    /// executes the command whatever it reads) says nothing more.</summary>
    [Fact]
    public void TheCancelIsSpokenOnceAtThePressAndCancelGoesOff() => RunSta(() =>
    {
        using var gate = new ManualResetEventSlim();
        using var import = new ImportSidebar(sourceFiles: 2, worker: ImportSidebar.HeldUntil(gate));
        import.Sidebar.ImportCommand.Execute(null);
        Assert.True(PumpedDispatcher.PumpUntil(() => import.Sidebar.IsImportRunning), "premise: the import never started copying");
        Assert.True(import.Sidebar.CancelImportCommand.CanExecute(null), "premise: Cancel import is off while the import copies");

        try
        {
            import.Sidebar.CancelImportCommand.Execute(null);
            PumpedDispatcher.Drain();

            A11yEvent cancelling = Assert.Single(import.Said);
            Assert.Equal("Cancelling import…", SlateUniffiMethods.A11yRender(Assert.IsType<A11yEvent.ImportCancelling>(cancelling)).Text);
            Assert.Equal(SlateUniffiMethods.ImportStatusLine(ImportPhase.Cancelling), import.Sidebar.ImportStatusLine);
            Assert.False(import.Sidebar.CancelImportCommand.CanExecute(null), "Cancel import stayed on after its press");

            import.Sidebar.CancelImportCommand.Execute(null);
            PumpedDispatcher.Drain();
            Assert.Single(import.Said);
        }
        finally
        {
            gate.Set();
        }

        import.AwaitTheImportAndItsPublication();
        Assert.IsType<A11yEvent.ImportFinished>(import.Said[^1]);
        Assert.Equal(2, import.Said.Count);
    });

    /// <summary>The merge judge's m1: an import whose summary cannot be said (the
    /// dispatcher's raise throws) still ends. With the summary said before the flags clear,
    /// a throw skipped the clears: the import stayed admitted, so the vault could not close
    /// for the rest of the session, and the status bar kept "Cancelling import…".</summary>
    [Fact]
    public void AnImportWhoseSummaryCannotBeSaidStillEnds() => RunSta(() =>
    {
        using var import = new ImportSidebar(
            sourceFiles: 2,
            worker: ImportSidebar.WaitForTheCancel,
            cannotSay: said => said is A11yEvent.ImportFinished);
        import.Sidebar.ImportCommand.Execute(null);
        Assert.True(PumpedDispatcher.PumpUntil(() => import.Sidebar.IsImportRunning), "premise: the import never started copying");
        import.Sidebar.CancelImportCommand.Execute(null);
        Assert.True(import.Sidebar.IsImportCancelling, "premise: the press did not mark the import as stopping");

        Assert.True(
            PumpedDispatcher.PumpUntil(() => import.Sidebar.ImportCompletion.IsCompleted, TimeSpan.FromSeconds(30)),
            "the import never ended");
        PumpedDispatcher.Drain();

        Assert.IsType<A11yEvent.ImportFinished>(import.Said[^1]);
        Assert.False(import.Sidebar.IsImportRunning, "the import still shows as copying after its summary could not be said");
        Assert.False(import.Sidebar.IsImportCancelling, "the import still shows as stopping after its summary could not be said");
        Assert.False(import.Sidebar.IsImporting, "the import stayed admitted after its summary could not be said, so the vault cannot close");
    });

    /// <summary>The red team's M3: after a cancelled import settles, the next import starts
    /// at "Importing…" with Cancel import on. The one reset is the end's, which runs even
    /// when the summary cannot be said (the merge judge's n2: a second reset as the next
    /// import started copying could never be reached, so this fact could not pin it).</summary>
    [Fact]
    public void ASecondImportAfterACancelledOneStartsAtImporting() => RunSta(() =>
    {
        using var first = new ManualResetEventSlim();
        using var second = new ManualResetEventSlim();
        int runs = 0;
        using var import = new ImportSidebar(
            sourceFiles: 2,
            worker: (work, token) => Task.Run(() =>
            {
                _ = (Interlocked.Increment(ref runs) == 1 ? first : second).Wait(TimeSpan.FromSeconds(30));
                work();
            }));
        import.Sidebar.ImportCommand.Execute(null);
        Assert.True(PumpedDispatcher.PumpUntil(() => import.Sidebar.IsImportRunning), "premise: the first import never started copying");
        import.Sidebar.CancelImportCommand.Execute(null);
        first.Set();
        import.AwaitTheImportAndItsPublication();

        try
        {
            import.Sidebar.ImportCommand.Execute(null);
            Assert.True(PumpedDispatcher.PumpUntil(() => import.Sidebar.IsImportRunning), "premise: the second import never started copying");

            Assert.Equal(SlateUniffiMethods.ImportStatusLine(ImportPhase.Importing), import.Sidebar.ImportStatusLine);
            Assert.True(import.Sidebar.CancelImportCommand.CanExecute(null), "Cancel import stayed off for the next import");
        }
        finally
        {
            second.Set();
        }

        import.AwaitTheImportAndItsPublication();
    });

    private static void RunSta(Action body) =>
        StaThread.RunPumped(body, TimeSpan.FromSeconds(120), "The import outcome fact timed out.");

    /// <summary>A Files sidebar over a fresh vault holding <c>a.md</c>, whose import picks
    /// <c>n0000.md</c> onward from a folder outside the vault (nothing, for zero files) and
    /// copies on <see cref="Worker"/> (the pool, as the app does, by default).</summary>
    private sealed class ImportSidebar : IDisposable
    {
        private readonly string _root;
        private readonly string _source;
        private readonly VaultSession _session;

        public ImportSidebar(
            int sourceFiles,
            Func<Action, CancellationToken, Task>? worker = null,
            Func<Task<IReadOnlyList<string>>>? pick = null,
            bool malformedSidebarSettings = false,
            Func<A11yEvent, bool>? cannotSay = null)
        {
            _root = NewVault();
            if (malformedSidebarSettings)
            {
                Directory.CreateDirectory(Path.Combine(_root, ".slate"));
                File.WriteAllText(Path.Combine(_root, ".slate", "sidebar.json"), "{\"version\":1,\"sort\":\"bogus\"}");
            }

            _source = NewSource(sourceFiles);
            _session = VaultSession.OpenFilesystem(_root);
            using (var cancel = new CancelToken())
            {
                _session.ScanInitial(cancel);
            }

            Sidebar = new FilesSidebarViewModel(
                _session,
                said =>
                {
                    Said.Add(said);
                    // The dispatcher's UIA raise throws on a failed HRESULT (the merge
                    // judge's m1); this one throws on the events the fact names.
                    if (cannotSay?.Invoke(said) == true)
                    {
                        throw new InvalidOperationException("The announcement could not be raised.");
                    }
                },
                vaultRoot: _root,
                pickImportSources: pick ?? (() => Task.FromResult(Sources)),
                localAppDataRoot: _root + "-device",
                importWorker: worker);
            PumpedDispatcher.PumpUntilDrained(Sidebar.TreeRefreshCompletion, TimeSpan.FromSeconds(30));
            PumpedDispatcher.Drain();
            Said.Clear();
        }

        public FilesSidebarViewModel Sidebar { get; }

        public List<A11yEvent> Said { get; } = [];

        /// <summary>The files a pick chooses, in name order.</summary>
        public IReadOnlyList<string> Sources =>
            Directory.GetFiles(_source).Order(StringComparer.Ordinal).ToArray();

        /// <summary>A worker that runs nothing until its import is cancelled.</summary>
        public static async Task WaitForTheCancel(Action work, CancellationToken token)
        {
            var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using (token.Register(() => cancelled.TrySetCanceled(token)))
            {
                await cancelled.Task.ConfigureAwait(false);
            }

            work();
        }

        /// <summary>A worker that copies on the pool once <paramref name="gate"/> is set,
        /// whatever its token says meanwhile.</summary>
        public static Func<Action, CancellationToken, Task> HeldUntil(ManualResetEventSlim gate) =>
            (work, ignoredToken) => Task.Run(() =>
            {
                _ = gate.Wait(TimeSpan.FromSeconds(30));
                work();
            });

        public static string NewVault()
        {
            string root = Path.Combine(Path.GetTempPath(), $"slate-cb16-import-{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "a.md"), "# A\n");
            return root;
        }

        public static string NewSource(int files)
        {
            string source = Path.Combine(Path.GetTempPath(), $"slate-cb16-import-source-{Guid.NewGuid():N}");
            Directory.CreateDirectory(source);
            for (int i = 0; i < files; i++)
            {
                File.WriteAllText(Path.Combine(source, $"n{i:D4}.md"), $"# {i}\n");
            }

            return source;
        }

        public int ImportedFilesOnDisk() =>
            Directory.GetFiles(_root, "n*.md").Length;

        /// <summary>The import ended, and the refresh it started published.</summary>
        public void AwaitTheImportAndItsPublication()
        {
            PumpedDispatcher.PumpUntilDrained(Sidebar.ImportCompletion, TimeSpan.FromSeconds(30));
            PumpedDispatcher.Drain();
            PumpedDispatcher.PumpUntilDrained(Sidebar.TreeRefreshCompletion, TimeSpan.FromSeconds(30));
            PumpedDispatcher.Drain();
        }

        public void Dispose()
        {
            try
            {
                PumpedDispatcher.PumpUntilDrained(Sidebar.BeginSessionShutdownAndCaptureWork().SessionWork, TimeSpan.FromSeconds(30));
            }
            finally
            {
                _session.Dispose();
                foreach (string folder in new[] { _root, _source, _root + "-device" })
                {
                    try
                    {
                        if (Directory.Exists(folder))
                        {
                            Directory.Delete(folder, recursive: true);
                        }
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
    }
}
