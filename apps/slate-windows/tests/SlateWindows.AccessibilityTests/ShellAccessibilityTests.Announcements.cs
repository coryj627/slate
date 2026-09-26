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
    /// order, queued while the process was unadvised and drained at the advise
    /// — and then Ctrl+Alt+I's "Right pane hidden." once, with no menu ever
    /// opened (a menu's popup window used to unlock WPF's gate, record F1).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The run is made to exercise the launch queue, not left to chance. A
    /// launch whose first line already finds the process advised raises at
    /// once and cannot show the drain, so it is closed and Slate relaunched,
    /// up to <see cref="MaxAnnouncementLaunches"/> times. Then nothing touches
    /// the window through UIA until the launch lines are queued: the walk that
    /// follows connects the status provider and brings the advise, and the
    /// queue must drain once, in order. Every tuple Slate raised is recorded
    /// in the evidence artifact (<c>announcements-launch.json</c>) with each
    /// launch attempt and the listener states, sources and drain the app
    /// logged.
    /// </para>
    /// <para>
    /// A manual-trait journey (contract 40 AR-1): it was not 6/6 stable on
    /// the final build — its one miss was its own precondition, all thirty
    /// launches advised at their first line on a busy desktop — so the shell
    /// gate's evidence for R-1 is the hosted launch-queue facts and
    /// <c>AnnouncementSeamCensus</c>, and this journey runs when
    /// <c>SLATE_MANUAL_JOURNEYS=1</c>. Its counts are recorded with the W7-2
    /// notification etiquette checklist.
    /// </para>
    /// </remarks>
    [ManualJourneyFact]
    [Trait("gate", "manual")]
    public void Announcements_ReachADesktopScopedListenerFromLaunch()
    {
        // Core's own sentences, rendered through the binding before launch:
        // the witness asserts core's copy, never a transcription of it.
        string[] launchLines =
        [
            SlateUniffiMethods.A11yRender(new A11yEvent.VaultOpened("Accessible Vault", string.Empty)).Text,
            SlateUniffiMethods.A11yRender(new A11yEvent.VaultScanStarted(2)).Text,
            SlateUniffiMethods.A11yRender(new A11yEvent.VaultScanFinished(2)).Text,
            SlateUniffiMethods.A11yRender(new A11yEvent.EditorPaneFocused(1, 1, "Empty pane", string.Empty)).Text,
        ];
        RenderedAnnouncement rightPaneHidden = SlateUniffiMethods.A11yRender(new A11yEvent.RightPaneHidden());

        string testRoot = Path.Combine(Path.GetTempPath(), $"slate-announcements-{Guid.NewGuid():N}");
        string logFile = string.Empty;
        var attempts = new List<string>();
        var received = new ConcurrentQueue<ReceivedNotification>();
        var clock = Stopwatch.StartNew();
        Process? process = null;
        try
        {
            using var automation = new UIA3Automation();
            // BEFORE the launch, on the root: the window does not exist yet,
            // so nothing can have been advised to it on this client's behalf.
            using var listener = new DesktopNotificationListener(automation, (processId, automationId, kind, processing, displayString, activityId) =>
                received.Enqueue(new ReceivedNotification(
                    processId, automationId, kind, processing, displayString, activityId, clock.ElapsedMilliseconds)));

            bool unadvised = false;
            for (int attempt = 1; !unadvised && attempt <= MaxAnnouncementLaunches; attempt++)
            {
                if (process is not null)
                {
                    StopProcess(process);
                    process.Dispose();
                }

                string vaultRoot = Path.Combine(testRoot, $"launch-{attempt}", "Accessible Vault");
                string logDirectory = Path.Combine(testRoot, $"launch-{attempt}", "logs");
                logFile = Path.Combine(logDirectory, "slate-windows.log");
                WriteShellFixtureVault(vaultRoot);
                clock.Restart();
                process = StartShellProcess(vaultRoot, logDirectory);
                if (!HasInteractiveDesktop(process, "announcements"))
                {
                    return;
                }

                string first = AwaitDiagnostic(process, logFile, "AnnouncementListenerState", TimeSpan.FromSeconds(30));
                unadvised = first.Contains("notificationListenerExists=False", StringComparison.Ordinal);
                attempts.Add($"launch {attempt}: {first}");
            }

            Assert.True(
                unadvised,
                "Every launch found the process already advised at its first line, so no run exercised the "
                + "launch queue: " + string.Join(" | ", attempts));
            Process slate = process!;
            int processId = slate.Id;
            string[] HeardFromSlate() => [.. received.Where(notification => notification.ProcessId == processId)
                .Select(notification => notification.DisplayString)];

            // The launch lines are posted into the unadvised process with the
            // window still untouched (see remarks); a two-file scan finishes
            // well inside the settle.
            Thread.Sleep(TimeSpan.FromSeconds(3));

            // The walk: the status provider connects, UIA advises the process,
            // and the queue drains — each launch line once, in order.
            Window window = WaitForMainWindow(slate, automation, logFile, TimeSpan.FromSeconds(30));
            WaitForElement(window, "RightPaneLeaves", TimeSpan.FromSeconds(30));
            AwaitHeard(HeardFromSlate, launchLines, TimeSpan.FromSeconds(15), logFile);

            // A chord-driven line after launch, still with no menu opened.
            window.SetForeground();
            AssertEventuallyFocused(
                WaitForElement(window, "FilesTree", TimeSpan.FromSeconds(10)),
                "The launch focus did not land on the Files tree before Ctrl+Alt+I.");
            PressUntilGone(window, automation, "RightPaneLeaves", VirtualKeyShort.CONTROL, VirtualKeyShort.ALT, VirtualKeyShort.KEY_I);
            AwaitHeard(HeardFromSlate, [.. launchLines, rightPaneHidden.Text], TimeSpan.FromSeconds(10), logFile);

            // The exact tuples: kind Other, the priority's processing (contract
            // 38 D-1 — every line here is Medium), the shared activity id.
            Assert.All(
                received.Where(notification => notification.ProcessId == processId),
                notification => Assert.Equal(
                    (NotificationKind.Other, NotificationProcessing.All, "slate-accessibility-announcement"),
                    (notification.Kind, notification.Processing, notification.ActivityId)));

            // R-1 / OD-7 diagnostics, read from the production log: the state
            // written once per change, a listening client reported, and the
            // unadvised launch's queue drained.
            string[] states = DiagnosticLines(logFile, "AnnouncementListenerState");
            Assert.True(
                states.Any(state => state.Contains("clientsListening=True", StringComparison.Ordinal)),
                "The app never logged a listening client: " + string.Join(" | ", states));
            Assert.True(
                states.Zip(states.Skip(1)).All(pair => pair.First != pair.Second),
                "The listener state was logged twice without changing: " + string.Join(" | ", states));
            string[] drains = DiagnosticLines(logFile, "AnnouncementReplay");
            Assert.True(
                drains.Any(drain => drain.Contains("drained=", StringComparison.Ordinal)),
                "The unadvised launch logged no drain: " + string.Join(" | ", drains));
        }
        finally
        {
            int slateId = process?.Id ?? -1;
            WriteAnnouncementEvidence(
                "launch",
                [.. received.Where(notification => notification.ProcessId == slateId)],
                received.Count(notification => notification.ProcessId != slateId),
                [
                    .. attempts,
                    .. DiagnosticLines(logFile, "AnnouncementListenerState"),
                    .. DiagnosticLines(logFile, "AnnouncementSource"),
                    .. DiagnosticLines(logFile, "AnnouncementReplay"),
                ]);
            if (process is not null)
            {
                StopProcess(process);
                process.Dispose();
            }

            try { Directory.Delete(testRoot, recursive: true); } catch (IOException) { }
        }
    }

    /// <summary>
    /// A journey outside the shell gate: skipped, with this reason, unless
    /// <c>SLATE_MANUAL_JOURNEYS=1</c> — so a harness flake is never a gate
    /// failure, and a skipped run is never counted as evidence.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    private sealed class ManualJourneyFactAttribute : FactAttribute
    {
        public ManualJourneyFactAttribute()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("SLATE_MANUAL_JOURNEYS"), "1", StringComparison.Ordinal))
            {
                Skip = "A manual-trait journey (W7-7 AR-1): set SLATE_MANUAL_JOURNEYS=1 to run it.";
            }
        }
    }

    /// <summary>How many times the journey relaunches Slate for a launch whose
    /// first line lands before UIA's advise. Measured, the share of launches
    /// already advised at their first line ran from one in three (2026-09-22)
    /// to five in six (2026-09-23), and on a busy desktop one run found all
    /// thirty advised (2026-09-23): the harness flakiness AR-1 names, which
    /// decides whether this journey sits in the shell gate. Each launch costs
    /// about a second.</summary>
    private const int MaxAnnouncementLaunches = 30;

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
            + string.Join(" | ", DiagnosticLines(logFile, "AnnouncementListenerState"))
            + " | " + string.Join(" | ", DiagnosticLines(logFile, "AnnouncementSource"))
            + " | " + string.Join(" | ", DiagnosticLines(logFile, "AnnouncementReplay")));
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

        public void Dispose() => _automation.RemoveEventHandlerGroup(_root, _group);
    }

    private static void WriteAnnouncementEvidence(
        string surface, ReceivedNotification[] fromSlate, int otherProcesses, string[] diagnostics)
    {
        string? directory = Environment.GetEnvironmentVariable("SLATE_ACCESSIBILITY_EVIDENCE_DIR");
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        var evidence = new
        {
            schemaVersion = 1,
            surface,
            recordedAtUtc = DateTimeOffset.UtcNow,
            sourceRevision = Environment.GetEnvironmentVariable("GITHUB_SHA"),
            operatingSystem = Environment.OSVersion.VersionString,
            dotnetRuntime = Environment.Version.ToString(),
            listener = "IUIAutomation6 event-handler group on the desktop root, subtree scope, registered before launch",
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
