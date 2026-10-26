// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.Tools;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using uniffi.slate_uniffi;

namespace SlateWindows.AccessibilityTests;

public sealed partial class ShellAccessibilityTests
{
    /// <summary>
    /// #1252 (R-9, OD-1): a file created outside Slate appears after one
    /// Files Sidebar → Refresh — in the tree and in Quick Open — an open
    /// note changed outside Slate shows its new content after the next, and
    /// a deleted file leaves the tree after the one after that.
    /// </summary>
    /// <remarks>
    /// Each Refresh is heard through PR 1's desktop-scoped notification
    /// listener (#1244, R-1: NVDA's registration shape, on the desktop root)
    /// as exactly one line, core's rendering of its
    /// <c>VaultRescanFinished</c>, and nothing else — no scan-family line, no
    /// "missing from disk" line: "Files refreshed. 1 new or changed, 0
    /// removed." after the first Refresh and after the second (the open
    /// note's change), and "Files refreshed. 0 new or changed, 1 removed."
    /// after the third. The hosted <c>RescanTests</c> facts pin the same
    /// sentences through the real lifecycle.
    /// </remarks>
    [Fact]
    public void ExternalFiles_AppearAfterRefresh()
    {
        string testRoot = Path.Combine(
            Path.GetTempPath(), $"slate-external-files-{Guid.NewGuid():N}");
        string vaultRoot = Path.Combine(testRoot, "External Files Vault");
        string logDirectory = Path.Combine(testRoot, "logs");
        Directory.CreateDirectory(vaultRoot);
        string notePath = Path.Combine(vaultRoot, "note.md");
        string latePath = Path.Combine(vaultRoot, "late.md");
        File.WriteAllText(notePath, "# Note\n\nOriginal body.\n");

        string logFile = Path.Combine(logDirectory, "slate-windows.log");
        var heard = new ConcurrentQueue<ReceivedNotification>();
        RenderedAnnouncement Refreshed(ulong changed, ulong removed) => SlateUniffiMethods.A11yRender(
            new A11yEvent.VaultRescanFinished(RescanReason.Explicit, changed, removed));

        Process? process = null;
        // #1326: the evidence records what happened, never a capture that did not.
        string registered = NotRegistered;
        ListenerOutcome outcome = ListenerOutcome.Aborted;
        try
        {
            var startInfo = new ProcessStartInfo(SlateWindowsExe()) { UseShellExecute = false };
            ShellLaunch.ApplyDeviceState(startInfo);
            startInfo.ArgumentList.Add(vaultRoot);
            startInfo.Environment["SLATE_CENSUS_INSTANCE_ID"] =
                $"slate-external-files-{Guid.NewGuid():N}";
            startInfo.Environment["SLATE_LOG_DIR"] = logDirectory;
            // The announcement dispatcher's R-1 diagnostics, for the listener's
            // failure message and the evidence artifact.
            startInfo.Environment["SLATE_UIA_DIAGNOSTICS"] = "1";
            // Plan 43 PR 20b (#1267, design §4.7): this journey counts what
            // each Refresh found, so core's vault watcher stays off — a
            // Watcher run could index the file first and leave the Refresh
            // saying "No changes". ExternalFiles_AppearWithoutRefresh runs
            // the real watcher.
            startInfo.Environment[VaultWatcherSwitch] = "1";
            process = Process.Start(startInfo)
                ?? throw new Xunit.Sdk.XunitException("SlateWindows.exe did not start.");
            if (!HasInteractiveDesktop(process, "external-files"))
            {
                return;
            }

            using var automation = new UIA3Automation();
            Window window = WaitForMainWindow(
                process,
                automation,
                logFile,
                TimeSpan.FromSeconds(30));
            // PR 1's desktop-scoped notification listener (#1244; NVDA's
            // registration shape), registered once the window exists and kept
            // for every leg below; the legs start once Slate's launch has
            // settled (R-1 — its lines are the launch journey's to witness).
            using DesktopNotificationListener listener = ListenOnTheDesktop(automation, heard);
            (registered, outcome) = (RegisteredAfterTheWindow, ListenerOutcome.Failed);
            window.SetForeground();
            WaitForVaultOpen(window);
            AutomationElement filesTree = WaitForElement(window, "FilesTree", TimeSpan.FromSeconds(10));

            // The note is open in the editor before anything changes on disk.
            _ = SelectTreeItem(window, automation, "note");
            _ = WaitForEditor(window, automation, "note.md editor", TimeSpan.FromSeconds(10));
            WaitForEditorText(window, automation, "note.md editor", "Original body.");

            // --- leg 1: a file created outside Slate --------------------------
            int leg1 = LaunchSettledMark(heard, process.Id, logFile);
            File.WriteAllText(latePath, "# Late\n");
            InvokeFilesSidebarRefreshFromTheMenu(window);

            _ = WaitForTreeItemStartingWith(filesTree, automation, "late");
            // Exactly one line since the Refresh — no "Scanning vault…" or
            // "Scan complete…" (a rescan never speaks the scan family).
            AwaitHeardSince(heard, process.Id, leg1, [Refreshed(1, 0)], TimeSpan.FromSeconds(15), logFile);
            AssertQuickOpenFinds(window, automation, "late", "late");
            // W7-7 PR 3 (#1246, R-4): the rows the rescan republished — the
            // tree's and every list's — are named speakably, never by a .NET
            // type name or a record dump.
            AssertItemNamesAreSpeakable(process, "rescan-refreshed-files");

            // --- leg 2: the OPEN note changed outside Slate --------------------
            int leg2 = QuietMark(heard, process.Id);
            DateTime before = File.GetLastWriteTimeUtc(notePath);
            File.WriteAllText(notePath, "# Note\n\nChanged outside Slate.\n");
            File.SetLastWriteTimeUtc(notePath, before.AddSeconds(5));
            InvokeRefreshButton(window);

            WaitForEditorText(window, automation, "note.md editor", "Changed outside Slate.");
            Assert.DoesNotContain(
                "Original body.",
                EditorText(window, automation, "note.md editor"),
                StringComparison.Ordinal);

            // One line, once, and no "missing from disk" line.
            AwaitHeardSince(heard, process.Id, leg2, [Refreshed(1, 0)], TimeSpan.FromSeconds(15), logFile);

            // --- leg 3: a file deleted outside Slate ---------------------------
            int leg3 = QuietMark(heard, process.Id);
            File.Delete(latePath);
            InvokeRefreshButton(window);

            Assert.True(
                SpinWait.SpinUntil(
                    () => !TreeListsItemStartingWith(filesTree, automation, "late"),
                    TimeSpan.FromSeconds(15)),
                "late.md stayed in the files tree after it was deleted and Refresh ran.");

            // Once — the announcement counts the removal.
            AwaitHeardSince(heard, process.Id, leg3, [Refreshed(0, 1)], TimeSpan.FromSeconds(15), logFile);
            outcome = ListenerOutcome.Completed;
        }
        finally
        {
            int slateId = process?.Id ?? -1;
            WriteAnnouncementEvidence(
                "rescan",
                registered,
                outcome,
                HeardFrom(heard, slateId),
                heard.Count(notification => notification.ProcessId != slateId),
                AnnouncementDiagnostics(logFile));
            if (process is not null && !process.HasExited)
            {
                process.CloseMainWindow();
                if (!process.WaitForExit(5_000))
                {
                    process.Kill(entireProcessTree: true);
                }
            }

            try
            {
                Directory.Delete(testRoot, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    /// <summary>
    /// Plan 43 PR 20b (#1267, design §4.7): core's vault watcher, end to end —
    /// ReadDirectoryChangesW, core's coalescer, the listener, the lifecycle's
    /// Watcher run. With the main window active and no Refresh, a file created
    /// outside Slate appears in the tree and in Quick Open, heard as exactly
    /// one line, core's rendering of a Watcher run's
    /// <c>VaultRescanFinished</c> (the shared "Files refreshed. 1 new or
    /// changed, 0 removed."), and the app's log names the run's route. The
    /// run's reconcile keeps every row it did not change — the note's row,
    /// held since before the change, is still one the tree shows — and the
    /// window scans clean right after it. Then the open note changed outside
    /// Slate shows its new content, and nothing is said: a second watcher
    /// sentence inside the minute is silent.
    /// </summary>
    /// <remarks>
    /// The window is brought forward once, at the start, and never again
    /// before the changes are shown: a watcher request while it is away waits
    /// for the next activation (§4.5), and that activation's foreground rescan
    /// would find the changes too — which is why the log must name a Watcher
    /// run.
    /// </remarks>
    [Fact]
    public void ExternalFiles_AppearWithoutRefresh()
    {
        string testRoot = Path.Combine(
            Path.GetTempPath(), $"slate-watched-files-{Guid.NewGuid():N}");
        string vaultRoot = Path.Combine(testRoot, "Watched Vault");
        string logDirectory = Path.Combine(testRoot, "logs");
        Directory.CreateDirectory(vaultRoot);
        string notePath = Path.Combine(vaultRoot, "note.md");
        string latePath = Path.Combine(vaultRoot, "late.md");
        File.WriteAllText(notePath, "# Note\n\nOriginal body.\n");

        string logFile = Path.Combine(logDirectory, "slate-windows.log");
        var heard = new ConcurrentQueue<ReceivedNotification>();
        RenderedAnnouncement watcherLine = SlateUniffiMethods.A11yRender(
            new A11yEvent.VaultRescanFinished(RescanReason.Watcher, 1, 0));

        Process? process = null;
        // #1326: the evidence records what happened, never a capture that did not.
        string registered = NotRegistered;
        ListenerOutcome outcome = ListenerOutcome.Aborted;
        try
        {
            var startInfo = new ProcessStartInfo(SlateWindowsExe()) { UseShellExecute = false };
            ShellLaunch.ApplyDeviceState(startInfo);
            startInfo.ArgumentList.Add(vaultRoot);
            startInfo.Environment["SLATE_CENSUS_INSTANCE_ID"] =
                $"slate-watched-files-{Guid.NewGuid():N}";
            startInfo.Environment["SLATE_LOG_DIR"] = logDirectory;
            // The dispatcher's R-1 diagnostics and the rescan's route line.
            startInfo.Environment["SLATE_UIA_DIAGNOSTICS"] = "1";
            // The real watcher, whatever the runner's environment holds.
            _ = startInfo.Environment.Remove(VaultWatcherSwitch);
            process = Process.Start(startInfo)
                ?? throw new Xunit.Sdk.XunitException("SlateWindows.exe did not start.");
            if (!HasInteractiveDesktop(process, "watched-files"))
            {
                return;
            }

            using var automation = new UIA3Automation();
            Window window = WaitForMainWindow(
                process,
                automation,
                logFile,
                TimeSpan.FromSeconds(30));
            using DesktopNotificationListener listener = ListenOnTheDesktop(automation, heard);
            (registered, outcome) = (RegisteredAfterTheWindow, ListenerOutcome.Failed);
            window.SetForeground();
            WaitForVaultOpen(window);
            AutomationElement filesTree = WaitForElement(window, "FilesTree", TimeSpan.FromSeconds(10));
            AutomationElement noteRow = SelectTreeItem(window, automation, "note");
            _ = WaitForEditor(window, automation, "note.md editor", TimeSpan.FromSeconds(10));
            WaitForEditorText(window, automation, "note.md editor", "Original body.");
            string noteRowName = noteRow.Name;

            // --- leg 1: a file created outside Slate, and no Refresh ----------
            int leg1 = LaunchSettledMark(heard, process.Id, logFile);
            File.WriteAllText(latePath, "# Late\n");
            Assert.True(
                SpinWait.SpinUntil(
                    () => TreeListedItemStartingWith(filesTree, automation, "late"),
                    TimeSpan.FromSeconds(30)),
                "late.md never reached the files tree without a Refresh. Rescans: "
                + string.Join(" | ", DiagnosticLines(logFile, "VaultRescanStarted")));
            AwaitHeardSince(heard, process.Id, leg1, [watcherLine], TimeSpan.FromSeconds(15), logFile);
            string[] runs = DiagnosticLines(logFile, "VaultRescanStarted");
            Assert.True(
                runs.Contains("SlateWindows.VaultRescanStarted (Watcher)") && !runs.Any(run => run.EndsWith("(Explicit)", StringComparison.Ordinal)),
                "the file must arrive through a Watcher run, with no Refresh; the log names: " + string.Join(" | ", runs));
            // Plan 43 PR 20b (the shell gate, 2026-10-18): the reconcile kept
            // the note's row — a reconcile that replaced it left this element
            // dark, as the Templates journey's scan and the shell journey's
            // SetFocus met it — and right after the reconcile the window scans
            // clean, the item-name census (W7-7 PR 3, R-4) included.
            AssertHeldRowStillShown(noteRow, noteRowName);
            AssertAxeClean(process, "rescan-watcher-files");
            AssertQuickOpenFinds(window, automation, "late", "late");

            // --- leg 2: the OPEN note changed outside Slate, inside the minute -
            int leg2 = QuietMark(heard, process.Id);
            DateTime before = File.GetLastWriteTimeUtc(notePath);
            File.WriteAllText(notePath, "# Note\n\nChanged outside Slate.\n");
            File.SetLastWriteTimeUtc(notePath, before.AddSeconds(5));
            WaitForEditorText(window, automation, "note.md editor", "Changed outside Slate.");
            Assert.True(
                QuietMark(heard, process.Id) == leg2,
                "a second watcher run inside the sentence window spoke: "
                + string.Join(" | ", HeardFrom(heard, process.Id).Skip(leg2).Select(line => line.DisplayString)));
            runs = DiagnosticLines(logFile, "VaultRescanStarted");
            Assert.True(
                runs.Count(run => run == "SlateWindows.VaultRescanStarted (Watcher)") >= 2
                    && !runs.Any(run => run.EndsWith("(Explicit)", StringComparison.Ordinal)),
                "the open note's change must arrive through a second Watcher run; the log names: "
                + string.Join(" | ", runs));
            outcome = ListenerOutcome.Completed;
        }
        finally
        {
            int slateId = process?.Id ?? -1;
            WriteAnnouncementEvidence(
                "rescan-watcher",
                registered,
                outcome,
                HeardFrom(heard, slateId),
                heard.Count(notification => notification.ProcessId != slateId),
                AnnouncementDiagnostics(logFile));
            if (process is not null && !process.HasExited)
            {
                process.CloseMainWindow();
                if (!process.WaitForExit(5_000))
                {
                    process.Kill(entireProcessTree: true);
                }
            }

            try
            {
                Directory.Delete(testRoot, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    /// <summary>
    /// #1515 (a change under an open folder): the reader on a note inside an
    /// open folder, and a watcher run — no Refresh — for the note beside it,
    /// retitled and rewritten outside Slate. The reader's row is the element
    /// held before the run, with the keys and no focus change; the changed
    /// note's row is the same element too, renamed in place, and so is every
    /// other row; and the window scans clean right after the run.
    /// </summary>
    /// <remarks>Until #1515 a change anywhere under an open folder replaced
    /// the folder's rows, and the reader's with them: with the selected row
    /// among them the whole tree was replaced, the held element answered
    /// UIA_E_ELEMENTNOTAVAILABLE, and the keys went to the new row.</remarks>
    [Fact]
    public void ExternalFiles_AChangeInsideTheReadersOpenFolderKeepsTheirRow() =>
        AssertAWatcherRunKeepsTheReadersRow(
            journey: "watched-open-folder",
            seed: vault =>
            {
                Directory.CreateDirectory(Path.Combine(vault, "Folder"));
                File.WriteAllText(Path.Combine(vault, "alpha.md"), "# Alpha\n");
                File.WriteAllText(Path.Combine(vault, "note.md"), "# Note\n");
                File.WriteAllText(Path.Combine(vault, "Folder", "child.md"), "# Child note\n");
                File.WriteAllText(Path.Combine(vault, "Folder", "sibling.md"), "# Sibling\n");
            },
            openFolders: ["Folder"],
            reader: "child.md",
            held: [("alpha.md", "alpha.md"), ("Folder", "Folder"), ("sibling.md", "sibling retitled"), ("note.md", "note.md")],
            change: vault => File.WriteAllText(
                Path.Combine(vault, "Folder", "sibling.md"),
                "---\ntitle: sibling retitled\n---\n\n# Sibling\n\nChanged outside Slate, beside the reader's note.\n"),
            arrival: "sibling retitled",
            surface: "rescan-watcher-open-folder");

    /// <summary>
    /// #1515 (a kept open folder's recycled container): the reader on a note
    /// inside an open folder, another open folder last in the tree, and a
    /// watcher run for a note created outside Slate that sorts first. Every
    /// row is the element held before the run — the last folder's rows
    /// included, whose container WPF's recycling panel used to recycle on an
    /// insertion above it — and the reader's row keeps the keys, with no focus
    /// change; the window scans clean right after the run.
    /// </summary>
    [Fact]
    public void ExternalFiles_ARowCreatedAtTheTopKeepsEveryOpenFoldersRows() =>
        AssertAWatcherRunKeepsTheReadersRow(
            journey: "watched-row-at-top",
            seed: vault =>
            {
                Directory.CreateDirectory(Path.Combine(vault, "Folder"));
                Directory.CreateDirectory(Path.Combine(vault, "Zebra"));
                File.WriteAllText(Path.Combine(vault, "alpha.md"), "# Alpha\n");
                File.WriteAllText(Path.Combine(vault, "note.md"), "# Note\n");
                File.WriteAllText(Path.Combine(vault, "Folder", "child.md"), "# Child note\n");
                File.WriteAllText(Path.Combine(vault, "Folder", "sibling.md"), "# Sibling\n");
                File.WriteAllText(Path.Combine(vault, "Zebra", "spot.md"), "# Spot\n");
                File.WriteAllText(Path.Combine(vault, "Zebra", "stripe.md"), "# Stripe\n");
            },
            openFolders: ["Folder", "Zebra"],
            reader: "child.md",
            held:
            [
                ("alpha.md", "alpha.md"),
                ("Folder", "Folder"),
                ("sibling.md", "sibling.md"),
                ("note.md", "note.md"),
                ("Zebra", "Zebra"),
                ("spot.md", "spot.md"),
                ("stripe.md", "stripe.md"),
            ],
            change: vault => File.WriteAllText(Path.Combine(vault, "0-top.md"), "# Top\n"),
            arrival: "0-top.md",
            surface: "rescan-watcher-row-at-top");

    /// <summary>
    /// Plan 44 WB-6 (#1522): the shared journey with Group by date on (the
    /// vault's own sidebar settings, newest first), the reader on a note in
    /// "Today", and a watcher run for a note created outside Slate, which
    /// joins "Today" at its top. The reader's row and the "Today" header are
    /// the elements held before the run, and so is every other row; the keys
    /// never moved, and the window scans clean right after. Until WB-6 no date
    /// group was kept, so every header and every row under one had a new
    /// element on every run.
    /// </summary>
    [Fact]
    public void ExternalFiles_AChangeUnderADateGroupKeepsTheReadersRow() =>
        AssertAWatcherRunKeepsTheReadersRow(
            journey: "watched-date-group",
            seed: vault =>
            {
                SeedDateGroupedSidebar(vault);
                File.WriteAllText(Path.Combine(vault, "note.md"), "# Note\n");
                File.WriteAllText(Path.Combine(vault, "alpha.md"), "# Alpha\n");
                File.WriteAllText(Path.Combine(vault, "beta.md"), "# Beta\n");
            },
            openFolders: [],
            reader: "alpha.md",
            held: [("Today", "Today"), ("note.md", "note.md"), ("beta.md", "beta.md")],
            change: vault => File.WriteAllText(Path.Combine(vault, "0-top.md"), "# Top\n"),
            arrival: "0-top.md",
            surface: "rescan-watcher-date-group",
            firstRow: "Today");

    /// <summary>The vault's own sidebar settings, grouped by date and newest
    /// first, written before the launch reads them.</summary>
    private static void SeedDateGroupedSidebar(string vault)
    {
        Directory.CreateDirectory(Path.Combine(vault, ".slate"));
        File.WriteAllText(
            Path.Combine(vault, ".slate", "sidebar.json"),
            "{\"version\":1,\"sort\":{\"field\":\"modified\",\"direction\":\"desc\"},\"grouping\":\"dateBuckets\"}");
    }

    /// <summary>
    /// Plan 44 WB-6 (#1515 case 3): the reader on b.md, and b.md deleted
    /// outside Slate — a watcher run, no Refresh. The keys are on c.md's row,
    /// which is not selected; the tree selects nothing; no tab opens (the tab
    /// strip holds what it held); and the window scans clean right after.
    /// Until WB-6 a publication that removed the selected row replaced the
    /// whole tree, because WPF's tree selects its first row — and opens its
    /// note — when the selected container leaves in place.
    /// </summary>
    [Fact]
    public void ExternalFiles_TheReadersRowRemovedUnderThemLandsTheKeysBeside()
    {
        string testRoot = Path.Combine(Path.GetTempPath(), $"slate-watched-removed-reader-{Guid.NewGuid():N}");
        string vaultRoot = Path.Combine(testRoot, "Watched Vault");
        string logDirectory = Path.Combine(testRoot, "logs");
        Directory.CreateDirectory(vaultRoot);
        File.WriteAllText(Path.Combine(vaultRoot, "alpha.md"), "# Alpha\n");
        File.WriteAllText(Path.Combine(vaultRoot, "b.md"), "# B\n");
        File.WriteAllText(Path.Combine(vaultRoot, "c.md"), "# C\n");
        string logFile = Path.Combine(logDirectory, "slate-windows.log");

        Process? process = null;
        try
        {
            var startInfo = new ProcessStartInfo(SlateWindowsExe()) { UseShellExecute = false };
            ShellLaunch.ApplyDeviceState(startInfo);
            startInfo.ArgumentList.Add(vaultRoot);
            startInfo.Environment["SLATE_CENSUS_INSTANCE_ID"] = $"slate-watched-removed-reader-{Guid.NewGuid():N}";
            startInfo.Environment["SLATE_LOG_DIR"] = logDirectory;
            // The rescan's route line.
            startInfo.Environment["SLATE_UIA_DIAGNOSTICS"] = "1";
            // The real watcher, whatever the runner's environment holds.
            _ = startInfo.Environment.Remove(VaultWatcherSwitch);
            process = Process.Start(startInfo)
                ?? throw new Xunit.Sdk.XunitException("SlateWindows.exe did not start.");
            if (!HasInteractiveDesktop(process, "watched-removed-reader"))
            {
                return;
            }

            using var automation = new UIA3Automation();
            Window window = WaitForMainWindow(process, automation, logFile, TimeSpan.FromSeconds(30));
            window.SetForeground();
            WaitForVaultOpen(window);
            AutomationElement tree = WaitForElement(window, "FilesTree", TimeSpan.FromSeconds(10));
            AssertFocusStaysOnRow(automation, tree, "alpha.md", "Opening the vault did not land the keys on the Files tree's first row.");
            WaitForTreeItemStartingWith(tree, automation, "b.md").Focus();
            _ = WaitForEditor(window, automation, "b.md editor", TimeSpan.FromSeconds(10));
            AssertFocusStaysOnRow(automation, tree, "b.md", "The b.md row did not keep the keys before the change.");
            AutomationElement tabs = WaitForElement(window, "WorkspaceTabs", TimeSpan.FromSeconds(10));
            string[] tabsBefore = TabNames(tabs, automation);

            File.Delete(Path.Combine(vaultRoot, "b.md"));
            Assert.True(
                SpinWait.SpinUntil(() => !TreeListedItemStartingWith(tree, automation, "b.md"), TimeSpan.FromSeconds(30)),
                "the deletion never reached the files tree without a Refresh. Rescans: "
                + string.Join(" | ", DiagnosticLines(logFile, "VaultRescanStarted")));
            Assert.True(
                SpinWait.SpinUntil(
                    () => automation.FocusedElement() is { } focused
                        && (focused.Properties.Name.ValueOrDefault ?? string.Empty).StartsWith("c.md", StringComparison.Ordinal),
                    TimeSpan.FromSeconds(10)),
                $"the keys did not land on c.md's row; they are on {DescribeFocus(automation)}.");
            // Any focus event the publication raised arrives.
            Thread.Sleep(FocusSettle);

            string[] runs = DiagnosticLines(logFile, "VaultRescanStarted");
            Assert.True(
                runs.Contains("SlateWindows.VaultRescanStarted (Watcher)") && !runs.Any(run => run.EndsWith("(Explicit)", StringComparison.Ordinal)),
                "the deletion must arrive through a Watcher run, with no Refresh; the log names: " + string.Join(" | ", runs));
            AutomationElement landed = automation.FocusedElement();
            Assert.StartsWith("c.md", landed.Properties.Name.Value, StringComparison.Ordinal);
            Assert.False(landed.Patterns.SelectionItem.Pattern.IsSelected.Value, "the landing selected c.md's row.");
            Assert.Empty(tree.Patterns.Selection.Pattern.Selection.Value);
            Assert.Equal(tabsBefore, TabNames(tabs, automation));
            // Right after the landing, the item-name census included.
            AssertAxeClean(process, "rescan-watcher-removed-reader");
        }
        finally
        {
            if (process is not null && !process.HasExited)
            {
                process.CloseMainWindow();
                if (!process.WaitForExit(5_000))
                {
                    process.Kill(entireProcessTree: true);
                }
            }

            try
            {
                Directory.Delete(testRoot, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    /// <summary>The workspace tab strip's tab names, in order.</summary>
    private static string[] TabNames(AutomationElement tabs, UIA3Automation automation) =>
    [
        .. tabs.FindAllDescendants(automation.ConditionFactory.ByControlType(ControlType.TabItem))
            .Select(tab => tab.Properties.Name.ValueOrDefault ?? string.Empty),
    ];

    /// <summary>#1515: launch the vault <paramref name="seed"/> writes, open
    /// <paramref name="openFolders"/>, put the keys on
    /// <paramref name="reader"/>'s row and hold its element and each
    /// <paramref name="held"/> row's (found by its name before the run, and
    /// after it). Then make <paramref name="change"/> outside Slate and, once
    /// <paramref name="arrival"/>'s row shows the watcher run's publication,
    /// require every held element to be the one the tree shows, the reader's
    /// with the keys and no focus change since the change, and scan the
    /// window as <paramref name="surface"/>.</summary>
    private static void AssertAWatcherRunKeepsTheReadersRow(
        string journey,
        Action<string> seed,
        string[] openFolders,
        string reader,
        (string Before, string After)[] held,
        Action<string> change,
        string arrival,
        string surface,
        string firstRow = "alpha.md")
    {
        string testRoot = Path.Combine(Path.GetTempPath(), $"slate-{journey}-{Guid.NewGuid():N}");
        string vaultRoot = Path.Combine(testRoot, "Watched Vault");
        string logDirectory = Path.Combine(testRoot, "logs");
        Directory.CreateDirectory(vaultRoot);
        seed(vaultRoot);
        string logFile = Path.Combine(logDirectory, "slate-windows.log");

        Process? process = null;
        try
        {
            var startInfo = new ProcessStartInfo(SlateWindowsExe()) { UseShellExecute = false };
            ShellLaunch.ApplyDeviceState(startInfo);
            startInfo.ArgumentList.Add(vaultRoot);
            startInfo.Environment["SLATE_CENSUS_INSTANCE_ID"] = $"slate-{journey}-{Guid.NewGuid():N}";
            startInfo.Environment["SLATE_LOG_DIR"] = logDirectory;
            // The rescan's route line.
            startInfo.Environment["SLATE_UIA_DIAGNOSTICS"] = "1";
            // The real watcher, whatever the runner's environment holds.
            _ = startInfo.Environment.Remove(VaultWatcherSwitch);
            process = Process.Start(startInfo)
                ?? throw new Xunit.Sdk.XunitException("SlateWindows.exe did not start.");
            if (!HasInteractiveDesktop(process, journey))
            {
                return;
            }

            using var automation = new UIA3Automation();
            Window window = WaitForMainWindow(
                process,
                automation,
                logFile,
                TimeSpan.FromSeconds(30));
            window.SetForeground();
            WaitForVaultOpen(window);
            AutomationElement tree = WaitForElement(window, "FilesTree", TimeSpan.FromSeconds(10));
            // The launch lands the keys on the first row once the vault has
            // published its rows (W7-5); a step before that races it. Each
            // journey's vault sorts alpha.md first, or, grouped by date, the
            // Today header.
            AssertFocusStaysOnRow(automation, tree, firstRow, "Opening the vault did not land the keys on the Files tree's first row.");
            foreach (string folder in openFolders)
            {
                AutomationElement folderRow = WaitForTreeItemStartingWith(tree, automation, folder, requireItems: true);
                folderRow.Patterns.ExpandCollapse.Pattern.Expand();
                Assert.True(
                    SpinWait.SpinUntil(
                        () => folderRow.Patterns.ExpandCollapse.Pattern.ExpandCollapseState.Value == ExpandCollapseState.Expanded,
                        TimeSpan.FromSeconds(10)),
                    $"the {folder} row did not open.");
            }

            // The keys on the reader's row: its selection shows the note,
            // and the keys stay on the row (W7-7 R-2).
            WaitForTreeItemStartingWith(tree, automation, reader).Focus();
            _ = WaitForEditor(window, automation, $"{reader} editor", TimeSpan.FromSeconds(10));
            AssertFocusStaysOnRow(automation, tree, reader, $"The {reader} row did not keep the keys before the change.");
            AutomationElement readerRow = WaitForTreeItemStartingWith(tree, automation, reader);
            string readerName = readerRow.Name;
            int[] readerId = readerRow.Properties.RuntimeId.Value;
            var rows = new List<(string After, AutomationElement Row, int[] Id)>();
            foreach ((string before, string after) in held)
            {
                AutomationElement element = WaitForTreeItemStartingWith(tree, automation, before);
                rows.Add((after, element, element.Properties.RuntimeId.Value));
            }

            var focusMoves = new ConcurrentQueue<string>();
            int slate = process.Id;
            var recorder = automation.RegisterFocusChangedEvent(element =>
            {
                try
                {
                    if (element.Properties.ProcessId.ValueOrDefault == slate)
                    {
                        focusMoves.Enqueue($"{element.Properties.ControlType.ValueOrDefault} '{element.Properties.Name.ValueOrDefault}'");
                    }
                }
                catch (Exception exception) when (IsTransientUiaFault(exception))
                {
                    focusMoves.Enqueue("<unreadable>");
                }
            });
            try
            {
                // Registration settles; nothing before the change is the run's.
                Thread.Sleep(TimeSpan.FromMilliseconds(500));
                focusMoves.Clear();
                change(vaultRoot);
                Assert.True(
                    SpinWait.SpinUntil(
                        () => TreeListedItemStartingWith(tree, automation, arrival),
                        TimeSpan.FromSeconds(30)),
                    $"the change never reached the files tree without a Refresh ('{arrival}'). Rescans: "
                    + string.Join(" | ", DiagnosticLines(logFile, "VaultRescanStarted")));
                // Any focus event the publication raised arrives.
                Thread.Sleep(FocusSettle);
            }
            finally
            {
                automation.UnregisterFocusChangedEvent(recorder);
            }

            string[] runs = DiagnosticLines(logFile, "VaultRescanStarted");
            Assert.True(
                runs.Contains("SlateWindows.VaultRescanStarted (Watcher)") && !runs.Any(run => run.EndsWith("(Explicit)", StringComparison.Ordinal)),
                "the change must arrive through a Watcher run, with no Refresh; the log names: " + string.Join(" | ", runs));
            AssertHeldRowStillShown(readerRow, readerName);
            AssertTheTreeShowsTheHeldRow(tree, automation, readerRow, readerId, reader);
            Assert.True(
                focusMoves.IsEmpty,
                $"the watcher run moved the keys: {string.Join(" → ", focusMoves)}");
            Assert.True(
                automation.FocusedElement() is { } focused && focused.Properties.RuntimeId.Value.SequenceEqual(readerId),
                $"the keys left the {reader} row's element for {DescribeFocus(automation)}.");
            foreach ((string after, AutomationElement row, int[] id) in rows)
            {
                AssertTheTreeShowsTheHeldRow(tree, automation, row, id, after);
            }

            // Right after the reconcile, the item-name census included.
            AssertAxeClean(process, surface);
        }
        finally
        {
            if (process is not null && !process.HasExited)
            {
                process.CloseMainWindow();
                if (!process.WaitForExit(5_000))
                {
                    process.Kill(entireProcessTree: true);
                }
            }

            try
            {
                Directory.Delete(testRoot, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    /// <summary>#1515: <paramref name="row"/>, held since before a watcher
    /// run, is still the element the tree shows for the row named
    /// <paramref name="prefix"/> — the same runtime id, not a let-go row that
    /// still answers beside a new one — and it reads that row's name, with a
    /// rectangle.</summary>
    private static void AssertTheTreeShowsTheHeldRow(
        AutomationElement tree,
        UIA3Automation automation,
        AutomationElement row,
        int[] runtimeId,
        string prefix)
    {
        AutomationElement shown = WaitForTreeItemStartingWith(tree, automation, prefix);
        try
        {
            Assert.True(
                shown.Properties.RuntimeId.Value.SequenceEqual(runtimeId),
                $"the watcher run gave the '{prefix}' row a new UI Automation element.");
            Assert.Equal(shown.Properties.Name.Value, row.Properties.Name.Value);
            Assert.False(
                row.Properties.BoundingRectangle.Value.IsEmpty,
                $"the held '{prefix}' row has no rectangle after the watcher run.");
        }
        catch (Exception exception) when (exception is COMException or FlaUI.Core.Exceptions.ElementNotAvailableException)
        {
            Assert.Fail(
                $"the watcher run's reconcile let go of the held '{prefix}' row: "
                + $"{exception.GetType().Name} 0x{exception.HResult:X8}");
        }
    }

    /// <summary>Plan 43 PR 20b: core's test switch for the vault watcher
    /// (<c>SLATE_TEST_DISABLE_VAULT_WATCHER</c>), which a journey that counts
    /// a Refresh's findings sets and the watcher's journey clears.</summary>
    /// <remarks>
    /// Only the Refresh journey (<see cref="ExternalFiles_AppearAfterRefresh"/>)
    /// sets it: it is the one journey that counts what a rescan found (design
    /// §4.7). Every other journey runs the real watcher, decided on 2026-10-18
    /// from what they do: only two others change the vault outside Slate
    /// after the launch — this one, and the Templates journey's template file,
    /// whose Watcher run reconciles the tree under its open picker — and none
    /// counts a rescan's findings, while a reconcile under a journey's rows is
    /// exactly what the watcher makes ordinary.
    /// </remarks>
    private const string VaultWatcherSwitch = "SLATE_TEST_DISABLE_VAULT_WATCHER";

    /// <summary>A Files row held since before a Watcher run is still a row
    /// the tree shows: on screen, with a rectangle and the same name. A row a
    /// publication let go of answers UIA_E_ELEMENTNOTAVAILABLE
    /// (0x80040201) instead — the shell journey's SetFocus on its held note
    /// row, 2026-10-18.</summary>
    private static void AssertHeldRowStillShown(AutomationElement row, string name)
    {
        try
        {
            Assert.False(row.Properties.IsOffscreen.Value, $"the held row '{name}' reads off-screen after the Watcher run");
            Assert.False(
                row.Properties.BoundingRectangle.Value.IsEmpty,
                $"the held row '{name}' has no rectangle after the Watcher run");
            Assert.Equal(name, row.Properties.Name.Value);
        }
        catch (Exception exception) when (exception is COMException or FlaUI.Core.Exceptions.ElementNotAvailableException)
        {
            Assert.Fail(
                $"the Watcher run's reconcile let go of the held row '{name}': "
                + $"{exception.GetType().Name} 0x{exception.HResult:X8}");
        }
    }

    /// <summary>Whether the tree lists a row starting with
    /// <paramref name="prefix"/> now; a transient UIA fault reads as not
    /// yet.</summary>
    private static bool TreeListedItemStartingWith(
        AutomationElement tree, UIA3Automation automation, string prefix)
    {
        try
        {
            return tree
                .FindAllDescendants(automation.ConditionFactory.ByControlType(ControlType.TreeItem))
                .Any(item => (item.Properties.Name.ValueOrDefault ?? string.Empty)
                    .StartsWith(prefix, StringComparison.Ordinal));
        }
        catch (Exception exception) when (IsTransientUiaFault(exception))
        {
            return false;
        }
    }

    /// <summary>File ▸ Files Sidebar ▸ Refresh — the menu route.</summary>
    private static void InvokeFilesSidebarRefreshFromTheMenu(Window window)
    {
        AutomationElement fileMenu = WaitForElement(window, "FileMenu", TimeSpan.FromSeconds(10));
        fileMenu.Patterns.ExpandCollapse.Pattern.Expand();
        AutomationElement sidebarMenu = WaitForElement(window, "FilesSidebarMenu", TimeSpan.FromSeconds(10));
        sidebarMenu.Patterns.ExpandCollapse.Pattern.Expand();
        AutomationElement refresh = WaitForElement(
            window, "FilesSidebarRefreshMenuItem", TimeSpan.FromSeconds(10));
        Assert.True(refresh.IsEnabled, "Files Sidebar ▸ Refresh is disabled.");
        refresh.Patterns.Invoke.Pattern.Invoke();
        Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(250));
    }

    /// <summary>The Files pane's "Refresh files" button — the button route.</summary>
    private static void InvokeRefreshButton(Window window)
    {
        AutomationElement button = WaitForElement(window, "SidebarRefresh", TimeSpan.FromSeconds(10));
        Assert.Equal("Refresh files", button.Name);
        button.Patterns.Invoke.Pattern.Invoke();
        Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(250));
    }

    /// <summary>Ctrl+O from the sidebar filter (the main journey's route —
    /// an Edit always takes focus, where UIA refuses SetFocus on the tree
    /// control while one of its items holds the selection), type the
    /// query, find a row whose name starts with
    /// <paramref name="expectedPrefix"/>, then Escape.</summary>
    private static void AssertQuickOpenFinds(
        Window window, UIA3Automation automation, string query, string expectedPrefix)
    {
        ReassertForegroundForAChord(window);
        AutomationElement filter = WaitForElement(window, "SidebarFilter", TimeSpan.FromSeconds(10));
        filter.Focus();
        AssertEventuallyFocused(filter, "The sidebar filter could not receive focus.");
        Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(250));
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_O);
        AutomationElement search = WaitForElement(window, "QuickSwitcherSearch", TimeSpan.FromSeconds(10));
        AssertEventuallyFocused(search, "Quick Open did not move focus to its search field.");
        Keyboard.Type(query);
        AutomationElement results = WaitForElement(window, "QuickSwitcherResults", TimeSpan.FromSeconds(10));
        string[] names = [];
        bool found = SpinWait.SpinUntil(
            () =>
            {
                try
                {
                    names = [.. results
                        .FindAllDescendants(automation.ConditionFactory.ByControlType(ControlType.ListItem))
                        .Select(item => item.Properties.Name.ValueOrDefault ?? string.Empty)];
                    return names.Any(name => name.StartsWith(expectedPrefix, StringComparison.Ordinal));
                }
                catch (Exception exception) when (IsTransientUiaFault(exception))
                {
                    return false;
                }
            },
            TimeSpan.FromSeconds(10));
        PressKey(VirtualKeyShort.ESCAPE);
        AssertQuickSwitcherDisappears(window, automation);
        Assert.True(
            found,
            $"Quick Open did not find '{expectedPrefix}' for \"{query}\"; rows: [{string.Join(" | ", names)}]");
    }

    private static bool TreeListsItemStartingWith(
        AutomationElement tree, UIA3Automation automation, string prefix)
    {
        try
        {
            return tree
                .FindAllDescendants(automation.ConditionFactory.ByControlType(ControlType.TreeItem))
                .Any(item => (item.Properties.Name.ValueOrDefault ?? string.Empty)
                    .StartsWith(prefix, StringComparison.Ordinal));
        }
        catch (Exception exception) when (IsTransientUiaFault(exception))
        {
            return true;
        }
    }

    /// <summary>The named editor's whole text, re-found on every read: a
    /// reload swaps the document under the control, and a stale element
    /// must not read as an empty note.</summary>
    private static string EditorText(Window window, UIA3Automation automation, string editorName)
    {
        try
        {
            AutomationElement? editor = window
                .FindAllDescendants(automation.ConditionFactory.ByAutomationId("MarkdownEditor"))
                .FirstOrDefault(candidate => string.Equals(
                    candidate.Properties.Name.ValueOrDefault, editorName, StringComparison.Ordinal));
            return editor?.Patterns.Text.Pattern.DocumentRange.GetText(-1) ?? string.Empty;
        }
        catch (Exception exception) when (IsTransientUiaFault(exception))
        {
            return string.Empty;
        }
    }

    private static void WaitForEditorText(
        Window window, UIA3Automation automation, string editorName, string expected) =>
        Assert.True(
            SpinWait.SpinUntil(
                () => EditorText(window, automation, editorName).Contains(expected, StringComparison.Ordinal),
                TimeSpan.FromSeconds(15)),
            $"the open note never showed \"{expected}\"; it shows "
            + $"\"{EditorText(window, automation, editorName)}\"");
}
