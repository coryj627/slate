// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.Tools;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using Point = System.Drawing.Point;
using Rectangle = System.Drawing.Rectangle;

namespace SlateWindows.AccessibilityTests;

/// <summary>DS-15 (#1816): a note has a header with its place and its mode.</summary>
public sealed partial class ShellAccessibilityTests
{
    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "GetDpiForWindow")]
    private static extern uint NoteHeaderDpiForWindow(IntPtr window);

    /// <summary>Every stop in the bar has a name that isn't a glyph; Tab meets each folder,
    /// the switch's checked option once, and ⋯, in that order; an arrow moves the choice and
    /// keeps the keys on the switch; Ctrl+Shift+E still lands in the note.</summary>
    [Fact]
    public void NoteHeader_NamesKeysAndAxe_AreClean()
    {
        string root = Path.Combine(Path.GetTempPath(), "slate-note-header-" + Guid.NewGuid().ToString("N"));
        string vault = Path.Combine(root, "Vault");
        string device = Path.Combine(root, "device");
        string logs = Path.Combine(root, "logs");
        Directory.CreateDirectory(Path.Combine(vault, "learning", "maths"));
        Directory.CreateDirectory(device);
        File.WriteAllText(Path.Combine(vault, "learning", "maths", "Lecture.md"), "# Lecture\n\nA line.\n");
        Process? process = null;
        try
        {
            using var automation = new UIA3Automation();
            var start = new ProcessStartInfo(SlateWindowsExe()) { UseShellExecute = false };
            ShellLaunch.ApplyDeviceState(start);
            start.Environment[ShellLaunch.Variable] = device;
            start.Environment["SLATE_CENSUS_INSTANCE_ID"] = "note-header-" + Guid.NewGuid().ToString("N");
            start.Environment["SLATE_LOG_DIR"] = logs;
            start.ArgumentList.Add(vault);
            process = Process.Start(start) ?? throw new Xunit.Sdk.XunitException("Slate did not start.");
            if (!HasInteractiveDesktop(process, "note-header")) { return; }
            Window window = WaitForMainWindow(process, automation, Path.Combine(logs, "slate-windows.log"), TimeSpan.FromSeconds(30));
            window.SetForeground();
            // At the app's default size with both panes the editor is about 520 wide, where the
            // bar goes compact; this pass is the full bar's. The pattern sizes in pixels, so
            // 1500 by 820 effective pixels is scaled by the window's DPI.
            double scale = NoteHeaderDpiForWindow(window.Properties.NativeWindowHandle.Value) / 96.0;
            window.Patterns.Transform.Pattern.Resize(1500 * scale, 820 * scale);
            AutomationElement tree = WaitForElement(window, "FilesTree", TimeSpan.FromSeconds(30));
            // A window can't be wider than its screen, and a CI VM's is about 1044: there the
            // right pane goes aside (Ctrl+Alt+I, with the keys in Files) so the bar is full.
            if (window.BoundingRectangle.Width < 1400 * scale)
            {
                tree.Focus();
                PressChord(VirtualKeyShort.CONTROL, VirtualKeyShort.ALT, VirtualKeyShort.KEY_I);
                Retry.WhileTrue(
                    () => window.FindFirstDescendant(automation.ConditionFactory.ByAutomationId("RightPaneLeaves")) is { IsOffscreen: false },
                    TimeSpan.FromSeconds(5));
            }

            AutomationElement folder = WaitForTreeItemStartingWith(tree, automation, "learning");
            folder.Patterns.ExpandCollapse.Pattern.Expand();
            AutomationElement maths = WaitForTreeItemStartingWith(tree, automation, "maths");
            maths.Patterns.ExpandCollapse.Pattern.Expand();
            WaitForTreeItemStartingWith(tree, automation, "Lecture.md").Focus();
            PressKey(VirtualKeyShort.RETURN);

            AutomationElement source = WaitForElement(window, "NoteHeaderSource", TimeSpan.FromSeconds(15));
            AutomationElement reading = WaitForElement(window, "NoteHeaderReading", TimeSpan.FromSeconds(5));
            AutomationElement more = WaitForElement(window, "NoteHeaderMore", TimeSpan.FromSeconds(5));
            AutomationElement[] segments = [];
            Retry.WhileFalse(
                () => (segments = window.FindAllDescendants(automation.ConditionFactory.ByAutomationId("NoteHeaderFolder"))).Length == 2,
                TimeSpan.FromSeconds(10));
            Assert.True(
                segments.Length == 2,
                $"the bar shows {segments.Length} folders in a window at {window.BoundingRectangle}; "
                + $"compact (\"Folders\" shown): {window.FindFirstDescendant(automation.ConditionFactory.ByAutomationId("NoteHeaderFolders")) is not null}; "
                + $"the foreground window: {automation.FocusedElement()?.Properties.ProcessId.ValueOrDefault} (ours {process.Id}).");
            Assert.Equal(["learning", "maths"], segments.Select(segment => segment.Name));
            foreach (AutomationElement stop in segments.Append(source).Append(reading).Append(more))
            {
                Assert.False(string.IsNullOrWhiteSpace(stop.Name), $"{stop.AutomationId} has no name.");
                Assert.DoesNotContain(stop.Name, character => character is >= '\uE000' and <= '\uF8FF');
            }

            Assert.Equal(ControlType.RadioButton, source.ControlType);
            Assert.Equal("More note actions", more.Name);
            Assert.True(source.Patterns.SelectionItem.Pattern.IsSelected.Value);

            AssertAxeClean(process, "note-header");

            // Tab: each folder, the switch's checked option, then ⋯.
            segments[0].Focus();
            AssertEventuallyFocused(segments[0], "the first folder didn't take the keys.");
            PressKey(VirtualKeyShort.TAB);
            AssertEventuallyFocused(segments[1], "Tab didn't reach the second folder.");
            PressKey(VirtualKeyShort.TAB);
            AssertEventuallyFocused(source, "Tab didn't reach the checked option.");
            PressKey(VirtualKeyShort.TAB);
            AssertEventuallyFocused(more, "Tab went to the unchecked option, not ⋯.");

            // An arrow moves the choice and keeps the keys on the switch.
            source.Focus();
            AssertEventuallyFocused(source, "the switch didn't take the keys.");
            PressKey(VirtualKeyShort.RIGHT);
            Retry.WhileFalse(() => reading.Patterns.SelectionItem.Pattern.IsSelected.Value, TimeSpan.FromSeconds(5));
            Assert.True(reading.Patterns.SelectionItem.Pattern.IsSelected.Value, "Right didn't choose Reading.");
            AssertEventuallyFocused(reading, "the arrow took the keys off the switch.");

            // Ctrl+Shift+E lands in the note, and the switch follows.
            PressChord(VirtualKeyShort.CONTROL, VirtualKeyShort.SHIFT, VirtualKeyShort.KEY_E);
            Retry.WhileFalse(() => source.Patterns.SelectionItem.Pattern.IsSelected.Value, TimeSpan.FromSeconds(5));
            Assert.True(source.Patterns.SelectionItem.Pattern.IsSelected.Value, "Ctrl+Shift+E didn't go back to Source.");
            AssertEventuallyFocused(WaitForElement(window, "MarkdownEditor", TimeSpan.FromSeconds(5)), "Ctrl+Shift+E didn't land in the note.");
        }
        finally
        {
            try { process?.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            process?.WaitForExit(10_000);
            try { Directory.Delete(root, recursive: true); } catch (IOException) { }
        }
    }

    /// <summary>How long the keys must stay on a menu's landing row with the pointer resting
    /// over another: step 0 saw the row under the pointer take them 130 to 151 ms after the
    /// landing, every time.</summary>
    private static readonly TimeSpan RestingPointerKeysStay = TimeSpan.FromSeconds(1.5);

    /// <summary>#1849's steps: Lecture split right from ⋯, Alpha opened in the right pane, the
    /// keys in its editor. Then the left ⋯ opens with the pointer resting on its last row, by
    /// its Invoke pattern (a reader's object navigation), by Enter and by Shift+F10 on the
    /// button: each time the keys land on Split Right and stay, and Down goes on from there.
    /// On main the row under the pointer took them every time (step 0, 5 of 5), and Shift+F10
    /// landed no row at all.</summary>
    [Fact]
    public void NoteHeader_APointerRestingWhereItsMenuOpensLeavesTheKeysOnTheFirstRow() =>
        RunRestingPointerJourney("note-header-resting-pointer", (process, window, automation, said) =>
        {
            QuickOpen(window, automation, "Lecture", VirtualKeyShort.RETURN);
            AssertEventuallyFocused(WaitForEditor(window, automation, "Lecture.md editor", TimeSpan.FromSeconds(10)), "Quick Open didn't land in Lecture.");
            AutomationElement only = WaitForElement(window, "NoteHeaderMore", TimeSpan.FromSeconds(10));
            only.Patterns.Invoke.Pattern.Invoke();
            WaitForOpenMenuRow(window, automation, "NoteHeaderSplitRight").Patterns.Invoke.Pattern.Invoke();
            AutomationElement[] editors = [];
            Assert.True(
                Retry.WhileFalse(() => (editors = Editors(window, automation)).Length == 2, TimeSpan.FromSeconds(10)).Success,
                $"Split Right made {editors.Length} editors, not 2.");
            editors[^1].Focus();
            AssertEventuallyFocused(editors[^1], "the right pane's editor didn't take the keys.");
            QuickOpen(window, automation, "Alpha", VirtualKeyShort.RETURN);
            AutomationElement alpha = WaitForEditor(window, automation, "Alpha.md editor", TimeSpan.FromSeconds(10));
            AssertEventuallyFocused(alpha, "Quick Open didn't land in Alpha.");
            editors = Editors(window, automation);
            Assert.True(
                editors.Length == 2 && editors[^1].Equals(alpha),
                $"Alpha didn't open in the right pane: the editors are {string.Join(", ", editors.Select(DescribeForDiagnosis))}.");

            AutomationElement more = WaitForLeftmost(window, automation, "NoteHeaderMore");
            Assert.False(more.IsOffscreen, $"the left pane's ⋯ is off the screen. {said}");
            Point resting = RestingPlaceOn(
                window,
                automation,
                () => more.Patterns.Invoke.Pattern.Invoke(),
                "NoteHeaderSplitRight",
                rows => rows.Single(row => row.AutomationId == "NoteHeaderBulkRename"));

            (string How, Action PutTheKeys, Action Open)[] opens =
            [
                ("its Invoke pattern, with the keys in the right pane's editor", () => FocusFor(alpha), () => more.Patterns.Invoke.Pattern.Invoke()),
                ("Enter on ⋯", () => FocusFor(more), () => PressKey(VirtualKeyShort.RETURN)),
                ("Shift+F10 on ⋯", () => FocusFor(more), () => PressChord(VirtualKeyShort.SHIFT, VirtualKeyShort.F10)),
            ];
            foreach ((string how, Action putTheKeys, Action open) in opens)
            {
                putTheKeys();
                RestThePointerAt(resting);
                open();
                string context = $"⋯ opened by {how}, the pointer resting on Bulk Rename Properties… at {resting}. {said}";
                AssertTheKeysStayOn(automation, WaitForOpenMenuRow(window, automation, "NoteHeaderSplitRight"), context);
                Assert.Equal(resting, Mouse.Position);
                PressKey(VirtualKeyShort.DOWN);
                AssertEventuallyFocused(WaitForOpenMenuRow(window, automation, "NoteHeaderAddProperty"), $"Down from Split Right didn't reach Add Property…: {context}");
                CloseOpenMenu(window, automation, "NoteHeaderSplitRight");
            }
        });

    /// <summary>#1849's hold gives the rows back to the pointer: with ⋯ open over a resting
    /// pointer, a move within the row under it gives that row the keys, and a click with the
    /// pointer still resting works the row (Bulk Rename's sheet opens). Nothing is held on
    /// main, so this passes there too; it pins the hold's releases.</summary>
    [Fact]
    public void NoteHeader_AMovedPointerHoversAndAStillClickWorksTheRow() =>
        RunRestingPointerJourney("note-header-moved-pointer", (process, window, automation, said) =>
        {
            QuickOpen(window, automation, "Lecture", VirtualKeyShort.RETURN);
            AutomationElement editor = WaitForEditor(window, automation, "Lecture.md editor", TimeSpan.FromSeconds(10));
            AssertEventuallyFocused(editor, "Quick Open didn't land in Lecture.");
            AutomationElement more = WaitForElement(window, "NoteHeaderMore", TimeSpan.FromSeconds(10));
            Point resting = RestingPlaceOn(
                window,
                automation,
                () => more.Patterns.Invoke.Pattern.Invoke(),
                "NoteHeaderSplitRight",
                rows => rows.Single(row => row.AutomationId == "NoteHeaderBulkRename"));

            // A move within the row under the pointer gives it the keys.
            FocusFor(editor);
            RestThePointerAt(resting);
            more.Patterns.Invoke.Pattern.Invoke();
            AutomationElement bulkRename = WaitForOpenMenuRow(window, automation, "NoteHeaderBulkRename");
            SettledBoundsOf(bulkRename);
            Mouse.Position = resting with { X = resting.X + 4 };
            Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(250));
            Mouse.Position = resting with { X = resting.X + 8 };
            AssertEventuallyFocused(bulkRename, $"Moving the pointer within Bulk Rename Properties… didn't give it the keys. {said}");
            CloseOpenMenu(window, automation, "NoteHeaderSplitRight");

            // A click with the pointer still resting works the row under it.
            FocusFor(editor);
            RestThePointerAt(resting);
            more.Patterns.Invoke.Pattern.Invoke();
            SettledBoundsOf(WaitForOpenMenuRow(window, automation, "NoteHeaderBulkRename"));
            Assert.Equal(resting, Mouse.Position);
            Mouse.Click();
            Assert.True(
                TryWaitForElement(window, "BulkRenameSheet", TimeSpan.FromSeconds(10)) is not null,
                $"A click on Bulk Rename Properties… with the pointer resting didn't open its sheet. {said}");
            PressKey(VirtualKeyShort.ESCAPE);
            AssertElementDisappears(window, automation, "BulkRenameSheet");
        });

    /// <summary>A journey for the resting-pointer facts: a vault of three notes, the window
    /// sized as <see cref="NoteHeader_NamesKeysAndAxe_AreClean"/> sizes it (a CI screen is
    /// about 1044 wide, so there it is the screen's), the pointer away from every menu, and
    /// the pointer at (0, 0) at the end, as #1944's fact leaves it. The journey is handed a
    /// line that says whether Windows animations were on and where the window was, which
    /// each failure carries.</summary>
    private static void RunRestingPointerJourney(string name, Action<Process, Window, UIA3Automation, string> journey)
    {
        string root = Path.Combine(Path.GetTempPath(), $"slate-{name}-{Guid.NewGuid():N}");
        string vault = Path.Combine(root, "Vault");
        string device = Path.Combine(root, "device");
        string logs = Path.Combine(root, "logs");
        Directory.CreateDirectory(Path.Combine(vault, "notes"));
        Directory.CreateDirectory(device);
        File.WriteAllText(Path.Combine(vault, "notes", "Lecture.md"), "# Lecture\n\nA line.\n");
        File.WriteAllText(Path.Combine(vault, "Alpha.md"), "# Alpha\n\nA line.\n");
        File.WriteAllText(Path.Combine(vault, "Beta.md"), "# Beta\n\nA line.\n");
        Process? process = null;
        try
        {
            using var automation = new UIA3Automation();
            var start = new ProcessStartInfo(SlateWindowsExe()) { UseShellExecute = false };
            ShellLaunch.ApplyDeviceState(start);
            start.Environment[ShellLaunch.Variable] = device;
            start.Environment["SLATE_CENSUS_INSTANCE_ID"] = $"{name}-{Guid.NewGuid():N}";
            start.Environment["SLATE_LOG_DIR"] = logs;
            start.ArgumentList.Add(vault);
            process = Process.Start(start) ?? throw new Xunit.Sdk.XunitException("Slate did not start.");
            if (!HasInteractiveDesktop(process, name))
            {
                return;
            }

            Mouse.Position = new Point(0, 0);
            Window window = WaitForMainWindow(process, automation, Path.Combine(logs, "slate-windows.log"), TimeSpan.FromSeconds(30));
            window.SetForeground();
            double scale = NoteHeaderDpiForWindow(window.Properties.NativeWindowHandle.Value) / 96.0;
            window.Patterns.Transform.Pattern.Resize(1500 * scale, 820 * scale);
            AutomationElement tree = WaitForElement(window, "FilesTree", TimeSpan.FromSeconds(30));
            WaitForTreeItemStartingWith(tree, automation, "Alpha.md");
            string said = $"Windows animations were {(ClientAreaAnimationIsOn() ? "on" : "off")}; the window was at {window.BoundingRectangle} "
                + $"at {scale * 100:F0}% on a desktop at {automation.GetDesktop().BoundingRectangle}.";
            TestContext.Current.TestOutputHelper?.WriteLine($"{name}: {said}");
            journey(process, window, automation, said);
        }
        catch (Exception exception) when (exception is not Xunit.Sdk.SkipException)
        {
            PreserveEvidence(name, logs);
            throw;
        }
        finally
        {
            // A pointer left over the window is how the job VMs met #1939.
            Mouse.Position = new Point(0, 0);
            try { process?.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            process?.WaitForExit(10_000);
            try { Directory.Delete(root, recursive: true); } catch (IOException) { }
        }
    }

    /// <summary>Opens a menu with the pointer away from it, reads where its rows settle, and
    /// answers the centre of the row <paramref name="pick"/> chooses, once that row is whole on
    /// the screen and the place the pointer waited at is inside none of the rows. The menu is
    /// closed again.</summary>
    private static Point RestingPlaceOn(
        Window window, UIA3Automation automation, Action open, string anyRowId, Func<AutomationElement[], AutomationElement> pick)
    {
        Mouse.Position = new Point(0, 0);
        Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(250));
        open();
        AutomationElement[] rows = OpenMenuRows(WaitForOpenMenuRow(window, automation, anyRowId), automation);
        Rectangle[] bounds = [.. rows.Select(SettledBoundsOf)];
        AutomationElement target = pick(rows);
        Rectangle place = bounds[Array.FindIndex(rows, row => row.Equals(target))];
        Rectangle screen = automation.GetDesktop().BoundingRectangle;
        Assert.True(
            !target.IsOffscreen && screen.Contains(place),
            $"{DescribeForDiagnosis(target)} isn't whole on the screen: it is at {place} on a desktop at {screen}.");
        Assert.DoesNotContain(bounds, row => row.Contains(Mouse.Position));
        CloseOpenMenu(window, automation, anyRowId);
        return place.Center();
    }

    /// <summary>Puts the pointer at <paramref name="place"/> and lets it rest there.</summary>
    private static void RestThePointerAt(Point place)
    {
        Mouse.Position = place;
        Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(250));
        Assert.Equal(place, Mouse.Position);
    }

    private static void FocusFor(AutomationElement element)
    {
        element.Focus();
        AssertEventuallyFocused(element, $"{DescribeForDiagnosis(element)} didn't take the keys.");
    }

    /// <summary>The keys land on <paramref name="row"/> and are still there every time they're
    /// read for <see cref="RestingPointerKeysStay"/>.</summary>
    private static void AssertTheKeysStayOn(UIA3Automation automation, AutomationElement row, string context)
    {
        AssertEventuallyFocused(row, $"The keys never landed on {DescribeForDiagnosis(row)}: {context}");
        var clock = Stopwatch.StartNew();
        while (clock.Elapsed < RestingPointerKeysStay)
        {
            AutomationElement? focused;
            try
            {
                focused = automation.FocusedElement();
            }
            catch (Exception exception) when (IsTransientUiaFault(exception))
            {
                Thread.Sleep(50);
                continue;
            }

            if (focused is null || !focused.Equals(row))
            {
                throw new Xunit.Sdk.XunitException(
                    $"The keys left {DescribeForDiagnosis(row)} for {DescribeForDiagnosis(focused)} {clock.ElapsedMilliseconds} ms after they "
                    + $"landed: {context}");
            }

            Thread.Sleep(50);
        }
    }

    /// <summary>The open menu's row <paramref name="rowId"/>. A context menu is its own HWND, so
    /// it is found from the desktop, in this process.</summary>
    private static AutomationElement WaitForOpenMenuRow(Window window, UIA3Automation automation, string rowId) =>
        WaitForOpenMenuRow(window, automation, automation.ConditionFactory.ByAutomationId(rowId), rowId);

    private static AutomationElement WaitForOpenMenuRow(Window window, UIA3Automation automation, ConditionBase id, string said)
    {
        AutomationElement? row = null;
        Assert.True(
            SpinWait.SpinUntil(() => (row = FindOpenMenuRow(window, automation, id)) is not null, TimeSpan.FromSeconds(10)),
            $"No open menu has a row {said}.");
        return row!;
    }

    private static AutomationElement? FindOpenMenuRow(Window window, UIA3Automation automation, string rowId) =>
        FindOpenMenuRow(window, automation, automation.ConditionFactory.ByAutomationId(rowId));

    private static AutomationElement? FindOpenMenuRow(Window window, UIA3Automation automation, ConditionBase id)
    {
        try
        {
            return automation.GetDesktop()
                .FindAllChildren(automation.ConditionFactory.ByProcessId(window.Properties.ProcessId.Value))
                .Select(top => top.FindFirstDescendant(automation.ConditionFactory.ByControlType(ControlType.MenuItem).And(id)))
                .FirstOrDefault(row => row is not null);
        }
        catch (Exception exception) when (IsTransientUiaFault(exception))
        {
            return null;
        }
    }

    /// <summary>Sort and group's and More panels' fact: a menu built at each open that lands
    /// its <paramref name="landing"/> row on every open. With the pointer away, it reads the
    /// rows, where they settle and every toggle; then, with the keys on the button and the
    /// pointer resting on the last whole row that isn't the landing one, Enter: the keys
    /// land and stay, a move within the row under the pointer gives it the keys, Escape
    /// gives the button the keys back, and every toggle is as it was.</summary>
    private static void AssertALandingRowStaysUnderARestingPointer(
        Window window,
        UIA3Automation automation,
        string buttonId,
        string menuName,
        string rowIdPrefix,
        Func<AutomationElement[], AutomationElement> landing,
        string said)
    {
        AutomationElement button = WaitForElement(window, buttonId, TimeSpan.FromSeconds(10));
        Assert.False(button.IsOffscreen, $"{menuName}'s button is off the screen. {said}");
        ConditionBase anyRow = automation.ConditionFactory.ByAutomationId(rowIdPrefix, PropertyConditionFlags.MatchSubstring);

        // Its rows, where they settle and their toggles, with the pointer away from them.
        Mouse.Position = new Point(0, 0);
        Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(250));
        button.Patterns.Invoke.Pattern.Invoke();
        AutomationElement[] rows = OpenMenuRows(WaitForOpenMenuRow(window, automation, anyRow, rowIdPrefix + "…"), automation);
        Rectangle[] bounds = [.. rows.Select(SettledBoundsOf)];
        string[] before = ToggleStatesOf(rows);
        AutomationElement land = landing(rows);
        string landId = land.AutomationId;
        Rectangle screen = automation.GetDesktop().BoundingRectangle;
        int parked = -1;
        for (int index = rows.Length - 1; index >= 0 && parked < 0; index--)
        {
            if (!rows[index].Equals(land) && !rows[index].IsOffscreen && screen.Contains(bounds[index]))
            {
                parked = index;
            }
        }

        Assert.True(
            parked >= 0,
            $"{menuName} has no row but its landing row whole on the screen: {string.Join("; ", rows.Select((row, index) => $"{DescribeForDiagnosis(row)} at {bounds[index]}"))}. {said}");
        Assert.DoesNotContain(bounds, row => row.Contains(Mouse.Position));
        string parkedId = rows[parked].AutomationId;
        string parkedName = rows[parked].Name;
        Point resting = bounds[parked].Center();
        CloseOpenMenu(window, automation, landId);

        // The keys on the button, the pointer resting on that row, and Enter.
        FocusFor(button);
        RestThePointerAt(resting);
        PressKey(VirtualKeyShort.RETURN);
        string context = $"{menuName} opened by Enter, the pointer resting on '{parkedName}' at {resting}. {said}";
        AssertTheKeysStayOn(automation, WaitForOpenMenuRow(window, automation, landId), context);
        Assert.Equal(resting, Mouse.Position);

        // A move within that row gives it the keys.
        Mouse.Position = resting with { X = resting.X + 4 };
        Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(250));
        Mouse.Position = resting with { X = resting.X + 8 };
        AssertEventuallyFocused(WaitForOpenMenuRow(window, automation, parkedId), $"Moving the pointer within '{parkedName}' didn't give it the keys: {context}");
        CloseOpenMenu(window, automation, landId);
        AssertEventuallyFocused(button, $"Escape from {menuName} didn't give its button the keys back. {said}");

        // Every toggle as it was.
        Mouse.Position = new Point(0, 0);
        Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(250));
        button.Patterns.Invoke.Pattern.Invoke();
        string[] after = ToggleStatesOf(OpenMenuRows(WaitForOpenMenuRow(window, automation, landId), automation));
        CloseOpenMenu(window, automation, landId);
        Assert.Equal(before, after);
    }

    private static string[] ToggleStatesOf(AutomationElement[] rows) =>
        [.. rows.Select(row => $"{row.Name}={row.Patterns.Toggle.PatternOrDefault?.ToggleState.ValueOrDefault}")];

    private static bool IsToggledOn(AutomationElement row) =>
        row.Patterns.Toggle.PatternOrDefault?.ToggleState.ValueOrDefault == ToggleState.On;

    /// <summary>Every row of the menu that holds <paramref name="row"/>.</summary>
    private static AutomationElement[] OpenMenuRows(AutomationElement row, UIA3Automation automation) =>
        row.Parent?.FindAllChildren(automation.ConditionFactory.ByControlType(ControlType.MenuItem))
            ?? throw new Xunit.Sdk.XunitException($"{DescribeForDiagnosis(row)} has no menu.");

    /// <summary>Escape until the menu that holds <paramref name="rowId"/> is gone.</summary>
    private static void CloseOpenMenu(Window window, UIA3Automation automation, string rowId)
    {
        for (int press = 0; press < 3 && FindOpenMenuRow(window, automation, rowId) is not null; press++)
        {
            PressKey(VirtualKeyShort.ESCAPE);
        }

        Assert.True(
            SpinWait.SpinUntil(() => FindOpenMenuRow(window, automation, rowId) is null, TimeSpan.FromSeconds(5)),
            $"Escape didn't close the menu with {rowId}.");
    }

    private static AutomationElement[] Editors(Window window, UIA3Automation automation) =>
        [.. window.FindAllDescendants(automation.ConditionFactory.ByAutomationId("MarkdownEditor")).OrderBy(editor => editor.BoundingRectangle.X)];

    private static AutomationElement WaitForLeftmost(Window window, UIA3Automation automation, string automationId)
    {
        AutomationElement[] found = [];
        Assert.True(
            SpinWait.SpinUntil(
                () => (found = window.FindAllDescendants(automation.ConditionFactory.ByAutomationId(automationId))).Length > 0,
                TimeSpan.FromSeconds(10)),
            $"No {automationId}.");
        return found.OrderBy(element => element.BoundingRectangle.X).First();
    }

    /// <summary>Whether Windows animates controls (SPI_GETCLIENTAREAANIMATION), which slides a
    /// menu in under a resting pointer.</summary>
    private static bool ClientAreaAnimationIsOn() =>
        NoteHeaderSystemParameters(0x1042, 0, out int on, 0) && on != 0;

    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "SystemParametersInfoW")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool NoteHeaderSystemParameters(uint action, uint parameter, out int value, uint update);
}
