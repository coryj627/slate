// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics;
using System.Text.Json;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.Tools;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;

namespace SlateWindows.AccessibilityTests;

public sealed partial class ShellAccessibilityTests
{
    /// <summary>
    /// #1254 (R-11): typing into the palette at speed leaves exactly the
    /// view model's selection — never a row the user did not choose — and
    /// the rows core ranked for the final query.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Real keystrokes in one burst, as the NVDA pass typed "split": the
    /// ValuePattern would collapse the five query changes into one, and
    /// both the stall and the stale "Selected: New Canvas" lived in the
    /// per-keystroke path.
    /// </para>
    /// <para>
    /// TODO(#1244): the announcement half of this journey waits for PR 1's
    /// desktop-scoped notification listener (R-1), which this branch does
    /// not carry; see the TODO blocks below for where it subscribes and what
    /// it asserts. Until then the journey pins what the UI itself shows.
    /// The stale-selection mechanism is pinned deterministically by the
    /// hosted <c>CommandPaletteTests.TheShippedListAnnouncesOnlyTheViewModelsSelection</c>.
    /// </para>
    /// </remarks>
    [Fact]
    public void Palette_TypingAnnouncesOnlyTheFinalCountAndSelection()
    {
        string testRoot = Path.Combine(
            Path.GetTempPath(), $"slate-palette-typing-{Guid.NewGuid():N}");
        string vaultRoot = Path.Combine(testRoot, "Palette Typing Vault");
        string logDirectory = Path.Combine(testRoot, "logs");
        Directory.CreateDirectory(vaultRoot);
        File.WriteAllText(Path.Combine(vaultRoot, "alpha.md"), "# Alpha\n\nBody.\n");

        // Core's count for the final query, over the catalog the app
        // registers (chords.json travels with the binaries). Recents move
        // rows between sections but never change how many match.
        int splitMatches = PaletteMatchCount("split");
        Assert.True(splitMatches > 0, "no registered command matches \"split\"");

        // TODO(#1244): subscribe PR 1's desktop-scoped notification listener
        // HERE, before launch (R-1: announcements reach a listener from the
        // first frame), and keep it for both legs below.

        Process? process = null;
        try
        {
            var startInfo = new ProcessStartInfo(SlateWindowsExe()) { UseShellExecute = false };
            startInfo.ArgumentList.Add(vaultRoot);
            startInfo.Environment["SLATE_CENSUS_INSTANCE_ID"] =
                $"slate-palette-typing-{Guid.NewGuid():N}";
            startInfo.Environment["SLATE_LOG_DIR"] = logDirectory;
            process = Process.Start(startInfo)
                ?? throw new Xunit.Sdk.XunitException("SlateWindows.exe did not start.");
            if (!HasInteractiveDesktop(process, "palette-typing"))
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
            WaitForVaultOpen(window);

            // --- leg 1: "split", typed fast ----------------------------------
            (AutomationElement search, AutomationElement results) = OpenPaletteByChord(window);
            TypeIntoSearch(window, search, "split");

            AutomationElement selected = WaitForOneSelectedRow(results, automation);
            string selectedName = selected.Name;
            // The row the pass heard announced for a query that excludes it.
            Assert.False(
                selectedName.StartsWith("New Canvas", StringComparison.Ordinal),
                "typing \"split\" left the stale New Canvas row selected");
            AssertRowOnScreen(selected, results);
            // The grouped rows virtualize (R-11), so the list exposes the
            // rows near the viewport — never more than core ranked, and
            // always the selected one.
            string[] listed = WaitForListedRows(results, automation, minimum: 1);
            Assert.InRange(listed.Length, 1, splitMatches);
            Assert.Contains(selectedName, listed);

            // TODO(#1244), with the listener: across leg 1 exactly ONE
            // PaletteFilterCount, rendered by core for (splitMatches,
            // "split"), and no PaletteCommandSelected naming any row but
            // selectedName — the trailing count window (P10) and R-11's
            // selection guard, heard the way NVDA hears them.

            // --- leg 2: a selection that survives without being first -------
            // End selects the last row, which a one-letter query from its
            // own label keeps (P7) while ranking others above it: the case
            // that used to re-select the first row on the user's behalf.
            PressKey(VirtualKeyShort.ESCAPE);
            WaitForPaletteClosed(window, automation);
            (search, results) = OpenPaletteByChord(window);
            PressKey(VirtualKeyShort.END);
            AutomationElement last = WaitForOneSelectedRow(results, automation);
            string lastName = last.Name;
            string letter = QueryLetterFor(LabelOf(lastName));
            TypeIntoSearch(window, search, letter);

            AutomationElement kept = WaitForOneSelectedRow(results, automation);
            Assert.Equal(lastName, kept.Name);
            AssertRowOnScreen(kept, results);
            string firstListed = WaitForListedRows(results, automation, minimum: 2)[0];
            Assert.NotEqual(lastName, firstListed);

            // TODO(#1244), with the listener: across leg 2 NO
            // PaletteCommandSelected at all — the id survived (P7) — and
            // exactly one PaletteFilterCount, for `letter`.

            PressKey(VirtualKeyShort.ESCAPE);
            WaitForPaletteClosed(window, automation);
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

    /// <summary>Ctrl+Shift+P from the files tree, then the caret in the
    /// search box — the palette journey's opening, including its measured
    /// need for real keyboard focus before the chord.</summary>
    private static (AutomationElement Search, AutomationElement Results) OpenPaletteByChord(Window window)
    {
        WaitForElement(window, "FilesTree", TimeSpan.FromSeconds(10)).Focus();
        Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(250));
        ReassertForegroundForAChord(window);
        PressChord(VirtualKeyShort.CONTROL, VirtualKeyShort.SHIFT, VirtualKeyShort.KEY_P);
        AutomationElement search = WaitForElement(window, "CommandPaletteSearch", TimeSpan.FromSeconds(10));
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
        return (search, WaitForElement(window, "CommandPaletteResults", TimeSpan.FromSeconds(10)));
    }

    /// <summary>Real keystrokes, in one burst, into the search box — with
    /// the foreground and the caret re-asserted first, because a key sent
    /// while another window holds the foreground is lost, not queued.</summary>
    private static void TypeIntoSearch(Window window, AutomationElement search, string text)
    {
        window.SetForeground();
        search.Focus();
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
            "the palette's search box lost the caret before typing");
        Keyboard.Type(text);
        WaitForQuery(search, text);
    }

    private static void WaitForQuery(AutomationElement search, string query) =>
        Assert.True(
            SpinWait.SpinUntil(
                () =>
                {
                    try
                    {
                        return string.Equals(
                            search.Patterns.Value.Pattern.Value.ValueOrDefault,
                            query,
                            StringComparison.Ordinal);
                    }
                    catch (System.Runtime.InteropServices.COMException)
                    {
                        return false;
                    }
                },
                TimeSpan.FromSeconds(10)),
            $"the search box never read \"{query}\" — keystrokes were dropped; it reads "
            + $"\"{search.Patterns.Value.Pattern.Value.ValueOrDefault}\"");

    /// <summary>The list's selection once it holds exactly one row and has
    /// held it for a quiet moment — the last keystroke's work included.</summary>
    private static AutomationElement WaitForOneSelectedRow(
        AutomationElement results, UIA3Automation automation)
    {
        AutomationElement? selected = null;
        string? previous = null;
        int stableReads = 0;
        bool settled = SpinWait.SpinUntil(
            () =>
            {
                try
                {
                    AutomationElement[] selection =
                        results.Patterns.Selection.Pattern.Selection.ValueOrDefault ?? [];
                    string? name = selection.Length == 1 ? selection[0].Name : null;
                    stableReads = name is not null && name == previous ? stableReads + 1 : 0;
                    previous = name;
                    selected = selection.Length == 1 ? selection[0] : null;
                    Thread.Sleep(100);
                    return stableReads >= 3;
                }
                catch (System.Runtime.InteropServices.COMException)
                {
                    return false;
                }
            },
            TimeSpan.FromSeconds(10));
        Assert.True(
            settled && selected is not null,
            "the results list never settled on exactly one selected row; rows: "
            + string.Join(" | ", results
                .FindAllDescendants(automation.ConditionFactory.ByControlType(ControlType.ListItem))
                .Select(item => item.Name)));
        return selected!;
    }

    /// <summary>The rows the list exposes, in order, once at least
    /// <paramref name="minimum"/> are there.</summary>
    private static string[] WaitForListedRows(
        AutomationElement results, UIA3Automation automation, int minimum)
    {
        string[] names = [];
        Assert.True(
            SpinWait.SpinUntil(
                () =>
                {
                    try
                    {
                        names = [.. results
                            .FindAllDescendants(automation.ConditionFactory.ByControlType(ControlType.ListItem))
                            .Select(item => item.Name)];
                        return names.Length >= minimum;
                    }
                    catch (System.Runtime.InteropServices.COMException)
                    {
                        return false;
                    }
                },
                TimeSpan.FromSeconds(10)),
            $"the results list exposed {names.Length} rows, expected at least {minimum}: "
            + string.Join(" | ", names));
        return names;
    }

    /// <summary>A realized row inside the list's own bounds — highlighted
    /// where a sighted user and a screen reader's focus rectangle look.</summary>
    private static void AssertRowOnScreen(AutomationElement row, AutomationElement results)
    {
        System.Drawing.Rectangle rowBounds = row.BoundingRectangle;
        System.Drawing.Rectangle listBounds = results.BoundingRectangle;
        Assert.False(rowBounds.IsEmpty, $"'{row.Name}' has no bounds — never scrolled into view");
        Assert.True(
            listBounds.Contains(rowBounds),
            $"'{row.Name}' at {rowBounds} lies outside the list at {listBounds}");
    }

    private static void WaitForPaletteClosed(Window window, UIA3Automation automation) =>
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

    /// <summary>A letter of the row's label that most labels share, so the
    /// row survives the query (P7) without leading it.</summary>
    private static string QueryLetterFor(string label)
    {
        foreach (char vowel in "eaoin")
        {
            if (label.Contains(vowel, StringComparison.OrdinalIgnoreCase))
            {
                return vowel.ToString();
            }
        }

        return char.ToLowerInvariant(label[0]).ToString();
    }

    /// <summary>The label behind a row's composed accessible Name (P6:
    /// "{label}" or "{label}, {spoken chord}"). Core ranks labels and hints,
    /// never the spoken chord, so a query letter taken from the chord would
    /// not keep the row.</summary>
    private static string LabelOf(string rowName) => Assert.Single(
        RegisteredCatalog(),
        row => string.Equals(row.AccessibleName, rowName, StringComparison.Ordinal)).Label;

    /// <summary>Core's match count for <paramref name="query"/> over the
    /// registered catalog, through the binding.</summary>
    private static int PaletteMatchCount(string query)
    {
        uniffi.slate_uniffi.Command[] commands =
        [
            .. RegisteredCatalog().Select(row => new uniffi.slate_uniffi.Command(
                row.Id,
                row.Label,
                row.Hint,
                row.Chord,
                Enum.Parse<uniffi.slate_uniffi.CommandSection>(row.Section))),
        ];
        return uniffi.slate_uniffi.SlateUniffiMethods.PaletteSections(commands, query, [], [])
            .Sum(section => section.Rows.Length);
    }

    /// <summary>The commands the app registers, as the chord table records
    /// them (chords.json travels with the binaries).</summary>
    private static CatalogRow[] RegisteredCatalog()
    {
        using JsonDocument table = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "fixtures", "chords.json")));
        return
        [
            .. table.RootElement.GetProperty("commands").EnumerateArray()
                .Where(row => row.GetProperty("registered").GetBoolean())
                .Select(row => new CatalogRow(
                    row.GetProperty("id").GetString()!,
                    row.GetProperty("label").GetString()!,
                    row.GetProperty("hint").GetString(),
                    row.GetProperty("windows").GetString(),
                    row.GetProperty("windowsSpoken").GetString(),
                    row.GetProperty("section").GetString()!)),
        ];
    }

    private sealed record CatalogRow(
        string Id, string Label, string? Hint, string? Chord, string? Spoken, string Section)
    {
        public string AccessibleName => Spoken is null ? Label : $"{Label}, {Spoken}";
    }
}
