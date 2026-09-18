// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests;

/// <summary>Declaration evidence, not dataflow or audible evidence. C# sites
/// bind to the generated event type. Swift uses the recorded #1108 lexical
/// boundary: comments are excluded; contextual enum inference is not compiled.</summary>
internal static class A11yTriggerInventory
{
    internal sealed record Site(string Key, string Member, int Ordinal, string Expression);
    internal sealed record Inventory(string[] Keys, Site[] Windows, Site[] Mac);

    internal static Inventory Read()
    {
        string binding = Path.Combine(SourceText.RepoRoot(), "apps", "slate-windows", "src",
            "SlateUniffi", "generated", "slate_uniffi.cs");
        RecordDeclarationSyntax family = CSharpSyntaxTree.ParseText(File.ReadAllText(binding))
            .GetRoot().DescendantNodes().OfType<RecordDeclarationSyntax>()
            .Single(record => record.Identifier.ValueText == nameof(A11yEvent));
        string[] keys = family.Members.OfType<RecordDeclarationSyntax>()
            .Select(record => record.Identifier.ValueText).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(typeof(A11yEvent).GetNestedTypes().Select(type => type.Name).Order(StringComparer.Ordinal), keys);

        Site[] windows = ShellCompilation.Sources
            .SelectMany(entry => WindowsSites(entry.Relative, entry.Source.Root, ShellCompilation.ModelFor(entry.Source)))
            .OrderBy(site => site.Member, StringComparer.Ordinal).ThenBy(site => site.Ordinal).ToArray();
        string macRoot = Path.Combine(SourceText.RepoRoot(), "apps", "slate-mac", "Sources", "SlateMac");
        Site[] mac = Directory.EnumerateFiles(macRoot, "*.swift", SearchOption.AllDirectories)
            .Where(path => !Path.GetRelativePath(macRoot, path).Split(Path.DirectorySeparatorChar).Contains("generated"))
            .SelectMany(path => MacSites(Path.GetRelativePath(macRoot, path).Replace('\\', '/'), File.ReadAllText(path), keys))
            .OrderBy(site => site.Member, StringComparer.Ordinal).ThenBy(site => site.Ordinal).ToArray();
        return new(keys, windows, mac);
    }

    internal static Site[] WindowsSites(string path, SyntaxNode root, SemanticModel model)
    {
        var counts = new Dictionary<(string Key, string Member), int>();
        var sites = new List<Site>();
        foreach (BaseObjectCreationExpressionSyntax creation in root.DescendantNodes().OfType<BaseObjectCreationExpressionSyntax>())
        {
            if (model.GetTypeInfo(creation).Type is not INamedTypeSymbol type
                || type.ContainingType?.ToDisplayString() != "uniffi.slate_uniffi.A11yEvent"
                || type.ContainingAssembly.Identity.ToString() != typeof(A11yEvent).Assembly.FullName)
            {
                continue;
            }
            string member = path + "#" + MemberName(creation);
            var key = (type.Name, member);
            counts.TryGetValue(key, out int ordinal);
            counts[key] = ++ordinal;
            sites.Add(new(type.Name, member, ordinal, creation.NormalizeWhitespace().ToFullString()));
        }
        return sites.ToArray();
    }

    private static string MemberName(SyntaxNode node)
    {
        MemberDeclarationSyntax declaration = node.Ancestors().OfType<MemberDeclarationSyntax>()
            .First(member => member is BaseMethodDeclarationSyntax or BasePropertyDeclarationSyntax or FieldDeclarationSyntax);
        string name = declaration switch
        {
            MethodDeclarationSyntax method => method.Identifier.ValueText,
            ConstructorDeclarationSyntax constructor => constructor.Identifier.ValueText,
            PropertyDeclarationSyntax property => property.Identifier.ValueText,
            FieldDeclarationSyntax field => field.Declaration.Variables.Single().Identifier.ValueText,
            _ => throw new InvalidOperationException($"Unclassified event owner: {declaration.Kind()}"),
        };
        string type = declaration.Ancestors().OfType<TypeDeclarationSyntax>().First().Identifier.ValueText;
        return type + "." + name;
    }

    private static readonly Regex SwiftMember = new(
        @"^(?<indent> *)(?:(?:@\w+(?:\([^\n]*\))?|private|fileprivate|internal|public|open|static|final|override|mutating|nonisolated|lazy)\s+)*(?<kind>func|var|let|init)\s*(?<name>\w*)",
        RegexOptions.Multiline | RegexOptions.CultureInvariant);

    private sealed record SwiftBrace(int Start, int End, int Depth, int Delimiters);
    private sealed record SwiftOwner(Match Declaration, int End);

    private static (SwiftBrace[] Braces, (int Start, int End)[] Patterns) SwiftBoundaries(string text)
    {
        var braces = new List<SwiftBrace>();
        var open = new Stack<(int Start, int Delimiters)>();
        var patterns = new List<(int Start, int End)>();
        int delimiters = 0;
        int patternStart = -1;
        int patternDepth = 0;
        for (int index = 0; index < text.Length; index++)
        {
            int literalEnd = SwiftSource.EndOfLiteral(text, index);
            if (literalEnd > index) { index = literalEnd - 1; continue; }
            char token = text[index];
            if (char.IsLetter(token) || token == '_')
            {
                int end = index + 1;
                while (end < text.Length && (char.IsLetterOrDigit(text[end]) || text[end] == '_')) { end++; }
                string word = text[index..end];
                if (word == "case" && (index == 0 || text[index - 1] != '.'))
                {
                    patternStart = index;
                    patternDepth = delimiters;
                }
                else if (patternStart >= 0 && delimiters == patternDepth && word is "in" or "where")
                {
                    patterns.Add((patternStart, index));
                    patternStart = -1;
                }
                index = end - 1;
                continue;
            }
            if (patternStart >= 0 && delimiters == patternDepth && token is ':' or '=' or '{' or '}')
            {
                patterns.Add((patternStart, index));
                patternStart = -1;
            }
            if (token is '(' or '[') { delimiters++; }
            else if (token is ')' or ']') { delimiters--; }
            else if (token == '{') { open.Push((index, delimiters)); }
            else if (token == '}' && open.TryPop(out var start))
            {
                braces.Add(new(start.Start, index, open.Count, start.Delimiters));
            }
        }
        return (braces.OrderBy(brace => brace.Start).ToArray(), patterns.ToArray());
    }

    private static SwiftOwner[] SwiftOwners(string text, SwiftBrace[] braces)
    {
        Match[] declarations = SwiftMember.Matches(text).ToArray();
        return declarations.Select(declaration =>
        {
            SwiftBrace? enclosing = braces.LastOrDefault(brace => brace.Start < declaration.Index && brace.End > declaration.Index);
            int end = Math.Min(enclosing?.End ?? text.Length,
                declarations.FirstOrDefault(next => next.Index > declaration.Index
                    && next.Groups["indent"].Length <= declaration.Groups["indent"].Length)?.Index ?? text.Length);
            if (declaration.Groups["kind"].Value is "func" or "init")
            {
                // Skip braces in default-argument closures. A function owns
                // only its balanced body; after it closes its parent resumes.
                SwiftBrace? body = braces.FirstOrDefault(brace => brace.Start > declaration.Index && brace.Start < end
                    && brace.Depth == (enclosing?.Depth + 1 ?? 0) && brace.Delimiters == 0);
                if (body is not null) { end = body.End; }
            }
            return new SwiftOwner(declaration, end);
        }).ToArray();
    }

    internal static Site[] MacSites(string path, string source, string[] keys)
    {
        string text = SwiftSource.WithoutComments(source);
        // An enclosing function wins over its local variables and resumes
        // after a nested function closes. Nested types may put real members
        // at any indentation. Case patterns end at their delimiter, not the line.
        var (braces, patterns) = SwiftBoundaries(text);
        SwiftOwner[] declarations = SwiftOwners(text, braces);
        var counts = new Dictionary<(string Key, string Member), int>();
        var sites = new List<Site>();
        foreach (string key in keys)
        {
            string camel = char.ToLowerInvariant(key[0]) + key[1..];
            foreach (Match match in Regex.Matches(text, @"(?<![\w.])(?:A11yEvent)?\." + camel + @"\b"))
            {
                string after = text[(match.Index + match.Length)..];
                if (patterns.Any(pattern => pattern.Start <= match.Index && match.Index < pattern.End)) { continue; }
                if (key is "Canvas" or "Graph" && !Regex.IsMatch(after, @"^\s*\(\s*event\s*:")) { continue; }
                // A payloadless enum expression is a value wherever Swift
                // permits one, including arrays and implicit returns. Member
                // reads are excluded by the qualified-name boundary above.
                Match? owner = null;
                foreach (SwiftOwner candidate in declarations.Where(d => d.Declaration.Index < match.Index && match.Index < d.End))
                {
                    Match declaration = candidate.Declaration;
                    if (owner is null || declaration.Groups["indent"].Length <= owner.Groups["indent"].Length
                        || declaration.Groups["kind"].Value is "func" or "init")
                    {
                        owner = declaration;
                    }
                }
                Assert.True(owner is not null, $"No Swift owner for {path}:{text[..match.Index].Count(c => c == '\n') + 1} {key}");
                string name = owner.Groups["kind"].Value == "init" ? "init" : owner.Groups["name"].Value;
                string member = path + "#" + name;
                var identity = (key, member);
                counts.TryGetValue(identity, out int ordinal);
                counts[identity] = ++ordinal;
                int lineEnd = text.IndexOf('\n', match.Index);
                string expression = text[match.Index..(lineEnd < 0 ? text.Length : lineEnd)].Trim();
                sites.Add(new(key, member, ordinal, expression));
            }
        }
        return sites.ToArray();
    }
}
