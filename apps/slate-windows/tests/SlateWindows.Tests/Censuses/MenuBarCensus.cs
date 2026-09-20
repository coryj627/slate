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
    public void TheMenuBarIsOutOfTheTabOrder()
    {
        XElement menu = MainMenu();
        Assert.Equal("False", (string?)menu.Attribute("Focusable"));
        Assert.Equal("None", (string?)menu.Attribute("KeyboardNavigation.TabNavigation"));
    }
}
