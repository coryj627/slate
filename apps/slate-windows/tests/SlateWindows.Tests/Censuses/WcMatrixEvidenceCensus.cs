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
            if (labels.Length == 0 && !row.Cells[6].Contains("axe: none —", StringComparison.Ordinal))
            {
                failures.Add($"{row.Title}: name an axe scan, or record why this row has no dedicated scan");
            }
            foreach (string pattern in Enum.GetNames<System.Windows.Automation.Peers.PatternInterface>())
            {
                if (!Regex.IsMatch(row.Cells[3], @"\b" + pattern + @"\b")
                    || Regex.IsMatch(row.Cells[3], @"\b" + pattern + @"\s+n/a\b")) { continue; }
                if (!names.Any(name => projects.Any(project => project.HasPatternEvidence(name, pattern))))
                {
                    failures.Add($"{row.Title}: claimed {pattern} has no positive assertion or invocation in its named evidence");
                }
            }
            for (int i = 7; i < 10; i++) { ValidateHumanCell(row.Title, row.Cells[i], new[] { "Narrator", "NVDA", "JAWS" }[i - 7], failures); }
        }
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    private static void ValidateHumanCell(string title, string cell, string at, List<string> failures)
    {
        if (cell.StartsWith("Pending", StringComparison.Ordinal)) { return; }
        Match run = Regex.Match(cell, @"\]\((reports/[^)#]+\.md)(?:#[^)]*)?\)");
        if (!run.Success || !cell.Contains("verified", StringComparison.OrdinalIgnoreCase))
        {
            failures.Add($"{title}: human result has no named verification record: {cell}");
            return;
        }
        string path = Path.GetFullPath(Path.Combine(PlanRoot, run.Groups[1].Value));
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
                foreach (Match token in Regex.Matches(row[5], "`([^`]+)`"))
                {
                    Assert.True(TestEvidenceCompilation.Projects.Any(p => p.HasTestEvidence(token.Groups[1].Value)),
                        $"{wave}: no executable automated twin {token.Groups[1].Value}");
                }
                for (int i = 6; i < 9; i++) { ValidateHumanCell(wave + " " + row[0], row[i], new[] { "Narrator", "NVDA", "JAWS" }[i - 6], failures); }
            }
            Assert.True(failures.Count == 0, string.Join("\n", failures));
        }
    }

    [Theory]
    [InlineData("Verified", "NVDA")]
    [InlineData("Verified [record](reports/missing_run.md)", "NVDA")]
    [InlineData("Verified [record](reports/w3_1_nvda_field_verification.md)", "JAWS")]
    [InlineData("Verified [record](reports/_at_pass_template.md)", "NVDA")]
    public void HumanCellsRejectUnrecordedRunsAndTheOtherReadersEvidence(string cell, string at)
    {
        var failures = new List<string>();
        ValidateHumanCell("test surface", cell, at, failures);
        Assert.NotEmpty(failures);
    }

    [Fact]
    public void IdInventorySeesHelpersPrefixesAndOverridesButIgnoresDeadText()
    {
        string source = """
            class View {
                void Label(string id) { AutomationProperties.SetAutomationId(this, id + "Value"); }
                void Build() { Label("Meter"); AutomationProperties.SetAutomationId(this, "Node:" + key); }
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
        Assert.Equal(4, sites.Length);
    }
}
