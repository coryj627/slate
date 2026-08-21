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

    private static string SourceDirectory
    {
        get
        {
            // tests/.../bin/<cfg>/net10.0-windows/ -> apps/slate-windows is
            // six hops above (the trailing separator costs one — the
            // MutationHarnessCensus walk, two short of the repo root);
            // the shell XAML lives in src/SlateWindows.
            string dir = AppContext.BaseDirectory;
            for (int i = 0; i < 6; i++)
            {
                dir = Path.GetDirectoryName(dir)!;
            }
            return Path.Combine(dir, "src", "SlateWindows");
        }
    }

    private static readonly string[] ShellXaml =
    [
        "MainWindow.xaml",
        "WorkspaceTemplates.xaml",
    ];

    [Fact]
    public void EveryLayoutHostIsNonFocusableOrNamed()
    {
        var offenders = new List<string>();
        int hosts = 0;
        foreach (string file in ShellXaml)
        {
            string path = Path.Combine(SourceDirectory, file);
            Assert.True(File.Exists(path), $"shell XAML missing at {path}");
            XDocument document = XDocument.Load(path, LoadOptions.SetLineInfo);
            foreach (XElement element in document.Descendants())
            {
                if (!LayoutHosts.Contains(element.Name.LocalName, StringComparer.Ordinal))
                {
                    continue;
                }

                hosts++;
                bool nonFocusable = string.Equals(
                    Attribute(element, "Focusable"), "False", StringComparison.Ordinal);
                bool named = Attribute(element, "AutomationProperties.Name") is not null;
                if (!nonFocusable && !named)
                {
                    int line = ((IXmlLineInfo)element).LineNumber;
                    offenders.Add($"{file}:{line} <{element.Name.LocalName}>");
                }
            }
        }

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
