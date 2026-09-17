// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis.CSharp;

namespace SlateWindows.Tests.Censuses;

/// <summary>W7-4 (#750): the whole-shell §W-C register. The specialized
/// canvas/graph facts retain their peer and behavioral assertions; these shared
/// checks cover every wave, including their rows.</summary>
[Trait("census", "w-c-matrix")]
public sealed class WcMatrixEvidenceCensus
{
    private static string PlanRoot => Path.Combine(SourceText.RepoRoot(), "docs", "plans", "18_windows_port");
    private sealed record RegisteredId(string Source, string Expression, string Surface);
    private sealed record Row(string Title, string[] Cells);

    private static Row[] Rows() => File.ReadAllLines(Path.Combine(PlanRoot, "w_c_matrix.md"))
        .SkipWhile(line => !line.StartsWith("| Surface |", StringComparison.Ordinal)).Skip(2)
        .TakeWhile(line => !line.StartsWith("##", StringComparison.Ordinal))
        .Where(line => line.StartsWith("| ", StringComparison.Ordinal))
        .Select(line => Regex.Split(line.Trim().Trim('|'), @"(?<!\\)\|").Select(cell => cell.Trim()).ToArray())
        .Select(cells => new Row(cells[0], cells)).ToArray();

    [Fact]
    public void EveryAutomationIdConstructionHasExactlyOneSurface()
    {
        AutomationIdInventory.Site[] actual = AutomationIdInventory.Read();
        RegisteredId[] registered = JsonSerializer.Deserialize<RegisteredId[]>(
            File.ReadAllText(Path.Combine(PlanRoot, "w_c_automation_ids.json")))!;
        var expected = registered.Select(r => new AutomationIdInventory.Site(r.Source, r.Expression)).ToHashSet();
        var missing = actual.Except(expected).ToArray();
        var stale = expected.Except(actual).ToArray();
        Assert.True(missing.Length == 0 && stale.Length == 0,
            "Missing: " + JsonSerializer.Serialize(missing) + "\nStale: " + JsonSerializer.Serialize(stale));
        Assert.Equal(registered.Length, expected.Count);
        var titles = Rows().Select(row => row.Title).ToHashSet(StringComparer.Ordinal);
        foreach (RegisteredId id in registered)
        {
            Assert.True(titles.Contains(id.Surface), $"{id.Source}: {id.Expression} names missing surface {id.Surface}");
        }
    }

    [Fact]
    public void EveryRowHasTenCellsAndExecutableEvidence()
    {
        Row[] rows = Rows();
        Assert.NotEmpty(rows);
        Assert.Equal(rows.Length, rows.Select(row => row.Title).Distinct(StringComparer.Ordinal).Count());
        IReadOnlyList<TestEvidence> projects = TestEvidenceCompilation.Projects;
        var failures = new List<string>();
        foreach (Row row in rows)
        {
            Assert.True(row.Cells.Length == 10, $"{row.Title}: expected ten cells, got {row.Cells.Length}");
            Assert.All(row.Cells, cell => Assert.False(string.IsNullOrWhiteSpace(cell), row.Title));
            string[] names = Regex.Matches(row.Cells[6], "`([^`]+)`").Select(m => m.Groups[1].Value).ToArray();
            if (!names.Any(name => projects.Any(p => p.HasTestEvidence(name))))
            {
                failures.Add($"{row.Title}: no named executable test evidence");
            }
            string[] labels = Regex.Matches(row.Cells[6], @"axe:? ((?:`[^`]+`(?:, )?)+)")
                .SelectMany(m => Regex.Matches(m.Groups[1].Value, "`([^`]+)`").Select(n => n.Groups[1].Value)).ToArray();
            foreach (string name in names)
            {
                if (!projects.Any(p => p.HasTestEvidence(name)) && !labels.Contains(name)
                    && !TestEvidence.HasFixture(Path.Combine(SourceText.RepoRoot(), "crates", "slate-core", "tests", "fixtures"), name))
                {
                    failures.Add($"{row.Title}: `{name}` is not executable test evidence, an axe label or a fixture");
                }
            }
            foreach (string label in labels)
            {
                if (!projects.Any(p => p.HasAxeLabel(label))) { failures.Add($"{row.Title}: no test reaches axe scan {label}"); }
            }
            if (labels.Length == 0 && !HasAxeExemption(row.Cells[6]))
            {
                failures.Add($"{row.Title}: name an axe scan, or record why this row has no dedicated scan");
            }
            for (int i = 7; i < 10; i++) { ValidateHumanCell(row.Title, row.Cells[i], new[] { "Narrator", "NVDA", "JAWS" }[i - 7], PlanRoot, failures); }
        }
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    internal static void ValidateHumanCell(string title, string cell, string at, string documentDirectory, List<string> failures)
    {
        if (cell.StartsWith("Pending", StringComparison.Ordinal)) { return; }
        Match run = Regex.Match(cell, @"\]\(([^)#]+\.md)(?:#[^)]*)?\)");
        string disposition = Regex.IsMatch(cell, @"\bFinding\b", RegexOptions.IgnoreCase) ? "Finding" : "Verified";
        if (!run.Success || !Regex.IsMatch(cell, @"\b" + disposition + @"\b", RegexOptions.IgnoreCase))
        {
            failures.Add($"{title}: human result has no named verification record: {cell}");
            return;
        }
        string path = Path.GetFullPath(Path.Combine(documentDirectory, run.Groups[1].Value));
        if (!path.StartsWith(PlanRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
        {
            failures.Add($"{title}: missing verification record {run.Groups[1].Value}");
            return;
        }
        string record = File.ReadAllText(path);
        foreach (string field in new[] { "Tester", "AT", "OS", "Build", "Corpus", "Method" })
        {
            Match value = Regex.Match(record, @"\*\*" + field + @":\*\*\s*([^·\r\n]+)");
            if (!value.Success || string.IsNullOrWhiteSpace(value.Groups[1].Value)
                || value.Groups[1].Value.Contains("Pending", StringComparison.OrdinalIgnoreCase))
            {
                failures.Add($"{title}: verification record has no completed {field}");
            }
        }
        Match recordedAt = Regex.Match(record, @"\*\*AT:\*\*\s*([^·\r\n]+)");
        if (!recordedAt.Success || !Regex.IsMatch(recordedAt.Groups[1].Value, @"\b" + at + @"\b"))
        {
            failures.Add($"{title}: the {at} cell references a different AT's run");
        }
        if (!Regex.IsMatch(record, @"\b20\d{2}-\d{2}-\d{2}\b")) { failures.Add($"{title}: verification record has no date"); }
        if (!Regex.IsMatch(record, @"\b[0-9a-f]{7,40}\b")) { failures.Add($"{title}: verification record has no commit"); }
        // The one pre-existing field record predates the checklist table.
        // Its exception is limited to its actual surface and reader.
        bool historicalReading = path == Path.Combine(PlanRoot, "reports", "w3_1_nvda_field_verification.md")
            && title == "Reading view (W3-1)" && at == "NVDA" && disposition == "Verified";
        if (!historicalReading && !HasSubjectResult(record, title, disposition))
        {
            failures.Add($"{title}: the run does not record this specific surface/checklist item with an observation and transcript");
        }
    }

    private static bool HasSubjectResult(string record, string title, string disposition) =>
        record.Split('\n').Where(line => line.StartsWith("| ", StringComparison.Ordinal))
            .Select(line => line.Trim().Trim('|').Split('|').Select(value => value.Trim()).ToArray())
            .Any(cells => cells.Length == 5 && cells[0] == title && cells[1] == disposition
                && cells[2].Length > 0 && !cells[2].Contains("Pending", StringComparison.OrdinalIgnoreCase)
                && Regex.IsMatch(cells[3], @"\[[^\]]+\]\([^)]+\)")
                && (disposition != "Finding" || Regex.IsMatch(cells[4], @"\[[^\]]+\]\([^)]+\)")));

    [Theory]
    [InlineData("Finding", "Finding", "[issue](https://github.com/coryj627/slate/issues/750)", true)]
    [InlineData("Finding", "Finding", "Pending", false)]
    [InlineData("Finding", "Verified", "[issue](https://github.com/coryj627/slate/issues/750)", false)]
    [InlineData("Verified", "Verified", "None", true)]
    public void ARecordedFindingRemainsAFinding(string recorded, string claimed, string finding, bool expected)
    {
        string record = $"| subject | {recorded} | Actual observed speech | [transcript](run.txt) | {finding} |";
        Assert.Equal(expected, HasSubjectResult(record, "subject", claimed));
        Assert.False(HasSubjectResult(record, "unrelated surface", claimed));
    }

    [Fact]
    public void ChecklistRowsHaveSpecRoutesEvidenceAndUninventedHumanResults()
    {
        foreach (string wave in new[] { "w1_shell", "w2_editor", "w3_content", "w4_panels", "w5_commands" })
        {
            string text = File.ReadAllText(Path.Combine(PlanRoot, "reports", wave + "_at_checklist.md"));
            var rows = text.Split('\n').Where(line => Regex.IsMatch(line, @"^\| \d+ \|"))
                .Select(line => line.Trim().Trim('|').Split('|').Select(cell => cell.Trim()).ToArray()).ToArray();
            Assert.NotEmpty(rows);
            var failures = new List<string>();
            foreach (string[] row in rows)
            {
                Assert.Equal(9, row.Length);
                Assert.All(row, cell => Assert.False(string.IsNullOrWhiteSpace(cell)));
                MatchCollection twins = Regex.Matches(row[5], "`([^`]+)`");
                Assert.NotEmpty(twins);
                foreach (Match token in twins)
                {
                    Assert.True(TestEvidenceCompilation.Projects.Any(p => p.HasTestEvidence(token.Groups[1].Value)),
                        $"{wave}: no executable automated twin {token.Groups[1].Value}");
                }
                for (int i = 6; i < 9; i++) { ValidateHumanCell(wave + "_at_checklist.md#" + row[0], row[i], new[] { "Narrator", "NVDA", "JAWS" }[i - 6], Path.Combine(PlanRoot, "reports"), failures); }
            }
            Assert.True(failures.Count == 0, string.Join("\n", failures));
        }
    }

    [Theory]
    [InlineData("Verified", "NVDA")]
    [InlineData("Verified [record](reports/missing_run.md)", "NVDA")]
    [InlineData("Verified [record](reports/w3_1_nvda_field_verification.md)", "JAWS")]
    [InlineData("Verified [record](reports/w3_1_nvda_field_verification.md)", "NVDA")]
    [InlineData("Verified [record](reports/_at_pass_template.md)", "NVDA")]
    public void HumanCellsRejectUnrecordedRunsAndTheOtherReadersEvidence(string cell, string at)
    {
        var failures = new List<string>();
        ValidateHumanCell("test surface", cell, at, PlanRoot, failures);
        Assert.NotEmpty(failures);
    }

    [Fact]
    public void IdInventorySeesHelpersPrefixesAndOverridesButIgnoresDeadText()
    {
        string source = """
            class View {
                void Label(string id) { AutomationProperties.SetAutomationId(this, id + "Value"); }
                void Build() { var setter = new Setter(AutomationProperties.AutomationIdProperty, new Binding("Id")); Label("Meter"); AutomationProperties.SetAutomationId(this, "Node:" + key); }
                protected override string GetAutomationIdCore() => "Peer:" + key;
                // AutomationProperties.SetAutomationId(this, "Comment");
                string text = "SetAutomationId(this, Fake)";
            }
            """;
        string[] sites = [.. AutomationIdInventory.CSharpExpressions(CSharpSyntaxTree.ParseText(source).GetRoot())];
        Assert.Contains("\"Meter\"", sites);
        Assert.Contains("id + \"Value\"", sites);
        Assert.Contains("\"Node:\" + key", sites);
        Assert.Contains("\"Peer:\" + key", sites);
        Assert.Contains("new Binding(\"Id\")", sites);
        Assert.Equal(5, sites.Length);
    }

    [Theory]
    [InlineData("One")]
    [InlineData("Two")]
    public void IdInventoryTracksTheValueOfReorderedNamedArguments(string id)
    {
        string source = $$"""
            class View {
                void Build() {
                    AutomationProperties.SetAutomationId(value: "{{id}}", element: this);
                    var setter = new Setter(value: "{{id}}", property: AutomationProperties.AutomationIdProperty);
                }
            }
            """;
        string[] sites = [.. AutomationIdInventory.CSharpExpressions(CSharpSyntaxTree.ParseText(source).GetRoot())];
        Assert.Equal(new[] { "\"" + id + "\"", "\"" + id + "\"" }, sites);
    }

    [Fact]
    public void HelperInputsBelongToTheBoundMethodNotAnotherTypesNamesake()
    {
        string source = """
            class View {
                void Label(string id) { AutomationProperties.SetAutomationId(this, id); }
                void Build() { Label("Actual"); new Other().Label("NotAnId"); }
            }
            class Other { public void Label(string id) { } }
            """;
        string[] sites = [.. AutomationIdInventory.CSharpExpressions(CSharpSyntaxTree.ParseText(source).GetRoot())];
        Assert.Contains("\"Actual\"", sites);
        Assert.DoesNotContain("\"NotAnId\"", sites);
    }

    private static bool HasAxeExemption(string cell) => Regex.IsMatch(cell, @"\baxe:\s*none\b\s*(?:[—:;-]\s*)?\p{L}.+");

    [Theory]
    [InlineData("axe: none — the popup has its own HWND", true)]
    [InlineData("axe: none; the popup has its own HWND", true)]
    [InlineData("axe: none because the popup has its own HWND", true)]
    [InlineData("axe: none", false)]
    [InlineData("axe: none —", false)]
    public void AxeExemptionsNeedAReasonButNotOnePunctuationStyle(string cell, bool expected) =>
        Assert.Equal(expected, HasAxeExemption(cell));
}
