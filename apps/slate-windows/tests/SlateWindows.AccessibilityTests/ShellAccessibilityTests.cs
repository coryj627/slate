// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using Axe.Windows.Automation;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.Tools;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;

namespace SlateWindows.AccessibilityTests;

[Trait("gate", "W-C")]
public sealed class ShellAccessibilityTests
{
    private const int AutomationTimeoutHResult = unchecked((int)0x80131505);

    [Fact]
    public void MainWindowDiscovery_RetriesTransientComTimeouts()
    {
        int attempts = 0;
        COMException? lastAutomationError = null;
        string? result = null;

        bool found = SpinWait.SpinUntil(
            () =>
            {
                result = TryAutomationQuery(
                    () =>
                    {
                        if (Interlocked.Increment(ref attempts) == 1)
                        {
                            throw new COMException(
                                "Operation timed out.",
                                AutomationTimeoutHResult);
                        }

                        return "main-window";
                    },
                    ref lastAutomationError);
                return result is not null;
            },
            TimeSpan.FromSeconds(1));

        Assert.True(found);
        Assert.Equal("main-window", result);
        Assert.Equal(2, attempts);
        Assert.NotNull(lastAutomationError);
        Assert.Equal(AutomationTimeoutHResult, lastAutomationError.HResult);

        COMException nonTimeout = Assert.Throws<COMException>(
            () => TryAutomationQuery<string>(
                () => throw new COMException(
                    "Unexpected UIA failure.",
                    unchecked((int)0x80004005)),
                ref lastAutomationError));
        Assert.Equal(unchecked((int)0x80004005), nonTimeout.HResult);
    }

    [Fact]
    public void FluentShell_UiaPatternsKeyboardFocusAndAxe_AreClean()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                RunShellAccessibilityGate();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        Assert.True(thread.Join(TimeSpan.FromSeconds(120)), "Shell accessibility gate timed out.");
        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    private static void RunShellAccessibilityGate()
    {
        string testRoot = Path.Combine(
            Path.GetTempPath(),
            $"slate-shell-accessibility-{Guid.NewGuid():N}");
        string vaultRoot = Path.Combine(testRoot, "Accessible Vault");
        string logDirectory = Path.Combine(testRoot, "logs");
        Directory.CreateDirectory(vaultRoot);
        File.WriteAllText(
            Path.Combine(vaultRoot, "note.md"),
            "---\ntags:\n  - accessibility\n---\n\n![[Folder/child]]\n\n[@doe]\n\n# Accessible note\n");
        Directory.CreateDirectory(Path.Combine(vaultRoot, "Folder"));
        File.WriteAllText(Path.Combine(vaultRoot, "Folder", "child.md"), "# Child note\n");

        Process? process = null;
        try
        {
            var startInfo = new ProcessStartInfo(SlateWindowsExe())
            {
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
            };
            startInfo.ArgumentList.Add(vaultRoot);
            startInfo.Environment["SLATE_CENSUS_INSTANCE_ID"] =
                $"slate-accessibility-{Guid.NewGuid():N}";
            startInfo.Environment["SLATE_LOG_DIR"] = logDirectory;
            startInfo.Environment["SLATE_UIA_DIAGNOSTICS"] = "1";
            process = Process.Start(startInfo)
                ?? throw new Xunit.Sdk.XunitException("SlateWindows.exe did not start.");

            if (!Environment.UserInteractive)
            {
                if (string.Equals(
                    Environment.GetEnvironmentVariable("SLATE_REQUIRE_UI_AUTOMATION"),
                    "1",
                    StringComparison.Ordinal))
                {
                    throw new Xunit.Sdk.XunitException(
                        "The W1-1 accessibility gate requires an interactive Windows desktop, " +
                        "but this runner is executing in a non-interactive session.");
                }

                // Session-0 developer sandboxes cannot expose a desktop UIA
                // tree. Still keep the production startup half of this test:
                // the process must survive XAML load and initial vault scan.
                Assert.False(
                    process.WaitForExit(3_000),
                    $"Slate exited during the non-interactive startup smoke. " +
                    $"app log: {ReadSharedLog(Path.Combine(logDirectory, "slate-windows.log"))}");
                return;
            }

            using var automation = new UIA3Automation();
            Window window = WaitForMainWindow(
                process,
                automation,
                Path.Combine(logDirectory, "slate-windows.log"),
                TimeSpan.FromSeconds(30));

            // #1107: this journey drives real keystrokes and then asserts
            // keyboard focus, and HasKeyboardFocus is false for every
            // element in a process that does not own the foreground. The
            // palette journey added this when it was written; this one —
            // older, and the place the symptom was actually reported —
            // never had it, so its focus assertions read like product
            // defects whenever anything else took the foreground.
            window.SetForeground();

            AutomationElement workspace = WaitForElement(
                window,
                "WorkspaceView",
                TimeSpan.FromSeconds(30));
            Assert.Equal("Slate", window.Title);
            Assert.NotNull(workspace.FindFirstDescendant(
                automation.ConditionFactory.ByAutomationId("FilesPane")));
            Assert.NotNull(workspace.FindFirstDescendant(
                automation.ConditionFactory.ByAutomationId("ContentPane")));
            Assert.NotNull(workspace.FindFirstDescendant(
                automation.ConditionFactory.ByAutomationId("InspectorPane")));

            AutomationElement scanProgress = WaitForElement(
                window,
                "VaultScanProgress",
                TimeSpan.FromSeconds(10));
            Assert.Equal(ControlType.ProgressBar, scanProgress.ControlType);
            Assert.True(scanProgress.Patterns.RangeValue.IsSupported);
            var scanRange = scanProgress.Patterns.RangeValue.Pattern;
            Assert.True(scanRange.IsReadOnly.Value);
            Assert.Equal(0, scanRange.Minimum.Value);
            Assert.True(
                SpinWait.SpinUntil(
                    () => scanRange.Maximum.Value == 2
                        && scanRange.Value.Value == 2,
                    TimeSpan.FromSeconds(10)),
                $"The live scan RangeValue did not settle at 2/2: " +
                $"{scanRange.Value.Value}/{scanRange.Maximum.Value}.");
            Assert.Equal(2, scanRange.Maximum.Value);
            Assert.Equal(2, scanRange.Value.Value);

            AutomationElement sidebarRefresh = WaitForElement(
                window,
                "SidebarRefresh",
                TimeSpan.FromSeconds(10));
            Assert.Equal(ControlType.Button, sidebarRefresh.ControlType);
            Assert.True(sidebarRefresh.Patterns.Invoke.IsSupported);

            AutomationElement sortOrder = WaitForElement(
                window,
                "SidebarSortOrder",
                TimeSpan.FromSeconds(10));
            Assert.Equal(ControlType.ComboBox, sortOrder.ControlType);
            Assert.True(sortOrder.Patterns.Selection.IsSupported);

            AutomationElement groupDates = WaitForElement(
                window,
                "SidebarGroupDates",
                TimeSpan.FromSeconds(10));
            Assert.Equal(ControlType.CheckBox, groupDates.ControlType);
            Assert.True(groupDates.Patterns.Toggle.IsSupported);

            foreach (string automationId in new[]
            {
                "SidebarShowTags",
                "SidebarDualPaneToggle",
            })
            {
                AutomationElement toggle = WaitForElement(
                    window,
                    automationId,
                    TimeSpan.FromSeconds(10));
                Assert.Equal(ControlType.Button, toggle.ControlType);
                Assert.True(
                    toggle.Patterns.Toggle.IsSupported,
                    $"{automationId} does not expose Toggle.");
            }

            AutomationElement tabs = WaitForElement(
                window,
                "WorkspaceTabs",
                TimeSpan.FromSeconds(10));
            AssertEventuallyFocused(
                tabs,
                "Opening a vault did not focus its active TabControl.");

            AssertActionButtonCensus(
                WaitForElement(window, "SidebarBatchActions", TimeSpan.FromSeconds(10)),
                automation,
                "Add",
                "Move checked to Recycle Bin",
                "Move to…",
                "Remove");
            AssertActionButtonCensus(
                WaitForElement(window, "SidebarFileActions", TimeSpan.FromSeconds(10)),
                automation,
                "Add shortcut",
                "Cancel import",
                "Copy link",
                "Copy path",
                "Delete",
                "Delete folder note",
                "Duplicate",
                "Folder note",
                "Import…",
                "Move to…",
                "New folder",
                "New note",
                "New tab",
                "Open",
                "Pin",
                "Rename",
                "Reveal in File Explorer",
                "Split",
                "Unpin");
            AssertActionButtonCensus(
                WaitForElement(window, "SidebarShortcutsActions", TimeSpan.FromSeconds(10)),
                automation,
                "Remove shortcut");

            AutomationElement filesTree = WaitForElement(
                window,
                "FilesTree",
                TimeSpan.FromSeconds(10));
            Assert.Equal(ControlType.Tree, filesTree.ControlType);
            AutomationElement[] treeItems = filesTree.FindAllDescendants(
                automation.ConditionFactory.ByControlType(ControlType.TreeItem));
            AutomationElement noteItem = treeItems.FirstOrDefault(item =>
                item.Name.StartsWith("note", StringComparison.OrdinalIgnoreCase))
                ?? throw new Xunit.Sdk.XunitException("The note TreeItem is absent.");
            Assert.True(noteItem.Patterns.SelectionItem.IsSupported);
            noteItem.Patterns.SelectionItem.Pattern.Select();
            AutomationElement editor = WaitForEditor(
                window,
                automation,
                "note.md editor",
                TimeSpan.FromSeconds(10));
            Assert.Equal(ControlType.Document, editor.ControlType);
            Assert.True(editor.Patterns.Value.IsSupported);
            Assert.True(editor.Properties.IsKeyboardFocusable.Value);
            editor.Focus();
            AssertEventuallyFocused(editor, "The opened note editor could not receive focus.");
            PlaceCaretAtText(editor, "![[Folder/child]]");
            AutomationElement previewMenu = WaitForMenuItem(
                window,
                "EditorMenu",
                "EditorPreviewEmbedMenuItem",
                TimeSpan.FromSeconds(10));
            Assert.True(previewMenu.IsEnabled, "The Preview Embed menu item is disabled.");
            Assert.True(
                previewMenu.Patterns.Invoke.IsSupported,
                "The Preview Embed menu item does not expose Invoke.");
            previewMenu.Patterns.Invoke.Pattern.Invoke();
            AutomationElement? menuInteractionPopover = TryWaitForElement(
                window,
                "EditorInteractionPopover",
                TimeSpan.FromSeconds(10));
            if (menuInteractionPopover is null)
            {
                throw new Xunit.Sdk.XunitException(
                    "The bound Preview Embed menu command did not open the editor " +
                    "interaction popover. " +
                    $"app log: {ReadSharedLog(Path.Combine(logDirectory, "slate-windows.log"))}");
            }
            AutomationElement menuPopoverClose = WaitForElement(
                window,
                "EditorPopoverClose",
                TimeSpan.FromSeconds(10));
            menuPopoverClose.Patterns.Invoke.Pattern.Invoke();
            Assert.True(
                SpinWait.SpinUntil(
                    () => window.FindFirstDescendant(
                        automation.ConditionFactory.ByAutomationId(
                            "EditorInteractionPopover")) is null,
                    TimeSpan.FromSeconds(10)),
                "The menu-opened editor interaction popover did not close.");

            editor.Focus();
            AssertEventuallyFocused(editor, "The editor did not regain focus after menu preview.");
            PlaceCaretAtText(editor, "![[Folder/child]]");
            string editorTextBeforePreview = editor.Patterns.Value.Pattern.Value;
            PressChord(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_E);
            AutomationElement? interactionPopover = TryWaitForElement(
                window,
                "EditorInteractionPopover",
                TimeSpan.FromSeconds(10));
            string editorTextAfterPreview = editor.Patterns.Value.Pattern.Value;
            if (!string.Equals(
                    editorTextBeforePreview,
                    editorTextAfterPreview,
                    StringComparison.Ordinal))
            {
                throw new Xunit.Sdk.XunitException(
                    "Ctrl+E changed the editor text instead of invoking Preview Embed. " +
                    $"app log: {ReadSharedLog(Path.Combine(logDirectory, "slate-windows.log"))}");
            }
            if (interactionPopover is null)
            {
                throw new Xunit.Sdk.XunitException(
                    "Ctrl+E did not open the editor interaction popover after the clean " +
                    "menu command path succeeded. " +
                    $"app log: {ReadSharedLog(Path.Combine(logDirectory, "slate-windows.log"))}");
            }
            Assert.True(
                SpinWait.SpinUntil(
                    () => interactionPopover.Name.StartsWith(
                        "Embed preview for",
                        StringComparison.Ordinal),
                    TimeSpan.FromSeconds(10)),
                $"Embed preview did not finish loading: {interactionPopover.Name}");
            Assert.True(interactionPopover.Properties.IsDialog.Value);
            AutomationElement popoverClose = WaitForElement(
                window,
                "EditorPopoverClose",
                TimeSpan.FromSeconds(10));
            AutomationElement popoverOpenSource = WaitForElement(
                window,
                "EditorPopoverOpenSource",
                TimeSpan.FromSeconds(10));
            Assert.Equal(ControlType.Button, popoverClose.ControlType);
            Assert.True(popoverClose.Patterns.Invoke.IsSupported);
            AssertEventuallyFocused(
                popoverClose,
                "The embed popover did not focus its Close button.");
            PressKey(VirtualKeyShort.TAB);
            AssertEventuallyFocused(
                popoverOpenSource,
                "Tab from Close did not wrap to Open source.");
            PressChord(VirtualKeyShort.SHIFT, VirtualKeyShort.TAB);
            AssertEventuallyFocused(
                popoverClose,
                "Shift+Tab from Open source did not wrap to Close.");
            AssertAxeClean(process, "editor-embed-popover");
            popoverClose.Patterns.Invoke.Pattern.Invoke();
            Assert.True(
                SpinWait.SpinUntil(
                    () => window.FindFirstDescendant(
                        automation.ConditionFactory.ByAutomationId(
                            "EditorInteractionPopover")) is null,
                    TimeSpan.FromSeconds(10)),
                "The editor interaction popover did not close.");
            AssertEventuallyFocused(
                editor,
                "Closing the embed popover did not return focus to the editor.");
            editor.Focus();
            PlaceCaretAtText(editor, "[@doe]");
            string editorTextBeforeCitation = editor.Patterns.Value.Pattern.Value;
            PressChord(VirtualKeyShort.CONTROL, VirtualKeyShort.ENTER);
            AutomationElement citationPopover = WaitForElement(
                window,
                "EditorInteractionPopover",
                TimeSpan.FromSeconds(10));
            Assert.Equal(
                editorTextBeforeCitation,
                editor.Patterns.Value.Pattern.Value);
            Assert.StartsWith(
                "Citation",
                citationPopover.Name,
                StringComparison.Ordinal);
            AutomationElement citationClose = WaitForElement(
                window,
                "EditorPopoverClose",
                TimeSpan.FromSeconds(10));
            AssertEventuallyFocused(
                citationClose,
                "The citation popover did not focus its Close button.");
            AssertAxeClean(process, "editor-citation-popover");
            Keyboard.Press(VirtualKeyShort.ESCAPE);
            Assert.True(
                SpinWait.SpinUntil(
                    () => window.FindFirstDescendant(
                        automation.ConditionFactory.ByAutomationId(
                            "EditorInteractionPopover")) is null,
                    TimeSpan.FromSeconds(10)),
                "Escape did not close the editor interaction popover.");
            AssertEventuallyFocused(
                editor,
                "Escape did not return focus from the popover to the editor.");
            Keyboard.Press(VirtualKeyShort.F2);
            AssertEventuallyFocused(
                editor,
                "F2 escaped the editor even though the Files tree did not own focus.");
            noteItem.Focus();
            AssertEventuallyFocused(noteItem, "The note TreeItem could not receive focus.");
            Keyboard.Press(VirtualKeyShort.F2);
            AutomationElement renameInput = WaitForElement(
                window,
                "SidebarMutationName",
                TimeSpan.FromSeconds(10));
            AssertEventuallyFocused(
                renameInput,
                "F2 from the Files tree did not focus the rename field.");
            Assert.Equal("note.md", renameInput.Patterns.Value.Pattern.Value);
            Keyboard.Press(VirtualKeyShort.ESCAPE);
            AutomationElement folderItem = treeItems.FirstOrDefault(item =>
                item.Name.StartsWith("Folder", StringComparison.OrdinalIgnoreCase))
                ?? throw new Xunit.Sdk.XunitException("The folder TreeItem is absent.");
            Assert.True(folderItem.Patterns.ExpandCollapse.IsSupported);
            var folderExpansion = folderItem.Patterns.ExpandCollapse.Pattern;
            folderItem.Focus();
            folderExpansion.Expand();
            _ = WaitForNamedElement(
                window,
                automation,
                "child.md, file",
                TimeSpan.FromSeconds(10));
            Assert.True(
                SpinWait.SpinUntil(
                    () => folderExpansion.ExpandCollapseState.Value == ExpandCollapseState.Expanded,
                    TimeSpan.FromSeconds(5)),
                "The folder did not expose the expanded UIA state.");
            AssertEventuallyFocused(
                folderItem,
                "Expanding the folder through UIA moved focus away from its TreeItem.");
            folderExpansion.Collapse();
            Assert.True(
                SpinWait.SpinUntil(
                    () => folderExpansion.ExpandCollapseState.Value == ExpandCollapseState.Collapsed,
                    TimeSpan.FromSeconds(5)),
                "The folder did not expose the collapsed UIA state.");

            folderItem.Focus();
            Keyboard.Press(VirtualKeyShort.RIGHT);
            _ = WaitForNamedElement(
                window,
                automation,
                "child.md, file",
                TimeSpan.FromSeconds(10));
            Assert.True(
                SpinWait.SpinUntil(
                    () => folderExpansion.ExpandCollapseState.Value == ExpandCollapseState.Expanded,
                    TimeSpan.FromSeconds(5)),
                "Right Arrow did not expose the expanded UIA state.");
            AssertEventuallyFocused(
                folderItem,
                "Right Arrow expansion moved focus away from its TreeItem.");
            Keyboard.Press(VirtualKeyShort.LEFT);
            Assert.True(
                SpinWait.SpinUntil(
                    () => folderExpansion.ExpandCollapseState.Value == ExpandCollapseState.Collapsed,
                    TimeSpan.FromSeconds(5)),
                "Left Arrow did not expose the collapsed UIA state.");

            AutomationElement sidebarFilter = WaitForElement(
                window,
                "SidebarFilter",
                TimeSpan.FromSeconds(10));
            Assert.Equal(ControlType.Edit, sidebarFilter.ControlType);
            Assert.True(sidebarFilter.Patterns.Value.IsSupported);
            sidebarFilter.Patterns.Value.Pattern.SetValue("note");
            AutomationElement filterResults = WaitForElement(
                window,
                "SidebarFilterResults",
                TimeSpan.FromSeconds(10));
            Assert.Equal(ControlType.List, filterResults.ControlType);
            Assert.True(filterResults.Patterns.Selection.IsSupported);
            Assert.True(
                SpinWait.SpinUntil(
                    () => filterResults.FindAllDescendants(
                        automation.ConditionFactory.ByControlType(ControlType.ListItem)).Length > 0,
                    TimeSpan.FromSeconds(10)),
                "The asynchronous sidebar filter did not publish a result.");
            sidebarFilter.Patterns.Value.Pattern.SetValue(string.Empty);

            AutomationElement tagToggle = WaitForNamedElement(
                window,
                automation,
                "Show tag tree",
                TimeSpan.FromSeconds(10));
            Assert.True(tagToggle.Patterns.Toggle.IsSupported);
            tagToggle.Patterns.Toggle.Pattern.Toggle();
            AutomationElement tagTree = WaitForElement(
                window,
                "SidebarTagTree",
                TimeSpan.FromSeconds(10));
            Assert.Equal(ControlType.Tree, tagTree.ControlType);
            AutomationElement tagItem = WaitForNamedElement(
                tagTree,
                automation,
                "accessibility, 1 file",
                TimeSpan.FromSeconds(10));
            Assert.Equal(ControlType.TreeItem, tagItem.ControlType);
            Assert.True(tagItem.Patterns.SelectionItem.IsSupported);

            tabs = WaitForElement(
                window,
                "WorkspaceTabs",
                TimeSpan.FromSeconds(10));
            Assert.Equal(ControlType.Tab, tabs.ControlType);
            Assert.True(tabs.Patterns.Selection.IsSupported);
            AutomationElement tab = tabs.FindFirstDescendant(
                automation.ConditionFactory.ByControlType(ControlType.TabItem))
                ?? throw new Xunit.Sdk.XunitException("Workspace TabItem is absent.");
            Assert.True(tab.Patterns.SelectionItem.IsSupported);
            Assert.Contains("note", tab.Name, StringComparison.OrdinalIgnoreCase);
            editor.Patterns.Value.Pattern.SetValue("# Changed through UIA\n");
            Assert.True(
                SpinWait.SpinUntil(
                    () => tab.Name.EndsWith(", unsaved changes", StringComparison.Ordinal),
                    TimeSpan.FromSeconds(10)),
                "The dirty tab did not expose its unsaved state in its accessible name.");
            editor.Focus();
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_S);
            Assert.True(
                SpinWait.SpinUntil(
                    () => !tab.Name.Contains("unsaved changes", StringComparison.Ordinal),
                    TimeSpan.FromSeconds(10)),
                "The saved tab retained a stale unsaved accessible name.");

            AutomationElement rightPaneLeaves = WaitForElement(
                window,
                "RightPaneLeaves",
                TimeSpan.FromSeconds(10));
            Assert.Equal(ControlType.List, rightPaneLeaves.ControlType);
            Assert.True(rightPaneLeaves.Patterns.Selection.IsSupported);
            Assert.Equal(
                16,
                rightPaneLeaves.FindAllDescendants(
                    automation.ConditionFactory.ByControlType(ControlType.ListItem)).Length);

            AutomationElement splitRight = WaitForMenuItem(
                window,
                "WorkspaceMenu",
                "SplitRightMenuItem",
                TimeSpan.FromSeconds(10));
            Assert.True(splitRight.IsEnabled);
            foreach (string editorCommand in new[]
            {
                "EditorActivateMenuItem",
                "EditorPreviewEmbedMenuItem",
                "EditorToggleSpellCheckMenuItem",
                "EditorZoomInMenuItem",
                "EditorZoomOutMenuItem",
                "EditorActualSizeMenuItem",
            })
            {
                AutomationElement item = WaitForMenuItem(
                    window,
                    "EditorMenu",
                    editorCommand,
                    TimeSpan.FromSeconds(10));
                Assert.Equal(ControlType.MenuItem, item.ControlType);
                Assert.True(item.Patterns.Invoke.IsSupported);
            }
            Assert.True(splitRight.Patterns.Invoke.IsSupported);
            splitRight.Patterns.Invoke.Pattern.Invoke();
            Assert.True(SpinWait.SpinUntil(
                () => window.FindAllDescendants(
                    automation.ConditionFactory.ByAutomationId("WorkspaceTabs")).Length == 2,
                TimeSpan.FromSeconds(10)),
                "Split Right did not expose two navigable TabControls.");
            AutomationElement[] splitTabs = window.FindAllDescendants(
                automation.ConditionFactory.ByAutomationId("WorkspaceTabs"));
            AutomationElement splitHandle = WaitForElement(
                window,
                "WorkspaceSplitHandle",
                TimeSpan.FromSeconds(10));
            Assert.Single(window.FindAllDescendants(
                automation.ConditionFactory.ByAutomationId("WorkspaceSplitHandle")));
            Assert.Contains("Resize editor panes", splitHandle.Name, StringComparison.Ordinal);
            Assert.True(splitHandle.Properties.IsKeyboardFocusable.Value);
            splitHandle.Focus();
            AssertEventuallyFocused(
                splitHandle,
                "The recursive split resize handle could not receive keyboard focus.");
            Keyboard.Press(VirtualKeyShort.RIGHT);
            AssertEventuallyFocused(
                splitHandle,
                "Arrow-key resizing unexpectedly moved focus off the split handle.");
            AutomationElement leftEditor = splitTabs[0].FindFirstDescendant(
                automation.ConditionFactory.ByControlType(ControlType.Document))
                ?? throw new Xunit.Sdk.XunitException("The left split editor is absent.");
            AutomationElement rightEditor = splitTabs[1].FindFirstDescendant(
                automation.ConditionFactory.ByControlType(ControlType.Document))
                ?? throw new Xunit.Sdk.XunitException("The right split editor is absent.");
            rightEditor.Focus();
            AssertEventuallyFocused(rightEditor, "The right split editor could not receive focus.");
            if (ChordIsDeliverable(
                NativeHotkey.ModControl | NativeHotkey.ModAlt, 0x25 /* VK_LEFT */))
            {
                Keyboard.TypeSimultaneously(
                    VirtualKeyShort.CONTROL,
                    VirtualKeyShort.ALT,
                    VirtualKeyShort.LEFT);
                AssertEventuallyFocused(
                    leftEditor,
                    "Ctrl+Alt+Left changed the model but did not move keyboard "
                    + "focus to the left editor.");
            }
            else
            {
                SkipStolenChordLeg("Ctrl+Alt+Left");
            }

            AssertAxeClean(process, "workspace");

            AutomationElement quickOpen = WaitForMenuItem(
                window,
                "FileMenu",
                "QuickOpenMenuItem",
                TimeSpan.FromSeconds(10));
            Assert.True(quickOpen.Patterns.Invoke.IsSupported);
            sidebarFilter.Focus();
            AssertEventuallyFocused(sidebarFilter, "The sidebar filter could not receive focus.");
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_O);
            AutomationElement quickSearch = WaitForElement(
                window,
                "QuickSwitcherSearch",
                TimeSpan.FromSeconds(10));
            Assert.Equal(ControlType.Edit, quickSearch.ControlType);
            Assert.True(quickSearch.Patterns.Value.IsSupported);
            AutomationElement quickResultsList = WaitForElement(
                window,
                "QuickSwitcherResults",
                TimeSpan.FromSeconds(10));
            Assert.Equal(ControlType.List, quickResultsList.ControlType);
            AutomationElement? quickResult = null;
            Assert.True(
                SpinWait.SpinUntil(
                    () =>
                    {
                        quickResult = quickResultsList.FindAllDescendants(
                            automation.ConditionFactory.ByControlType(ControlType.ListItem))
                            .FirstOrDefault();
                        return quickResult is not null;
                    },
                    TimeSpan.FromSeconds(10)),
                "Quick Open did not publish its asynchronously ranked results.");
            Assert.False(string.IsNullOrWhiteSpace(quickResult!.Name));
            Assert.False(string.IsNullOrWhiteSpace(quickResult.HelpText));
            Assert.False(string.IsNullOrWhiteSpace(quickResultsList.ItemStatus));
            AssertEventuallyFocused(
                quickSearch,
                "Quick Open did not move focus to its search field.");
            Assert.False(sidebarFilter.IsEnabled);
            Assert.False(tabs.IsEnabled);
            AssertAxeClean(process, "quick-open");

            AutomationElement closeQuick = WaitForElement(
                window,
                "QuickSwitcherClose",
                TimeSpan.FromSeconds(10));
            Assert.True(closeQuick.Patterns.Invoke.IsSupported);
            Keyboard.Press(VirtualKeyShort.TAB);
            AssertEventuallyFocused(
                closeQuick,
                "Tab did not remain inside Quick Open or reach its Close button.");
            Keyboard.Press(VirtualKeyShort.ESCAPE);
            AssertQuickSwitcherDisappears(window, automation);
            AssertEventuallyFocused(
                sidebarFilter,
                "Escape did not restore the element focused before Quick Open.");
            Assert.True(sidebarFilter.IsEnabled);

            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_O);
            quickSearch = WaitForElement(
                window,
                "QuickSwitcherSearch",
                TimeSpan.FromSeconds(10));
            AssertEventuallyFocused(
                quickSearch,
                "Quick Open did not refocus search on its second invocation.");
            Keyboard.Press(VirtualKeyShort.ENTER);
            AssertQuickSwitcherDisappears(window, automation);
            editor = WaitForEditor(
                window,
                automation,
                "note.md editor",
                TimeSpan.FromSeconds(10));
            AssertEventuallyFocused(
                editor,
                "Committing Quick Open did not focus the destination editor.");

            AutomationElement splitDown = WaitForMenuItem(
                window,
                "WorkspaceMenu",
                "SplitDownMenuItem",
                TimeSpan.FromSeconds(10));
            Assert.True(splitDown.IsEnabled);
            Assert.True(splitDown.Patterns.Invoke.IsSupported);
            splitDown.Patterns.Invoke.Pattern.Invoke();
            Assert.True(SpinWait.SpinUntil(
                () => window.FindAllDescendants(
                    automation.ConditionFactory.ByAutomationId("WorkspaceTabs")).Length == 3,
                TimeSpan.FromSeconds(10)),
                "Split Down did not expose a third navigable TabControl.");
            AutomationElement verticalSplitHandle = WaitForElement(
                window,
                "WorkspaceSplitHandleVertical",
                TimeSpan.FromSeconds(10));
            Assert.Single(window.FindAllDescendants(
                automation.ConditionFactory.ByAutomationId("WorkspaceSplitHandleVertical")));
            Assert.Contains("vertically", verticalSplitHandle.Name, StringComparison.Ordinal);
            Assert.True(verticalSplitHandle.Properties.IsKeyboardFocusable.Value);
            // The workspace and Quick Open states are Axe-scanned above. The
            // split-specific contract is asserted directly here; a third full-tree
            // scan adds runtime and flake exposure without covering another state.

            AutomationElement closeVault = WaitForMenuItem(
                window,
                "FileMenu",
                "CloseVaultMenuItem",
                TimeSpan.FromSeconds(10));
            Assert.True(closeVault.IsEnabled);
            Assert.True(closeVault.Patterns.Invoke.IsSupported);
            closeVault.Patterns.Invoke.Pattern.Invoke();

            AutomationElement welcome = WaitForElement(
                window,
                "WelcomeView",
                TimeSpan.FromSeconds(10));
            AutomationElement openVault = welcome.FindFirstDescendant(
                automation.ConditionFactory.ByAutomationId("OpenVaultButton"))
                ?? throw new Xunit.Sdk.XunitException("Open Vault button is absent from welcome.");
            Assert.True(openVault.IsEnabled);
            Assert.True(openVault.Patterns.Invoke.IsSupported);
            Assert.Contains("Open Vault", openVault.Name, StringComparison.Ordinal);
            Assert.Contains(
                welcome.FindAllDescendants(
                    automation.ConditionFactory.ByControlType(ControlType.Button)),
                element => string.Equals(
                    element.Name,
                    "Accessible Vault",
                    StringComparison.Ordinal));

            AssertAxeClean(process, "welcome");
        }
        finally
        {
            if (process is not null && !process.HasExited)
            {
                process.CloseMainWindow();
                if (!process.WaitForExit(5_000))
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(5_000);
                }
            }

            process?.Dispose();
            try
            {
                Directory.Delete(testRoot, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    /// <summary>The focused element's identity for a failure message —
    /// which element took focus is the diagnosis, not merely that the
    /// expected one did not.</summary>
    private static string DescribeFocusedElement(UIA3Automation automation)
    {
        try
        {
            AutomationElement? focused = automation.FocusedElement();
            if (focused is null)
            {
                return "(nothing)";
            }

            return $"{focused.Properties.ControlType.ValueOrDefault} "
                + $"name='{focused.Properties.Name.ValueOrDefault}' "
                + $"id='{focused.Properties.AutomationId.ValueOrDefault}' "
                + $"class='{focused.Properties.ClassName.ValueOrDefault}'";
        }
        catch (Exception exception) when (IsTransientUiaFault(exception))
        {
            return $"(unreadable: {exception.GetType().Name})";
        }
    }

    internal static void AssertAxeClean(Process process, string surface)
    {
        var config = Config.Builder.ForProcessId(process.Id).Build();
        var output = ScannerFactory.CreateScanner(config).Scan(null);
        Assert.NotEmpty(output.WindowScanOutputs);
        var allErrors = output.WindowScanOutputs
            .SelectMany(result => result.Errors)
            .ToArray();
        var waived = allErrors.Where(IsFluentCollapsedScrollBarPart).ToArray();
        var errors = allErrors.Except(waived).ToArray();
        WriteAxeEvidence(
            surface,
            output.WindowScanOutputs.Count,
            errors.Select(DescribeAxeError).ToArray(),
            waived.Select(DescribeAxeError).ToArray());
        Assert.True(
            errors.Length == 0,
            string.Join(
                Environment.NewLine,
                errors.Select(error =>
                    $"{error.Rule.ID}: {error.Rule.Description}; " +
                    string.Join(", ", error.Element.Properties))));
    }

    private static object DescribeAxeError(Axe.Windows.Automation.ScanResult error) => new
    {
        ruleId = error.Rule.ID,
        description = error.Rule.Description,
        elementProperties = error.Element.Properties
            .Select(property => property.ToString())
            .ToArray(),
    };

    /// <summary>
    /// The ONE recorded axe waiver (#1115): the Fluent theme's scrollbar
    /// collapses to a ~4 px overlay until hovered, and its track/arrow
    /// RepeatButtons (PageUp/PageDown/LineUp/LineDown) then measure
    /// below the 25 px² floor of <c>BoundingRectangleSizeReasonable</c>
    /// whenever the scan catches the collapsed state — a geometric
    /// verdict on a mouse-only, non-focusable part that is not an AT
    /// stop, and one that depends on scan timing and display geometry
    /// (measured: the FluentShell journey failed 2/3 then 3/3 locally
    /// on the committed tree, green on CI by luck). Scoped to exactly
    /// that element shape: the rule, the RepeatButton class, the
    /// scrollbar part ids. Everything else the rule reports still
    /// fails. Waived findings are RECORDED in the evidence artifact, so
    /// the standing NVDA/JAWS field pass can revisit the call with what
    /// users actually experience; if that pass finds the collapsed bar
    /// matters, the fix is app-wide (keep Fluent's bars expanded), not
    /// a wider waiver.
    /// </summary>
    private static bool IsFluentCollapsedScrollBarPart(Axe.Windows.Automation.ScanResult error) =>
        IsFluentCollapsedScrollBarPart(
            error.Rule.ID.ToString(),
            ElementProperty(error, "ClassName"),
            ElementProperty(error, "AutomationId"));

    /// <summary>The waiver decision over the three properties that carry
    /// it — a pure function so both directions are testable without
    /// synthesizing a scan (<see cref="AxeWaiverTests"/>). Comparisons
    /// are ordinal-ignore-case: UIA property casing is the provider's,
    /// not ours, and the waiver must neither widen nor narrow on it.</summary>
    internal static bool IsFluentCollapsedScrollBarPart(
        string? ruleId, string? className, string? automationId)
    {
        if (!string.Equals(
                ruleId, "BoundingRectangleSizeReasonable", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.Equals(className, "RepeatButton", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return automationId is not null
            && (string.Equals(automationId, "PageUp", StringComparison.OrdinalIgnoreCase)
                || string.Equals(automationId, "PageDown", StringComparison.OrdinalIgnoreCase)
                || string.Equals(automationId, "LineUp", StringComparison.OrdinalIgnoreCase)
                || string.Equals(automationId, "LineDown", StringComparison.OrdinalIgnoreCase));
    }

    private static string? ElementProperty(Axe.Windows.Automation.ScanResult error, string name) =>
        error.Element.Properties.TryGetValue(name, out string? value) ? value : null;

    private static void WriteAxeEvidence(
        string surface,
        int windowCount,
        object[] errors,
        object[] waived)
    {
        string? directory = Environment.GetEnvironmentVariable(
            "SLATE_ACCESSIBILITY_EVIDENCE_DIR");
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        Directory.CreateDirectory(directory);
        var evidence = new
        {
            // Schema 2 (#1115): `waived` records the findings the one
            // scoped waiver set aside — the collapsed-state Fluent
            // scrollbar parts — so the field pass can review them.
            schemaVersion = 2,
            surface,
            recordedAtUtc = DateTimeOffset.UtcNow,
            sourceRevision = Environment.GetEnvironmentVariable("GITHUB_SHA"),
            operatingSystem = Environment.OSVersion.VersionString,
            dotnetRuntime = Environment.Version.ToString(),
            userInteractive = Environment.UserInteractive,
            scannedWindowCount = windowCount,
            outcome = errors.Length == 0 ? "pass" : "fail",
            errors,
            waived,
        };
        string path = Path.Combine(directory, $"axe-{surface}.json");
        File.WriteAllText(
            path,
            JsonSerializer.Serialize(
                evidence,
                new JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>
    /// W3-1 identity contract (`w3_spec.md` §W3-1): the W-E7 AT-idiom
    /// layers — an NVDA add-on appModule keyed on the executable name,
    /// JAWS scripts keyed on the window — depend on the exe name, the
    /// main-window AutomationId, and the reading-surface AutomationId.
    /// All three are externally-dependable API (recorded in
    /// `w_c_matrix.md`); renaming any of them breaks shipped AT
    /// configuration. (The WPF HWND class name is per-process
    /// randomized and is deliberately NOT part of the contract.)
    /// </summary>
    [Fact]
    public void ReadingIdentityContract_ExeWindowAndSurfaceIds_AreStable()
    {
        // The exe-name half of the contract holds in every session type.
        Assert.Equal("SlateWindows.exe", Path.GetFileName(SlateWindowsExe()));

        string testRoot = Path.Combine(
            Path.GetTempPath(),
            $"slate-identity-contract-{Guid.NewGuid():N}");
        string vaultRoot = Path.Combine(testRoot, "Identity Vault");
        string logDirectory = Path.Combine(testRoot, "logs");
        Directory.CreateDirectory(vaultRoot);
        File.WriteAllText(
            Path.Combine(vaultRoot, "note.md"),
            "# Identity note\n\nBody text.\n\n$$x^2 + 1$$\n\n"
                + "```mermaid\nflowchart LR\nA --> B\n```\n");

        Process? process = null;
        try
        {
            var startInfo = new ProcessStartInfo(SlateWindowsExe())
            {
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
            };
            startInfo.ArgumentList.Add(vaultRoot);
            startInfo.Environment["SLATE_CENSUS_INSTANCE_ID"] =
                $"slate-identity-{Guid.NewGuid():N}";
            startInfo.Environment["SLATE_LOG_DIR"] = logDirectory;
            process = Process.Start(startInfo)
                ?? throw new Xunit.Sdk.XunitException("SlateWindows.exe did not start.");

            if (!HasInteractiveDesktop(process, "identity-contract"))
            {
                return;
            }

            using var automation = new UIA3Automation();
            Window window = WaitForMainWindow(
                process,
                automation,
                Path.Combine(logDirectory, "slate-windows.log"),
                TimeSpan.FromSeconds(30));
            Assert.Equal("Slate.MainWindow", window.AutomationId);
            Assert.Equal(
                "SlateWindows",
                Process.GetProcessById(process.Id).ProcessName);

            // Open the note, toggle reading mode through the bound menu
            // command, and require the ReadingSurface AutomationId.
            AutomationElement filesTree = WaitForElement(
                window, "FilesTree", TimeSpan.FromSeconds(30));
            AutomationElement noteItem = filesTree
                .FindAllDescendants(
                    automation.ConditionFactory.ByControlType(ControlType.TreeItem))
                .FirstOrDefault(item =>
                    item.Name.StartsWith("note", StringComparison.OrdinalIgnoreCase))
                ?? throw new Xunit.Sdk.XunitException("The note TreeItem is absent.");
            noteItem.Patterns.SelectionItem.Pattern.Select();
            WaitForEditor(
                window, automation, "note.md editor", TimeSpan.FromSeconds(10));

            AutomationElement toggleReading = WaitForMenuItem(
                window,
                "EditorMenu",
                "EditorToggleReadingModeMenuItem",
                TimeSpan.FromSeconds(10));
            toggleReading.Patterns.Invoke.Pattern.Invoke();

            AutomationElement surface = WaitForElement(
                window, "ReadingSurface", TimeSpan.FromSeconds(10));
            Assert.Equal(ControlType.Document, surface.ControlType);
            Assert.True(
                surface.Patterns.Text.IsSupported,
                "ReadingSurface does not expose the Text pattern.");

            // W3-2 external verification (round-1 [high]): the MathML
            // convention property must be readable CROSS-PROCESS from
            // the production app with ZERO app cooperation — this test
            // registers the same GUID client-side exactly as NVDA does
            // (ids are process-local; the GUID is the wire key) and
            // reads the value off the math element's peer. The test
            // project deliberately never links the app assembly.
            int mathMlPropertyId = RegisterMathMlPropertyAsClient();
            AutomationElement? math = null;
            Assert.True(
                SpinWait.SpinUntil(
                    () =>
                    {
                        math = window.FindFirstDescendant(
                            cf => cf.ByLocalizedControlType("math"));
                        return math is not null;
                    },
                    TimeSpan.FromSeconds(10)),
                "no element with localized control type 'math' appeared");
            var native = (FlaUI.UIA3.UIA3FrameworkAutomationElement)
                math!.FrameworkAutomationElement;
            object? mathMl = native.NativeElement.GetCurrentPropertyValue(
                mathMlPropertyId);
            Assert.True(
                mathMl is string { Length: > 0 } markup
                    && markup.TrimStart().StartsWith("<math", StringComparison.Ordinal),
                $"MathML property value: {mathMl ?? "<null>"}");

            // W3-3 external verification: the diagram element's Name
            // — the canonical structured description, the ENTIRE
            // primary AT surface for diagrams — is readable
            // cross-process from the production app. Standard UIA
            // Name, no custom registration: what NVDA and JAWS speak
            // on focus is exactly what this asserts.
            AutomationElement? diagram = null;
            Assert.True(
                SpinWait.SpinUntil(
                    () =>
                    {
                        diagram = window.FindFirstDescendant(
                            cf => cf.ByLocalizedControlType("diagram"));
                        return diagram is not null;
                    },
                    TimeSpan.FromSeconds(10)),
                "no element with localized control type 'diagram' appeared");
            Assert.Equal("Flowchart with 1 step.", diagram!.Name);
        }
        finally
        {
            if (process is not null && !process.HasExited)
            {
                process.CloseMainWindow();
                if (!process.WaitForExit(5_000))
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(5_000);
                }
            }

            process?.Dispose();
            try
            {
                Directory.Delete(testRoot, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    /// <summary>
    /// W-E7 gate spike (task: RangeFromChild over custom peers),
    /// HARDENED per the #1072 adversarial round: the add-on positions
    /// browse mode via ITextProvider::RangeFromChild, NVDA treats a
    /// failing element as OUTSIDE the document
    /// (UIABrowseModeDocument.__contains__), and its renderer walks
    /// child ranges RECURSIVELY. WPF resolves in three tiers
    /// (TextAdaptor.RangeFromChild) and the custom elements ride the
    /// one-logical-hop UIContainer tier, so every interactive
    /// UIElement in the reading document must stay its container's
    /// DIRECT child.
    ///
    /// Proof shape (round-1 findings addressed):
    ///  - a POSITIVE CENSUS: every custom peer kind must be found and
    ///    must resolve - including the object-tree structural peers
    ///    (heading, code block, list, list item) that GetChildrenCore
    ///    serves outside the text-range child list;
    ///  - IDENTITY, not just sanity: embedded elements assert NVDA's
    ///    own round-trip (the child range's GetChildren yields the
    ///    probed element); TextElement peers assert their range TEXT
    ///    carries the element's own content;
    ///  - DOCUMENT ORDER: probe ranges are strictly ordered as
    ///    authored, so no two probes can share one bogus range;
    ///  - a RECURSIVE range walk (visited-guarded) over everything
    ///    the text pattern exposes, every level resolving.
    /// </summary>
    [Fact]
    public void ReadingTextPattern_RangeFromChildResolvesEveryCustomPeer()
    {
        string testRoot = Path.Combine(
            Path.GetTempPath(),
            $"slate-rangefromchild-{Guid.NewGuid():N}");
        string vaultRoot = Path.Combine(testRoot, "Range Vault");
        string logDirectory = Path.Combine(testRoot, "logs");
        Directory.CreateDirectory(vaultRoot);
        File.WriteAllText(
            Path.Combine(vaultRoot, "target.md"),
            "# Target\n\nEmbedded body.\n");
        File.WriteAllText(
            Path.Combine(vaultRoot, "note.md"),
            "# Range note\n\nBody with a [[target]] link.\n\n"
                + "- alpha bullet\n- beta bullet\n\n"
                + "- [ ] open task\n\n"
                + "$$x^2 + 1$$\n\n"
                + "```rust\nfn f() {}\n```\n\n"
                + "```mermaid\nflowchart LR\nA --> B\n```\n\n"
                + "![[target]]\n");

        Process? process = null;
        try
        {
            var startInfo = new ProcessStartInfo(SlateWindowsExe())
            {
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
            };
            startInfo.ArgumentList.Add(vaultRoot);
            startInfo.Environment["SLATE_CENSUS_INSTANCE_ID"] =
                $"slate-rangefromchild-{Guid.NewGuid():N}";
            startInfo.Environment["SLATE_LOG_DIR"] = logDirectory;
            process = Process.Start(startInfo)
                ?? throw new Xunit.Sdk.XunitException("SlateWindows.exe did not start.");

            if (!HasInteractiveDesktop(process, "RangeFromChild"))
            {
                return;
            }

            using var automation = new UIA3Automation();
            Window window = WaitForMainWindow(
                process,
                automation,
                Path.Combine(logDirectory, "slate-windows.log"),
                TimeSpan.FromSeconds(30));

            AutomationElement filesTree = WaitForElement(
                window, "FilesTree", TimeSpan.FromSeconds(30));
            AutomationElement noteItem = filesTree
                .FindAllDescendants(
                    automation.ConditionFactory.ByControlType(ControlType.TreeItem))
                .FirstOrDefault(item =>
                    item.Name.StartsWith("note", StringComparison.OrdinalIgnoreCase))
                ?? throw new Xunit.Sdk.XunitException("The note TreeItem is absent.");
            noteItem.Patterns.SelectionItem.Pattern.Select();
            WaitForEditor(
                window, automation, "note.md editor", TimeSpan.FromSeconds(10));

            AutomationElement toggleReading = WaitForMenuItem(
                window,
                "EditorMenu",
                "EditorToggleReadingModeMenuItem",
                TimeSpan.FromSeconds(10));
            toggleReading.Patterns.Invoke.Pattern.Invoke();

            AutomationElement surface = WaitForElement(
                window, "ReadingSurface", TimeSpan.FromSeconds(10));

            // Async artifacts (math, then the slower mermaid render)
            // re-project the document as they complete, KILLING every
            // previously-snapshotted UIA element - the census must not
            // race them (observed live: a CI runner finished the
            // diagram between the math wait and the snapshot, and
            // eight of nine kinds read "absent" off dead elements).
            _ = WaitForSurfaceDescendant(
                window,
                element => string.Equals(
                    element.Properties.LocalizedControlType.ValueOrDefault,
                    "math",
                    StringComparison.Ordinal),
                "math element",
                TimeSpan.FromSeconds(30));
            _ = WaitForSurfaceDescendant(
                window,
                element => string.Equals(
                    element.Properties.LocalizedControlType.ValueOrDefault,
                    "diagram",
                    StringComparison.Ordinal),
                "diagram element",
                // Generous by design: the FIRST mermaid render pays
                // fontdb's full system-font scan on a cold runner, and
                // the census-diag run (2026-08-01) proved 90 s is not
                // enough on the 2-vCPU gate runner — the peer tree held
                // 9 fresh children with NO diagram merge for the whole
                // window, while the render starved the UI thread hard
                // enough that concurrent UIA walks came back truncated
                // (2 of 9 children). SpinUntil returns the moment the
                // element exists, so the timeout only bounds the
                // failure case.
                TimeSpan.FromSeconds(300),
                Path.Combine(logDirectory, "slate-windows.log"));

            // POSITIVE CENSUS - every custom peer kind, object-tree
            // structural peers included. Each entry: kind, matcher,
            // embedded? (identity via NVDA round-trip) and, for
            // TextElement peers, the text their range must carry.
            var census = new (string Kind, Func<AutomationElement, bool> Match, bool Embedded, string? RangeText)[]
            {
                ("heading",
                    e => e.Properties.ControlType.ValueOrDefault == ControlType.Text
                        && string.Equals(e.Properties.Name.ValueOrDefault, "Range note", StringComparison.Ordinal),
                    false, "Range note"),
                ("list",
                    e => e.Properties.ControlType.ValueOrDefault == ControlType.List,
                    false, "alpha bullet"),
                ("list-item",
                    e => e.Properties.ControlType.ValueOrDefault == ControlType.ListItem
                        && (e.Properties.Name.ValueOrDefault ?? "").Contains("alpha", StringComparison.OrdinalIgnoreCase),
                    false, "alpha bullet"),
                ("code-block",
                    e => (e.Properties.Name.ValueOrDefault ?? "").StartsWith("Code block,", StringComparison.Ordinal),
                    false, "fn f()"),
                ("task-checkbox",
                    e => e.Properties.ControlType.ValueOrDefault == ControlType.CheckBox,
                    true, null),
                ("math",
                    e => string.Equals(e.Properties.LocalizedControlType.ValueOrDefault, "math", StringComparison.Ordinal),
                    true, null),
                ("copy-code",
                    e => string.Equals(e.Properties.Name.ValueOrDefault, "Copy code", StringComparison.Ordinal),
                    false, "Copy code"),
                ("diagram",
                    e => string.Equals(e.Properties.LocalizedControlType.ValueOrDefault, "diagram", StringComparison.Ordinal),
                    true, null),
                ("embed-jump",
                    e => string.Equals(e.Properties.AutomationId.ValueOrDefault, "ReadingBlockEmbed", StringComparison.Ordinal),
                    false, "Jump to source"),
            };

            // One CONSISTENT snapshot holding every census kind -
            // retried because artifact completions between snapshots
            // invalidate elements (the CI race above).
            AutomationElement[] descendants = Array.Empty<AutomationElement>();
            Assert.True(
                PollGently(
                    () =>
                    {
                        // Re-resolve EVERY spin: an artifact completion
                        // replaces the surface control, and the dead
                        // handle keeps answering with the old fragment
                        // (measured on CI, 2026-08-01: two descendants
                        // for the full 90s diagram wait).
                        surface = window.FindFirstDescendant(
                            cf => cf.ByAutomationId("ReadingSurface")) ?? surface;
                        ForceFullTextLayout(surface);
                        descendants = surface.FindAllDescendants();
                        return census.All(entry =>
                            descendants.Any(element => entry.Match(element)));
                    },
                    TimeSpan.FromSeconds(15)),
                "the census kinds never appeared together in one stable snapshot; "
                    + "present: " + string.Join(
                        ", ",
                        census.Where(entry =>
                            descendants.Any(element => entry.Match(element)))
                            .Select(entry => entry.Kind)));
            // Bind the Text pattern off the surface the census just
            // proved LIVE - the pre-wait handle may be a corpse.
            var text = surface.Patterns.Text.Pattern;
            var document = text.DocumentRange;
            var failures = new List<string>();
            var resolved = new List<(string Kind, FlaUI.Core.ITextRange Range)>();
            foreach ((string kind, Func<AutomationElement, bool> match, bool embedded, string? rangeText) in census)
            {
                AutomationElement? element = descendants.FirstOrDefault(match);
                if (element is null)
                {
                    failures.Add($"{kind}: element absent from the census");
                    continue;
                }
                string? failure = ProbeRangeFromChild(
                    text, document, kind, element, embedded, rangeText, out var range);
                if (failure is not null)
                {
                    failures.Add(failure);
                }
                else if (range is not null)
                {
                    resolved.Add((kind, range));
                }
            }

            // DOCUMENT ORDER: the census is authored top-to-bottom, so
            // successive resolved ranges must start strictly later -
            // two probes answered by one bogus range cannot pass.
            // (Embedded elements and their own containers - list vs
            // its first item, code block vs its Copy link - may share
            // a start, so strict ordering is asserted between
            // DISTINCT block-level kinds only.)
            var orderKinds = new[] { "heading", "list", "task-checkbox", "math", "diagram", "embed-jump" };
            var ordered = resolved.Where(r => orderKinds.Contains(r.Kind)).ToList();
            for (int i = 1; i < ordered.Count; i++)
            {
                if (ordered[i].Range.CompareEndpoints(
                        TextPatternRangeEndpoint.Start,
                        ordered[i - 1].Range,
                        TextPatternRangeEndpoint.Start) <= 0)
                {
                    failures.Add(
                        $"order: {ordered[i].Kind} does not start after {ordered[i - 1].Kind}");
                }
            }

            // RECURSIVE range walk - the renderer path. Every child at
            // every level must resolve; the visited set and the
            // embedded round-trip guard terminate the walk.
            var visited = new HashSet<string>();
            WalkRanges(text, document, document, depth: 0, visited, failures);

            Assert.True(
                failures.Count == 0,
                "RangeFromChild hardened probe failed:\n" + string.Join("\n", failures));
        }
        finally
        {
            if (process is not null && !process.HasExited)
            {
                process.CloseMainWindow();
                if (!process.WaitForExit(5_000))
                {
                    process.Kill(entireProcessTree: true);
                }
            }

            try
            {
                Directory.Delete(testRoot, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    /// <summary>
    /// One RangeFromChild probe with IDENTITY, not just sanity:
    /// embedded elements must round-trip through the child range's
    /// GetChildren (NVDA's own embedded-object detection); TextElement
    /// peers must carry their own content in the range text. All
    /// ranges must be ordered and inside the document.
    /// </summary>
    private static string? ProbeRangeFromChild(
        FlaUI.Core.Patterns.ITextPattern text,
        FlaUI.Core.ITextRange document,
        string kind,
        AutomationElement element,
        bool embedded,
        string? expectedRangeText,
        out FlaUI.Core.ITextRange? range)
    {
        range = null;
        try
        {
            range = text.RangeFromChild(element);
            if (range is null)
            {
                return $"{kind}: returned null";
            }
            if (range.CompareEndpoints(
                    TextPatternRangeEndpoint.Start,
                    range,
                    TextPatternRangeEndpoint.End) > 0)
            {
                return $"{kind}: endpoints out of order";
            }
            if (range.CompareEndpoints(
                    TextPatternRangeEndpoint.Start,
                    document,
                    TextPatternRangeEndpoint.Start) < 0
                || range.CompareEndpoints(
                    TextPatternRangeEndpoint.End,
                    document,
                    TextPatternRangeEndpoint.End) > 0)
            {
                return $"{kind}: range escapes the document";
            }
            if (embedded)
            {
                AutomationElement[] children = range.GetChildren();
                bool roundTrips = children.Any(child =>
                    child.Equals(element));
                if (!roundTrips)
                {
                    return $"{kind}: child range does not round-trip to the element "
                        + $"(GetChildren yielded {children.Length})";
                }
            }
            else if (expectedRangeText is not null)
            {
                string rangeText = range.GetText(-1) ?? string.Empty;
                if (!rangeText.Contains(expectedRangeText, StringComparison.Ordinal))
                {
                    return $"{kind}: range text {Truncate(rangeText)} lacks "
                        + $"\"{expectedRangeText}\"";
                }
            }
            return null;
        }
        catch (Exception exception)
        {
            return $"{kind}: {exception.GetType().Name}: {exception.Message}"
                + $" (hr=0x{exception.HResult:X8})";
        }
    }

    /// <summary>
    /// The NVDA renderer path: recursively resolve every child the
    /// text pattern exposes at every level. The embedded round-trip
    /// (single child equal to the element recursed from) terminates
    /// embedded-object branches exactly as NVDA's own detection does;
    /// the visited set guards against any other cycle.
    /// </summary>
    private static void WalkRanges(
        FlaUI.Core.Patterns.ITextPattern text,
        FlaUI.Core.ITextRange document,
        FlaUI.Core.ITextRange range,
        int depth,
        HashSet<string> visited,
        List<string> failures)
    {
        if (depth > 8)
        {
            failures.Add("walk: depth exceeded 8 - unexpected nesting or a cycle");
            return;
        }
        foreach (AutomationElement child in range.GetChildren())
        {
            string id = string.Join(
                ".",
                (child.Properties.RuntimeId.ValueOrDefault ?? Array.Empty<int>()));
            if (id.Length > 0 && !visited.Add($"{depth}:{id}"))
            {
                continue;
            }
            string kind =
                $"walk[{depth}]:{child.Properties.LocalizedControlType.ValueOrDefault ?? "?"}"
                + $":{Truncate(child.Properties.Name.ValueOrDefault ?? "")}";
            string? failure = ProbeRangeFromChild(
                text, document, kind, child, embedded: false, expectedRangeText: null,
                out FlaUI.Core.ITextRange? childRange);
            if (failure is not null)
            {
                failures.Add(failure);
                continue;
            }
            if (childRange is null)
            {
                continue;
            }
            AutomationElement[] grandChildren = childRange.GetChildren();
            bool embeddedLeaf = grandChildren.Length == 1
                && grandChildren[0].Equals(child);
            if (!embeddedLeaf)
            {
                WalkRanges(text, document, childRange, depth + 1, visited, failures);
            }
        }
    }

    private static string Truncate(string value) =>
        value.Length <= 40 ? value : value[..40] + "...";

    /// <summary>
    /// RichTextBox text layout is VIEWPORT-virtualized: content below
    /// the fold is never laid out — and its embedded elements never
    /// get navigable peers — until something scrolls it into view
    /// (measured 2026-08-01: the sibling walk ends cleanly at the
    /// last viewport child regardless of wait budget or poll cadence).
    /// Read the document the way a user does: scroll to the end, then
    /// back, through the Text pattern.
    /// </summary>
    private static void ForceFullTextLayout(AutomationElement surface)
    {
        try
        {
            if (surface.Patterns.Text.PatternOrDefault is { } text)
            {
                var endRange = text.DocumentRange.Clone();
                endRange.MoveEndpointByRange(
                    TextPatternRangeEndpoint.Start,
                    text.DocumentRange,
                    TextPatternRangeEndpoint.End);
                endRange.ScrollIntoView(alignToTop: false);
                var startRange = text.DocumentRange.Clone();
                startRange.MoveEndpointByRange(
                    TextPatternRangeEndpoint.End,
                    text.DocumentRange,
                    TextPatternRangeEndpoint.Start);
                startRange.ScrollIntoView(alignToTop: true);
            }
        }
        catch (Exception)
        {
            // A re-projection can kill the range mid-scroll — the next
            // poll retries against the fresh surface.
        }
    }

    /// <summary>One probe a second - the polls themselves must not
    /// starve the app's idle-priority background work (text layout)
    /// that produces the condition being awaited.</summary>
    private static bool PollGently(Func<bool> condition, TimeSpan timeout)
    {
        DateTime deadline = DateTime.UtcNow + timeout;
        while (true)
        {
            if (condition())
            {
                return true;
            }
            if (DateTime.UtcNow >= deadline)
            {
                return false;
            }
            Thread.Sleep(1000);
        }
    }

    private static AutomationElement WaitForSurfaceDescendant(
        Window window,
        Func<AutomationElement, bool> match,
        string description,
        TimeSpan timeout,
        string? diagnosticLogPath = null)
    {
        AutomationElement? found = null;
        AutomationElement[] last = Array.Empty<AutomationElement>();
        // GENTLE polling, deliberately: RichTextBox lays out text in
        // BACKGROUND idle time, and a tight UIA poll marshals into the
        // UI thread continuously - starving the exact idle work that
        // realizes embedded elements past the viewport (measured
        // 2026-08-01: five minutes of tight polling, tree frozen at
        // the first viewport's children). One probe a second leaves
        // the app idle ~95% of the wait.
        bool appeared = PollGently(
            () =>
            {
                // Artifact completions REPLACE the surface control
                // (same AutomationId, new UIA element); a handle
                // captured before the wait goes dead and answers with
                // a stale two-element fragment until the timeout
                // (measured on CI, 2026-08-01). Resolve fresh, always.
                AutomationElement? surface = window.FindFirstDescendant(
                    cf => cf.ByAutomationId("ReadingSurface"));
                if (surface is null)
                {
                    return false;
                }
                ForceFullTextLayout(surface);
                last = surface.FindAllDescendants();
                found = last.FirstOrDefault(match);
                return found is not null;
            },
            timeout);
        if (!appeared)
        {
            // Diagnostic failure (CI-only repro): what DID the surface
            // hold, and what does the app log say about the artifact
            // pipeline? Guessing at timeouts stops here.
            string census = string.Join(
                " | ",
                last.Take(40).Select(element =>
                {
                    string kind =
                        element.Properties.LocalizedControlType.ValueOrDefault ?? "?";
                    string name = element.Properties.Name.ValueOrDefault ?? "";
                    if (name.Length > 30)
                    {
                        name = name[..30] + "...";
                    }
                    return $"{kind}:{name}";
                }));
            // How many surfaces exist, and what does the probed one's
            // TEXT PATTERN carry? The range text separates "projection
            // never completed" (placeholder text) from "content merged
            // but peers stale" (full note text, missing elements).
            AutomationElement[] surfaces = window.FindAllDescendants(
                cf => cf.ByAutomationId("ReadingSurface"));
            // Manual sibling walk with the raw view walker: the diag
            // channel proved the peer holds 9 fresh children while
            // FindAll returns 2 — so navigation DIES on a specific
            // sibling. Name it, step by step, exception included.
            string walkTrace = "<no surface>";
            if (surfaces.Length > 0)
            {
                var steps = new List<string>();
                try
                {
                    var walker = surfaces[0].Automation.TreeWalkerFactory
                        .GetRawViewWalker();
                    AutomationElement? step = walker.GetFirstChild(surfaces[0]);
                    int guard = 0;
                    while (step is not null && guard++ < 20)
                    {
                        steps.Add(
                            (step.Properties.LocalizedControlType.ValueOrDefault ?? "?")
                            + ":"
                            + (step.Properties.Name.ValueOrDefault ?? ""));
                        step = walker.GetNextSibling(step);
                    }
                    steps.Add("<walk ended>");
                }
                catch (Exception exception)
                {
                    steps.Add($"<walk threw: {exception.GetType().Name}: "
                        + $"{exception.Message}>");
                }
                walkTrace = string.Join(" | ", steps);
            }
            string rangeText = "<no surface>";
            if (surfaces.Length > 0)
            {
                try
                {
                    rangeText = surfaces[0].Patterns.Text.PatternOrDefault
                        ?.DocumentRange.GetText(1200)
                        ?? "<no text pattern>";
                }
                catch (Exception exception)
                {
                    rangeText = $"<text pattern threw: {exception.GetType().Name}>";
                }
            }
            string logDiagnostics = "<no log requested>";
            if (diagnosticLogPath is not null)
            {
                string? directory = Path.GetDirectoryName(diagnosticLogPath);
                string listing = directory is not null && Directory.Exists(directory)
                    ? string.Join(
                        ", ",
                        Directory.EnumerateFiles(directory).Select(file =>
                            $"{Path.GetFileName(file)}({new FileInfo(file).Length}b)"))
                    : "<log dir absent>";
                string log = ReadSharedLog(diagnosticLogPath);
                string logTail = log.Length <= 2000 ? log : log[^2000..];
                // The peer-churn diagnostics ride their OWN file — the
                // app holds the main log open for the stderr redirect,
                // so a second writer dies on a sharing violation.
                string diag = directory is null
                    ? ""
                    : ReadSharedLog(Path.Combine(directory, "slate-census-diag.log"));
                string diagTail = diag.Length <= 2500 ? diag : diag[^2500..];
                logDiagnostics =
                    $"dir: {listing}\napp log tail: {logTail}\ncensus diag: {diagTail}";
            }
            Assert.Fail(
                $"no {description} appeared under ReadingSurface.\n"
                + $"surfaces: {surfaces.Length}\n"
                + $"descendants ({last.Length}): {census}\n"
                + $"sibling walk: {walkTrace}\n"
                + $"range text: {rangeText.Replace("\r", "\\r").Replace("\n", "\\n")}\n"
                + logDiagnostics);
        }
        return found!;
    }

    /// <summary>
    /// Waits for <paramref name="element"/> to hold keyboard focus, and on
    /// failure says what actually had it.
    /// </summary>
    /// <remarks>
    /// <c>HasKeyboardFocus</c> is foreground-dependent: if any other
    /// process owns the foreground window, every element in ours reports
    /// false and the bare assertion reads exactly like a product defect.
    /// #1107 was filed against this shape — a deterministic "Committing
    /// Quick Open did not focus the destination editor" that had been
    /// hidden for waves behind an earlier stolen-chord failure, with no way
    /// to tell an environmental cause from a real one. The failure now
    /// names the foreground owner, so the two are distinguishable on sight
    /// rather than by re-deriving it each time.
    /// </remarks>
    private static void AssertEventuallyFocused(AutomationElement element, string message)
    {
        if (SpinWait.SpinUntil(
            () => element.Properties.HasKeyboardFocus.Value,
            TimeSpan.FromSeconds(10)))
        {
            return;
        }

        throw new Xunit.Sdk.XunitException($"{message} {FocusDiagnosis()}");
    }

    /// <summary>
    /// Who owns the foreground, and whether it is this test's app — the
    /// difference between an environmental failure and a real one.
    /// </summary>
    private static string FocusDiagnosis()
    {
        IntPtr foreground = NativeForeground.GetForegroundWindow();
        if (foreground == IntPtr.Zero)
        {
            return "No window owns the foreground at all — the desktop is "
                + "locked, disconnected, or another session has it. This is "
                + "environmental, not a product defect.";
        }

        var title = new System.Text.StringBuilder(512);
        _ = NativeForeground.GetWindowText(foreground, title, title.Capacity);
        _ = NativeForeground.GetWindowThreadProcessId(foreground, out uint processId);
        string owner;
        try
        {
            owner = Process.GetProcessById((int)processId).ProcessName;
        }
        catch (ArgumentException)
        {
            owner = "<exited>";
        }

        return $"The foreground window is '{title}' owned by {owner} "
            + $"(pid {processId}). If that is not this test's Slate instance, "
            + "keyboard focus cannot be observed in ours and the failure is "
            + "environmental.";
    }

    private static class NativeForeground
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        internal static extern IntPtr GetForegroundWindow();

        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet =
            System.Runtime.InteropServices.CharSet.Unicode)]
        internal static extern int GetWindowText(
            IntPtr hWnd, System.Text.StringBuilder text, int count);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        internal static extern uint GetWindowThreadProcessId(
            IntPtr hWnd, out uint processId);
    }



    private static void AssertActionButtonCensus(
        AutomationElement expander,
        UIA3Automation automation,
        params string[] expectedNames)
    {
        Assert.True(expander.Patterns.ExpandCollapse.IsSupported);
        var expansion = expander.Patterns.ExpandCollapse.Pattern;
        if (expansion.ExpandCollapseState.Value != ExpandCollapseState.Expanded)
        {
            expansion.Expand();
        }
        Assert.True(
            SpinWait.SpinUntil(
                () => expansion.ExpandCollapseState.Value == ExpandCollapseState.Expanded,
                TimeSpan.FromSeconds(10)),
            $"{expander.AutomationId} did not expose its expanded state.");

        AutomationElement[] buttons = [];
        Assert.True(
            SpinWait.SpinUntil(
                () =>
                {
                    buttons = expander.FindAllDescendants(
                            automation.ConditionFactory.ByControlType(ControlType.Button))
                        .Where(button => button.Patterns.Invoke.IsSupported)
                        .ToArray();
                    return buttons.Length == expectedNames.Length;
                },
                TimeSpan.FromSeconds(10)),
            $"{expander.AutomationId} exposed {buttons.Length} Invoke buttons; " +
            $"expected {expectedNames.Length}.");

        foreach (AutomationElement button in buttons)
        {
            Assert.True(
                button.Patterns.Invoke.IsSupported,
                $"{expander.AutomationId}/{button.Name} does not expose Invoke.");
        }
        Assert.Equal(
            expectedNames.OrderBy(name => name, StringComparer.Ordinal).ToArray(),
            buttons.Select(button => button.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray());
    }

    private static void AssertElementDisappears(
        Window window,
        UIA3Automation automation,
        string automationId)
    {
        if (window.FindFirstDescendant(
            automation.ConditionFactory.ByAutomationId(automationId)) is null)
        {
            return;
        }

        Assert.True(
            SpinWait.SpinUntil(
                () => window.FindFirstDescendant(
                    automation.ConditionFactory.ByAutomationId(automationId)) is null,
                TimeSpan.FromSeconds(10)),
            $"UIA element {automationId} remained visible.");
    }

    private static void AssertQuickSwitcherDisappears(
        Window window,
        UIA3Automation automation)
    {
        foreach (string automationId in new[]
        {
            "QuickSwitcher",
            "QuickSwitcherSearch",
            "QuickSwitcherResults",
            "QuickSwitcherClose",
        })
        {
            AssertElementDisappears(window, automation, automationId);
        }
    }

    /// <summary>
    /// W4-2 (#734): the four link/structure leaves over the live app.
    /// Each leaf body appears with its mac-labeled rows when its rail
    /// entry is selected, and a backlink activation navigates the
    /// workspace to the source note.
    /// </summary>
    [Fact]
    public void RightPanePanels_LeafBodiesCarryRowsAndBacklinkNavigates()
    {
        string testRoot = Path.Combine(
            Path.GetTempPath(),
            $"slate-panels-{Guid.NewGuid():N}");
        string vaultRoot = Path.Combine(testRoot, "Panels Vault");
        string logDirectory = Path.Combine(testRoot, "logs");
        Directory.CreateDirectory(vaultRoot);
        File.WriteAllText(
            Path.Combine(vaultRoot, "host.md"),
            "# Alpha\n\n## Beta\n\nSee [[target]] and [[missing]] and "
                + "[link](https://example.com) here.\n\n![[target]]\n");
        File.WriteAllText(
            Path.Combine(vaultRoot, "target.md"),
            "# Target\n\nBody. Links back to [[host]].\n");

        Process? process = null;
        try
        {
            var startInfo = new ProcessStartInfo(SlateWindowsExe())
            {
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
            };
            startInfo.ArgumentList.Add(vaultRoot);
            startInfo.Environment["SLATE_CENSUS_INSTANCE_ID"] =
                $"slate-panels-{Guid.NewGuid():N}";
            startInfo.Environment["SLATE_LOG_DIR"] = logDirectory;
            process = Process.Start(startInfo)
                ?? throw new Xunit.Sdk.XunitException("SlateWindows.exe did not start.");

            if (!HasInteractiveDesktop(process, "panels"))
            {
                return;
            }

            using var automation = new UIA3Automation();
            Window window = WaitForMainWindow(
                process,
                automation,
                Path.Combine(logDirectory, "slate-windows.log"),
                TimeSpan.FromSeconds(30));

            AutomationElement filesTree = WaitForElement(
                window, "FilesTree", TimeSpan.FromSeconds(30));
            AutomationElement hostItem = filesTree
                .FindAllDescendants(
                    automation.ConditionFactory.ByControlType(ControlType.TreeItem))
                .FirstOrDefault(item =>
                    item.Name.StartsWith("host", StringComparison.OrdinalIgnoreCase))
                ?? throw new Xunit.Sdk.XunitException("The host TreeItem is absent.");
            hostItem.Patterns.SelectionItem.Pattern.Select();
            WaitForEditor(
                window, automation, "host.md editor", TimeSpan.FromSeconds(10));

            AutomationElement leaves = WaitForElement(
                window, "RightPaneLeaves", TimeSpan.FromSeconds(10));
            void SelectLeaf(string title)
            {
                // Retried: the rail's ListItems materialize lazily on
                // the gate runner (the W4-1 playbook — one-shot
                // enumeration races composition).
                AutomationElement? entry = null;
                Assert.True(
                    SpinWait.SpinUntil(
                        () =>
                        {
                            entry = leaves
                                .FindAllDescendants(
                                    automation.ConditionFactory.ByControlType(
                                        ControlType.ListItem))
                                .FirstOrDefault(item =>
                                    (item.Properties.Name.ValueOrDefault ?? "")
                                        == title);
                            return entry is not null;
                        },
                        TimeSpan.FromSeconds(15)),
                    $"No rail entry named {title}.");
                entry!.Patterns.SelectionItem.Pattern.Select();
            }
            AutomationElement WaitForRow(
                AutomationElement list, Func<string, bool> match, string description)
            {
                AutomationElement? found = null;
                Assert.True(
                    SpinWait.SpinUntil(
                        () =>
                        {
                            found = list
                                .FindAllDescendants(
                                    automation.ConditionFactory.ByControlType(
                                        ControlType.ListItem))
                                .FirstOrDefault(item =>
                                    match(item.Properties.Name.ValueOrDefault ?? ""));
                            return found is not null;
                        },
                        TimeSpan.FromSeconds(15)),
                    $"no row matching {description} appeared");
                return found!;
            }

            // Outline: flat rows with the level-labeled names.
            SelectLeaf("Outline");
            AutomationElement outline = WaitForElement(
                window, "PanelOutlineList", TimeSpan.FromSeconds(10));
            _ = WaitForRow(
                outline,
                name => name == "Level 1 heading: Alpha",
                "the level-1 heading");
            _ = WaitForRow(
                outline,
                name => name == "Level 2 heading: Beta",
                "the level-2 heading");

            // Outgoing links: the three-state contract in one list.
            SelectLeaf("Outgoing links");
            AutomationElement outgoing = WaitForElement(
                window, "PanelOutgoingLinksList", TimeSpan.FromSeconds(10));
            _ = WaitForRow(
                outgoing, name => name == "Link to target.md", "the resolved link");
            AutomationElement unresolvedRow = WaitForRow(
                outgoing,
                name => name == "Unresolved link: missing",
                "the unresolved link");
            Assert.Equal(
                "Cannot open. Target file is not in the vault.",
                unresolvedRow.Properties.HelpText.ValueOrDefault);
            _ = WaitForRow(
                outgoing,
                name => name == "External link: https://example.com",
                "the external link");

            // Embeds: the resolved card with the verbatim name shape
            // and its Jump affordance.
            SelectLeaf("Embeds");
            _ = WaitForElement(
                window, "PanelEmbedsList", TimeSpan.FromSeconds(10));
            Assert.True(
                SpinWait.SpinUntil(
                    () => window.FindFirstDescendant(
                        automation.ConditionFactory.ByName(
                            "Embedded note: target.md")) is not null,
                    TimeSpan.FromSeconds(20)),
                "the embed card never resolved");
            Assert.True(
                SpinWait.SpinUntil(
                    () => window.FindFirstDescendant(
                        automation.ConditionFactory.ByName(
                            "Jump to source: target.md")) is not null,
                    TimeSpan.FromSeconds(10)),
                "the embed Jump affordance is absent");

            // Backlinks: the composed row label, then a real
            // activation — Enter on the focused row navigates the
            // workspace to the source note.
            SelectLeaf("Backlinks");
            AutomationElement backlinks = WaitForElement(
                window, "PanelBacklinksList", TimeSpan.FromSeconds(10));
            AutomationElement backlinkRow = WaitForRow(
                backlinks,
                name => name.StartsWith(
                    "Backlink from target.md, context: ",
                    StringComparison.Ordinal),
                "the backlink row");
            Assert.Equal(
                "Opens the source note.",
                backlinkRow.Properties.HelpText.ValueOrDefault);
            backlinkRow.Patterns.SelectionItem.Pattern.Select();
            backlinkRow.Focus();
            Keyboard.Type(VirtualKeyShort.RETURN);
            WaitForEditor(
                window, automation, "target.md editor", TimeSpan.FromSeconds(10));

            AssertAxeClean(process, "right-pane-panels");
        }
        finally
        {
            if (process is not null && !process.HasExited)
            {
                process.CloseMainWindow();
                if (!process.WaitForExit(5_000))
                {
                    process.Kill(entireProcessTree: true);
                }
            }

            try
            {
                Directory.Delete(testRoot, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    /// <summary>W4-3 (#735): the two task leaves — note-scoped rows
    /// with the composed mac labels and a Space toggle that reaches
    /// disk, then the vault-wide review with its filter chips and
    /// filename-led rows.</summary>
    [Fact]
    public void TaskPanels_RowsToggleAndReviewCarriesTheMacShapes()
    {
        string testRoot = Path.Combine(
            Path.GetTempPath(),
            $"slate-tasks-{Guid.NewGuid():N}");
        string vaultRoot = Path.Combine(testRoot, "Tasks Vault");
        string logDirectory = Path.Combine(testRoot, "logs");
        Directory.CreateDirectory(vaultRoot);
        string todoPath = Path.Combine(vaultRoot, "todo.md");
        File.WriteAllText(
            todoPath,
            "# Todo\n\n- [ ] first open 📅 2026-03-01\n- [x] finished\n");
        File.WriteAllText(
            Path.Combine(vaultRoot, "other.md"), "- [ ] from other\n");

        Process? process = null;
        try
        {
            var startInfo = new ProcessStartInfo(SlateWindowsExe())
            {
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
            };
            startInfo.ArgumentList.Add(vaultRoot);
            startInfo.Environment["SLATE_CENSUS_INSTANCE_ID"] =
                $"slate-tasks-{Guid.NewGuid():N}";
            startInfo.Environment["SLATE_LOG_DIR"] = logDirectory;
            process = Process.Start(startInfo)
                ?? throw new Xunit.Sdk.XunitException("SlateWindows.exe did not start.");

            if (!HasInteractiveDesktop(process, "tasks"))
            {
                return;
            }

            using var automation = new UIA3Automation();
            Window window = WaitForMainWindow(
                process,
                automation,
                Path.Combine(logDirectory, "slate-windows.log"),
                TimeSpan.FromSeconds(30));

            AutomationElement filesTree = WaitForElement(
                window, "FilesTree", TimeSpan.FromSeconds(30));
            AutomationElement todoItem = filesTree
                .FindAllDescendants(
                    automation.ConditionFactory.ByControlType(ControlType.TreeItem))
                .FirstOrDefault(item =>
                    item.Name.StartsWith("todo", StringComparison.OrdinalIgnoreCase))
                ?? throw new Xunit.Sdk.XunitException("The todo TreeItem is absent.");
            todoItem.Patterns.SelectionItem.Pattern.Select();
            WaitForEditor(
                window, automation, "todo.md editor", TimeSpan.FromSeconds(10));

            AutomationElement leaves = WaitForElement(
                window, "RightPaneLeaves", TimeSpan.FromSeconds(10));
            void SelectLeaf(string title)
            {
                AutomationElement? entry = null;
                Assert.True(
                    SpinWait.SpinUntil(
                        () =>
                        {
                            entry = leaves
                                .FindAllDescendants(
                                    automation.ConditionFactory.ByControlType(
                                        ControlType.ListItem))
                                .FirstOrDefault(item =>
                                    (item.Properties.Name.ValueOrDefault ?? "")
                                        == title);
                            return entry is not null;
                        },
                        TimeSpan.FromSeconds(15)),
                    $"No rail entry named {title}.");
                entry!.Patterns.SelectionItem.Pattern.Select();
            }
            AutomationElement WaitForRow(
                string listId, Func<string, bool> match, string description)
            {
                AutomationElement list = WaitForElement(
                    window, listId, TimeSpan.FromSeconds(10));
                AutomationElement? found = null;
                Assert.True(
                    SpinWait.SpinUntil(
                        () =>
                        {
                            found = list
                                .FindAllDescendants(
                                    automation.ConditionFactory.ByControlType(
                                        ControlType.ListItem))
                                .FirstOrDefault(item =>
                                    match(item.Properties.Name.ValueOrDefault ?? ""));
                            return found is not null;
                        },
                        TimeSpan.FromSeconds(15)),
                    $"no row matching {description} appeared");
                return found!;
            }

            // The note-scoped Tasks leaf: composed mac labels on both
            // groups.
            SelectLeaf("Tasks");
            AutomationElement openRow = WaitForRow(
                "PanelTasksOpenList",
                name => name == "Open. first open. Due 2026-03-01. Open task.",
                "the open task row");
            Assert.Equal(
                "Scrolls the editor to this task's line.",
                openRow.Properties.HelpText.ValueOrDefault);
            _ = WaitForRow(
                "PanelTasksDoneList",
                name => name == "Done. finished. Done task.",
                "the done task row");

            // Space on the focused open row toggles through the
            // guarded tab path — the DISK flip is the observable.
            openRow.Patterns.SelectionItem.Pattern.Select();
            openRow.Focus();
            Keyboard.Type(VirtualKeyShort.SPACE);
            Assert.True(
                SpinWait.SpinUntil(
                    () =>
                    {
                        // Tolerant of the app's atomic temp+rename
                        // write racing this poll.
                        try
                        {
                            return File.ReadAllText(todoPath)
                                .Contains("- [x] first open");
                        }
                        catch (IOException)
                        {
                            return false;
                        }
                    },
                    TimeSpan.FromSeconds(20)),
                "the Space toggle never reached disk");

            // The vault-wide review: header, filter chips, and the
            // filename-led row shape (the other note's task proves
            // vault scope).
            SelectLeaf("Tasks Review");
            _ = WaitForElement(window, "PanelReviewList", TimeSpan.FromSeconds(10));
            _ = WaitForRow(
                "PanelReviewList",
                name => name == "other.md. from other. Open task.",
                "the cross-note review row");
            // The chips are RADIO buttons (adversarial round 3): the
            // active filter is a real UIA selection state — activate
            // one and assert the mutually exclusive selection moved.
            AutomationElement? overdueChip = null;
            AutomationElement? allChip = null;
            Assert.True(
                SpinWait.SpinUntil(
                    () =>
                    {
                        AutomationElement[] chips = window.FindAllDescendants(
                            automation.ConditionFactory.ByControlType(
                                ControlType.RadioButton));
                        overdueChip = chips.FirstOrDefault(chip =>
                            (chip.Properties.HelpText.ValueOrDefault ?? "")
                                == "Filter the review to overdue tasks.");
                        allChip = chips.FirstOrDefault(chip =>
                            (chip.Properties.HelpText.ValueOrDefault ?? "")
                                == "Filter the review to all tasks.");
                        return overdueChip is not null && allChip is not null;
                    },
                    TimeSpan.FromSeconds(10)),
                "the review filter radio chips are absent");
            Assert.True(
                allChip!.Patterns.SelectionItem.Pattern.IsSelected.ValueOrDefault,
                "the All chip must start selected");
            Assert.False(
                overdueChip!.Patterns.SelectionItem.Pattern.IsSelected.ValueOrDefault);

            overdueChip.Patterns.SelectionItem.Pattern.Select();
            Assert.True(
                SpinWait.SpinUntil(
                    () => overdueChip.Patterns.SelectionItem.Pattern
                            .IsSelected.ValueOrDefault
                        && !allChip.Patterns.SelectionItem.Pattern
                            .IsSelected.ValueOrDefault,
                    TimeSpan.FromSeconds(10)),
                "selecting the Overdue chip never took the UIA selection");

            AssertAxeClean(process, "task-panels");
        }
        finally
        {
            if (process is not null && !process.HasExited)
            {
                process.CloseMainWindow();
                if (!process.WaitForExit(5_000))
                {
                    process.Kill(entireProcessTree: true);
                }
            }

            try
            {
                Directory.Delete(testRoot, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    [Fact]
    public void PropertiesHeader_RowsEditSheetsAndRenameCarryTheMacShapes()
    {
        string testRoot = Path.Combine(
            Path.GetTempPath(),
            $"slate-props-{Guid.NewGuid():N}");
        string vaultRoot = Path.Combine(testRoot, "Props Vault");
        string logDirectory = Path.Combine(testRoot, "logs");
        Directory.CreateDirectory(vaultRoot);
        string notePath = Path.Combine(vaultRoot, "props.md");
        File.WriteAllText(
            notePath, "---\ntitle: Hello\ncount: 42\n---\nBody.\n");

        Process? process = null;
        try
        {
            var startInfo = new ProcessStartInfo(SlateWindowsExe())
            {
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
            };
            startInfo.ArgumentList.Add(vaultRoot);
            startInfo.Environment["SLATE_CENSUS_INSTANCE_ID"] =
                $"slate-props-{Guid.NewGuid():N}";
            startInfo.Environment["SLATE_LOG_DIR"] = logDirectory;
            process = Process.Start(startInfo)
                ?? throw new Xunit.Sdk.XunitException("SlateWindows.exe did not start.");

            if (!HasInteractiveDesktop(process, "properties"))
            {
                return;
            }

            using var automation = new UIA3Automation();
            Window window = WaitForMainWindow(
                process,
                automation,
                Path.Combine(logDirectory, "slate-windows.log"),
                TimeSpan.FromSeconds(30));

            AutomationElement filesTree = WaitForElement(
                window, "FilesTree", TimeSpan.FromSeconds(30));
            AutomationElement? noteItem = null;
            Assert.True(
                SpinWait.SpinUntil(
                    () =>
                    {
                        // The sidebar leads with the frontmatter
                        // title ("Hello"), not the filename.
                        noteItem = filesTree
                            .FindAllDescendants(
                                automation.ConditionFactory.ByControlType(
                                    ControlType.TreeItem))
                            .FirstOrDefault(item =>
                                item.Name.StartsWith(
                                    "Hello", StringComparison.OrdinalIgnoreCase));
                        return noteItem is not null;
                    },
                    TimeSpan.FromSeconds(30)),
                "The props TreeItem never appeared.");
            noteItem!.Patterns.SelectionItem.Pattern.Select();
            WaitForEditor(
                window, automation, "props.md editor", TimeSpan.FromSeconds(10));

            // The header group name counts the properties (§2.1) and
            // sits ABOVE the editor as its own expander.
            AutomationElement header = WaitForElement(
                window, "PropertiesHeader", TimeSpan.FromSeconds(15));
            Assert.True(
                SpinWait.SpinUntil(
                    () => (header.Properties.Name.ValueOrDefault ?? "")
                        == "Properties, 2 properties",
                    TimeSpan.FromSeconds(15)),
                "the header never took the counted group name; "
                    + $"last='{header.Properties.Name.ValueOrDefault}'");

            // The type-cued row editor (§2.8): edit through UIA and
            // commit with Enter — the DISK write is the observable.
            AutomationElement rows = WaitForElement(
                window, "PropertiesRows", TimeSpan.FromSeconds(10));
            AutomationElement? titleEditor = null;
            Assert.True(
                SpinWait.SpinUntil(
                    () =>
                    {
                        titleEditor = rows
                            .FindAllDescendants(
                                automation.ConditionFactory.ByControlType(
                                    ControlType.Edit))
                            .FirstOrDefault(item =>
                                (item.Properties.Name.ValueOrDefault ?? "")
                                    == "Property title, text, editable");
                        return titleEditor is not null;
                    },
                    TimeSpan.FromSeconds(15)),
                "the type-cued title editor is absent");
            titleEditor!.Patterns.Value.Pattern.SetValue("Renamed");
            titleEditor.Focus();
            Keyboard.Type(VirtualKeyShort.ENTER);
            Assert.True(
                SpinWait.SpinUntil(
                    () =>
                    {
                        try
                        {
                            return File.ReadAllText(notePath)
                                .Contains("title: Renamed");
                        }
                        catch (IOException)
                        {
                            return false;
                        }
                    },
                    TimeSpan.FromSeconds(20)),
                "the Enter commit never reached disk");

            // The add sheet opens as a named UIA dialog and Cancel
            // dismisses it.
            AutomationElement addButton = WaitForElement(
                window, "PropertiesAddButton", TimeSpan.FromSeconds(10));
            addButton.Patterns.Invoke.Pattern.Invoke();
            AutomationElement addSheet = WaitForElement(
                window, "AddPropertySheet", TimeSpan.FromSeconds(10));
            Assert.Equal(
                "Add property", addSheet.Properties.Name.ValueOrDefault);
            WaitForElement(window, "AddPropertyCancel", TimeSpan.FromSeconds(10))
                .Patterns.Invoke.Pattern.Invoke();

            // The bulk-rename sheet: old key prefills from the note,
            // preview populates the AccessibleDataGrid and the
            // core-rendered footer.
            AutomationElement renameButton = WaitForElement(
                window, "PropertiesBulkRenameButton", TimeSpan.FromSeconds(10));
            renameButton.Patterns.Invoke.Pattern.Invoke();
            _ = WaitForElement(window, "BulkRenameSheet", TimeSpan.FromSeconds(10));
            AutomationElement oldKey = WaitForElement(
                window, "BulkRenameOldKey", TimeSpan.FromSeconds(10));
            Assert.True(
                SpinWait.SpinUntil(
                    () => (oldKey.Patterns.Value.Pattern.Value.ValueOrDefault ?? "")
                        == "title",
                    TimeSpan.FromSeconds(10)),
                "the old key never prefilled from the active note");
            WaitForElement(window, "BulkRenameNewKey", TimeSpan.FromSeconds(10))
                .Patterns.Value.Pattern.SetValue("heading");
            WaitForElement(window, "BulkRenamePreviewButton", TimeSpan.FromSeconds(10))
                .Patterns.Invoke.Pattern.Invoke();
            AutomationElement footer = WaitForElement(
                window, "BulkRenameFooter", TimeSpan.FromSeconds(10));
            Assert.True(
                SpinWait.SpinUntil(
                    () => (footer.Properties.Name.ValueOrDefault ?? "").Length > 0,
                    TimeSpan.FromSeconds(15)),
                "the preview footer never rendered");
            AutomationElement grid = WaitForElement(
                window, "AccessibleDataGrid", TimeSpan.FromSeconds(10));
            Assert.True(
                SpinWait.SpinUntil(
                    () => grid
                        .FindAllDescendants(
                            automation.ConditionFactory.ByControlType(
                                ControlType.HeaderItem))
                        .Select(item => item.Properties.Name.ValueOrDefault ?? "")
                        .Where(name => name.Length > 0)
                        .ToHashSet()
                        .IsSupersetOf(["Path", "Status", "Before", "After"]),
                    TimeSpan.FromSeconds(15)),
                "the preview grid never exposed the four column headers");
            WaitForElement(window, "BulkRenameClose", TimeSpan.FromSeconds(10))
                .Patterns.Invoke.Pattern.Invoke();

            AssertAxeClean(process, "properties-header");
        }
        finally
        {
            if (process is not null && !process.HasExited)
            {
                process.CloseMainWindow();
                if (!process.WaitForExit(5_000))
                {
                    process.Kill(entireProcessTree: true);
                }
            }

            try
            {
                Directory.Delete(testRoot, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    /// <summary>
    /// W4-5 (#737): the citation surfaces over the live app. The
    /// citations leaf carries its rows, activation opens the details
    /// sheet as an IN-WINDOW dialog (D-1 — a Popup would put the UIA
    /// subtree in a sibling window), Escape returns focus to the row
    /// that opened it (contract 11), Ctrl+J lands on the bibliography
    /// entry, and BOTH bibliography segments expose grid column
    /// headers because both ride the substrate (contract 8 / D-6).
    /// </summary>
    [Fact]
    [Trait("gate", "W-C")]
    public void CitationSurfaces_GridsSheetsAndChords_AreClean()
    {
        string testRoot = Path.Combine(
            Path.GetTempPath(),
            $"slate-citations-{Guid.NewGuid():N}");
        string vaultRoot = Path.Combine(testRoot, "Citations Vault");
        string logDirectory = Path.Combine(testRoot, "logs");
        Directory.CreateDirectory(vaultRoot);
        File.WriteAllText(
            Path.Combine(vaultRoot, "library.bib"),
            "@article{knuth1984,\n  title = {Literate Programming},\n"
                + "  author = {Knuth, Donald E.},\n  year = {1984},\n"
                + "  journal = {The Computer Journal},\n"
                // The abstract and DOI exist so the disclosure and the
                // link path are EXERCISED. Without them the fixture
                // never built those elements, so the gate could not see
                // that neither reached assistive technology.
                + "  doi = {10.1093/comjnl/27.2.97},\n"
                + "  abstract = {Programs should be written for people to read.}\n}\n");
        File.WriteAllText(
            Path.Combine(vaultRoot, "cited.md"),
            "# Cited\n\nA citation [@knuth1984] and a ghost [@ghostkey].\n");
        File.Copy(CitationStyleFixture(), Path.Combine(vaultRoot, "ieee.csl"));
        // A style is REQUIRED for rows to render; without one every row
        // is a placeholder and nothing is expandable (contract 2).
        File.WriteAllText(
            Path.Combine(vaultRoot, "slate.json"),
            "{\"citations\":{\"bibliography\":\"library.bib\",\"cite_style\":\"ieee\"}}");

        Process? process = null;
        try
        {
            var startInfo = new ProcessStartInfo(SlateWindowsExe())
            {
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
            };
            startInfo.ArgumentList.Add(vaultRoot);
            startInfo.Environment["SLATE_CENSUS_INSTANCE_ID"] =
                $"slate-citations-{Guid.NewGuid():N}";
            startInfo.Environment["SLATE_LOG_DIR"] = logDirectory;
            process = Process.Start(startInfo)
                ?? throw new Xunit.Sdk.XunitException("SlateWindows.exe did not start.");

            if (!HasInteractiveDesktop(process, "citations"))
            {
                return;
            }

            using var automation = new UIA3Automation();
            Window window = WaitForMainWindow(
                process,
                automation,
                Path.Combine(logDirectory, "slate-windows.log"),
                TimeSpan.FromSeconds(30));

            AutomationElement filesTree = WaitForElement(
                window, "FilesTree", TimeSpan.FromSeconds(30));
            AutomationElement citedItem = filesTree
                .FindAllDescendants(
                    automation.ConditionFactory.ByControlType(ControlType.TreeItem))
                .FirstOrDefault(item =>
                    item.Name.StartsWith("cited", StringComparison.OrdinalIgnoreCase))
                ?? throw new Xunit.Sdk.XunitException("The cited TreeItem is absent.");
            citedItem.Patterns.SelectionItem.Pattern.Select();
            WaitForEditor(
                window, automation, "cited.md editor", TimeSpan.FromSeconds(10));

            AutomationElement leaves = WaitForElement(
                window, "RightPaneLeaves", TimeSpan.FromSeconds(10));
            void SelectLeaf(string title)
            {
                AutomationElement? entry = null;
                Assert.True(
                    SpinWait.SpinUntil(
                        () =>
                        {
                            entry = leaves
                                .FindAllDescendants(
                                    automation.ConditionFactory.ByControlType(
                                        ControlType.ListItem))
                                .FirstOrDefault(item =>
                                    (item.Properties.Name.ValueOrDefault ?? "") == title);
                            return entry is not null;
                        },
                        TimeSpan.FromSeconds(15)),
                    $"No rail entry named {title}.");
                entry!.Patterns.SelectionItem.Pattern.Select();
            }

            // ---- The citations leaf ------------------------------
            SelectLeaf("Citations");
            AutomationElement citations = WaitForElement(
                window, "PanelCitationsList", TimeSpan.FromSeconds(15));
            AutomationElement? resolvedRow = null;
            Assert.True(
                SpinWait.SpinUntil(
                    () =>
                    {
                        resolvedRow = citations
                            .FindAllDescendants(
                                automation.ConditionFactory.ByControlType(
                                    ControlType.ListItem))
                            .FirstOrDefault(item =>
                                (item.Properties.Name.ValueOrDefault ?? "")
                                    .Contains("Knuth", StringComparison.OrdinalIgnoreCase));
                        return resolvedRow is not null;
                    },
                    TimeSpan.FromSeconds(20)),
                "the citations leaf never rendered the resolved Knuth row");

            // The unresolved key is a real state, not an error
            // (contract 7): it is a row, and it says so.
            Assert.Contains(
                citations
                    .FindAllDescendants(
                        automation.ConditionFactory.ByControlType(ControlType.ListItem))
                    .Select(item => item.Properties.Name.ValueOrDefault ?? ""),
                name => name.Contains("Unresolved", StringComparison.OrdinalIgnoreCase));

            // ---- #1098: a save's republish keeps focus on the list --
            // Dirty the note, sit on a citation row, save. The save's
            // republish destroys every row container (WPF ejects focus
            // to the window root when a focused item unloads — the
            // W5-4 tree finding), so restoring only the SELECTION left
            // AT users Tabbing back to resume; the restored row must
            // own keyboard focus again.
            AutomationElement citedEditor = WaitForEditor(
                window, automation, "cited.md editor", TimeSpan.FromSeconds(10));
            citedEditor.Focus();
            AssertEventuallyFocused(citedEditor, "the cited.md editor could not take focus.");
            Keyboard.Type(" ");
            resolvedRow!.Patterns.SelectionItem.Pattern.Select();
            resolvedRow.Focus();
            AssertEventuallyFocused(
                resolvedRow, "the Knuth row could not take keyboard focus before the save.");
            PressChord(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_S);
            AutomationElement? republishedRow = null;
            Assert.True(
                SpinWait.SpinUntil(
                    () =>
                    {
                        try
                        {
                            republishedRow = citations
                                .FindAllDescendants(
                                    automation.ConditionFactory.ByControlType(
                                        ControlType.ListItem))
                                .FirstOrDefault(item =>
                                    (item.Properties.Name.ValueOrDefault ?? "")
                                        .Contains("Knuth", StringComparison.OrdinalIgnoreCase));
                            return republishedRow is not null
                                && republishedRow.Properties.HasKeyboardFocus.ValueOrDefault;
                        }
                        catch (Exception exception) when (IsTransientUiaFault(exception))
                        {
                            // The republish replaces every row container,
                            // so the poll must tolerate an element going
                            // away mid-read — through the shared filter,
                            // since FlaUI wraps the COM fault (#1142).
                            return false;
                        }
                    },
                    TimeSpan.FromSeconds(10)),
                "after the save's republish, keyboard focus did not return to the "
                + "restored citation row (#1098); focus is on "
                + DescribeFocusedElement(automation)
                + "; list selection = ["
                + string.Join(
                    ", ",
                    (citations.Patterns.Selection.PatternOrDefault?.Selection.ValueOrDefault
                        ?? [])
                        .Select(item => item.Properties.Name.ValueOrDefault ?? "?"))
                + "]; rows = "
                + citations.FindAllDescendants(
                    automation.ConditionFactory.ByControlType(ControlType.ListItem)).Length);
            resolvedRow = republishedRow;

            // ---- Details sheet: in-window, and focus returns -------
            resolvedRow!.Patterns.SelectionItem.Pattern.Select();
            resolvedRow.Focus();
            PressKey(VirtualKeyShort.RETURN);
            AutomationElement details = WaitForElement(
                window, "CitationDetailsSheet", TimeSpan.FromSeconds(10));
            // D-1: the sheet is inside THIS window's subtree. A Popup
            // would make it a sibling HWND and this lookup would miss.
            Assert.NotNull(window.FindFirstDescendant(
                automation.ConditionFactory.ByAutomationId("CitationDetailsSheet")));
            Assert.True(
                details.Patterns.Window.IsSupported
                    || details.Properties.ControlType.ValueOrDefault == ControlType.Pane,
                "the details sheet did not surface as a dialog-shaped element");
            // The fields must REACH assistive technology. Asserting the
            // sheet exists is what the first version of this test did,
            // and it passed while every field was absent from the UIA
            // tree: the item roots were bare Panels (no peer at all)
            // wrapping presentation-suppressed TextBlocks, so a screen
            // reader heard the dialog name and then only "Close".
            AutomationElement fields = WaitForElement(
                window, "CitationDetailsFields", TimeSpan.FromSeconds(10));
            string[] fieldNames = [];
            Assert.True(
                SpinWait.SpinUntil(
                    () =>
                    {
                        fieldNames = [.. fields
                            .FindAllDescendants(
                                automation.ConditionFactory.ByControlType(
                                    ControlType.ListItem))
                            .Select(item => item.Properties.Name.ValueOrDefault ?? "")
                            .Where(name => name.Length > 0)];
                        return fieldNames.Length > 0;
                    },
                    TimeSpan.FromSeconds(15)),
                "the details sheet exposed no named field elements at all");
            // Verbatim "Label: Value" per field, the mac shape.
            Assert.Contains(
                fieldNames,
                name => name.StartsWith("Title:", StringComparison.Ordinal));
            Assert.Contains(
                fieldNames,
                name => name.Contains("Knuth", StringComparison.OrdinalIgnoreCase));

            AutomationElement detailsClose = WaitForElement(
                window, "CitationDetailsClose", TimeSpan.FromSeconds(10));
            AssertEventuallyFocused(
                detailsClose, "The details sheet did not focus its Close button.");

            // The abstract's TEXT must reach the CONTROL VIEW, which is
            // the tree assistive technology walks. mac keeps the body an
            // accessible child (`children: .contain`); a
            // presentation-suppressed TextBlock is still in the RAW
            // tree, so asserting mere presence proves nothing — that is
            // the same existence-assertion trap that let the field
            // blocker ship. IsControlElement is the discriminating
            // property.
            AutomationElement abstractGroup = WaitForElement(
                window, "CitationDetailsAbstract", TimeSpan.FromSeconds(10));
            abstractGroup.Patterns.ExpandCollapse.Pattern.Expand();
            Assert.True(
                SpinWait.SpinUntil(
                    () => abstractGroup
                        .FindAllDescendants()
                        .Any(node =>
                            (node.Properties.Name.ValueOrDefault ?? "")
                                .Contains("people to read", StringComparison.OrdinalIgnoreCase)
                            && node.Properties.IsControlElement.ValueOrDefault),
                    TimeSpan.FromSeconds(10)),
                "the expanded abstract exposed its text nowhere in the control view");
            // A long abstract is clipped and scrolls. If the scroller
            // is not focusable there is no keyboard route to the rest
            // of it — mouse wheel and drag only.
            // Constrained to the BODY, not the disclosure header: the
            // header's name is "Abstract: <text>", so a Contains match
            // hits the Button and proves nothing about the scroller.
            // Three earlier assertions in this suite matched the wrong
            // node the same way.
            AutomationElement abstractBody = abstractGroup
                .FindAllDescendants()
                .Single(node => string.Equals(
                    node.Properties.Name.ValueOrDefault ?? "",
                    "Programs should be written for people to read.",
                    StringComparison.Ordinal));
            Assert.True(
                abstractBody.Properties.IsKeyboardFocusable.ValueOrDefault,
                "the abstract body has no keyboard route to its clipped remainder");

            // The DOI must be FOLLOWABLE from the keyboard, not just
            // rendered as a link. Round 4 argued it could not be:
            // ListBox sets KeyboardNavigation.TabNavigation=Once, so
            // the reasoning went that Tab leaves the whole list without
            // ever descending into item content. Measured here instead
            // — Tab from the DOI row lands on the Hyperlink — because a
            // Hyperlink is a FrameworkContentElement and does not obey
            // that rule the way a child control would.
            AutomationElement doiRow = fields
                .FindAllDescendants(
                    automation.ConditionFactory.ByControlType(ControlType.ListItem))
                .First(item => (item.Properties.Name.ValueOrDefault ?? "")
                    .StartsWith("DOI", StringComparison.Ordinal));
            doiRow.Focus();
            Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(300));
            PressKey(VirtualKeyShort.TAB);
            AutomationElement focusedAfterTab = automation.FocusedElement();
            Assert.Equal(
                ControlType.Hyperlink,
                focusedAfterTab.Properties.ControlType.ValueOrDefault);
            Assert.Contains(
                "10.1093",
                focusedAfterTab.Properties.Name.ValueOrDefault ?? "",
                StringComparison.Ordinal);

            AssertAxeClean(process, "citation-details-sheet");

            // S11 (W5-2 round 1) reversed the W3 round-4 measurement
            // here: the menu now DISABLES under every modal surface —
            // mac parity, AppKit disables the menu bar while a sheet is
            // up — so the probe that once expanded Edit OVER this sheet
            // asserts the opposite. Jump's enable condition IS the
            // sheet (CitationDetails non-null), and S11 disables the
            // menu exactly then, so the MENU route is gone in every
            // reachable state — the recorded SR-4 consequence, measured
            // below as correctly-greyed; Ctrl+J (which fires with the
            // sheet open) is the working route.
            AutomationElement mainMenu = WaitForElement(
                window, "MainMenu", TimeSpan.FromSeconds(10));
            Assert.False(
                mainMenu.Properties.IsEnabled.ValueOrDefault,
                "the menu is enabled over the citation details sheet — "
                + "S11 disables it under every modal surface.");
            detailsClose.Focus();

            PressKey(VirtualKeyShort.ESCAPE);
            AssertElementDisappears(window, automation, "CitationDetailsSheet");
            AssertEventuallyFocused(
                resolvedRow, "Escape did not return focus to the citation row.");

            // The Jump menu item after S11: its enable condition IS the
            // details sheet (CitationDetails non-null — the Windows
            // twin of mac's expandedCitation), and S11 disables the
            // whole menu exactly while that sheet is up. The item is
            // therefore chord advertisement: present and correctly
            // greyed whenever the menu is reachable, with Ctrl+J
            // (measured below, sheet open) as the working route.
            // Recorded in 29_search_overlay_contracts.md S11.
            Assert.True(
                SpinWait.SpinUntil(
                    () => mainMenu.Properties.IsEnabled.ValueOrDefault,
                    TimeSpan.FromSeconds(10)),
                "the menu did not re-enable after the sheet closed");
            AutomationElement editMenu = mainMenu
                .FindAllChildren(
                    automation.ConditionFactory.ByControlType(ControlType.MenuItem))
                .First(item => (item.Properties.Name.ValueOrDefault ?? "")
                    .Contains("Edit", StringComparison.OrdinalIgnoreCase));
            editMenu.Patterns.ExpandCollapse.Pattern.Expand();
            AutomationElement jumpItem = WaitForElement(
                window, "JumpToBibliographyMenuItem", TimeSpan.FromSeconds(10));
            Assert.False(
                jumpItem.Properties.IsEnabled.ValueOrDefault,
                "Jump to Bibliography is enabled with no citation "
                + "expanded — the command has no key to jump to, and "
                + "invoking it here would be the W4-5 disabled-state "
                + "defect reborn.");
            editMenu.Patterns.ExpandCollapse.Pattern.Collapse();
            Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(300));

            // ---- Ctrl+J: the landing, not just the announcement -----
            // The docstring claimed this for a version of the test that
            // never pressed Ctrl+J, so the interaction with three known
            // false-positive paths had no end-to-end cover at all.
            resolvedRow.Focus();
            PressKey(VirtualKeyShort.RETURN);
            WaitForElement(window, "CitationDetailsSheet", TimeSpan.FromSeconds(10));
            PressChord(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_J);
            // The focus-trapped sheet must get out of the way (mac
            // clears expandedCitation) ...
            AssertElementDisappears(window, automation, "CitationDetailsSheet");
            // ... and the entries grid must actually hold the key the
            // announcement names, on the entries segment, in a visible
            // pane. If any of those is false the user is told they
            // arrived somewhere they never went.
            AutomationElement jumped = WaitForElement(
                window, "BibliographyEntries", TimeSpan.FromSeconds(15));
            Assert.True(
                SpinWait.SpinUntil(
                    () => jumped
                        .FindAllDescendants(
                            automation.ConditionFactory.ByControlType(ControlType.Custom))
                        .Concat(jumped.FindAllDescendants(
                            automation.ConditionFactory.ByControlType(ControlType.DataItem)))
                        .Any(row => (row.Properties.Name.ValueOrDefault ?? "")
                            .Contains("Knuth", StringComparison.OrdinalIgnoreCase)),
                    TimeSpan.FromSeconds(15)),
                "Ctrl+J announced a jump but the entry never appeared in the bound grid");

            // ---- Ctrl+J on a MISS must still land somewhere --------
            // The jump closes the focus-trapped sheet before resolving,
            // so returning early on a miss left focus on the window
            // root: the user pressed a key, heard "Searching
            // bibliography for: X." and was nowhere, with Tab
            // restarting at the menu bar. Measured before the fix as
            // FocusedElement = Slate.MainWindow.
            SelectLeaf("Citations");
            AutomationElement citationsAgain = WaitForElement(
                window, "PanelCitationsList", TimeSpan.FromSeconds(15));
            AutomationElement ghostRow = citationsAgain
                .FindAllDescendants(
                    automation.ConditionFactory.ByControlType(ControlType.ListItem))
                .First(item => (item.Properties.Name.ValueOrDefault ?? "")
                    .Contains("Unresolved", StringComparison.OrdinalIgnoreCase));
            ghostRow.Patterns.SelectionItem.Pattern.Select();
            ghostRow.Focus();
            PressKey(VirtualKeyShort.RETURN);
            WaitForElement(window, "CitationDetailsSheet", TimeSpan.FromSeconds(10));
            PressChord(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_J);
            AssertElementDisappears(window, automation, "CitationDetailsSheet");
            AutomationElement afterMiss = WaitForElement(
                window, "BibliographySearch", TimeSpan.FromSeconds(10));
            AssertEventuallyFocused(
                afterMiss,
                "A Ctrl+J miss stranded focus instead of landing in the bibliography.");
            // The jump filtered the leaf to a key with no entry, which
            // is correct but leaves the grid empty for what follows.
            // Clear it the way a user would.
            PressChord(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
            PressKey(VirtualKeyShort.DELETE);
            Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(300));

            // ---- Ctrl+Shift+J: the summary sheet ------------------
            PressChord(VirtualKeyShort.SHIFT, VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_J);
            WaitForElement(window, "CitationSummarySheet", TimeSpan.FromSeconds(10));
            AssertAxeClean(process, "citation-summary-sheet");
            WaitForElement(window, "CitationSummaryDismiss", TimeSpan.FromSeconds(10))
                .Patterns.Invoke.Pattern.Invoke();
            AssertElementDisappears(window, automation, "CitationSummarySheet");

            // ---- The bibliography leaf: BOTH segments are grids ----
            SelectLeaf("Bibliography");
            AutomationElement entriesGrid = WaitForElement(
                window, "BibliographyEntries", TimeSpan.FromSeconds(15));
            Assert.True(entriesGrid.Patterns.Grid.IsSupported, "Grid pattern missing on entries");
            Assert.True(
                SpinWait.SpinUntil(
                    () => entriesGrid
                        .FindAllDescendants(
                            automation.ConditionFactory.ByControlType(ControlType.HeaderItem))
                        .Select(item => item.Properties.Name.ValueOrDefault ?? "")
                        .Where(name => name.Length > 0)
                        .ToHashSet()
                        .IsSupersetOf(["Title", "Authors", "Year", "Journal", "Key"]),
                    TimeSpan.FromSeconds(15)),
                "the entries grid never exposed its five column headers");

            WaitForElement(window, "BibliographySegmentUnresolved", TimeSpan.FromSeconds(10))
                .Patterns.SelectionItem.Pattern.Select();
            AutomationElement unresolvedGrid = WaitForElement(
                window, "BibliographyUnresolved", TimeSpan.FromSeconds(15));
            Assert.True(
                unresolvedGrid.Patterns.Grid.IsSupported,
                "the unresolved segment did not render as a grid (D-6)");
            Assert.True(
                SpinWait.SpinUntil(
                    () => unresolvedGrid
                        .FindAllDescendants(
                            automation.ConditionFactory.ByControlType(ControlType.HeaderItem))
                        .Select(item => item.Properties.Name.ValueOrDefault ?? "")
                        .Where(name => name.Length > 0)
                        .ToHashSet()
                        .IsSupersetOf(["Key", "File"]),
                    TimeSpan.FromSeconds(15)),
                "the unresolved grid never exposed its Key/File headers");
            AssertAxeClean(process, "bibliography-leaf");

            // ---- The files-citing sheet ---------------------------
            // Never opened by any earlier version of this gate, so its
            // "0 files" naming defect was found by reading rather than
            // by running. Reached the way a user reaches it: the row
            // action on a bibliography entry.
            WaitForElement(window, "BibliographySegmentEntries", TimeSpan.FromSeconds(10))
                .Patterns.SelectionItem.Pattern.Select();
            AutomationElement entriesAgain = WaitForElement(
                window, "BibliographyEntries", TimeSpan.FromSeconds(15));
            entriesAgain.Focus();
            AutomationElement entryCell = entriesAgain
                .FindAllDescendants(
                    automation.ConditionFactory.ByControlType(ControlType.Custom))
                .FirstOrDefault(cell => (cell.Properties.Name.ValueOrDefault ?? "")
                    .Contains("Knuth", StringComparison.OrdinalIgnoreCase))
                ?? throw new Xunit.Sdk.XunitException(
                    "no bibliography cell to open the row menu from");
            entryCell.Focus();
            PressChord(VirtualKeyShort.SHIFT, VirtualKeyShort.F10);

            AutomationElement? action = null;
            Assert.True(
                SpinWait.SpinUntil(
                    () =>
                    {
                        action = window
                            .FindAllDescendants(
                                automation.ConditionFactory.ByControlType(
                                    ControlType.MenuItem))
                            .FirstOrDefault(item =>
                                (item.Properties.Name.ValueOrDefault ?? "")
                                    .StartsWith(
                                        "Show files citing", StringComparison.Ordinal));
                        return action is not null;
                    },
                    TimeSpan.FromSeconds(10)),
                "the entries grid did not offer the files-citing row action");
            action!.Patterns.Invoke.Pattern.Invoke();

            AutomationElement filesCiting = WaitForElement(
                window, "FilesCitingSheet", TimeSpan.FromSeconds(10));
            // The sheet settles on the real count. NOTE: this does NOT
            // pin the "0 files at appear" defect — by the time UIA can
            // poll, the load has landed, and this assertion passes
            // against the broken version too (verified). The at-appear
            // naming is pinned in FilesCitingNamingTests, where the
            // publish can be held. What this covers is that the sheet
            // opens from the row action at all, reaches a truthful
            // name, is axe-clean, and closes — none of which had any
            // end-to-end cover before.
            Assert.True(
                SpinWait.SpinUntil(
                    () => (filesCiting.Properties.Name.ValueOrDefault ?? "")
                        .Contains("1 file", StringComparison.OrdinalIgnoreCase),
                    TimeSpan.FromSeconds(15)),
                "the files-citing sheet never named the real count; last name was "
                    + $"\"{filesCiting.Properties.Name.ValueOrDefault}\"");
            AssertAxeClean(process, "files-citing-sheet");

            // ESCAPE, not Invoke — and assert where focus goes.
            // Contract 11 says Escape returns focus exactly to the row
            // that opened the sheet. Round 4 argued this path could not
            // satisfy it, on the grounds that the row action captures
            // Keyboard.FocusedElement while the context menu is open,
            // so the token would be a MenuItem that is gone by the time
            // the sheet closes, falling through to a collapsed control.
            // Measured instead: focus returns to the originating
            // bibliography CELL. Closing by Invoke, as this journey did
            // before, asserted nothing about any of it.
            PressKey(VirtualKeyShort.ESCAPE);
            AssertElementDisappears(window, automation, "FilesCitingSheet");
            Assert.True(
                SpinWait.SpinUntil(
                    () => (automation.FocusedElement().Properties.Name.ValueOrDefault ?? "")
                        .Contains("Knuth", StringComparison.OrdinalIgnoreCase),
                    TimeSpan.FromSeconds(10)),
                "Escape from the files-citing sheet did not return focus to the "
                    + "bibliography row that opened it; focus was on "
                    + $"\"{automation.FocusedElement().Properties.Name.ValueOrDefault}\"");
        }
        finally
        {
            if (process is not null && !process.HasExited)
            {
                process.CloseMainWindow();
                if (!process.WaitForExit(5_000))
                {
                    process.Kill(entireProcessTree: true);
                }
            }

            try
            {
                Directory.Delete(testRoot, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    /// <summary>
    /// The committed demo-vault CSL style, copied into this project's
    /// output by a linked Content item.
    ///
    /// It is read from the OUTPUT DIRECTORY, never by walking up to a
    /// repo root: this gate downloads built binaries and runs with no
    /// checkout, so the repo does not exist at run time. Walking up
    /// passes locally and fails only on CI, which is how the first
    /// version of this shipped.
    /// </summary>
    private static string CitationStyleFixture()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "fixtures", "ieee.csl");
        Assert.True(
            File.Exists(path),
            $"The CSL fixture is missing at {path}. It is a linked Content " +
            "item in SlateWindows.AccessibilityTests.csproj and must be " +
            "copied to the output directory.");
        return path;
    }

    /// <summary>
    /// W4-5 (#737): the DEGRADED vault. Contract 5 says the leaf's
    /// notice region is the surface for a failed load — but every
    /// fixture in this suite loads cleanly, so the gate had never once
    /// seen that region populated, never axe-scanned it, and never
    /// checked that a user could reach it.
    ///
    /// §2.6 forbids SPEAKING bibliography copy at vault open, so the
    /// reason is deliberately not announced. That makes reachability
    /// the whole contract: if the notice is not in the tab order, a
    /// screen-reader user is told "0 entries" and has no route to why.
    /// </summary>
    [Fact]
    [Trait("gate", "W-C")]
    public void CitationSurfaces_ADegradedVaultSurfacesTheReasonReachably()
    {
        string testRoot = Path.Combine(
            Path.GetTempPath(),
            $"slate-citations-degraded-{Guid.NewGuid():N}");
        string vaultRoot = Path.Combine(testRoot, "Degraded Vault");
        string logDirectory = Path.Combine(testRoot, "logs");
        Directory.CreateDirectory(vaultRoot);
        File.WriteAllText(
            Path.Combine(vaultRoot, "cited.md"),
            "# Cited\n\nA citation [@knuth1984].\n");
        // Configured, and pointing at a file that is not there. Core's
        // set_bibliography_sources is all-or-nothing, so the seed fails
        // and the D-13 refusal takes over.
        File.WriteAllText(
            Path.Combine(vaultRoot, "slate.json"),
            "{\"citations\":{\"bibliography\":\"nowhere.bib\",\"cite_style\":\"ieee\"}}");

        Process? process = null;
        try
        {
            var startInfo = new ProcessStartInfo(SlateWindowsExe())
            {
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
            };
            startInfo.ArgumentList.Add(vaultRoot);
            startInfo.Environment["SLATE_CENSUS_INSTANCE_ID"] =
                $"slate-degraded-{Guid.NewGuid():N}";
            startInfo.Environment["SLATE_LOG_DIR"] = logDirectory;
            process = Process.Start(startInfo)
                ?? throw new Xunit.Sdk.XunitException("SlateWindows.exe did not start.");

            if (!HasInteractiveDesktop(process, "accessibility"))
            {
                return;
            }

            using var automation = new UIA3Automation();
            Window window = WaitForMainWindow(
                process,
                automation,
                Path.Combine(logDirectory, "slate-windows.log"),
                TimeSpan.FromSeconds(30));

            AutomationElement leaves = WaitForElement(
                window, "RightPaneLeaves", TimeSpan.FromSeconds(30));
            AutomationElement? bibliographyEntry = null;
            Assert.True(
                SpinWait.SpinUntil(
                    () =>
                    {
                        bibliographyEntry = leaves
                            .FindAllDescendants(
                                automation.ConditionFactory.ByControlType(ControlType.ListItem))
                            .FirstOrDefault(item =>
                                (item.Properties.Name.ValueOrDefault ?? "") == "Bibliography");
                        return bibliographyEntry is not null;
                    },
                    TimeSpan.FromSeconds(15)),
                "no Bibliography rail entry.");
            bibliographyEntry!.Patterns.SelectionItem.Pattern.Select();

            // The reason is on screen, verbatim, naming the source.
            AutomationElement? notice = null;
            Assert.True(
                SpinWait.SpinUntil(
                    () =>
                    {
                        notice = window
                            .FindAllDescendants()
                            .FirstOrDefault(node =>
                                (node.Properties.Name.ValueOrDefault ?? "")
                                    .Contains("nowhere.bib", StringComparison.OrdinalIgnoreCase)
                                && node.Properties.IsControlElement.ValueOrDefault);
                        return notice is not null;
                    },
                    TimeSpan.FromSeconds(20)),
                "the failed load surfaced no notice naming the source in the control view");

            // And a keyboard user can actually GET to it. Degradation
            // copy nobody can reach satisfies contract 5 on screen only.
            //
            // ANY element carrying the reason will do — the text sits
            // inside an ItemsControl, so it appears twice: once as the
            // non-focusable item container and once as the TextBlock
            // itself. Asking the first match is a coin flip on tree
            // order, which is how the first version of this assertion
            // failed against working code.
            Assert.True(
                window.FindAllDescendants().Any(node =>
                    (node.Properties.Name.ValueOrDefault ?? "")
                        .Contains("nowhere.bib", StringComparison.OrdinalIgnoreCase)
                    && node.Properties.IsControlElement.ValueOrDefault
                    && node.Properties.IsKeyboardFocusable.ValueOrDefault),
                "no element carrying the load-failure reason is reachable from "
                    + "the keyboard");

            // The refusal must not ALSO claim something false: sources
            // are configured here, so the no-sources sentence must not
            // appear beside the failure.
            Assert.DoesNotContain(
                window.FindAllDescendants()
                    .Select(node => node.Properties.Name.ValueOrDefault ?? ""),
                name => name.Contains(
                    "No bibliography sources configured", StringComparison.OrdinalIgnoreCase));

            AssertAxeClean(process, "bibliography-degraded");
        }
        finally
        {
            if (process is not null && !process.HasExited)
            {
                process.CloseMainWindow();
                if (!process.WaitForExit(5_000))
                {
                    process.Kill(entireProcessTree: true);
                }
            }

            try
            {
                Directory.Delete(testRoot, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    /// <summary>
    /// W4-6 (#738) §W-C journey, per-surface: the .base tab (grid
    /// identity, §8.7 headers, quick filter via the grid-scoped
    /// Ctrl+F with Escape clearing, and a REAL F2 cell edit committed
    /// through the coordinator into the file), the Queries leaf, the
    /// DOCK leaf (dock a base file from the leaf, distinct
    /// BasesDockGrid id), the builder overlay (dialog focus-on-open,
    /// save-as-saved-query, Escape close), the saved-query TAB, the
    /// dashboard EDITOR overlay (focus-on-open, section authoring),
    /// and the dashboard TAB — each surface axe-scanned. Every
    /// element asserted here is one the contracts name; the
    /// degraded-vault twin lives with the unit facts (banner
    /// wording), because a broken .base still opens a healthy shell.
    /// </summary>
    [Fact]
    [Trait("gate", "W-C")]
    public void BasesSurfaces_GridBuilderAndLeaves_AreClean()
    {
        string testRoot = Path.Combine(
            Path.GetTempPath(), $"slate-bases-surfaces-{Guid.NewGuid():N}");
        string vaultRoot = Path.Combine(testRoot, "Bases Vault");
        string logDirectory = Path.Combine(testRoot, "logs");
        Directory.CreateDirectory(vaultRoot);
        File.WriteAllText(
            Path.Combine(vaultRoot, "alpha.md"),
            "---\nstatus: todo\n---\n\n# Alpha\n\nBody.\n");
        File.WriteAllText(
            Path.Combine(vaultRoot, "beta.md"),
            "---\nstatus: done\n---\n\n# Beta\n\nBody.\n");
        File.WriteAllText(
            Path.Combine(vaultRoot, "Notes.base"),
            "filters: 'file.ext == \"md\"'\n" +
            "views:\n" +
            "  - type: table\n" +
            "    name: Main\n" +
            "    order:\n" +
            "      - file.name\n" +
            "      - note.status\n");

        Process? process = null;
        try
        {
            var startInfo = new ProcessStartInfo(SlateWindowsExe())
            {
                UseShellExecute = false,
            };
            startInfo.ArgumentList.Add(vaultRoot);
            startInfo.Environment["SLATE_CENSUS_INSTANCE_ID"] =
                $"slate-bases-surfaces-{Guid.NewGuid():N}";
            startInfo.Environment["SLATE_LOG_DIR"] = logDirectory;
            process = Process.Start(startInfo)
                ?? throw new Xunit.Sdk.XunitException("SlateWindows.exe did not start.");

            if (!HasInteractiveDesktop(process, "Bases"))
            {
                return;
            }

            using var automation = new UIA3Automation();
            Window window = WaitForMainWindow(
                process,
                automation,
                Path.Combine(logDirectory, "slate-windows.log"),
                TimeSpan.FromSeconds(30));
            // Keyboard-driven journey: the launch-time foreground grab
            // is best-effort under the Windows foreground lock (another
            // app's recent input denies it), and every chord below
            // requires the window to actually be foreground.
            window.SetForeground();
            window.Focus();

            // Open the base from the files tree.
            AutomationElement filesTree = WaitForElement(
                window, "FilesTree", TimeSpan.FromSeconds(30));
            AutomationElement baseItem = filesTree
                .FindAllDescendants(
                    automation.ConditionFactory.ByControlType(ControlType.TreeItem))
                .FirstOrDefault(item =>
                    item.Name.StartsWith("Notes", StringComparison.OrdinalIgnoreCase))
                ?? throw new Xunit.Sdk.XunitException("The Notes.base TreeItem is absent.");
            baseItem.Patterns.SelectionItem.Pattern.Select();

            // The tab surface: grid identity + §8.7 headers.
            AutomationElement grid = WaitForElement(
                window, "BaseTabGrid", TimeSpan.FromSeconds(15));
            Assert.True(grid.Patterns.Grid.IsSupported, "Grid pattern missing");
            Assert.True(
                SpinWait.SpinUntil(
                    () => grid
                        .FindAllDescendants(automation.ConditionFactory.ByControlType(
                            ControlType.HeaderItem))
                        .Select(header => header.Name)
                        .ToHashSet(StringComparer.Ordinal)
                        .IsSupersetOf(["file.name", "note.status"]),
                    TimeSpan.FromSeconds(15)),
                "the base grid's core-labelled headers never materialized");
            AssertAxeClean(process, "bases-tab");

            // Quick filter: grid-scoped Ctrl+F focuses the transient
            // field; typing narrows the count readout.
            AutomationElement firstCell = grid
                .FindAllDescendants(
                    automation.ConditionFactory.ByControlType(ControlType.Custom))
                .FirstOrDefault(cell =>
                    cell.Name.Contains("alpha", StringComparison.OrdinalIgnoreCase))
                ?? throw new Xunit.Sdk.XunitException("No alpha cell in the base grid.");
            // Foreground re-asserted at the keyboard boundary: another
            // app can re-take foreground between launch and this point
            // (measured locally: an unrelated always-on-top window
            // held focus and every chord went to it). The synthetic
            // Alt tap satisfies the Windows foreground lock — plain
            // SetForegroundWindow is DENIED to a background process.
            Keyboard.Press(VirtualKeyShort.ALT);
            window.SetForeground();
            Keyboard.Release(VirtualKeyShort.ALT);
            window.Focus();
            firstCell.Focus();
            PressChord(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_F);
            AutomationElement quickFilter = WaitForElement(
                window, "BaseQuickFilter", TimeSpan.FromSeconds(10));
            bool filterFocused = SpinWait.SpinUntil(
                () => quickFilter.Properties.HasKeyboardFocus.ValueOrDefault,
                TimeSpan.FromSeconds(10));
            if (!filterFocused)
            {
                AutomationElement? focused = automation.FocusedElement();
                throw new Xunit.Sdk.XunitException(
                    "Ctrl+F did not focus the quick filter; focus is on "
                    + $"'{focused?.Properties.AutomationId.ValueOrDefault}'"
                    + $"/'{focused?.Properties.Name.ValueOrDefault}'"
                    + $" ({focused?.Properties.ControlType.ValueOrDefault})"
                    + $" in '{focused?.Properties.ProcessId.ValueOrDefault}'"
                    + $" vs app {process.Id}");
            }
            Keyboard.Type("alpha");
            AutomationElement countReadout = WaitForElement(
                window, "BaseCountReadout", TimeSpan.FromSeconds(10));
            Assert.True(
                SpinWait.SpinUntil(
                    () => countReadout.Name.Contains(
                        "1 of", StringComparison.Ordinal),
                    TimeSpan.FromSeconds(10)),
                $"the filtered count never arrived; readout: {countReadout.Name}");
            // Escape clears and returns focus to the content.
            Keyboard.Press(VirtualKeyShort.ESCAPE);
            Keyboard.Release(VirtualKeyShort.ESCAPE);
            Assert.True(
                SpinWait.SpinUntil(
                    () => !countReadout.Name.Contains(
                        "1 of", StringComparison.Ordinal),
                    TimeSpan.FromSeconds(10)),
                "Escape did not clear the transient filter");

            // The EDIT seam under real UIA (contract C7/C8): F2 on the
            // editable status cell opens the seeded editor; Enter
            // commits through the coordinator and the write lands in
            // the FILE.
            // CONTAINS, not equality (the alpha-cell finder's rule):
            // cell names are decorated beyond the raw value. "todo"
            // appears only in alpha's status cell in this fixture.
            AutomationElement statusCell = grid
                .FindAllDescendants(
                    automation.ConditionFactory.ByControlType(ControlType.Custom))
                .FirstOrDefault(cell =>
                    cell.Name.Contains("todo", StringComparison.OrdinalIgnoreCase))
                ?? throw new Xunit.Sdk.XunitException(
                    "No editable status cell in the base grid.");
            statusCell.Focus();
            Keyboard.Press(VirtualKeyShort.F2);
            Keyboard.Release(VirtualKeyShort.F2);
            AutomationElement? cellEditor = null;
            Assert.True(
                SpinWait.SpinUntil(
                    () =>
                    {
                        cellEditor = window.FindFirstDescendant(
                            automation.ConditionFactory.ByName("note.status edit"));
                        return cellEditor is not null;
                    },
                    TimeSpan.FromSeconds(10)),
                "F2 did not open the cell editor");
            Assert.True(
                SpinWait.SpinUntil(
                    () => cellEditor!.Properties.HasKeyboardFocus.ValueOrDefault,
                    TimeSpan.FromSeconds(10)),
                "the cell editor did not take focus");
            Keyboard.Type("todo-edited");
            Keyboard.Press(VirtualKeyShort.RETURN);
            Keyboard.Release(VirtualKeyShort.RETURN);
            Assert.True(
                SpinWait.SpinUntil(
                    () =>
                    {
                        // Tolerant of the app's atomic temp+rename
                        // write racing this poll (the CI sharing
                        // violation this journey tripped on PR #1124).
                        try
                        {
                            return File.ReadAllText(
                                    Path.Combine(vaultRoot, "alpha.md"))
                                .Contains(
                                    "status: todo-edited",
                                    StringComparison.Ordinal);
                        }
                        catch (IOException)
                        {
                            return false;
                        }
                    },
                    TimeSpan.FromSeconds(15)),
                "the committed cell edit never reached the file");
            // The committed edit's vault event re-executes every Bases
            // surface (C9): the tab grid re-binds BEHIND the right
            // pane while the journey moves on, and an axe snapshot
            // mid-bind catches ON-SCREEN DataGridRows whose peers
            // still report null bounding rectangles (the axe rule the
            // CI runner tripped; 2-core timing — the rebind settles
            // long before this point locally). Wait the transient out
            // before the next scan; steady state converges instantly.
            Assert.True(
                SpinWait.SpinUntil(
                    () =>
                    {
                        try
                        {
                            return window
                                .FindAllDescendants(automation.ConditionFactory
                                    .ByControlType(ControlType.DataItem))
                                .All(item =>
                                    item.Properties.IsOffscreen.ValueOrDefault
                                    || !item.Properties.BoundingRectangle
                                        .ValueOrDefault.IsEmpty);
                        }
                        catch (System.Runtime.InteropServices.COMException)
                        {
                            return false;
                        }
                    },
                    TimeSpan.FromSeconds(15)),
                "the base grids never settled after the committed cell edit");

            // The Queries leaf renders and scans clean.
            AutomationElement leaves = WaitForElement(
                window, "RightPaneLeaves", TimeSpan.FromSeconds(10));
            AutomationElement? queriesLeaf = null;
            Assert.True(
                SpinWait.SpinUntil(
                    () =>
                    {
                        queriesLeaf = leaves
                            .FindAllDescendants(automation.ConditionFactory
                                .ByControlType(ControlType.ListItem))
                            .FirstOrDefault(item => string.Equals(
                                item.Name, "Queries", StringComparison.Ordinal));
                        return queriesLeaf is not null;
                    },
                    TimeSpan.FromSeconds(10)),
                "the Queries leaf option never appeared");
            queriesLeaf!.Patterns.SelectionItem.Pattern.Select();
            // The leaf BODY is a WPF Panel — no automation peer, no id
            // in the tree (the W4-5 lesson); presence is asserted via
            // its peered children.
            _ = WaitForElement(window, "QueriesRefresh", TimeSpan.FromSeconds(10));
            AssertAxeClean(process, "bases-queries-leaf");

            // Dock a base file from the leaf: the dock leaf reveals
            // with its OWN read-only surface (BasesDockGrid — the
            // D-12 distinct-id rule) and scans clean. This is the
            // basesDock LEAF_DELIVERED evidence.
            AutomationElement baseFilesList = WaitForElement(
                window, "QueriesBaseFilesList", TimeSpan.FromSeconds(10));
            AutomationElement baseFileItem = baseFilesList
                .FindAllDescendants(
                    automation.ConditionFactory.ByControlType(ControlType.ListItem))
                .FirstOrDefault()
                ?? throw new Xunit.Sdk.XunitException(
                    "No base files in the queries leaf.");
            baseFileItem.Patterns.SelectionItem.Pattern.Select();
            AutomationElement dockButton = WaitForElement(
                window, "QueriesDockBaseFile", TimeSpan.FromSeconds(10));
            dockButton.Patterns.Invoke.Pattern.Invoke();
            _ = WaitForElement(window, "BasesDockGrid", TimeSpan.FromSeconds(15));
            AssertAxeClean(process, "bases-dock-leaf");

            // The builder overlay opens as a DIALOG with focus moved
            // inside, scans clean, and Escape closes it (the W4-5
            // overlay lifecycle).
            AutomationElement newQuery = WaitForMenuItem(
                window, "BaseMenu", "BasesNewQueryMenuItem",
                TimeSpan.FromSeconds(10));
            newQuery.Patterns.Invoke.Pattern.Invoke();
            AutomationElement builderSheet = WaitForElement(
                window, "BaseQueryBuilderSheet", TimeSpan.FromSeconds(10));
            Assert.True(
                SpinWait.SpinUntil(
                    () => builderSheet
                        .FindAllDescendants()
                        .Any(descendant =>
                            descendant.Properties.HasKeyboardFocus.ValueOrDefault),
                    TimeSpan.FromSeconds(10)),
                "the builder overlay opened without moving focus into the dialog");
            AssertAxeClean(process, "bases-builder-overlay");

            // Save the query from the overlay — the saved-query TAB
            // surface and the dashboard surfaces ride on it below.
            // The name is SET via the Value pattern and verified: an
            // unasserted focus + keyboard race left the box empty on
            // CI, the save refused (name needed), and the collapsed
            // empty list downstream read as a missing element. The
            // keyboard PATH itself is already proven by the F2 and
            // quick-filter legs.
            AutomationElement saveNameBox = WaitForElement(
                window, "BuilderSaveNameBox", TimeSpan.FromSeconds(10));
            saveNameBox.Patterns.Value.Pattern.SetValue("Journey query");
            Assert.True(
                SpinWait.SpinUntil(
                    () => string.Equals(
                        saveNameBox.Patterns.Value.Pattern.Value.ValueOrDefault,
                        "Journey query",
                        StringComparison.Ordinal),
                    TimeSpan.FromSeconds(5)),
                "the saved-query name never landed in the box");
            AutomationElement saveAsQuery = WaitForElement(
                window, "BuilderSaveAsSavedQuery", TimeSpan.FromSeconds(10));
            saveAsQuery.Patterns.Invoke.Pattern.Invoke();
            // A visible SaveError means the builder REFUSED — surface
            // the reason now, before Escape destroys the element.
            AutomationElement? builderError = null;
            _ = SpinWait.SpinUntil(
                () => (builderError = window.FindFirstDescendant(
                    automation.ConditionFactory.ByAutomationId("BuilderSaveError")))
                    is not null,
                TimeSpan.FromSeconds(2));
            if (builderError is not null)
            {
                throw new Xunit.Sdk.XunitException(
                    $"the builder refused the save: '{builderError.Name}'");
            }

            Keyboard.Press(VirtualKeyShort.ESCAPE);
            Keyboard.Release(VirtualKeyShort.ESCAPE);
            Assert.True(
                SpinWait.SpinUntil(
                    () => window.FindFirstDescendant(
                        automation.ConditionFactory.ByAutomationId(
                            "BuilderCombinator")) is null,
                    TimeSpan.FromSeconds(10)),
                "Escape did not close the builder overlay");

            // Run the saved query from the leaf: the saved-query TAB
            // carries the full Bases surface (contract C12) and scans
            // clean. The list collapses when EMPTY, so its absence is
            // ambiguous — the failure message distinguishes "leaf
            // never revealed" from "save never landed".
            queriesLeaf!.Patterns.SelectionItem.Pattern.Select();
            AutomationElement? savedList = null;
            if (!SpinWait.SpinUntil(
                () =>
                {
                    savedList = window.FindFirstDescendant(
                        automation.ConditionFactory.ByAutomationId(
                            "QueriesSavedList"));
                    return savedList is not null;
                },
                TimeSpan.FromSeconds(10)))
            {
                bool leafRevealed = window.FindFirstDescendant(
                    automation.ConditionFactory.ByAutomationId("QueriesRefresh"))
                    is not null;
                bool emptyState = window.FindFirstDescendant(
                    automation.ConditionFactory.ByName("No saved queries"))
                    is not null;
                bool overlayStillOpen = window.FindFirstDescendant(
                    automation.ConditionFactory.ByAutomationId(
                        "BuilderSaveAsSavedQuery")) is not null;
                string appLog = ReadSharedLog(
                    Path.Combine(logDirectory, "slate-windows.log"));
                string logTail = appLog.Length > 800 ? appLog[^800..] : appLog;
                throw new Xunit.Sdk.XunitException(
                    "QueriesSavedList did not become available; "
                    + $"leafRevealed={leafRevealed}, emptyState={emptyState}, "
                    + $"builderStillOpen={overlayStillOpen}; app log tail: {logTail}");
            }
            AutomationElement? savedItem = null;
            Assert.True(
                SpinWait.SpinUntil(
                    () =>
                    {
                        savedItem = savedList!
                            .FindAllDescendants(automation.ConditionFactory
                                .ByControlType(ControlType.ListItem))
                            .FirstOrDefault(item => item.Name.Contains(
                                "Journey query", StringComparison.Ordinal));
                        return savedItem is not null;
                    },
                    TimeSpan.FromSeconds(10)),
                "the saved query never appeared in the queries leaf");
            savedItem!.Patterns.SelectionItem.Pattern.Select();
            AutomationElement runButton = WaitForElement(
                window, "QueriesRun", TimeSpan.FromSeconds(10));
            runButton.Patterns.Invoke.Pattern.Invoke();
            Assert.True(
                SpinWait.SpinUntil(
                    () => window.FindFirstDescendant(
                        automation.ConditionFactory.ByName("Base Journey query"))
                        is not null,
                    TimeSpan.FromSeconds(15)),
                "the saved-query tab surface never appeared");
            AssertAxeClean(process, "bases-saved-query-tab");

            // The dashboard editor overlay and the dashboard TAB
            // (contract C12): create one section over the saved query,
            // save, open, and scan both surfaces.
            queriesLeaf!.Patterns.SelectionItem.Pattern.Select();
            AutomationElement newDashboard = WaitForElement(
                window, "QueriesNewDashboard", TimeSpan.FromSeconds(10));
            newDashboard.Patterns.Invoke.Pattern.Invoke();
            AutomationElement editorName = WaitForElement(
                window, "DashboardEditorName", TimeSpan.FromSeconds(10));
            Assert.True(
                SpinWait.SpinUntil(
                    () => editorName.Properties.HasKeyboardFocus.ValueOrDefault,
                    TimeSpan.FromSeconds(10)),
                "the dashboard editor opened without focusing the name field");
            // Value pattern for the same determinism as the builder's
            // save-name entry (the focus assert above still proves the
            // dialog's focus-on-open contract).
            editorName.Patterns.Value.Pattern.SetValue("Journey board");
            Assert.True(
                SpinWait.SpinUntil(
                    () => string.Equals(
                        editorName.Patterns.Value.Pattern.Value.ValueOrDefault,
                        "Journey board",
                        StringComparison.Ordinal),
                    TimeSpan.FromSeconds(5)),
                "the dashboard name never landed in the box");
            AutomationElement queryPicker = WaitForElement(
                window, "DashboardEditorQueryPicker", TimeSpan.FromSeconds(10));
            queryPicker.Patterns.ExpandCollapse.Pattern.Expand();
            AutomationElement? pickerItem = null;
            Assert.True(
                SpinWait.SpinUntil(
                    () =>
                    {
                        pickerItem = queryPicker
                            .FindAllDescendants(automation.ConditionFactory
                                .ByControlType(ControlType.ListItem))
                            .FirstOrDefault(item => item.Name.Contains(
                                "Journey query", StringComparison.Ordinal));
                        return pickerItem is not null;
                    },
                    TimeSpan.FromSeconds(10)),
                "the saved query never appeared in the section picker");
            pickerItem!.Patterns.SelectionItem.Pattern.Select();
            WaitForElement(window, "DashboardEditorAddSection", TimeSpan.FromSeconds(10))
                .Patterns.Invoke.Pattern.Invoke();
            AssertAxeClean(process, "bases-dashboard-editor");
            WaitForElement(window, "DashboardEditorSave", TimeSpan.FromSeconds(10))
                .Patterns.Invoke.Pattern.Invoke();
            AutomationElement? dashboardItem = null;
            Assert.True(
                SpinWait.SpinUntil(
                    () =>
                    {
                        dashboardItem = window
                            .FindFirstDescendant(automation.ConditionFactory
                                .ByAutomationId("QueriesDashboardsList"))
                            ?.FindAllDescendants(automation.ConditionFactory
                                .ByControlType(ControlType.ListItem))
                            .FirstOrDefault(item => item.Name.Contains(
                                "Journey board", StringComparison.Ordinal));
                        return dashboardItem is not null;
                    },
                    TimeSpan.FromSeconds(10)),
                "the dashboard never appeared in the queries leaf");
            dashboardItem!.Patterns.SelectionItem.Pattern.Select();
            WaitForElement(window, "QueriesOpenDashboard", TimeSpan.FromSeconds(10))
                .Patterns.Invoke.Pattern.Invoke();
            _ = WaitForElement(window, "DashboardSurface", TimeSpan.FromSeconds(15));
            _ = WaitForElement(window, "DashboardSection0Grid", TimeSpan.FromSeconds(15));
            AssertAxeClean(process, "bases-dashboard-tab");
        }
        finally
        {
            if (process is not null && !process.HasExited)
            {
                process.CloseMainWindow();
                if (!process.WaitForExit(5_000))
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            try
            {
                Directory.Delete(testRoot, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    /// <summary>
    /// W4-7 (#739) §W-C journey, per-surface: the history leaf via the
    /// slate.history.showPanel menu route, real in-app saves creating
    /// version rows, the day-group + markers toggle, a core
    /// StructuredDiff walkthrough via the per-row Compare, the Deleted
    /// segment with its standing footer, and a REAL restore through
    /// the production confirmation dialog landing on disk — each
    /// surface axe-scanned. Honors the recorded journey traps
    /// (foreground re-assert, async Invoke settle, panel-no-peer).
    /// </summary>
    [Fact]
    [Trait("gate", "W-C")]
    public void HistorySurfaces_LeafDiffAndRestore_AreClean()
    {
        string testRoot = Path.Combine(
            Path.GetTempPath(), $"slate-history-surfaces-{Guid.NewGuid():N}");
        string vaultRoot = Path.Combine(testRoot, "History Vault");
        string logDirectory = Path.Combine(testRoot, "logs");
        Directory.CreateDirectory(vaultRoot);
        File.WriteAllText(
            Path.Combine(vaultRoot, "alpha.md"),
            "# Alpha\n\nOriginal body.\n");

        Process? process = null;
        try
        {
            var startInfo = new ProcessStartInfo(SlateWindowsExe())
            {
                UseShellExecute = false,
            };
            startInfo.ArgumentList.Add(vaultRoot);
            startInfo.Environment["SLATE_CENSUS_INSTANCE_ID"] =
                $"slate-history-surfaces-{Guid.NewGuid():N}";
            startInfo.Environment["SLATE_LOG_DIR"] = logDirectory;
            process = Process.Start(startInfo)
                ?? throw new Xunit.Sdk.XunitException("SlateWindows.exe did not start.");

            if (!HasInteractiveDesktop(process, "history"))
            {
                return;
            }

            using var automation = new UIA3Automation();
            Window window = WaitForMainWindow(
                process,
                automation,
                Path.Combine(logDirectory, "slate-windows.log"),
                TimeSpan.FromSeconds(30));
            window.SetForeground();
            window.Focus();

            // Open the note and create two REAL versions via in-app
            // saves (files bind to an op-log on their first Slate
            // save).
            AutomationElement filesTree = WaitForElement(
                window, "FilesTree", TimeSpan.FromSeconds(30));
            AutomationElement noteItem = filesTree
                .FindAllDescendants(
                    automation.ConditionFactory.ByControlType(ControlType.TreeItem))
                .FirstOrDefault(item =>
                    item.Name.StartsWith("alpha", StringComparison.OrdinalIgnoreCase))
                ?? throw new Xunit.Sdk.XunitException("The alpha TreeItem is absent.");
            noteItem.Patterns.SelectionItem.Pattern.Select();
            AutomationElement editor = WaitForEditor(
                window, automation, "alpha.md editor", TimeSpan.FromSeconds(10));
            Keyboard.Press(VirtualKeyShort.ALT);
            window.SetForeground();
            Keyboard.Release(VirtualKeyShort.ALT);
            editor.Focus();
            Keyboard.Type("first revision ");
            PressChord(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_S);
            Assert.True(
                SpinWait.SpinUntil(
                    () =>
                    {
                        // Tolerant of the atomic write racing the poll.
                        try
                        {
                            return File.ReadAllText(
                                    Path.Combine(vaultRoot, "alpha.md"))
                                .Contains(
                                    "first revision", StringComparison.Ordinal);
                        }
                        catch (IOException)
                        {
                            return false;
                        }
                    },
                    TimeSpan.FromSeconds(10)),
                "the first in-app save never reached disk");
            Keyboard.Type("second revision ");
            PressChord(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_S);
            Assert.True(
                SpinWait.SpinUntil(
                    () =>
                    {
                        // Tolerant of the atomic write racing the poll.
                        try
                        {
                            return File.ReadAllText(
                                    Path.Combine(vaultRoot, "alpha.md"))
                                .Contains(
                                    "second revision", StringComparison.Ordinal);
                        }
                        catch (IOException)
                        {
                            return false;
                        }
                    },
                    TimeSpan.FromSeconds(10)),
                "the second in-app save never reached disk");

            // slate.history.showPanel: the chordless menu route
            // (contract H11) reveals the leaf.
            AutomationElement showHistory = WaitForMenuItem(
                window, "WorkspaceMenu", "ShowHistoryPanelMenuItem",
                TimeSpan.FromSeconds(10));
            showHistory.Patterns.Invoke.Pattern.Invoke();
            // The leaf BODY is a WPF Panel — presence via peered
            // children (the W4-5 lesson).
            _ = WaitForElement(
                window, "HistorySegmentThisNote", TimeSpan.FromSeconds(10));
            // The scope group name rides a peered landmark (H2) — a
            // name on the bare panel would be DROPPED from UIA (red
            // team round 1).
            Assert.NotNull(window.FindFirstDescendant(
                automation.ConditionFactory.ByName("History scope")));
            AutomationElement versionHeader = WaitForElement(
                window, "HistoryVersionHeader", TimeSpan.FromSeconds(15));
            Assert.Contains(
                "version", versionHeader.Name, StringComparison.OrdinalIgnoreCase);
            Assert.True(
                SpinWait.SpinUntil(
                    () => window.FindAllDescendants(
                        automation.ConditionFactory.ByControlType(ControlType.Group))
                        .Any(candidate =>
                            (candidate.Properties.AutomationId.ValueOrDefault ?? "")
                                .StartsWith("HistoryDay", StringComparison.Ordinal)),
                    TimeSpan.FromSeconds(15)),
                "no day group appeared after two in-app saves");
            // The composed row name reaches UIA: one Group per version
            // row carrying "{date}, {core fragment}" (H3 — red team
            // round 1: a plain Border host published NOTHING).
            AutomationElement versionRow = window
                .FindAllDescendants(
                    automation.ConditionFactory.ByControlType(ControlType.Group))
                .FirstOrDefault(candidate =>
                    (candidate.Properties.AutomationId.ValueOrDefault ?? "")
                        .StartsWith("HistoryRow", StringComparison.Ordinal))
                ?? throw new Xunit.Sdk.XunitException(
                    "no peered version-row Group reached UIA");
            Assert.Contains(",", versionRow.Name, StringComparison.Ordinal);

            // The markers toggle re-filters without a re-query, and the
            // header count (core's marker-INCLUSIVE TotalFiltered, H3)
            // never moves. Every toggle rebuilds the panel, so the
            // probe RE-ACQUIRES the checkbox each poll and treats a
            // disconnected element as "not yet" (stale-element trap —
            // red team round 1).
            string headerBeforeMarkers = versionHeader.Name;
            bool MarkersToggleReads(ToggleState desired)
            {
                try
                {
                    AutomationElement? current = window.FindFirstDescendant(
                        automation.ConditionFactory.ByAutomationId(
                            "HistoryShowMarkers"));
                    return current is not null
                        && current.Patterns.Toggle.Pattern.ToggleState.Value
                            == desired;
                }
                catch (System.Runtime.InteropServices.COMException)
                {
                    return false;
                }
            }
            AutomationElement markers = WaitForElement(
                window, "HistoryShowMarkers", TimeSpan.FromSeconds(10));
            markers.Patterns.Toggle.Pattern.Toggle();
            Assert.True(
                SpinWait.SpinUntil(
                    () => MarkersToggleReads(ToggleState.On),
                    TimeSpan.FromSeconds(10)),
                "the markers toggle never reached the On state");
            Assert.Equal(
                headerBeforeMarkers,
                WaitForElement(
                    window, "HistoryVersionHeader", TimeSpan.FromSeconds(5)).Name);
            markers = WaitForElement(
                window, "HistoryShowMarkers", TimeSpan.FromSeconds(5));
            markers.Patterns.Toggle.Pattern.Toggle();
            Assert.True(
                SpinWait.SpinUntil(
                    () => MarkersToggleReads(ToggleState.Off),
                    TimeSpan.FromSeconds(10)),
                "the markers toggle never returned to the Off state");

            // A per-row Compare on the OLDEST row (a real delta —
            // the newest row would diff against itself and publish
            // zero operations) publishes core's StructuredDiff
            // walkthrough: the AudioSummary header AND at least one
            // single-stop operation element (H5 — red team round 1:
            // the summary alone gated nothing).
            AutomationElement? compareButton = null;
            Assert.True(
                SpinWait.SpinUntil(
                    () =>
                    {
                        compareButton = window
                            .FindAllDescendants(automation.ConditionFactory
                                .ByControlType(ControlType.Button))
                            .LastOrDefault(button =>
                                (button.Name ?? "").StartsWith(
                                    "Compare,", StringComparison.Ordinal));
                        return compareButton is not null;
                    },
                    TimeSpan.FromSeconds(10)),
                "no per-row Compare button appeared");
            compareButton!.Patterns.Invoke.Pattern.Invoke();
            AutomationElement diffSummary = WaitForElement(
                window, "HistoryDiffSummary", TimeSpan.FromSeconds(15));
            Assert.False(
                string.IsNullOrWhiteSpace(diffSummary.Name),
                "the diff summary is empty");
            AutomationElement diffOperation = WaitForElement(
                window, "HistoryDiffOp0", TimeSpan.FromSeconds(10));
            Assert.False(
                string.IsNullOrWhiteSpace(diffOperation.Name),
                "the first diff operation carries no accessible sentence");
            AssertAxeClean(process, "history-this-note");

            // The Deleted segment: the recorded empty state + the
            // standing footer, both verbatim (H2/H10).
            AutomationElement deletedSegment = WaitForElement(
                window, "HistorySegmentDeleted", TimeSpan.FromSeconds(10));
            deletedSegment.Patterns.SelectionItem.Pattern.Select();
            AutomationElement deletedEmpty = WaitForElement(
                window, "HistoryDeletedEmpty", TimeSpan.FromSeconds(15));
            Assert.Equal("No recently deleted files.", deletedEmpty.Name);
            AutomationElement deletedFooter = WaitForElement(
                window, "HistoryDeletedFooter", TimeSpan.FromSeconds(10));
            Assert.Equal(
                "Files deleted before Slate saved them go to the system Trash.",
                deletedFooter.Name);
            AssertAxeClean(process, "history-deleted");
            AutomationElement thisNoteSegment = WaitForElement(
                window, "HistorySegmentThisNote", TimeSpan.FromSeconds(10));
            thisNoteSegment.Patterns.SelectionItem.Pattern.Select();

            // A REAL restore through the production confirmation
            // dialog: the oldest version's body lands back on disk and
            // the restore appends a new head row (history never
            // rewrites).
            AutomationElement? restoreButton = null;
            Assert.True(
                SpinWait.SpinUntil(
                    () =>
                    {
                        restoreButton = window
                            .FindAllDescendants(automation.ConditionFactory
                                .ByControlType(ControlType.Button))
                            .LastOrDefault(button =>
                                (button.Name ?? "").StartsWith(
                                    "Restore,", StringComparison.Ordinal));
                        return restoreButton is not null;
                    },
                    TimeSpan.FromSeconds(10)),
                "no per-row Restore button appeared");
            restoreButton!.Patterns.Invoke.Pattern.Invoke();
            // The modal is its own top-level window titled
            // "Restore version?" with the PINNED Cancel/Restore
            // buttons (H7) — confirm via Restore.
            Assert.True(
                SpinWait.SpinUntil(
                    () =>
                    {
                        Window[] modals = window.ModalWindows;
                        Window? confirm = modals.FirstOrDefault(candidate =>
                            candidate.Title.Contains(
                                "Restore version?", StringComparison.Ordinal));
                        if (confirm is null)
                        {
                            return false;
                        }
                        // No Asserts inside the poll: a modal that
                        // enumerates before its buttons materialize is
                        // "not yet", never a failure.
                        AutomationElement[] buttons = confirm
                            .FindAllDescendants(automation.ConditionFactory
                                .ByControlType(ControlType.Button));
                        AutomationElement? cancel = buttons
                            .FirstOrDefault(button =>
                                string.Equals(
                                    button.Name,
                                    "Cancel",
                                    StringComparison.Ordinal));
                        AutomationElement? restore = buttons
                            .FirstOrDefault(button =>
                                string.Equals(
                                    button.Name,
                                    "Restore",
                                    StringComparison.Ordinal));
                        if (cancel is null || restore is null)
                        {
                            return false;
                        }
                        restore.Patterns.Invoke.Pattern.Invoke();
                        return true;
                    },
                    TimeSpan.FromSeconds(15)),
                "the restore confirmation dialog with its pinned "
                + "Cancel/Restore buttons never appeared");
            Assert.True(
                SpinWait.SpinUntil(
                    () =>
                    {
                        // Tolerant of the atomic write racing the poll.
                        try
                        {
                            return !File.ReadAllText(
                                    Path.Combine(vaultRoot, "alpha.md"))
                                .Contains(
                                    "second revision", StringComparison.Ordinal);
                        }
                        catch (IOException)
                        {
                            return false;
                        }
                    },
                    TimeSpan.FromSeconds(15)),
                "the restore never landed on disk");
            // Success returns focus to the NEW HEAD row — exactly
            // HistoryRow0, not the anchor row or its buttons, whose
            // ids also start with "HistoryRow" (H7 / WCAG 2.4.3;
            // round 2 tightened the gate). The reload is
            // asynchronous, so the focus request is consumed only
            // after the publish renders the fresh rows (round 1:
            // four agents found the pre-publish focus race).
            Assert.True(
                SpinWait.SpinUntil(
                    () =>
                    {
                        AutomationElement? focused = automation.FocusedElement();
                        return string.Equals(
                            focused?.Properties.AutomationId.ValueOrDefault,
                            "HistoryRow0",
                            StringComparison.Ordinal);
                    },
                    TimeSpan.FromSeconds(15)),
                "focus never landed on the new head row after the restore");
        }
        finally
        {
            if (process is not null && !process.HasExited)
            {
                process.CloseMainWindow();
                if (!process.WaitForExit(5_000))
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            try
            {
                Directory.Delete(testRoot, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    /// <summary>
    /// W4-8 (#740) §W-C journey: the Sync leaf behind the rail over a
    /// fixture vault carrying three REAL provider markers — a
    /// <c>.git</c> directory, a <c>.stfolder</c>, and the vault's own
    /// path nested under a directory named <c>OneDrive</c>. All three
    /// arms read only in-vault entries or the vault's own path, so the
    /// fixture stays deterministic on every machine with zero
    /// environment dependence, and Syncthing + OneDrive (both Medium)
    /// also render the multi-sync warning row through the axe scan.
    /// The OneDrive nesting is load-bearing, not decoration: that arm
    /// alone yields CANONICALIZED evidence, so it is the only one whose
    /// path carries a <c>\\?\</c> prefix before SD9 normalization —
    /// asserting on Git's relative <c>.git</c> literal could never
    /// fail (red team round 1). Covers the populated report, the
    /// composed provider-row name reaching UIA, the Evidence
    /// disclosure with its SD9-normalized paths, the chordless
    /// slate.diagnostics.refreshSync menu route, and the SD8 marker
    /// watcher end-to-end via a FOURTH marker planted mid-session —
    /// the one link no unit fact covers. Honors the recorded journey
    /// traps (no Asserts inside SpinUntil, re-acquire after every
    /// publish, async Invoke settle).
    /// </summary>
    [Fact]
    [Trait("gate", "W-C")]
    public void SyncDiagnostics_LeafReportAndRefresh_AreClean()
    {
        string testRoot = Path.Combine(
            Path.GetTempPath(), $"slate-sync-diagnostics-{Guid.NewGuid():N}");
        // The vault sits under a directory named exactly "OneDrive" so
        // core's OneDrive path-COMPONENT arm fires. That arm is the one
        // whose evidence is the CANONICALIZED vault path — on Windows a
        // `\\?\C:\...` verbatim path — which is what makes the SD9
        // normalization assertion below a real gate. Git's and
        // Syncthing's evidence are relative literals (".git",
        // ".stfolder") that never pass through the boundary at all, so
        // asserting on them could never fail (red team round 1).
        string vaultRoot = Path.Combine(testRoot, "OneDrive", "Sync Vault");
        string logDirectory = Path.Combine(testRoot, "logs");
        Directory.CreateDirectory(vaultRoot);
        File.WriteAllText(
            Path.Combine(vaultRoot, "alpha.md"),
            "# Alpha\n\nOriginal body.\n");
        // Provider-REAL markers (SD11): core's Git arm fires on
        // `{root}/.git` existing and its Syncthing arm on a
        // `{root}/.stfolder` DIRECTORY. Neither arm reads the
        // environment, the home directory, or an installed client, and
        // the OneDrive arm above reads only the vault's own path — so
        // the count below is a fact about the fixture, not the machine.
        // Syncthing and OneDrive are both Medium risk, so this fixture
        // also renders the multi-sync warning row through the axe scan.
        Directory.CreateDirectory(Path.Combine(vaultRoot, ".git"));
        Directory.CreateDirectory(Path.Combine(vaultRoot, ".stfolder"));

        Process? process = null;
        try
        {
            var startInfo = new ProcessStartInfo(SlateWindowsExe())
            {
                UseShellExecute = false,
            };
            startInfo.ArgumentList.Add(vaultRoot);
            startInfo.Environment["SLATE_CENSUS_INSTANCE_ID"] =
                $"slate-sync-diagnostics-{Guid.NewGuid():N}";
            startInfo.Environment["SLATE_LOG_DIR"] = logDirectory;
            process = Process.Start(startInfo)
                ?? throw new Xunit.Sdk.XunitException("SlateWindows.exe did not start.");

            if (!HasInteractiveDesktop(process, "sync-diagnostics"))
            {
                return;
            }

            using var automation = new UIA3Automation();
            Window window = WaitForMainWindow(
                process,
                automation,
                Path.Combine(logDirectory, "slate-windows.log"),
                TimeSpan.FromSeconds(30));
            window.SetForeground();
            window.Focus();

            // The RAIL is the reveal route: SD7 registers no showPanel
            // command for this leaf on either platform, so there is no
            // menu path to the surface — only the refresh below.
            AutomationElement leaves = WaitForElement(
                window, "RightPaneLeaves", TimeSpan.FromSeconds(30));
            AutomationElement? syncLeaf = null;
            Assert.True(
                SpinWait.SpinUntil(
                    () =>
                    {
                        syncLeaf = leaves
                            .FindAllDescendants(automation.ConditionFactory
                                .ByControlType(ControlType.ListItem))
                            .FirstOrDefault(item => string.Equals(
                                item.Name, "Sync", StringComparison.Ordinal));
                        return syncLeaf is not null;
                    },
                    TimeSpan.FromSeconds(10)),
                "the Sync leaf option never appeared");
            syncLeaf!.Patterns.SelectionItem.Pattern.Select();

            // Every publish CLEARS and rebuilds the surface, so every
            // element handle held across one is dead. Each probe below
            // re-acquires from the window and reads a COM fault as
            // "not yet" (the recorded stale-element trap).
            string? HeaderName()
            {
                try
                {
                    return window.FindFirstDescendant(automation.ConditionFactory
                        .ByAutomationId("SyncDiagnosticsHeader"))?.Name;
                }
                catch (System.Runtime.InteropServices.COMException)
                {
                    return null;
                }
            }

            // The leaf BODY is a WPF Panel — no peer, no id in the tree
            // (the W4-5 lesson); presence rides its peered children. The
            // vault-open probe is ASYNC (SD4 arm-then-probe), so the
            // count header is POLLED rather than asserted once.
            Assert.True(
                SpinWait.SpinUntil(
                    () => HeaderName() is { } name
                        && name.Contains(
                            "3 systems detected", StringComparison.Ordinal),
                    TimeSpan.FromSeconds(30)),
                "the sync header never reported the three planted markers; "
                + $"last header name: '{HeaderName()}'");

            // The COMPOSED row name reaches UIA: one Group per provider
            // named "{DisplayName}: {risk word}. {Recommendation}" (SD3)
            // — a name on a bare Border publishes NOTHING, which is why
            // the row rides AutomationNamedRowBorder.
            AutomationElement gitRow = WaitForElement(
                window, "SyncDiagnosticsProviderGit", TimeSpan.FromSeconds(10));
            Assert.Equal(ControlType.Group, gitRow.ControlType);
            Assert.Contains("Git", gitRow.Name, StringComparison.Ordinal);
            // Core's normative recommendation, relayed verbatim (SD1).
            // A SUBSTRING, not equality: the host does not own this
            // sentence and a copy edit in core must not break the
            // journey on wording it only relays.
            Assert.Contains(
                "This vault is a Git working tree",
                gitRow.Name,
                StringComparison.Ordinal);

            // The Evidence disclosure is a SEPARATE operable sibling of
            // the row (SD3(3)); each path inside is its own focusable
            // line, verbatim from core and normalized at the SD9
            // boundary — a `\\?\` prefix reaching a reader is the defect
            // that boundary exists to prevent. ONEDRIVE, not Git: this
            // arm's evidence IS the canonicalized vault path, so on
            // Windows it carries the verbatim prefix before
            // normalization and the assertion below can actually fail.
            AutomationElement evidence = WaitForElement(
                window, "SyncDiagnosticsEvidenceOneDrive", TimeSpan.FromSeconds(10));
            Assert.True(
                evidence.Patterns.ExpandCollapse.IsSupported,
                "the OneDrive Evidence disclosure does not expose ExpandCollapse");
            evidence.Patterns.ExpandCollapse.Pattern.Expand();
            AutomationElement? evidenceLine = null;
            Assert.True(
                SpinWait.SpinUntil(
                    () =>
                    {
                        try
                        {
                            evidenceLine = window
                                .FindAllDescendants(automation.ConditionFactory
                                    .ByControlType(ControlType.Text))
                                .FirstOrDefault(candidate =>
                                    (candidate.Properties.AutomationId
                                        .ValueOrDefault ?? "").StartsWith(
                                            "SyncDiagnosticsEvidenceOneDrivePath",
                                            StringComparison.Ordinal)
                                    && candidate.Properties.IsKeyboardFocusable
                                        .ValueOrDefault
                                    && !string.IsNullOrWhiteSpace(candidate.Name));
                            return evidenceLine is not null;
                        }
                        catch (System.Runtime.InteropServices.COMException)
                        {
                            return false;
                        }
                    },
                    TimeSpan.FromSeconds(10)),
                "no focusable OneDrive evidence line reached UIA after expanding "
                + "the disclosure");
            // The canon-derived path reached the reader with its prefix
            // stripped — and still names the vault, so normalization
            // cannot pass by emptying the line.
            Assert.False(
                evidenceLine!.Name.StartsWith(@"\\?\", StringComparison.Ordinal),
                $"an evidence path kept its verbatim prefix: '{evidenceLine.Name}'");
            Assert.Contains(
                "Sync Vault", evidenceLine.Name, StringComparison.Ordinal);
            AssertAxeClean(process, "sync-diagnostics");

            // The chordless slate.diagnostics.refreshSync route (SD7):
            // the menu item re-runs detection idempotently and never
            // reveals the leaf. Invoke is ASYNC, and the publish rebuilds
            // the whole surface, so the settle poll re-acquires the
            // header and the row every pass and expects the SAME count —
            // refresh republishes without wrecking the surface.
            AutomationElement refreshItem = WaitForMenuItem(
                window, "WorkspaceMenu", "RefreshSyncDiagnosticsMenuItem",
                TimeSpan.FromSeconds(10));
            refreshItem.Patterns.Invoke.Pattern.Invoke();
            Assert.True(
                SpinWait.SpinUntil(
                    () =>
                    {
                        if (HeaderName() is not { } name
                            || !name.Contains(
                                "3 systems detected", StringComparison.Ordinal))
                        {
                            return false;
                        }
                        try
                        {
                            return window.FindFirstDescendant(
                                automation.ConditionFactory.ByAutomationId(
                                    "SyncDiagnosticsProviderGit")) is not null;
                        }
                        catch (System.Runtime.InteropServices.COMException)
                        {
                            return false;
                        }
                    },
                    TimeSpan.FromSeconds(15)),
                "the sync leaf never re-resolved after the refresh command; "
                + $"last header name: '{HeaderName()}'");

            // THE WATCHER, end to end (SD8) — the link no unit fact
            // covers. Core's LiveSync arm fires on
            // `.obsidian/plugins/obsidian-livesync/` holding a
            // manifest.json; creating that chain churns the WATCHED
            // vault root, and the manifest is on disk long before the
            // 2.5 s trailing debounce (10 s ceiling) elapses, so the
            // re-detection that fires sees all four markers. The
            // timeout is deliberately generous: debounce + ceiling +
            // two FFI calls, never a tight race.
            string livesyncDirectory = Path.Combine(
                vaultRoot, ".obsidian", "plugins", "obsidian-livesync");
            Directory.CreateDirectory(livesyncDirectory);
            File.WriteAllText(
                Path.Combine(livesyncDirectory, "manifest.json"),
                "{\"id\":\"obsidian-livesync\"}");
            Assert.True(
                SpinWait.SpinUntil(
                    () => HeaderName() is { } name
                        && name.Contains(
                            "4 systems detected", StringComparison.Ordinal),
                    TimeSpan.FromSeconds(30)),
                "the marker watcher never republished after a fourth marker "
                + $"landed mid-session; last header name: '{HeaderName()}'");
        }
        finally
        {
            if (process is not null && !process.HasExited)
            {
                process.CloseMainWindow();
                if (!process.WaitForExit(5_000))
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            try
            {
                Directory.Delete(testRoot, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }


    /// <summary>
    /// W5-1 (#741): the command palette end to end — open by chord, filter,
    /// the composed row name, invoke, and focus restore, with an axe scan
    /// over the open overlay.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The unit facts rank against the real core engine but never render,
    /// so everything UIA-visible is unproven until here: whether the
    /// overlay's window-relative Visibility binding actually resolves (the
    /// FallbackValue=Collapsed trap that shipped twice), whether the
    /// composed row name reaches a client, and whether the grouped list
    /// publishes headers rather than swallowing them.
    /// </para>
    /// <para>
    /// Text goes in through the ValuePattern rather than synthetic
    /// keystrokes, and the foreground is re-asserted before input — both
    /// recorded journey traps.
    /// </para>
    /// </remarks>
    [Fact]
    public void CommandPalette_OpensFiltersInvokesAndRestoresFocus_IsClean()
    {
        string testRoot = Path.Combine(
            Path.GetTempPath(), $"slate-command-palette-{Guid.NewGuid():N}");
        string vaultRoot = Path.Combine(testRoot, "Palette Vault");
        string logDirectory = Path.Combine(testRoot, "logs");
        Directory.CreateDirectory(vaultRoot);
        File.WriteAllText(
            Path.Combine(vaultRoot, "alpha.md"),
            "# Alpha\n\nOriginal body.\n");

        Process? process = null;
        try
        {
            var startInfo = new ProcessStartInfo(SlateWindowsExe())
            {
                UseShellExecute = false,
            };
            startInfo.ArgumentList.Add(vaultRoot);
            startInfo.Environment["SLATE_CENSUS_INSTANCE_ID"] =
                $"slate-command-palette-{Guid.NewGuid():N}";
            startInfo.Environment["SLATE_LOG_DIR"] = logDirectory;
            process = Process.Start(startInfo)
                ?? throw new Xunit.Sdk.XunitException("SlateWindows.exe did not start.");

            if (!HasInteractiveDesktop(process, "command-palette"))
            {
                return;
            }

            using var automation = new UIA3Automation();
            Window window = WaitForMainWindow(
                process,
                automation,
                Path.Combine(logDirectory, "slate-windows.log"),
                TimeSpan.FromSeconds(30));
            window.SetForeground();
            window.Focus();

            // The right pane is the observable the invocation leg asserts
            // on, so read its starting state before anything opens.
            bool RightPaneVisible()
            {
                try
                {
                    return window.FindFirstDescendant(automation.ConditionFactory
                        .ByAutomationId("RightPaneLeaves")) is not null;
                }
                catch (System.Runtime.InteropServices.COMException)
                {
                    return false;
                }
            }

            // WAIT FOR THE VAULT, not merely for the window. The palette
            // refuses to open with no vault and deliberately leaves its
            // flag false (contract P14), so a chord sent between window
            // and vault is correctly swallowed — the app is right and the
            // journey would be racing it. The rail only exists once a
            // vault is open, so it is the readiness signal.
            _ = WaitForElement(window, "RightPaneLeaves", TimeSpan.FromSeconds(30));

            bool rightPaneAtStart = RightPaneVisible();

            // --- open by chord (PD-2) ---------------------------------
            // Put keyboard focus on a real element first. A freshly shown
            // window can have NO WPF keyboard focus at all, and the chord
            // rides Window.PreviewKeyDown, which needs focus inside the
            // window to see the key — measured, after a run where the
            // focused element reported neither id nor name and the chord
            // vanished.
            WaitForElement(window, "FilesTree", TimeSpan.FromSeconds(10)).Focus();
            Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(250));
            window.SetForeground();
            PressChord(
                VirtualKeyShort.CONTROL, VirtualKeyShort.SHIFT, VirtualKeyShort.KEY_P);

            AutomationElement overlay = WaitForElement(
                window, "CommandPalette", TimeSpan.FromSeconds(10));
            Assert.Equal("Command Palette", overlay.Name);

            // Focus lands in the search field, not on the overlay root:
            // a palette that opens without a caret is a palette you have
            // to click into.
            AutomationElement search = WaitForElement(
                window, "CommandPaletteSearch", TimeSpan.FromSeconds(10));
            Assert.True(
                SpinWait.SpinUntil(
                    () =>
                    {
                        try
                        {
                            return search.Properties.HasKeyboardFocus.ValueOrDefault;
                        }
                        catch (System.Runtime.InteropServices.COMException)
                        {
                            return false;
                        }
                    },
                    TimeSpan.FromSeconds(10)),
                "the palette opened without focusing its search field");

            // Axe over the OPEN palette: the overlay, the grouped result
            // list, every row name, and the headers — the whole surface
            // W5-1 owns.
            AssertAxeClean(process, "command-palette");

            // --- filter ------------------------------------------------
            search.Patterns.Value.Pattern.SetValue("right pane");

            // The COMPOSED row name is the P6 contract: one accessible
            // name per row carrying the label AND the spoken chord. The
            // bolded runs, the visible chord, and any unavailability
            // caption are presentation-only and must contribute no stops
            // — so this exact string is what a screen reader hears.
            AutomationElement results = WaitForElement(
                window, "CommandPaletteResults", TimeSpan.FromSeconds(10));
            AutomationElement? togglePaneRow = null;
            Assert.True(
                SpinWait.SpinUntil(
                    () =>
                    {
                        try
                        {
                            togglePaneRow = results
                                .FindAllDescendants(automation.ConditionFactory
                                    .ByControlType(ControlType.ListItem))
                                .FirstOrDefault(item => string.Equals(
                                    item.Name,
                                    "Toggle Right Pane, Control Alt I",
                                    StringComparison.Ordinal));
                            return togglePaneRow is not null;
                        }
                        catch (System.Runtime.InteropServices.COMException)
                        {
                            return false;
                        }
                    },
                    TimeSpan.FromSeconds(10)),
                "no palette row published the composed name "
                + "'Toggle Right Pane, Control Alt I'; names seen: "
                + string.Join(
                    " | ",
                    results
                        .FindAllDescendants(automation.ConditionFactory
                            .ByControlType(ControlType.ListItem))
                        .Select(item => item.Name)));

            // Section headers reach the tree as their own elements — the
            // palette groups by the section core PLACED each row in, and
            // a header swallowed into a bare Panel would publish nothing.
            Assert.Contains(
                results.FindAllDescendants(),
                element => string.Equals(element.Name, "View", StringComparison.Ordinal));

            // A SECOND scan, now that the box has text in it (#1106). The
            // Fluent TextBox template reveals a clear button once the box
            // is non-empty and keyboard focus is inside it, and that
            // button published a Segoe MDL2 private-use glyph as its
            // accessible name. Every axe scan in this suite until W5-1
            // walked past an EMPTY box, which is exactly why a defect
            // shared by every TextBox in the app stayed hidden since W1.
            // The gap was never the rule — it was that nothing typed
            // first. This is the leg that closes it.
            AssertAxeClean(process, "command-palette-filtered");

            // --- text editing survives the modal swallow ---------------
            // The overlay swallows shell chords so they cannot fire
            // underneath it, and the first version swallowed EVERY
            // modified key — which killed Ctrl+A, Ctrl+C, Ctrl+V and
            // Shift-selection inside the palette's own search box, because
            // TextBox reaches those through InputBindings and WPF runs
            // InputBindings only for UNHANDLED key events. Ctrl+A then
            // Delete discriminates: with Ctrl+A swallowed the caret sits
            // at the end and Delete removes nothing.
            PressChord(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
            PressKey(VirtualKeyShort.DELETE);
            Assert.True(
                SpinWait.SpinUntil(
                    () =>
                    {
                        try
                        {
                            return string.IsNullOrEmpty(
                                search.Patterns.Value.Pattern.Value.ValueOrDefault);
                        }
                        catch (System.Runtime.InteropServices.COMException)
                        {
                            return false;
                        }
                    },
                    TimeSpan.FromSeconds(5)),
                "Ctrl+A did not reach the palette's search box — the modal "
                + "swallow is eating text-editing chords. Query is still: "
                + $"'{search.Patterns.Value.Pattern.Value.ValueOrDefault}'");

            // Put the filter back for the invocation leg.
            search.Patterns.Value.Pattern.SetValue("right pane");
            togglePaneRow = null;
            Assert.True(
                SpinWait.SpinUntil(
                    () =>
                    {
                        try
                        {
                            togglePaneRow = results
                                .FindAllDescendants(automation.ConditionFactory
                                    .ByControlType(ControlType.ListItem))
                                .FirstOrDefault(item => string.Equals(
                                    item.Name,
                                    "Toggle Right Pane, Control Alt I",
                                    StringComparison.Ordinal));
                            return togglePaneRow is not null;
                        }
                        catch (System.Runtime.InteropServices.COMException)
                        {
                            return false;
                        }
                    },
                    TimeSpan.FromSeconds(10)),
                "the filtered row did not come back after retyping");

            // --- invoke, and assert the command actually ran -----------
            // Selection is view-model-authoritative, so drive it the way a
            // user does rather than by setting SelectedItem.
            togglePaneRow!.Patterns.SelectionItem.Pattern.Select();
            Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(250));
            PressKey(VirtualKeyShort.ENTER);

            // The palette closes on success only, and the right pane flips.
            Assert.True(
                SpinWait.SpinUntil(
                    () => RightPaneVisible() != rightPaneAtStart,
                    TimeSpan.FromSeconds(10)),
                "invoking Toggle Right Pane from the palette did not change "
                + "the right pane's visibility — the registry round-trip "
                + "reached no live command.");

            Assert.True(
                SpinWait.SpinUntil(
                    () =>
                    {
                        try
                        {
                            return window.FindFirstDescendant(automation.ConditionFactory
                                .ByAutomationId("CommandPalette")) is null;
                        }
                        catch (System.Runtime.InteropServices.COMException)
                        {
                            return true;
                        }
                    },
                    TimeSpan.FromSeconds(10)),
                "the palette stayed open after a successful invocation");

            // --- PD-2: the chord re-opens rather than toggling ---------
            // Round 2 gate: reverting the re-open branch failed NOTHING,
            // because every other chord press in this journey starts from a
            // CLOSED palette. Pressing it while open must clear the query,
            // not close the overlay and not sit inert.
            // The palette is CLOSED here (the invocation above dismissed
            // it), so the chord must first re-open it — otherwise this leg
            // exercises the ordinary open path and passes whether or not
            // the re-open branch exists, which is exactly how it shipped.
            window.SetForeground();
            PressChord(
                VirtualKeyShort.CONTROL, VirtualKeyShort.SHIFT, VirtualKeyShort.KEY_P);
            _ = WaitForElement(window, "CommandPalette", TimeSpan.FromSeconds(10));
            AutomationElement reopened = WaitForElement(
                window, "CommandPaletteSearch", TimeSpan.FromSeconds(10));
            reopened.Patterns.Value.Pattern.SetValue("zzznothing");
            Assert.Equal(
                "zzznothing",
                reopened.Patterns.Value.Pattern.Value.ValueOrDefault);

            // NOW press it while OPEN — the branch under test.
            window.SetForeground();
            PressChord(
                VirtualKeyShort.CONTROL, VirtualKeyShort.SHIFT, VirtualKeyShort.KEY_P);
            Assert.True(
                SpinWait.SpinUntil(
                    () =>
                    {
                        try
                        {
                            return window.FindFirstDescendant(automation.ConditionFactory
                                .ByAutomationId("CommandPalette")) is not null
                                && string.IsNullOrEmpty(
                                    window.FindFirstDescendant(automation.ConditionFactory
                                        .ByAutomationId("CommandPaletteSearch"))
                                    ?.Patterns.Value.Pattern.Value.ValueOrDefault);
                        }
                        catch (System.Runtime.InteropServices.COMException)
                        {
                            return false;
                        }
                    },
                    TimeSpan.FromSeconds(10)),
                "Ctrl+Shift+P while open did not re-open the palette with a "
                + "cleared query — PD-2 says it re-opens rather than toggling.");

            // --- Escape closes and restores focus ----------------------
            window.SetForeground();
            PressChord(
                VirtualKeyShort.CONTROL, VirtualKeyShort.SHIFT, VirtualKeyShort.KEY_P);
            _ = WaitForElement(window, "CommandPalette", TimeSpan.FromSeconds(10));
            Keyboard.Press(VirtualKeyShort.ESCAPE);
            Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(250));

            Assert.True(
                SpinWait.SpinUntil(
                    () =>
                    {
                        try
                        {
                            return window.FindFirstDescendant(automation.ConditionFactory
                                .ByAutomationId("CommandPalette")) is null;
                        }
                        catch (System.Runtime.InteropServices.COMException)
                        {
                            return true;
                        }
                    },
                    TimeSpan.FromSeconds(10)),
                "Escape did not close the palette");

            // A closed palette must publish no part of itself to the
            // CONTROL view, which is the view an AT walks.
            //
            // Measured, not assumed: all three elements remain in the UIA
            // RAW tree when closed, and FindFirstDescendant misses them
            // because WPF drops invisible elements from the control view.
            // That means the children's own Visibility bindings are
            // redundant on this runtime — kept for consistency with Quick
            // Open, not because they are load-bearing — and this assertion
            // pins the observable rather than the mechanism.
            foreach (string id in new[] { "CommandPaletteSearch", "CommandPaletteResults" })
            {
                Assert.True(
                    SpinWait.SpinUntil(
                        () =>
                        {
                            try
                            {
                                return window.FindFirstDescendant(automation.ConditionFactory
                                    .ByAutomationId(id)) is null;
                            }
                            catch (System.Runtime.InteropServices.COMException)
                            {
                                return true;
                            }
                        },
                        TimeSpan.FromSeconds(5)),
                    $"{id} stayed in the UIA tree after the palette closed");
            }

            // Focus must land somewhere real. A dismissed overlay that
            // leaves focus on the window root strands a keyboard user.
            Assert.True(
                SpinWait.SpinUntil(
                    () =>
                    {
                        try
                        {
                            AutomationElement focused = automation.FocusedElement();
                            return focused is not null
                                && !string.Equals(
                                    focused.Properties.AutomationId.ValueOrDefault,
                                    "Slate.MainWindow",
                                    StringComparison.Ordinal);
                        }
                        catch (System.Runtime.InteropServices.COMException)
                        {
                            return false;
                        }
                    },
                    TimeSpan.FromSeconds(10)),
                "closing the palette left focus on the window root");
        }
        finally
        {
            if (process is not null && !process.HasExited)
            {
                process.CloseMainWindow();
                if (!process.WaitForExit(5_000))
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            try
            {
                Directory.Delete(testRoot, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    /// <summary>
    /// W5-2 (#742): the vault-search overlay end to end — open by chord,
    /// the composed one-stop row names over a fixture vault, activation
    /// asserted by EFFECT on the editor document, Esc focus restore to
    /// the pre-open element, and the control-view teardown of every
    /// SearchOverlay* id, with axe scans over both the empty and the
    /// typed overlay.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The unit facts run the pipeline against a fake source and never
    /// render, so everything UIA-visible is unproven until here: whether
    /// the window-relative Visibility binding resolves (the
    /// FallbackValue=Collapsed trap that shipped twice), whether the
    /// composed S4 row name reaches a client as ONE stop, and whether
    /// activation actually opens the hit.
    /// </para>
    /// <para>
    /// Every recorded journey trap is honoured: the journey waits for
    /// the VAULT (the rail), not merely the window; the foreground is
    /// re-asserted before every chord; text goes in through the
    /// ValuePattern rather than synthetic keystrokes; and assertions
    /// key on AutomationId + Name.
    /// </para>
    /// <para>
    /// The typed axe scan exists because of #1106: the Fluent TextBox
    /// clear button appears only once the box has text and focus, and
    /// its unnamed glyph hid from every empty-box scan since W1. #1106
    /// is fixed; this leg keeps it fixed for the search box.
    /// </para>
    /// </remarks>
    [Fact]
    public void SearchOverlay_OpensSearchesActivatesAndRestoresFocus_IsClean()
    {
        string testRoot = Path.Combine(
            Path.GetTempPath(), $"slate-search-overlay-{Guid.NewGuid():N}");
        string vaultRoot = Path.Combine(testRoot, "Search Vault");
        string logDirectory = Path.Combine(testRoot, "logs");
        Directory.CreateDirectory(vaultRoot);
        // Two fixture files carry the pinned token, one does not — so the
        // query has a known hit set and the composed row names are
        // predictable up to core's snippet windowing.
        File.WriteAllText(
            Path.Combine(vaultRoot, "meridian-alpha.md"),
            "# Alpha\n\nThe meridian crosses the alpha field.\n");
        File.WriteAllText(
            Path.Combine(vaultRoot, "meridian-beta.md"),
            "# Beta\n\nA second meridian sits in the beta note.\n");
        File.WriteAllText(
            Path.Combine(vaultRoot, "quiet.md"),
            "# Quiet\n\nNothing to find here.\n");

        Process? process = null;
        try
        {
            var startInfo = new ProcessStartInfo(SlateWindowsExe())
            {
                UseShellExecute = false,
            };
            startInfo.ArgumentList.Add(vaultRoot);
            startInfo.Environment["SLATE_CENSUS_INSTANCE_ID"] =
                $"slate-search-overlay-{Guid.NewGuid():N}";
            startInfo.Environment["SLATE_LOG_DIR"] = logDirectory;
            process = Process.Start(startInfo)
                ?? throw new Xunit.Sdk.XunitException("SlateWindows.exe did not start.");

            if (!HasInteractiveDesktop(process, "search-overlay"))
            {
                return;
            }

            using var automation = new UIA3Automation();
            Window window = WaitForMainWindow(
                process,
                automation,
                Path.Combine(logDirectory, "slate-windows.log"),
                TimeSpan.FromSeconds(30));
            window.SetForeground();
            window.Focus();

            // WAIT FOR THE VAULT, not merely for the window (the palette
            // journey's recorded trap): the overlay refuses to open with
            // no vault (it announces SearchNeedsVault and stays closed),
            // so a chord sent between window and vault is correctly
            // swallowed and the journey would be racing the app. The
            // right-pane rail only exists once a vault is open.
            _ = WaitForElement(window, "RightPaneLeaves", TimeSpan.FromSeconds(30));

            // --- open by chord ----------------------------------------
            // Put keyboard focus on a real element first: a freshly
            // shown window can have NO WPF keyboard focus at all, and
            // the chord rides Window.PreviewKeyDown.
            WaitForElement(window, "FilesTree", TimeSpan.FromSeconds(10)).Focus();
            Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(250));
            window.SetForeground();
            PressChord(
                VirtualKeyShort.CONTROL, VirtualKeyShort.SHIFT, VirtualKeyShort.KEY_F);

            AutomationElement overlay = WaitForElement(
                window, "SearchOverlay", TimeSpan.FromSeconds(10));
            Assert.Equal("Search Vault", overlay.Name);

            // Focus lands IN the search box, not on the overlay root: an
            // overlay that opens without a caret is one you have to
            // click into.
            AutomationElement search = WaitForElement(
                window, "SearchOverlaySearch", TimeSpan.FromSeconds(10));
            Assert.Equal("Search vault", search.Name);
            Assert.True(
                SpinWait.SpinUntil(
                    () =>
                    {
                        try
                        {
                            return search.Properties.HasKeyboardFocus.ValueOrDefault;
                        }
                        catch (Exception exception) when (IsTransientUiaFault(exception))
                        {
                            return false;
                        }
                    },
                    TimeSpan.FromSeconds(10)),
                "the search overlay opened without focusing its search box");

            // Axe over the OPEN, EMPTY overlay: the idle hint, the
            // field, and the dialog chrome.
            AssertAxeClean(process, "search-overlay");

            // --- type a query, walk the composed rows -----------------
            // ValuePattern, not synthetic keystrokes (the recorded
            // journey trap): the debounced pipeline sees one change.
            search.Patterns.Value.Pattern.SetValue("meridian");

            AutomationElement results = WaitForElement(
                window, "SearchOverlayResults", TimeSpan.FromSeconds(10));
            AutomationElement[] rows = [];
            Assert.True(
                SpinWait.SpinUntil(
                    () =>
                    {
                        try
                        {
                            rows = results.FindAllDescendants(
                                automation.ConditionFactory
                                    .ByControlType(ControlType.ListItem));
                            return rows.Length == 2;
                        }
                        catch (Exception exception) when (IsTransientUiaFault(exception))
                        {
                            return false;
                        }
                    },
                    TimeSpan.FromSeconds(10)),
                "the query 'meridian' did not produce exactly its two fixture "
                + "hits; names seen: "
                + string.Join(" | ", rows.Select(item => item.Name)));

            // Contract S4: one accessible stop per row, named
            // "{basename}: {marker-stripped snippet}". The emphasis
            // runs are presentation-only, so the control view must show
            // NO children under a row — and the name must carry no STX
            // or ETX marker byte, which is what "stripped" means.
            var seenBasenames = new List<string>();
            FlaUI.Core.ITreeWalker controlWalker =
                automation.TreeWalkerFactory.GetControlViewWalker();
            foreach (AutomationElement row in rows)
            {
                string name = row.Name;
                int separator = name.IndexOf(": ", StringComparison.Ordinal);
                Assert.True(
                    separator > 0,
                    $"row name '{name}' is not the composed "
                    + "'{{basename}}: {{snippet}}' shape.");
                seenBasenames.Add(name[..separator]);
                Assert.Contains("meridian", name, StringComparison.Ordinal);
                Assert.DoesNotContain('', name);
                Assert.DoesNotContain('', name);
                AutomationElement? strayChild = controlWalker.GetFirstChild(row);
                Assert.True(
                    strayChild is null,
                    $"row '{name}' publishes a control-view child — the "
                    + "emphasis runs must be presentation-only (S4). Child: "
                    + $"type={strayChild?.Properties.ControlType.ValueOrDefault}, "
                    + $"class='{strayChild?.Properties.ClassName.ValueOrDefault}', "
                    + $"name='{strayChild?.Properties.Name.ValueOrDefault}', "
                    + $"id='{strayChild?.Properties.AutomationId.ValueOrDefault}'");
            }

            Assert.Equal(
                new[] { "meridian-alpha.md", "meridian-beta.md" },
                seenBasenames.OrderBy(item => item, StringComparer.Ordinal).ToArray());

            // A SECOND scan with text in the box (#1106): the Fluent
            // clear button exists only now, and the workspace behind the
            // overlay is unchanged — a new failure here is the overlay's.
            AssertAxeClean(process, "search-overlay-typed");

            // --- Enter opens the target, asserted by EFFECT -----------
            // The top row is auto-selected on publish; read its basename
            // rather than assuming which file ranks first.
            string topRowName = rows[0].Name;
            string topBasename = topRowName[..topRowName.IndexOf(
                ": ", StringComparison.Ordinal)];
            window.SetForeground();
            PressKey(VirtualKeyShort.ENTER);

            Assert.True(
                SpinWait.SpinUntil(
                    () =>
                    {
                        try
                        {
                            return window.FindFirstDescendant(automation.ConditionFactory
                                .ByAutomationId("SearchOverlay")) is null;
                        }
                        catch (Exception exception) when (IsTransientUiaFault(exception))
                        {
                            return true;
                        }
                    },
                    TimeSpan.FromSeconds(10)),
                "the overlay stayed open after activating a result");

            // The EFFECT: the target note is open in the editor. The
            // document name is the "{filename} editor" contract, so this
            // cannot pass on a closed overlay alone.
            _ = WaitForEditor(
                window,
                automation,
                $"{topBasename} editor",
                TimeSpan.FromSeconds(10));

            // --- Esc on a REOPENED overlay restores the pre-open focus -
            // Park focus somewhere deliberate first, and remember the
            // exact element by runtime id — "restores prior focus" means
            // THAT element, not merely "not the window root" (SD-2).
            AutomationElement filesTree = WaitForElement(
                window, "FilesTree", TimeSpan.FromSeconds(10));
            filesTree.Focus();
            Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(250));
            AutomationElement? focusedBefore = null;
            Assert.True(
                SpinWait.SpinUntil(
                    () =>
                    {
                        try
                        {
                            focusedBefore = automation.FocusedElement();
                            return focusedBefore is not null;
                        }
                        catch (Exception exception) when (IsTransientUiaFault(exception))
                        {
                            return false;
                        }
                    },
                    TimeSpan.FromSeconds(10)),
                "no element reported keyboard focus before the reopen");
            int[] runtimeIdBefore = focusedBefore!.Properties.RuntimeId.Value;

            window.SetForeground();
            PressChord(
                VirtualKeyShort.CONTROL, VirtualKeyShort.SHIFT, VirtualKeyShort.KEY_F);
            _ = WaitForElement(window, "SearchOverlay", TimeSpan.FromSeconds(10));
            AutomationElement reopenedSearch = WaitForElement(
                window, "SearchOverlaySearch", TimeSpan.FromSeconds(10));
            Assert.True(
                SpinWait.SpinUntil(
                    () =>
                    {
                        try
                        {
                            return reopenedSearch.Properties
                                .HasKeyboardFocus.ValueOrDefault;
                        }
                        catch (Exception exception) when (IsTransientUiaFault(exception))
                        {
                            return false;
                        }
                    },
                    TimeSpan.FromSeconds(10)),
                "the reopened overlay did not focus its search box");

            Keyboard.Press(VirtualKeyShort.ESCAPE);
            Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(250));

            Assert.True(
                SpinWait.SpinUntil(
                    () =>
                    {
                        try
                        {
                            return window.FindFirstDescendant(automation.ConditionFactory
                                .ByAutomationId("SearchOverlay")) is null;
                        }
                        catch (Exception exception) when (IsTransientUiaFault(exception))
                        {
                            return true;
                        }
                    },
                    TimeSpan.FromSeconds(10)),
                "Escape did not close the search overlay");

            Assert.True(
                SpinWait.SpinUntil(
                    () =>
                    {
                        try
                        {
                            AutomationElement? focusedAfter = automation.FocusedElement();
                            int[]? runtimeIdAfter =
                                focusedAfter?.Properties.RuntimeId.ValueOrDefault;
                            return runtimeIdAfter is not null
                                && runtimeIdAfter.SequenceEqual(runtimeIdBefore);
                        }
                        catch (Exception exception) when (IsTransientUiaFault(exception))
                        {
                            return false;
                        }
                    },
                    TimeSpan.FromSeconds(10)),
                "Escape did not restore focus to the pre-open element "
                + $"(id '{focusedBefore.Properties.AutomationId.ValueOrDefault}')");

            // --- the close button dismisses by pointer (contract S15,
            // red-team round 1): before it, Esc was the only way out of
            // a surface the menu can open by pointer ------------------
            window.SetForeground();
            PressChord(
                VirtualKeyShort.CONTROL, VirtualKeyShort.SHIFT, VirtualKeyShort.KEY_F);
            _ = WaitForElement(window, "SearchOverlay", TimeSpan.FromSeconds(10));
            AutomationElement closeButton = WaitForElement(
                window, "SearchOverlayClose", TimeSpan.FromSeconds(10));
            Assert.Equal("Close search", closeButton.Name);
            closeButton.Patterns.Invoke.Pattern.Invoke();

            Assert.True(
                SpinWait.SpinUntil(
                    () =>
                    {
                        try
                        {
                            return window.FindFirstDescendant(automation.ConditionFactory
                                .ByAutomationId("SearchOverlay")) is null;
                        }
                        catch (Exception exception) when (IsTransientUiaFault(exception))
                        {
                            return true;
                        }
                    },
                    TimeSpan.FromSeconds(10)),
                "the Close search button did not close the overlay");

            // --- SD-5: the palette SUPERSEDES an open search overlay --
            // Opening the palette CLOSES search: a closed overlay is
            // collapsed, and a collapsed overlay is absent from the
            // control view, which retires the round-9/round-10
            // hidden-but-exposed UIA class by construction. The
            // preserved query then makes Ctrl+Shift+F the way back.
            // Focus is parked on a deliberate element FIRST so the
            // supersession's focus lineage — palette adopts search's
            // pre-open token — is asserted by identity, not vibes
            // (codex round 11: the adoption was previously unprovable
            // from this journey).
            AutomationElement parkedBeforeSearch = WaitForElement(
                window, "FilesTree", TimeSpan.FromSeconds(10));
            parkedBeforeSearch.Focus();
            Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(250));
            int[] parkedRuntimeId = parkedBeforeSearch.Properties.RuntimeId.Value;

            window.SetForeground();
            PressChord(
                VirtualKeyShort.CONTROL, VirtualKeyShort.SHIFT, VirtualKeyShort.KEY_F);
            AutomationElement supersededSearch = WaitForElement(
                window, "SearchOverlaySearch", TimeSpan.FromSeconds(10));
            supersededSearch.Patterns.Value.Pattern.SetValue("meridian");
            _ = WaitForElement(
                window, "SearchOverlayResults", TimeSpan.FromSeconds(10));

            window.SetForeground();
            PressChord(
                VirtualKeyShort.CONTROL, VirtualKeyShort.SHIFT, VirtualKeyShort.KEY_P);
            AutomationElement paletteBox = WaitForElement(
                window, "CommandPaletteSearch", TimeSpan.FromSeconds(10));
            Assert.True(
                SpinWait.SpinUntil(
                    () =>
                    {
                        try
                        {
                            return paletteBox.Properties.HasKeyboardFocus.ValueOrDefault;
                        }
                        catch (Exception exception) when (IsTransientUiaFault(exception))
                        {
                            return false;
                        }
                    },
                    TimeSpan.FromSeconds(10)),
                "the palette superseded search without taking focus");
            Assert.True(
                SpinWait.SpinUntil(
                    () =>
                    {
                        try
                        {
                            return window.FindFirstDescendant(automation.ConditionFactory
                                .ByAutomationId("SearchOverlay")) is null;
                        }
                        catch (Exception exception) when (IsTransientUiaFault(exception))
                        {
                            return true;
                        }
                    },
                    TimeSpan.FromSeconds(10)),
                "the search overlay is still in the control view beneath "
                + "the palette — SD-5 exclusivity did not close it, so a "
                + "UIA client can walk (and drive) a surface the palette "
                + "hides.");

            window.SetForeground();
            Keyboard.Press(VirtualKeyShort.ESCAPE);
            Assert.True(
                SpinWait.SpinUntil(
                    () =>
                    {
                        try
                        {
                            return window.FindFirstDescendant(automation.ConditionFactory
                                .ByAutomationId("CommandPalette")) is null;
                        }
                        catch (Exception exception) when (IsTransientUiaFault(exception))
                        {
                            return true;
                        }
                    },
                    TimeSpan.FromSeconds(10)),
                "Escape did not dismiss the palette after the supersession");

            // The lineage must LAND: dismissing the palette restores
            // the element focused before SEARCH opened — the whole
            // point of the consume-first handoff — asserted by runtime
            // id, not by "something has focus".
            Assert.True(
                SpinWait.SpinUntil(
                    () =>
                    {
                        try
                        {
                            int[]? focusedId = automation.FocusedElement()?
                                .Properties.RuntimeId.ValueOrDefault;
                            return focusedId is not null
                                && focusedId.SequenceEqual(parkedRuntimeId);
                        }
                        catch (Exception exception) when (IsTransientUiaFault(exception))
                        {
                            return false;
                        }
                    },
                    TimeSpan.FromSeconds(10)),
                "dismissing the palette did not restore focus to the "
                + "element from before search opened — the supersession "
                + "dropped the focus lineage (codex round 11).");

            window.SetForeground();
            PressChord(
                VirtualKeyShort.CONTROL, VirtualKeyShort.SHIFT, VirtualKeyShort.KEY_F);
            AutomationElement restoredSearch = WaitForElement(
                window, "SearchOverlaySearch", TimeSpan.FromSeconds(10));
            Assert.Equal(
                "meridian", restoredSearch.Patterns.Value.Pattern.Value.Value);
            _ = WaitForElement(
                window, "SearchOverlayResults", TimeSpan.FromSeconds(10));

            window.SetForeground();
            Keyboard.Press(VirtualKeyShort.ESCAPE);
            Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(250));

            // --- teardown: nothing of the overlay survives in the
            // control view, which is the view an AT walks --------------
            foreach (string id in new[]
            {
                "SearchOverlay",
                "SearchOverlaySearch",
                "SearchOverlayClose",
                "SearchOverlayResults",
                "SearchOverlayIdleHint",
                "SearchOverlayTagChip",
                "SearchOverlayClearTagScope",
                "SearchOverlayClearRecents",
                "SearchOverlaySearching",
                "SearchOverlayNoResults",
            })
            {
                Assert.True(
                    SpinWait.SpinUntil(
                        () =>
                        {
                            try
                            {
                                return window.FindFirstDescendant(
                                    automation.ConditionFactory
                                        .ByAutomationId(id)) is null;
                            }
                            catch (Exception exception) when (IsTransientUiaFault(exception))
                            {
                                return true;
                            }
                        },
                        TimeSpan.FromSeconds(5)),
                    $"{id} stayed in the UIA control view after the overlay closed");
            }
        }
        finally
        {
            if (process is not null && !process.HasExited)
            {
                process.CloseMainWindow();
                if (!process.WaitForExit(5_000))
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            try
            {
                Directory.Delete(testRoot, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    /// <summary>
    /// W5-3 (#743): create-from-template end to end. Chord opens the
    /// picker with focus on the first row; Esc cancels writing nothing;
    /// the full flow renders through core, creates exclusively, opens
    /// the note, and parks the caret at the template's `{{cursor}}` —
    /// proven by TYPING at the landed caret and reading the note's
    /// bytes back off disk after Ctrl+S (contracts T7/T8). Axe scans
    /// the picker, the prompt step, and the name step.
    /// </summary>
    [Fact]
    public void Templates_PickerPromptsCreateAndCancel_AreClean()
    {
        string testRoot = Path.Combine(
            Path.GetTempPath(), $"slate-templates-{Guid.NewGuid():N}");
        string vaultRoot = Path.Combine(testRoot, "Template Vault");
        string logDirectory = Path.Combine(testRoot, "logs");
        string templatesDir = Path.Combine(vaultRoot, "Templates");
        // The vault starts with NO templates folder: the journey's
        // first leg exercises the EMPTY picker state, then creates the
        // template on disk and lands Try Again — the availability
        // transition whose focus re-seat mac wires deliberately
        // (red team, a11y finding 1).
        Directory.CreateDirectory(vaultRoot);
        File.WriteAllText(
            Path.Combine(vaultRoot, "existing.md"),
            "# Existing\n\nAlready here.\n");

        Process? process = null;
        try
        {
            var startInfo = new ProcessStartInfo(SlateWindowsExe())
            {
                UseShellExecute = false,
            };
            startInfo.ArgumentList.Add(vaultRoot);
            startInfo.Environment["SLATE_CENSUS_INSTANCE_ID"] =
                $"slate-templates-{Guid.NewGuid():N}";
            startInfo.Environment["SLATE_LOG_DIR"] = logDirectory;
            process = Process.Start(startInfo)
                ?? throw new Xunit.Sdk.XunitException("SlateWindows.exe did not start.");

            if (!HasInteractiveDesktop(process, "templates"))
            {
                return;
            }

            using var automation = new UIA3Automation();
            Window window = WaitForMainWindow(
                process,
                automation,
                Path.Combine(logDirectory, "slate-windows.log"),
                TimeSpan.FromSeconds(30));
            window.SetForeground();
            window.Focus();

            // Wait for the VAULT, not merely the window (the palette
            // journey's recorded trap): the chord needs a workspace.
            _ = WaitForElement(window, "RightPaneLeaves", TimeSpan.FromSeconds(30));
            string[] filesBefore = VaultUserFiles(vaultRoot);

            // --- open by chord on a template-less vault: the EMPTY
            // state, with focus on Try Again ---------------------------
            WaitForElement(window, "FilesTree", TimeSpan.FromSeconds(10)).Focus();
            Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(250));
            window.SetForeground();
            PressChord(
                VirtualKeyShort.CONTROL, VirtualKeyShort.SHIFT, VirtualKeyShort.KEY_N);

            AutomationElement picker = WaitForElement(
                window, "TemplatePickerSheet", TimeSpan.FromSeconds(10));
            Assert.Equal("Choose a template", picker.Name);
            // The destination subtitle is REAL UIA content — an AT user
            // must be able to learn where the note will be created
            // (red team, a11y finding 2).
            AutomationElement subtitle = WaitForElement(
                window, "TemplatePickerSubtitle", TimeSpan.FromSeconds(10));
            Assert.Equal(
                "Create in the vault root. Ctrl+Shift+N. Escape to cancel.",
                subtitle.Name);
            AutomationElement tryAgain = WaitForElement(
                window, "TemplatePickerTryAgain", TimeSpan.FromSeconds(10));
            Assert.True(
                SpinWait.SpinUntil(
                    () =>
                    {
                        try
                        {
                            return tryAgain.Properties.HasKeyboardFocus.ValueOrDefault;
                        }
                        catch (Exception exception) when (IsTransientUiaFault(exception))
                        {
                            return false;
                        }
                    },
                    TimeSpan.FromSeconds(10)),
                "the empty picker did not focus Try Again (T3).");

            // --- the template appears on disk; Try Again lands
            // Available and RE-SEATS focus on the first row — the
            // availability transition that evicted keyboard focus from
            // the modal before the picker-state observer existed ------
            // One prompt to complete via the Value pattern, one left
            // untouched (an empty answer substitutes EMPTY, T4), a
            // multibyte char before {{cursor}} so the caret leg pins
            // the UTF-8→UTF-16 conversion end to end, and a description
            // for the composed row name.
            Directory.CreateDirectory(templatesDir);
            File.WriteAllText(
                Path.Combine(templatesDir, "Meeting.md"),
                "---\ndescription: Journey fixture\n---\n# {{title}}\n\n"
                + "Topic: {{prompt:Topic}}\nNotes: {{prompt:Extra}}\n\nCafé {{cursor}}end\n");
            window.SetForeground();
            PressKey(VirtualKeyShort.ENTER);

            AutomationElement pickerList = WaitForElement(
                window, "TemplatePickerList", TimeSpan.FromSeconds(10));
            AutomationElement? firstRow = null;
            Assert.True(
                SpinWait.SpinUntil(
                    () =>
                    {
                        try
                        {
                            firstRow = pickerList.FindFirstDescendant(
                                automation.ConditionFactory
                                    .ByControlType(ControlType.ListItem));
                            return firstRow?.Properties
                                .HasKeyboardFocus.ValueOrDefault == true;
                        }
                        catch (Exception exception) when (IsTransientUiaFault(exception))
                        {
                            return false;
                        }
                    },
                    TimeSpan.FromSeconds(10)),
                "the Try Again landing did not re-seat focus on the first row "
                + "(red team a11y finding 1 — focus was evicted from the modal).");
            // The composed row name: "{name}. {description}." (mac's
            // rowAccessibilityLabel, T3).
            Assert.Equal("Meeting. Journey fixture.", firstRow!.Name);

            AssertAxeClean(process, "template-picker");

            // --- Esc cancels the picker; nothing is written (T7) ------
            string[] filesBeforeCancel = VaultUserFiles(vaultRoot);
            window.SetForeground();
            Keyboard.Press(VirtualKeyShort.ESCAPE);
            Assert.True(
                SpinWait.SpinUntil(
                    () =>
                    {
                        try
                        {
                            return window.FindFirstDescendant(automation.ConditionFactory
                                .ByAutomationId("TemplatePickerSheet")) is null;
                        }
                        catch (Exception exception) when (IsTransientUiaFault(exception))
                        {
                            return true;
                        }
                    },
                    TimeSpan.FromSeconds(10)),
                "Escape did not close the template picker");
            Assert.Equal(filesBeforeCancel, VaultUserFiles(vaultRoot));
            // The pre-template snapshot differs only by the template
            // file this journey itself wrote — the app wrote nothing.
            Assert.Equal(filesBefore.Length + 1, filesBeforeCancel.Length);

            // --- Esc at the PROMPT STEP cancels the whole flow too
            // (T7 promises Esc at every step; red team, tests
            // finding 9) -----------------------------------------------
            window.SetForeground();
            PressChord(
                VirtualKeyShort.CONTROL, VirtualKeyShort.SHIFT, VirtualKeyShort.KEY_N);
            _ = WaitForElement(window, "TemplatePickerSheet", TimeSpan.FromSeconds(10));
            Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(250));
            window.SetForeground();
            PressKey(VirtualKeyShort.ENTER);
            _ = WaitForElement(window, "TemplateFlowSheet", TimeSpan.FromSeconds(10));
            window.SetForeground();
            Keyboard.Press(VirtualKeyShort.ESCAPE);
            Assert.True(
                SpinWait.SpinUntil(
                    () =>
                    {
                        try
                        {
                            return window.FindFirstDescendant(automation.ConditionFactory
                                .ByAutomationId("TemplateFlowSheet")) is null;
                        }
                        catch (Exception exception) when (IsTransientUiaFault(exception))
                        {
                            return true;
                        }
                    },
                    TimeSpan.FromSeconds(10)),
                "Escape did not cancel the flow from the prompt step");
            Assert.Equal(filesBeforeCancel, VaultUserFiles(vaultRoot));

            // --- reopen, activate the row, complete a prompt ----------
            window.SetForeground();
            PressChord(
                VirtualKeyShort.CONTROL, VirtualKeyShort.SHIFT, VirtualKeyShort.KEY_N);
            _ = WaitForElement(window, "TemplatePickerSheet", TimeSpan.FromSeconds(10));
            Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(250));
            window.SetForeground();
            PressKey(VirtualKeyShort.ENTER);

            AutomationElement flow = WaitForElement(
                window, "TemplateFlowSheet", TimeSpan.FromSeconds(10));
            Assert.Equal("Create from template", flow.Name);

            // The prompt step: fields in declaration order, first
            // focused, labelled with the author's text (T4). Value
            // pattern, not synthetic keystrokes (the recorded journey
            // trap).
            AutomationElement? topicBox = null;
            Assert.True(
                SpinWait.SpinUntil(
                    () =>
                    {
                        try
                        {
                            topicBox = flow.FindAllDescendants(
                                    automation.ConditionFactory
                                        .ByControlType(ControlType.Edit))
                                .FirstOrDefault(box => box.Name == "Topic");
                            return topicBox?.Properties
                                .HasKeyboardFocus.ValueOrDefault == true;
                        }
                        catch (Exception exception) when (IsTransientUiaFault(exception))
                        {
                            return false;
                        }
                    },
                    TimeSpan.FromSeconds(10)),
                "the prompt step did not focus its first field (T4).");
            topicBox!.Patterns.Value.Pattern.SetValue("Quarterly sync");

            AssertAxeClean(process, "template-prompts");

            window.SetForeground();
            PressKey(VirtualKeyShort.ENTER);

            // The name step: seeded with "{template}.md", focused (T6).
            AutomationElement nameBox = WaitForElement(
                window, "TemplateFlowName", TimeSpan.FromSeconds(10));
            Assert.True(
                SpinWait.SpinUntil(
                    () =>
                    {
                        try
                        {
                            return nameBox.Properties
                                .HasKeyboardFocus.ValueOrDefault;
                        }
                        catch (Exception exception) when (IsTransientUiaFault(exception))
                        {
                            return false;
                        }
                    },
                    TimeSpan.FromSeconds(10)),
                "the name step did not focus the name field (T6).");
            Assert.Equal("Meeting.md", nameBox.Patterns.Value.Pattern.Value.Value);

            AssertAxeClean(process, "template-name");

            nameBox.Patterns.Value.Pattern.SetValue("Journey Note");
            window.SetForeground();
            PressKey(VirtualKeyShort.ENTER);

            // --- the create: sheet closes, the note opens -------------
            Assert.True(
                SpinWait.SpinUntil(
                    () =>
                    {
                        try
                        {
                            return window.FindFirstDescendant(automation.ConditionFactory
                                .ByAutomationId("TemplateFlowSheet")) is null;
                        }
                        catch (Exception exception) when (IsTransientUiaFault(exception))
                        {
                            return true;
                        }
                    },
                    TimeSpan.FromSeconds(10)),
                "the flow sheet stayed open after Create");

            AutomationElement editor = WaitForEditor(
                window,
                automation,
                "Journey Note.md editor",
                TimeSpan.FromSeconds(10));

            // Focus follows content (T8): the editor holds the keyboard.
            Assert.True(
                SpinWait.SpinUntil(
                    () =>
                    {
                        try
                        {
                            return editor.Properties.HasKeyboardFocus.ValueOrDefault;
                        }
                        catch (Exception exception) when (IsTransientUiaFault(exception))
                        {
                            return false;
                        }
                    },
                    TimeSpan.FromSeconds(10)),
                "the created note's editor did not take keyboard focus (T8).");

            // --- the caret leg: TYPE at the landed caret, save, and
            // read the bytes back — the {{cursor}} offset is proven by
            // where the typed marker lands on DISK, after the multibyte
            // 'é' that makes byte-vs-UTF-16 confusion visible ----------
            window.SetForeground();
            Keyboard.Type("MARK");
            Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(250));
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_S);

            string createdPath = Path.Combine(vaultRoot, "Journey Note.md");
            string saved = string.Empty;
            Assert.True(
                SpinWait.SpinUntil(
                    () =>
                    {
                        try
                        {
                            saved = File.ReadAllText(createdPath);
                            return saved.Contains("MARK", StringComparison.Ordinal);
                        }
                        catch (IOException)
                        {
                            return false;
                        }
                    },
                    TimeSpan.FromSeconds(10)),
                "the typed caret marker never reached the note on disk.");
            // The template's own frontmatter renders through into the
            // note (that is how template frontmatter seeds properties;
            // mac-identical), and MARK sits exactly where {{cursor}}
            // stood — after the multibyte 'é'.
            Assert.Equal(
                "---\ndescription: Journey fixture\n---\n# Journey Note\n\n"
                + "Topic: Quarterly sync\nNotes: \n\nCafé MARKend\n",
                saved);
        }
        finally
        {
            if (process is not null && !process.HasExited)
            {
                process.CloseMainWindow();
                if (!process.WaitForExit(5_000))
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            try
            {
                Directory.Delete(testRoot, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    /// <summary>
    /// W5-4 (#744): file management end to end —
    /// create → rename → duplicate → move → delete, with disk
    /// verification at every step. The create hands off to inline
    /// rename with the stem selected (F1); Duplicate walks the Finder
    /// namer (F5); Move To… stages the picker sheet whose typed filter
    /// IS the New Folder path — create-then-move in one gesture (F4);
    /// the tree-scoped Delete key trashes a file with no confirmation
    /// (F6, Finder parity). Axe scans the sidebar's file-actions
    /// surface and the Move-To picker.
    /// </summary>
    [Fact]
    public void FileManagement_CreateRenameDuplicateMoveDelete_IsClean()
    {
        string testRoot = Path.Combine(
            Path.GetTempPath(), $"slate-filemgmt-{Guid.NewGuid():N}");
        string vaultRoot = Path.Combine(testRoot, "File Management Vault");
        string logDirectory = Path.Combine(testRoot, "logs");
        Directory.CreateDirectory(vaultRoot);
        File.WriteAllText(
            Path.Combine(vaultRoot, "existing.md"),
            "# Existing\n\nAlready here.\n");

        Process? process = null;
        try
        {
            var startInfo = new ProcessStartInfo(SlateWindowsExe())
            {
                UseShellExecute = false,
            };
            startInfo.ArgumentList.Add(vaultRoot);
            startInfo.Environment["SLATE_CENSUS_INSTANCE_ID"] =
                $"slate-filemgmt-{Guid.NewGuid():N}";
            startInfo.Environment["SLATE_LOG_DIR"] = logDirectory;
            process = Process.Start(startInfo)
                ?? throw new Xunit.Sdk.XunitException("SlateWindows.exe did not start.");

            if (!HasInteractiveDesktop(process, "file management"))
            {
                return;
            }

            using var automation = new UIA3Automation();
            Window window = WaitForMainWindow(
                process,
                automation,
                Path.Combine(logDirectory, "slate-windows.log"),
                TimeSpan.FromSeconds(30));
            window.SetForeground();
            window.Focus();
            _ = WaitForElement(window, "RightPaneLeaves", TimeSpan.FromSeconds(30));

            // --- create: chordless verb via the File actions button;
            // the untitled note lands and the flow hands off to inline
            // rename with the stem selected (F1) ----------------------
            AutomationElement fileActions = WaitForElement(
                window, "SidebarFileActions", TimeSpan.FromSeconds(10));
            fileActions.Patterns.ExpandCollapse.Pattern.Expand();
            AutomationElement newNote = WaitForButton(
                fileActions, automation, "New note");
            newNote.Patterns.Invoke.Pattern.Invoke();
            Assert.True(
                SpinWait.SpinUntil(
                    () => File.Exists(Path.Combine(vaultRoot, "Untitled.md")),
                    TimeSpan.FromSeconds(10)),
                "the untitled create never reached the disk (F1).");

            AutomationElement nameBox = WaitForElement(
                window, "SidebarMutationName", TimeSpan.FromSeconds(10));
            Assert.True(
                SpinWait.SpinUntil(
                    () =>
                    {
                        try
                        {
                            return nameBox.Properties.HasKeyboardFocus.ValueOrDefault;
                        }
                        catch (Exception exception) when (IsTransientUiaFault(exception))
                        {
                            return false;
                        }
                    },
                    TimeSpan.FromSeconds(10)),
                "the create did not hand off to inline rename (F1).");

            // The stem is selected; typing replaces it, keeping ".md".
            Keyboard.Type("journey");
            Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(250));
            PressKey(VirtualKeyShort.ENTER);
            Assert.True(
                SpinWait.SpinUntil(
                    () => File.Exists(Path.Combine(vaultRoot, "journey.md"))
                        && !File.Exists(Path.Combine(vaultRoot, "Untitled.md")),
                    TimeSpan.FromSeconds(10)),
                "the inline-rename commit never landed on disk (F1/F3).");

            AssertAxeClean(process, "file-management-sidebar");

            // --- duplicate: the Finder namer (F5) --------------------
            SelectTreeItem(window, automation, "journey.md");
            AutomationElement duplicate = WaitForButton(
                fileActions, automation, "Duplicate");
            duplicate.Patterns.Invoke.Pattern.Invoke();
            Assert.True(
                SpinWait.SpinUntil(
                    () => File.Exists(Path.Combine(vaultRoot, "journey copy.md")),
                    TimeSpan.FromSeconds(10)),
                "the duplicate never landed as 'journey copy.md' (F5).");

            // --- move: the picker sheet; the typed filter IS the New
            // Folder path — create then move, one gesture (F4) --------
            SelectTreeItem(window, automation, "journey copy.md");
            AutomationElement moveTo = WaitForButton(
                fileActions, automation, "Move to…");
            Assert.True(moveTo.IsEnabled, "the Move to… button is disabled.");
            moveTo.Patterns.Invoke.Pattern.Invoke();
            AutomationElement sheet;
            try
            {
                sheet = WaitForElement(
                    window, "MoveToSheet", TimeSpan.FromSeconds(10));
            }
            catch (Xunit.Sdk.XunitException)
            {
                // Tolerant read: the app holds the log open (the Bases
                // journey's recorded sharing-violation pattern).
                string log = Path.Combine(logDirectory, "slate-windows.log");
                string tail = "(no log)";
                try
                {
                    using var stream = new FileStream(
                        log, FileMode.Open, FileAccess.Read,
                        FileShare.ReadWrite | FileShare.Delete);
                    using var reader = new StreamReader(stream);
                    tail = string.Join(
                        " | ",
                        reader.ReadToEnd().Split('\n').TakeLast(8));
                }
                catch (IOException)
                {
                }

                throw new Xunit.Sdk.XunitException(
                    $"MoveToSheet never appeared; exited={process.HasExited}; "
                    + $"log tail: {tail}");
            }
            Assert.Equal("Move to folder", sheet.Name);
            AutomationElement filter = WaitForElement(
                window, "MoveToFilter", TimeSpan.FromSeconds(10));
            Assert.True(
                SpinWait.SpinUntil(
                    () =>
                    {
                        try
                        {
                            return filter.Properties.HasKeyboardFocus.ValueOrDefault;
                        }
                        catch (Exception exception) when (IsTransientUiaFault(exception))
                        {
                            return false;
                        }
                    },
                    TimeSpan.FromSeconds(10)),
                "the Move-To sheet did not focus its filter box (F4).");

            AssertAxeClean(process, "move-to-picker");

            Keyboard.Type("arch");
            Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(250));
            PressKey(VirtualKeyShort.ENTER);
            Assert.True(
                SpinWait.SpinUntil(
                    () => File.Exists(
                        Path.Combine(vaultRoot, "arch", "journey copy.md")),
                    TimeSpan.FromSeconds(10)),
                "the create-then-move gesture never landed (F4).");
            Assert.True(
                SpinWait.SpinUntil(
                    () =>
                    {
                        try
                        {
                            return window.FindFirstDescendant(
                                automation.ConditionFactory
                                    .ByAutomationId("MoveToSheet")) is null;
                        }
                        catch (Exception exception) when (IsTransientUiaFault(exception))
                        {
                            return true;
                        }
                    },
                    TimeSpan.FromSeconds(10)),
                "the Move-To sheet did not close after the move (F4).");

            // --- delete: the tree-scoped Delete key; a FILE trashes
            // immediately, no confirmation (F6, Finder parity) --------
            AutomationElement journeyItem =
                SelectTreeItem(window, automation, "journey.md");
            journeyItem.Focus();
            AssertEventuallyFocused(
                journeyItem, "the journey.md TreeItem could not receive focus.");
            PressKey(VirtualKeyShort.DELETE);
            Assert.True(
                SpinWait.SpinUntil(
                    () => !File.Exists(Path.Combine(vaultRoot, "journey.md")),
                    TimeSpan.FromSeconds(10)),
                "the Delete key never trashed the file (F6/FD-4).");

            // Terminal disk state: the original, and the moved copy in
            // its typed folder.
            Assert.True(File.Exists(Path.Combine(vaultRoot, "existing.md")));
            Assert.True(
                File.Exists(Path.Combine(vaultRoot, "arch", "journey copy.md")));
        }
        finally
        {
            if (process is not null && !process.HasExited)
            {
                process.CloseMainWindow();
                if (!process.WaitForExit(5_000))
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            try
            {
                Directory.Delete(testRoot, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    /// <summary>Find a named Button under a root (the sidebar's
    /// file-actions buttons carry no AutomationIds — their labels are
    /// the accessible names).</summary>
    private static AutomationElement WaitForButton(
        AutomationElement root, UIA3Automation automation, string name)
    {
        AutomationElement? button = null;
        if (!SpinWait.SpinUntil(
            () =>
            {
                try
                {
                    button = root.FindAllDescendants(
                            automation.ConditionFactory.ByControlType(ControlType.Button))
                        .FirstOrDefault(candidate => candidate.Name == name);
                    return button is not null;
                }
                catch (Exception exception) when (IsTransientUiaFault(exception))
                {
                    return false;
                }
            },
            TimeSpan.FromSeconds(10)))
        {
            throw new Xunit.Sdk.XunitException($"button '{name}' never appeared.");
        }

        return button!;
    }

    /// <summary>
    /// Whether an exception is the transient "the element went away
    /// underneath me" fault every tolerant poll in this file means to
    /// retry on.
    /// </summary>
    /// <remarks>
    /// UIA reports a vanished provider as <see cref="COMException"/>,
    /// but FlaUI WRAPS it: <c>Com.Call</c> converts the underlying
    /// <c>0x80040201</c> into
    /// <c>FlaUI.Core.Exceptions.ElementNotAvailableException</c>, which
    /// does not derive from <see cref="COMException"/> at all. Every
    /// poll here was written as <c>catch (COMException)</c> and so
    /// missed the wrapper — the exact condition it existed for escaped
    /// the retry and failed the run. Measured as the second, distinct
    /// FileManagement journey flake on CI (the first being #1136):
    /// <c>SelectTreeItem</c>'s <c>Select()</c> landing on a container
    /// the tree refresh had just replaced.
    /// </remarks>
    private static bool IsTransientUiaFault(Exception exception) =>
        exception is COMException
            or FlaUI.Core.Exceptions.ElementNotAvailableException
            or FlaUI.Core.Exceptions.PropertyNotSupportedException
        || (exception is FlaUI.Core.Exceptions.FlaUIException
            && exception.InnerException is COMException);

    /// <summary>Select a FilesTree item by name via the SelectionItem
    /// pattern. The item is RE-FOUND each attempt and the selection is
    /// verified to stick: a mutation's tree refresh republishes node
    /// containers, and a Select() landing on a container the refresh
    /// is about to replace registers on nothing (the journey's
    /// recorded flake).</summary>
    private static AutomationElement SelectTreeItem(
        Window window, UIA3Automation automation, string name)
    {
        AutomationElement filesTree = WaitForElement(
            window, "FilesTree", TimeSpan.FromSeconds(10));
        AutomationElement? item = null;
        if (!SpinWait.SpinUntil(
            () =>
            {
                try
                {
                    item = filesTree.FindAllDescendants(
                            automation.ConditionFactory
                                .ByControlType(ControlType.TreeItem))
                        .FirstOrDefault(candidate => candidate.Name.StartsWith(
                            name, StringComparison.Ordinal));
                    if (item is null)
                    {
                        return false;
                    }

                    item.Patterns.SelectionItem.Pattern.Select();
                    Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(100));
                    return item.Patterns.SelectionItem.Pattern.IsSelected;
                }
                catch (Exception exception) when (IsTransientUiaFault(exception))
                {
                    return false;
                }
            },
            TimeSpan.FromSeconds(10)))
        {
            throw new Xunit.Sdk.XunitException(
                $"tree item '{name}' never appeared or would not stay selected.");
        }

        return item!;
    }

    /// <summary>Every user-visible vault file with its content — the
    /// nothing-was-written snapshot for the cancel legs. ALL files,
    /// not just Markdown; only the `.slate` index directory is carved
    /// out (reads legitimately touch it), so the claim the cancel legs
    /// make is honest (red team, tests finding 9).</summary>
    private static string[] VaultUserFiles(string vaultRoot) =>
        [.. Directory
            .EnumerateFiles(vaultRoot, "*", SearchOption.AllDirectories)
            .Where(path => !path
                .Replace('\\', '/')
                .Contains("/.slate/", StringComparison.Ordinal))
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path => $"{path}|{File.ReadAllText(path)}")];

    /// <summary>
    /// Whether a chord can actually be delivered to the app, or is held
    /// globally by another process.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Ctrl+Alt+{Left,Right,Up,Down} are registered process-wide by
    /// <b>Windows Magnifier</b>, which uses them to pan the magnified
    /// view — and, on developer boxes, also by AMD Radeon Software, the
    /// NVIDIA container and Parsec. The app then never receives the
    /// chord, and the resulting assertion reads exactly like a product
    /// defect. It has been re-diagnosed more than once, and cost a
    /// disagreement between two review rounds over whether the suite was
    /// 17/18 or 18/18 — Magnifier being toggled on and off mid-session is
    /// exactly why the same probe answered FREE and then TAKEN within a
    /// day.
    /// </para>
    /// <para>
    /// RegisterHotKey returning ERROR_HOTKEY_ALREADY_REGISTERED (1409) is
    /// the definitive test, so the suite now runs it instead of leaving a
    /// human to remember. Success registers the chord for an instant and
    /// releases it immediately.
    /// </para>
    /// </remarks>
    private static bool ChordIsDeliverable(uint modifiers, uint virtualKey)
    {
        // A fresh id per probe. A fixed one collides with ITSELF if two
        // probes overlap, and a self-collision reports 1409 — the suite
        // would then blame Magnifier for a chord that is perfectly free.
        int probeId = System.Threading.Interlocked.Increment(ref _chordProbeId);
        if (NativeHotkey.RegisterHotKey(IntPtr.Zero, probeId, modifiers, virtualKey))
        {
            NativeHotkey.UnregisterHotKey(IntPtr.Zero, probeId);
            return true;
        }

        // Only 1409 means another process holds the chord. The comment
        // above always said so; the code did not, and treated EVERY
        // failure as a steal — so an unrelated Win32 error would have been
        // reported as a Magnifier collision, which is the one diagnosis
        // this probe exists to stop people guessing at. Codoki caught the
        // gap between the two.
        int error = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
        Assert.True(
            error == ErrorHotkeyAlreadyRegistered,
            $"RegisterHotKey failed with Win32 error {error}, which is not "
            + $"ERROR_HOTKEY_ALREADY_REGISTERED ({ErrorHotkeyAlreadyRegistered}). "
            + "The probe cannot say whether the chord is deliverable, and "
            + "silently skipping the leg here would look like a hotkey steal.");
        return false;
    }

    /// <summary>
    /// W6-1 §E TE-11e (E14/E19, the journeys): the AUTHORING journey,
    /// end to end through the real chrome — File menu New Canvas into
    /// its own tab, the onboarding region carrying core's chord
    /// sentence, Ctrl+Alt+N creating the first card, activation opening
    /// the card editor sheet (axe-scanned), Escape COMMITTING the
    /// draft (t0 §2 M8), and Ctrl+Z undoing through the history domain
    /// — every leg a real keystroke or UIA invoke, no test seams.
    /// </summary>
    [Fact]
    public void CanvasAuthoring_NewCanvasCardEditorAndUndo_AreReachable()
    {
        string testRoot = Path.Combine(
            Path.GetTempPath(), $"slate-canvas-authoring-{Guid.NewGuid():N}");
        string vaultRoot = Path.Combine(testRoot, "Authoring Vault");
        string logDirectory = Path.Combine(testRoot, "logs");
        Directory.CreateDirectory(vaultRoot);
        // Deliberately NO canvas: the journey CREATES one.
        File.WriteAllText(
            Path.Combine(vaultRoot, "note.md"), "# Note\n\nBody.\n");

        Process? process = null;
        try
        {
            var startInfo = new ProcessStartInfo(SlateWindowsExe())
            {
                UseShellExecute = false,
            };
            startInfo.ArgumentList.Add(vaultRoot);
            startInfo.Environment["SLATE_CENSUS_INSTANCE_ID"] =
                $"slate-canvas-authoring-{Guid.NewGuid():N}";
            startInfo.Environment["SLATE_LOG_DIR"] = logDirectory;
            process = Process.Start(startInfo)
                ?? throw new Xunit.Sdk.XunitException("SlateWindows.exe did not start.");

            if (!HasInteractiveDesktop(process, "Canvas authoring"))
            {
                return;
            }

            using var automation = new UIA3Automation();
            Window window = WaitForMainWindow(
                process,
                automation,
                Path.Combine(logDirectory, "slate-windows.log"),
                TimeSpan.FromSeconds(30));
            window.SetForeground();
            window.Focus();

            // ---- New Canvas from the File menu (TE-10) --------------
            AutomationElement newCanvas = WaitForMenuItem(
                window, "FileMenu", "NewCanvasMenuItem", TimeSpan.FromSeconds(10));
            Assert.True(newCanvas.Patterns.Invoke.IsSupported);
            newCanvas.Patterns.Invoke.Pattern.Invoke();

            // The new document opened in its OWN tab and the onboarding
            // region leads with the REAL chord (E14's first swap).
            AutomationElement onboarding = WaitForElement(
                window, "CanvasEmptyOnboarding", TimeSpan.FromSeconds(20));
            Assert.Contains(
                "Control Alt N",
                onboarding.Properties.Name.Value,
                StringComparison.Ordinal);

            // ---- The first card, by chord (E19) ---------------------
            // Every republish rebuilds the tree's items, so a cached
            // element handle goes stale mid-walk (COM E_UNEXPECTED).
            // Probe FRESH each time and treat staleness as "not yet".
            AutomationElement[] Rows()
            {
                try
                {
                    AutomationElement? liveTree = window.FindFirstDescendant(
                        automation.ConditionFactory.ByAutomationId(
                            "CanvasOutlineTree"));
                    return liveTree is null
                        ? []
                        : liveTree.FindAllDescendants(
                            automation.ConditionFactory.ByControlType(
                                ControlType.TreeItem));
                }
                catch (System.Runtime.InteropServices.COMException)
                {
                    return [];
                }
            }

            AutomationElement tree = WaitForElement(
                window, "CanvasOutlineTree", TimeSpan.FromSeconds(10));
            tree.Focus();
            Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(250));
            Keyboard.TypeSimultaneously(
                VirtualKeyShort.CONTROL, VirtualKeyShort.ALT, VirtualKeyShort.KEY_N);
            AutomationElement? card = null;
            Assert.True(
                SpinWait.SpinUntil(
                    () =>
                    {
                        card = Rows().FirstOrDefault(item =>
                            item.Properties.Name.ValueOrDefault?.StartsWith(
                                "Text card ", StringComparison.Ordinal) == true);
                        return card is not null;
                    },
                    TimeSpan.FromSeconds(15)),
                "Ctrl+Alt+N never produced a text card row; exited="
                + process.HasExited
                + "; app log tail: "
                + ReadSharedLog(Path.Combine(logDirectory, "slate-windows.log")));

            // ---- Activation opens the editor sheet (TE-11b) ---------
            card!.Patterns.SelectionItem.Pattern.Select();
            card.Focus();
            Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(250));
            Keyboard.Press(VirtualKeyShort.ENTER);
            AutomationElement sheet = WaitForElement(
                window, "CanvasCardEditorSheet", TimeSpan.FromSeconds(10));
            AssertAxeClean(process, "canvas-card-editor");

            AutomationElement draft = sheet
                .FindAllDescendants(
                    automation.ConditionFactory.ByControlType(ControlType.Edit))
                .FirstOrDefault(edit => edit.Patterns.Value.IsSupported)
                ?? throw new Xunit.Sdk.XunitException(
                    "the card editor sheet exposes no editable draft");
            // REAL keystrokes, not the automation pattern: WPF's peer
            // SetValue writes Text without composing through the input
            // stack, and the journey's premise is the keyboard.
            draft.Focus();
            AssertEventuallyFocused(draft, "the draft box could not take focus.");
            Keyboard.Type("Hello from the journey");
            Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(250));
            Assert.True(
                SpinWait.SpinUntil(
                    () => draft.Patterns.Value.Pattern.Value.ValueOrDefault
                        == "Hello from the journey",
                    TimeSpan.FromSeconds(5)),
                "SetValue never landed in the draft box; value now: '"
                + draft.Patterns.Value.Pattern.Value.ValueOrDefault
                + "'; box id: "
                + draft.Properties.AutomationId.ValueOrDefault);

            // Escape COMMITS (t0 §2 M8) — the sheet closes and the row
            // carries the new title.
            draft.Focus();
            Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(250));
            Keyboard.Press(VirtualKeyShort.ESCAPE);
            Assert.True(
                SpinWait.SpinUntil(
                    () => Rows().Any(item =>
                        item.Properties.Name.ValueOrDefault?.Contains(
                            "Hello from the journey", StringComparison.Ordinal) == true),
                    TimeSpan.FromSeconds(15)),
                "the committed draft never reached the outline row; sheetOpen="
                + (window.FindFirstDescendant(
                    automation.ConditionFactory.ByAutomationId(
                        "CanvasCardEditorSheet")) is not null)
                + "; rows=["
                + string.Join(
                    " | ",
                    Rows().Select(r => r.Properties.Name.ValueOrDefault ?? "?"))
                + "]; disk: "
                + string.Join(
                    " / ",
                    Directory.EnumerateFiles(vaultRoot, "*.canvas")
                        .Select(f => Path.GetFileName(f) + "=" + File.ReadAllText(f)))
                + "; log: "
                + ReadSharedLog(Path.Combine(logDirectory, "slate-windows.log")));

            // ---- Ctrl+Z drives the history domain (ED-1) ------------
            // The republish rebuilt the tree; focus the ROW (the
            // pre-commit leg's own idiom - a bare tree SetFocus is
            // refused while the container is mid-layout).
            AutomationElement? committedRow = null;
            Assert.True(
                SpinWait.SpinUntil(
                    () =>
                    {
                        committedRow = Rows().FirstOrDefault();
                        return committedRow is not null;
                    },
                    TimeSpan.FromSeconds(10)),
                "no outline row to focus for the undo leg");
            committedRow!.Patterns.SelectionItem.Pattern.Select();
            committedRow.Focus();
            Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(250));
            Keyboard.TypeSimultaneously(
                VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
            Assert.True(
                SpinWait.SpinUntil(
                    () =>
                    {
                        AutomationElement[] rows = Rows();
                        return rows.Length > 0 && rows.All(item =>
                            item.Properties.Name.ValueOrDefault?.Contains(
                                "Hello from the journey",
                                StringComparison.Ordinal) != true);
                    },
                    TimeSpan.FromSeconds(15)),
                "Ctrl+Z did not undo the committed edit");
        }
        finally
        {
            if (process is not null && !process.HasExited)
            {
                process.CloseMainWindow();
                if (!process.WaitForExit(5_000))
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            try
            {
                Directory.Delete(testRoot, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }


    /// <summary>§G TG-7 (G9): the MARKS journey — a real keyboard marks
    /// two cards by chord with the rows' marked state visible to UIA,
    /// opens the marks list from the palette (sheet + axe), Jumps to a
    /// row and lands reader focus on it, deletes the marked set in one
    /// palette action, and undoes it in one chord. One launch, every
    /// leg in sequence, the journey traps honored.</summary>
    [Fact]
    [Trait("gate", "W-C")]
    public void CanvasMarks_ToggleListJumpDeleteAndUndo_AreReachable()
    {
        string testRoot = Path.Combine(
            Path.GetTempPath(), $"slate-canvas-marks-{Guid.NewGuid():N}");
        string vaultRoot = Path.Combine(testRoot, "Marks Vault");
        string logDirectory = Path.Combine(testRoot, "logs");
        Directory.CreateDirectory(vaultRoot);
        File.WriteAllText(
            Path.Combine(vaultRoot, "note.md"), "# Note\n\nBody.\n");

        Process? process = null;
        try
        {
            var startInfo = new ProcessStartInfo(SlateWindowsExe())
            {
                UseShellExecute = false,
            };
            startInfo.ArgumentList.Add(vaultRoot);
            startInfo.Environment["SLATE_CENSUS_INSTANCE_ID"] =
                $"slate-canvas-marks-{Guid.NewGuid():N}";
            startInfo.Environment["SLATE_LOG_DIR"] = logDirectory;
            process = Process.Start(startInfo)
                ?? throw new Xunit.Sdk.XunitException("SlateWindows.exe did not start.");

            if (!HasInteractiveDesktop(process, "Canvas marks"))
            {
                return;
            }

            using var automation = new UIA3Automation();
            Window window = WaitForMainWindow(
                process,
                automation,
                Path.Combine(logDirectory, "slate-windows.log"),
                TimeSpan.FromSeconds(30));
            window.SetForeground();
            window.Focus();

            // ---- A canvas with two cards, by the authoring path ----
            AutomationElement newCanvas = WaitForMenuItem(
                window, "FileMenu", "NewCanvasMenuItem", TimeSpan.FromSeconds(10));
            newCanvas.Patterns.Invoke.Pattern.Invoke();
            // The first canvas open on a fresh process is the slowest step
            // of the journey on a loaded runner (the previous journey's
            // app may still be tearing down); wait longer than the modes
            // journey does and, on a miss, say what the app logged so the
            // next miss is diagnosable rather than a bare timeout.
            AutomationElement? tree = TryWaitForElement(
                window, "CanvasOutlineTree", TimeSpan.FromSeconds(60));
            if (tree is null)
            {
                string log = ReadSharedLog(
                    Path.Combine(logDirectory, "slate-windows.log"));
                Assert.Fail(
                    "CanvasOutlineTree never appeared after New Canvas; app log tail: "
                    + (log.Length > 3000 ? log[^3000..] : log));
            }
            tree.Focus();
            Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(250));

            AutomationElement[] Rows()
            {
                try
                {
                    AutomationElement? liveTree = window.FindFirstDescendant(
                        automation.ConditionFactory.ByAutomationId(
                            "CanvasOutlineTree"));
                    return liveTree is null
                        ? []
                        : liveTree.FindAllDescendants(
                            automation.ConditionFactory.ByControlType(
                                ControlType.TreeItem));
                }
                catch (System.Runtime.InteropServices.COMException)
                {
                    return [];
                }
            }

            int MarkedRows()
            {
                try
                {
                    return Rows().Count(item =>
                        item.Properties.ItemStatus.ValueOrDefault?.Contains(
                            "marked", StringComparison.OrdinalIgnoreCase) == true);
                }
                catch (System.Runtime.InteropServices.COMException)
                {
                    return -1;
                }
            }

            void RunPaletteCommand(string query, string rowPrefix)
            {
                window.SetForeground();
                PressChord(
                    VirtualKeyShort.CONTROL, VirtualKeyShort.SHIFT, VirtualKeyShort.KEY_P);
                AutomationElement search = WaitForElement(
                    window, "CommandPaletteSearch", TimeSpan.FromSeconds(10));
                search.Patterns.Value.Pattern.SetValue(query);
                AutomationElement results = WaitForElement(
                    window, "CommandPaletteResults", TimeSpan.FromSeconds(10));
                AutomationElement? row = null;
                bool found = SpinWait.SpinUntil(
                    () =>
                    {
                        try
                        {
                            row = results
                                .FindAllDescendants(automation.ConditionFactory
                                    .ByControlType(ControlType.ListItem))
                                .FirstOrDefault(item => item.Name.StartsWith(
                                    rowPrefix, StringComparison.Ordinal));
                            return row is not null;
                        }
                        catch (System.Runtime.InteropServices.COMException)
                        {
                            return false;
                        }
                    },
                    TimeSpan.FromSeconds(10));
                if (!found)
                {
                    Assert.Fail(
                        "no palette row for " + rowPrefix + "; app log tail: "
                        + ReadSharedLog(Path.Combine(logDirectory, "slate-windows.log")));
                }
                row!.Patterns.SelectionItem.Pattern.Select();
                PressKey(VirtualKeyShort.ENTER);
                Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(250));
            }

            Keyboard.TypeSimultaneously(
                VirtualKeyShort.CONTROL, VirtualKeyShort.ALT, VirtualKeyShort.KEY_N);
            Assert.True(
                SpinWait.SpinUntil(() => Rows().Length >= 1, TimeSpan.FromSeconds(15)),
                "the first Ctrl+Alt+N never produced a card row");
            Keyboard.TypeSimultaneously(
                VirtualKeyShort.CONTROL, VirtualKeyShort.ALT, VirtualKeyShort.KEY_N);
            Assert.True(
                SpinWait.SpinUntil(() => Rows().Length >= 2, TimeSpan.FromSeconds(15)),
                "the second Ctrl+Alt+N never produced a card row");

            // ---- Mark both by chord; the rows say so to UIA (G1) ----
            Keyboard.TypeSimultaneously(
                VirtualKeyShort.CONTROL, VirtualKeyShort.ALT, VirtualKeyShort.KEY_M);
            if (!SpinWait.SpinUntil(() => MarkedRows() == 1, TimeSpan.FromSeconds(10)))
            {
                Assert.Fail(
                    "Ctrl+Alt+M never marked the selected row; statuses: "
                    + string.Join(" | ", Rows().Select(r => r.Properties.ItemStatus.ValueOrDefault ?? "<null>"))
                    + "; names: "
                    + string.Join(" | ", Rows().Select(r => r.Name))
                    + "; app log tail: "
                    + ReadSharedLog(Path.Combine(logDirectory, "slate-windows.log")));
            }
            PressKey(VirtualKeyShort.UP);
            Keyboard.TypeSimultaneously(
                VirtualKeyShort.CONTROL, VirtualKeyShort.ALT, VirtualKeyShort.KEY_M);
            Assert.True(
                SpinWait.SpinUntil(() => MarkedRows() == 2, TimeSpan.FromSeconds(10)),
                "the second Ctrl+Alt+M never marked the row above");

            // ---- The marks list from the palette (G4), scanned -----
            RunPaletteCommand("Show Marked", "Canvas: Show Marked Cards");
            AutomationElement sheet = WaitForElement(
                window, "CanvasPromptSheet", TimeSpan.FromSeconds(10));
            Assert.NotNull(sheet);
            AssertAxeClean(process, "canvas-marks-list");

            // ---- Jump: the sheet closes, reader focus lands on a row --
            PressKey(VirtualKeyShort.ENTER);
            Assert.True(
                SpinWait.SpinUntil(
                    () => window.FindFirstDescendant(
                        automation.ConditionFactory.ByAutomationId(
                            "CanvasPromptSheet")) is null,
                    TimeSpan.FromSeconds(10)),
                "Jump never closed the marks list");
            Assert.True(
                SpinWait.SpinUntil(
                    () =>
                    {
                        try
                        {
                            return Rows().Any(item =>
                                item.Properties.HasKeyboardFocus.ValueOrDefault);
                        }
                        catch (System.Runtime.InteropServices.COMException)
                        {
                            return false;
                        }
                    },
                    TimeSpan.FromSeconds(10)),
                "Jump never landed reader focus on a card row");

            // ---- Delete Marked: one action removes both (G5) ---------
            RunPaletteCommand("Delete Marked", "Canvas: Delete Marked Cards");
            Assert.True(
                SpinWait.SpinUntil(() => Rows().Length == 0, TimeSpan.FromSeconds(10)),
                "Delete Marked never removed both cards");

            // ---- One undo restores both (G8) --------------------------
            // Ctrl+Z is two chords split by a focus gate (canvas undo
            // under the canvas, structural undo elsewhere). The deletion
            // destroyed the focused row, so seat focus on the (now empty)
            // outline tree deliberately — the modes journey seats a row
            // before its chord for the same reason — rather than trust
            // where the palette's close happened to leave it.
            WaitForElement(window, "CanvasEmptyOnboarding", TimeSpan.FromSeconds(10));
            AutomationElement emptyTree = WaitForElement(
                window, "CanvasOutlineTree", TimeSpan.FromSeconds(10));
            window.SetForeground();
            emptyTree.Focus();
            Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(250));
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
            if (!SpinWait.SpinUntil(() => Rows().Length == 2, TimeSpan.FromSeconds(10)))
            {
                Assert.Fail(
                    "Ctrl+Z never restored both cards; rows: " + Rows().Length
                    + "; app log tail: "
                    + ReadSharedLog(Path.Combine(logDirectory, "slate-windows.log")));
            }
        }
        finally
        {
            try
            {
                if (process is { HasExited: false })
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(5000);
                }
                process?.Dispose();
                Directory.Delete(testRoot, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    /// <summary>
    /// W6-1 PR A (#745): the canvas outline journey. Opening a
    /// <c>.canvas</c> from the files tree gives a real UIA Tree whose
    /// items carry core-composed names and the t0 §3 positional status,
    /// expand and collapse, drive one shared selection, and open a card
    /// on Invoke — plus the degraded banner and its focusable warning
    /// rows on a fixture with preserved-but-unshown entries.
    /// </summary>
    /// <remarks>
    /// Honors the recorded journey traps: peered elements only (the
    /// outline's Invoke lives on the tree item's own peer, so nothing
    /// here hunts for a nested button), an async settle after Invoke,
    /// and a foreground re-assert at every keyboard boundary — a plain
    /// <c>SetForegroundWindow</c> is DENIED to a background process, and
    /// the synthetic Alt tap is what satisfies the Windows foreground
    /// lock.
    /// </remarks>
    /// <summary>§F TF-11: the MODES journey — a real keyboard drives
    /// move mode, resize mode and the connect picker end to end: the
    /// M6 visible controls appear with the mode and collapse with it,
    /// the sheets land, and axe scans the mode-active and
    /// picker-open states. Budgeted per the recorded accepted risk:
    /// one launch, every leg in sequence.</summary>
    [Fact]
    [Trait("gate", "W-C")]
    public void CanvasModes_MoveResizeAndConnectPicker_AreReachable()
    {
        string testRoot = Path.Combine(
            Path.GetTempPath(), $"slate-canvas-modes-{Guid.NewGuid():N}");
        string vaultRoot = Path.Combine(testRoot, "Modes Vault");
        string logDirectory = Path.Combine(testRoot, "logs");
        Directory.CreateDirectory(vaultRoot);
        File.WriteAllText(
            Path.Combine(vaultRoot, "note.md"), "# Note\n\nBody.\n");

        Process? process = null;
        try
        {
            var startInfo = new ProcessStartInfo(SlateWindowsExe())
            {
                UseShellExecute = false,
            };
            startInfo.ArgumentList.Add(vaultRoot);
            startInfo.Environment["SLATE_CENSUS_INSTANCE_ID"] =
                $"slate-canvas-modes-{Guid.NewGuid():N}";
            startInfo.Environment["SLATE_LOG_DIR"] = logDirectory;
            process = Process.Start(startInfo)
                ?? throw new Xunit.Sdk.XunitException("SlateWindows.exe did not start.");

            if (!HasInteractiveDesktop(process, "Canvas modes"))
            {
                return;
            }

            using var automation = new UIA3Automation();
            Window window = WaitForMainWindow(
                process,
                automation,
                Path.Combine(logDirectory, "slate-windows.log"),
                TimeSpan.FromSeconds(30));
            window.SetForeground();
            window.Focus();

            // ---- A canvas with two cards, by the authoring path ----
            AutomationElement newCanvas = WaitForMenuItem(
                window, "FileMenu", "NewCanvasMenuItem", TimeSpan.FromSeconds(10));
            newCanvas.Patterns.Invoke.Pattern.Invoke();
            AutomationElement tree = WaitForElement(
                window, "CanvasOutlineTree", TimeSpan.FromSeconds(20));
            tree.Focus();
            Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(250));

            AutomationElement[] Rows()
            {
                try
                {
                    AutomationElement? liveTree = window.FindFirstDescendant(
                        automation.ConditionFactory.ByAutomationId(
                            "CanvasOutlineTree"));
                    return liveTree is null
                        ? []
                        : liveTree.FindAllDescendants(
                            automation.ConditionFactory.ByControlType(
                                ControlType.TreeItem));
                }
                catch (System.Runtime.InteropServices.COMException)
                {
                    return [];
                }
            }

            Keyboard.TypeSimultaneously(
                VirtualKeyShort.CONTROL, VirtualKeyShort.ALT, VirtualKeyShort.KEY_N);
            Assert.True(
                SpinWait.SpinUntil(() => Rows().Length >= 1, TimeSpan.FromSeconds(15)),
                "the first Ctrl+Alt+N never produced a card row");
            Keyboard.TypeSimultaneously(
                VirtualKeyShort.CONTROL, VirtualKeyShort.ALT, VirtualKeyShort.KEY_N);
            Assert.True(
                SpinWait.SpinUntil(() => Rows().Length >= 2, TimeSpan.FromSeconds(15)),
                "the second Ctrl+Alt+N never produced a card row");

            // ---- Move mode: the M6 controls appear with the mode ----
            AutomationElement? Commit() =>
                window.FindFirstDescendant(
                    automation.ConditionFactory.ByAutomationId("CanvasCommitMode"));

            Keyboard.TypeSimultaneously(
                VirtualKeyShort.CONTROL, VirtualKeyShort.ALT, VirtualKeyShort.KEY_G);
            Assert.True(
                SpinWait.SpinUntil(
                    () => Commit() is { } button && !button.Properties.IsOffscreen,
                    TimeSpan.FromSeconds(10)),
                "move mode never showed the commit control; app log tail: "
                + ReadSharedLog(Path.Combine(logDirectory, "slate-windows.log")));
            AssertAxeClean(process, "canvas-move-mode-active");

            Keyboard.Press(VirtualKeyShort.RIGHT);
            Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(250));
            Keyboard.Press(VirtualKeyShort.ENTER);
            Assert.True(
                SpinWait.SpinUntil(
                    () => Commit() is null or { Properties.IsOffscreen.Value: true },
                    TimeSpan.FromSeconds(10)),
                "Return never collapsed the mode controls");

            // ---- Resize mode enters and Escape restores -------------
            Keyboard.TypeSimultaneously(
                VirtualKeyShort.CONTROL, VirtualKeyShort.ALT, VirtualKeyShort.KEY_R);
            Assert.True(
                SpinWait.SpinUntil(
                    () => Commit() is { } button && !button.Properties.IsOffscreen,
                    TimeSpan.FromSeconds(10)),
                "resize mode never showed the commit control");
            Keyboard.Press(VirtualKeyShort.ESCAPE);
            Assert.True(
                SpinWait.SpinUntil(
                    () => Commit() is null or { Properties.IsOffscreen.Value: true },
                    TimeSpan.FromSeconds(10)),
                "Escape never collapsed the mode controls");

            // ---- Connect To…: the picker sheet, scanned, then Esc ---
            Keyboard.TypeSimultaneously(
                VirtualKeyShort.CONTROL, VirtualKeyShort.ALT, VirtualKeyShort.KEY_C);
            AutomationElement picker = WaitForElement(
                window, "CanvasCardPickerSheet", TimeSpan.FromSeconds(10));
            Assert.NotNull(picker);
            AssertAxeClean(process, "canvas-card-picker");
            Keyboard.Press(VirtualKeyShort.ESCAPE);
            Assert.True(
                SpinWait.SpinUntil(
                    () => window.FindFirstDescendant(
                        automation.ConditionFactory.ByAutomationId(
                            "CanvasCardPickerSheet")) is null,
                    TimeSpan.FromSeconds(10)),
                "Escape never closed the picker sheet");
        }
        finally
        {
            try
            {
                if (process is { HasExited: false })
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(5000);
                }
                process?.Dispose();
                Directory.Delete(testRoot, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    [Fact]
    [Trait("gate", "W-C")]
    public void CanvasSurfaces_OutlineTreeSelectionAndActivation_AreClean()
    {
        string testRoot = Path.Combine(
            Path.GetTempPath(), $"slate-canvas-outline-{Guid.NewGuid():N}");
        string vaultRoot = Path.Combine(testRoot, "Canvas Vault");
        string logDirectory = Path.Combine(testRoot, "logs");
        Directory.CreateDirectory(vaultRoot);
        // The three fixtures the spec names, byte-exact from the demo
        // vault: the same bytes the unit facts and the §W-A goldens run
        // over, so a journey failure is never "a different canvas".
        foreach (string fixture in new[]
        {
            "sample.canvas", "groups_nested.canvas", "malformed.canvas",
        })
        {
            File.Copy(
                Path.Combine(DemoVaultCanvasDirectory(), fixture),
                Path.Combine(vaultRoot, fixture));
        }
        File.WriteAllText(
            Path.Combine(vaultRoot, "note.md"), "# Note\n\nBody.\n");

        Process? process = null;
        try
        {
            var startInfo = new ProcessStartInfo(SlateWindowsExe())
            {
                UseShellExecute = false,
            };
            startInfo.ArgumentList.Add(vaultRoot);
            startInfo.Environment["SLATE_CENSUS_INSTANCE_ID"] =
                $"slate-canvas-outline-{Guid.NewGuid():N}";
            startInfo.Environment["SLATE_LOG_DIR"] = logDirectory;
            process = Process.Start(startInfo)
                ?? throw new Xunit.Sdk.XunitException("SlateWindows.exe did not start.");

            if (!HasInteractiveDesktop(process, "Canvas"))
            {
                return;
            }

            using var automation = new UIA3Automation();
            Window window = WaitForMainWindow(
                process,
                automation,
                Path.Combine(logDirectory, "slate-windows.log"),
                TimeSpan.FromSeconds(30));
            window.SetForeground();
            window.Focus();

            OpenCanvasFromTree(window, automation, "sample");

            // The projection is a real UIA Tree, named from the host's
            // label inventory (contract A8).
            AutomationElement tree = WaitForElement(
                window, "CanvasOutlineTree", TimeSpan.FromSeconds(20));
            Assert.Equal(ControlType.Tree, tree.Properties.ControlType.Value);
            Assert.Equal("Canvas outline", tree.Properties.Name.Value);

            AutomationElement[] items = WaitForTreeItems(automation, tree, 5);
            // Contract A9: every row's Name is the t0 §1.1 card
            // reference over core's kind word and core's speakable_name
            // — never a bare title, never host prose.
            Assert.Contains(
                items,
                item => item.Properties.Name.Value.StartsWith(
                    "Group \"", StringComparison.Ordinal));
            Assert.Contains(
                items,
                item => item.Properties.Name.Value.StartsWith(
                    "Text card \"", StringComparison.Ordinal));
            Assert.Contains(
                items,
                item => item.Properties.Name.Value.StartsWith(
                    "Link card \"", StringComparison.Ordinal));

            // Contract A10: the t0 §3 inspectability slot is READABLE,
            // not announcement-only.
            AutomationElement textCard = items.First(item =>
                item.Properties.Name.Value.StartsWith("Text card \"", StringComparison.Ordinal));
            Assert.Matches(
                @"^\d+ of \d+ in .+", textCard.Properties.ItemStatus.Value);
            Assert.False(
                string.IsNullOrEmpty(textCard.Properties.HelpText.Value),
                "every row carries its per-kind activation hint");

            // ExpandCollapse on a group (contract A8).
            AutomationElement group = items.First(item =>
                item.Properties.Name.Value.StartsWith("Group \"", StringComparison.Ordinal));
            Assert.True(
                group.Patterns.ExpandCollapse.IsSupported,
                "a group row must expose ExpandCollapse");
            group.Patterns.ExpandCollapse.Pattern.Collapse();
            Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(250));
            Assert.Equal(
                ExpandCollapseState.Collapsed,
                group.Patterns.ExpandCollapse.Pattern.ExpandCollapseState.Value);
            group.Patterns.ExpandCollapse.Pattern.Expand();
            Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(250));

            // SelectionItem drives the ONE selection, and the selected
            // card grows its connection rows (contract A11/A12).
            Assert.True(
                textCard.Patterns.SelectionItem.IsSupported,
                "a card row must expose SelectionItem");
            textCard.Patterns.SelectionItem.Pattern.Select();
            Assert.True(
                SpinWait.SpinUntil(
                    () => textCard.Patterns.SelectionItem.Pattern.IsSelected.Value,
                    TimeSpan.FromSeconds(10)),
                "SelectionItem.Select did not take");
            Assert.True(
                SpinWait.SpinUntil(
                    () => tree
                        .FindAllDescendants(automation.ConditionFactory.ByControlType(
                            ControlType.TreeItem))
                        .Any(item => item.Properties.ItemStatus.Value.StartsWith(
                            "connection ", StringComparison.Ordinal)),
                    TimeSpan.FromSeconds(10)),
                "the selected card's connection rows never materialized");

            AssertAxeClean(process, "canvas-outline");

            // Invoke on a text card opens the CARD EDITOR SHEET
            // (§E TE-11b: the interim read-only detail retired; the
            // activation seam asks the workspace for the real editor).
            // The pattern lives on the ITEM's own peer — no nested
            // element, which is the peered-elements-only trap.
            AutomationElement textCardToOpen = tree
                .FindAllDescendants(
                    automation.ConditionFactory.ByControlType(ControlType.TreeItem))
                .First(item => item.Properties.Name.Value.StartsWith(
                    "Text card \"", StringComparison.Ordinal));
            Assert.True(
                textCardToOpen.Patterns.Invoke.IsSupported,
                "a card row must expose Invoke");
            textCardToOpen.Patterns.Invoke.Pattern.Invoke();
            // Invoke is asynchronous by contract: settle before reading.
            AutomationElement sheet = WaitForElement(
                window, "CanvasCardEditorSheet", TimeSpan.FromSeconds(10));
            Assert.Equal("Card editor", sheet.Properties.Name.Value);
            AutomationElement draft = WaitForElement(
                window, "CanvasCardEditorText", TimeSpan.FromSeconds(10));
            Assert.True(
                SpinWait.SpinUntil(
                    () => draft.Patterns.Value.IsSupported
                        && !string.IsNullOrEmpty(
                            draft.Patterns.Value.Pattern.Value.Value),
                    TimeSpan.FromSeconds(10)),
                "the editor sheet never carried the card's text");
            // Escape COMMITS (no changes here) and closes — the modal
            // must not swallow the legs below.
            draft.Focus();
            Keyboard.Press(VirtualKeyShort.ESCAPE);
            Assert.True(
                SpinWait.SpinUntil(
                    () => window.FindFirstDescendant(
                        automation.ConditionFactory.ByAutomationId(
                            "CanvasCardEditorSheet")) is null,
                    TimeSpan.FromSeconds(10)),
                "the editor sheet never closed on Escape");

            // Keyboard-only reachability: focus the tree and press Enter
            // on a row. Foreground is re-asserted first — another app can
            // take it between launch and here.
            Keyboard.Press(VirtualKeyShort.ALT);
            window.SetForeground();
            Keyboard.Release(VirtualKeyShort.ALT);
            window.Focus();
            textCardToOpen.Focus();
            AssertEventuallyFocused(
                textCardToOpen, "the outline row never took keyboard focus");
            PressKey(VirtualKeyShort.RETURN);
            Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(500));
            // Enter opens the editor sheet now (TE-11b): close it so
            // the legs below are not under a modal.
            _ = WaitForElement(
                window, "CanvasCardEditorSheet", TimeSpan.FromSeconds(10));
            Keyboard.Press(VirtualKeyShort.ESCAPE);
            Assert.True(
                SpinWait.SpinUntil(
                    () => window.FindFirstDescendant(
                        automation.ConditionFactory.ByAutomationId(
                            "CanvasCardEditorSheet")) is null,
                    TimeSpan.FromSeconds(10)),
                "the keyboard leg's editor sheet never closed");

            // The surface switcher is one named group (A18). All three
            // arms have shipped — the table in PR B, the visual in PR D
            // (whose flip this branch carries) — so every arm is live,
            // and the visual arm's own journey exercises the switch.
            AutomationElement switcher = WaitForElement(
                window, "CanvasSurfaceSwitcher", TimeSpan.FromSeconds(10));
            Assert.Equal("Canvas view", switcher.Properties.Name.Value);
            AutomationElement visualChoice = WaitForElement(
                window, "CanvasShowVisual", TimeSpan.FromSeconds(10));
            Assert.True(
                visualChoice.Properties.IsEnabled.Value,
                "the Visual arm is disabled: the flip regressed.");

            // The t0 §5 banner and its focusable detail rows, on a
            // fixture whose entries core preserved but cannot show.
            OpenCanvasFromTree(window, automation, "malformed");
            AutomationElement banner = WaitForElement(
                window, "CanvasDegradedBanner", TimeSpan.FromSeconds(20));
            Assert.True(
                SpinWait.SpinUntil(
                    () => banner.Properties.Name.Value.Contains(
                        "preserved in the file but not shown", StringComparison.Ordinal),
                    TimeSpan.FromSeconds(10)),
                $"the degraded banner never arrived; it reads '{banner.Properties.Name.Value}'");
            AutomationElement warnings = WaitForElement(
                window, "CanvasWarningRows", TimeSpan.FromSeconds(10));
            Assert.Equal("Canvas load warnings", warnings.Properties.Name.Value);
            Assert.NotEmpty(
                warnings.FindAllDescendants(
                    automation.ConditionFactory.ByControlType(ControlType.ListItem)));

            AssertAxeClean(process, "canvas-degraded");
        }
        finally
        {
            if (process is not null && !process.HasExited)
            {
                process.CloseMainWindow();
                if (!process.WaitForExit(5_000))
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            try
            {
                Directory.Delete(testRoot, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    /// <summary>
    /// W6-1 PR B (#745): the canvas TABLE journey (the spec's
    /// "Canvas_TableJourney"). Switching the surface switcher to Table
    /// swaps the projection in the live UIA tree — the outline LEAVES it
    /// entirely — and what arrives is the W4-1 grid: Table and Grid
    /// patterns, the six mac column headers, the separately-focusable
    /// summary region, keyboard sort on Ctrl+Alt+S, and Enter activation
    /// that opens a card.
    /// </summary>
    /// <remarks>
    /// Honors the recorded journey traps: peered elements only, a
    /// foreground re-assert before every keyboard boundary (a plain
    /// SetForegroundWindow is DENIED to a background process; the
    /// synthetic Alt tap is what satisfies the foreground lock), and
    /// assertions that tolerate row virtualization — only realized rows
    /// are in the tree, so the sort is checked as "the realized column is
    /// non-decreasing and starts where ascending order starts", never as
    /// a fixed row count.
    /// </remarks>
    [Fact]
    [Trait("gate", "W-C")]
    public void CanvasSurfaces_TableGridSortSelectionAndActivation_AreClean()
    {
        string testRoot = Path.Combine(
            Path.GetTempPath(), $"slate-canvas-table-{Guid.NewGuid():N}");
        string vaultRoot = Path.Combine(testRoot, "Canvas Vault");
        string logDirectory = Path.Combine(testRoot, "logs");
        Directory.CreateDirectory(vaultRoot);
        File.Copy(
            Path.Combine(DemoVaultCanvasDirectory(), "sample.canvas"),
            Path.Combine(vaultRoot, "sample.canvas"));

        Process? process = null;
        try
        {
            var startInfo = new ProcessStartInfo(SlateWindowsExe())
            {
                UseShellExecute = false,
            };
            startInfo.ArgumentList.Add(vaultRoot);
            startInfo.Environment["SLATE_CENSUS_INSTANCE_ID"] =
                $"slate-canvas-table-{Guid.NewGuid():N}";
            startInfo.Environment["SLATE_LOG_DIR"] = logDirectory;
            process = Process.Start(startInfo)
                ?? throw new Xunit.Sdk.XunitException("SlateWindows.exe did not start.");

            if (!HasInteractiveDesktop(process, "Canvas table"))
            {
                return;
            }

            using var automation = new UIA3Automation();
            Window window = WaitForMainWindow(
                process,
                automation,
                Path.Combine(logDirectory, "slate-windows.log"),
                TimeSpan.FromSeconds(30));
            window.SetForeground();
            window.Focus();

            OpenCanvasFromTree(window, automation, "sample");
            _ = WaitForElement(window, "CanvasOutlineTree", TimeSpan.FromSeconds(20));

            // The switcher's table arm is LIVE from this slice (contract
            // B10) — it was disabled in PR A.
            AutomationElement tableChoice = WaitForElement(
                window, "CanvasShowTable", TimeSpan.FromSeconds(10));
            Assert.True(
                tableChoice.Properties.IsEnabled.Value,
                "the Table arm of the surface switcher must be enabled once its "
                + "projection ships");
            tableChoice.Patterns.SelectionItem.Pattern.Select();

            AutomationElement grid = WaitForElement(
                window, "CanvasTableGrid", TimeSpan.FromSeconds(20));
            // Contract B11: exactly one projection is in the tree. The
            // outline is COLLAPSED, which takes it out of the tree
            // entirely rather than leaving it off screen.
            Assert.True(
                SpinWait.SpinUntil(
                    () => window.FindFirstDescendant(
                        automation.ConditionFactory.ByAutomationId("CanvasOutlineTree"))
                        is null,
                    TimeSpan.FromSeconds(10)),
                "the outline projection is still in the UIA tree while the table "
                + "is showing");

            // The substrate's patterns, on the element a client reads.
            Assert.True(grid.Patterns.Grid.IsSupported, "the table must expose Grid");
            Assert.True(grid.Patterns.Table.IsSupported, "the table must expose Table");
            Assert.Equal("Canvas table", grid.Properties.Name.Value);

            // Contract B2: the mac column inventory, in mac's order.
            Assert.True(
                SpinWait.SpinUntil(
                    () => grid.FindAllDescendants(
                        automation.ConditionFactory.ByControlType(ControlType.HeaderItem))
                        .Length >= 6,
                    TimeSpan.FromSeconds(10)),
                "the canvas table never materialized its six column headers");
            Assert.Equal(
                new[] { "Type", "Title", "Group", "Target", "Connections", "Color" },
                grid.FindAllDescendants(
                    automation.ConditionFactory.ByControlType(ControlType.HeaderItem))
                    .Select(header => header.Properties.Name.Value)
                    .Take(6)
                    .ToArray());

            // Contract B9: mac's summary sentence, in the substrate's
            // separately-focusable region. `sample.canvas` is seven cards
            // and two groups.
            AutomationElement summary = WaitForElement(
                window, "CanvasTableGridSummary", TimeSpan.FromSeconds(10));
            Assert.Equal(
                "Summary: Canvas table: 7 cards, 2 groups.",
                summary.Properties.Name.Value);

            // Keyboard sort: Ctrl+Alt+S on the focused column (§8.7
            // forbids header-click-only sorting), which for the entry
            // cell is Type. Ascending kind order starts at `file`.
            AutomationElement firstTypeCell = WaitForCellStartingWith(grid, "Type: ");
            ReassertForegroundForAChord(window);
            firstTypeCell.Focus();
            AssertEventuallyFocused(
                firstTypeCell, "the canvas table's first cell never took focus");
            PressChord(VirtualKeyShort.CONTROL, VirtualKeyShort.ALT, VirtualKeyShort.KEY_S);
            Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(500));

            Assert.True(
                SpinWait.SpinUntil(
                    () => TypeColumn(grid) is ["Type: File", ..],
                    TimeSpan.FromSeconds(10)),
                "Ctrl+Alt+S did not sort the Type column ascending; the column "
                + $"reads [{string.Join(", ", TypeColumn(grid))}]");
            string[] sortedTypes = TypeColumn(grid);
            Assert.Equal(
                sortedTypes.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                sortedTypes);

            AssertAxeClean(process, "canvas-table");

            // Contract B6: the row-actions menu on the KEYBOARD route
            // (the Menu key; Shift+F10 is its twin), with the two
            // unshipped verbs listed DISABLED and their reasons readable
            // as HelpText — the channel the mac RowAction contract names
            // and the one a screen-reader user gets. The menu is a
            // separate popup HWND, so it is looked up from the desktop
            // rather than under the window.
            // §E TE-8 flipped Delete LIVE on card rows; the menu is
            // asserted from a TEXT row so the expectation is
            // deterministic whatever the sort put first.
            AutomationElement menuAnchorCell =
                WaitForCellStartingWith(grid, "Type: Text");
            ReassertForegroundForAChord(window);
            menuAnchorCell.Focus();
            AssertEventuallyFocused(
                menuAnchorCell, "the text row never took focus for the menu");
            PressKey(VirtualKeyShort.APPS);
            AutomationElement[] rowActions = WaitForRowActionItems(automation, process.Id);
            Assert.Equal(
                new[] { "Open", "Toggle Mark", "Delete" },
                rowActions.Select(item => item.Properties.Name.Value).ToArray());
            Assert.True(
                rowActions[0].Properties.IsEnabled.Value,
                "Open must be live — it is the shipped verb");
            Assert.False(
                rowActions[1].Properties.IsEnabled.Value,
                "Toggle Mark stays staged until PR G");
            Assert.Contains(
                "arrives in a later slice",
                rowActions[1].Properties.HelpText.Value,
                StringComparison.Ordinal);
            Assert.True(
                rowActions[2].Properties.IsEnabled.Value,
                "Delete went live on card rows in TE-8; a disabled Delete "
                + "on a text row is the stale staging");
            // Close it before the activation leg: a live popup owns the
            // keyboard, and Enter would pick a menu item instead.
            PressKey(VirtualKeyShort.ESCAPE);
            Assert.True(
                SpinWait.SpinUntil(
                    () => FindRowActionItems(automation, process.Id).Length == 0,
                    TimeSpan.FromSeconds(10)),
                "the row-actions menu never closed on Escape");

            // Contract B6: Enter opens the card through the same
            // activation seam the outline uses — a text card opens the
            // CARD EDITOR SHEET (§E TE-11b).
            AutomationElement textCell = WaitForCellStartingWith(grid, "Type: Text");
            ReassertForegroundForAChord(window);
            textCell.Focus();
            AssertEventuallyFocused(
                textCell, "the canvas table's text-card row never took focus");
            PressKey(VirtualKeyShort.RETURN);

            _ = WaitForElement(
                window, "CanvasCardEditorSheet", TimeSpan.FromSeconds(10));
            AutomationElement tableDraft = WaitForElement(
                window, "CanvasCardEditorText", TimeSpan.FromSeconds(10));
            Assert.True(
                SpinWait.SpinUntil(
                    () => tableDraft.Patterns.Value.IsSupported
                        && !string.IsNullOrEmpty(
                            tableDraft.Patterns.Value.Pattern.Value.Value),
                    TimeSpan.FromSeconds(10)),
                "activating a table row never opened the editor's text");
        }
        finally
        {
            if (process is not null && !process.HasExited)
            {
                process.CloseMainWindow();
                if (!process.WaitForExit(5_000))
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            try
            {
                Directory.Delete(testRoot, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    /// <summary>
    /// The canvas table's row-action items, from the popup the Menu key
    /// opened. A WPF <c>ContextMenu</c> is its own HWND, so the search
    /// starts at the DESKTOP — and is filtered to THIS process, because
    /// a desktop-wide menu search on a shared runner can otherwise pick
    /// up another application's popup (the suite's recorded popup
    /// discipline). The substrate's menu is identified by its first item
    /// rather than by an automation id, because the id would be a hook
    /// this journey invented for itself.
    /// </summary>
    private static AutomationElement[] FindRowActionItems(
        UIA3Automation automation, int processId)
    {
        foreach (AutomationElement menu in automation.GetDesktop()
            .FindAllDescendants(
                automation.ConditionFactory.ByControlType(ControlType.Menu)
                    .And(automation.ConditionFactory.ByProcessId(processId))))
        {
            AutomationElement[] items = menu.FindAllDescendants(
                automation.ConditionFactory.ByControlType(ControlType.MenuItem));
            if (items.Any(item =>
                string.Equals(
                    item.Properties.Name.ValueOrDefault, "Open", StringComparison.Ordinal)))
            {
                return items;
            }
        }
        return [];
    }

    /// <summary>
    /// W6-1 PR C (#745): the canvas NAVIGATOR journey (the spec's
    /// "Canvas_NavigatorJourney"). Ctrl+F reaches the one filter field
    /// and its Value carries the needle; the result summary becomes a
    /// readable element and the projection narrows with it; Escape walks
    /// the t0 §2 M5 ladder one rung per press — the filter first, then
    /// the transient region; and Ctrl+Alt+Shift+I opens the Where-am-I
    /// panel with keyboard focus inside it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The ANNOUNCEMENTS are deliberately not asserted here, and that is
    /// the honest split rather than an omission (the B7 precedent). The
    /// shell posts them as UIA notification events, which this suite has
    /// no capture for; every one of them is pinned instead in
    /// `CanvasNavigatorTests` at the production funnel's own post seam,
    /// as the RENDERED text. What only a live UIA tree can prove — that
    /// the field is an Edit whose Value a client can read, that the
    /// summary is its own element, and that a chord really lands
    /// keyboard focus in the panel — is what this journey asserts.
    /// </para>
    /// <para>
    /// Honors the recorded journey traps: peered elements only, a
    /// foreground re-assert at every keyboard boundary, and an async
    /// settle after each chord.
    /// </para>
    /// </remarks>
    [Fact]
    [Trait("gate", "W-C")]
    public void CanvasSurfaces_NavigatorFilterAndWhereAmI_AreClean()
    {
        string testRoot = Path.Combine(
            Path.GetTempPath(), $"slate-canvas-navigator-{Guid.NewGuid():N}");
        string vaultRoot = Path.Combine(testRoot, "Canvas Vault");
        string logDirectory = Path.Combine(testRoot, "logs");
        Directory.CreateDirectory(vaultRoot);
        File.Copy(
            Path.Combine(DemoVaultCanvasDirectory(), "sample.canvas"),
            Path.Combine(vaultRoot, "sample.canvas"));

        Process? process = null;
        try
        {
            var startInfo = new ProcessStartInfo(SlateWindowsExe())
            {
                UseShellExecute = false,
            };
            startInfo.ArgumentList.Add(vaultRoot);
            startInfo.Environment["SLATE_CENSUS_INSTANCE_ID"] =
                $"slate-canvas-navigator-{Guid.NewGuid():N}";
            startInfo.Environment["SLATE_LOG_DIR"] = logDirectory;
            process = Process.Start(startInfo)
                ?? throw new Xunit.Sdk.XunitException("SlateWindows.exe did not start.");

            if (!HasInteractiveDesktop(process, "Canvas navigator"))
            {
                return;
            }

            using var automation = new UIA3Automation();
            Window window = WaitForMainWindow(
                process,
                automation,
                Path.Combine(logDirectory, "slate-windows.log"),
                TimeSpan.FromSeconds(30));
            window.SetForeground();
            window.Focus();

            OpenCanvasFromTree(window, automation, "sample");
            AutomationElement tree = WaitForElement(
                window, "CanvasOutlineTree", TimeSpan.FromSeconds(20));
            int unfiltered = WaitForTreeItems(automation, tree, 5).Length;

            // Ctrl+F reaches the ONE canvas filter field (contract C10).
            ReassertForegroundForAChord(window);
            PressChord(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_F);
            AutomationElement filter = WaitForElement(
                window, "CanvasFilterField", TimeSpan.FromSeconds(10));
            Assert.Equal(ControlType.Edit, filter.Properties.ControlType.Value);
            Assert.Equal("Filter cards", filter.Properties.Name.Value);
            AssertEventuallyFocused(
                filter, "Ctrl+F never landed focus in the canvas filter field");

            // The needle goes in through the Value pattern — the same
            // surface a client reads it back from.
            filter.Patterns.Value.Pattern.SetValue("Research");
            Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(500));
            Assert.Equal("Research", filter.Patterns.Value.Pattern.Value.Value);

            // t0 §3: the result summary is its own readable element, and
            // the projection narrowed with it.
            AutomationElement summary = WaitForElement(
                window, "CanvasFilterSummary", TimeSpan.FromSeconds(10));
            Assert.True(
                SpinWait.SpinUntil(
                    () => summary.Properties.Name.Value.StartsWith(
                        "Filter results:", StringComparison.Ordinal),
                    TimeSpan.FromSeconds(10)),
                $"the filter summary never became readable; it reads "
                + $"'{summary.Properties.Name.ValueOrDefault}'");
            Assert.True(
                SpinWait.SpinUntil(
                    () => tree.FindAllDescendants(
                        automation.ConditionFactory.ByControlType(ControlType.TreeItem))
                        .Length < unfiltered,
                    TimeSpan.FromSeconds(10)),
                "the outline never narrowed to the filtered set");

            // M5 rung 2: one Escape clears the filter and no more.
            ReassertForegroundForAChord(window);
            PressKey(VirtualKeyShort.ESCAPE);
            Assert.True(
                SpinWait.SpinUntil(
                    () => filter.Patterns.Value.Pattern.Value.Value.Length == 0,
                    TimeSpan.FromSeconds(10)),
                "Escape did not consume the filter rung of the ladder");
            Assert.True(
                SpinWait.SpinUntil(
                    () => tree.FindAllDescendants(
                        automation.ConditionFactory.ByControlType(ControlType.TreeItem))
                        .Length == unfiltered,
                    TimeSpan.FromSeconds(10)),
                "clearing the filter did not restore every card");

            // Where-am-I: the chord opens the panel and puts the reader
            // in it (t0 §1.4, contract C11).
            ReassertForegroundForAChord(window);
            PressChord(
                VirtualKeyShort.CONTROL,
                VirtualKeyShort.ALT,
                VirtualKeyShort.SHIFT,
                VirtualKeyShort.KEY_I);
            AutomationElement readback = WaitForElement(
                window, "CanvasWhereAmIReadback", TimeSpan.FromSeconds(10));
            Assert.True(
                SpinWait.SpinUntil(
                    () => readback.Patterns.Value.IsSupported
                        && !string.IsNullOrEmpty(
                            readback.Patterns.Value.Pattern.Value.Value),
                    TimeSpan.FromSeconds(10)),
                "the Where-am-I panel never carried the readback text");
            AssertEventuallyFocused(
                readback, "Ctrl+Alt+Shift+I never landed focus in the Where-am-I panel");
            AutomationElement panel = WaitForElement(
                window, "CanvasWhereAmIPanel", TimeSpan.FromSeconds(10));
            Assert.Equal("Where am I?", panel.Properties.Name.Value);

            AssertAxeClean(process, "canvas-navigator");

            // M5 rung 3: the next Escape dismisses the panel.
            ReassertForegroundForAChord(window);
            PressKey(VirtualKeyShort.ESCAPE);
            Assert.True(
                SpinWait.SpinUntil(
                    () => window.FindFirstDescendant(
                        automation.ConditionFactory.ByAutomationId(
                            "CanvasWhereAmIPanel")) is null,
                    TimeSpan.FromSeconds(10)),
                "Escape did not consume the surface rung and close the panel");
        }
        finally
        {
            if (process is not null && !process.HasExited)
            {
                process.CloseMainWindow();
                if (!process.WaitForExit(5_000))
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            try
            {
                Directory.Delete(testRoot, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    /// <summary>§D TD-7: the visual board's journey — the container
    /// and card peers through the real UIA bridge, the declared Value
    /// pattern, a rectangle that CHANGES after Ctrl+= (the stale-frame
    /// classic, asserted at the level it is true), and axe over peered
    /// elements only (the recorded trap).</summary>
    [Fact]
    [Trait("gate", "W-C")]
    public void CanvasSurfaces_VisualBoardPeersAndZoom_AreClean()
    {
        string testRoot = Path.Combine(
            Path.GetTempPath(), $"slate-canvas-visual-{Guid.NewGuid():N}");
        string vaultRoot = Path.Combine(testRoot, "Canvas Vault");
        string logDirectory = Path.Combine(testRoot, "logs");
        Directory.CreateDirectory(vaultRoot);
        File.Copy(
            Path.Combine(DemoVaultCanvasDirectory(), "sample.canvas"),
            Path.Combine(vaultRoot, "sample.canvas"));

        Process? process = null;
        try
        {
            var startInfo = new ProcessStartInfo(SlateWindowsExe())
            {
                UseShellExecute = false,
            };
            startInfo.ArgumentList.Add(vaultRoot);
            startInfo.Environment["SLATE_CENSUS_INSTANCE_ID"] =
                $"slate-canvas-visual-{Guid.NewGuid():N}";
            startInfo.Environment["SLATE_LOG_DIR"] = logDirectory;
            process = Process.Start(startInfo)
                ?? throw new Xunit.Sdk.XunitException("SlateWindows.exe did not start.");

            if (!HasInteractiveDesktop(process, "Canvas visual"))
            {
                return;
            }

            using var automation = new UIA3Automation();
            Window window = WaitForMainWindow(
                process,
                automation,
                Path.Combine(logDirectory, "slate-windows.log"),
                TimeSpan.FromSeconds(30));
            window.SetForeground();
            window.Focus();

            OpenCanvasFromTree(window, automation, "sample");

            // The switcher's Visual arm is LIVE (the flip) and drives
            // the one surface switch.
            AutomationElement visualChoice = WaitForElement(
                window, "CanvasShowVisual", TimeSpan.FromSeconds(20));
            Assert.True(
                visualChoice.Properties.IsEnabled.Value,
                "the Visual radio is disabled: the flip did not reach the tree.");
            visualChoice.AsRadioButton().IsChecked = true;

            // The board is a real Group with the declared VALUE
            // pattern (DD-5) carrying the zoom.
            AutomationElement board = WaitForElement(
                window, "CanvasVisualBoard", TimeSpan.FromSeconds(20));
            Assert.Equal("Canvas visual view", board.Properties.Name.Value);
            var value = board.Patterns.Value;
            Assert.True(value.IsSupported, "the container's Value pattern is missing (DD-5).");
            Assert.StartsWith("Zoom ", value.Pattern.Value.Value, StringComparison.Ordinal);

            // Card peers: materialized Buttons with unique non-empty
            // names from core's speakable name (D3/D5's journey half).
            AutomationElement[] cards = Retry.WhileEmpty(
                () => board.FindAllChildren(
                    finder => finder.ByControlType(ControlType.Button)),
                TimeSpan.FromSeconds(20)).Result;
            Assert.True(cards.Length >= 1, "no card peers materialized.");
            string[] names = [.. cards.Select(card => card.Properties.Name.Value)];
            Assert.All(names, name => Assert.False(string.IsNullOrWhiteSpace(name)));
            Assert.Equal(names.Length, names.Distinct(StringComparer.Ordinal).Count());

            // The stale-frame classic: zoom, then re-query the SAME
            // card's rectangle — it must change (D3).
            System.Drawing.Rectangle before = cards[0].Properties.BoundingRectangle.Value;
            board.Focus();
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.OEM_PLUS);
            Wait.UntilInputIsProcessed();
            AutomationElement after = Retry.WhileNull(
                () => board.FindAllChildren(
                        finder => finder.ByControlType(ControlType.Button))
                    .FirstOrDefault(card => card.Properties.Name.Value == names[0]),
                TimeSpan.FromSeconds(10)).Result!;
            Assert.True(
                Retry.WhileTrue(
                    () => after.Properties.BoundingRectangle.Value == before,
                    TimeSpan.FromSeconds(10)).Success,
                "the card's rectangle did not change after Ctrl+=: the peer "
                + "answered a stale frame.");

            // A select through the pattern moves the selection.
            var item = after.Patterns.SelectionItem;
            Assert.True(item.IsSupported, "the card's SelectionItem pattern is missing.");
            item.Pattern.Select();
            Assert.True(
                Retry.WhileFalse(
                    () => item.Pattern.IsSelected.Value,
                    TimeSpan.FromSeconds(10)).Success,
                "the selected card does not report IsSelected.");

            AssertAxeClean(process, "canvas-visual");
        }
        finally
        {
            process?.Kill(entireProcessTree: true);
            process?.Dispose();
            try
            {
                Directory.Delete(testRoot, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    private static AutomationElement[] WaitForRowActionItems(
        UIA3Automation automation, int processId)
    {
        AutomationElement[] items = [];
        Assert.True(
            SpinWait.SpinUntil(
                () =>
                {
                    items = FindRowActionItems(automation, processId);
                    return items.Length >= 3;
                },
                TimeSpan.FromSeconds(15)),
            "the canvas table's row-actions menu never opened on the Menu key "
            + $"(saw {items.Length} items)");
        return items;
    }

    /// <summary>
    /// The Type column, top to bottom, through the UIA Grid pattern —
    /// which resolves a cell by (row, column) whether or not its
    /// container is realized, so row virtualization (the substrate's
    /// AT-safe setting) cannot turn this into an assertion about the
    /// window size. The same route `GridConformanceTests` reads cells
    /// by.
    /// </summary>
    private static string[] TypeColumn(AutomationElement grid)
    {
        FlaUI.Core.Patterns.IGridPattern pattern = grid.Patterns.Grid.Pattern;
        int rows = pattern.RowCount.Value;
        var names = new string[rows];
        for (int row = 0; row < rows; row++)
        {
            names[row] = pattern.GetItem(row, 0).Name;
        }
        return names;
    }

    private static AutomationElement WaitForCellStartingWith(
        AutomationElement grid, string prefix)
    {
        AutomationElement? cell = null;
        Assert.True(
            SpinWait.SpinUntil(
                () =>
                {
                    FlaUI.Core.Patterns.IGridPattern pattern = grid.Patterns.Grid.Pattern;
                    for (int row = 0; row < pattern.RowCount.Value; row++)
                    {
                        AutomationElement candidate = pattern.GetItem(row, 0);
                        if ((candidate.Name ?? string.Empty)
                            .StartsWith(prefix, StringComparison.Ordinal))
                        {
                            cell = candidate;
                            return true;
                        }
                    }
                    return false;
                },
                TimeSpan.FromSeconds(20)),
            $"no canvas-table cell named '{prefix}…' materialized");
        return cell!;
    }

    /// <summary>
    /// Put the window in the foreground for a CHORD. Synthesized input
    /// goes to the foreground queue, and Windows denies
    /// SetForegroundWindow to a background process unless it owns recent
    /// input — a synthesized key grants exactly that credential. The tap
    /// is CONTROL rather than Alt on purpose: a bare Alt tap drops the
    /// target window into system-menu mode and the next key is eaten by
    /// menu navigation (the recorded `GridConformanceTests` lesson,
    /// which matters here because the next key is Ctrl+Alt+S).
    /// </summary>
    private static void ReassertForegroundForAChord(Window window)
    {
        Keyboard.Press(VirtualKeyShort.CONTROL);
        Keyboard.Release(VirtualKeyShort.CONTROL);
        window.SetForeground();
        window.Focus();
        Wait.UntilInputIsProcessed();
    }

    /// <summary>Open a canvas by selecting its row in the files tree —
    /// the route a user takes, not a synthesized command.</summary>
    private static void OpenCanvasFromTree(
        Window window, UIA3Automation automation, string stem)
    {
        AutomationElement filesTree = WaitForElement(
            window, "FilesTree", TimeSpan.FromSeconds(30));
        AutomationElement item = filesTree
            .FindAllDescendants(
                automation.ConditionFactory.ByControlType(ControlType.TreeItem))
            .FirstOrDefault(candidate => candidate.Name.StartsWith(
                stem, StringComparison.OrdinalIgnoreCase))
            ?? throw new Xunit.Sdk.XunitException(
                $"the {stem}.canvas TreeItem is absent from the files tree.");
        item.Patterns.SelectionItem.Pattern.Select();
    }

    private static AutomationElement[] WaitForTreeItems(
        UIA3Automation automation, AutomationElement tree, int atLeast)
    {
        AutomationElement[] items = [];
        Assert.True(
            SpinWait.SpinUntil(
                () =>
                {
                    items = tree.FindAllDescendants(
                        automation.ConditionFactory.ByControlType(ControlType.TreeItem));
                    return items.Length >= atLeast;
                },
                TimeSpan.FromSeconds(20)),
            $"the canvas outline never materialized {atLeast} tree items "
            + $"(saw {items.Length})");
        return items;
    }

    /// <summary>
    /// The committed demo-vault canvases.
    ///
    /// Read from the OUTPUT DIRECTORY, never by walking up to a repo root
    /// — the same rule as <see cref="CitationStyleFixture"/>, and for the
    /// same reason: this gate downloads built binaries and runs with no
    /// checkout, so the repo does not exist at run time. The first version
    /// of THIS helper walked up to <c>Cargo.toml</c>; it passed locally
    /// and failed on CI in 4 ms, which is precisely the failure
    /// <c>CitationStyleFixture</c>'s comment already warned about.
    /// </summary>
    private static string DemoVaultCanvasDirectory()
    {
        string canvases = Path.Combine(AppContext.BaseDirectory, "fixtures", "canvas");
        Assert.True(
            Directory.Exists(canvases),
            $"The canvas fixtures are missing at {canvases}. They are linked "
            + "Content items in SlateWindows.AccessibilityTests.csproj and "
            + "must be copied to the output directory.");
        return canvases;
    }

    /// <summary><c>ERROR_HOTKEY_ALREADY_REGISTERED</c>.</summary>
    private const int ErrorHotkeyAlreadyRegistered = 1409;

    private static int _chordProbeId = 0x5131;

    private static class NativeHotkey
    {
        internal const uint ModAlt = 0x0001;
        internal const uint ModControl = 0x0002;

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        internal static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        internal static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    }

    /// <summary>
    /// Records that a chord leg was skipped because another process owns
    /// the chord, and fails instead when CI has declared the gate
    /// mandatory.
    /// </summary>
    private static void SkipStolenChordLeg(string chord)
    {
        if (RequiresUiAutomation())
        {
            throw new Xunit.Sdk.XunitException(
                $"{chord} is registered by another process (win32 1409), so the "
                + "app cannot receive it. On a CI runner that is a real failure: "
                + "the runner must not have global hotkey owners installed.");
        }

        Console.WriteLine(
            $"[accessibility] skipped the {chord} leg: another process holds the "
            + "chord globally (win32 1409). Environmental, not a product defect.");
    }

    /// <summary>Whether CI has declared the interactive UIA gate mandatory.</summary>
    private static bool RequiresUiAutomation() =>
        string.Equals(
            Environment.GetEnvironmentVariable("SLATE_REQUIRE_UI_AUTOMATION"),
            "1",
            StringComparison.Ordinal);

    /// <summary>
    /// The shared non-interactive gate. Returns <see langword="false"/>
    /// when the journey must degrade to a startup smoke.
    /// </summary>
    /// <remarks>
    /// Every journey used to make this decision itself, and only ONE of
    /// the thirteen honoured SLATE_REQUIRE_UI_AUTOMATION. On a
    /// non-interactive runner the other twelve returned green having
    /// asserted nothing, so the suite could report success while covering
    /// none of what it claims. Routing them through one gate means CI sets
    /// the variable once and the suite cannot silently degrade.
    /// </remarks>
    private static bool HasInteractiveDesktop(Process process, string gateName)
    {
        if (Environment.UserInteractive)
        {
            return true;
        }

        if (RequiresUiAutomation())
        {
            throw new Xunit.Sdk.XunitException(
                $"The {gateName} accessibility gate requires an interactive "
                + "Windows desktop, but this runner is executing in a "
                + "non-interactive session.");
        }

        Assert.False(
            process.WaitForExit(3_000),
            $"Slate exited during the {gateName} startup smoke.");
        return false;
    }

    private static Window WaitForMainWindow(
        Process process,
        UIA3Automation automation,
        string logFile,
        TimeSpan timeout)
    {
        Window? window = null;
        COMException? lastAutomationError = null;
        bool found = SpinWait.SpinUntil(
            () =>
            {
                if (process.HasExited)
                {
                    return true;
                }

                window = TryAutomationQuery(
                    () => automation
                        .GetDesktop()
                        .FindFirstChild(
                            automation.ConditionFactory.ByProcessId(process.Id))
                        ?.AsWindow(),
                    ref lastAutomationError);
                return window is not null;
            },
            timeout);

        if (process.HasExited)
        {
            string appLog = ReadSharedLog(logFile);
            throw new Xunit.Sdk.XunitException(
                $"Slate exited with code {process.ExitCode} before its main window appeared. " +
                $"stdout: {process.StandardOutput.ReadToEnd()} stderr: {process.StandardError.ReadToEnd()} " +
                $"app log: {appLog}");
        }

        if (!found || window is null)
        {
            throw new Xunit.Sdk.XunitException(
                "Slate main window did not become available through UIA3. " +
                $"Last UIA HRESULT: {lastAutomationError?.HResult:X8}. " +
                $"app log: {ReadSharedLog(logFile)}");
        }

        return window;
    }

    private static T? TryAutomationQuery<T>(
        Func<T?> query,
        ref COMException? lastAutomationError)
        where T : class
    {
        try
        {
            return query();
        }
        catch (COMException exception)
            when (exception.HResult == AutomationTimeoutHResult)
        {
            lastAutomationError = exception;
            return null;
        }
    }

    private static AutomationElement WaitForNamedElement(
        AutomationElement root,
        UIA3Automation automation,
        string name,
        TimeSpan timeout)
    {
        AutomationElement? element = null;
        Assert.True(
            SpinWait.SpinUntil(
                () =>
                {
                    element = root.FindFirstDescendant(
                        automation.ConditionFactory.ByName(name));
                    return element is not null;
                },
                timeout),
            $"The UIA element named '{name}' did not appear.");
        return element!;
    }

    private static AutomationElement WaitForEditor(
        AutomationElement root,
        UIA3Automation automation,
        string name,
        TimeSpan timeout)
    {
        AutomationElement? element = null;
        string[] observedNames = [];
        Assert.True(
            SpinWait.SpinUntil(
                () =>
                {
                    AutomationElement[] candidates = root.FindAllDescendants(
                        automation.ConditionFactory.ByAutomationId("MarkdownEditor"));
                    observedNames = candidates
                        .Select(candidate => candidate.Name)
                        .ToArray();
                    element = candidates.FirstOrDefault(candidate =>
                        string.Equals(candidate.Name, name, StringComparison.Ordinal));
                    return element is not null;
                },
                timeout),
            $"The editor named '{name}' did not appear. " +
            $"Observed MarkdownEditor names: [{string.Join(", ", observedNames)}].");
        return element!;
    }

    private static AutomationElement WaitForMenuItem(
        Window window,
        string menuAutomationId,
        string itemAutomationId,
        TimeSpan timeout)
    {
        AutomationElement menu = WaitForElement(window, menuAutomationId, timeout);
        Assert.True(
            menu.Patterns.ExpandCollapse.IsSupported,
            $"Menu {menuAutomationId} does not expose ExpandCollapse.");
        menu.Patterns.ExpandCollapse.Pattern.Expand();
        return WaitForElement(window, itemAutomationId, timeout);
    }

    private static string ReadSharedLog(string path)
    {
        if (!File.Exists(path))
        {
            return "<no app log>";
        }

        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static AutomationElement? TryWaitForElement(
        Window window,
        string automationId,
        TimeSpan timeout)
    {
        AutomationElement? element = null;
        _ = SpinWait.SpinUntil(
            () =>
            {
                element = window.FindFirstDescendant(
                    condition => condition.ByAutomationId(automationId));
                if (element is not null)
                {
                    return true;
                }

                Thread.Sleep(50);
                return false;
            },
            timeout);
        return element;
    }

    private static void PressChord(
        VirtualKeyShort modifier,
        VirtualKeyShort key)
    {
        using (Keyboard.Pressing(modifier))
        {
            Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(250));
            PressKey(key);
        }
        Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(250));
    }

    /// <summary>Two-modifier form, for chords like Ctrl+Shift+J.</summary>
    private static void PressChord(
        VirtualKeyShort firstModifier,
        VirtualKeyShort secondModifier,
        VirtualKeyShort key)
    {
        using (Keyboard.Pressing(firstModifier, secondModifier))
        {
            Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(250));
            PressKey(key);
        }
        Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(250));
    }

    /// <summary>Three-modifier form, for the G18-disambiguated chords —
    /// W6-1's Ctrl+Alt+Shift+I is the first (owner decision D-2).</summary>
    private static void PressChord(
        VirtualKeyShort firstModifier,
        VirtualKeyShort secondModifier,
        VirtualKeyShort thirdModifier,
        VirtualKeyShort key)
    {
        using (Keyboard.Pressing(firstModifier, secondModifier, thirdModifier))
        {
            Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(250));
            PressKey(key);
        }
        Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(250));
    }

    private static void PlaceCaretAtText(AutomationElement editor, string text)
    {
        Assert.True(
            editor.Patterns.Text.IsSupported,
            "The Markdown editor does not expose TextPattern.");
        var textPattern = editor.Patterns.Text.Pattern;
        var target = textPattern.DocumentRange.FindText(
            text,
            backward: false,
            ignoreCase: false)
            ?? throw new Xunit.Sdk.XunitException(
                $"The editor text does not contain '{text}'.");
        var caretTarget = target.Clone();
        caretTarget.MoveEndpointByRange(
            TextPatternRangeEndpoint.End,
            caretTarget,
            TextPatternRangeEndpoint.Start);
        var prefix = textPattern.DocumentRange.Clone();
        prefix.MoveEndpointByRange(
            TextPatternRangeEndpoint.End,
            target,
            TextPatternRangeEndpoint.Start);
        int targetOffset = prefix.GetText(-1).Length;
        Assert.InRange(targetOffset, 0, 128);

        // AvalonEdit's TextRangeProvider.Select does not move TextArea.Caret.
        // Anchor the real caret and navigate through the bounded ASCII fixture.
        PressChord(VirtualKeyShort.CONTROL, VirtualKeyShort.HOME);
        for (int index = 0; index < targetOffset; index++)
        {
            PressKey(VirtualKeyShort.RIGHT);
        }

        Assert.True(
            SpinWait.SpinUntil(
                () =>
                {
                    var selections = textPattern.GetSelection();
                    bool atTarget = selections.Length == 1
                        && selections[0].CompareEndpoints(
                            TextPatternRangeEndpoint.Start,
                            caretTarget,
                            TextPatternRangeEndpoint.Start) == 0
                        && selections[0].CompareEndpoints(
                            TextPatternRangeEndpoint.End,
                            caretTarget,
                            TextPatternRangeEndpoint.End) == 0;
                    if (!atTarget)
                    {
                        Thread.Sleep(50);
                    }

                    return atTarget;
                },
                TimeSpan.FromSeconds(5)),
            $"The editor caret did not move to '{text}'.");
    }
    private static void PressKey(VirtualKeyShort key)
    {
        Keyboard.Type(key);
        Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(250));
    }
    private static AutomationElement WaitForElement(
        Window window,
        string automationId,
        TimeSpan timeout)
    {
        AutomationElement? element = null;
        bool found = SpinWait.SpinUntil(
            () =>
            {
                element = window.FindFirstDescendant(
                    condition => condition.ByAutomationId(automationId));
                if (element is not null)
                {
                    return true;
                }

                Thread.Sleep(50);
                return false;
            },
            timeout);

        Assert.True(found, $"UIA element {automationId} did not become available.");
        return element!;
    }

    /// <summary>Client-side registration of the Word/Chromium MathML
    /// convention property — the exact dance NVDA performs. Returns
    /// the process-local property id mapped from the shared GUID.</summary>
    private static int RegisterMathMlPropertyAsClient()
    {
        var registrar = (IUIAutomationRegistrar)new CUIAutomationRegistrar();
        nint name = Marshal.StringToCoTaskMemUni("MathML");
        try
        {
            var info = new UiaPropertyInfo
            {
                Guid = new Guid("FA170AB3-3229-4E7C-827F-DD05EE0481D9"),
                ProgrammaticName = name,
                Type = 3, // UIAutomationType_String
            };
            registrar.RegisterProperty(ref info, out int id);
            return id;
        }
        finally
        {
            Marshal.FreeCoTaskMem(name);
        }
    }

    [System.Runtime.InteropServices.ComImport]
    [System.Runtime.InteropServices.Guid("6E29FABF-9977-42D1-8D0E-CA7E61AD87E6")]
    private class CUIAutomationRegistrar
    {
    }

    [System.Runtime.InteropServices.ComImport]
    [System.Runtime.InteropServices.Guid("8609C4EC-4A1A-4D88-A357-5A66E060E1CF")]
    [System.Runtime.InteropServices.InterfaceType(
        System.Runtime.InteropServices.ComInterfaceType.InterfaceIsIUnknown)]
    private interface IUIAutomationRegistrar
    {
        void RegisterProperty(ref UiaPropertyInfo property, out int propertyId);
    }

    [System.Runtime.InteropServices.StructLayout(
        System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct UiaPropertyInfo
    {
        public Guid Guid;
        public nint ProgrammaticName;
        public int Type;
    }

    private static string SlateWindowsExe()
    {
        string exe = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "SlateWindows", "bin", BuildConfiguration(),
            "net10.0-windows", "SlateWindows.exe");
        exe = Path.GetFullPath(exe);
        Assert.True(File.Exists(exe), $"SlateWindows.exe not built at {exe}.");
        return exe;
    }

    private static string BuildConfiguration()
    {
#if DEBUG
        return "Debug";
#else
        return "Release";
#endif
    }
}
