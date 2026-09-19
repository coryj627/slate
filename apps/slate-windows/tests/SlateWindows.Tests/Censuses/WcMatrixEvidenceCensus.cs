// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace SlateWindows.Tests.Censuses;

/// <summary>W7-4 (#750): the whole-shell §W-C register. The specialized
/// canvas/graph facts retain their peer and behavioral assertions; these shared
/// checks cover every wave, including their rows.</summary>
[Trait("census", "w-c-matrix")]
public sealed class WcMatrixEvidenceCensus
{
    private static readonly string PlanRoot = Path.Combine(SourceText.RepoRoot(), "docs", "plans", "18_windows_port");
    private static readonly string FixtureRoot = Path.Combine(SourceText.RepoRoot(), "crates", "slate-core", "tests", "fixtures");
    private sealed record RegisteredId(string Source, string Expression, string Surface);
    private sealed record Row(string Title, string[] Cells);

    private static Row[] Rows() => MatrixRows().Select(cells => new Row(cells[0], cells)).ToArray();

    /// <summary>The §W-C table's rows as cells, split on unescaped pipes:
    /// the one parser the canvas and graph censuses share, so no row counts
    /// ten cells here and eleven there.</summary>
    internal static string[][] MatrixRows(string titlePrefix = "") => File.ReadAllLines(Path.Combine(PlanRoot, "w_c_matrix.md"))
        .SkipWhile(line => !line.StartsWith("| Surface |", StringComparison.Ordinal)).Skip(2)
        .TakeWhile(line => !line.StartsWith("##", StringComparison.Ordinal))
        .Where(line => line.StartsWith("| " + titlePrefix, StringComparison.Ordinal))
        .Select(SplitCells).ToArray();

    internal static string[] SplitCells(string line) =>
        Regex.Split(line.Trim().Trim('|'), @"(?<!\\)\|").Select(cell => cell.Trim()).ToArray();

    [Fact]
    public void MatrixCellsSplitOnUnescapedPipesOnly() =>
        Assert.Equal(new[] { @"A \| B", "c" }, SplitCells(@"| A \| B | c |"));

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
            var executable = names.Where(name => projects.Any(p => p.HasTestEvidence(name))).ToHashSet(StringComparer.Ordinal);
            if (executable.Count == 0)
            {
                failures.Add($"{row.Title}: no named executable test evidence");
            }
            string[] labels = Regex.Matches(row.Cells[6], @"axe:? ((?:`[^`]+`(?:, )?)+)")
                .SelectMany(m => Regex.Matches(m.Groups[1].Value, "`([^`]+)`").Select(n => n.Groups[1].Value)).ToArray();
            foreach (string name in names)
            {
                if (!executable.Contains(name) && !labels.Contains(name) && !TestEvidence.HasFixture(FixtureRoot, name))
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
        if (IsPending(cell)) { return; }
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
        // The one pre-existing field record predates the template and its
        // checklist table. Its exception is limited to its actual surface and reader.
        bool historicalReading = path == Path.Combine(PlanRoot, "reports", "w3_1_nvda_field_verification.md")
            && title == "Reading view (W3-1)" && at == "NVDA" && disposition == "Verified";
        ValidateRecord(title, at, disposition, record, Path.GetDirectoryName(path)!, historicalReading, failures);
    }

    /// <summary>The record's own fields, not text anywhere in it: the
    /// template's Run date carries the date, its Build the commit, and the
    /// subject row's transcript link resolves to a file beside the record.</summary>
    internal static void ValidateRecord(string title, string at, string disposition, string record, string recordDirectory,
        bool historicalReading, List<string> failures)
    {
        string[] required = historicalReading
            ? ["Tester", "AT", "OS", "Build", "Corpus", "Method"]
            : ["Tester", "AT", "OS", "Build", "Corpus", "Method", "Run date", "Evidence reference"];
        foreach (string field in required)
        {
            if (Field(record, field) is not { } value || value.Contains("Pending", StringComparison.OrdinalIgnoreCase))
            {
                failures.Add($"{title}: verification record has no completed {field}");
            }
        }
        if (Field(record, "AT") is not { } recordedAt || !Regex.IsMatch(recordedAt, @"\b" + at + @"\b"))
        {
            failures.Add($"{title}: the {at} cell references a different AT's run");
        }
        if (historicalReading)
        {
            if (!Regex.IsMatch(record, @"\b20\d{2}-\d{2}-\d{2}\b")) { failures.Add($"{title}: verification record has no date"); }
            if (!Regex.IsMatch(record, @"\b[0-9a-f]{7,40}\b")) { failures.Add($"{title}: verification record has no commit"); }
            return;
        }
        if (!Regex.IsMatch(Field(record, "Run date") ?? "", @"^\d{4}-\d{2}-\d{2}\b"))
        {
            failures.Add($"{title}: the record's Run date is not a date");
        }
        // A commit is seven to forty hex digits with a digit among them; a
        // word such as "defaced" is not one.
        if (!Regex.IsMatch(Field(record, "Build") ?? "", @"\b(?=[0-9a-f]*\d)[0-9a-f]{7,40}\b"))
        {
            failures.Add($"{title}: the record's Build names no commit");
        }
        if (SubjectTranscript(record, title, disposition) is not { } transcript)
        {
            failures.Add($"{title}: the run does not record this specific surface/checklist item with an observation and transcript");
            return;
        }
        if (!IsFileBeside(recordDirectory, transcript))
        {
            failures.Add($"{title}: transcript {transcript} is not a file beside the record");
        }
    }

    private static bool IsFileBeside(string directory, string relative)
    {
        try
        {
            string root = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string path = Path.GetFullPath(Path.Combine(root, relative));
            return path.StartsWith(root, StringComparison.OrdinalIgnoreCase) && File.Exists(path);
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or NotSupportedException)
        {
            return false;
        }
    }

    private static string? Field(string record, string field)
    {
        Match value = Regex.Match(record, @"\*\*" + Regex.Escape(field) + @":\*\*\s*([^·\r\n]+)");
        return value.Success && !string.IsNullOrWhiteSpace(value.Groups[1].Value) ? value.Groups[1].Value.Trim() : null;
    }

    private static bool HasSubjectResult(string record, string title, string disposition) =>
        SubjectTranscript(record, title, disposition) is not null;

    /// <summary>The subject row's transcript link target: the row names the
    /// surface (a checklist key may be backticked), the claimed disposition,
    /// an observation and a linked transcript — and, for a finding, a linked
    /// issue or retest.</summary>
    private static string? SubjectTranscript(string record, string title, string disposition) =>
        record.Split('\n').Where(line => line.StartsWith("| ", StringComparison.Ordinal))
            .Select(line => line.Trim().Trim('|').Split('|').Select(value => value.Trim()).ToArray())
            .Where(cells => cells.Length == 5 && cells[0].Trim('`') == title && cells[1] == disposition
                && cells[2].Length > 0 && !cells[2].Contains("Pending", StringComparison.OrdinalIgnoreCase)
                && (disposition != "Finding" || Regex.IsMatch(cells[4], @"\[[^\]]+\]\([^)]+\)")))
            .Select(cells => Regex.Match(cells[3], @"\[[^\]]+\]\(([^)]+)\)"))
            .Where(link => link.Success).Select(link => link.Groups[1].Value).FirstOrDefault();

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
        foreach (string wave in new[] { "w1_shell", "w2_editor", "w3_content", "w4_panels", "w5_commands", "w6_1_canvas", "w6_2_graph" })
        {
            string text = File.ReadAllText(Path.Combine(PlanRoot, "reports", wave + "_at_checklist.md"));
            var rows = text.Split('\n').Where(line => Regex.IsMatch(line, @"^\| \d+ \|"))
                .Select(line => line.Trim().Trim('|').Split('|').Select(cell => cell.Trim()).ToArray()).ToArray();
            Assert.NotEmpty(rows);
            var failures = new List<string>();
            bool anyRun = rows.Any(row => row.Skip(6).Any(cell => !IsPending(cell)));
            string tester = Regex.Match(text, @"\*\*Tester:\*\*\s*([^·\r\n]+)").Groups[1].Value.Trim();
            string runDate = Regex.Match(text, @"\*\*Run date:\*\*\s*([^·\r\n]+)").Groups[1].Value.Trim();
            Assert.True(ChecklistHeaderMatchesRuns(tester, runDate, anyRun), $"{wave}: Tester / Run date disagree with the human cells");
            foreach (string[] row in rows)
            {
                Assert.Equal(9, row.Length);
                Assert.All(row, cell => Assert.False(string.IsNullOrWhiteSpace(cell)));
                // The automated twin is named, or the row says "none — human
                // only" in those words; every backticked name still binds.
                MatchCollection twins = Regex.Matches(row[5], "`([^`]+)`");
                Assert.True(twins.Count > 0 || row[5].StartsWith("none — human only", StringComparison.Ordinal),
                    $"{wave}#{row[0]}: no automated twin and no \"none — human only\"");
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

    private const string CompleteRecord = """
        **Tester:** Cory Joseph
        **AT:** NVDA 2026.1
        **OS:** Windows 11 Pro 26200
        **Build:** main, verified commit 4e09b68a, Release executable
        **Corpus:** fixture vault
        **Method:** Speech Viewer
        **Run date:** 2026-09-18 (UTC)
        **Evidence reference:** run.txt

        | Matrix surface / checklist item | Result | Heard/observed behavior | Transcript reference | Finding / retest |
        |---|---|---|---|---|
        | subject | Verified | heard the result count | [transcript](run.txt) | None |
        """;

    private static List<string> RecordFailures(string record, string recordDirectory, string title = "subject")
    {
        var failures = new List<string>();
        ValidateRecord(title, "NVDA", "Verified", record, recordDirectory, historicalReading: false, failures);
        return failures;
    }

    [Fact]
    public void ACompleteRecordWithItsTranscriptBesideItPasses()
    {
        using var directory = FixtureVault.Create(0, "at-record");
        File.WriteAllText(Path.Combine(directory.Root, "run.txt"), "speech");
        Assert.Empty(RecordFailures(CompleteRecord, directory.Root));
    }

    [Fact]
    public void AMissingTranscriptFileIsNotEvidence()
    {
        using var directory = FixtureVault.Create(0, "at-record");
        Assert.Contains(RecordFailures(CompleteRecord, directory.Root), failure => failure.Contains("run.txt", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("**Run date:** 2026-09-18 (UTC)", "**Run date:** Pending (date and time zone)")]
    [InlineData("**Build:** main, verified commit 4e09b68a, Release executable", "**Build:** main, verified commit defaced, Release executable")]
    [InlineData("**Evidence reference:** run.txt", "**Evidence reference:** Pending (transcript path beside this record)")]
    public void TheTemplatesOwnFieldsCarryTheDateAndTheCommit(string field, string replacement)
    {
        using var directory = FixtureVault.Create(0, "at-record");
        File.WriteAllText(Path.Combine(directory.Root, "run.txt"), "speech");
        // A date or a hex-looking word elsewhere in the record does not stand in for the field.
        string record = CompleteRecord.Replace(field, replacement, StringComparison.Ordinal)
            + "\nThe method prose mentions 2026-09-17 and the word defaced.\n";
        Assert.NotEmpty(RecordFailures(record, directory.Root));
    }

    [Fact]
    public void AChecklistKeyMayBeBackticked()
    {
        using var directory = FixtureVault.Create(0, "at-record");
        File.WriteAllText(Path.Combine(directory.Root, "run.txt"), "speech");
        string record = CompleteRecord.Replace("| subject |", "| `w1_shell_at_checklist.md#1` |", StringComparison.Ordinal);
        Assert.Empty(RecordFailures(record, directory.Root, title: "w1_shell_at_checklist.md#1"));
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
        string[] sites = Inventory(source);
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
        string[] sites = Inventory(source);
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
        string[] sites = Inventory(source);
        Assert.Contains("\"Actual\"", sites);
        Assert.DoesNotContain("\"NotAnId\"", sites);
    }

    [Fact]
    public void MalformedInventorySourceFailsWithAnExplicitDiagnostic() =>
        Assert.Throws<ArgumentException>(() => Inventory("class View { void (string id) {"));

    [Fact]
    public void ParserWarningsDoNotStopTheInventory()
    {
        string[] sites = Inventory("#warning retire after W8\nclass View { void Build() { AutomationProperties.SetAutomationId(this, \"Warned\"); } }");
        Assert.Equal(new[] { "\"Warned\"" }, sites);
    }

    [Fact]
    public void IdInventoryReadsTheBackingFieldsDefaultRootButNotItsSetterEcho()
    {
        string source = """
            class View {
                private string _automationIdRoot = "Dash";
                public string AutomationIdRoot { get => _automationIdRoot; set { _automationIdRoot = value; AutomationProperties.SetAutomationId(this, value + "Surface"); } }
            }
            """;
        string[] sites = Inventory(source);
        Assert.Contains("\"Dash\"", sites);
        Assert.Contains("value + \"Surface\"", sites);
        Assert.DoesNotContain("value", sites);
    }

    [Fact]
    public void AnUnboundHelperCallFailsInsteadOfSkippingItsInput()
    {
        string source = """
            class View {
                void Notice(object element, string id) { AutomationProperties.SetAutomationId(element, id); }
                void Build() { Missing.Notice(this, "Skipped"); }
            }
            """;
        Assert.Throws<InvalidOperationException>(() => Inventory(source));
    }

    [Fact]
    public void HelperInputsAreInventoriedAcrossFiles()
    {
        SyntaxTree host = CSharpSyntaxTree.ParseText("""
            static class Host { internal static void Notice(object element, string id) { AutomationProperties.SetAutomationId(element, id); } }
            """);
        SyntaxTree caller = CSharpSyntaxTree.ParseText("""
            class View { void Build() { Host.Notice(this, "CrossFile"); } }
            """);
        CSharpCompilation compilation = CSharpCompilation.Create("AutomationIdFixture", syntaxTrees: [host, caller],
            references: [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
        var helpers = AutomationIdInventory.HelperParameters(
            [(host.GetRoot(), compilation.GetSemanticModel(host)), (caller.GetRoot(), compilation.GetSemanticModel(caller))]);
        Assert.Contains("\"CrossFile\"", AutomationIdInventory.CSharpExpressions(caller.GetRoot(), compilation.GetSemanticModel(caller), helpers));
    }

    [Fact]
    public void HelperInputsAreInventoriedThroughASecondLevelHelper()
    {
        string source = """
            class View {
                void Notice(object element, string id) { AutomationProperties.SetAutomationId(element, id); }
                void Row(string id) => Notice(this, id);
                void Build() { Row("TwoLevel"); }
            }
            """;
        Assert.Contains("\"TwoLevel\"", Inventory(source));
    }

    [Fact]
    public void XamlNamesWithoutAnExplicitIdAreRuntimeIds()
    {
        XDocument document = XDocument.Parse("""
            <Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
              <Menu x:Name="MainMenu" />
              <Button x:Name="Named" AutomationProperties.AutomationId="Explicit" />
              <TextBlock Name="Plain" />
              <Style><Setter Property="AutomationProperties.AutomationId" Value="Styled" /></Style>
            </Window>
            """);
        Assert.Equal(new[] { "Explicit", "MainMenu", "Plain", "Styled" },
            AutomationIdInventory.XamlExpressions(document).Order(StringComparer.Ordinal));
    }

    private static string[] Inventory(string source)
    {
        SyntaxTree tree = CSharpSyntaxTree.ParseText(source);
        CSharpCompilation compilation = CSharpCompilation.Create("AutomationIdFixture", syntaxTrees: [tree],
            references: [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
        return [.. AutomationIdInventory.CSharpExpressions(tree.GetRoot(), compilation.GetSemanticModel(tree))];
    }

    internal static bool IsPending(string value) => value.StartsWith("Pending", StringComparison.OrdinalIgnoreCase);

    internal static bool ChecklistHeaderMatchesRuns(string tester, string runDate, bool anyRun) => anyRun
        ? !string.IsNullOrWhiteSpace(tester) && !tester.Contains("Pending", StringComparison.OrdinalIgnoreCase)
            && Regex.IsMatch(runDate, @"^\d{4}-\d{2}-\d{2}\b")
        : IsPending(tester) && IsPending(runDate);

    [Theory]
    [InlineData("Pending", "Pending", false, true)]
    [InlineData("pending", "PENDING", false, true)]
    [InlineData("Pending", "Pending", true, false)]
    [InlineData("Cory Joseph", "Pending", true, false)]
    [InlineData("", "2026-09-17", true, false)]
    [InlineData("Cory Joseph", "2026-09-17", true, true)]
    public void ChecklistHeadersTrackWhetherAnyHumanRunIsRecorded(string tester, string date, bool anyRun, bool expected) =>
        Assert.Equal(expected, ChecklistHeaderMatchesRuns(tester, date, anyRun));

    [Theory]
    [InlineData("Pending")]
    [InlineData("pending")]
    [InlineData("PENDING (scheduled)")]
    public void PendingHumanCellsAreCaseInsensitiveAndNeverCountAsARun(string cell)
    {
        Assert.True(IsPending(cell));
        var failures = new List<string>();
        ValidateHumanCell("unexecuted", cell, "NVDA", PlanRoot, failures);
        Assert.Empty(failures);
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
