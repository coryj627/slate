// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// W7-7 PR 4b (#1247, contract R-5; the owner's S6): the site-to-witness
// manifest. Every place the shell can take the element holding the keys
// away under them — a code assignment of an ItemsSource, a panel's
// Children.Clear() rebuild, a XAML trigger that disables or collapses an
// element — is listed in docs/plans/18_windows_port/focus_integrity_manifest.json
// with the hosted fact that witnesses its landing, or an exemption and its
// reason. The census scrapes the sites and fails on a site the manifest does
// not list, on a listed site that is gone, and on a witness that names no
// fact in this project — so the next instance of any of these classes is
// caught when it is written, not in the next review round. Region roots are
// the region guard census's (RegionGuardCensus); a binding that collapses or
// disables an element inside a guarded region is the guard's, witnessed by
// RegionFocusGuardTests. SLATE_FOCUS_MANIFEST_UPDATE=1 rewrites the manifest
// with the scraped sites, keeping every assignment and leaving new sites
// unassigned, which fails until a witness is written.

using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SlateWindows.Tests.Censuses;

[Trait("census", "focus-integrity-manifest")]
public sealed class FocusIntegrityManifestCensus
{
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static string ManifestPath() =>
        Path.Combine(SourceText.RepoRoot(), "docs", "plans", "18_windows_port", "focus_integrity_manifest.json");

    [Fact]
    public void EverySiteThatCanTakeTheKeysAwayNamesItsWitness()
    {
        SortedSet<string> scraped = Sites();
        Assert.True(scraped.Count >= 30, $"only {scraped.Count} sites were scraped; the scrape is broken.");
        Dictionary<string, string> manifest = Manifest();

        if (Environment.GetEnvironmentVariable("SLATE_FOCUS_MANIFEST_UPDATE") == "1")
        {
            Write(scraped.ToDictionary(site => site, site => manifest.GetValueOrDefault(site, string.Empty)));
            manifest = Manifest();
        }

        string[] unlisted = [.. scraped.Where(site => !manifest.ContainsKey(site))];
        string[] stale = [.. manifest.Keys.Where(site => !scraped.Contains(site))];
        HashSet<string> facts = Facts();
        string[] unwitnessed =
        [
            .. manifest
                .Where(entry => !(entry.Value.StartsWith("exempt: ", StringComparison.Ordinal) && entry.Value.Length > 12)
                    && !facts.Contains(entry.Value))
                .Select(entry => $"{entry.Key} → '{entry.Value}'"),
        ];
        Assert.True(unlisted.Length == 0, "Sites that can take the keys away with no witness in the manifest (S6):\n  " + string.Join("\n  ", unlisted));
        Assert.True(stale.Length == 0, "Manifest entries whose site is gone:\n  " + string.Join("\n  ", stale));
        Assert.True(unwitnessed.Length == 0, "Manifest witnesses that name no fact in this project:\n  " + string.Join("\n  ", unwitnessed));
    }

    /// <summary>The scraped sites: code ItemsSource assignments and
    /// Children.Clear() rebuilds, by declaring member and receiver; XAML
    /// triggers setting IsEnabled or Visibility, by the element or keyed
    /// style that carries them.</summary>
    internal static SortedSet<string> Sites()
    {
        var sites = new SortedSet<string>(StringComparer.Ordinal);
        foreach ((string _, CSharpSource source) in ShellCompilation.Sources)
        {
            SemanticModel model = ShellCompilation.ModelFor(source);
            foreach (AssignmentExpressionSyntax assignment in source.Root.DescendantNodes().OfType<AssignmentExpressionSyntax>())
            {
                if (assignment.Left is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "ItemsSource" } access)
                {
                    sites.Add($"items-source {Member(model, assignment)} {access.Expression}");
                }
            }

            foreach (InvocationExpressionSyntax call in source.Root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (call.Expression is MemberAccessExpressionSyntax
                    {
                        Name.Identifier.ValueText: "Clear",
                        Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Children" } children,
                    })
                {
                    sites.Add($"children-clear {Member(model, call)} {children.Expression}");
                }
            }
        }

        foreach (string path in Directory.EnumerateFiles(SourceText.ShellSourceRoot(), "*.xaml", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)))
        {
            XDocument document = XDocument.Load(path, LoadOptions.SetLineInfo);
            foreach (XElement setter in document.Descendants().Where(element => element.Name.LocalName == "Setter"
                && (string?)element.Attribute("Property") is "IsEnabled" or "Visibility"
                && element.Ancestors().Any(ancestor => ancestor.Name.LocalName.EndsWith(".Triggers", StringComparison.Ordinal))))
            {
                XElement owner = setter.Ancestors().First(ancestor =>
                    ancestor.Name.LocalName is "Style" or "ControlTemplate" or "DataTemplate");
                string carrier = (string?)owner.Attribute(Xaml + "Key") is { } key
                    ? $"{owner.Name.LocalName}:{key}"
                    : Identity(owner.Parent?.Parent) ?? $"line {((IXmlLineInfo)owner).LineNumber}";
                sites.Add($"xaml-trigger {Path.GetFileName(path)} {carrier} {(string?)setter.Attribute("Property")}");
            }
        }

        return sites;
    }

    /// <summary>An element's identity that survives edits elsewhere in the
    /// file: its own name or automation id; else the first named element it
    /// holds; else its type, the nearest named element holding it, and its
    /// ordinal among that element's descendants of its type.</summary>
    private static string? Identity(XElement? element)
    {
        if (element is null)
        {
            return null;
        }

        if (Named(element) is { } own)
        {
            return own;
        }

        if (element.Descendants().Select(Named).FirstOrDefault(name => name is not null) is { } held)
        {
            return $"{element.Name.LocalName}({held})";
        }

        XElement? holder = element.Ancestors().FirstOrDefault(ancestor => Named(ancestor) is not null);
        int ordinal = holder is null
            ? 0
            : holder.Descendants(element.Name).TakeWhile(candidate => !ReferenceEquals(candidate, element)).Count();
        return $"{element.Name.LocalName} in {(holder is null ? "the window" : Named(holder))}#{ordinal}";
    }

    private static string? Named(XElement element) =>
        (string?)element.Attribute(Xaml + "Name")
            ?? (string?)element.Attribute("AutomationProperties.AutomationId")
            ?? (string?)element.Attribute(Xaml + "Key");

    private static string Member(SemanticModel model, SyntaxNode node)
    {
        ISymbol? member = model.GetEnclosingSymbol(node.SpanStart);
        while (member is IMethodSymbol { MethodKind: MethodKind.AnonymousFunction or MethodKind.LocalFunction })
        {
            member = member.ContainingSymbol;
        }

        return $"{member?.ContainingType?.ToDisplayString()}.{member?.Name}";
    }

    private static Dictionary<string, string> Manifest()
    {
        if (!File.Exists(ManifestPath()))
        {
            return [];
        }

        JsonNode root = JsonNode.Parse(File.ReadAllText(ManifestPath()))!;
        return root["sites"]!.AsObject().ToDictionary(entry => entry.Key, entry => (string?)entry.Value ?? string.Empty, StringComparer.Ordinal);
    }

    private static void Write(Dictionary<string, string> sites)
    {
        var root = new JsonObject
        {
            ["_doc"] = "W7-7 PR 4b (#1247, R-5; the owner's S6): every site that can take the element holding the keys away under them, "
                + "and the hosted fact that witnesses its landing ('Class.Method'), or 'exempt: <reason>'. "
                + "FocusIntegrityManifestCensus scrapes the sites; SLATE_FOCUS_MANIFEST_UPDATE=1 rewrites this file, keeping every assignment.",
            ["sites"] = new JsonObject(sites.OrderBy(entry => entry.Key, StringComparer.Ordinal)
                .Select(entry => KeyValuePair.Create(entry.Key, (JsonNode?)JsonValue.Create(entry.Value)))),
        };
        File.WriteAllText(
            ManifestPath(),
            root.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            }) + "\n");
    }

    /// <summary>Every fact in this project, as "Class.Method".</summary>
    private static HashSet<string> Facts() =>
    [
        .. typeof(FocusIntegrityManifestCensus).Assembly.GetTypes()
            .SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(method => method.GetCustomAttributes<FactAttribute>(inherit: true).Any())
                .Select(method => $"{type.Name}.{method.Name}")),
    ];
}
