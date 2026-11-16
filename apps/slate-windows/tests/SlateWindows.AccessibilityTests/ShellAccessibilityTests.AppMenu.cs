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

    /// <summary>#1939: a pointer resting where the menu opens doesn't take the keys. When the
    /// popup appears under a resting pointer WPF hovers the row there, and WPF's own guard
    /// against that lapses at Background priority, so on the job VMs the keys went to the row
    /// under the pointer (Exit, where Enter quits). That is a race, and there is no input that
    /// forces its losing side from outside the app (WPF drops a move that doesn't change the
    /// pointer's place), so this opens the menu over a resting pointer until a middle row is
    /// under it and wants the first row every time; with the guard off it fails on the first
    /// such open on the owner's desktop. Once the pointer moves, hovering a row takes the keys,
    /// and a click with the pointer still resting works the row under it.</summary>
    [Fact]
    public void AppMenu_APointerRestingWhereItOpensLeavesTheKeysOnTheFirstRow() =>
        RunFindInNoteJourney("app-menu-resting-pointer", (process, window, automation, editor) =>
        {
            try
            {
                // A move makes hovering take the keys again.
                editor.Focus();
                AssertEventuallyFocused(editor, "the note didn't take the keys.");
                PressChord(VirtualKeyShort.ALT, VirtualKeyShort.KEY_F);
                AssertOpenOnItsFirstRow(window, automation, "FileMenu");
                AutomationElement[] rows = RowsOf(window, "FileMenu");
                System.Drawing.Point resting = SettledBoundsOf(rows[rows.Length / 2]).Center();
                AutomationElement exit = rows[^1];
                System.Drawing.Point onExit = SettledBoundsOf(exit).Center();
                Mouse.Position = onExit with { X = onExit.X + 4 };
                Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(250));
                Mouse.Position = onExit with { X = onExit.X + 8 };
                AssertEventuallyFocused(exit, "Moving the pointer onto Exit didn't give it the keys.");
                CloseTheAppMenu(window, automation, editor);
                Mouse.Position = resting;
                Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(500));

                // File's popup stops somewhere a little different each open (it's placed
                // against File's row while the logo's popup still slides in), so the pointer
                // stays put and the menu opens again until a middle row is under it: a
                // separator or the first row there tests nothing, and the click below
                // mustn't land on Exit.
                AutomationElement? under = null;
                for (int open = 0; open < 8 && under is null; open++)
                {
                    if (open > 0)
                    {
                        CloseTheAppMenu(window, automation, editor);
                    }

                    editor.Focus();
                    AssertEventuallyFocused(editor, "the note didn't take the keys.");
                    PressChord(VirtualKeyShort.ALT, VirtualKeyShort.KEY_F);
                    AssertOpenOnItsFirstRow(window, automation, "FileMenu");
                    rows = RowsOf(window, "FileMenu");
                    SettledBoundsOf(rows[0]);
                    under = rows[1..^1].FirstOrDefault(row => row.BoundingRectangle.Contains(resting));
                }

                Assert.True(
                    under is not null,
                    $"Eight opens never put a middle row under the pointer at {resting}; "
                    + MenuDiagnosis(window, automation, WaitForElement(window, "FileMenu", TimeSpan.FromSeconds(5))));
                Assert.Equal(resting, Mouse.Position);
                AssertItemNamesAreSpeakable(process, "app-menu-resting-pointer");

                // A hovered menu's row opens its menu after the menu-show delay (Windows' 400 ms
                // unless changed): the row under the resting pointer stays shut well past it.
                Thread.Sleep(TimeSpan.FromMilliseconds(1500));
                Assert.True(
                    under!.Patterns.ExpandCollapse.PatternOrDefault?.ExpandCollapseState.ValueOrDefault != ExpandCollapseState.Expanded,
                    $"The resting pointer opened {DescribeForDiagnosis(under)}'s menu.");
                AssertOpenOnItsFirstRow(window, automation, "FileMenu");

                // A hover that didn't take the keys could still take the menu's selection, and
                // Down would then go on from the row under the pointer; it goes on from the first.
                PressKey(VirtualKeyShort.DOWN);
                AssertEventuallyFocused(rows[1], $"Down from the first row didn't reach the second, with the pointer resting on {DescribeForDiagnosis(under)}.");

                // A click with the pointer still resting works the row under it: a command
                // closes the menu, a menu's row opens its menu.
                string clicked = DescribeForDiagnosis(under);
                Mouse.Click();
                Assert.True(
                    Retry.WhileFalse(
                        () => StateOf(window, "AppMenu") != ExpandCollapseState.Expanded
                            || under!.Patterns.ExpandCollapse.PatternOrDefault?.ExpandCollapseState.ValueOrDefault == ExpandCollapseState.Expanded,
                        TimeSpan.FromSeconds(5)).Success,
                    $"A click on {clicked} with the pointer resting didn't work it.");
            }
            finally
            {
                // A pointer left over the window is how the job VMs met this.
                Mouse.Position = new System.Drawing.Point(0, 0);
            }
        });

    /// <summary><paramref name="row"/>'s bounds once two reads 150 ms apart agree: the popup
    /// slides in, so a read during the slide finds it short of where it stops.</summary>
    private static System.Drawing.Rectangle SettledBoundsOf(AutomationElement row)
    {
        System.Drawing.Rectangle was = row.BoundingRectangle;
        for (int read = 0; read < 20; read++)
        {
            Thread.Sleep(150);
            System.Drawing.Rectangle now = row.BoundingRectangle;
            if (now == was)
            {
                return now;
            }

            was = now;
        }

        throw new Xunit.Sdk.XunitException($"{row.Name}'s bounds never settled; last at {was}.");
    }

    private static AutomationElement[] RowsOf(Window window, string menuId) =>
        WaitForElement(window, menuId, TimeSpan.FromSeconds(5))
            .FindAllChildren(condition => condition.ByControlType(ControlType.MenuItem));

    private static AutomationElement LastRowOf(Window window, string menuId) => RowsOf(window, menuId).Last();

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
    /// failure reports the focus at each check, the menu's rows and where they sit against
    /// the window and the screen, and the foreground, focus and keys down
    /// (<see cref="KeyInputDiagnosis"/>). The check reads what it always read, in the same
    /// order: it keeps only the focused element it already fetched, and every name is read
    /// after the failure, guarded, so no diagnostic can change the check or replace the
    /// failure.</remarks>
    private static void AssertOpenOnItsFirstRow(Window window, UIA3Automation automation, string menuId)
    {
        AutomationElement menu = WaitForElement(window, menuId, TimeSpan.FromSeconds(10));
        AutomationElement? first = null;
        var focusAtEachCheck = new List<AutomationElement?>();
        bool landed = Retry.WhileFalse(
            () =>
            {
                first = menu.FindAllChildren(condition => condition.ByControlType(ControlType.MenuItem))
                    .FirstOrDefault(row => row.IsEnabled && !row.IsOffscreen);
                if (menu.Patterns.ExpandCollapse.Pattern.ExpandCollapseState.Value != ExpandCollapseState.Expanded
                    || first is null)
                {
                    return false;
                }

                AutomationElement? focused = automation.FocusedElement();
                // Retry checks about every 100 ms, so 256 holds the whole 10 s.
                if (focusAtEachCheck.Count < 256)
                {
                    focusAtEachCheck.Add(focused);
                }

                return focused is not null && focused.Equals(first);
            },
            TimeSpan.FromSeconds(10)).Success;
        if (!landed)
        {
            throw new Xunit.Sdk.XunitException(
                $"{menuId} didn't open with the keys on its first row ({DescribeForDiagnosis(first)}). "
                + "Focus at each check while it was open with a first row: "
                + (focusAtEachCheck.Count == 0
                    ? "never checked, it never was"
                    : string.Join(" -> ", Collapsed(focusAtEachCheck.Select(DescribeForDiagnosis))))
                + ". "
                + MenuDiagnosis(window, automation, menu) + " "
                + KeyInputDiagnosis(automation) + " "
                + MenuKeysDiagnosis());
        }
    }

    /// <summary>An element's name and automation id for a failure message, or why there are
    /// none; never throws, so it can't replace the failure it explains.</summary>
    private static string DescribeForDiagnosis(AutomationElement? element)
    {
        try
        {
            return element is null
                ? "none"
                : $"'{element.Properties.Name.ValueOrDefault}' ({element.Properties.AutomationId.ValueOrDefault})";
        }
        catch (Exception exception)
        {
            return $"<unreadable: {exception.GetType().Name}>";
        }
    }

    /// <summary>Each run of equal labels as one label with its count.</summary>
    private static IEnumerable<string> Collapsed(IEnumerable<string> labels)
    {
        string? run = null;
        int count = 0;
        foreach (string label in labels)
        {
            if (label == run)
            {
                count++;
                continue;
            }

            if (run is not null)
            {
                yield return count == 1 ? run : $"{run} x{count}";
            }

            run = label;
            count = 1;
        }

        if (run is not null)
        {
            yield return count == 1 ? run : $"{run} x{count}";
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
            IEnumerable<string> rows = menu.FindAllChildren().Select(DescribeRowForDiagnosis);
            report.Append(string.Join("; ", rows)).Append('.');
        }
        catch (Exception exception)
        {
            report.Append($"<menu unreadable: {exception.GetType().Name}: {exception.Message}>.");
        }

        report.Append(" The window is at ").Append(Guarded(() => window.Properties.BoundingRectangle.ValueOrDefault.ToString()))
            .Append(" with DPI ").Append(Guarded(() =>
            {
                IntPtr handle = window.Properties.NativeWindowHandle.ValueOrDefault;
                return handle == IntPtr.Zero
                    ? "unknown"
                    : NativeMenuDiagnosis.GetDpiForWindow(handle).ToString(System.Globalization.CultureInfo.InvariantCulture);
            }))
            .Append("; the desktop is ").Append(Guarded(() => automation.GetDesktop().Properties.BoundingRectangle.ValueOrDefault.ToString()))
            .Append('.');
        return report.ToString();

        static string Guarded(Func<string> read)
        {
            try
            {
                return read();
            }
            catch (Exception exception)
            {
                return $"<unreadable: {exception.GetType().Name}>";
            }
        }
    }

    /// <summary>One of a menu's rows with its state and bounds; never throws, so one row gone
    /// can't drop the others from the report.</summary>
    private static string DescribeRowForDiagnosis(AutomationElement row)
    {
        try
        {
            return $"'{row.Properties.Name.ValueOrDefault}' {row.Properties.ControlType.ValueOrDefault}"
                + $" enabled={row.Properties.IsEnabled.ValueOrDefault}"
                + $" offscreen={row.Properties.IsOffscreen.ValueOrDefault}"
                + $" focused={row.Properties.HasKeyboardFocus.ValueOrDefault}"
                + $" at {row.Properties.BoundingRectangle.ValueOrDefault}";
        }
        catch (Exception exception)
        {
            return $"<row unreadable: {exception.GetType().Name}>";
        }
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
