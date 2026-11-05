// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// #1758: on main, the launched app in the dark theme drew light menu text on
// WPF's fixed light bar, close to unreadable, and every property a hosted test
// could read looked right, because the brushes were each valid and only the
// pair was wrong. So this reads the pixels of the real window, in whichever
// theme Windows has chosen: the surface's most common colour is its background,
// and its strongest ink has to stand off that.

using System.Drawing;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using Microsoft.Win32;

namespace SlateWindows.AccessibilityTests;

public sealed partial class ShellAccessibilityTests
{
    [Fact]
    public void ThemedShell_TheMenuBarAndTheOpenTabAreReadable() =>
        RunFindInNoteJourney("theme-readability", (process, window, automation, editor) =>
        {
            AutomationElement menu = window.FindFirstDescendant(cf => cf.ByName("Application commands"))
                ?? throw new Xunit.Sdk.XunitException("no menu bar in the window.");
            AutomationElement tabs = WaitForElement(window, "WorkspaceTabs", TimeSpan.FromSeconds(10));
            AutomationElement tab = tabs.FindFirstChild(cf => cf.ByControlType(ControlType.TabItem))
                ?? throw new Xunit.Sdk.XunitException("no open tab in the window.");
            Thread.Sleep(500);

            AssertReadable(menu, "the menu bar");
            AssertReadable(tab, "the open tab");

            AssertAxeClean(process, "theme-readability");
        });

    /// <summary>DS-6: an effect turns ClearType off for what's inside it, and each overlay
    /// had one on itself. With the shadow on a sibling, the palette's text has colour
    /// fringes, the method of spike 4. A stem's middle is solid at a high display scale, so
    /// plain window text is 34 to 53% fringes at 200%, the palette 38%, and with the effect
    /// 0%: a quarter tells them apart. A display with ClearType off can't show any, so the
    /// fact says so, and a missing fringe isn't mistaken for a pass.</summary>
    [Fact]
    public void ThemedShell_APalettesTextKeepsItsClearType() =>
        RunFindInNoteJourney("palette-cleartype", (process, window, automation, editor) =>
        {
            if (!SystemUsesClearType())
            {
                Assert.Skip("this desktop has ClearType off, so a palette can't show colour fringes on it");
            }

            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.SHIFT, VirtualKeyShort.KEY_P);
            AutomationElement palette = WaitForElement(window, "CommandPalette", TimeSpan.FromSeconds(10));
            Thread.Sleep(800);

            int ink;
            int fringed;
            using (FlaUI.Core.Capturing.CaptureImage capture = CapturePhysicalPixels(palette))
            {
                (ink, fringed) = FringedInk(capture.Bitmap);
            }

            Assert.True(ink > 200, $"the palette has only {ink} pixels of text to read.");
            Assert.True(fringed * 4 >= ink, $"{fringed} of the palette's {ink} text pixels have colour fringes.");
            AssertAxeClean(process, "palette-cleartype");
        });

    /// <summary>The lower part of a surface, below its field and first row's highlight:
    /// what stands off its most common colour, and how much of that isn't grey.</summary>
    private static (int Ink, int Fringed) FringedInk(Bitmap bitmap)
    {
        var counts = new Dictionary<int, int>();
        int top = bitmap.Height * 2 / 5;
        for (var y = top; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                int color = bitmap.GetPixel(x, y).ToArgb();
                counts[color] = counts.GetValueOrDefault(color) + 1;
            }
        }

        Color surface = Color.FromArgb(counts.MaxBy(count => count.Value).Key);
        int ink = 0;
        int fringed = 0;
        for (var y = top; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                Color pixel = bitmap.GetPixel(x, y);
                if (Math.Abs(pixel.G - surface.G) <= 48)
                {
                    continue;
                }

                ink++;
                int most = Math.Max(Math.Max(pixel.R, pixel.G), pixel.B);
                int least = Math.Min(Math.Min(pixel.R, pixel.G), pixel.B);
                if (most - least > 24)
                {
                    fringed++;
                }
            }
        }

        return (ink, fringed);
    }

    private static bool SystemUsesClearType()
    {
        const uint GetFontSmoothing = 0x004A;
        const uint GetFontSmoothingType = 0x200A;
        const uint ClearType = 2;
        uint on = 0;
        uint kind = 0;
        return SystemParametersInfo(GetFontSmoothing, 0, ref on, 0) && on != 0
            && SystemParametersInfo(GetFontSmoothingType, 0, ref kind, 0) && kind == ClearType;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool SystemParametersInfo(uint action, uint parameter, ref uint value, uint flags);

    /// <summary>DS-5: launched at the seam's largest size, the chrome's minimums give way
    /// to its text, and the shell stays clean. Spike 5 measured a status bar of 39.9 and a
    /// tab strip of 45.9 at this size.</summary>
    [Fact]
    public void ThemedShell_AtTheLargestTextTheShellGrows()
    {
        (double Status, double Tab)? normal = ChromeHeights("largest-text-base", "1");
        (double Status, double Tab)? larger = ChromeHeights("largest-text", "2.25");

        // With no interactive desktop the journey stops at its startup smoke.
        if (normal is not { } small || larger is not { } large)
        {
            return;
        }

        Assert.True(small.Status > 0 && small.Tab > 0, $"the launch drew a {small.Status} status bar and a {small.Tab} tab.");
        Assert.True(large.Status > small.Status, $"the status bar stayed {small.Status} at 225%, and reads {large.Status}.");
        Assert.True(large.Tab > small.Tab, $"the open tab stayed {small.Tab} at 225%, and reads {large.Tab}.");
    }

    private static (double Status, double Tab)? ChromeHeights(string name, string textScale)
    {
        (double, double)? heights = null;
        RunFindInNoteJourney(
            name,
            (process, window, automation, editor) =>
            {
                AutomationElement tabs = WaitForElement(window, "WorkspaceTabs", TimeSpan.FromSeconds(10));
                AutomationElement tab = tabs.FindFirstChild(cf => cf.ByControlType(ControlType.TabItem))
                    ?? throw new Xunit.Sdk.XunitException("no open tab in the window.");
                AutomationElement status = window.FindFirstDescendant(cf => cf.ByControlType(ControlType.StatusBar))
                    ?? throw new Xunit.Sdk.XunitException("no status bar in the window.");
                heights = (status.BoundingRectangle.Height, tab.BoundingRectangle.Height);
                AssertAxeClean(process, name);
            },
            textScale);
        return heights;
    }

    /// <summary>#1763: the editor's selection in the launched app. It's the solid
    /// pair, drawn under the text, so the words stay readable on it.</summary>
    [Fact]
    public void ThemedShell_TheEditorSelectionStandsOutAndKeepsItsText() =>
        RunFindInNoteJourney("editor-selection", (process, window, automation, editor) =>
        {
            editor.Focus();
            Color highlight = Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                "AppsUseLightTheme",
                1) is 0
                ? Color.FromArgb(0x6B, 0x4D, 0x00)
                : Color.FromArgb(0xF9, 0xE7, 0xB8);
            Thread.Sleep(300);
            int before = Measure(editor, highlight).Highlight;
            Assert.True(before < 50, $"premise: {before} pixels are the highlight with nothing selected.");

            using (Keyboard.Pressing(VirtualKeyShort.CONTROL))
            {
                Keyboard.Type(VirtualKeyShort.KEY_A);
            }

            Thread.Sleep(400);
            (int selected, double ink) = Measure(editor, highlight);
            Assert.True(
                selected > 500,
                $"only {selected} pixels are the highlight {highlight}. Most common colours {MostCommonColors(editor)}.");
            Assert.True(ink >= 4.5, $"the selected text's strongest ink measures {ink:F2}:1 on the highlight.");

            AssertAxeClean(process, "editor-selection");
        });

    /// <summary>#1767: on main, the palette's list was classic white under the
    /// dark theme's pale text. Pixels again: every brush was valid.</summary>
    [Fact]
    public void ThemedShell_TheCommandPaletteListIsReadable() =>
        RunFindInNoteJourney("palette-readability", (process, window, automation, editor) =>
        {
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.SHIFT, VirtualKeyShort.KEY_P);
            AutomationElement results = WaitForElement(window, "CommandPaletteResults", TimeSpan.FromSeconds(10));
            Thread.Sleep(500);

            AssertReadable(results, "the command palette's list");

            AssertAxeClean(process, "palette-readability");
        });

    /// <summary>#1772: Fluent's focus ring is drawn outside the row, and the list
    /// clips it: the rail showed only its top and bottom lines.</summary>
    [Fact]
    public void ThemedShell_AFocusedPanelRowShowsItsWholeRing() =>
        RunFindInNoteJourney("row-focus", (process, window, automation, editor) =>
        {
            AutomationElement rail = WaitForElement(window, "RightPaneLeaves", TimeSpan.FromSeconds(10));
            AutomationElement[] rows = rail.FindAllChildren(cf => cf.ByControlType(ControlType.ListItem));
            rows[1].Focus();
            PressKey(VirtualKeyShort.DOWN);
            Thread.Sleep(500);
            AutomationElement row = rail
                .FindAllChildren(cf => cf.ByControlType(ControlType.ListItem))
                .Single(item => item.Properties.HasKeyboardFocus.ValueOrDefault);

            using FlaUI.Core.Capturing.CaptureImage capture = CapturePhysicalPixels(row);
            Bitmap bitmap = capture.Bitmap;
            Color surface = MostCommonColor(row);
            int RingCount(IEnumerable<(int X, int Y)> line) =>
                line.Count(point => Contrast(bitmap.GetPixel(point.X, point.Y), surface) >= 3);
            int Column(int x) => RingCount(Enumerable.Range(0, bitmap.Height).Select(y => (x, y)));
            int Row(int y) => RingCount(Enumerable.Range(0, bitmap.Width).Select(x => (x, y)));
            int left = Enumerable.Range(0, 6).Max(Column);
            int right = Enumerable.Range(bitmap.Width - 6, 6).Max(Column);
            int top = Enumerable.Range(0, 6).Max(Row);
            int bottom = Enumerable.Range(bitmap.Height - 6, 6).Max(Row);
            Assert.True(
                left > bitmap.Height / 2 && right > bitmap.Height / 2 && top > bitmap.Width / 2 && bottom > bitmap.Width / 2,
                $"the ring in a {bitmap.Width}x{bitmap.Height} row measures left {left}, right {right}, top {top}, bottom {bottom}.");

            AssertAxeClean(process, "row-focus");
        });

    /// <summary>#1772: the name sat above the middle of its checkbox, because an
    /// empty metadata line still took its height under it.</summary>
    [Fact]
    public void ThemedShell_TreeRowLabelsLineUpWithTheirCheckboxes() =>
        RunFindInNoteJourney("tree-row-alignment", (process, window, automation, editor) =>
        {
            AutomationElement tree = WaitForElement(window, "FilesTree", TimeSpan.FromSeconds(10));
            AutomationElement row = tree.FindFirstChild(cf => cf.ByControlType(ControlType.TreeItem))
                ?? throw new Xunit.Sdk.XunitException("no row in the Files tree.");
            AutomationElement box = row.FindFirstChild(cf => cf.ByControlType(ControlType.CheckBox))
                ?? throw new Xunit.Sdk.XunitException("no checkbox on the row.");
            // A file has a metadata line under its name; the two are centred together.
            AutomationElement[] texts = row.FindAllChildren(cf => cf.ByControlType(ControlType.Text));
            Assert.NotEmpty(texts);
            double top = texts.Min(text => text.BoundingRectangle.Top);
            double bottom = texts.Max(text => text.BoundingRectangle.Bottom);
            double middle = box.BoundingRectangle.Top + (box.BoundingRectangle.Height / 2.0);
            double offset = Math.Abs(middle - ((top + bottom) / 2.0));
            Assert.True(
                offset <= box.BoundingRectangle.Height / 8.0,
                $"the row's text is {offset:F1}px from the middle of its checkbox: {top} to {bottom}, and {box.BoundingRectangle}.");

            AssertAxeClean(process, "tree-row-alignment");
        });

    /// <summary>#1772: "Graph inspector" ran to the window's edge, and the rail
    /// scrolled sideways.</summary>
    [Fact]
    public void ThemedShell_TheRightPaneRailFitsItsLabels() =>
        RunFindInNoteJourney("rail-fit", (process, window, automation, editor) =>
        {
            AutomationElement rail = WaitForElement(window, "RightPaneLeaves", TimeSpan.FromSeconds(10));
            Assert.True(rail.Patterns.Scroll.IsSupported, "premise: the rail has no scroll pattern.");
            Assert.False(rail.Patterns.Scroll.Pattern.HorizontallyScrollable.Value, "the rail scrolls sideways.");

            AssertAxeClean(process, "rail-fit");
        });

    /// <summary>#1769: an open overlay disables the shell behind it, and Fluent's
    /// grid animates its Disabled state to a fixed white under the theme's pale
    /// text. A hosted window doesn't show it, so this reads the launched app.</summary>
    [Fact]
    public void ThemedShell_ADisabledGridStaysOnItsSurface() =>
        RunFindInNoteJourney("disabled-grid", (process, window, automation, editor) =>
        {
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.SHIFT, VirtualKeyShort.KEY_P);
            AutomationElement search = WaitForElement(window, "CommandPaletteSearch", TimeSpan.FromSeconds(10));
            search.Patterns.Value.Pattern.SetValue("Open Graph");
            Thread.Sleep(500);
            PressKey(VirtualKeyShort.ENTER);
            AutomationElement grid = WaitForElement(window, "GraphTableGrid", TimeSpan.FromSeconds(20));
            Thread.Sleep(500);
            Color enabled = MostCommonColor(grid);

            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.SHIFT, VirtualKeyShort.KEY_F);
            WaitForElement(window, "SearchOverlay", TimeSpan.FromSeconds(10));
            Thread.Sleep(500);
            Assert.False(grid.IsEnabled, "premise: the overlay didn't disable the grid.");

            // DS-6: the overlay's scrim is over it now, a flat 30% black in both themes.
            Color disabled = MostCommonColor(grid);
            Color expected = UnderTheScrim(enabled);
            Assert.True(
                Math.Abs(expected.R - disabled.R) <= 2 && Math.Abs(expected.G - disabled.G) <= 2 && Math.Abs(expected.B - disabled.B) <= 2,
                $"the grid's surface was {enabled}, {expected} under the scrim, and is {disabled} while disabled. Most common colours {MostCommonColors(grid)}.");

            AssertAxeClean(process, "disabled-grid");
        });

    private static Color UnderTheScrim(Color surface)
    {
        const double Through = 1 - (0x4D / 255.0);
        return Color.FromArgb((int)Math.Round(surface.R * Through), (int)Math.Round(surface.G * Through), (int)Math.Round(surface.B * Through));
    }

    /// <summary>#1774: the launched app's empty field has readable ink inside its
    /// border: a blank box has none at all.</summary>
    [Fact]
    public void ThemedShell_AnEmptyFieldShowsItsPurpose() =>
        RunFindInNoteJourney("field-prompt", (process, window, automation, editor) =>
        {
            AutomationElement filter = WaitForElement(window, "SidebarFilter", TimeSpan.FromSeconds(10));
            Assert.True(string.IsNullOrEmpty(filter.Patterns.Value.Pattern.Value.Value), "premise: the filter has text.");
            Thread.Sleep(300);

            using FlaUI.Core.Capturing.CaptureImage capture = CapturePhysicalPixels(filter);
            Bitmap bitmap = capture.Bitmap;
            Color fill = MostCommonColor(filter);
            int ink = 0;
            for (int y = bitmap.Height / 4; y < bitmap.Height * 3 / 4; y++)
            {
                for (int x = bitmap.Width / 16; x < bitmap.Width / 2; x++)
                {
                    ink += Contrast(bitmap.GetPixel(x, y), fill) >= 4.5 ? 1 : 0;
                }
            }

            Assert.True(ink > 20, $"only {ink} pixels of readable ink inside the empty filter, on {fill}.");

            AssertAxeClean(process, "field-prompt");
        });

    /// <summary>#1781: the selected dropdown row was WPF's classic blue, the last
    /// blue in the app. Slate's selection is a warm surface in both themes.</summary>
    [Fact]
    public void ThemedShell_TheSelectedDropdownRowIsNotBlue() =>
        RunFindInNoteJourney("dropdown-row", (process, window, automation, editor) =>
        {
            AutomationElement combo = WaitForElement(window, "SidebarSortOrder", TimeSpan.FromSeconds(10));
            combo.Patterns.ExpandCollapse.Pattern.Expand();
            Thread.Sleep(500);
            AutomationElement row = combo.FindAllDescendants(cf => cf.ByControlType(ControlType.ListItem))
                .First(item => item.Patterns.SelectionItem.Pattern.IsSelected.Value);

            Color fill = MostCommonColor(row);
            Assert.True(fill.B <= fill.R, $"the selected row is {fill}.");

            combo.Patterns.ExpandCollapse.Pattern.Collapse();
            Thread.Sleep(300);
            AssertAxeClean(process, "dropdown-row");
        });

    private static void AssertReadable(AutomationElement surface, string name)
    {
        Color background = MostCommonColor(surface);
        (_, double ink) = Measure(surface, background);
        Assert.True(
            ink >= 4.5,
            $"{name}'s strongest ink measures {ink:F2}:1 on its background {background}. "
            + $"Most common colours {MostCommonColors(surface)}.");
    }

    private static Color MostCommonColor(AutomationElement surface)
    {
        using FlaUI.Core.Capturing.CaptureImage capture = CapturePhysicalPixels(surface);
        Bitmap bitmap = capture.Bitmap;
        var counts = new Dictionary<int, int>();
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                int color = bitmap.GetPixel(x, y).ToArgb();
                counts[color] = counts.GetValueOrDefault(color) + 1;
            }
        }

        return Color.FromArgb(counts.MaxBy(pair => pair.Value).Key);
    }
}
