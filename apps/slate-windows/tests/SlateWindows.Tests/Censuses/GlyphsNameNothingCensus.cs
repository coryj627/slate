// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Xml.Linq;

namespace SlateWindows.Tests.Censuses;

/// <summary>DS-9: a private-use character is a drawing, and a screen reader reads it as
/// garbage if it becomes a name. So a button that draws one has a name of its own, and
/// only a control's template or a presentation text block may hold one.</summary>
[Trait("census", "glyphs")]
public sealed partial class GlyphsNameNothingCensus
{
    /// <summary>A button built in code that draws a glyph. It has to name itself.</summary>
    [GeneratedRegex(@"\b(\w+)\s*=\s*new\s+\w*Button\s*\{[^}]*\bContent\s*=\s*""\\u[EeFf][0-9A-Fa-f]{3}""")]
    private static partial Regex GlyphButtonInCode();

    private static bool HasGlyph(string? text) => text?.Any(character => character is >= '\uE000' and <= '\uF8FF') == true;

    private static string[] Offenders(XDocument source)
    {
        var found = new List<string>();
        foreach (XElement element in source.Descendants())
        {
            string kind = element.Name.LocalName;
            string where = $"{kind} at line {((System.Xml.IXmlLineInfo)element).LineNumber}";
            string directText = string.Concat(element.Nodes().OfType<XText>().Select(text => text.Value));
            if (kind.EndsWith("Button", StringComparison.Ordinal))
            {
                bool glyph = HasGlyph((string?)element.Attribute("Content")) || HasGlyph(directText)
                    || ((string?)element.Attribute("Style"))?.Contains("SubtleIcon", StringComparison.Ordinal) == true;
                string? name = (string?)element.Attribute("AutomationProperties.Name");
                if (glyph && string.IsNullOrWhiteSpace(name))
                {
                    found.Add($"{where} draws a glyph and has no name");
                }
                else if (HasGlyph(name))
                {
                    found.Add($"{where} has a glyph in its name");
                }
            }
            else if (kind is "TextBlock" or "Run"
                && (HasGlyph((string?)element.Attribute("Text")) || HasGlyph(directText))
                && !element.Ancestors().Any(ancestor => ancestor.Name.LocalName == "ControlTemplate"))
            {
                found.Add($"{where} holds a glyph outside a template, so its peer is named by it");
            }
        }

        return found.ToArray();
    }

    private static XDocument Xaml(string body) => XDocument.Parse(
        "<ResourceDictionary xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" "
        + "xmlns:local=\"clr-namespace:SlateWindows\">" + body + "</ResourceDictionary>",
        LoadOptions.SetLineInfo);

    /// <summary>The census's teeth, because a clean pass on the source proves nothing.</summary>
    [Theory]
    [InlineData("<Button Content=\"&#xE711;\" />")]
    [InlineData("<Button Content=\"&#xE711;\" AutomationProperties.Name=\"  \" />")]
    [InlineData("<Button Content=\"&#xE711;\" AutomationProperties.Name=\"Close &#xE711;\" />")]
    [InlineData("<RepeatButton Content=\"&#xE70E;\" />")]
    [InlineData("<ToggleButton Content=\"&#xE70E;\" />")]
    [InlineData("<Button>&#xE711;</Button>")]
    [InlineData("<Button Style=\"{DynamicResource Slate.SubtleIconButtonStyle}\" Content=\"{Binding Glyph}\" />")]
    [InlineData("<TextBlock Text=\"&#xE711;\" />")]
    [InlineData("<TextBlock>&#xE711;</TextBlock>")]
    [InlineData("<DataTemplate><TextBlock Text=\"&#xE711;\" /></DataTemplate>")]
    [InlineData("<TextBlock><Run Text=\"&#xE711;\" /></TextBlock>")]
    public void APlantedFailureIsCaught(string body) =>
        Assert.NotEmpty(Offenders(Xaml(body)));

    /// <summary>And each way of doing it properly passes.</summary>
    [Theory]
    [InlineData("<Button Content=\"&#xE711;\" AutomationProperties.Name=\"Close\" />")]
    [InlineData("<Button Content=\"Close\" />")]
    [InlineData("<Button Style=\"{DynamicResource Slate.SubtleIconButtonStyle}\" Content=\"&#xE711;\" AutomationProperties.Name=\"{Binding Label}\" />")]
    [InlineData("<local:AutomationPresentationTextBlock Text=\"&#xE711;\" />")]
    [InlineData("<ControlTemplate><TextBlock Text=\"&#xE711;\" /></ControlTemplate>")]
    [InlineData("<TextBlock Text=\"×\" />")]
    public void AProperGlyphPasses(string body) =>
        Assert.Empty(Offenders(Xaml(body)));

    [Theory]
    [InlineData("var remove = new Button { Content = \"\\uE711\", Padding = new Thickness(6) };", false)]
    [InlineData("var remove = new Button { Content = \"\\uE711\" };\nAutomationProperties.SetName(remove, name);", true)]
    [InlineData("var remove = new RepeatButton { Margin = new Thickness(1), Content = \"\\uF0AD\" };", false)]
    public void TheCodeScanSeesAButtonBuiltThere(string source, bool named) =>
        Assert.Equal(named, !CodeOffenders(source).Any());

    private static IEnumerable<string> CodeOffenders(string source) => GlyphButtonInCode().Matches(source)
        .Where(match => !source.Contains($"SetName({match.Groups[1].Value},", StringComparison.Ordinal))
        .Select(match => match.Value);

    [Fact]
    public void TheSourcesGlyphsNameNothing()
    {
        string root = SourceText.ShellSourceRoot();
        string[] files = Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
            .Where(path => Path.GetRelativePath(root, path).Split(Path.DirectorySeparatorChar)[0] is not ("bin" or "obj"))
            .ToArray();
        var offenders = new List<string>();
        foreach (string file in files.Where(path => path.EndsWith(".xaml", StringComparison.Ordinal)))
        {
            offenders.AddRange(Offenders(XDocument.Load(file, LoadOptions.SetLineInfo)).Select(line => $"{Path.GetFileName(file)}: {line}"));
        }

        foreach (string file in files.Where(path => path.EndsWith(".cs", StringComparison.Ordinal)))
        {
            offenders.AddRange(CodeOffenders(File.ReadAllText(file)).Select(line => $"{Path.GetFileName(file)}: {line}"));
        }

        Assert.True(offenders.Count == 0, $"{offenders.Count} glyphs that name something:\n" + string.Join('\n', offenders));
    }

    /// <summary>A private-use character written out is invisible in an editor, and a change
    /// of encoding loses it. In C# it's an escape, which says which one it is.</summary>
    [Fact]
    public void AGlyphInCodeIsAnEscape()
    {
        string shell = SourceText.ShellSourceRoot();
        string[] roots = [shell, Path.GetFullPath(Path.Combine(shell, "..", "..", "tests"))];
        string[] literal = roots
            .SelectMany(root => Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .SelectMany(path => File.ReadLines(path)
                .Select((line, index) => (Line: line, Where: $"{Path.GetFileName(path)}:{index + 1}")))
            .Where(found => HasGlyph(found.Line))
            .Select(found => found.Where)
            .ToArray();

        Assert.True(literal.Length == 0, "A glyph written out: " + string.Join(", ", literal));
    }

    /// <summary>What's realized from styles and bindings, which the source can't show.</summary>
    [Fact]
    public void AShownShellsGlyphsNameNothing() => TextScaleTests.OnOwnThread(() =>
    {
        using var host = new ShownShell(("Lecture.md", "---\nkey: 1\n---\n# Lecture\n"));
        host.Workspace.OpenPath("Lecture.md");
        host.Settle();
        host.Settle();
        var offenders = new List<string>();
        foreach (DependencyObject element in ShownShell.Descendants(host.Shell))
        {
            switch (element)
            {
                case ButtonBase button when HasGlyph(button.Content as string):
                    string name = System.Windows.Automation.AutomationProperties.GetName(button);
                    if (string.IsNullOrWhiteSpace(name) || HasGlyph(name))
                    {
                        offenders.Add($"a {button.GetType().Name} named \"{name}\" draws a glyph");
                    }

                    break;
                case TextBlock text when HasGlyph(text.Text) && text is not AutomationPresentationTextBlock && text.TemplatedParent is null:
                    offenders.Add($"a plain TextBlock under {System.Windows.Media.VisualTreeHelper.GetParent(text)?.GetType().Name} holds a glyph");
                    break;
            }
        }

        Assert.True(offenders.Count == 0, $"{offenders.Count} glyphs that name something:\n" + string.Join('\n', offenders));
    });

    /// <summary>What the spec lists: no text glyph is left on a button. A tab's close button
    /// is DS-8's, which draws the design's tab.</summary>
    [Fact]
    public void NoTextGlyphIsLeftOnAButton()
    {
        string root = SourceText.ShellSourceRoot();
        string[] left = Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
            .Where(path => Path.GetRelativePath(root, path).Split(Path.DirectorySeparatorChar)[0] is not ("bin" or "obj"))
            .Where(path => path.EndsWith(".xaml", StringComparison.Ordinal) || path.EndsWith(".cs", StringComparison.Ordinal))
            .SelectMany(path => File.ReadLines(path).Select(line => (File: Path.GetFileName(path), Line: line)))
            .Where(found => found.Line.Contains("Content=\"", StringComparison.Ordinal) || found.Line.Contains("Content = \"", StringComparison.Ordinal))
            .Where(found => found.Line.IndexOfAny(['↻', '✕', '▲', '▼', '⟳', '✖']) >= 0 || found.Line.Contains("\\u2715", StringComparison.Ordinal))
            .Select(found => $"{found.File}: {found.Line.Trim()}")
            .ToArray();

        Assert.True(left.Length == 0, "Text glyphs on buttons:\n" + string.Join('\n', left));
    }

    /// <summary>The fields that search or filter, in XAML and in code. They all draw the glyph.</summary>
    [Theory]
    [InlineData("MainWindow.xaml", "SidebarFilterTextBox")]
    [InlineData("MainWindow.xaml", "BibliographySearchBox")]
    [InlineData("MainWindow.xaml", "QuickSwitcherSearchTextBox")]
    [InlineData("MainWindow.xaml", "SearchOverlaySearchTextBox")]
    [InlineData("MainWindow.xaml", "CommandPaletteSearchTextBox")]
    public void ASearchFieldInXamlDrawsTheGlyph(string file, string name)
    {
        XElement field = XDocument.Load(Path.Combine(SourceText.ShellSourceRoot(), file)).Descendants()
            .Single(element => element.Attributes().Any(attribute => attribute.Name.LocalName == "Name" && attribute.Value == name));
        Assert.Contains(field.Attributes(), attribute => attribute.Name.LocalName == "SearchGlyph.IsEnabled" && attribute.Value == "True");
    }

    [Theory]
    [InlineData(@"Canvas\CanvasSurfaceView.cs", "_filterField")]
    [InlineData(@"Graph\GraphSurfaceView.cs", "_filterField")]
    [InlineData(@"Bases\BaseSurfaceView.cs", "_quickFilter")]
    public void ASearchFieldInCodeDrawsTheGlyph(string file, string field) =>
        Assert.Contains($"SearchGlyph.SetIsEnabled({field}, true)", File.ReadAllText(Path.Combine(SourceText.ShellSourceRoot(), file)), StringComparison.Ordinal);
}
