// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using uniffi.slate_uniffi;
using UiaInterop = Interop.UIAutomationClient;

namespace SlateWindows.AccessibilityTests;

public sealed partial class ShellAccessibilityTests
{
    /// <summary>
    /// W7-7 PR 1 (#1244; contract R-1, owner decision OD-7): an announcement
    /// reaches a UIA client that was listening before Slate started. The
    /// listener has NVDA's shape (<see cref="DesktopNotificationListener"/>):
    /// a notification handler in an event-handler group on the DESKTOP root,
    /// subtree scope, registered before the window exists and never pointed
    /// at it. It must hear the EXACT launch sequence — each line once, in
    /// order — and then Ctrl+Alt+I's "Right pane hidden." once, with no menu
    /// ever opened (a menu's popup window used to unlock WPF's gate, record
    /// F1), each line with its own activity ID (contract 38 D-1: every line
    /// here is Medium, whose processing asks for every notification).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every run exercises the launch queue's drain (codex PR 1 round 5; R-1
    /// §2.4, AR-1: the journey ships only while it discriminates). Slate is
    /// launched as a census instance with the test-only launch switch
    /// <c>SLATE_TEST_ANNOUNCEMENT_QUEUE_MS</c>
    /// (<c>LaunchSeams.WithTestLaunchQueue</c>; inert in production), which
    /// holds the status provider absent for <see cref="LaunchQueueMilliseconds"/>
    /// after the first frame. So the first line always finds Slate unready and
    /// is queued, and the queue must drain exactly once, in order — on UIA's
    /// advise, or after the three-second hold without it. With the replay
    /// disabled the launch lines are never raised and the run fails. The
    /// ready-at-first-line path (lines raised back to back, each with its own
    /// activity ID) is the hosted facts' to pin
    /// (<c>LinesRaisedWhileReadyCarryTheirActivityIdByProcessing</c>). Nothing
    /// touches the window through UIA until the launch lines are posted. Every
    /// tuple Slate raised is recorded in the evidence artifact
    /// (<c>announcements-launch.json</c>) with the path, the listener states,
    /// sources and the drain the app logged.
    /// </para>
    /// <para>
    /// In the shell gate (contract 40 AR-1); the counts are recorded with the
    /// W7-2 notification etiquette checklist.
    /// </para>
    /// </remarks>
    [Fact]
    public void Announcements_ReachADesktopScopedListenerFromLaunch()
    {
        // The vault's display name is this run's own. The recents list is
        // device-wide (%LOCALAPPDATA%\Slate\recent-vaults.json, one store for
        // every launch), every shell-gate journey opens an "Accessible Vault"
        // of its own, and VaultOpened speaks a recent vault as its welcome
        // button names it (W7-7 PR 3, OD-9: RecentVault.SpokenName) — with
        // its path when another recent's display name reads alike. A name no
        // other recent can share is announced bare, whatever the recents hold.
        string vaultName = $"Announcements Vault {Guid.NewGuid():N}"[..28];
        // Core's own sentences, rendered through the binding before launch:
        // the witness asserts core's copy, never a transcription of it.
        string[] launchLines =
        [
            SlateUniffiMethods.A11yRender(new A11yEvent.VaultOpened(vaultName, string.Empty)).Text,
            SlateUniffiMethods.A11yRender(new A11yEvent.VaultScanStarted(2)).Text,
            // OD-6 (W7-7 PR 7): both counts — a fresh vault's two files are both new.
            SlateUniffiMethods.A11yRender(new A11yEvent.VaultScanFinished(2, 2)).Text,
            SlateUniffiMethods.A11yRender(new A11yEvent.EditorPaneFocused(1, 1, "Empty pane", string.Empty)).Text,
        ];
        RenderedAnnouncement rightPaneHidden = SlateUniffiMethods.A11yRender(new A11yEvent.RightPaneHidden());

        string testRoot = Path.Combine(Path.GetTempPath(), $"slate-announcements-{Guid.NewGuid():N}");
        string logFile = string.Empty;
        var attempts = new List<string>();
        var received = new ConcurrentQueue<ReceivedNotification>();
        var clock = Stopwatch.StartNew();
        Process? process = null;
        // #1326: the evidence records what happened, never a capture that did not.
        string registered = NotRegistered;
        ListenerOutcome outcome = ListenerOutcome.Aborted;
        try
        {
            using var automation = new UIA3Automation();
            // BEFORE the launch, on the root: the window does not exist yet,
            // so nothing can have been advised to it on this client's behalf.
            using var listener = new DesktopNotificationListener(automation, (processId, automationId, kind, processing, displayString, activityId) =>
                received.Enqueue(new ReceivedNotification(
                    processId, automationId, kind, processing, displayString, activityId, clock.ElapsedMilliseconds)));
            (registered, outcome) = (RegisteredBeforeLaunch, ListenerOutcome.Failed);

            string vaultRoot = Path.Combine(testRoot, vaultName);
            string logDirectory = Path.Combine(testRoot, "logs");
            logFile = Path.Combine(logDirectory, "slate-windows.log");
            WriteShellFixtureVault(vaultRoot);
            clock.Restart();
            process = StartShellProcess(vaultRoot, logDirectory, LaunchQueueMilliseconds);
            if (!HasInteractiveDesktop(process, "announcements"))
            {
                // A startup smoke: no window, nothing a listener could hear.
                outcome = ListenerOutcome.Aborted;
                return;
            }

            // The first check logs both lines. The launch switch holds the
            // status provider absent, so the first line queues: the run must
            // take the drain path, never the ready one.
            string source = AwaitDiagnostic(process, logFile, "AnnouncementSource", TimeSpan.FromSeconds(30));
            string state = AwaitDiagnostic(process, logFile, "AnnouncementListenerState", TimeSpan.FromSeconds(30));
            attempts.Add($"{state}; {source}");
            Assert.True(
                source.Contains("statusPeerProvider=null", StringComparison.Ordinal),
                "The launch switch did not hold the status provider absent at the first line, so this run cannot "
                + "witness the drain: " + string.Join(" | ", attempts));
            Process slate = process;
            int processId = slate.Id;
            string[] HeardFromSlate() => [.. received.Where(notification => notification.ProcessId == processId)
                .Select(notification => notification.DisplayString)];

            // The window is still untouched (see remarks); a two-file scan
            // finishes well inside the settle.
            Thread.Sleep(TimeSpan.FromSeconds(3));

            // The walk: the status provider connects once the switch releases
            // it, and the queue drains — each launch line heard once, in order.
            Window window = WaitForMainWindow(slate, automation, logFile, TimeSpan.FromSeconds(30));
            WaitForElement(window, "RightPaneLeaves", TimeSpan.FromSeconds(30));
            AwaitHeard(HeardFromSlate, launchLines, TimeSpan.FromSeconds(15), logFile);

            // A chord-driven line after launch, still with no menu opened. The
            // launch lands the keys on a ROW of the Files tree, never the bare
            // tree (W7-7 PR 4, R-5 as the owner amended it: with no file
            // selected, the first row, unselected).
            window.SetForeground();
            AssertFilesTreeRegionFocused(
                window,
                automation,
                WaitForElement(window, "FilesTree", TimeSpan.FromSeconds(10)),
                "The launch focus did not land on the Files tree before Ctrl+Alt+I.");
            PressUntilGone(window, automation, "RightPaneLeaves", VirtualKeyShort.CONTROL, VirtualKeyShort.ALT, VirtualKeyShort.KEY_I);
            AwaitHeard(HeardFromSlate, [.. launchLines, rightPaneHidden.Text], TimeSpan.FromSeconds(10), logFile);

            // R-4 (#1246; codex PR 3 round 8): the runtime item-name census at
            // this journey's representative state.
            AssertItemNamesAreSpeakable(process, "announcements");

            // R-1 / OD-7 diagnostics, read from the production log: the state
            // written once per change, a listening client reported, and the
            // queued launch lines drained exactly once.
            string[] states = DiagnosticLines(logFile, "AnnouncementListenerState");
            Assert.True(
                states.Any(state => state.Contains("clientsListening=True", StringComparison.Ordinal)),
                "The app never logged a listening client: " + string.Join(" | ", states));
            Assert.True(
                states.Zip(states.Skip(1)).All(pair => pair.First != pair.Second),
                "The listener state was logged twice without changing: " + string.Join(" | ", states));
            string[] drains = DiagnosticLines(logFile, "AnnouncementReplay");
            Assert.True(
                drains.Count(drain => drain.Contains("drained=", StringComparison.Ordinal)) == 1,
                "The queued launch lines did not log exactly one drain: " + string.Join(" | ", drains));
            // #1326: the launch settled once, as the switch forces — its lines
            // queued and replayed whole, none lost.
            string settled = Assert.Single(DiagnosticLines(logFile, "AnnouncementLaunchSettled"));
            Assert.True(
                settled.Contains("(outcome=drained,", StringComparison.Ordinal)
                    && settled.Contains(", lost=0,", StringComparison.Ordinal),
                "The queued launch did not settle as drained with nothing lost: " + settled);

            // The exact tuples: kind Other, the priority's processing (contract
            // 38 D-1 — every line here is Medium, All), and each its own
            // activity id, the dispatcher's sequence in order — the drained
            // lines and every later one alike, no two sharing one, so NVDA's
            // UIA rate limiter coalesces none.
            ReceivedNotification[] fromSlate = [.. received.Where(notification => notification.ProcessId == processId)];
            Assert.Equal(
                fromSlate.Select((_, index) => (
                    NotificationKind.Other,
                    NotificationProcessing.All,
                    $"slate-accessibility-announcement.{index + 1}")),
                fromSlate.Select(notification => (notification.Kind, notification.Processing, notification.ActivityId)));
            outcome = ListenerOutcome.Completed;
        }
        finally
        {
            int slateId = process?.Id ?? -1;
            WriteAnnouncementEvidence(
                "launch",
                registered,
                outcome,
                [.. received.Where(notification => notification.ProcessId == slateId)],
                received.Count(notification => notification.ProcessId != slateId),
                [
                    .. attempts,
                    .. AnnouncementDiagnostics(logFile),
                ]);
            if (process is not null)
            {
                StopProcess(process);
                process.Dispose();
            }

            try { Directory.Delete(testRoot, recursive: true); } catch (IOException) { }
        }
    }

    /// <summary>How long the launch switch holds the status provider absent
    /// after the first frame: the three-second advise hold, so the launch lines
    /// (posted by then; a two-file scan) are all queued before the drain.</summary>
    private const int LaunchQueueMilliseconds = 3000;

    /// <summary>Registers NVDA's listener shape (<see
    /// cref="DesktopNotificationListener"/>) on the desktop root now, in a
    /// journey already under way, and records every notification it hears
    /// into <paramref name="heard"/>.</summary>
    private static DesktopNotificationListener ListenOnTheDesktop(
        UIA3Automation automation, ConcurrentQueue<ReceivedNotification> heard)
    {
        var clock = Stopwatch.StartNew();
        return new DesktopNotificationListener(automation, (processId, automationId, kind, processing, displayString, activityId) =>
            heard.Enqueue(new ReceivedNotification(
                processId, automationId, kind, processing, displayString, activityId, clock.ElapsedMilliseconds)));
    }

    /// <summary>
    /// W7-7 PR 6 (#1251, R-8; #1278): the shell gate's Ctrl+E result reaches
    /// a screen reader as core's sentence. Core renders
    /// <c>EmbedPreviewShown</c> for what the fixture's embed resolves to —
    /// the note <c>Folder/child.md</c>, which core titles with
    /// <c>resolved_embed_title</c> — through the binding here, never a
    /// transcription. Since <paramref name="mark"/>, Slate must have raised
    /// exactly that line, once, as kind Other and ImportantMostRecent (core's
    /// High; contract 38 D-1) with the shared activity ID a superseding line
    /// keeps (D-1). Returns the rendered text.
    /// </summary>
    private static string AssertHeardEmbedPreviewShown(
        ConcurrentQueue<ReceivedNotification> heard, int processId, int mark, string logFile)
    {
        RenderedAnnouncement shown = SlateUniffiMethods.A11yRender(
            new A11yEvent.EmbedPreviewShown("Folder/child", new ResolvedEmbed.Note("Folder/child.md")));
        Assert.Equal(A11yPriority.High, shown.Priority);
        ReceivedNotification[] FromSlate() => [.. HeardFrom(heard, processId).Skip(mark)];
        string[] HeardFromSlate() => [.. FromSlate().Select(notification => notification.DisplayString)];
        AwaitHeard(HeardFromSlate, [shown.Text], TimeSpan.FromSeconds(10), logFile);
        (NotificationKind, NotificationProcessing, string, string)[] expected =
        [
            (NotificationKind.Other, NotificationProcessing.ImportantMostRecent, shown.Text, "slate-accessibility-announcement"),
        ];
        Assert.Equal(
            expected,
            FromSlate().Select(notification => (
                notification.Kind, notification.Processing, notification.DisplayString, notification.ActivityId)));
        return shown.Text;
    }

    /// <summary>Waits until the listener has heard exactly <paramref
    /// name="expected"/> from Slate, in that order — a missing, extra,
    /// repeated or reordered line fails with what was heard.</summary>
    private static void AwaitHeard(Func<string[]> heard, string[] expected, TimeSpan timeout, string logFile)
    {
        _ = SpinWait.SpinUntil(
            () =>
            {
                if (heard().Length >= expected.Length)
                {
                    return true;
                }

                Thread.Sleep(50);
                return false;
            },
            timeout);
        // A settle past the last expected line, so a late duplicate shows.
        Thread.Sleep(TimeSpan.FromMilliseconds(500));
        string[] actual = heard();
        Assert.True(
            actual.SequenceEqual(expected),
            "The desktop listener did not hear exactly the expected lines, once each, in order. Expected: ["
            + string.Join(" | ", expected) + "]. Heard: [" + string.Join(" | ", actual) + "]. Logged: "
            + string.Join(" | ", AnnouncementDiagnostics(logFile)));
    }

    /// <summary>The announcement dispatcher's R-1 and OD-7 diagnostics in the
    /// app log (under <c>SLATE_UIA_DIAGNOSTICS=1</c>), for a failure message
    /// or an evidence artifact: the listener's states, the status provider's,
    /// the drains, drops and expiry, and the launch's settled line
    /// (#1326).</summary>
    private static string[] AnnouncementDiagnostics(string logFile) =>
    [
        .. DiagnosticLines(logFile, "AnnouncementListenerState"),
        .. DiagnosticLines(logFile, "AnnouncementSource"),
        .. DiagnosticLines(logFile, "AnnouncementReplay"),
        .. DiagnosticLines(logFile, "AnnouncementLaunchSettled"),
    ];

    /// <summary>The activity ID every line Slate raises starts from
    /// (contract 38 D-1): a superseding (High) line keeps it, and a Medium
    /// line carries its own numbered one.</summary>
    private const string SlateActivityId = "slate-accessibility-announcement";

    /// <summary>Everything a journey's desktop listener has heard from
    /// Slate, in order.</summary>
    private static ReceivedNotification[] HeardFrom(ConcurrentQueue<ReceivedNotification> heard, int processId) =>
        [.. heard.Where(notification => notification.ProcessId == processId)];

    /// <summary>
    /// Where a step's own lines start: waits until nothing new has been heard
    /// from Slate for a quiet second — whatever the steps before it raised has
    /// arrived (at most fifteen seconds) — and returns how many lines have
    /// been heard so far.
    /// </summary>
    private static int QuietMark(ConcurrentQueue<ReceivedNotification> heard, int processId)
    {
        int count = HeardFrom(heard, processId).Length;
        var quiet = Stopwatch.StartNew();
        var waited = Stopwatch.StartNew();
        while (quiet.Elapsed < TimeSpan.FromSeconds(1) && waited.Elapsed < TimeSpan.FromSeconds(15))
        {
            Thread.Sleep(100);
            int now = HeardFrom(heard, processId).Length;
            if (now != count)
            {
                count = now;
                quiet.Restart();
            }
        }

        return count;
    }

    /// <summary>
    /// The first step's mark after a launch. Slate's launch settles once
    /// (R-1, OD-7) and logs how, under <c>SLATE_UIA_DIAGNOSTICS=1</c>
    /// (<c>AnnouncementLaunchSettled</c>, #1326): <c>drained</c> — the lines
    /// posted before readiness queued and replayed together, on UIA's advise
    /// or once the three-second hold ran out; <c>ready-empty</c> — ready at
    /// its first line, nothing queued, every line raised as it was posted (the
    /// CI shell gate's path: the journey's own listener is advised before the
    /// first launch line, and no drain is ever logged); or a launch line lost
    /// unspoken — <c>dropped</c> or <c>expired</c> — a defect this fails on at
    /// once. A step taken before the launch settled would count its lines as
    /// the step's own, so this waits for the settled line (the thirty-second
    /// launch window bounds it), then for a quiet second (<see cref="QuietMark"/>).
    /// </summary>
    private static int LaunchSettledMark(ConcurrentQueue<ReceivedNotification> heard, int processId, string logFile)
    {
        string settled = string.Empty;
        bool logged = SpinWait.SpinUntil(
            () =>
            {
                if (DiagnosticLines(logFile, "AnnouncementLaunchSettled") is [string line, ..])
                {
                    settled = line;
                    return true;
                }

                Thread.Sleep(100);
                return false;
            },
            TimeSpan.FromSeconds(45));
        Assert.True(
            logged,
            "Slate's launch never settled (no AnnouncementLaunchSettled diagnostic): "
            + string.Join(" | ", AnnouncementDiagnostics(logFile)));
        Assert.True(
            settled.Contains("(outcome=drained,", StringComparison.Ordinal)
                || settled.Contains("(outcome=ready-empty,", StringComparison.Ordinal),
            "Slate lost a launch line before it was spoken: "
            + string.Join(" | ", AnnouncementDiagnostics(logFile)));
        return QuietMark(heard, processId);
    }

    /// <summary>
    /// Waits until Slate has raised exactly <paramref name="expected"/> since
    /// <paramref name="mark"/> — core's renderings, once each, in order, and
    /// nothing else (<see cref="AwaitHeard"/>) — then checks each line's UIA
    /// shape (<see cref="AssertRaisedAsRendered"/>).
    /// </summary>
    private static void AwaitHeardSince(
        ConcurrentQueue<ReceivedNotification> heard,
        int processId,
        int mark,
        RenderedAnnouncement[] expected,
        TimeSpan timeout,
        string logFile)
    {
        AwaitHeard(
            () => [.. HeardFrom(heard, processId).Skip(mark).Select(notification => notification.DisplayString)],
            [.. expected.Select(line => line.Text)],
            timeout,
            logFile);
        ReceivedNotification[] lines = [.. HeardFrom(heard, processId).Skip(mark)];
        for (int index = 0; index < expected.Length; index++)
        {
            AssertRaisedAsRendered(expected[index], lines[index]);
        }
    }

    /// <summary>
    /// A line Slate raised has the shape contract 38 D-1 gives core's
    /// priority: kind Other; a High line ImportantMostRecent under the shared
    /// activity ID (a newer one supersedes it); a Medium line All under a
    /// numbered ID of its own, so NVDA's UIA rate limiter coalesces none
    /// (#1244).
    /// </summary>
    private static void AssertRaisedAsRendered(RenderedAnnouncement expected, ReceivedNotification heard)
    {
        (NotificationProcessing processing, bool numbered) = expected.Priority switch
        {
            A11yPriority.High => (NotificationProcessing.ImportantMostRecent, false),
            A11yPriority.Medium => (NotificationProcessing.All, true),
            _ => throw new Xunit.Sdk.XunitException(
                $"contract 38 D-1 maps no processing for {expected.Priority} (\"{expected.Text}\")."),
        };
        Assert.True(
            heard.Kind == NotificationKind.Other
                && heard.Processing == processing
                && (numbered
                    ? System.Text.RegularExpressions.Regex.IsMatch(
                        heard.ActivityId, "^" + SlateActivityId + @"\.\d+$")
                    : heard.ActivityId == SlateActivityId),
            $"\"{heard.DisplayString}\" was raised as ({heard.Kind}, {heard.Processing}, {heard.ActivityId}); core's "
            + $"{expected.Priority} line is (Other, {processing}, "
            + (numbered ? SlateActivityId + ".<n>" : SlateActivityId) + ") (contract 38 D-1).");
    }

    private static void StopProcess(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
            _ = process.WaitForExit(10_000);
        }
        catch (InvalidOperationException)
        {
        }
    }

    /// <summary>The first app-log line of one host diagnostic, waited for while
    /// Slate runs.</summary>
    private static string AwaitDiagnostic(Process process, string logFile, string diagnosticEvent, TimeSpan timeout)
    {
        string? line = null;
        _ = SpinWait.SpinUntil(
            () =>
            {
                line = DiagnosticLines(logFile, diagnosticEvent).FirstOrDefault();
                if (line is not null || process.HasExited)
                {
                    return true;
                }

                Thread.Sleep(50);
                return false;
            },
            timeout);
        return line ?? throw new Xunit.Sdk.XunitException(
            $"Slate logged no {diagnosticEvent} line (exited: {process.HasExited}). app log: {ReadSharedLog(logFile)}");
    }

    /// <summary>Presses a chord until the element it hides is gone — a press
    /// the desktop swallowed (another window briefly holding the foreground)
    /// is pressed again, never more than three times.</summary>
    private static void PressUntilGone(
        Window window, UIA3Automation automation, string automationId, VirtualKeyShort first, VirtualKeyShort second, VirtualKeyShort key)
    {
        for (int press = 1; press <= 3; press++)
        {
            window.SetForeground();
            PressChord(first, second, key);
            if (SpinWait.SpinUntil(
                () => window.FindFirstDescendant(automation.ConditionFactory.ByAutomationId(automationId)) is null,
                TimeSpan.FromSeconds(10)))
            {
                return;
            }
        }

        throw new Xunit.Sdk.XunitException($"UIA element {automationId} remained visible after three presses. {FocusDiagnosis()}");
    }

    /// <summary>A notification the desktop listener received.</summary>
    private sealed record ReceivedNotification(
        int ProcessId,
        string? SenderAutomationId,
        NotificationKind Kind,
        NotificationProcessing Processing,
        string DisplayString,
        string ActivityId,
        long ElapsedMilliseconds);

    /// <summary>The app log's lines for one host diagnostic, in order.</summary>
    private static string[] DiagnosticLines(string logFile, string diagnosticEvent) =>
    [
        .. ReadSharedLog(logFile)
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.StartsWith("SlateWindows." + diagnosticEvent + " ", StringComparison.Ordinal)),
    ];

    /// <summary>
    /// A desktop-wide notification listener registered the way NVDA's UIA
    /// handler registers its own: through raw <c>IUIAutomation6</c> COM, a
    /// notification handler added to an event-handler GROUP, and the group
    /// added to the desktop root with subtree scope and a cache request.
    /// </summary>
    /// <remarks>
    /// Not FlaUI's <c>RegisterNotificationEvent</c>, and that is measured,
    /// not assumed (AR-1): its <c>IUIAutomation5.AddNotificationEventHandler</c>
    /// registration is advised to the new window, which fills WPF's listener
    /// map, so the journey heard every line against the gated
    /// <c>peer.RaiseNotificationEvent</c> too and proved nothing. A handler
    /// group is not advised to the window, which is the gap NVDA hit.
    /// </remarks>
    private sealed class DesktopNotificationListener
        : UiaInterop.IUIAutomationNotificationEventHandler, IDisposable
    {
        private readonly UiaInterop.IUIAutomation6 _automation;
        private readonly UiaInterop.IUIAutomationElement _root;
        private readonly UiaInterop.IUIAutomationEventHandlerGroup _group;
        private readonly Action<int, string?, NotificationKind, NotificationProcessing, string, string> _received;
        private bool _disposed;

        internal DesktopNotificationListener(
            UIA3Automation automation,
            Action<int, string?, NotificationKind, NotificationProcessing, string, string> received)
        {
            _received = received;
            _automation = (UiaInterop.IUIAutomation6)automation.NativeAutomation;
            _root = _automation.GetRootElement();
            UiaInterop.IUIAutomationCacheRequest cache = _automation.CreateCacheRequest();
            cache.AddProperty(UiaInterop.UIA_PropertyIds.UIA_ProcessIdPropertyId);
            cache.AddProperty(UiaInterop.UIA_PropertyIds.UIA_AutomationIdPropertyId);
            _automation.CreateEventHandlerGroup(out _group);
            _group.AddNotificationEventHandler(UiaInterop.TreeScope.TreeScope_Subtree, cache, this);
            _automation.AddEventHandlerGroup(_root, _group);
        }

        public void HandleNotificationEvent(
            UiaInterop.IUIAutomationElement sender,
            UiaInterop.NotificationKind notificationKind,
            UiaInterop.NotificationProcessing notificationProcessing,
            string displayString,
            string activityId)
        {
            int processId;
            string? automationId;
            try
            {
                processId = sender.CachedProcessId;
                automationId = sender.CachedAutomationId;
            }
            catch (COMException)
            {
                processId = -1;
                automationId = null;
            }

            // The interop enums and FlaUI's carry UIA's own values.
            _received(processId, automationId, (NotificationKind)(int)notificationKind,
                (NotificationProcessing)(int)notificationProcessing, displayString, activityId);
        }

        /// <summary>Removes the group once; a journey that disposes early
        /// and its <c>using</c> both call it.</summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _automation.RemoveEventHandlerGroup(_root, _group);
        }
    }

    /// <summary>The launch journey's listener: registered before Slate
    /// launched, which is that journey's subject (R-1).</summary>
    private const string RegisteredBeforeLaunch = "registered before launch";

    /// <summary>Every other journey's listener: registered once
    /// <c>WaitForMainWindow</c> had reached the window, its legs taken after the
    /// launch settled (<see cref="LaunchSettledMark"/>).</summary>
    private const string RegisteredAfterTheWindow = "registered after WaitForMainWindow reached the window";

    /// <summary>#1326: a listener that was never registered — the journey
    /// returned (no interactive desktop) or failed before its registration
    /// returned. The evidence says so, with an aborted outcome, and claims no
    /// capture.</summary>
    private const string NotRegistered = "not registered";

    /// <summary>#1326: how far a journey (or a leg) got with its desktop
    /// listener, as its evidence records it.</summary>
    private enum ListenerOutcome
    {
        /// <summary>Nothing could be heard: the listener was never registered,
        /// or the run degraded to a startup smoke with no window.</summary>
        Aborted,

        /// <summary>Registered, and the journey stopped before its end: an
        /// assertion failed or an exception escaped.</summary>
        Failed,

        /// <summary>Registered, and the journey reached its end.</summary>
        Completed,
    }

    /// <summary>Writes one surface's announcement evidence (under
    /// <c>SLATE_ACCESSIBILITY_EVIDENCE_DIR</c>): what Slate raised, how many
    /// lines came from other processes, the app's diagnostics, where the
    /// listener was registered — <see cref="RegisteredBeforeLaunch"/>,
    /// <see cref="RegisteredAfterTheWindow"/> or <see cref="NotRegistered"/> —
    /// and how far the journey got (<see cref="ListenerOutcome"/>; a listener
    /// never registered is always an aborted one).</summary>
    private static void WriteAnnouncementEvidence(
        string surface,
        string registered,
        ListenerOutcome outcome,
        ReceivedNotification[] fromSlate,
        int otherProcesses,
        string[] diagnostics)
    {
        string? directory = Environment.GetEnvironmentVariable("SLATE_ACCESSIBILITY_EVIDENCE_DIR");
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        var evidence = new
        {
            schemaVersion = 2,
            surface,
            recordedAtUtc = DateTimeOffset.UtcNow,
            sourceRevision = Environment.GetEnvironmentVariable("GITHUB_SHA"),
            operatingSystem = Environment.OSVersion.VersionString,
            dotnetRuntime = Environment.Version.ToString(),
            listener = "IUIAutomation6 event-handler group on the desktop root, subtree scope, " + registered,
            outcome = (registered == NotRegistered ? ListenerOutcome.Aborted : outcome).ToString().ToLowerInvariant(),
            received = fromSlate.Select(notification => new
            {
                displayString = notification.DisplayString,
                processing = notification.Processing.ToString(),
                activityId = notification.ActivityId,
                kind = notification.Kind.ToString(),
                sender = notification.SenderAutomationId,
                elapsedMilliseconds = notification.ElapsedMilliseconds,
            }).ToArray(),
            otherProcesses,
            diagnostics,
        };
        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(
                Path.Combine(directory, $"announcements-{surface}.json"),
                JsonSerializer.Serialize(evidence, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Written from a finally: an unwritable artifact must never
            // replace the journey's own failure.
        }
    }
}
