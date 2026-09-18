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

    internal static Site[] MacSites(string path, string source, string[] keys)
    {
        string text = SwiftSource.WithoutComments(source);
        // A member is at the type's indentation, never a local variable.
        // Swift files use either two or four spaces; the enclosing function
        // wins over deeper declarations. A case spelling is retained only at
        // a construction (payload parentheses or an argument/return position).
        Match[] declarations = SwiftMember.Matches(text).Where(match => match.Groups["indent"].Length <= 4).ToArray();
        var counts = new Dictionary<(string Key, string Member), int>();
        var sites = new List<Site>();
        foreach (string key in keys)
        {
            string camel = char.ToLowerInvariant(key[0]) + key[1..];
            foreach (Match match in Regex.Matches(text, @"(?<![\w.])(?:A11yEvent)?\." + camel + @"\b"))
            {
                string after = text[(match.Index + match.Length)..];
                string before = text[..match.Index];
                string linePrefix = before[(before.LastIndexOf('\n') + 1)..];
                if (Regex.IsMatch(linePrefix, @"\bcase\b")) { continue; }
                if (key is "Canvas" or "Graph" && !Regex.IsMatch(after, @"^\s*\(\s*event\s*:")) { continue; }
                bool hasPayload = Regex.IsMatch(after, @"^\s*\(");
                bool bareArgument = Regex.IsMatch(before, @"(?:[(:=?]|\breturn)\s*$")
                    && Regex.IsMatch(after, @"^\s*(?:[,:)}\n]|$)");
                if (!hasPayload && !bareArgument) { continue; }
                Match? owner = null;
                foreach (Match declaration in declarations.TakeWhile(d => d.Index < match.Index))
                {
                    if (owner is null || declaration.Groups["indent"].Length <= owner.Groups["indent"].Length
                        || declaration.Groups["kind"].Value is "func" or "init")
                    {
                        owner = declaration;
                    }
                }
                Assert.NotNull(owner);
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
