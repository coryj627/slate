// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests.Censuses;

[Trait("census", "editor-peer-doctrine")]
public sealed class EditorPeerDoctrineCensus
{
    [Fact]
    public void TheContractTableAndMappingSwitchCoverTheSameCanonicalKindsAndAttributes()
    {
        string contract = File.ReadAllText(Path.Combine(SourceText.RepoRoot(), "docs", "plans", "37_editor_peer_contracts.md"));
        var documented = Regex.Matches(contract, @"(?m)^\| (\w+) \| (.+) \|$")
            .Where(match => match.Groups[1].Value != "Canonical")
            .ToDictionary(match => match.Groups[1].Value, match =>
                Regex.Matches(match.Groups[2].Value, @"(?:^|; )(\w+) =")
                    .Select(attribute => attribute.Groups[1].Value).Order().ToArray());
        CSharpSource source = CSharpSource.Load("EditorSemanticText.cs");
        SwitchExpressionSyntax mapping = Assert.Single(source.Method("Value").DescendantNodes().OfType<SwitchExpressionSyntax>());
        var actual = new Dictionary<string, HashSet<string>>();
        foreach (SwitchExpressionArmSyntax arm in mapping.Arms)
        {
            if (arm.Pattern is not RecursivePatternSyntax tuple || tuple.PositionalPatternClause is null)
            {
                continue;
            }
            var patterns = tuple.PositionalPatternClause.Subpatterns;
            string attribute = patterns[1].Pattern.ToString().Replace("Attribute", "", StringComparison.Ordinal);
            IEnumerable<string> kinds = patterns[0].DescendantNodes().OfType<QualifiedNameSyntax>()
                .Where(name => name.Left.ToString() == "EditorSpanKind").Select(name => name.Right.ToString())
                .Concat(patterns[0].DescendantNodes().OfType<MemberAccessExpressionSyntax>()
                    .Where(member => member.Expression.ToString() == "EditorSpanKind").Select(member => member.Name.ToString()));
            foreach (string kind in kinds.Distinct())
            {
                if (!actual.TryGetValue(kind, out HashSet<string>? attributes))
                {
                    actual[kind] = attributes = [];
                }
                attributes.Add(attribute);
            }
        }
        string[] canonical = typeof(EditorSpanKind).GetNestedTypes().Where(type => type.IsSubclassOf(typeof(EditorSpanKind)))
            .Select(type => type.Name).Order().ToArray();
        Assert.Equal(canonical, documented.Keys.Order());
        Assert.Equal(canonical, actual.Keys.Order());
        foreach (string kind in canonical)
        {
            Assert.Equal(documented[kind], actual[kind].Order());
        }
    }

    [Fact]
    public void ThePeerUsesOnlyTheSessionsReadOnlyQueryAndDoesNotClassifyMarkdown()
    {
        CSharpSource peer = CSharpSource.Load("EditorSemanticText.cs");
        InvocationExpressionSyntax[] queries = peer.Root.DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Where(call => call.Expression is MemberAccessExpressionSyntax member && member.Name.Identifier.ValueText == "InspectInRange").ToArray();
        Assert.Single(queries);
        Assert.Equal("_session.InspectInRange(start, end)", queries[0].ToString());
        foreach (string file in new[] { "EditorSemanticText.cs", "SlateTextEditor.cs", "EditorHighlighting.cs" })
        {
            CSharpSource source = CSharpSource.Load(file);
            string[] names = source.Root.DescendantNodes().OfType<MemberAccessExpressionSyntax>()
                .Select(member => member.Name.Identifier.ValueText).ToArray();
            Assert.DoesNotContain("HostComposed", names);
            Assert.DoesNotContain("EditorHighlightSpans", names);
            Assert.DoesNotContain("EditorHighlightSpansInRange", names);
            Assert.DoesNotContain(source.Root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>(),
                creation => creation.Type.ToString().Contains("Regex", StringComparison.Ordinal)
                    || creation.Type.ToString().Contains("Markdown", StringComparison.Ordinal));
        }
        Assert.DoesNotContain(peer.Root.DescendantNodes().OfType<MemberAccessExpressionSyntax>(),
            member => member.Name.Identifier.ValueText is "HighlightInRange" or "LatestHighlightWindow");
        CSharpSource session = CSharpSource.Load("AvalonDocumentBufferSession.cs");
        Assert.Contains(session.Method("InspectInRange").DescendantNodes().OfType<InvocationExpressionSyntax>(),
            call => call.ToString() == "ComputeHighlightWindow(startUtf16, endUtf16, retain: false)");
    }
}
