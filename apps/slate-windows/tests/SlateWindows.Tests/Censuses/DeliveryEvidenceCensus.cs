// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text.Json;
using System.Text.RegularExpressions;

namespace SlateWindows.Tests.Censuses;

/// <summary>
/// W6-1 §H TH-9 (H7, IH-18, IH-19, IH-45): the C# mirror of the two
/// validations PR H added to <c>scripts/generate-parity-matrix.py</c>
/// (13 and 14), so the delivery evidence in <c>chords.json</c> is
/// checked on every test run and not only when the matrix is
/// regenerated. Change both implementations together, never one.
/// (13) A marker into a C# file names a DECLARATION — a type, a method,
/// a constructor, a property, an event or a field — not a substring
/// that could live in a comment or an unrelated call; a test anchor may
/// instead name an automation id the journey drives as a quoted string.
/// (14) An issue's group is SCOPE-COMPLETE: for every command group any
/// of the issue's commands maps to, the issue's group carries one of
/// that group's implementation anchors and one of its test anchors. And
/// the canvas issue maps to the aggregate that spans all five canvas
/// command groups.
/// </summary>
[Trait("census", "delivery-evidence")]
public sealed class DeliveryEvidenceCensus
{
    private static string RepoRoot => SourceText.RepoRoot();

    private static readonly Regex TypeHead = new(@"\b(?:class|record|interface|enum|struct)[ \t]+(?<name>\w+)\b", RegexOptions.Compiled);

    private static readonly Regex DeclarationHead = new(
        @"^[ \t]*(?:\[[^\]]*\][ \t]*)*(?:public|internal|private|protected)\b[^;{=(]*?\b(?<name>\w+)[ \t]*(?:\(|\{|=>|=|;|\r?$)",
        RegexOptions.Compiled | RegexOptions.Multiline);

    private static JsonElement Evidence()
    {
        using var doc = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(RepoRoot, "apps", "slate-windows", "chords.json")));
        return doc.RootElement.GetProperty("deliveryEvidence").Clone();
    }

    /// <summary>Comments and string literals are not declaration sites
    /// (review round 1, IH-58): the generator strips them before it reads
    /// heads, and so does this mirror.</summary>
    private static readonly Regex CodeOnly = new(
        "//[^\n]*|/\\*.*?\\*/|\"(?:[^\"\\\\\n]|\\\\.)*\"",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private static bool Declares(string text, string marker)
    {
        string code = CodeOnly.Replace(text, "");
        return TypeHead.Matches(code).Any(m => m.Groups["name"].Value == marker)
            || DeclarationHead.Matches(code).Any(m => m.Groups["name"].Value == marker);
    }

    [Fact]
    public void EveryCSharpMarkerIsADeclarationOrAJourneysAutomationId()
    {
        JsonElement evidence = Evidence();
        var failures = new List<string>();
        foreach (JsonProperty group in evidence.GetProperty("groups").EnumerateObject())
        {
            foreach (string kind in (string[])["implementation", "tests"])
            {
                foreach (JsonElement reference in group.Value.GetProperty(kind).EnumerateArray())
                {
                    string value = reference.GetString()!;
                    string[] parts = value.Split('#', 2);
                    string path = Path.Combine(RepoRoot, parts[0].Replace('/', Path.DirectorySeparatorChar));
                    if (!File.Exists(path))
                    {
                        failures.Add($"{group.Name}/{kind}: no file {parts[0]}");
                        continue;
                    }
                    if (!parts[0].EndsWith(".cs", StringComparison.Ordinal))
                    {
                        continue;
                    }
                    string text = File.ReadAllText(path);
                    bool ok = Declares(text, parts[1]) || (kind == "tests" && text.Contains($"\"{parts[1]}\"", StringComparison.Ordinal));
                    if (!ok)
                    {
                        failures.Add($"{group.Name}/{kind}: {value} is not a declaration (nor a journey's quoted id)");
                    }
                }
            }
        }
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    [Fact]
    public void EveryIssueGroupIsScopeCompleteOverItsCommandGroups()
    {
        JsonElement evidence = Evidence();
        JsonElement groups = evidence.GetProperty("groups");
        JsonElement commands = evidence.GetProperty("commands");
        var failures = new List<string>();
        // the issue each command belongs to is the chord table's, read from the
        // generated matrix's rows: `| `id` | label | chord | spoken | #nnn (…) | status |`
        string matrix = File.ReadAllText(Path.Combine(RepoRoot, "docs", "plans", "18_windows_port", "parity_matrix.md"));
        var issueOfCommand = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match m in Regex.Matches(matrix, @"^\| `(slate\.[\w.]+)` \| [^|]* \| [^|]* \| [^|]* \| (#\d+)", RegexOptions.Multiline))
        {
            issueOfCommand[m.Groups[1].Value] = m.Groups[2].Value;
        }
        foreach (JsonProperty issue in evidence.GetProperty("issues").EnumerateObject())
        {
            string issueGroup = issue.Value.GetString()!;
            Assert.True(groups.TryGetProperty(issueGroup, out JsonElement aggregate), $"{issue.Name} names no group {issueGroup}");
            var commandGroups = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonProperty command in commands.EnumerateObject())
            {
                if (issueOfCommand.TryGetValue(command.Name, out string? of) && of == issue.Name)
                {
                    commandGroups.Add(command.Value.GetString()!);
                }
            }
            commandGroups.Remove(issueGroup);
            foreach (string commandGroup in commandGroups.OrderBy(g => g, StringComparer.Ordinal))
            {
                foreach (string kind in (string[])["implementation", "tests"])
                {
                    var own = aggregate.GetProperty(kind).EnumerateArray().Select(e => e.GetString()!).ToHashSet(StringComparer.Ordinal);
                    var theirs = groups.GetProperty(commandGroup).GetProperty(kind).EnumerateArray().Select(e => e.GetString()!);
                    if (!theirs.Any(own.Contains))
                    {
                        failures.Add($"{issue.Name} group {issueGroup} carries no {kind} anchor of command group {commandGroup}");
                    }
                }
            }
        }
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    /// <summary>W6-2 PR B (B-20): every command the parity matrix records
    /// as implemented maps to a command group — the scope-completeness
    /// check above walks the issues' commands, so a delivered id that was
    /// never added to the map went unseen (the mutation sweep found it).</summary>
    [Fact]
    public void EveryImplementedCommandMapsToACommandGroup()
    {
        JsonElement commands = Evidence().GetProperty("commands");
        string matrix = File.ReadAllText(Path.Combine(RepoRoot, "docs", "plans", "18_windows_port", "parity_matrix.md"));
        var failures = new List<string>();
        int implemented = 0;
        foreach (Match m in Regex.Matches(matrix, @"^\| `(slate\.[\w.]+)` \| [^|]* \| [^|]* \| [^|]* \| #\d+[^|]* \| (implemented[^|]*)\|", RegexOptions.Multiline))
        {
            implemented++;
            if (!commands.TryGetProperty(m.Groups[1].Value, out _))
            {
                failures.Add($"{m.Groups[1].Value} is implemented ({m.Groups[2].Value.Trim()}) but maps to no command group");
            }
        }
        Assert.True(implemented > 100, $"the matrix parse found only {implemented} implemented rows — the row shape moved");
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    [Fact]
    public void TheCanvasIssueMapsToTheAggregateOverAllFiveCommandGroups()
    {
        JsonElement evidence = Evidence();
        Assert.Equal("canvas", evidence.GetProperty("issues").GetProperty("#745").GetString());
        JsonElement aggregate = evidence.GetProperty("groups").GetProperty("canvas");
        var implementation = aggregate.GetProperty("implementation").EnumerateArray().Select(e => e.GetString()!).ToHashSet(StringComparer.Ordinal);
        var tests = aggregate.GetProperty("tests").EnumerateArray().Select(e => e.GetString()!).ToHashSet(StringComparer.Ordinal);
        foreach (string commandGroup in (string[])["canvasSurfaces", "canvasNavigator", "canvasMutations", "canvasModes", "canvasMarks"])
        {
            JsonElement group = evidence.GetProperty("groups").GetProperty(commandGroup);
            Assert.Contains(group.GetProperty("implementation").EnumerateArray().Select(e => e.GetString()!), implementation.Contains);
            Assert.Contains(group.GetProperty("tests").EnumerateArray().Select(e => e.GetString()!), tests.Contains);
        }
        // the close-out's own gates ride the aggregate too
        Assert.Contains(tests, t => t.EndsWith("#OpenSampleExposesOutlineTableAndScene", StringComparison.Ordinal));
        Assert.Contains(tests, t => t.EndsWith("#TheLedgerHasARowForEveryStructuralKeyAndNoOther", StringComparison.Ordinal));
        Assert.Contains(tests, t => t.EndsWith("#EveryManifestSurfaceIsARowOfTenCellsAndNoCanvasRowIsUnknown", StringComparison.Ordinal));
    }
}
