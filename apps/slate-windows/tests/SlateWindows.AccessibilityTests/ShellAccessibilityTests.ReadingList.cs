// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;

namespace SlateWindows.AccessibilityTests;

public sealed partial class ShellAccessibilityTests
{
    private const int BigListItems = 1_996;
    private const int SmallListItems = 3;

    /// <summary>WB-11 (#1329): arrowing through the largest note the reading
    /// view renders costs about what arrowing through a small one does. The
    /// note is the issue's shape cut to the 2,000-block cap (core makes every
    /// list item a block): 4 + 1,996 blocks, so every line renders. The
    /// client does what NVDA does — it holds NVDA's event registrations for
    /// the whole journey (WPF does the peer work behind an event only while a
    /// client listens) and after every key runs NVDA's per-move queries,
    /// timed from the press. A miss of content fails as "does not fit the
    /// reading cap" or a reach message; only then is every move held to
    /// max(5 × the small note's median for that kind of move, 250 ms), a
    /// ratio because the shell gate runs every journey with no filter.</summary>
    [Fact]
    public void ReadingList_ArrowingThroughTheBlockCapIsBounded()
    {
        string root = Path.Combine(Path.GetTempPath(), "slate-reading-list-" + Guid.NewGuid().ToString("N"));
        string vault = Path.Combine(root, "Vault");
        string logs = Path.Combine(root, "logs");
        Directory.CreateDirectory(vault);
        File.WriteAllText(Path.Combine(vault, "big-list.md"), ListNote("Big list", BigListItems));
        File.WriteAllText(Path.Combine(vault, "small-list.md"), ListNote("Small list", SmallListItems));

        Process? process = null;
        try
        {
            var start = new ProcessStartInfo(SlateWindowsExe()) { UseShellExecute = false };
            ShellLaunch.ApplyDeviceState(start);
            start.ArgumentList.Add(vault);
            start.Environment["SLATE_CENSUS_INSTANCE_ID"] = "reading-list-" + Guid.NewGuid().ToString("N");
            start.Environment["SLATE_LOG_DIR"] = logs;
            process = Process.Start(start) ?? throw new Xunit.Sdk.XunitException("Slate did not start.");
            if (!HasInteractiveDesktop(process, "reading-list")) { return; }
            using var automation = new UIA3Automation();
            Window window = WaitForMainWindow(process, automation, Path.Combine(logs, "slate-windows.log"), TimeSpan.FromSeconds(30));
            window.SetForeground();
            WaitForElement(window, "RightPaneLeaves", TimeSpan.FromSeconds(30));

            // (a) NVDA's registrations, held for the whole journey.
            int heard = 0;
            var focus = automation.RegisterFocusChangedEvent(_ => Interlocked.Increment(ref heard));
            try
            {
                using var selection = window.RegisterAutomationEvent(
                    automation.EventLibrary.Text.TextSelectionChangedEvent, TreeScope.Subtree, (_, _) => Interlocked.Increment(ref heard));
                using var changed = window.RegisterAutomationEvent(
                    automation.EventLibrary.Text.TextChangedEvent, TreeScope.Subtree, (_, _) => Interlocked.Increment(ref heard));
                using var structure = window.RegisterStructureChangedEvent(
                    TreeScope.Subtree, (_, _, _) => Interlocked.Increment(ref heard));
                var properties = automation.PropertyLibrary;
                using var property = window.RegisterPropertyChangedEvent(
                    TreeScope.Subtree,
                    (_, _, _) => Interlocked.Increment(ref heard),
                    properties.Element.Name,
                    properties.Element.ItemStatus,
                    properties.Element.IsEnabled,
                    properties.Value.Value,
                    properties.Toggle.ToggleState,
                    properties.ExpandCollapse.ExpandCollapseState);

                // A warm pass over the small note, unmeasured, so neither
                // note's numbers carry the reading view's first use.
                _ = WalkTheListNote(window, automation, "small-list.md", "Small list", SmallListItems);
                List<ReadingMove> small = WalkTheListNote(window, automation, "small-list.md", "Small list", SmallListItems);
                List<ReadingMove> big = WalkTheListNote(window, automation, "big-list.md", "Big list", BigListItems);

                string table = string.Join(
                    "; ",
                    small.Zip(big, (s, b) => $"{b.Key}: {b.Elapsed.TotalMilliseconds:F0} ms (small {s.Elapsed.TotalMilliseconds:F0} ms)"));
                TestContext.Current.TestOutputHelper?.WriteLine($"WB-11 reading list moves ({heard} events heard): {table}");
                var slow = new List<string>();
                foreach (ReadingMove move in big)
                {
                    double median = Median(small.Where(s => s.Kind == move.Kind).Select(s => s.Elapsed.TotalMilliseconds));
                    double bound = Math.Max(5 * median, 250);
                    if (move.Elapsed.TotalMilliseconds > bound)
                    {
                        slow.Add($"{move.Key} took {move.Elapsed.TotalMilliseconds:F0} ms, bound {bound:F0} ms");
                    }
                }
                Assert.True(
                    slow.Count == 0,
                    $"Arrowing through big-list.md is too slow: {string.Join("; ", slow)}. All moves: {table}");
            }
            finally
            {
                automation.UnregisterFocusChangedEvent(focus);
            }
        }
        finally
        {
            try { process?.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            try { Directory.Delete(root, recursive: true); } catch (IOException) { }
        }
    }

    /// <summary>WB-11 (#1330): Ctrl+A in the reading view raises
    /// TextSelectionChanged on the surface, and the selection a client reads
    /// back is the whole note — what Narrator needs to say the selection and
    /// read it back.</summary>
    [Fact]
    public void ReadingSelectAll_RaisesTextSelectionChangedAndReturnsTheNote()
    {
        string root = Path.Combine(Path.GetTempPath(), "slate-reading-select-all-" + Guid.NewGuid().ToString("N"));
        string vault = Path.Combine(root, "Vault");
        string logs = Path.Combine(root, "logs");
        Directory.CreateDirectory(vault);
        File.WriteAllText(Path.Combine(vault, "trailing.md"), "body\n\n#\n");

        Process? process = null;
        try
        {
            var start = new ProcessStartInfo(SlateWindowsExe()) { UseShellExecute = false };
            ShellLaunch.ApplyDeviceState(start);
            start.ArgumentList.Add(vault);
            start.Environment["SLATE_CENSUS_INSTANCE_ID"] = "reading-select-all-" + Guid.NewGuid().ToString("N");
            start.Environment["SLATE_LOG_DIR"] = logs;
            process = Process.Start(start) ?? throw new Xunit.Sdk.XunitException("Slate did not start.");
            if (!HasInteractiveDesktop(process, "reading-select-all")) { return; }
            using var automation = new UIA3Automation();
            Window window = WaitForMainWindow(process, automation, Path.Combine(logs, "slate-windows.log"), TimeSpan.FromSeconds(30));
            window.SetForeground();
            WaitForElement(window, "RightPaneLeaves", TimeSpan.FromSeconds(30));

            AutomationElement surface = EnterReadingView(window, automation, "trailing.md", "body");
            var text = surface.Patterns.Text.Pattern;
            var heard = new System.Collections.Concurrent.ConcurrentQueue<long>();
            using var selection = surface.RegisterAutomationEvent(
                automation.EventLibrary.Text.TextSelectionChangedEvent,
                TreeScope.Element,
                (_, _) => heard.Enqueue(Stopwatch.GetTimestamp()));
            // The listener is live before Ctrl+A is judged: WPF raises the
            // event only once it knows a client listens, so a caret move must
            // be heard first. Then the caret's events settle at the top.
            PressChord(VirtualKeyShort.CONTROL, VirtualKeyShort.END);
            Assert.True(
                SpinWait.SpinUntil(() => !heard.IsEmpty, TimeSpan.FromSeconds(10)),
                $"Ctrl+End raised no TextSelectionChanged within 10 s; focus is {DescribeFocusedElement(automation)}. {FocusDiagnosis()}");
            PressChord(VirtualKeyShort.CONTROL, VirtualKeyShort.HOME);
            Assert.True(
                SpinWait.SpinUntil(
                    () => Stopwatch.GetElapsedTime(heard.Last()) > TimeSpan.FromMilliseconds(750),
                    TimeSpan.FromSeconds(10)),
                "The caret's selection events never settled.");

            long pressed = Stopwatch.GetTimestamp();
            PressChord(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
            bool raised = SpinWait.SpinUntil(() => heard.Any(at => at > pressed), TimeSpan.FromSeconds(2));
            string whole = text.DocumentRange.GetText(-1);
            string selected = text.GetSelection().Single().GetText(-1);
            TestContext.Current.TestOutputHelper?.WriteLine($"WB-11 select all: event {(raised ? "raised" : "NOT raised")}; selection '{selected}'; note '{whole}'");
            Assert.True(
                raised,
                $"Ctrl+A raised no TextSelectionChanged on the reading surface within 2 s; the selection read '{selected}'; focus is {DescribeFocusedElement(automation)}.");
            Assert.Equal(whole, selected);
            Assert.Contains("body", selected, StringComparison.Ordinal);
        }
        finally
        {
            try { process?.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            try { Directory.Delete(root, recursive: true); } catch (IOException) { }
        }
    }

    private enum ReadingMoveKind { Entry, Line, ListLine }

    private sealed record ReadingMove(string Key, ReadingMoveKind Kind, TimeSpan Elapsed);

    private static string ListNote(string title, int items)
    {
        var note = new System.Text.StringBuilder();
        note.Append("# ").Append(title).Append("\n\nIntro line.\n\n");
        for (int i = 1; i <= items; i++)
        {
            note.Append("- item ").Append(i).Append('\n');
        }
        note.Append("\n## After the list\n\nClosing paragraph.\n");
        return note.ToString();
    }

    /// <summary>Opens <paramref name="file"/>, enters its reading view and
    /// walks it: the entry, Ctrl+Home, four Downs, Ctrl+End, four Ups, each
    /// timed with its workload, its reach checked untimed after it. Leaves the
    /// tab in its editor.</summary>
    private static List<ReadingMove> WalkTheListNote(
        Window window, UIA3Automation automation, string file, string title, int items)
    {
        var moves = new List<ReadingMove>();
        AutomationElement tree = WaitForElement(window, "FilesTree", TimeSpan.FromSeconds(10));
        WaitForTreeItemStartingWith(tree, automation, file).Patterns.SelectionItem.Pattern.Select();
        AutomationElement editor = WaitForEditor(window, automation, $"{file} editor", TimeSpan.FromSeconds(10));
        editor.Focus();
        AssertEventuallyFocused(editor, $"{file}'s editor did not take focus.");

        var timer = Stopwatch.StartNew();
        PressChord(VirtualKeyShort.CONTROL, VirtualKeyShort.SHIFT, VirtualKeyShort.KEY_E);
        AutomationElement surface = WaitForElement(window, "ReadingSurface", TimeSpan.FromSeconds(120));
        var text = surface.Patterns.Text.Pattern;
        bool reached = SpinWait.SpinUntil(
            () =>
            {
                try { return text.DocumentRange.GetText(-1).Contains("Closing paragraph.", StringComparison.Ordinal); }
                catch (Exception exception) when (IsTransientUiaFault(exception)) { return false; }
            },
            TimeSpan.FromSeconds(120));
        ReadingWorkload entry = reached ? RunReadingWorkload(automation, surface, expectedLine: null) : default;
        timer.Stop();
        string document = text.DocumentRange.GetText(-1);
        Assert.True(
            reached && !document.Contains("Reading view shows the first", StringComparison.Ordinal),
            $"{file} does not fit the reading cap: the reading view never held its closing paragraph, or held the degraded notice.");
        moves.Add(new ReadingMove($"{file} entry", ReadingMoveKind.Entry, timer.Elapsed));
        _ = entry;

        (string Key, VirtualKeyShort? Modifier, VirtualKeyShort Key2, string Line)[] script =
        [
            ("Ctrl+Home", VirtualKeyShort.CONTROL, VirtualKeyShort.HOME, title),
            ("Down 1", null, VirtualKeyShort.DOWN, "Intro line."),
            ("Down 2", null, VirtualKeyShort.DOWN, "item 1"),
            ("Down 3", null, VirtualKeyShort.DOWN, "item 2"),
            ("Down 4", null, VirtualKeyShort.DOWN, "item 3"),
            ("Ctrl+End", VirtualKeyShort.CONTROL, VirtualKeyShort.END, "Closing paragraph."),
            ("Up 1", null, VirtualKeyShort.UP, "After the list"),
            ("Up 2", null, VirtualKeyShort.UP, $"item {items}"),
            ("Up 3", null, VirtualKeyShort.UP, $"item {items - 1}"),
            ("Up 4", null, VirtualKeyShort.UP, $"item {items - 2}"),
        ];
        foreach ((string key, VirtualKeyShort? modifier, VirtualKeyShort press, string line) in script)
        {
            timer.Restart();
            if (modifier is { } held)
            {
                using (Keyboard.Pressing(held))
                {
                    NavigationKeys.Type(press);
                }
            }
            else
            {
                NavigationKeys.Type(press);
            }
            ReadingWorkload workload = RunReadingWorkload(automation, surface, line);
            timer.Stop();
            bool isItem = line.StartsWith("item ", StringComparison.Ordinal);
            moves.Add(new ReadingMove(key, isItem ? ReadingMoveKind.ListLine : ReadingMoveKind.Line, timer.Elapsed));

            // Reach, untimed: the line is the move's, and a list line's item
            // and list say where the reader is. A bullet item's name carries
            // the bullet WPF renders ("•\titem 1").
            Assert.True(
                workload.Line == line,
                $"{file} {key}: the caret line read '{workload.Line}', not '{line}' within 120 s; focus is {DescribeFocusedElement(automation)}. {FocusDiagnosis()}");
            if (isItem)
            {
                int n = int.Parse(line["item ".Length..], System.Globalization.CultureInfo.InvariantCulture);
                Assert.True(
                    workload.ItemName?.TrimStart('•', '\t') == line && workload.Position == n && workload.Size == items && workload.ListName == $"{items} entries",
                    $"{file} {key}: the list context read item '{workload.ItemName}' {workload.Position} of {workload.Size} in '{workload.ListName}'.");
            }
        }

        PressChord(VirtualKeyShort.CONTROL, VirtualKeyShort.SHIFT, VirtualKeyShort.KEY_E);
        AssertEventuallyFocused(WaitForEditor(window, automation, $"{file} editor", TimeSpan.FromSeconds(30)), $"Ctrl+Shift+E back did not land {file}'s editor.");
        return moves;
    }

    private readonly record struct ReadingWorkload(string Line, string? ListName, string? ItemName, int Position, int Size);

    /// <summary>NVDA's per-move queries against the reading surface: the
    /// caret line (polled until it is <paramref name="expectedLine"/>, 120 s),
    /// its StyleId, the enclosing element and its raw-view ancestors' control
    /// fields, the line's children each resolved to a range, and for a list
    /// line the list and the item by sibling steps from the nearer end.</summary>
    private static ReadingWorkload RunReadingWorkload(UIA3Automation automation, AutomationElement surface, string? expectedLine)
    {
        var text = surface.Patterns.Text.Pattern;
        var walker = automation.TreeWalkerFactory.GetRawViewWalker();
        FlaUI.Core.ITextRange? line = null;
        string read = string.Empty;
        var deadline = Stopwatch.StartNew();
        do
        {
            try
            {
                line = text.GetSelection()[0].Clone();
                line.ExpandToEnclosingUnit(TextUnit.Line);
                // WPF can expand a caret at a heading's end over the next
                // line too ("After the list\r\nClosing paragraph."); the
                // caret's line is the first.
                read = line.GetText(-1).Split('\r', '\n')[0].Trim();
            }
            catch (Exception exception) when (IsTransientUiaFault(exception))
            {
                line = null;
            }
        }
        while ((line is null || (expectedLine is not null && read != expectedLine)) && deadline.Elapsed < TimeSpan.FromSeconds(120));
        if (line is null)
        {
            return new ReadingWorkload(read, null, null, 0, 0);
        }

        _ = line.GetAttributeValue(automation.TextAttributeLibrary.StyleId);
        AutomationElement? element = line.GetEnclosingElement();
        for (int depth = 0; element is not null && depth < 12; depth++)
        {
            _ = element.Properties.Name.ValueOrDefault;
            _ = element.Properties.ControlType.ValueOrDefault;
            _ = element.Properties.PositionInSet.ValueOrDefault;
            _ = element.Properties.SizeOfSet.ValueOrDefault;
            _ = element.Properties.Level.ValueOrDefault;
            if (element.Properties.AutomationId.ValueOrDefault == "ReadingSurface")
            {
                break;
            }
            element = walker.GetParent(element);
        }
        foreach (AutomationElement child in line.GetChildren())
        {
            _ = text.RangeFromChild(child);
        }

        if (!read.StartsWith("item ", StringComparison.Ordinal)
            || !int.TryParse(read["item ".Length..], System.Globalization.CultureInfo.InvariantCulture, out int n))
        {
            return new ReadingWorkload(read, null, null, 0, 0);
        }
        AutomationElement? list = walker.GetFirstChild(surface);
        while (list is not null && list.Properties.ControlType.ValueOrDefault != ControlType.List)
        {
            list = walker.GetNextSibling(list);
        }
        if (list is null)
        {
            return new ReadingWorkload(read, null, null, 0, 0);
        }
        string listName = list.Properties.Name.ValueOrDefault ?? string.Empty;
        int size = 0;
        AutomationElement? item;
        if (n <= 3)
        {
            item = walker.GetFirstChild(list);
            for (int i = 1; i < n && item is not null; i++)
            {
                item = walker.GetNextSibling(item);
            }
        }
        else
        {
            item = walker.GetLastChild(list);
            size = item?.Properties.SizeOfSet.ValueOrDefault ?? 0;
            for (int i = size; i > n && item is not null; i--)
            {
                item = walker.GetPreviousSibling(item);
            }
        }
        return item is null
            ? new ReadingWorkload(read, listName, null, 0, 0)
            : new ReadingWorkload(
                read,
                listName,
                item.Properties.Name.ValueOrDefault,
                item.Properties.PositionInSet.ValueOrDefault,
                item.Properties.SizeOfSet.ValueOrDefault);
    }

    /// <summary>Opens <paramref name="file"/> and toggles its reading view
    /// with Ctrl+Shift+E, returning the surface once it reads
    /// <paramref name="noteText"/>.</summary>
    private static AutomationElement EnterReadingView(Window window, UIA3Automation automation, string file, string noteText)
    {
        AutomationElement tree = WaitForElement(window, "FilesTree", TimeSpan.FromSeconds(10));
        WaitForTreeItemStartingWith(tree, automation, file).Patterns.SelectionItem.Pattern.Select();
        AutomationElement editor = WaitForEditor(window, automation, $"{file} editor", TimeSpan.FromSeconds(10));
        editor.Focus();
        AssertEventuallyFocused(editor, $"{file}'s editor did not take focus.");
        PressChord(VirtualKeyShort.CONTROL, VirtualKeyShort.SHIFT, VirtualKeyShort.KEY_E);
        AssertReadingSurfaceLanding(window, automation, $"Ctrl+Shift+E into {file}'s reading view", noteText);
        return WaitForElement(window, "ReadingSurface", TimeSpan.FromSeconds(10));
    }

    private static double Median(IEnumerable<double> values)
    {
        double[] sorted = values.OrderBy(value => value).ToArray();
        if (sorted.Length == 0)
        {
            return 0;
        }
        int middle = sorted.Length / 2;
        return sorted.Length % 2 == 1 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2;
    }
}
