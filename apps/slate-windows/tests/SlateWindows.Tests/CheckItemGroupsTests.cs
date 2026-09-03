// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.ComponentModel;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Data;
using SlateWindows.Canvas;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>
/// m1 (§C-unit's minors table; the reconciliation's R-1), fixed
/// 2026-09-03: a radio group rendered as CHECK menu items — WPF has no
/// radio menu item — bound OneWay to the group's level. A click on the
/// already-selected item toggles its IsChecked to false locally before
/// the command runs; the command found the value unchanged, returned
/// early, and nothing re-pushed the binding, so the selected item read
/// unchecked. Five groups shipped that way (math speech style, math
/// verbosity, braille code, code preamble, canvas verbosity). The fix is
/// one rule: a re-selection re-raises the group's notifications, and
/// persists and speaks nothing.
/// </summary>
public sealed class CheckItemGroupsTests
{
    /// <summary>The symptom itself, through a real menu item and the
    /// toggle pattern a screen reader drives: the item bound to the
    /// selected canvas verbosity, toggled, reads checked again.</summary>
    [Fact]
    public void ReSelectingTheCurrentCanvasVerbosityReAssertsItsCheckItem() => RunSta(() =>
    {
        var preferences = new CanvasPreferencesViewModel(null);
        Assert.True(preferences.IsVerbosityStandard);
        var item = new CheckMenuItem
        {
            IsCheckable = true,
            DataContext = preferences,
            Command = preferences.SetVerbosityCommand,
            CommandParameter = "standard",
        };
        _ = BindingOperations.SetBinding(
            item,
            MenuItem.IsCheckedProperty,
            new Binding(nameof(CanvasPreferencesViewModel.IsVerbosityStandard)) { Mode = BindingMode.OneWay });
        Assert.True(item.IsChecked);

        // The click path: WPF toggles the local value at once and DEFERS
        // the Click event and the command to the dispatcher at render
        // priority — so the pump below is the render the app gets.
        var peer = UIElementAutomationPeer.CreatePeerForElement(item);
        var toggle = Assert.IsAssignableFrom<IToggleProvider>(peer.GetPattern(PatternInterface.Toggle));
        toggle.Toggle();
        PumpDeferredClicks();

        Assert.True(item.IsChecked, "the selected item read unchecked after its own re-click (m1)");
        Assert.True(preferences.IsVerbosityStandard);
        Assert.NotNull(BindingOperations.GetBindingExpression(item, MenuItem.IsCheckedProperty));

        // The second half: through the pattern, a DIFFERENT item changes
        // the level — the stock peer only flipped the check.
        var verbose = new CheckMenuItem
        {
            IsCheckable = true,
            DataContext = preferences,
            Command = preferences.SetVerbosityCommand,
            CommandParameter = "verbose",
        };
        _ = BindingOperations.SetBinding(
            verbose,
            MenuItem.IsCheckedProperty,
            new Binding(nameof(CanvasPreferencesViewModel.IsVerbosityVerbose)) { Mode = BindingMode.OneWay });
        var verbosePeer = UIElementAutomationPeer.CreatePeerForElement(verbose);
        Assert.IsAssignableFrom<IToggleProvider>(verbosePeer.GetPattern(PatternInterface.Toggle)).Toggle();
        PumpDeferredClicks();
        Assert.True(preferences.IsVerbosityVerbose, "the toggle pattern flipped the check and never ran the command");
        Assert.True(verbose.IsChecked);
        Assert.False(item.IsChecked);
        Assert.Equal(AutomationControlType.MenuItem, verbosePeer.GetAutomationControlType());
    });

    /// <summary>The four editor groups, at the view model: a re-selection
    /// raises the group's notifications and speaks nothing; a change
    /// still speaks its sentence.</summary>
    [Fact]
    public void ReSelectingTheCurrentEditorLevelsReAssertTheirCheckItems()
    {
        var announced = new List<A11yEvent>();
        using var preferences = new EditorPreferencesViewModel(announced.Add);
        var raised = new List<string>();
        preferences.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? "");

        (System.Windows.Input.ICommand Command, string Current, string[] Group)[] groups =
        [
            (preferences.SetMathSpeechStyleCommand, "clearSpeak",
                [nameof(EditorPreferencesViewModel.IsMathSpeechClearSpeak), nameof(EditorPreferencesViewModel.IsMathSpeechSimpleSpeak)]),
            (preferences.SetMathVerbosityCommand, "medium",
                [nameof(EditorPreferencesViewModel.IsMathVerbosityTerse), nameof(EditorPreferencesViewModel.IsMathVerbosityMedium), nameof(EditorPreferencesViewModel.IsMathVerbosityVerbose)]),
            (preferences.SetMathBrailleCodeCommand, "nemeth",
                [nameof(EditorPreferencesViewModel.IsMathBrailleNemeth), nameof(EditorPreferencesViewModel.IsMathBrailleUeb)]),
            (preferences.SetCodePreambleVerbosityCommand, "preambleOnly",
                [nameof(EditorPreferencesViewModel.IsCodeVerbosityPreambleOnly), nameof(EditorPreferencesViewModel.IsCodeVerbosityFirstLine), nameof(EditorPreferencesViewModel.IsCodeVerbosityAllTokens)]),
        ];
        foreach ((System.Windows.Input.ICommand command, string current, string[] group) in groups)
        {
            raised.Clear();
            announced.Clear();
            command.Execute(current);
            foreach (string name in group)
            {
                Assert.Contains(name, raised);
            }
            Assert.Empty(announced);
        }

        // A change still speaks — the re-assertion did not swallow it.
        raised.Clear();
        preferences.SetMathVerbosityCommand.Execute("terse");
        Assert.Contains(nameof(EditorPreferencesViewModel.IsMathVerbosityTerse), raised);
        Assert.Single(announced);
    }

    /// <summary>Run what a menu item queued at render priority — its Click
    /// and its command — the way the app's next render does.</summary>
    private static void PumpDeferredClicks()
    {
        var frame = new System.Windows.Threading.DispatcherFrame();
        _ = System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.Background,
            () => frame.Continue = false);
        System.Windows.Threading.Dispatcher.PushFrame(frame);
    }

    private static void RunSta(Action body)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                body();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(120)), "STA test body timed out.");
        if (failure is not null)
        {
            throw failure;
        }
    }
}
