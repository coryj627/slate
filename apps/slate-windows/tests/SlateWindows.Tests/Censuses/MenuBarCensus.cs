// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// W7-5 (#1239): two shell-level menu-bar contracts the first human NVDA
// field pass found broken, pinned at the source so they cannot come
// back.
//
// 1. Every top-level menu carries a UNIQUE mnemonic and an AutomationId.
//    `_File` and `_Files` both advertised Alt+F; WPF resolves a shared
//    mnemonic by moving focus to the first match and waiting for a second
//    press, so Alt+F never opened a menu while Alt+W, Alt+B, Alt+E and
//    Alt+G all did. `_Files` was also the one top-level menu without an
//    AutomationId, invisible to every by-id lookup the FlaUI suite uses.
//
// 2. The menu bar is not in the Tab order. WPF's Menu defaults
//    KeyboardNavigation.TabNavigation to Cycle, so a Tab that enters the
//    bar never leaves it: Tab from the right-pane rail landed on File,
//    and Tab then cycled File → … → Files → File without ever leaving.
//    TabNavigation=None keeps Tab out of the bar entirely (the Win32
//    shape); the menu bar is reached by Alt, Alt+letter and F6 (W7-6,
//    #1240), never by Tab.
//
// 3. W7-7 PR 4 (#1247, contract R-5): nor by an arrow key. WPF's Menu
//    defaults DirectionalNavigation to Cycle, which makes the bar a
//    directional GROUP that every arrow search in the window can pick —
//    from the empty editor stop Down and Up reached Canvas, Left Graph
//    and Right File (NVDA pass F4). None keeps the arrows out. The bar's
//    own Left/Right between headers is directional navigation inside the
//    header's nearest group, which that same Cycle used to be: with the
//    Menu None the ends stopped wrapping (measured: Left on File and Right
//    on Graph went nowhere), so the items panel carries the Cycle now.

using System.Xml.Linq;

namespace SlateWindows.Tests.Censuses;

[Trait("census", "menu-bar")]
public sealed class MenuBarCensus
{
    private static XElement MainMenu()
    {
        XDocument window = XDocument.Load(
            Path.Combine(SourceText.ShellSourceRoot(), "MainWindow.xaml"));
        return window.Descendants()
            .Single(element => element.Name.LocalName == "Menu"
                && (string?)element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == "MainMenu");
    }

    private static IReadOnlyList<XElement> TopLevelItems() =>
        MainMenu().Elements()
            .Where(element => element.Name.LocalName == "MenuItem")
            .ToList();

    [Fact]
    public void EveryTopLevelMenuHasAUniqueMnemonicAndAnAutomationId()
    {
        IReadOnlyList<XElement> items = TopLevelItems();
        Assert.NotEmpty(items);

        var byMnemonic = new Dictionary<char, List<string>>();
        var offenders = new List<string>();
        foreach (XElement item in items)
        {
            string header = (string?)item.Attribute("Header") ?? "";
            int underscore = header.IndexOf('_', StringComparison.Ordinal);
            if (underscore < 0 || underscore + 1 >= header.Length)
            {
                offenders.Add($"'{header}': no mnemonic (an `_` before a letter)");
                continue;
            }

            char mnemonic = char.ToUpperInvariant(header[underscore + 1]);
            if (!byMnemonic.TryGetValue(mnemonic, out List<string>? headers))
            {
                headers = [];
                byMnemonic[mnemonic] = headers;
            }

            headers.Add(header);

            if (string.IsNullOrWhiteSpace((string?)item.Attribute("AutomationProperties.AutomationId")))
            {
                offenders.Add($"'{header}': no AutomationProperties.AutomationId");
            }
        }

        foreach ((char mnemonic, List<string> headers) in byMnemonic.Where(pair => pair.Value.Count > 1))
        {
            offenders.Add($"Alt+{mnemonic} is claimed by {string.Join(" and ", headers)}");
        }

        Assert.True(
            offenders.Count == 0,
            "Top-level menu contract violations:\n  " + string.Join("\n  ", offenders));
    }

    [Fact]
    public void NeitherTabNorAnArrowEntersTheMenuBar()
    {
        XElement menu = MainMenu();
        Assert.Equal("False", (string?)menu.Attribute("Focusable"));
        Assert.Equal("None", (string?)menu.Attribute("KeyboardNavigation.TabNavigation"));
        Assert.Equal("None", (string?)menu.Attribute("KeyboardNavigation.DirectionalNavigation"));
    }

    /// <summary>W7-7 PR 4 (#1247): the headers' own panel cycles, so Left
    /// on File still reaches the last menu and Right on the last reaches
    /// File with the Menu itself closed to arrows.</summary>
    [Fact]
    public void TheMenuBarsOwnArrowsStillWrap()
    {
        XElement panels = Assert.Single(MainMenu().Elements(), element => element.Name.LocalName == "Menu.ItemsPanel");
        XElement panel = Assert.Single(panels.Descendants(), element => element.Name.LocalName == "WrapPanel");
        Assert.Equal("Cycle", (string?)panel.Attribute("KeyboardNavigation.DirectionalNavigation"));
    }

    /// <summary>W7-7 PR 4 (#1247, R-5): the editor region keeps its arrows
    /// — none walks from inside the content pane into the next region or
    /// the menu bar.</summary>
    [Fact]
    public void TheEditorRegionContainsItsArrows()
    {
        XDocument window = XDocument.Load(
            Path.Combine(SourceText.ShellSourceRoot(), "MainWindow.xaml"));
        XElement contentPane = window.Descendants().Single(
            element => (string?)element.Attribute("AutomationProperties.AutomationId") == "ContentPane");
        Assert.Equal("Contained", (string?)contentPane.Attribute("KeyboardNavigation.DirectionalNavigation"));
    }

    /// <summary>W7-6 (#1240): F6 and Shift+F6 are delivered by window
    /// KeyBindings bound to the two region verbs, and the menu bar hands
    /// F6 on while in menu mode (WPF's menu mode would otherwise keep it).</summary>
    [Fact]
    public void F6AndShiftF6AreBoundToTheRegionVerbs()
    {
        XDocument window = XDocument.Load(
            Path.Combine(SourceText.ShellSourceRoot(), "MainWindow.xaml"));
        var bindings = window.Descendants()
            .Where(element => element.Name.LocalName == "KeyBinding" && (string?)element.Attribute("Key") == "F6")
            .ToDictionary(
                element => (string?)element.Attribute("Modifiers") ?? "",
                element => (string?)element.Attribute("Command"));
        Assert.Equal("{Binding Workspace.FocusNextPaneCommand}", bindings[""]);
        Assert.Equal("{Binding Workspace.FocusPreviousPaneCommand}", bindings["Shift"]);
        Assert.Equal("MainMenu_PreviewKeyDown", (string?)MainMenu().Attribute("PreviewKeyDown"));
    }

    /// <summary>W7-6 final review (#1240): the two stops F6 added — the
    /// content pane's border and the status bar — are focusable so the
    /// ring can land on them, and OUT of the Tab order so Tab's walk
    /// through the shell is exactly what it was before F6 existed.</summary>
    [Fact]
    public void TheF6OnlyLandingsAreNotTabStops()
    {
        XDocument window = XDocument.Load(
            Path.Combine(SourceText.ShellSourceRoot(), "MainWindow.xaml"));
        XName name = XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml");

        XElement contentPane = window.Descendants().Single(
            element => (string?)element.Attribute("AutomationProperties.AutomationId") == "ContentPane");
        XElement statusBar = window.Descendants().Single(
            element => (string?)element.Attribute(name) == "ShellStatusBar");

        var offenders = new List<string>();
        foreach ((string label, XElement element) in new[]
                 {
                     ("ContentPane", contentPane),
                     ("ShellStatusBar", statusBar),
                 })
        {
            if ((string?)element.Attribute("Focusable") != "True")
            {
                offenders.Add($"{label}: F6 needs Focusable=\"True\"");
            }

            if ((string?)element.Attribute("KeyboardNavigation.IsTabStop") != "False")
            {
                offenders.Add($"{label}: Tab must skip it — KeyboardNavigation.IsTabStop=\"False\"");
            }
        }

        Assert.True(
            offenders.Count == 0,
            "F6-only landing violations:\n  " + string.Join("\n  ", offenders));
    }
}
