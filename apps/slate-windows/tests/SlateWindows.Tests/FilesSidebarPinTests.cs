// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Concurrent;
using System.Security.AccessControl;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// WB-12 (#1505, WD6): a pin whose note is gone is pruned lazily on a
/// complete level fetch, once per folder per session, never from a
/// truncated level or a read-only file. A worker proves absence on disk,
/// error-aware; the dispatcher commits in one turn — the pin-generation
/// check, the store's locked re-read and re-probe, and memory's removal — so
/// the file's pins and memory's agree after every prune. Nothing is spoken.
/// </summary>
/// <remarks>The sidebar's tree publications post to a queued context the
/// fact drains, as the dispatcher would run them. "The file's pins" is a
/// fresh <see cref="SidebarSettingsStore"/> load; "memory's pins" is
/// <see cref="FilesSidebarViewModel.PinnedForTests"/>.</remarks>
public sealed class FilesSidebarPinTests
{
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(10);

    [Fact]
    public void AStalePinIsPrunedOnACompleteLevelFetch()
    {
        using var rig = Rig.Create(pins: ["a.md", "keep.md"]);
        rig.DeleteOutside("a.md");
        int spoken = rig.Announced.Count;

        rig.RefreshAndDrain();

        Assert.Equal(["keep.md"], rig.FilePins());
        Assert.Equal(["keep.md"], rig.MemoryPins());
        Assert.Equal(spoken, rig.Announced.Count);
    }

    [Fact]
    public void AStalePinIsNotPrunedFromATruncatedLevel()
    {
        using var rig = Rig.Create(
            pins: ["a.md", "keep.md"],
            seed: root =>
            {
                for (int index = 0; index <= FilesSidebarViewModel.MaxMaterializedDirectoryItems; index++)
                {
                    Directory.CreateDirectory(Path.Combine(root, $"folder-{index:D4}"));
                }
            });
        rig.DeleteOutside("a.md");

        rig.RefreshAndDrain();

        Assert.Contains(rig.Sidebar.RootNodes, node => node.DisplayName.StartsWith("More than", StringComparison.Ordinal));
        Assert.Equal(["a.md", "keep.md"], rig.FilePins());
        Assert.Equal(["a.md", "keep.md"], rig.MemoryPins());
        Assert.Equal(0, rig.ProbeRuns);
    }

    [Fact]
    public void AStalePinIsNotPrunedWhileTheSettingsFileIsReadOnly()
    {
        using var rig = Rig.Create(pins: ["a.md", "keep.md"]);
        rig.DeleteOutside("a.md");
        // Blocked after the sidebar adopted it: the commit's locked re-read
        // finds it malformed, refuses, and the store turns read-only.
        string blocked = """{"version":1,"sort":"bogus","pins":{"":["a.md","keep.md"]}}""";
        File.WriteAllText(rig.SettingsPath, blocked);

        rig.RefreshAndDrain();

        Assert.Equal(blocked, File.ReadAllText(rig.SettingsPath));
        Assert.Equal(["a.md", "keep.md"], rig.MemoryPins());
        Assert.Equal(1, rig.ProbeRuns);

        // Read-only now: a later fetch does not even probe.
        rig.RefreshAndDrain();
        Assert.Equal(1, rig.ProbeRuns);
        Assert.Equal(blocked, File.ReadAllText(rig.SettingsPath));
    }

    [Fact]
    public void APinIsPrunedAtMostOncePerFolderPerSession()
    {
        using var rig = Rig.Create(pins: ["a.md", "keep.md"]);
        rig.DeleteOutside("a.md");
        rig.RefreshAndDrain();
        Assert.Equal(["keep.md"], rig.FilePins());
        Assert.Equal(1, rig.ProbeRuns);

        rig.DeleteOutside("keep.md");
        rig.RefreshAndDrain();

        Assert.Equal(["keep.md"], rig.FilePins());
        Assert.Equal(["keep.md"], rig.MemoryPins());
        Assert.Equal(1, rig.ProbeRuns);
    }

    [Fact]
    public void APinWhoseNoteIsBackByTheMutationIsKept()
    {
        using var rig = Rig.Create(pins: ["a.md", "keep.md"]);
        rig.DeleteOutside("a.md");
        rig.HoldTheProbe = true;
        rig.RefreshAndDrain();
        Action probe = rig.TakeHeldProbe();

        // The worker proves a.md absent and posts its commit; the note is
        // back before the dispatcher runs it.
        Assert.True(Task.Run(probe).Wait(Bound), "the probe never finished");
        File.WriteAllText(Path.Combine(rig.Root, "a.md"), "back");
        rig.Context.Drain();

        Assert.Equal(["a.md", "keep.md"], rig.FilePins());
        Assert.Equal(["a.md", "keep.md"], rig.MemoryPins());
    }

    [Fact]
    public void AnInaccessibleCandidateIsNeverPruned()
    {
        using var rig = Rig.Create(pins: ["a.md", "keep.md"]);
        rig.DeleteOutside("a.md");
        using (DenyListing.Of(rig.Root))
        {
            rig.RefreshAndDrain();
        }

        Assert.Equal(1, rig.ProbeRuns);
        Assert.Equal(["a.md", "keep.md"], rig.FilePins());
        Assert.Equal(["a.md", "keep.md"], rig.MemoryPins());
    }

    [Fact]
    public void ACandidateUnderAReparsePointIsNeverPruned()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "pin-prune-reparse");
        string target = Path.Combine(fixture.Root, "real");
        Directory.CreateDirectory(target);
        string link = Path.Combine(fixture.Root, "link");
        Directory.CreateSymbolicLink(link, target);

        Assert.Empty(VaultAbsence.AbsentAmong(fixture.Root, "link", ["link/gone.md"]));
        Assert.False(VaultAbsence.IsDefinitelyAbsent(fixture.Root, "link/gone.md"));

        // A junction (an NTFS mount point) is a link too: LinkTarget names it.
        string junction = Path.Combine(fixture.Root, "junction");
        var start = new System.Diagnostics.ProcessStartInfo("cmd.exe", $"/c mklink /J \"{junction}\" \"{target}\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using (System.Diagnostics.Process? mklink = System.Diagnostics.Process.Start(start))
        {
            Assert.True(mklink is not null, "premise: cmd.exe started to run mklink /J");
            Assert.True(mklink.WaitForExit(10_000) && mklink.ExitCode == 0, "premise: mklink /J made the junction");
        }
        Assert.NotNull(new DirectoryInfo(junction).LinkTarget);
        Assert.False(VaultAbsence.IsDefinitelyAbsent(fixture.Root, "junction/gone.md"));
        Assert.True(VaultAbsence.IsDefinitelyAbsent(fixture.Root, "real/gone.md"), "premise: the same shape without the link is absent");
    }

    [Fact]
    public void AMissingCandidateIsProbedWithoutStattingTheLeaf()
    {
        using FixtureVault fixture = FixtureVault.Create(0, "pin-prune-leaf");
        Directory.CreateDirectory(Path.Combine(fixture.Root, "sub"));
        File.WriteAllText(Path.Combine(fixture.Root, "sub", "here.md"), "here");
        var statted = new List<string>();

        IReadOnlySet<string> absent = VaultAbsence.AbsentAmong(
            fixture.Root,
            "sub",
            ["sub/gone.md", "sub/here.md"],
            path =>
            {
                statted.Add(path);
                return File.GetAttributes(path);
            });

        Assert.Equal(["sub/gone.md"], absent);
        Assert.DoesNotContain(statted, path => path.EndsWith("gone.md", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(statted, path => path.EndsWith("here.md", StringComparison.OrdinalIgnoreCase));

        // A missing folder proves its candidates absent only through its
        // parent's listing.
        Assert.Equal(["gone/x.md"], VaultAbsence.AbsentAmong(fixture.Root, "gone", ["gone/x.md"]));
    }

    [Fact]
    public void APinEditBeforeThePruneCommitsLeavesFileAndMemoryAgreeing()
    {
        using var rig = Rig.Create(pins: ["a.md", "keep.md"]);
        rig.DeleteOutside("a.md");
        rig.HoldTheProbe = true;
        rig.RefreshAndDrain();
        Action probe = rig.TakeHeldProbe();

        rig.Pin("b.md");
        Assert.True(Task.Run(probe).Wait(Bound), "the probe never finished");
        rig.Context.Drain();

        Assert.Equal(rig.MemoryPins(), rig.FilePins());
        Assert.Equal(["a.md", "b.md", "keep.md"], rig.FilePins());

        rig.HoldTheProbe = false;
        rig.RefreshAndDrain();
        Assert.Equal(["b.md", "keep.md"], rig.FilePins());
        Assert.Equal(["b.md", "keep.md"], rig.MemoryPins());

        rig.Pin("c.md");
        Assert.Equal(["b.md", "c.md", "keep.md"], rig.FilePins());
        Assert.Equal(rig.MemoryPins(), rig.FilePins());
    }

    [Fact]
    public void APinEditQueuedDuringThePruneCommitRunsAfterItsPublication()
    {
        using var rig = Rig.Create(pins: ["a.md", "keep.md"]);
        rig.DeleteOutside("a.md");
        rig.Sidebar.StalePinCommitForTests = () =>
        {
            rig.Sidebar.StalePinCommitForTests = null;
            rig.Context.Post(_ => rig.Pin("c.md"), null);
        };

        rig.RefreshAndDrain();

        Assert.Equal(["c.md", "keep.md"], rig.FilePins());
        Assert.Equal(["c.md", "keep.md"], rig.MemoryPins());
    }

    /// <summary>A Deny ACE on listing a folder for the current user,
    /// removed on dispose: a real folder the probe cannot enumerate.</summary>
    private sealed class DenyListing : IDisposable
    {
        private readonly DirectoryInfo _folder;
        private readonly FileSystemAccessRule _rule;

        private DenyListing(string folder)
        {
            _folder = new DirectoryInfo(folder);
            _rule = new FileSystemAccessRule(
                System.Security.Principal.WindowsIdentity.GetCurrent().User!,
                FileSystemRights.ListDirectory,
                AccessControlType.Deny);
            DirectorySecurity security = _folder.GetAccessControl();
            security.AddAccessRule(_rule);
            _folder.SetAccessControl(security);
        }

        public static DenyListing Of(string folder) => new(folder);

        public void Dispose()
        {
            DirectorySecurity security = _folder.GetAccessControl();
            _ = security.RemoveAccessRule(_rule);
            _folder.SetAccessControl(security);
        }
    }

    /// <summary>A queued stand-in for the dispatcher: posts run when the fact
    /// drains them, in order.</summary>
    private sealed class QueuedContext : SynchronizationContext
    {
        private readonly ConcurrentQueue<Action> _pending = new();

        public override void Post(SendOrPostCallback callback, object? state) => _pending.Enqueue(() => callback(state));

        public void Drain()
        {
            while (_pending.TryDequeue(out Action? action))
            {
                action();
            }
        }
    }

    private sealed class Rig : IDisposable
    {
        private readonly FixtureVault _fixture;
        private readonly VaultSession _session;
        private readonly string _scratch;
        private Action? _heldProbe;
        private int _probeRuns;

        private Rig(FixtureVault fixture, VaultSession session, string scratch)
        {
            _fixture = fixture;
            _session = session;
            _scratch = scratch;
            Sidebar = new FilesSidebarViewModel(
                session,
                Announced.Add,
                vaultRoot: fixture.Root,
                localAppDataRoot: DeviceStateForTests.FreshFileRecentsRoot(),
                treeUiContext: Context,
                writeJournal: new SidebarWriteJournal(Path.Combine(scratch, "journal")),
                pinPruneWorker: (work, token) =>
                {
                    _ = Interlocked.Increment(ref _probeRuns);
                    if (HoldTheProbe)
                    {
                        Assert.Null(_heldProbe);
                        _heldProbe = work;
                        return Task.CompletedTask;
                    }

                    work();
                    return Task.CompletedTask;
                });
            DrainRefresh();
        }

        public static Rig Create(string[] pins, Action<string>? seed = null)
        {
            FixtureVault fixture = FixtureVault.Create(0, "pin-prune");
            foreach (string name in new[] { "a.md", "b.md", "c.md", "keep.md" })
            {
                File.WriteAllText(Path.Combine(fixture.Root, name), name);
            }

            seed?.Invoke(fixture.Root);
            Directory.CreateDirectory(Path.Combine(fixture.Root, ".slate"));
            File.WriteAllText(
                Path.Combine(fixture.Root, ".slate", "sidebar.json"),
                "{\"version\":1,\"pins\":{\"\":[" + string.Join(',', pins.Select(pin => $"\"{pin}\"")) + "]}}");
            VaultSession session = VaultSession.OpenFilesystem(fixture.Root);
            using (var cancel = new CancelToken())
            {
                session.ScanInitial(cancel);
            }

            string scratch = Path.Combine(Path.GetTempPath(), $"slate-pin-prune-{Guid.NewGuid():N}");
            return new Rig(fixture, session, scratch);
        }

        public QueuedContext Context { get; } = new();

        public FilesSidebarViewModel Sidebar { get; }

        public List<A11yEvent> Announced { get; } = [];

        public string Root => _fixture.Root;

        public string SettingsPath => Path.Combine(Root, ".slate", "sidebar.json");

        /// <summary>While set, the prune worker captures its probe instead of
        /// running it.</summary>
        public bool HoldTheProbe { get; set; }

        public int ProbeRuns => Volatile.Read(ref _probeRuns);

        public Action TakeHeldProbe()
        {
            Action probe = Assert.IsType<Action>(_heldProbe);
            _heldProbe = null;
            return probe;
        }

        /// <summary>Deleted outside Slate, and the session's index caught up,
        /// so the next level fetch does not list it.</summary>
        public void DeleteOutside(string relative)
        {
            File.Delete(Path.Combine(Root, relative));
            using var cancel = new CancelToken();
            _ = _session.Rescan(cancel);
        }

        public void RefreshAndDrain()
        {
            Sidebar.Refresh();
            DrainRefresh();
        }

        public void Pin(string relative)
        {
            Sidebar.SelectedNode = Sidebar.RootNodes.Single(node => node.Path == relative);
            Sidebar.PinCommand.Execute(null);
        }

        public string[] FilePins() =>
            [.. new SidebarSettingsStore(Root).Load().Pins.Order(StringComparer.Ordinal)];

        public string[] MemoryPins() =>
            [.. Sidebar.PinnedForTests.Order(StringComparer.Ordinal)];

        private void DrainRefresh()
        {
            Task completion = Sidebar.TreeRefreshCompletion;
            DateTime deadline = DateTime.UtcNow + Bound;
            while (!completion.IsCompleted)
            {
                Assert.True(DateTime.UtcNow < deadline, "the tree refresh never finished");
                Context.Drain();
                Thread.Sleep(1);
            }

            Context.Drain();
            completion.GetAwaiter().GetResult();
            Context.Drain();
        }

        public void Dispose()
        {
            SidebarSessionShutdown shutdown = Sidebar.BeginSessionShutdownAndCaptureWork();
            shutdown.SessionWork.GetAwaiter().GetResult();
            _session.Dispose();
            _fixture.Dispose();
            try
            {
                Directory.Delete(_scratch, recursive: true);
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
