// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;

namespace SlateWindows.AccessibilityTests;

public sealed partial class ShellAccessibilityTests
{
    /// <summary>DS-16: opens a right-pane panel the way a reader does. A panel in the
    /// strip by its row; one of the other twelve through More panels and its menu's
    /// row, which a toggle picks as a click does. Returns once the panel is open: the
    /// strip's row selected, or the button's name naming it.</summary>
    private static void OpenRailPanel(Window window, UIA3Automation automation, string title)
    {
        AutomationElement rail = WaitForElement(window, "RightPaneLeaves", TimeSpan.FromSeconds(30));
        AutomationElement? row = null;
        int rows = 0;
        // Retried: the strip's rows materialize lazily on the gate runner (the W4-1
        // playbook: a one-shot enumeration races composition).
        Assert.True(
            SpinWait.SpinUntil(
                () =>
                {
                    try
                    {
                        AutomationElement[] items = rail.FindAllDescendants(automation.ConditionFactory.ByControlType(ControlType.ListItem));
                        rows = items.Length;
                        row = items.FirstOrDefault(item => (item.Properties.Name.ValueOrDefault ?? string.Empty) == title);
                        return row is not null || rows >= 5;
                    }
                    catch (Exception exception) when (IsTransientUiaFault(exception))
                    {
                        return false;
                    }
                },
                TimeSpan.FromSeconds(15)),
            $"The strip showed {rows} rows.");
        if (row is not null)
        {
            row.Patterns.SelectionItem.Pattern.Select();
            Assert.True(
                SpinWait.SpinUntil(() => IsChosen(row), TimeSpan.FromSeconds(10)),
                $"The strip's row {title} did not take the selection.");
            return;
        }

        AutomationElement more = WaitForElement(window, "RightPaneMorePanels", TimeSpan.FromSeconds(10));
        more.Patterns.Invoke.Pattern.Invoke();
        int processId = window.Properties.ProcessId.Value;
        AutomationElement? item = null;
        Assert.True(
            SpinWait.SpinUntil(
                () =>
                {
                    try
                    {
                        // A ContextMenu is its own HWND: from the desktop, in this process,
                        // whatever its popup's own control type.
                        item = automation.GetDesktop()
                            .FindAllChildren(automation.ConditionFactory.ByProcessId(processId))
                            .Select(top => top.FindFirstDescendant(automation.ConditionFactory.ByControlType(ControlType.MenuItem)
                                .And(automation.ConditionFactory.ByName(title))
                                .And(automation.ConditionFactory.ByAutomationId("RightPaneMorePanels.", PropertyConditionFlags.MatchSubstring))))
                            .FirstOrDefault(entry => entry is not null);
                        return item is not null;
                    }
                    catch (Exception exception) when (IsTransientUiaFault(exception))
                    {
                        return false;
                    }
                },
                TimeSpan.FromSeconds(15)),
            $"More panels has no row named {title}.");
        item!.Patterns.Toggle.Pattern.Toggle();
        Assert.True(
            SpinWait.SpinUntil(
                () =>
                {
                    try
                    {
                        return more.Properties.Name.ValueOrDefault == uniffi.slate_uniffi.SlateUniffiMethods.MorePanelsName(title);
                    }
                    catch (Exception exception) when (IsTransientUiaFault(exception))
                    {
                        return false;
                    }
                },
                TimeSpan.FromSeconds(10)),
            $"More panels did not open {title}.");
    }

    /// <summary>DS-16: the rail's landing for the open panel <paramref name="title"/>:
    /// its row when the strip holds it, else More panels, whose name names it.</summary>
    private static void AssertLandedOnRail(Window window, UIA3Automation automation, string title, string message)
    {
        AutomationElement rail = WaitForElement(window, "RightPaneLeaves", TimeSpan.FromSeconds(10));
        bool inStrip = rail.FindAllDescendants(automation.ConditionFactory.ByControlType(ControlType.ListItem))
            .Any(item => (item.Properties.Name.ValueOrDefault ?? string.Empty) == title);
        if (inStrip)
        {
            AssertFocusedListItem(automation, rail, title, message);
            return;
        }

        Assert.True(
            SpinWait.SpinUntil(
                () =>
                {
                    try
                    {
                        return automation.FocusedElement() is { } focused
                            && focused.Properties.AutomationId.ValueOrDefault == "RightPaneMorePanels"
                            && focused.Properties.Name.ValueOrDefault == uniffi.slate_uniffi.SlateUniffiMethods.MorePanelsName(title);
                    }
                    catch (Exception exception) when (IsTransientUiaFault(exception))
                    {
                        return false;
                    }
                },
                TimeSpan.FromSeconds(10)),
            $"{message} (More panels, {title}) Focus is on {DescribeFocusedElement(automation)}. {FocusDiagnosis()}");
    }
}
