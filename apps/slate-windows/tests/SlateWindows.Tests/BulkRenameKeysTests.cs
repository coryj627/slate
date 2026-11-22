// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using SlateWindows.Panels;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// Plan 47 CB-17b (#1866, CD11): Bulk Rename's Preview and Apply, and the keys. Preview stays
/// on while a run is in flight (the owner's answer to Call 4, "Stay, and speak on a press"): the
/// keys stay on it, and a press while a run goes on starts nothing and says core's
/// <c>WorkStillRunning</c>. Apply moves the keys to Old property key, where the guard put them
/// after the off, before it goes off: Close is no landing, since Close during an apply cancels
/// it. Every press is Enter, on the real window with the sidebar attached; the previews and
/// applies run on their workers and publish through the dispatcher, as in the app.
/// </summary>
public sealed class BulkRenameKeysTests
{
    private const string Note = "---\nstatus: draft\n---\n# A\n";

    /// <summary>Preview by Enter, its worker held: Preview never goes off and keeps the keys
    /// through the run. A second Enter while the run is held starts no run and says "The
    /// preview is still running."; the preview then publishes once. The stays-on witness of
    /// BulkRenamePreviewButton.</summary>
    [Fact]
    public void PreviewStaysOnThroughItsRunAndAPressMeanwhileOnlySaysSo() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", Note));
        using var sidebar = new ShownShellSidebar(host);
        FrameworkElement overlay = OpenBulkRename(host);
        BulkRenameViewModel sheet = host.Workspace.BulkRenameSheet!;
        sheet.OldKey = "status";
        sheet.NewKey = "state";
        host.Settle();
        Button preview = ButtonOf(overlay, "BulkRenamePreviewButton");
        Assert.True(
            preview.Focus() && ReferenceEquals(preview, Keyboard.FocusedElement),
            $"keyboard premise: Preview could not take the keys (they are on {Describe(Keyboard.FocusedElement)})");
        int published = 0;
        sheet.RunPublished += () => published++;
        int heard = host.Announced.Count;
        int runsBefore = sheet.RequestIdForTests;
        int runsAfterTheSecondPress = -1;
        using var held = new HeldWork(hook => sheet.PreviewHookForTests = hook);

        KeysFirstRecorder.AssertItStaysOn("Preview", preview, () =>
        {
            Press(host, Key.Enter);
            held.WaitUntilEntered();
            Press(host, Key.Enter);
            runsAfterTheSecondPress = sheet.RequestIdForTests;
            held.Release();
            Assert.True(PumpedDispatcher.PumpUntil(() => published == 1 && !sheet.WorkInFlight, TimeSpan.FromSeconds(30)), "the preview never published");
            host.Settle();
        }, () => published == 1);

        held.AssertHeldUntilReleased();
        Assert.True(runsBefore + 1 == runsAfterTheSecondPress, $"the second press started a run of its own ({runsAfterTheSecondPress - runsBefore} runs)");
        A11yEvent[] said = [.. host.Announced.Skip(heard)];
        Assert.Contains(said, line => line is A11yEvent.WorkStillRunning { Work: RunningWork.BulkRenamePreview });
        Assert.Single(said.OfType<A11yEvent.RenameSummary>());
        Assert.True(
            ReferenceEquals(preview, Keyboard.FocusedElement),
            $"after the preview the keys were on {Describe(Keyboard.FocusedElement)}, not on Preview");
    });

    /// <summary>Enter in Old property key while a preview runs reaches the same refusal, and
    /// now says it: before, Enter in a field did nothing while the run held Preview
    /// off.</summary>
    [Fact]
    public void EnterInAFieldWhileAPreviewRunsSaysThePreviewIsStillRunning() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", Note));
        using var sidebar = new ShownShellSidebar(host);
        FrameworkElement overlay = OpenBulkRename(host);
        BulkRenameViewModel sheet = host.Workspace.BulkRenameSheet!;
        sheet.OldKey = "status";
        sheet.NewKey = "state";
        host.Settle();
        TextBox oldKey = host.Shell.BulkRenameOldKeyTextBox;
        Assert.True(
            ReferenceEquals(oldKey, Keyboard.FocusedElement) || oldKey.Focus(),
            $"keyboard premise: Old property key could not take the keys (they are on {Describe(Keyboard.FocusedElement)})");
        int published = 0;
        sheet.RunPublished += () => published++;
        int heard = host.Announced.Count;
        int runsBefore = sheet.RequestIdForTests;
        using var held = new HeldWork(hook => sheet.PreviewHookForTests = hook);

        Press(host, Key.Enter);
        held.WaitUntilEntered();
        Assert.True(sheet.WorkInFlight, "premise: Enter in the field ran no preview");
        Press(host, Key.Enter);
        int runsAfterTheSecondPress = sheet.RequestIdForTests;
        held.Release();
        Assert.True(PumpedDispatcher.PumpUntil(() => published == 1 && !sheet.WorkInFlight, TimeSpan.FromSeconds(30)), "the preview never published");
        held.AssertHeldUntilReleased();

        Assert.Equal(runsBefore + 1, runsAfterTheSecondPress);
        Assert.Contains(host.Announced.Skip(heard), line => line is A11yEvent.WorkStillRunning { Work: RunningWork.BulkRenamePreview });
        Assert.True(overlay.IsKeyboardFocusWithin, $"the keys left the sheet, for {Describe(Keyboard.FocusedElement)}");
    });

    /// <summary>Apply by Enter, its worker held: the keys go to Old property key before Apply
    /// goes off, and stay in the sheet through the publish. A held Enter (the key repeating)
    /// reaches Old property key, where it says "The rename is still running." and starts
    /// nothing; it never reaches Close, whose press would cancel the apply. The keys-first
    /// witness of BulkRenameApplyButton.</summary>
    [Fact]
    public void ApplyLetsTheKeysGoToOldPropertyKeyBeforeItGoesOff() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", Note));
        using var sidebar = new ShownShellSidebar(host);
        FrameworkElement overlay = OpenBulkRename(host);
        BulkRenameViewModel sheet = host.Workspace.BulkRenameSheet!;
        sheet.OldKey = "status";
        sheet.NewKey = "state";
        int published = 0;
        sheet.RunPublished += () => published++;
        sheet.Preview();
        Assert.True(PumpedDispatcher.PumpUntil(() => published == 1 && !sheet.WorkInFlight, TimeSpan.FromSeconds(30)), "premise: the preview never published");
        host.Settle();
        Assert.True(sheet.CanApply, "premise: the preview did not arm Apply");
        Button apply = ButtonOf(overlay, "BulkRenameApplyButton");
        Button close = ButtonOf(overlay, "BulkRenameClose");
        TextBox oldKey = host.Shell.BulkRenameOldKeyTextBox;
        Assert.True(
            apply.IsEnabled && apply.Focus() && ReferenceEquals(apply, Keyboard.FocusedElement),
            $"keyboard premise: Apply could not take the keys (they are on {Describe(Keyboard.FocusedElement)})");
        int heard = host.Announced.Count;
        List<IInputElement> changes = host.RecordFocusChanges();
        using var held = new HeldWork(hook => sheet.ApplyHookForTests = hook);

        KeysFirstRecorder.AssertTheKeysLeaveFirst("Apply", apply, () =>
        {
            Press(host, Key.Enter);
            Assert.True(PumpedDispatcher.PumpUntil(() => held.Entered, TimeSpan.FromSeconds(30)), "the apply's worker never started");
            Press(host, Key.Enter);
            held.Release();
            Assert.True(PumpedDispatcher.PumpUntil(() => published == 2 && !sheet.WorkInFlight, TimeSpan.FromSeconds(30)), "the apply never published");
            host.Settle();
        });

        held.AssertHeldUntilReleased();
        Assert.True(
            ReferenceEquals(oldKey, Keyboard.FocusedElement),
            $"after the apply the keys were on {Describe(Keyboard.FocusedElement)}, not on Old property key");
        Assert.DoesNotContain(changes, change => ReferenceEquals(change, close));
        Assert.All(changes, change => Assert.True(
            change is DependencyObject element && overlay.IsAncestorOf(element),
            $"the keys stopped outside the sheet on the way: {string.Join(" -> ", changes.Select(Describe))}"));
        Assert.NotNull(host.Workspace.BulkRenameSheet);
        A11yEvent[] said = [.. host.Announced.Skip(heard)];
        Assert.Contains(said, line => line is A11yEvent.WorkStillRunning { Work: RunningWork.BulkRenameApply });
        Assert.Contains(said, line => line is A11yEvent.RenameSummary { Applied: true, Renamed: 1 });
        Assert.Contains("state: draft", host.Read("a.md"), StringComparison.Ordinal);
    });

    /// <summary>An Apply refused at its press (a note's header draft is unsaved) says why, and
    /// leaves the keys on Apply, which stays on: only an admitted apply moves them.</summary>
    [Fact]
    public void ARefusedApplyLeavesTheKeysOnApply() => RunSta(() =>
    {
        using var host = new ShownShell(("a.md", Note));
        using var sidebar = new ShownShellSidebar(host);
        FrameworkElement overlay = OpenBulkRename(host);
        BulkRenameViewModel sheet = host.Workspace.BulkRenameSheet!;
        sheet.OldKey = "status";
        sheet.NewKey = "state";
        int published = 0;
        sheet.RunPublished += () => published++;
        sheet.Preview();
        Assert.True(PumpedDispatcher.PumpUntil(() => published == 1 && !sheet.WorkInFlight, TimeSpan.FromSeconds(30)), "premise: the preview never published");
        NotePropertiesViewModel properties = host.Workspace.ActiveGroup.ActiveTab?.Properties
            ?? throw new Xunit.Sdk.XunitException("premise: the note's tab has no Properties header");
        properties.IsExpanded = true;
        Assert.True(
            PumpedDispatcher.PumpUntil(() => !properties.IsLoading && properties.Rows.Any(row => row.KeyIdentity == "status"), TimeSpan.FromSeconds(30)),
            "premise: the header never listed the note's property");
        properties.Rows.Single(row => row.KeyIdentity == "status").EditorText = "unsaved";
        host.Settle();
        Assert.True(properties.AnyRowDirty, "premise: the header's draft is not dirty");
        Button apply = ButtonOf(overlay, "BulkRenameApplyButton");
        Assert.True(
            sheet.CanApply && apply.Focus() && ReferenceEquals(apply, Keyboard.FocusedElement),
            $"keyboard premise: Apply could not take the keys (they are on {Describe(Keyboard.FocusedElement)})");
        int heard = host.Announced.Count;
        List<IInputElement> changes = host.RecordFocusChanges();

        Press(host, Key.Enter);
        host.Settle();

        Assert.False(sheet.WorkInFlight, "the refused apply went in flight");
        Assert.Contains(host.Announced.Skip(heard), line => line is A11yEvent.HostComposed { Text: var text } && text == PropertyPhrase.BulkRenameDirtyDraftsReason);
        Assert.True(changes.Count == 0, $"the refused apply moved the keys: {string.Join(" -> ", changes.Select(Describe))}");
        Assert.True(
            ReferenceEquals(apply, Keyboard.FocusedElement) && apply.IsEnabled,
            $"after the refusal the keys were on {Describe(Keyboard.FocusedElement)}, not on Apply, on");
    });

    /// <summary>The real Bulk Rename sheet over a note, opened from the editor, holding the
    /// keys.</summary>
    private static FrameworkElement OpenBulkRename(ShownShell host)
    {
        host.Workspace.OpenPath("a.md");
        host.Settle();
        host.Shell.Activate();
        host.Settle();
        Assert.True(host.Land(ShellRegionKind.Editor), "premise: the editor did not take the keys");
        host.Settle();
        host.Workspace.OpenBulkRenameSheet();
        host.Settle();
        var overlay = (FrameworkElement)host.Shell.FindName("BulkRenameOverlay");
        Assert.True(overlay.IsKeyboardFocusWithin, $"premise: the sheet did not take the keys (they are on {Describe(Keyboard.FocusedElement)})");
        return overlay;
    }

    private static Button ButtonOf(FrameworkElement overlay, string id) =>
        ShownShell.Descendants(overlay).OfType<Button>().Single(candidate => AutomationProperties.GetAutomationId(candidate) == id);

    /// <summary>A key through WPF's input pipeline, down then up, to the element holding
    /// the keys.</summary>
    private static void Press(ShownShell host, Key key)
    {
        foreach (RoutedEvent direction in new[] { Keyboard.PreviewKeyDownEvent, Keyboard.PreviewKeyUpEvent })
        {
            InputManager.Current.ProcessInput(
                new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(host.Shell)!, Environment.TickCount, key)
                {
                    RoutedEvent = direction,
                });
        }
    }

    private static string Describe(object? element) => element switch
    {
        null => "nothing",
        FrameworkElement shown => $"{shown.GetType().Name} {(shown.Name.Length > 0 ? shown.Name : AutomationProperties.GetAutomationId(shown))} \"{AutomationProperties.GetName(shown)}\"",
        _ => element.GetType().Name,
    };

    private static void RunSta(Action body) =>
        StaThread.RunPumped(body, TimeSpan.FromSeconds(90), "STA test body timed out.");
}
