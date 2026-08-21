// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// #1120: Control-derived pure-layout hosts are focusable by default
// (Control.Focusable is true), so a ScrollViewer, ItemsControl, or
// ContentControl that merely wraps content becomes an UNNAMED stop in
// the Tab cycle — a screen-reader user lands on "pane" with nothing to
// say about it. The axe gate cannot see this class: its name-required
// rules exclude the Pane control type, so the W4/W5 journeys stayed
// green while the stops shipped. This census is the source-level pin
// the gate is structurally blind to: every such host in the shell's
// XAML is either non-focusable (Focusable="False") or deliberately
// focusable AND named (AutomationProperties.Name — the scrollable embed
// preview, the citation abstract region).

using System.Xml;
using System.Xml.Linq;

namespace SlateWindows.Tests.Censuses;

[Trait("census", "focusable-layout-hosts")]
public sealed class FocusableLayoutHostCensus
{
    /// <summary>The Control-derived elements the shell uses as pure
    /// layout or items hosts. Other Controls (Expander, TabControl,
    /// ListBox, the editor) are interactive and name themselves.</summary>
    private static readonly string[] LayoutHosts =
    [
        "ScrollViewer",
        "ItemsControl",
        "ContentControl",
    ];

    /// <summary>The shell's VIEW XAML — the files that declare live
    /// elements. Discovered rather than hardcoded, and rooted by the
    /// repo walk rather than a hop count (codoki): the theme resource
    /// dictionaries and App.xaml are excluded because they declare
    /// styles and templates, not instances — a <c>Setter</c> for
    /// <c>Focusable</c> inside a control template is a different
    /// question this census does not answer.</summary>
    private static IEnumerable<string> ShellViewXaml() =>
        Directory.EnumerateFiles(
                SourceText.ShellSourceRoot(), "*.xaml", SearchOption.AllDirectories)
            .Where(path => !path.Contains(
                $"{Path.DirectorySeparatorChar}Themes{Path.DirectorySeparatorChar}",
                StringComparison.OrdinalIgnoreCase))
            .Where(path => !string.Equals(
                Path.GetFileName(path), "App.xaml", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.Ordinal);

    [Fact]
    public void EveryLayoutHostIsNonFocusableOrNamed()
    {
        var offenders = new List<string>();
        int hosts = 0;
        int files = 0;
        foreach (string path in ShellViewXaml())
        {
            files++;
            string file = Path.GetFileName(path);
            XDocument document = XDocument.Load(path, LoadOptions.SetLineInfo);
            foreach (XElement element in document.Descendants())
            {
                if (!LayoutHosts.Contains(element.Name.LocalName, StringComparer.Ordinal))
                {
                    continue;
                }

                hosts++;
                // XAML's boolean parse is case-insensitive, so "false"
                // must count exactly as "False" does (codoki) — a
                // case-only difference is not an accessibility defect,
                // and failing on it would train people to distrust the
                // census.
                bool nonFocusable = string.Equals(
                    Attribute(element, "Focusable"), "False", StringComparison.OrdinalIgnoreCase);
                bool named = Attribute(element, "AutomationProperties.Name") is not null;
                if (!nonFocusable && !named)
                {
                    int line = ((IXmlLineInfo)element).LineNumber;
                    offenders.Add($"{file}:{line} <{element.Name.LocalName}>");
                }
            }
        }

        Assert.True(files > 0, "the census found no shell view XAML — the discovery is broken");
        Assert.True(hosts > 0, "the census found no layout hosts — the scrape is broken");
        Assert.True(
            offenders.Count == 0,
            "unnamed focusable layout hosts (add Focusable=\"False\", or name a "
            + "deliberately focusable region with AutomationProperties.Name):\n  "
            + string.Join("\n  ", offenders));
    }

    /// <summary>XAML attached properties are plain attributes whose
    /// local name carries the dot (<c>AutomationProperties.Name</c>); a
    /// namespace-qualified form (<c>x:Name</c>) is a different thing
    /// and is deliberately NOT accepted as a name here — it is a code
    /// handle, not an accessible name.</summary>
    private static string? Attribute(XElement element, string localName) =>
        element.Attributes()
            .FirstOrDefault(attribute =>
                attribute.Name.Namespace == XNamespace.None
                && string.Equals(attribute.Name.LocalName, localName, StringComparison.Ordinal))
            ?.Value;
}
