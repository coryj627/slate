// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SlateWindows.Tests.Censuses;

/// <summary>N-1/N-2: enumerate authored files; a new help surface cannot
/// silently introduce a second spelling of a spoken chord.</summary>
[Trait("census", "chord-speech")]
public sealed class ChordSpeechAuditCensus
{
    private static readonly Regex SpokenChord = new(
        @"\b(?:(?:Control|Alt|Shift|Windows)\s+)+(?:[A-Z0-9]|F(?:[1-9]|1[0-9]|2[0-4])|Enter|Return|Escape|Tab|Space|Up|Down|Left|Right|Home|End|Page Up|Page Down|Delete|Backspace|Plus|Minus|Comma|Period|Slash|Backslash|Semicolon|Quote|Equals|Backtick)\b|\b(?:(?:Left|Right) Bracket|(?:Left|Right|Up|Down) Arrow)\b",
        RegexOptions.CultureInvariant);

    internal static readonly string[] BareKeys =
        ["Enter", "Return", "Escape", "Tab", "Space", "Up", "Down", "Home", "End", "Page Up", "Page Down", "Delete", "Backspace", "F2"];

    [Fact]
    public void EveryAuthoredSpokenChordComesFromTheTable()
    {
        var failures = new List<string>();
        foreach ((string relative, CSharpSource source) in ShellCompilation.Sources)
        {
            failures.AddRange(CSharpViolations(source.Root, ShellCompilation.ModelFor(source), relative));
        }
        foreach (string file in Directory.EnumerateFiles(SourceText.ShellSourceRoot(), "*.xaml", SearchOption.AllDirectories)
            .Where(path => !Path.GetRelativePath(SourceText.ShellSourceRoot(), path).Split(Path.DirectorySeparatorChar)
                .Any(part => part is "obj" or "bin")))
        {
            failures.AddRange(XamlViolations(File.ReadAllText(file)).Select(value => $"{file}: {value}"));
        }
        Assert.True(failures.Count == 0, "Compose these phrases with a chord-table row:\n" + string.Join("\n", failures));
    }

    private static IEnumerable<string> CSharpViolations(CompilationUnitSyntax root, SemanticModel model, string relative)
    {
        foreach (ExpressionSyntax expression in root.DescendantNodes().OfType<ExpressionSyntax>()
            .Where(node => node is LiteralExpressionSyntax or InterpolatedStringExpressionSyntax
                || node.IsKind(SyntaxKind.AddExpression)))
        {
            if (IsDictionaryDefinition(expression, relative)) { continue; }
            string? text = TextOf(expression, model);
            if (text is not null && SpokenChord.IsMatch(text))
            {
                yield return $"{relative}:{expression.GetLocation().GetLineSpan().StartLinePosition.Line + 1}: {text}";
            }
        }
    }

    private static string? TextOf(ExpressionSyntax expression, SemanticModel model)
    {
        Optional<object?> constant = model.GetConstantValue(expression);
        if (constant.HasValue && constant.Value is string text) { return text; }
        return expression switch
        {
            // A placeholder keeps a literal modifier prefix visible even when
            // somebody hand-composes its key from a variable. Table-derived
            // complete chord tokens have no modifier literal beside them.
            InterpolatedStringExpressionSyntax interpolation => string.Concat(interpolation.Contents.Select(part =>
                part is InterpolatedStringTextSyntax literal ? literal.TextToken.ValueText
                    : part is InterpolationSyntax hole ? TextOf(hole.Expression, model) ?? "A" : "")),
            BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.AddExpression) =>
                (TextOf(binary.Left, model) ?? "A") + (TextOf(binary.Right, model) ?? "A"),
            ParenthesizedExpressionSyntax parenthesized => TextOf(parenthesized.Expression, model),
            _ => null,
        };
    }

    private static bool IsDictionaryDefinition(ExpressionSyntax expression, string relative)
    {
        if (relative != "Commands/HotkeyChords.cs" || expression is not LiteralExpressionSyntax literal
            || literal.Token.ValueText is not ("Left Bracket" or "Right Bracket" or "Left Arrow" or "Right Arrow" or "Up Arrow" or "Down Arrow")
            || expression.Parent is not AssignmentExpressionSyntax assignment || assignment.Right != expression
            || assignment.Left is not ImplicitElementAccessSyntax element || element.ArgumentList.Arguments.Count != 1
            || element.ArgumentList.Arguments[0].Expression is not LiteralExpressionSyntax key)
        {
            return false;
        }
        string? field = expression.Ancestors().OfType<VariableDeclaratorSyntax>().FirstOrDefault()?.Identifier.ValueText;
        string? owner = expression.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault()?.Identifier.ValueText;
        string? expectedKey = literal.Token.ValueText switch
        {
            "Left Bracket" => "[",
            "Right Bracket" => "]",
            "Left Arrow" => owner == "MacHotkeySpoken" ? "←" : "Left",
            "Right Arrow" => owner == "MacHotkeySpoken" ? "→" : "Right",
            "Up Arrow" => owner == "MacHotkeySpoken" ? "↑" : "Up",
            "Down Arrow" => owner == "MacHotkeySpoken" ? "↓" : "Down",
            _ => null,
        };
        return field == "KeyWord" && owner is "MacHotkeySpoken" or "WindowsHotkeySpoken"
            && key.Token.ValueText == expectedKey;
    }

    private static IEnumerable<string> XamlViolations(string source)
    {
        XDocument document = XDocument.Parse(source);
        return document.Descendants().SelectMany(element => element.Attributes().Select(attribute => attribute.Value)
                .Concat(element.Nodes().OfType<XText>().Select(node => node.Value)))
            .Concat(document.Descendants().Where(element => element.Name.LocalName is "TextBlock" or "Paragraph" or "Span" or "Hyperlink")
                .Select(InlineText))
            .Where(value => SpokenChord.IsMatch(value));
    }

    private static string InlineText(XElement element)
    {
        string text = (string?)element.Attribute("Text") ?? "";
        if (text.StartsWith('{')) { text = "A"; }
        return text + string.Concat(element.Nodes().Select(node => node switch
        {
            XText literal => literal.Value,
            XElement inline when inline.Name.LocalName == "LineBreak" => "\n",
            XElement inline when inline.Name.LocalName is "Run" or "Span" or "Bold" or "Italic" or "Underline" or "Hyperlink" => InlineText(inline),
            _ => "",
        }));
    }

    [Theory]
    [InlineData("\"Control Enter\"")]
    [InlineData("\"Control \" + \"Alt Enter\"")]
    [InlineData("\"\"\"Control Alt Enter\"\"\"")]
    [InlineData("$\"Use Control {key}\"")]
    [InlineData("\"Control \" + key")]
    [InlineData("prefix + \" Enter\"")]
    public void AddedFilesCannotHideOrdinaryRawConcatenatedOrInterpolatedChords(string expression)
    {
        string source = "class NewHelp { const string prefix = \"Control\"; string Help(string key) => " + expression + "; }";
        Assert.NotEmpty(FixtureViolations(source, "NewHelp.cs"));
    }

    [Fact]
    public void CommentsInactiveCodeAndTableCompositionAreNotLiteralSpeech()
    {
        string source = """
            class NewHelp {
                // Control Enter is a comment.
            #if NEVER_DEFINED
                string Old = "Control Enter";
            #endif
                string Help(string spoken) => $"Use {spoken} to open. Escape closes.";
            }
            """;
        Assert.Empty(FixtureViolations(source, "NewHelp.cs"));
        Assert.Empty(XamlViolations("<TextBlock><!-- Control Enter --><Run Text='Enter opens. Escape closes.'/></TextBlock>"));
        Assert.All(BareKeys, key => Assert.DoesNotMatch(SpokenChord, key));
    }

    [Fact]
    public void DictionaryExemptionIsOnlyTheActualBracketAndArrowDefinitions()
    {
        string source = """
            class WindowsHotkeySpoken {
                static readonly System.Collections.Generic.Dictionary<string,string> KeyWord = new() {
                    ["["] = "Left Bracket", ["]"] = "Right Bracket",
                    ["Left"] = "Left Arrow", ["Right"] = "Right Arrow",
                    ["Up"] = "Up Arrow", ["Down"] = "Down Arrow"
                };
                string Help => "Control Enter";
            }
            """;
        Assert.Single(FixtureViolations(source, "Commands/HotkeyChords.cs"));
        Assert.Equal(7, FixtureViolations(source, "Other.cs").Length);
    }

    [Theory]
    [InlineData("<TextBlock Text='Control Enter'/>")]
    [InlineData("<TextBlock>Control Alt Enter</TextBlock>")]
    [InlineData("<TextBlock Text='Left Bracket'/>")]
    [InlineData("<TextBlock Text='Right Arrow'/>")]
    [InlineData("<TextBlock Text='Control Equals'/>")]
    [InlineData("<TextBlock Text='Alt Backtick'/>")]
    [InlineData("<TextBlock><Run>Control </Run><Run>Enter</Run></TextBlock>")]
    [InlineData("<TextBlock><Run Text='Control '/><Run Text='Enter'/></TextBlock>")]
    [InlineData("<TextBlock><Span><Run Text='Control '/></Span><Run Text='Enter'/></TextBlock>")]
    public void XamlAttributesAndTextAreAudited(string source) => Assert.NotEmpty(XamlViolations(source));

    private static string[] FixtureViolations(string source, string relative)
    {
        SyntaxTree tree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview));
        Assert.DoesNotContain(tree.GetDiagnostics(), diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        CSharpCompilation compilation = CSharpCompilation.Create("SpeechFixture", [tree],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
        return CSharpViolations((CompilationUnitSyntax)tree.GetRoot(), compilation.GetSemanticModel(tree), relative).ToArray();
    }
}
