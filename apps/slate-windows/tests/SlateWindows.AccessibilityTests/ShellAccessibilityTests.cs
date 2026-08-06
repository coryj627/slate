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
                "Move selected",
                "Remove",
                "Trash selected");
            AssertActionButtonCensus(
                WaitForElement(window, "SidebarFileActions", TimeSpan.FromSeconds(10)),
                automation,
                "Add shortcut",
                "Cancel import",
                "Copy link",
                "Delete",
                "Delete folder note",
                "Folder note",
                "Import…",
                "New folder",
                "New note",
                "New tab",
                "Open",
                "Pin",
                "Rename",
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
            Keyboard.TypeSimultaneously(
                VirtualKeyShort.CONTROL,
                VirtualKeyShort.ALT,
                VirtualKeyShort.LEFT);
            AssertEventuallyFocused(
                leftEditor,
                "Ctrl+Alt+Left changed the model but did not move keyboard focus to the left editor.");

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

    internal static void AssertAxeClean(Process process, string surface)
    {
        var config = Config.Builder.ForProcessId(process.Id).Build();
        var output = ScannerFactory.CreateScanner(config).Scan(null);
        Assert.NotEmpty(output.WindowScanOutputs);
        var errors = output.WindowScanOutputs
            .SelectMany(result => result.Errors)
            .ToArray();
        object[] evidenceErrors = errors.Select(error => (object)new
        {
            ruleId = error.Rule.ID,
            description = error.Rule.Description,
            elementProperties = error.Element.Properties
                .Select(property => property.ToString())
                .ToArray(),
        }).ToArray();
        WriteAxeEvidence(
            surface,
            output.WindowScanOutputs.Count,
            evidenceErrors);
        Assert.True(
            errors.Length == 0,
            string.Join(
                Environment.NewLine,
                errors.Select(error =>
                    $"{error.Rule.ID}: {error.Rule.Description}; " +
                    string.Join(", ", error.Element.Properties))));
    }

    private static void WriteAxeEvidence(
        string surface,
        int windowCount,
        object[] errors)
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
            schemaVersion = 1,
            surface,
            recordedAtUtc = DateTimeOffset.UtcNow,
            sourceRevision = Environment.GetEnvironmentVariable("GITHUB_SHA"),
            operatingSystem = Environment.OSVersion.VersionString,
            dotnetRuntime = Environment.Version.ToString(),
            userInteractive = Environment.UserInteractive,
            scannedWindowCount = windowCount,
            outcome = errors.Length == 0 ? "pass" : "fail",
            errors,
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

            if (!Environment.UserInteractive)
            {
                // Session-0 fallback, as the shell gate: the desktop UIA
                // half runs on interactive runners; here the process must
                // still survive startup.
                Assert.False(
                    process.WaitForExit(3_000),
                    "Slate exited during the identity-contract startup smoke. " +
                    $"app log: {ReadSharedLog(Path.Combine(logDirectory, "slate-windows.log"))}");
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

            if (!Environment.UserInteractive)
            {
                // Session-0 fallback, as the shell gate: the UIA half
                // needs a desktop; here the process must still survive
                // startup.
                Assert.False(
                    process.WaitForExit(3_000),
                    "Slate exited during the RangeFromChild startup smoke. " +
                    $"app log: {ReadSharedLog(Path.Combine(logDirectory, "slate-windows.log"))}");
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

    private static void AssertEventuallyFocused(AutomationElement element, string message)
    {
        Assert.True(
            SpinWait.SpinUntil(
                () => element.Properties.HasKeyboardFocus.Value,
                TimeSpan.FromSeconds(10)),
            message);
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

            if (!Environment.UserInteractive)
            {
                // Session-0 fallback, as the shell gate.
                Assert.False(
                    process.WaitForExit(3_000),
                    "Slate exited during the panels startup smoke. " +
                    $"app log: {ReadSharedLog(Path.Combine(logDirectory, "slate-windows.log"))}");
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

            if (!Environment.UserInteractive)
            {
                Assert.False(
                    process.WaitForExit(3_000),
                    "Slate exited during the tasks startup smoke. " +
                    $"app log: {ReadSharedLog(Path.Combine(logDirectory, "slate-windows.log"))}");
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

            if (!Environment.UserInteractive)
            {
                Assert.False(
                    process.WaitForExit(3_000),
                    "Slate exited during the properties startup smoke. " +
                    $"app log: {ReadSharedLog(Path.Combine(logDirectory, "slate-windows.log"))}");
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

            if (!Environment.UserInteractive)
            {
                // Session-0 fallback, as the shell gate.
                Assert.False(
                    process.WaitForExit(3_000),
                    "Slate exited during the citations startup smoke. " +
                    $"app log: {ReadSharedLog(Path.Combine(logDirectory, "slate-windows.log"))}");
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

            PressKey(VirtualKeyShort.ESCAPE);
            AssertElementDisappears(window, automation, "CitationDetailsSheet");
            AssertEventuallyFocused(
                resolvedRow, "Escape did not return focus to the citation row.");

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

            if (!Environment.UserInteractive)
            {
                Assert.False(
                    process.WaitForExit(3_000),
                    "Slate exited during the degraded-vault smoke. "
                        + $"app log: {ReadSharedLog(Path.Combine(logDirectory, "slate-windows.log"))}");
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
