// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.Tools;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;

namespace SlateWindows.AccessibilityTests;

/// <summary>DS-13 (#1814): the menus open from the logo with every key they had. A hosted
/// key event can't carry a system key, and a hosted popup can't take the keys, so these are
/// real keys against the launched app.</summary>
public sealed partial class ShellAccessibilityTests
{
    private static readonly (VirtualKeyShort Letter, string Menu)[] AppMenuLetters =
    [
        (VirtualKeyShort.KEY_F, "FileMenu"),
        (VirtualKeyShort.KEY_W, "WorkspaceMenu"),
        (VirtualKeyShort.KEY_B, "BaseMenu"),
        (VirtualKeyShort.KEY_E, "EditorMenu"),
        (VirtualKeyShort.KEY_N, "CanvasMenu"),
        (VirtualKeyShort.KEY_G, "GraphMenu"),
    ];

    /// <summary>Alt and each menu's letter opens the logo's popup at that menu, with the keys
    /// on its first enabled row; Esc backs out a level at a time to the note.</summary>
    [Fact]
    public void AppMenu_AltLetterOpensThatMenu() =>
        RunFindInNoteJourney("app-menu-alt-letter", (process, window, automation, editor) =>
        {
            foreach ((VirtualKeyShort letter, string menu) in AppMenuLetters)
            {
                editor.Focus();
                AssertEventuallyFocused(editor, "the note didn't take the keys.");
                PressChord(VirtualKeyShort.ALT, letter);
                AssertOpenOnItsFirstRow(window, automation, menu);
                if (menu == "FileMenu")
                {
                    // The census sees the open popup's rows, the menus and File's own.
                    AssertItemNamesAreSpeakable(process, "app-menu-file");
                }

                CloseTheAppMenu(window, automation, editor);
            }
        });

    /// <summary>With one menu open, Alt and another's letter closes it and opens the other.</summary>
    [Fact]
    public void AppMenu_AltLetterWhileOpenSwitchesMenus() =>
        RunFindInNoteJourney("app-menu-switch", (process, window, automation, editor) =>
        {
            editor.Focus();
            AssertEventuallyFocused(editor, "the note didn't take the keys.");
            PressChord(VirtualKeyShort.ALT, VirtualKeyShort.KEY_F);
            AssertOpenOnItsFirstRow(window, automation, "FileMenu");

            PressChord(VirtualKeyShort.ALT, VirtualKeyShort.KEY_W);
            AssertOpenOnItsFirstRow(window, automation, "WorkspaceMenu");
            Assert.True(
                Retry.WhileTrue(() => StateOf(window, "FileMenu") == ExpandCollapseState.Expanded, TimeSpan.FromSeconds(5)).Success,
                "File stayed open beside Workspace.");
            AssertItemNamesAreSpeakable(process, "app-menu-workspace");
            CloseTheAppMenu(window, automation, editor);
        });

    /// <summary>F10 opens the popup on its first row, File; plain Alt keeps WPF's menu mode,
    /// the keys on the logo with no popup.</summary>
    [Fact]
    public void AppMenu_F10OpensItAndPlainAltFocusesTheLogo() =>
        RunFindInNoteJourney("app-menu-f10", (process, window, automation, editor) =>
        {
            editor.Focus();
            AssertEventuallyFocused(editor, "the note didn't take the keys.");
            PressKey(VirtualKeyShort.F10);
            AssertEventuallyFocused(WaitForElement(window, "FileMenu", TimeSpan.FromSeconds(10)), "F10 didn't open the menu on File.");
            Assert.Equal(ExpandCollapseState.Expanded, StateOf(window, "AppMenu"));
            CloseTheAppMenu(window, automation, editor);

            PressKey(VirtualKeyShort.ALT);
            AutomationElement logo = WaitForElement(window, "AppMenu", TimeSpan.FromSeconds(10));
            AssertEventuallyFocused(logo, "Alt didn't put the keys on the logo.");
            Assert.NotEqual(ExpandCollapseState.Expanded, StateOf(window, "AppMenu"));
            Assert.Equal("Slate menu", logo.Name);
            Assert.Equal("Alt+F", logo.Properties.AcceleratorKey.ValueOrDefault);
            AssertAxeClean(process, "app-menu-logo");
            PressKey(VirtualKeyShort.ESCAPE);
            AssertEventuallyFocused(editor, "Esc from the logo didn't give the note its keys back.");
        });

    /// <summary>The find bar keeps its own letters: with it open, Alt+N steps to the next
    /// match and Alt+W toggles whole word, and neither opens a menu.</summary>
    [Fact]
    public void AppMenu_TheFindBarKeepsItsKeys() =>
        RunFindInNoteJourney("app-menu-find-bar", (process, window, automation, editor) =>
        {
            AutomationElement query = OpenFindBarAndType(window, editor, "alpha");
            Assert.True(query.Properties.HasKeyboardFocus.ValueOrDefault, "Ctrl+F did not land in the find bar's query");

            int before = SelectionStart(editor);
            PressChord(VirtualKeyShort.ALT, VirtualKeyShort.KEY_N);
            Assert.True(
                SpinWait.SpinUntil(() => SelectionStart(editor) != before, TimeSpan.FromSeconds(10)),
                "Alt+N in the find bar didn't step to the next match");
            Assert.NotEqual(ExpandCollapseState.Expanded, StateOf(window, "AppMenu"));

            AutomationElement wholeWord = WaitForElement(window, "FindInNoteWholeWord", TimeSpan.FromSeconds(5));
            ToggleState was = wholeWord.Patterns.Toggle.Pattern.ToggleState.Value;
            PressChord(VirtualKeyShort.ALT, VirtualKeyShort.KEY_W);
            Assert.True(
                Retry.WhileTrue(() => wholeWord.Patterns.Toggle.Pattern.ToggleState.Value == was, TimeSpan.FromSeconds(5)).Success,
                "Alt+W in the find bar didn't toggle whole word");
            Assert.NotEqual(ExpandCollapseState.Expanded, StateOf(window, "AppMenu"));
            AssertItemNamesAreSpeakable(process, "app-menu-find-bar");
        });

    private static ExpandCollapseState StateOf(Window window, string automationId) =>
        window.FindFirstDescendant(condition => condition.ByAutomationId(automationId))?
            .Patterns.ExpandCollapse.PatternOrDefault?.ExpandCollapseState.ValueOrDefault ?? ExpandCollapseState.Collapsed;

    /// <summary>The logo's popup is open at <paramref name="menuId"/>, and the keys are on
    /// that menu's first enabled row.</summary>
    /// <remarks>#1939: on the home pool's job VMs the keys land on the last row instead, so a
    /// failure reports where focus went while waiting, the menu's rows and where they sit
    /// against the window and the screen, and the foreground, focus and keys down
    /// (<see cref="KeyInputDiagnosis"/>).</remarks>
    private static void AssertOpenOnItsFirstRow(Window window, UIA3Automation automation, string menuId)
    {
        AutomationElement menu = WaitForElement(window, menuId, TimeSpan.FromSeconds(10));
        AutomationElement? first = null;
        var focusSeen = new List<string>();
        bool landed = Retry.WhileFalse(
            () =>
            {
                first = menu.FindAllChildren(condition => condition.ByControlType(ControlType.MenuItem))
                    .FirstOrDefault(row => row.IsEnabled && !row.IsOffscreen);
                AutomationElement? focused = automation.FocusedElement();
                NoteFocus(focusSeen, focused);
                return menu.Patterns.ExpandCollapse.Pattern.ExpandCollapseState.Value == ExpandCollapseState.Expanded
                    && first is not null
                    && focused is not null
                    && focused.Equals(first);
            },
            TimeSpan.FromSeconds(10)).Success;
        if (!landed)
        {
            throw new Xunit.Sdk.XunitException(
                $"{menuId} didn't open with the keys on its first row ({first?.Name}); they're on {automation.FocusedElement()?.Name} ({automation.FocusedElement()?.AutomationId}). "
                + $"Focus while waiting, in order: {string.Join(" -> ", focusSeen)}. "
                + MenuDiagnosis(window, automation, menu) + " "
                + KeyInputDiagnosis(automation) + " "
                + MenuKeysDiagnosis());
        }
    }

    /// <summary>Adds the focused element to <paramref name="seen"/> when it differs from the
    /// last one there; never throws, so it can't replace the failure it explains.</summary>
    private static void NoteFocus(List<string> seen, AutomationElement? focused)
    {
        string label;
        try
        {
            label = focused is null
                ? "none"
                : $"'{focused.Properties.Name.ValueOrDefault}' ({focused.Properties.AutomationId.ValueOrDefault})";
        }
        catch (Exception exception)
        {
            label = $"<unreadable: {exception.GetType().Name}>";
        }

        if (seen.Count < 40 && (seen.Count == 0 || seen[^1] != label))
        {
            seen.Add(label);
        }
    }

    /// <summary>The menu's state, each row with its bounds, and the window's and the
    /// screen's bounds and DPI, so a popup placed off the screen or a row left offscreen
    /// shows. Never throws.</summary>
    private static string MenuDiagnosis(Window window, UIA3Automation automation, AutomationElement menu)
    {
        var report = new System.Text.StringBuilder();
        try
        {
            report.Append($"{menu.Properties.AutomationId.ValueOrDefault} is {menu.Patterns.ExpandCollapse.PatternOrDefault?.ExpandCollapseState.ValueOrDefault}; its rows: ");
            IEnumerable<string> rows = menu.FindAllChildren()
                .Select(row =>
                    $"'{row.Properties.Name.ValueOrDefault}' {row.Properties.ControlType.ValueOrDefault}"
                    + $" enabled={row.Properties.IsEnabled.ValueOrDefault}"
                    + $" offscreen={row.Properties.IsOffscreen.ValueOrDefault}"
                    + $" focused={row.Properties.HasKeyboardFocus.ValueOrDefault}"
                    + $" at {row.Properties.BoundingRectangle.ValueOrDefault}");
            report.Append(string.Join("; ", rows)).Append('.');
        }
        catch (Exception exception)
        {
            report.Append($"<menu unreadable: {exception.GetType().Name}: {exception.Message}>.");
        }

        try
        {
            IntPtr handle = window.Properties.NativeWindowHandle.ValueOrDefault;
            report.Append($" The window is at {window.Properties.BoundingRectangle.ValueOrDefault}")
                .Append($" with DPI {(handle == IntPtr.Zero ? "unknown" : NativeMenuDiagnosis.GetDpiForWindow(handle).ToString(System.Globalization.CultureInfo.InvariantCulture))};")
                .Append($" the desktop is {automation.GetDesktop().Properties.BoundingRectangle.ValueOrDefault}.");
        }
        catch (Exception exception)
        {
            report.Append($" <window unreadable: {exception.GetType().Name}: {exception.Message}>.");
        }

        return report.ToString();
    }

    /// <summary>The keys that move within a menu, which <see cref="KeyInputDiagnosis"/>
    /// doesn't watch: one still down could carry the keys to the last row.</summary>
    private static string MenuKeysDiagnosis()
    {
        (int VirtualKey, string Name)[] watched =
        [
            (0x26, "Up"),
            (0x28, "Down"),
            (0x24, "Home"),
            (0x23, "End"),
            (0x21, "Page Up"),
            (0x22, "Page Down"),
            (0x79, "F10"),
            (0x46, "F"),
            (0x57, "W"),
        ];
        string[] down = watched
            .Where(key => (NativeForeground.GetAsyncKeyState(key.VirtualKey) & 0x8000) != 0)
            .Select(key => key.Name)
            .ToArray();
        return "Menu keys down now: " + (down.Length == 0 ? "none" : string.Join(", ", down)) + ".";
    }

    private static class NativeMenuDiagnosis
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        internal static extern uint GetDpiForWindow(IntPtr hWnd);
    }

    private static void CloseTheAppMenu(Window window, UIA3Automation automation, AutomationElement editor)
    {
        for (int press = 0; press < 4 && StateOf(window, "AppMenu") == ExpandCollapseState.Expanded; press++)
        {
            PressKey(VirtualKeyShort.ESCAPE);
            Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(200));
        }

        if (automation.FocusedElement()?.AutomationId == "AppMenu")
        {
            PressKey(VirtualKeyShort.ESCAPE);
        }

        AssertEventuallyFocused(editor, "Esc didn't give the note its keys back.");
    }
}
