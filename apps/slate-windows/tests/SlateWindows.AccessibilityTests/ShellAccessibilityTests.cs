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
            "---\ntags:\n  - accessibility\n---\n# Accessible note\n");
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
            AutomationElement tabs = WaitForElement(
                window,
                "WorkspaceTabs",
                TimeSpan.FromSeconds(10));
            AssertEventuallyFocused(
                tabs,
                "Opening a vault did not focus its active TabControl.");

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
            AutomationElement editor = WaitForNamedElement(
                window,
                automation,
                "note.md editor",
                TimeSpan.FromSeconds(10));
            editor.Focus();
            AssertEventuallyFocused(editor, "The opened note editor could not receive focus.");
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
            AutomationElement tagItem = tagTree.FindFirstDescendant(
                automation.ConditionFactory.ByControlType(ControlType.TreeItem))
                ?? throw new Xunit.Sdk.XunitException("The tag TreeItem is absent.");
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
            Assert.True(editor.Patterns.Value.IsSupported);
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
                automation.ConditionFactory.ByControlType(ControlType.Edit))
                ?? throw new Xunit.Sdk.XunitException("The left split editor is absent.");
            AutomationElement rightEditor = splitTabs[1].FindFirstDescendant(
                automation.ConditionFactory.ByControlType(ControlType.Edit))
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
            editor = WaitForNamedElement(
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

    private static void AssertAxeClean(Process process, string surface)
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

    private static void AssertEventuallyFocused(AutomationElement element, string message)
    {
        Assert.True(
            SpinWait.SpinUntil(
                () => element.Properties.HasKeyboardFocus.Value,
                TimeSpan.FromSeconds(10)),
            message);
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
                return element is not null;
            },
            timeout);

        Assert.True(found, $"UIA element {automationId} did not become available.");
        return element!;
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
