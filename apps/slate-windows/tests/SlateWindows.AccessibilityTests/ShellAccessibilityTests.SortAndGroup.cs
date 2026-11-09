// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;

namespace SlateWindows.AccessibilityTests;

public sealed partial class ShellAccessibilityTests
{
    /// <summary>DS-17: opens the Files toolbar's Sort and group, as a reader does, and
    /// returns its row <paramref name="rowId"/> (a sort order's
    /// <c>SidebarSort.NameAscending</c>, or <c>SidebarGroupDates</c>,
    /// <c>SidebarShowTags</c>, <c>SidebarDualPaneToggle</c>). A ContextMenu is its own
    /// HWND, so the row is found from the desktop, in this process.</summary>
    private static AutomationElement OpenSortAndGroupRow(Window window, UIA3Automation automation, string rowId)
    {
        AutomationElement button = WaitForElement(window, "SidebarSortAndGroup", TimeSpan.FromSeconds(10));
        button.Patterns.Invoke.Pattern.Invoke();
        int processId = window.Properties.ProcessId.Value;
        AutomationElement? row = null;
        Assert.True(
            SpinWait.SpinUntil(
                () =>
                {
                    try
                    {
                        row = automation.GetDesktop()
                            .FindAllChildren(automation.ConditionFactory.ByProcessId(processId))
                            .Select(top => top.FindFirstDescendant(automation.ConditionFactory.ByControlType(ControlType.MenuItem)
                                .And(automation.ConditionFactory.ByAutomationId(rowId))))
                            .FirstOrDefault(entry => entry is not null);
                        return row is not null;
                    }
                    catch (Exception exception) when (IsTransientUiaFault(exception))
                    {
                        return false;
                    }
                },
                TimeSpan.FromSeconds(15)),
            $"Sort and group has no row {rowId}.");
        return row!;
    }

    /// <summary>DS-17: flips a Sort and group row, as a reader's toggle does, and the menu
    /// must close with it, as it does for a click: nothing presses Escape for it.</summary>
    private static void ToggleSortAndGroupRow(Window window, UIA3Automation automation, string rowId)
    {
        AutomationElement row = OpenSortAndGroupRow(window, automation, rowId);
        row.Patterns.Toggle.Pattern.Toggle();
        Assert.True(
            SpinWait.SpinUntil(() => !SortAndGroupIsOpen(window, automation), TimeSpan.FromSeconds(5)),
            $"Toggling {rowId} didn't close Sort and group.");
    }

    private static bool SortAndGroupIsOpen(Window window, UIA3Automation automation)
    {
        try
        {
            return automation.GetDesktop()
                .FindAllChildren(automation.ConditionFactory.ByProcessId(window.Properties.ProcessId.Value))
                .Any(top => top.FindFirstDescendant(automation.ConditionFactory.ByAutomationId("SidebarGroupDates")) is not null);
        }
        catch (Exception exception) when (IsTransientUiaFault(exception))
        {
            return false;
        }
    }

    /// <summary>A row of Sort and group while it's open.</summary>
    private static AutomationElement WaitForSortRowInOpenMenu(UIA3Automation automation, Window window, string rowId)
    {
        AutomationElement? row = null;
        Assert.True(
            SpinWait.SpinUntil(
                () =>
                {
                    try
                    {
                        row = automation.GetDesktop()
                            .FindAllChildren(automation.ConditionFactory.ByProcessId(window.Properties.ProcessId.Value))
                            .Select(top => top.FindFirstDescendant(automation.ConditionFactory.ByAutomationId(rowId)))
                            .FirstOrDefault(entry => entry is not null);
                        return row is not null;
                    }
                    catch (Exception exception) when (IsTransientUiaFault(exception))
                    {
                        return false;
                    }
                },
                TimeSpan.FromSeconds(10)),
            $"Sort and group has no row {rowId} while open.");
        return row!;
    }
}