// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// W7-7 PR 4b (#1247, R-5; the completeness sweep's G19, flag 4 of PR 4): a
// standalone scroll viewer is layout, not a stop. WPF makes one focusable by
// default, and the shell's were: F6 into the Sync leaf landed on an unnamed
// scroll viewer (the 2026-09-22 NVDA pass: "the first stop is an unnamed
// pane"), a docked dashboard's first stop was one, and WPF handed a rebuilt
// dashboard cell's keys up to one. Every scroll viewer the shell declares — in
// its XAML, or built in its code with `new ScrollViewer` — says
// Focusable=False, or is a stop on purpose, named and written here.

using System.Xml;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SlateWindows.Tests.Censuses;

[Trait("census", "scroll-viewer-stops")]
public sealed class ScrollViewerStopCensus
{
    /// <summary>The scroll viewers that are stops on purpose, by the
    /// identity of the element that holds them, and why.</summary>
    private static readonly Dictionary<string, string> Stops = new(StringComparer.Ordinal)
    {
        ["CitationDetailsAbstract"] = "the citation's abstract: a named stop the keyboard scrolls, or a long abstract has no keyboard route to its end (W4-5)",
    };

    [Fact]
    public void EveryScrollViewerTheXamlDeclaresIsNoStop()
    {
        var offenders = new List<string>();
        var stops = new List<string>();
        int judged = 0;
        foreach (string path in Directory.EnumerateFiles(SourceText.ShellSourceRoot(), "*.xaml", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)))
        {
            XDocument document = XDocument.Load(path, LoadOptions.SetLineInfo);
            foreach (XElement viewer in document.Descendants().Where(element => element.Name.LocalName == "ScrollViewer"))
            {
                judged++;
                if ((string?)viewer.Attribute("Focusable") == "False")
                {
                    continue;
                }

                string? holder = viewer.Ancestors()
                    .Select(ancestor => (string?)ancestor.Attribute("AutomationProperties.AutomationId"))
                    .FirstOrDefault(id => id is not null);
                if (holder is not null && Stops.ContainsKey(holder))
                {
                    stops.Add(holder);
                    continue;
                }

                offenders.Add($"{Path.GetFileName(path)}:{((IXmlLineInfo)viewer).LineNumber} <ScrollViewer> under '{holder}' is a stop");
            }
        }

        Assert.True(judged >= 8, $"only {judged} XAML scroll viewers were judged; the scrape is broken.");
        Assert.True(offenders.Count == 0, "Scroll viewers that take the keys (G19):\n  " + string.Join("\n  ", offenders));
        Assert.All(Stops.Keys, id => Assert.Contains(id, stops));
    }

    [Fact]
    public void EveryScrollViewerTheCodeBuildsIsNoStop()
    {
        var offenders = new List<string>();
        int judged = 0;
        foreach ((string relative, CSharpSource source) in ShellCompilation.Sources)
        {
            SemanticModel model = ShellCompilation.ModelFor(source);
            foreach (BaseObjectCreationExpressionSyntax creation in source.Root.DescendantNodes().OfType<BaseObjectCreationExpressionSyntax>())
            {
                if (model.GetTypeInfo(creation).Type?.ToDisplayString() != "System.Windows.Controls.ScrollViewer")
                {
                    continue;
                }

                judged++;
                bool declared = creation.Initializer?.Expressions
                    .OfType<AssignmentExpressionSyntax>()
                    .Any(assignment => assignment.Left.ToString() == "Focusable" && assignment.Right.ToString() == "false") == true;
                if (!declared)
                {
                    offenders.Add($"{relative}:{creation.GetLocation().GetLineSpan().StartLinePosition.Line + 1} new ScrollViewer without Focusable = false");
                }
            }
        }

        Assert.True(judged >= 4, $"only {judged} code-built scroll viewers were judged; the scrape is broken.");
        Assert.True(offenders.Count == 0, "Scroll viewers that take the keys (G19):\n  " + string.Join("\n  ", offenders));
    }
}
