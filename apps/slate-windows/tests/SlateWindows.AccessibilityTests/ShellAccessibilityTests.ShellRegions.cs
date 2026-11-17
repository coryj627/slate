// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Concurrent;
using System.Diagnostics;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;

namespace SlateWindows.AccessibilityTests;

public sealed partial class ShellAccessibilityTests
{
    /// <summary>W7-6 (#1240) spec §5: F6 walks the ring forward from the
    /// Files tree and Shift+F6 walks it back; hiding the right pane drops
    /// both right stops; a zero-tab vault stops on the empty pane. The Files
    /// region's landing is the selected file's row, else the tree's first row
    /// unselected (W7-7 PR 4, R-5 as the owner amended it) — a row of the
    /// tree, never the bare tree.</summary>
    [Fact]
    public void ShellRegions_F6CyclesForwardAndShiftF6Back()
    {
        string root = Path.Combine(Path.GetTempPath(), "slate-shell-regions-" + Guid.NewGuid().ToString("N"));
        string vault = Path.Combine(root, "Vault");
        string logs = Path.Combine(root, "logs");
        Directory.CreateDirectory(vault);
        File.WriteAllText(Path.Combine(vault, "alpha.md"), "# Alpha\n\nBody.\n");

        Process? process = null;
        try
        {
            var start = new ProcessStartInfo(SlateWindowsExe()) { UseShellExecute = false };
            ShellLaunch.ApplyDeviceState(start);
            start.ArgumentList.Add(vault);
            start.Environment["SLATE_CENSUS_INSTANCE_ID"] = "shell-regions-" + Guid.NewGuid().ToString("N");
            start.Environment["SLATE_LOG_DIR"] = logs;
            process = Process.Start(start) ?? throw new Xunit.Sdk.XunitException("Slate did not start.");
            if (!HasInteractiveDesktop(process, "shell-regions")) { return; }
            using var automation = new UIA3Automation();
            Window window = WaitForMainWindow(process, automation, Path.Combine(logs, "slate-windows.log"), TimeSpan.FromSeconds(30));
            window.SetForeground();
            WaitForElement(window, "RightPaneLeaves", TimeSpan.FromSeconds(30));

            // Zero tabs: Files → empty pane → leaf/rail → status bar → menu bar → Files.
            AutomationElement tree = WaitForElement(window, "FilesTree", TimeSpan.FromSeconds(10));
            tree.Focus();
            AssertFilesTreeRegionFocused(window, automation, tree, "The Files tree did not take focus.");
            PressKey(VirtualKeyShort.F6);
            AssertEventuallyFocused(WaitForElement(window, "ContentPane", TimeSpan.FromSeconds(10)), "F6 from Files with no tab did not land on the empty editor pane.");
            PressKey(VirtualKeyShort.F6);
            AssertRightPaneStop(window, automation, "F6 from the empty pane did not land in the right pane.");
            PressKey(VirtualKeyShort.F6);
            AdvancePastTheRailIfItTookFocus(window, automation);
            AssertEventuallyFocused(WaitForElement(window, "StatusBar", TimeSpan.FromSeconds(10)), "F6 did not reach the status bar.");
            PressKey(VirtualKeyShort.F6);
            AssertEventuallyFocused(WaitForElement(window, "AppMenu", TimeSpan.FromSeconds(10)), "F6 from the status bar did not wrap to the menu bar.");
            PressKey(VirtualKeyShort.F6);
            AssertFilesTreeRegionFocused(window, automation, tree, "F6 from the menu bar did not land on Files.");

            // Open a note: Files → tab bar → editor → … ; then Shift+F6 back.
            // The tree item's automation name is "{DisplayName}, {kind}"
            // ("alpha.md, file"), so this matches by prefix the way the
            // other journeys in this file do rather than an exact name.
            AutomationElement note = WaitForTreeItemStartingWith(tree, automation, "alpha.md");
            note.Patterns.SelectionItem.Pattern.Select();
            AutomationElement editor = WaitForEditor(window, automation, "alpha.md editor", TimeSpan.FromSeconds(10));
            // Selecting the row shows the note and leaves focus where it
            // was (W7-7, R-2); UIA's Select() moves no keyboard focus, and
            // once a TreeViewItem is selected, WPF's TreeView automation
            // peer throws InvalidOperationException from SetFocus() on the
            // TreeView container itself (a known WPF/UIA quirk for
            // ItemsControl containers with a live selection) — focus the
            // selected row instead and confirm with a descendant check
            // against the tree, the same pattern the right-pane rail uses
            // above for its own container-vs-selected-item quirk.
            FocusFilesTreeSelection(window, automation, "alpha.md", "The Files tree did not take focus back.");
            PressKey(VirtualKeyShort.F6);
            AutomationElement tabs = WaitForElement(window, "WorkspaceTabs", TimeSpan.FromSeconds(10));
            AutomationElement tab = FindDescendantWithin(tabs, automation.ConditionFactory.ByControlType(ControlType.TabItem), TimeSpan.FromSeconds(10))
                ?? throw new Xunit.Sdk.XunitException("No tab item.");
            AssertEventuallyFocused(tab, "F6 from Files did not land on the tab bar.");
            PressKey(VirtualKeyShort.F6);
            AssertEventuallyFocused(editor, "F6 from the tab bar did not land in the editor.");

            // With a note open the right pane has a leaf body, so the ring's
            // two right stops are both live: forward to them and the status
            // bar, then back through them to the editor. The zero-tab walk
            // above cannot cover this — its Outline leaf is empty.
            PressKey(VirtualKeyShort.F6);
            AssertRightPaneStop(window, automation, "F6 from the editor did not land in the right pane.");
            PressKey(VirtualKeyShort.F6);
            AdvancePastTheRailIfItTookFocus(window, automation);
            AssertEventuallyFocused(WaitForElement(window, "StatusBar", TimeSpan.FromSeconds(10)), "F6 from the right pane did not reach the status bar.");
            PressChord(VirtualKeyShort.SHIFT, VirtualKeyShort.F6);
            AssertRightPaneStop(window, automation, "Shift+F6 from the status bar did not return to the right pane.");
            RetreatPastTheRightPane(window, automation);
            AssertEventuallyFocused(WaitForElement(window, "MarkdownEditor", TimeSpan.FromSeconds(10)), "Shift+F6 from the right pane did not return to the editor.");

            PressChord(VirtualKeyShort.SHIFT, VirtualKeyShort.F6);
            AssertEventuallyFocused(tab, "Shift+F6 from the editor did not return to the tab bar.");
            PressChord(VirtualKeyShort.SHIFT, VirtualKeyShort.F6);
            AssertFilesTreeRegionFocused(window, automation, tree, "Shift+F6 from the tab bar did not return to Files.");
            PressChord(VirtualKeyShort.SHIFT, VirtualKeyShort.F6);
            AssertEventuallyFocused(WaitForElement(window, "AppMenu", TimeSpan.FromSeconds(10)), "Shift+F6 from Files did not land on the menu bar.");
            PressKey(VirtualKeyShort.ESCAPE);
            // Spec §4: Escape leaves menu mode and WPF restores focus to
            // where the menu took it from — the Files region, the origin of
            // the Shift+F6 that landed on the bar. Restored to the SELECTED
            // alpha.md row, not the TreeView container, so this is the
            // region check the two presses above already use.
            AssertFilesTreeRegionFocused(window, automation, tree, "Escape from the menu bar did not return to the origin region.");

            // Hidden right pane: editor → status bar directly.
            editor.Focus();
            AssertEventuallyFocused(editor, "The editor did not take focus.");
            PressChord(VirtualKeyShort.CONTROL, VirtualKeyShort.ALT, VirtualKeyShort.KEY_I);
            AssertElementDisappears(window, automation, "RightPaneLeaves");
            editor.Focus();
            PressKey(VirtualKeyShort.F6);
            AssertEventuallyFocused(WaitForElement(window, "StatusBar", TimeSpan.FromSeconds(10)), "With the right pane hidden, F6 from the editor did not skip to the status bar.");

            AssertAxeClean(process, "shell-regions");
        }
        finally
        {
            try { process?.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            try { Directory.Delete(root, recursive: true); } catch (IOException) { }
        }
    }

    /// <summary>DS-7 (#1808): the status bar's toggles through UIA's Toggle, the path that
    /// runs no command. A hidden Files sidebar leaves the ring and takes its stops with it,
    /// the toggle follows a route that brings it back, and with both panes hidden the
    /// window is still clean.</summary>
    [Fact]
    public void PaneToggles_HideThePanesFromTheRingAndARouteBringsThemBack()
    {
        string root = Path.Combine(Path.GetTempPath(), "slate-pane-toggles-" + Guid.NewGuid().ToString("N"));
        string vault = Path.Combine(root, "Vault");
        string logs = Path.Combine(root, "logs");
        Directory.CreateDirectory(vault);
        File.WriteAllText(Path.Combine(vault, "alpha.md"), "# Alpha\n\nBody.\n");

        Process? process = null;
        try
        {
            var start = new ProcessStartInfo(SlateWindowsExe()) { UseShellExecute = false };
            ShellLaunch.ApplyDeviceState(start);
            start.ArgumentList.Add(vault);
            start.Environment["SLATE_CENSUS_INSTANCE_ID"] = "pane-toggles-" + Guid.NewGuid().ToString("N");
            start.Environment["SLATE_LOG_DIR"] = logs;
            process = Process.Start(start) ?? throw new Xunit.Sdk.XunitException("Slate did not start.");
            if (!HasInteractiveDesktop(process, "pane-toggles")) { return; }
            using var automation = new UIA3Automation();
            Window window = WaitForMainWindow(process, automation, Path.Combine(logs, "slate-windows.log"), TimeSpan.FromSeconds(30));
            window.SetForeground();
            WaitForElement(window, "RightPaneLeaves", TimeSpan.FromSeconds(30));
            AutomationElement files = WaitForElement(window, "FilesSidebarToggle", TimeSpan.FromSeconds(10));
            AutomationElement right = WaitForElement(window, "RightPaneToggle", TimeSpan.FromSeconds(10));
            Assert.Equal(ToggleState.On, files.Patterns.Toggle.Pattern.ToggleState.Value);
            Assert.Equal(ToggleState.On, right.Patterns.Toggle.Pattern.ToggleState.Value);
            Assert.Equal("Files sidebar", files.Name);
            Assert.Equal("Right pane", right.Name);

            files.Patterns.Toggle.Pattern.Toggle();
            AssertElementDisappears(window, automation, "FilesPane");
            Assert.Equal(ToggleState.Off, files.Patterns.Toggle.Pattern.ToggleState.Value);

            // The ring steps from the menu bar straight to the empty pane.
            AutomationElement statusBar = WaitForElement(window, "StatusBar", TimeSpan.FromSeconds(10));
            ReassertForegroundForAChord(window);
            statusBar.Focus();
            AssertEventuallyFocused(statusBar, "The status bar did not take focus.");
            PressKey(VirtualKeyShort.F6);
            AssertEventuallyFocused(WaitForElement(window, "AppMenu", TimeSpan.FromSeconds(10)), "F6 from the status bar did not land on the menu bar.");
            PressKey(VirtualKeyShort.F6);
            AssertEventuallyFocused(WaitForElement(window, "ContentPane", TimeSpan.FromSeconds(10)), "With Files hidden, F6 from the menu bar did not skip to the empty pane.");

            // A route that needs the pane shows it, and the toggle follows.
            PressChord(VirtualKeyShort.CONTROL, VirtualKeyShort.ALT, VirtualKeyShort.KEY_F);
            WaitForElement(window, "FilesPane", TimeSpan.FromSeconds(10));
            Assert.Equal(ToggleState.On, files.Patterns.Toggle.Pattern.ToggleState.Value);

            files.Patterns.Toggle.Pattern.Toggle();
            AssertElementDisappears(window, automation, "FilesPane");
            right.Patterns.Toggle.Pattern.Toggle();
            AssertElementDisappears(window, automation, "RightPaneLeaves");
            Assert.Equal(ToggleState.Off, right.Patterns.Toggle.Pattern.ToggleState.Value);
            AssertAxeClean(process, "pane-toggles");
        }
        finally
        {
            try { process?.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            try { Directory.Delete(root, recursive: true); } catch (IOException) { }
        }
    }

    /// <summary>DS-8 (#1809): the tab strip's real keys. Delete closes the focused tab and
    /// Ctrl+F4 the active one, and the strip stays clean with a kind icon and a close glyph
    /// in every tab.</summary>
    [Fact]
    public void TabStrip_DeleteAndCtrlF4CloseAndTheStripIsClean()
    {
        string root = Path.Combine(Path.GetTempPath(), "slate-tab-strip-" + Guid.NewGuid().ToString("N"));
        string vault = Path.Combine(root, "Vault");
        string logs = Path.Combine(root, "logs");
        Directory.CreateDirectory(vault);
        foreach (string name in new[] { "alpha", "beta", "gamma" })
        {
            File.WriteAllText(Path.Combine(vault, name + ".md"), $"# {name}\n\nBody.\n");
        }

        Process? process = null;
        try
        {
            var start = new ProcessStartInfo(SlateWindowsExe()) { UseShellExecute = false };
            ShellLaunch.ApplyDeviceState(start);
            start.ArgumentList.Add(vault);
            start.Environment["SLATE_CENSUS_INSTANCE_ID"] = "tab-strip-" + Guid.NewGuid().ToString("N");
            start.Environment["SLATE_LOG_DIR"] = logs;
            process = Process.Start(start) ?? throw new Xunit.Sdk.XunitException("Slate did not start.");
            if (!HasInteractiveDesktop(process, "tab-strip")) { return; }
            using var automation = new UIA3Automation();
            Window window = WaitForMainWindow(process, automation, Path.Combine(logs, "slate-windows.log"), TimeSpan.FromSeconds(30));
            window.SetForeground();
            AutomationElement tree = WaitForElement(window, "FilesTree", TimeSpan.FromSeconds(30));
            foreach (string name in new[] { "alpha.md", "beta.md", "gamma.md" })
            {
                ReassertForegroundForAChord(window);
                tree.Focus();
                WaitForTreeItemStartingWith(tree, automation, name).Patterns.SelectionItem.Pattern.Select();
                PressChord(VirtualKeyShort.CONTROL, VirtualKeyShort.ENTER);
                Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(500));
            }

            AutomationElement tabs = WaitForElement(window, "WorkspaceTabs", TimeSpan.FromSeconds(10));
            int TabCount() => tabs.FindAllChildren(automation.ConditionFactory.ByControlType(ControlType.TabItem)).Length;
            void AwaitTabs(int expected, string what)
            {
                DateTime deadline = DateTime.UtcNow.AddSeconds(15);
                while (TabCount() != expected && DateTime.UtcNow < deadline)
                {
                    Thread.Sleep(100);
                }

                Assert.True(TabCount() == expected, $"{what}: {TabCount()} tabs, wanting {expected}.");
            }

            AwaitTabs(3, "opening three notes");
            AssertAxeClean(process, "tab-strip");

            ReassertForegroundForAChord(window);
            tree.Focus();
            PressKey(VirtualKeyShort.F6);
            AssertEventuallyFocused(
                tabs.FindAllChildren(automation.ConditionFactory.ByControlType(ControlType.TabItem)).Single(tab => tab.Patterns.SelectionItem.Pattern.IsSelected.Value),
                "F6 from Files did not land on the open tab.");
            PressKey(VirtualKeyShort.DELETE);
            AwaitTabs(2, "Delete on the focused tab");

            ReassertForegroundForAChord(window);
            PressChord(VirtualKeyShort.CONTROL, VirtualKeyShort.F4);
            AwaitTabs(1, "Ctrl+F4");
        }
        finally
        {
            try { process?.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            try { Directory.Delete(root, recursive: true); } catch (IOException) { }
        }
    }

    /// <summary>WB-5 (#1281; W7-6 §4): F6 and Shift+F6 from inside the
    /// editor's Ctrl+E popover are a no-op. Focus stays on the popover's
    /// Close button, and the desktop listener hears nothing from Slate. With
    /// focus moved out of the still-open popover (Ctrl+Alt+F, the Files
    /// filter), F6 walks the ring again, to the tab bar.</summary>
    [Fact]
    public void EditorPopover_F6AndShiftF6StayInsideIt()
    {
        string root = Path.Combine(Path.GetTempPath(), "slate-popover-ring-" + Guid.NewGuid().ToString("N"));
        string vault = Path.Combine(root, "Vault");
        string logs = Path.Combine(root, "logs");
        Directory.CreateDirectory(vault);
        File.WriteAllText(Path.Combine(vault, "target.md"), "# Target\n\nThe embedded body.\n");
        File.WriteAllText(Path.Combine(vault, "source.md"), "# Source\n\n![[target]]\n\nAfter the embed.\n");

        Process? process = null;
        try
        {
            var start = new ProcessStartInfo(SlateWindowsExe()) { UseShellExecute = false };
            ShellLaunch.ApplyDeviceState(start);
            start.ArgumentList.Add(vault);
            start.Environment["SLATE_CENSUS_INSTANCE_ID"] = "popover-ring-" + Guid.NewGuid().ToString("N");
            start.Environment["SLATE_LOG_DIR"] = logs;
            process = Process.Start(start) ?? throw new Xunit.Sdk.XunitException("Slate did not start.");
            if (!HasInteractiveDesktop(process, "popover-ring")) { return; }
            using var automation = new UIA3Automation();
            string log = Path.Combine(logs, "slate-windows.log");
            Window window = WaitForMainWindow(process, automation, log, TimeSpan.FromSeconds(30));
            window.SetForeground();
            _ = WaitForElement(window, "RightPaneLeaves", TimeSpan.FromSeconds(30));
            var heard = new ConcurrentQueue<ReceivedNotification>();
            using DesktopNotificationListener listener = ListenOnTheDesktop(automation, heard);

            AutomationElement tree = WaitForElement(window, "FilesTree", TimeSpan.FromSeconds(10));
            WaitForTreeItemStartingWith(tree, automation, "source.md").Patterns.SelectionItem.Pattern.Select();
            AutomationElement editor = WaitForEditor(window, automation, "source.md editor", TimeSpan.FromSeconds(10));
            FocusTheEditor(window, editor);
            PlaceCaretAtText(editor, "![[target]]");
            PressChord(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_E);
            _ = WaitForElement(window, "EditorInteractionPopover", TimeSpan.FromSeconds(10));
            AutomationElement close = WaitForElement(window, "EditorPopoverClose", TimeSpan.FromSeconds(10));
            AssertEventuallyFocused(close, "Ctrl+E on the embed did not focus the popover's Close button.");
            AssertItemNamesAreSpeakable(process, "editor-popover-ring");

            // Whatever opening the popover raised has arrived; the presses
            // below must add nothing.
            int mark = QuietMark(heard, process.Id);
            AssertAKeyKeepsFocus(
                automation, close, () => PressKey(VirtualKeyShort.F6), "F6 inside the popover moved focus (W7-6 §4, #1281).");
            AssertAKeyKeepsFocus(
                automation,
                close,
                () => PressChord(VirtualKeyShort.SHIFT, VirtualKeyShort.F6),
                "Shift+F6 inside the popover moved focus (W7-6 §4, #1281).");
            AwaitHeard(
                () => [.. HeardFrom(heard, process.Id).Skip(mark).Select(notification => notification.DisplayString)],
                [],
                TimeSpan.FromSeconds(1),
                log);

            // Focus leaves the popover without closing it: the ring works again.
            PressChord(VirtualKeyShort.CONTROL, VirtualKeyShort.ALT, VirtualKeyShort.KEY_F);
            AssertEventuallyFocused(
                WaitForElement(window, "SidebarFilter", TimeSpan.FromSeconds(10)),
                "Ctrl+Alt+F did not move focus to the Files filter.");
            Assert.NotNull(window.FindFirstDescendant(automation.ConditionFactory.ByAutomationId("EditorInteractionPopover")));
            PressKey(VirtualKeyShort.F6);
            AutomationElement tabs = WaitForElement(window, "WorkspaceTabs", TimeSpan.FromSeconds(10));
            Assert.True(
                SpinWait.SpinUntil(() => automation.FocusedElement() is { } focused && IsDescendantOf(focused, tabs), TimeSpan.FromSeconds(10)),
                $"F6 with focus outside the open popover did not move to the tab bar. {FocusDiagnosis()}");
        }
        catch (Exception)
        {
            PreserveEvidence(nameof(EditorPopover_F6AndShiftF6StayInsideIt), logs);
            throw;
        }
        finally
        {
            try { process?.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            try { Directory.Delete(root, recursive: true); } catch (IOException) { }
        }
    }

    /// <summary>A press that must move nothing. Focus is read on
    /// <paramref name="element"/> once before <paramref name="press"/> (the
    /// premise), the key is pressed, and from the press on every read for
    /// <see cref="FocusSettle"/>, the time a landing would take to arrive,
    /// must see it there: the first that sees it gone fails
    /// (<see cref="FocusSettleWatch.WatchAcrossAKey"/>).</summary>
    private static void AssertAKeyKeepsFocus(UIA3Automation automation, AutomationElement element, Action press, string message)
    {
        // What the read that saw focus gone found: another element, or one
        // of the same name with another runtime id (the element was
        // published again). The message carries it.
        string elsewhere = string.Empty;
        string? why = FocusSettleWatch.WatchAcrossAKey(
            () => FocusIsOn(automation, element, found => elsewhere = found), press, FocusSettle);
        Assert.True(why is null, $"{message} {why}{elsewhere} Focused: {DescribeFocus(automation)}. {FocusDiagnosis()}");
    }

    /// <summary>A key that must move nothing, for any state a journey can
    /// read as in place, gone or unreadable: read once before the key,
    /// watched from the press on for <paramref name="window"/>. No wait for
    /// a read in place stands between the key and the watch, so a key that
    /// moved the state away and let it come back fails.</summary>
    private static void AssertAKeyMovesNothing(
        UIA3Automation automation, Func<bool?> inPlace, Action press, string message, TimeSpan window)
    {
        string? why = FocusSettleWatch.WatchAcrossAKey(inPlace, press, window);
        Assert.True(why is null, $"{message} {why} Focused: {DescribeFocus(automation)}. {FocusDiagnosis()}");
    }

    /// <summary>Whether focus is on <paramref name="element"/>: true, false,
    /// or null when UI Automation faulted or answered no element, which is
    /// no observation either way.</summary>
    private static bool? FocusIsOn(UIA3Automation automation, AutomationElement element, Action<string>? elsewhere = null)
    {
        try
        {
            if (automation.FocusedElement() is not { } focused)
            {
                return null;
            }

            if (focused.Equals(element))
            {
                return true;
            }

            int[] found = focused.Properties.RuntimeId.ValueOrDefault ?? [];
            elsewhere?.Invoke(
                $" The read that saw it gone found {focused.Properties.ControlType.ValueOrDefault} "
                + $"'{focused.Properties.Name.ValueOrDefault}' #{focused.Properties.AutomationId.ValueOrDefault} "
                + $"class {focused.Properties.ClassName.ValueOrDefault}, runtime id [{string.Join(",", found)}], where runtime id "
                + $"[{string.Join(",", element.Properties.RuntimeId.ValueOrDefault ?? [])}] was expected."
                + WindowFacts(found, element));
            return false;
        }
        catch (Exception exception) when (IsTransientUiaFault(exception))
        {
            return null;
        }
    }

    /// <summary>For a focus answer that is a bare window (runtime id
    /// <c>[42, hwnd]</c>): whose window it is and how it stands to the
    /// app's own, read at once. Never throws.</summary>
    private static string WindowFacts(int[] runtimeId, AutomationElement expected)
    {
        try
        {
            if (runtimeId is not [42, int handle])
            {
                return " The answer is not a bare window.";
            }

            var window = new IntPtr(handle);
            int app = expected.Properties.ProcessId.ValueOrDefault;
            IntPtr main = IntPtr.Zero;
            try
            {
                using var process = Process.GetProcessById(app);
                main = process.MainWindowHandle;
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
            {
            }

            IntPtr nearest = IntPtr.Zero;
            for (AutomationElement? at = expected; at is not null && nearest == IntPtr.Zero; at = at.Parent)
            {
                nearest = at.Properties.NativeWindowHandle.ValueOrDefault;
            }

            uint thread = NativeWindowFacts.GetWindowThreadProcessId(window, out uint owner);
            IntPtr root = NativeWindowFacts.GetAncestor(window, 2);
            _ = NativeWindowFacts.GetWindowThreadProcessId(root, out uint rootOwner);
            IntPtr foreground = NativeWindowFacts.GetForegroundWindow();
            _ = NativeWindowFacts.GetWindowThreadProcessId(foreground, out uint foregroundOwner);
            uint uiThread = NativeWindowFacts.GetWindowThreadProcessId(main, out _);
            var gui = new NativeWindowFacts.GuiThreadInfo { Size = System.Runtime.InteropServices.Marshal.SizeOf<NativeWindowFacts.GuiThreadInfo>() };
            bool haveGui = uiThread != 0 && NativeWindowFacts.GetGUIThreadInfo(uiThread, ref gui);
            return $" WINDOW RECORD: hwnd 0x{handle:X} alive={NativeWindowFacts.IsWindow(window)} class '{ClassOf(window)}' "
                + $"title '{TitleOf(window)}' process {owner} ({NameOf(owner)}) thread {thread}; the app is process {app}, "
                + $"same process={owner == (uint)app}; the app's main window 0x{main.ToInt64():X} (same={window == main}); the nearest "
                + $"window above the expected element 0x{nearest.ToInt64():X} (same={window == nearest}); the answer's root window "
                + $"0x{root.ToInt64():X} class '{ClassOf(root)}' process {rootOwner} (root is the main window={root == main}); "
                + $"foreground 0x{foreground.ToInt64():X} class '{ClassOf(foreground)}' process {foregroundOwner} ({NameOf(foregroundOwner)}); "
                + (haveGui
                    ? $"the app's UI thread {uiThread}: active 0x{gui.Active.ToInt64():X}, focus 0x{gui.Focus.ToInt64():X} "
                        + $"class '{ClassOf(gui.Focus)}', capture 0x{gui.Capture.ToInt64():X}, flags 0x{gui.Flags:X}."
                    : $"the app's UI thread {uiThread}: no GUI thread info.");
        }
        catch (Exception exception)
        {
            return $" WINDOW RECORD unreadable: {exception.GetType().Name}: {exception.Message}";
        }

        static string ClassOf(IntPtr window)
        {
            var name = new System.Text.StringBuilder(256);
            return NativeWindowFacts.GetClassName(window, name, name.Capacity) > 0 ? name.ToString() : string.Empty;
        }

        static string TitleOf(IntPtr window)
        {
            var title = new System.Text.StringBuilder(256);
            return NativeWindowFacts.GetWindowText(window, title, title.Capacity) > 0 ? title.ToString() : string.Empty;
        }

        static string NameOf(uint process)
        {
            try
            {
                using var found = Process.GetProcessById((int)process);
                return found.ProcessName;
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
            {
                return "gone";
            }
        }
    }

    private static class NativeWindowFacts
    {
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        internal struct GuiThreadInfo
        {
            public int Size;
            public uint Flags;
            public IntPtr Active;
            public IntPtr Focus;
            public IntPtr Capture;
            public IntPtr MenuOwner;
            public IntPtr MoveSize;
            public IntPtr Caret;
            public int CaretLeft;
            public int CaretTop;
            public int CaretRight;
            public int CaretBottom;
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        internal static extern IntPtr GetForegroundWindow();

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        internal static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        internal static extern IntPtr GetAncestor(IntPtr window, uint flags);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        internal static extern bool IsWindow(IntPtr window);

        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        internal static extern int GetClassName(IntPtr window, System.Text.StringBuilder name, int count);

        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        internal static extern int GetWindowText(IntPtr window, System.Text.StringBuilder text, int count);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        internal static extern bool GetGUIThreadInfo(uint thread, ref GuiThreadInfo info);
    }

    /// <summary>Whether focus is on <paramref name="container"/> or inside
    /// it: true, false, or null when UI Automation faulted or answered no
    /// element.</summary>
    private static bool? FocusIsWithin(UIA3Automation automation, AutomationElement container)
    {
        try
        {
            return automation.FocusedElement() is { } focused
                ? focused.Equals(container) || IsDescendantOf(focused, container)
                : null;
        }
        catch (Exception exception) when (IsTransientUiaFault(exception))
        {
            return null;
        }
    }

    /// <summary>Focuses the Files tree row starting with <paramref
    /// name="prefix"/> — not the TreeView container, whose SetFocus()
    /// throws InvalidOperationException once a TreeViewItem is selected
    /// (a WPF/UIA quirk, not this journey's or the host's defect) — and
    /// confirms the focus landed somewhere inside the tree. Re-finds the
    /// row each attempt: opening a note republishes the tree's row peers
    /// for a moment, so an element fetched an instant earlier can already
    /// be disconnected by the time Focus() runs.</summary>
    private static void FocusFilesTreeSelection(Window window, UIA3Automation automation, string prefix, string message)
    {
        Assert.True(
            SpinWait.SpinUntil(
                () =>
                {
                    try
                    {
                        AutomationElement tree = WaitForElement(window, "FilesTree", TimeSpan.FromSeconds(10));
                        AutomationElement? row = tree
                            .FindAllDescendants(automation.ConditionFactory.ByControlType(ControlType.TreeItem))
                            .FirstOrDefault(item => (item.Properties.Name.ValueOrDefault ?? string.Empty)
                                .StartsWith(prefix, StringComparison.Ordinal));
                        if (row is null) { return false; }
                        row.Focus();
                        return automation.FocusedElement() is { } focused && IsDescendantOf(focused, tree);
                    }
                    catch (InvalidOperationException) { return false; }
                    catch (Exception exception) when (IsTransientUiaFault(exception)) { return false; }
                },
                TimeSpan.FromSeconds(10)),
            $"{message} {FocusDiagnosis()}");
    }

    private static void AssertFilesTreeRegionFocused(Window window, UIA3Automation automation, AutomationElement tree, string message)
    {
        Assert.True(
            SpinWait.SpinUntil(() => automation.FocusedElement() is { } focused && IsDescendantOf(focused, tree), TimeSpan.FromSeconds(10)),
            $"{message} {FocusDiagnosis()}");
    }

    /// <summary>The right-pane content stop for the default Outline leaf
    /// with no note open is the rail itself when the leaf has no
    /// focusable stop; either is a right-pane landing.</summary>
    private static void AssertRightPaneStop(Window window, UIA3Automation automation, string message)
    {
        AutomationElement pane = WaitForElement(window, "InspectorPane", TimeSpan.FromSeconds(10));
        Assert.True(
            SpinWait.SpinUntil(() => automation.FocusedElement() is { } focused && IsDescendantOf(focused, pane), TimeSpan.FromSeconds(10)),
            $"{message} {FocusDiagnosis()}");
    }

    private static void AdvancePastTheRailIfItTookFocus(Window window, UIA3Automation automation)
    {
        // With the Outline leaf empty, the content stop is skipped and this
        // press already reached the rail; one more press reaches the status
        // bar. When a stop exists, this press lands on the rail. The rail
        // is a ListBox, and WPF/UIA report keyboard focus on the selected
        // ListBoxItem rather than the ListBox itself, so this checks
        // whether the focused element is the rail or a descendant of it
        // rather than the rail's own HasKeyboardFocus property.
        AutomationElement rail = WaitForElement(window, "RightPaneLeaves", TimeSpan.FromSeconds(10));
        if (automation.FocusedElement() is { } focused && IsDescendantOf(focused, rail))
        {
            PressKey(VirtualKeyShort.F6);
        }
    }

    /// <summary>The backward twin of <see
    /// cref="AdvancePastTheRailIfItTookFocus"/>: Shift+F6 out of the right
    /// pane, which holds one stop (the rail alone) or two (a leaf body's
    /// first stop and the rail) depending on the shown leaf.</summary>
    private static void RetreatPastTheRightPane(Window window, UIA3Automation automation)
    {
        AutomationElement pane = WaitForElement(window, "InspectorPane", TimeSpan.FromSeconds(10));
        for (int step = 0; step < 2; step++)
        {
            if (automation.FocusedElement() is not { } focused || !IsDescendantOf(focused, pane))
            {
                return;
            }

            PressChord(VirtualKeyShort.SHIFT, VirtualKeyShort.F6);
        }
    }
}
