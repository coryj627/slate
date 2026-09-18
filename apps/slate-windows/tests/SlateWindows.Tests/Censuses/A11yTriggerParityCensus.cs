// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using uniffi.slate_uniffi;

namespace SlateWindows.Tests.Censuses;

[Trait("census", "a11y-trigger-parity")]
public sealed class A11yTriggerParityCensus
{
    private static readonly Lazy<A11yTriggerInventory.Inventory> Inventory = new(A11yTriggerInventory.Read);
    private static string ContractsPath => Path.Combine(SourceText.RepoRoot(), "docs", "plans", "38_notification_dispatcher_contracts.md");
    private static Register Registration => JsonSerializer.Deserialize<Register>(File.ReadAllText(
        Path.Combine(SourceText.RepoRoot(), "docs", "plans", "18_windows_port", "a11y_trigger_designations.json")),
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    private sealed record Designation(string Reason, string Decision, string Status);
    private sealed record Residue(string Engine, string Decision, string Status);
    private sealed record Register(Dictionary<string, Dictionary<string, Designation>> Designated,
        Dictionary<string, string> Roles, Dictionary<string, Dictionary<string, Residue>> Residue);
    private sealed record Row(string Key, string Role, string Mac, string Windows, string Facts, string Observed, string Designation);

    private static string[][] Table(string header, int columns) => File.ReadAllText(ContractsPath).Split('\n')
        .SkipWhile(line => !line.StartsWith(header, StringComparison.Ordinal)).Skip(2)
        .TakeWhile(line => line.StartsWith('|')).Select(line =>
        {
            string[] cells = Regex.Split(line.Trim().Trim('|'), @"(?<!\\)\|").Select(cell => cell.Trim()).ToArray();
            Assert.Equal(columns, cells.Length);
            Assert.All(cells, cell => Assert.NotEmpty(cell));
            return cells;
        }).ToArray();

    private static Row[] Rows() => Table("| Key | Role | Mac site(s) |", 7)
        .Select(c => new Row(c[0].Trim('`'), c[1], c[2], c[3], c[4], c[5], c[6])).ToArray();

    private static string SiteId(A11yTriggerInventory.Site site) => $"{site.Member}@{site.Ordinal}";
    private static string[] Tokens(string cell) => Regex.Matches(cell, "`([^`]+)`").Select(match => match.Groups[1].Value).ToArray();

    private static string[] CoverageFailures(IEnumerable<string> actual, IEnumerable<string> recorded)
    {
        string[] expected = actual.ToArray();
        string[] registered = recorded.ToArray();
        return expected.Except(registered, StringComparer.Ordinal).Select(site => "Missing " + site)
            .Concat(registered.Except(expected, StringComparer.Ordinal).Select(site => "Stale " + site))
            .Concat(registered.GroupBy(site => site, StringComparer.Ordinal).Where(group => group.Count() != 1)
                .Select(group => "Duplicate " + group.Key)).ToArray();
    }

    [Fact]
    public void EveryBindingKeyAndEveryConstructionIsCoveredInBothDirections()
    {
        A11yTriggerInventory.Inventory inventory = Inventory.Value;
        Register register = Registration;
        Row[] rows = Rows();
        Assert.Empty(CoverageFailures(inventory.Keys, rows.Select(row => row.Key)));
        Assert.Equal(["Mac", "Windows"], register.Designated.Keys.Order());
        Assert.Equal(["GridGroup"], register.Roles.Keys);
        Assert.Equal("label", register.Roles["GridGroup"]);
        foreach (var (platform, sites) in new[] { ("Mac", inventory.Mac), ("Windows", inventory.Windows) })
        {
            string[] missing = inventory.Keys.Except(sites.Select(site => site.Key)).ToArray();
            Assert.Empty(CoverageFailures(missing, register.Designated[platform].Keys));
            foreach (Row row in rows)
            {
                if (row.Key is "Canvas" or "Graph")
                {
                    Assert.Equal("family-ref", row.Role);
                    string document = row.Key == "Canvas" ? "34_canvas_contracts.md" : "35_graph_contracts.md";
                    Assert.Contains(document, row.Mac, StringComparison.Ordinal);
                    Assert.Contains(document, row.Windows, StringComparison.Ordinal);
                    continue;
                }
                Assert.Equal(register.Roles.GetValueOrDefault(row.Key, "posted"), row.Role);
                string cell = platform == "Mac" ? row.Mac : row.Windows;
                Assert.Empty(CoverageFailures(sites.Where(site => site.Key == row.Key).Select(SiteId), Tokens(cell)));
                if (register.Designated[platform].TryGetValue(row.Key, out Designation? designation))
                {
                    Assert.Equal("—", cell);
                    Assert.Contains($"{platform} {designation.Status}: {designation.Reason}", row.Designation, StringComparison.Ordinal);
                    AssertDecision(designation.Decision, designation.Status);
                }
            }
        }
    }

    [Fact]
    public void EveryResidueConstructionHasItsOwnNamedEngineAndNoStaleWaivers()
    {
        A11yTriggerInventory.Inventory inventory = Inventory.Value;
        Register register = Registration;
        string[][] rows = Table("| Platform | Site | Engine |", 5);
        Assert.Equal(["Mac", "Windows"], register.Residue.Keys.Order());
        Assert.Equal(["Mac", "Windows"], rows.Select(row => row[0]).Distinct().Order());
        foreach (var (platform, sites) in new[] { ("Mac", inventory.Mac), ("Windows", inventory.Windows) })
        {
            string[] actual = sites.Where(site => site.Key == "HostComposed").Select(SiteId).ToArray();
            Assert.Empty(CoverageFailures(actual, register.Residue[platform].Keys));
            Assert.Empty(CoverageFailures(actual, rows.Where(row => row[0] == platform).Select(row => row[1].Trim('`'))));
            foreach (string[] row in rows.Where(row => row[0] == platform))
            {
                Residue entry = register.Residue[platform][row[1].Trim('`')];
                Assert.NotEmpty(entry.Engine);
                Assert.Equal(entry.Engine, row[2]);
                Assert.Contains(entry.Decision, row[3], StringComparison.Ordinal);
                Assert.Equal(entry.Status, row[4]);
                AssertDecision(entry.Decision, entry.Status);
            }
        }
    }

    [Fact]
    public void EveryNonFamilyRowHasAnExecutableCorpusAndDispatchWitness()
    {
        string[] corpusKinds = A11yCorpusCensus.DispatcherCases().Select(sample => sample.Event.GetType().Name)
            .Distinct().Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(Inventory.Value.Keys, corpusKinds);
        foreach (Row row in Rows())
        {
            foreach (string fact in Tokens(row.Facts).Concat(Tokens(row.Observed)))
            {
                string[] parts = fact.Split('#');
                Assert.Equal(2, parts.Length);
                string path = Path.Combine(SourceText.RepoRoot(), "apps", "slate-windows", "tests", "SlateWindows.Tests", parts[0]);
                Assert.True(File.Exists(path), $"Missing evidence source: {fact}");
                var method = CSharpSource.LoadPath(path).Method(parts[1]);
                Assert.True(method.AttributeLists.SelectMany(list => list.Attributes)
                    .Any(attribute => attribute.Name.ToString() is "Fact" or "Theory"), $"Not an xUnit fact: {fact}");
                Assert.Contains(TestEvidenceCompilation.Projects, evidence => evidence.HasTestEvidence(parts[1]));
            }
            if (row.Key is "Canvas" or "Graph") { continue; }
            Assert.Equal("unit-observed: `AccessibilityNotificationDispatcherTests.cs#EveryCorpusEventReachesTheNativeBoundaryWithItsGoldenTextAndPriority`", row.Observed);
            Assert.Equal("`Censuses/A11yCorpusCensus.cs#EveryCorpusEventRendersTheCommittedIdentityTextAndPriority`", row.Facts);
        }
    }

    [Fact]
    public void CoverageRejectsMissingStaleDuplicateAndWrongMemberEvidence()
    {
        string[] actual = ["File.cs#Owner.Post@1", "File.cs#Owner.Post@2", "Other.swift#announce@1"];
        Assert.Empty(CoverageFailures(actual, actual));
        Assert.NotEmpty(CoverageFailures(actual, actual.Skip(1)));
        Assert.NotEmpty(CoverageFailures(actual, actual.Append("Dead.cs#Post@1")));
        Assert.NotEmpty(CoverageFailures(actual, actual.Append(actual[0])));
        Assert.NotEmpty(CoverageFailures(actual, actual.Select(site => site.Replace("Owner.Post", "Owner.Decoy", StringComparison.Ordinal))));
    }

    [Fact]
    public void WindowsInventoryBindsRealConstructionsIncludingAliasesAndTargetTypedNew()
    {
        const string source = """
            using E = uniffi.slate_uniffi.A11yEvent;
            class Owner {
                // new E.NoteSaved("comment")
                string decoy = "new E.NoteSaved(\"string\")";
                object Post() => new E.NoteSaved("real");
                E.NoteSaved Typed() => new("target-typed");
                object Fake() => new A11yEvent.NoteSaved();
            }
            class A11yEvent { public class NoteSaved {} }
            """;
        SyntaxTree tree = CSharpSyntaxTree.ParseText(source);
        CSharpCompilation compilation = CSharpCompilation.Create("inventory-mutation", [tree],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
             MetadataReference.CreateFromFile(typeof(A11yEvent).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        A11yTriggerInventory.Site[] sites = A11yTriggerInventory.WindowsSites("File.cs", tree.GetRoot(), compilation.GetSemanticModel(tree));
        Assert.Equal(["File.cs#Owner.Post", "File.cs#Owner.Typed"], sites.Select(site => site.Member));
        Assert.All(sites, site => Assert.Equal("NoteSaved", site.Key));
    }

    [Fact]
    public void MacInventoryRejectsCommentsPatternsAndUnrelatedMemberReads()
    {
        const string source = """
            final class Host {
                func announce() {
                    // post(.noteSaved(filename: "comment"))
                    /* outer /* post(.noteSaved(filename: "nested")) */ */
                    post(.noteSaved(filename: "real"))
                    event = .rightPaneShown
                    post(open ? .rightPaneShown : .rightPaneHidden)
                    if case .canvas(let value) = item { consume(value) }
                    let state = item.noteSaved
                    post(.canvas(event: canvasEvent))
                }
            }
            """;
        A11yTriggerInventory.Site[] sites = A11yTriggerInventory.MacSites("File.swift", source,
            ["NoteSaved", "RightPaneShown", "RightPaneHidden", "Canvas"]);
        Assert.Equal(5, sites.Length);
        Assert.All(sites, site => Assert.Equal("File.swift#announce", site.Member));
        Assert.Single(sites, site => site.Key == "NoteSaved");
        Assert.Single(sites, site => site.Key == "Canvas");
    }

    [Theory]
    [InlineData("switch event { case .rightPaneHidden: post(.rightPaneShown) }", 1)]
    [InlineData("let events: [A11yEvent] = [.rightPaneShown, .rightPaneShown]", 2)]
    [InlineData(".rightPaneShown", 1)]
    [InlineData("switch event { case .noteSaved(filename: let name): post(.rightPaneShown) }", 1)]
    [InlineData("if case .rightPaneHidden = event { post(.rightPaneShown) }", 1)]
    public void MacInventoryCountsExpressionsAfterPatternsInArraysAndImplicitReturns(string statement, int expected)
    {
        string source = "class Host {\n  func event() -> A11yEvent {\n    " + statement + "\n  }\n}";
        A11yTriggerInventory.Site[] sites = A11yTriggerInventory.MacSites("File.swift", source,
            ["RightPaneShown", "RightPaneHidden", "NoteSaved"]);
        Assert.Equal(expected, sites.Length);
        Assert.All(sites, site =>
        {
            Assert.Equal("RightPaneShown", site.Key);
            Assert.Equal("File.swift#event", site.Member);
        });
    }

    [Fact]
    public void MacInventoryRestoresTheEnclosingOwnerAfterANestedFunctionEnds()
    {
        const string source = """
            class Host {
              func outer() {
                func inner() {
                  post(.rightPaneShown)
                }
                post(.rightPaneShown)
              }
            }
            """;
        A11yTriggerInventory.Site[] sites = A11yTriggerInventory.MacSites("File.swift", source, ["RightPaneShown"]);
        Assert.Equal(["File.swift#inner@1", "File.swift#outer@1"], sites.Select(SiteId));
        string moved = source.Replace("    }\n    post", "    post", StringComparison.Ordinal)
            .Replace("  }\n}", "    }\n  }\n}", StringComparison.Ordinal);
        Assert.Equal(["File.swift#inner@1", "File.swift#inner@2"],
            A11yTriggerInventory.MacSites("File.swift", moved, ["RightPaneShown"]).Select(SiteId));
    }

    [Fact]
    public void MacInventoryFindsNestedTypeMembersAndIgnoresLiteralScopeDelimiters()
    {
        const string source = """
            class Host {
                func earlier() {}
                class Coordinator {
                    func scroll() {
                        let guidance = "} case [ ( {"
                        post(.rightPaneShown)
                    }
                }
            }
            """;
        A11yTriggerInventory.Site site = Assert.Single(
            A11yTriggerInventory.MacSites("File.swift", source, ["RightPaneShown"]));
        Assert.Equal("File.swift#scroll", site.Member);
    }

    private static void AssertDecision(string path, string status)
    {
        Assert.StartsWith("docs/plans/", path);
        Assert.DoesNotContain("..", path, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(SourceText.RepoRoot(), path)), $"Missing recorded decision: {path}");
        Assert.Contains(status, new[] { "recorded", "proposed" });
    }

    [Fact]
    public void SourceInventoryCanBeExportedForTheLedgerGenerator()
    {
        A11yTriggerInventory.Inventory inventory = Inventory.Value;
        Assert.NotEmpty(inventory.Keys);
        Assert.NotEmpty(inventory.Windows);
        Assert.NotEmpty(inventory.Mac);
        if (Environment.GetEnvironmentVariable("SLATE_A11Y_INVENTORY_PATH") is { Length: > 0 } path)
        {
            File.WriteAllText(path, JsonSerializer.Serialize(inventory, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
