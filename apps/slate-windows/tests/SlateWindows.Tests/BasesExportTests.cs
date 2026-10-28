// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using SlateWindows.Bases;
using SlateWindows.Tests.Support;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// WB-12 (#1508, WD5): a Bases export saved inside the open vault is written
/// through core, as the session's own write. The destination is observed on
/// disk before the app asks to replace it, and the publication is bound to
/// that observation: created exclusively when it was absent, saved under the
/// observed hash when it was present (core re-reads the disk bytes in its
/// writer lock), never replaced when it could not be observed. Every refusal
/// leaves the newer bytes on disk. Outside the vault the export writes the
/// file directly, after the same question.
/// </summary>
/// <remarks>"Rewritten" is <see cref="File.WriteAllBytes"/> with different
/// bytes, outside the session. The workspace runs headless, so each step's
/// worker runs inline, in order.</remarks>
public sealed class BasesExportTests : IDisposable
{
    private const string StatusBase =
        "views:\n  - type: table\n    name: Main\n    order:\n      - file.name\n      - note.status\n";

    private static readonly byte[] Theirs = "written by someone else\n"u8.ToArray();

    private readonly string _root;
    private readonly VaultSession _session;
    private readonly List<A11yEvent> _announced = [];
    private readonly List<string> _confirmed = [];

    public BasesExportTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"slate-windows-bases-export-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "note0.md"), "---\nstatus: todo\n---\n\n# note0\n");
        File.WriteAllText(Path.Combine(_root, "note1.md"), "---\nstatus: done\n---\n\n# note1\n");
        File.WriteAllText(Path.Combine(_root, "Status.base"), StatusBase);
        _session = VaultSession.OpenFilesystem(_root);
        Scan();
    }

    public void Dispose()
    {
        _session.Dispose();
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    /// <summary>#1508: with the real watcher live, an export into the vault
    /// is the session's own write, so the watch hears it as an echo: one
    /// sentence ("Exported."), no Watcher rescan, no call to another
    /// listener, and the tree lists the file. Main's direct write made the
    /// watcher's "Files refreshed. 1 new or changed, 0 removed." follow it.
    /// The sentinel written last proves the watch was live throughout.</summary>
    [Fact]
    public void AnExportInsideTheVaultIsAnEchoAndSpeaksOnce() => StaThread.Run(() =>
    {
        using IDisposable watcher = VaultWatcherSwitch.Lift();
        using var h = new RescanTests.Harness(
            "bases-export-echo",
            ("note0.md", "---\nstatus: todo\n---\n\n# note0\n"),
            ("Status.base", StatusBase));
        var outside = new EventRecorder();
        _ = h.Lifecycle.SessionForTests!.RegisterEventListener(outside);
        h.Workspace.OpenPath("Status.base");
        h.Workspace.PickBasesExportDestination = (_, _) => Path.Combine(h.Root, "export.md");
        int scans = h.ScanCalls;
        h.Events.Clear();

        h.Workspace.BasesExportMarkdownCommand.Execute(null);
        // Past core's quiet window and its spacing: a watch that heard the
        // export as someone else's would have called by now.
        h.Context.Await(Task.Delay(TimeSpan.FromSeconds(10)), "the watcher's quiet window");
        h.Settle();

        Assert.Single(h.Events, e => e is A11yEvent.BasesViewExported);
        Assert.DoesNotContain(h.Events, e => e is A11yEvent.VaultRescanFinished
            or A11yEvent.VaultRescanIncomplete
            or A11yEvent.BasesViewExportFailed);
        Assert.Equal(0, outside.ExternalChanges);
        Assert.Equal(scans, h.ScanCalls);
        Assert.Contains(h.Sidebar.RootNodes, node => node.Path == "export.md");

        h.Write("sentinel.md", "# Sentinel\n");
        h.Context.RunUntil(() => outside.ExternalChanges > 0, "the watch hearing the sentinel");
    }, TimeSpan.FromSeconds(120), "STA test body timed out.");

    [Theory]
    [InlineData("md")]
    [InlineData("csv")]
    public void AnExportOverAnExistingVaultFileReplacesItUnderTheObservedHash(string extension)
    {
        string target = Seed($"export.{extension}", "old export\n"u8.ToArray());
        using WorkspaceViewModel workspace = Workspace(target, confirm: true);
        string expected = ExpectedText(workspace, extension);

        Export(workspace, extension);

        Assert.Equal(expected, File.ReadAllText(target));
        Assert.Single(_announced, e => e is A11yEvent.BasesViewExported);
        Assert.DoesNotContain(_announced, e => e is A11yEvent.BasesViewExportFailed);
        Assert.Equal([target], _confirmed);
    }

    [Fact]
    public void AnExportWhoseDestinationChangedDuringTheConfirmationIsRefused()
    {
        string target = Seed("export.md", "old export\n"u8.ToArray());
        using WorkspaceViewModel workspace = Workspace(target, confirm: true);
        workspace.ConfirmBasesExportOverwrite = path =>
        {
            _confirmed.Add(path);
            File.WriteAllBytes(target, Theirs);
            return true;
        };

        Export(workspace, "md");

        AssertRefusedWithTheirBytes(target, "the file changed after you confirmed replacing it; nothing was replaced");
    }

    [Fact]
    public void AnExportWhoseDestinationChangedAfterTheConfirmationIsRefused()
    {
        string target = Seed("export.md", "old export\n"u8.ToArray());
        using WorkspaceViewModel workspace = Workspace(target, confirm: true);
        workspace.BasesExportPublishingForTests = _ =>
        {
            File.WriteAllBytes(target, Theirs);
            Scan();
        };

        Export(workspace, "md");

        AssertRefusedWithTheirBytes(target, "the file changed after you confirmed replacing it; nothing was replaced");
    }

    [Fact]
    public void AnExportRefusesADestinationChangeTheIndexHasNotSeen()
    {
        string target = Seed("export.md", "old export\n"u8.ToArray());
        using WorkspaceViewModel workspace = Workspace(target, confirm: true);
        string? indexed = IndexedHash("export.md");
        workspace.BasesExportPublishingForTests = _ => File.WriteAllBytes(target, Theirs);

        Export(workspace, "md");

        AssertRefusedWithTheirBytes(target, "the file changed after you confirmed replacing it; nothing was replaced");
        Assert.Equal(indexed, IndexedHash("export.md"));
    }

    [Fact]
    public void AnExportOntoAFileThatAppearedAfterItWasChosenIsRefused()
    {
        string target = Path.Combine(_root, "export.md");
        using WorkspaceViewModel workspace = Workspace(target, confirm: true);
        workspace.BasesExportPublishingForTests = _ => File.WriteAllBytes(target, Theirs);

        Export(workspace, "md");

        AssertRefusedWithTheirBytes(target, "the file appeared after you chose it; nothing was replaced");
        Assert.Empty(_confirmed);
    }

    [Fact]
    public void ADeclinedOverwriteWritesNothing()
    {
        byte[] old = "old export\n"u8.ToArray();
        string target = Seed("export.md", old);
        using WorkspaceViewModel workspace = Workspace(target, confirm: false);

        Export(workspace, "md");

        Assert.Equal(old, File.ReadAllBytes(target));
        Assert.Equal([target], _confirmed);
        Assert.DoesNotContain(_announced, e => e is A11yEvent.BasesViewExported or A11yEvent.BasesViewExportFailed);
    }

    [Fact]
    public void AnUnobservableDestinationIsNeverReplaced()
    {
        byte[] invalid = [0xFF, 0xFE, 0x00];
        string target = Seed("export.md", invalid);
        using WorkspaceViewModel workspace = Workspace(target, confirm: true);

        Export(workspace, "md");

        Assert.Equal(invalid, File.ReadAllBytes(target));
        Assert.Empty(_confirmed);
        Assert.Single(_announced, e => e is A11yEvent.BasesViewExportFailed);
        Assert.DoesNotContain(_announced, e => e is A11yEvent.BasesViewExported);
    }

    [Fact]
    public void AFailedPublishLeavesTheExistingExportIntact()
    {
        string name = $"export-{Guid.NewGuid():N}.md";
        byte[] old = "old export\n"u8.ToArray();
        string target = Seed(name, old);
        using WorkspaceViewModel workspace = Workspace(target, confirm: true);
        string? prior = Environment.GetEnvironmentVariable("SLATE_TEST_FAULT_PRE_WRITE");
        try
        {
            Environment.SetEnvironmentVariable("SLATE_TEST_FAULT_PRE_WRITE", name);
            Export(workspace, "md");
        }
        finally
        {
            Environment.SetEnvironmentVariable("SLATE_TEST_FAULT_PRE_WRITE", prior);
        }

        Assert.Equal(old, File.ReadAllBytes(target));
        Assert.Single(_announced, e => e is A11yEvent.BasesViewExportFailed);
        Assert.DoesNotContain(_announced, e => e is A11yEvent.BasesViewExported);
    }

    [Fact]
    public void AnExportToANewVaultFileIsCreatedThroughCore()
    {
        string target = Path.Combine(_root, "Exports", "fresh.csv");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        using WorkspaceViewModel workspace = Workspace(target, confirm: true);
        string expected = ExpectedText(workspace, "csv");

        Export(workspace, "csv");

        Assert.Equal(expected, File.ReadAllText(target));
        Assert.Single(_announced, e => e is A11yEvent.BasesViewExported);
        Assert.Empty(_confirmed);
        // The session's own write indexed it: no scan has run since.
        Assert.NotNull(IndexedHash("Exports/fresh.csv"));
    }

    [Fact]
    public void AnExportOutsideTheVaultStillWritesDirectly()
    {
        string outside = Path.Combine(Path.GetTempPath(), $"slate-windows-bases-export-out-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outside);
        try
        {
            string fresh = Path.Combine(outside, "fresh.md");
            using (WorkspaceViewModel workspace = Workspace(fresh, confirm: true))
            {
                Export(workspace, "md");
                Assert.Equal(ExpectedText(workspace, "md"), File.ReadAllText(fresh));
                Assert.Single(_announced, e => e is A11yEvent.BasesViewExported);
            }
            Assert.Empty(_confirmed);

            string existing = Path.Combine(outside, "existing.md");
            File.WriteAllText(existing, "old\n");
            using (WorkspaceViewModel workspace = Workspace(existing, confirm: true))
            {
                Export(workspace, "md");
                Assert.Equal(ExpectedText(workspace, "md"), File.ReadAllText(existing));
                Assert.Single(_announced, e => e is A11yEvent.BasesViewExported);
            }
            Assert.Equal([existing], _confirmed);
        }
        finally
        {
            Directory.Delete(outside, recursive: true);
        }
    }

    /// <summary>Red team: a dot-prefixed folder is hidden from the scanner,
    /// so its write is no watcher noise, and core refuses it as a save path.
    /// The export writes it directly, as before WB-12.</summary>
    [Fact]
    public void AnExportIntoAHiddenVaultFolderWritesDirectly()
    {
        string target = Path.Combine(_root, ".exports", "x.md");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        using WorkspaceViewModel workspace = Workspace(target, confirm: true);
        string expected = ExpectedText(workspace, "md");

        Export(workspace, "md");

        Assert.Equal(expected, File.ReadAllText(target));
        Assert.Single(_announced, e => e is A11yEvent.BasesViewExported);
        Assert.Empty(_confirmed);
    }

    /// <summary>Red team: with the production scheduling, the export's core
    /// call is tracked into the workspace's teardown drain, so Dispose waits
    /// it out before the session can go; and its continuation, landing after
    /// Dispose, neither asks nor announces.</summary>
    [Fact]
    public void AnExportStillPublishingAtCloseIsDrainedAndSaysNothingAfter() => StaThread.RunPumped(() =>
    {
        string target = Path.Combine(_root, "export.md");
        var workspace = new WorkspaceViewModel(
            _session, _root, () => [], _announced.Add, startInteractionBackgroundWork: true);
        workspace.OpenPath("Status.base");
        BaseDocumentViewModel document = Assert.IsType<BaseDocumentViewModel>(workspace.ActiveBaseDocument);
        Assert.True(
            PumpedDispatcher.PumpUntil(() => document.State == BaseLoadState.Ready, TimeSpan.FromSeconds(10)),
            "the base never loaded");
        workspace.PickBasesExportDestination = (_, _) => target;
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        workspace.BasesExportPublishingForTests = relative =>
        {
            entered.Set();
            _ = release.Wait(TimeSpan.FromSeconds(10));
        };
        _announced.Clear();

        workspace.BasesExportMarkdownCommand.Execute(null);
        Assert.True(
            PumpedDispatcher.PumpUntil(() => entered.IsSet, TimeSpan.FromSeconds(10)),
            "the publication never started");
        _ = Task.Run(() =>
        {
            Thread.Sleep(TimeSpan.FromSeconds(1));
            release.Set();
        });
        workspace.Dispose();

        Assert.True(release.IsSet, "Dispose returned while the export's core call was in flight");
        PumpedDispatcher.Drain();
        Assert.True(File.Exists(target), "the publication the drain waited for did not land");
        Assert.DoesNotContain(_announced, e => e is A11yEvent.BasesViewExported or A11yEvent.BasesViewExportFailed);
    }, TimeSpan.FromSeconds(60), "STA test body timed out.");

    private string? IndexedHash(string relative)
    {
        using var cancel = new CancelToken();
        return Assert.Single(_session.IndexedContentHashes([relative], cancel));
    }

    private void Scan()
    {
        using var cancel = new CancelToken();
        _ = _session.Rescan(cancel);
    }

    /// <summary>A file at the vault's top level, indexed.</summary>
    private string Seed(string name, byte[] bytes)
    {
        string path = Path.Combine(_root, name);
        File.WriteAllBytes(path, bytes);
        Scan();
        return path;
    }

    /// <summary>A headless workspace on Status.base whose export picks
    /// <paramref name="target"/> and whose overwrite question records the
    /// path and answers <paramref name="confirm"/>.</summary>
    private WorkspaceViewModel Workspace(string target, bool confirm)
    {
        var workspace = new WorkspaceViewModel(
            _session, _root, () => [], _announced.Add, startInteractionBackgroundWork: false);
        workspace.OpenPath("Status.base");
        Assert.Equal(BaseLoadState.Ready, Assert.IsType<BaseDocumentViewModel>(workspace.ActiveBaseDocument).State);
        workspace.PickBasesExportDestination = (_, _) => target;
        workspace.ConfirmBasesExportOverwrite = path =>
        {
            _confirmed.Add(path);
            return confirm;
        };
        _announced.Clear();
        return workspace;
    }

    private static void Export(WorkspaceViewModel workspace, string extension) =>
        (extension == "csv" ? workspace.BasesExportCsvCommand : workspace.BasesExportMarkdownCommand).Execute(null);

    private static string ExpectedText(WorkspaceViewModel workspace, string extension)
    {
        string? text = null;
        Assert.IsType<BaseDocumentViewModel>(workspace.ActiveBaseDocument).ExportText(
            extension == "csv" ? ExportFormat.Csv : ExportFormat.Markdown, includeQuickFilter: true, exported => text = exported);
        return Assert.IsType<string>(text);
    }

    private void AssertRefusedWithTheirBytes(string target, string reason)
    {
        Assert.Equal(Theirs, File.ReadAllBytes(target));
        A11yEvent.BasesViewExportFailed failed = Assert.IsType<A11yEvent.BasesViewExportFailed>(
            Assert.Single(_announced, e => e is A11yEvent.BasesViewExportFailed));
        Assert.Equal(reason, failed.Detail);
        Assert.DoesNotContain(_announced, e => e is A11yEvent.BasesViewExported);
    }
}
