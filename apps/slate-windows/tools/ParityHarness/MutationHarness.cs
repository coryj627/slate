// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// W5-4 (#744) H1-H6: the mutation mode of the §W-A differential
// harness. The scenario scripts are DATA
// (crates/slate-core/tests/fixtures/mutation_golden/scenarios.json),
// shared verbatim with the Swift twin, so identical sequences on both
// platforms are enforced by construction. Each scenario builds its own
// deterministic fixture vault from the seed set, drives the op list,
// and serializes one canonical artifact: checkpoints (tree snapshots),
// normalized reports, typed refusals, the terminal tree, per-file
// op-log projections, and the read-harness links artifact re-emitted
// over the terminal vault.
//
// Driver-enforced invariants (H5): assertEqual checkpoint pairs are
// byte-identical trees (1/3/7); 'untouched' paths never change across
// checkpoints (1); an op with 'expect' MUST refuse with that typed
// kind and an op without one MUST NOT throw; 'terminalTreeMatches'
// pins S2's idempotence (6). Referential integrity (4) and op-log
// consistency (5) are pinned by the links/oplogs artifact sections
// against the committed goldens.

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using uniffi.slate_uniffi;

namespace ParityHarness;

public sealed record MutationSeed(string Path, string Content);

public sealed record MutationOp(
    string Op,
    string? Label,
    string? Path,
    string? Content,
    string? NewName,
    string? NewParent,
    IReadOnlyList<string>? Items,
    string? Destination,
    int? OpRef,
    string? Name,
    string? Value,
    string? Expect);

public sealed record MutationScenario(
    string Name,
    IReadOnlyList<MutationSeed> Seed,
    IReadOnlyList<string> Untouched,
    IReadOnlyList<IReadOnlyList<string>> AssertEqual,
    string? TerminalTreeMatches,
    IReadOnlyList<MutationOp> Ops);

public static class MutationScenarios
{
    public static IReadOnlyList<MutationScenario> Load(string scenariosPath)
    {
        using JsonDocument document = JsonDocument.Parse(
            File.ReadAllBytes(scenariosPath));
        var scenarios = new List<MutationScenario>();
        foreach (JsonElement scenario in document.RootElement
            .GetProperty("scenarios").EnumerateArray())
        {
            var seeds = new List<MutationSeed>();
            foreach (JsonElement seed in scenario.GetProperty("seed").EnumerateArray())
            {
                seeds.Add(new MutationSeed(
                    seed.GetProperty("path").GetString()!,
                    seed.GetProperty("content").GetString()!));
            }

            var untouched = new List<string>();
            if (scenario.TryGetProperty("untouched", out JsonElement untouchedElement))
            {
                untouched.AddRange(untouchedElement.EnumerateArray()
                    .Select(item => item.GetString()!));
            }

            var pairs = new List<IReadOnlyList<string>>();
            if (scenario.TryGetProperty("assertEqual", out JsonElement pairsElement))
            {
                foreach (JsonElement pair in pairsElement.EnumerateArray())
                {
                    pairs.Add([.. pair.EnumerateArray().Select(item => item.GetString()!)]);
                }
            }

            var ops = new List<MutationOp>();
            foreach (JsonElement op in scenario.GetProperty("ops").EnumerateArray())
            {
                ops.Add(new MutationOp(
                    Op: op.GetProperty("op").GetString()!,
                    Label: OptionalString(op, "label"),
                    Path: OptionalString(op, "path"),
                    Content: OptionalString(op, "content"),
                    NewName: OptionalString(op, "newName"),
                    NewParent: OptionalString(op, "newParent"),
                    Items: op.TryGetProperty("items", out JsonElement items)
                        ? [.. items.EnumerateArray().Select(item => item.GetString()!)]
                        : null,
                    Destination: OptionalString(op, "destination"),
                    OpRef: op.TryGetProperty("opRef", out JsonElement opRef)
                        ? opRef.GetInt32()
                        : null,
                    Name: OptionalString(op, "name"),
                    Value: OptionalString(op, "value"),
                    Expect: OptionalString(op, "expect")));
            }

            scenarios.Add(new MutationScenario(
                scenario.GetProperty("name").GetString()!,
                seeds,
                untouched,
                pairs,
                scenario.TryGetProperty("terminalTreeMatches", out JsonElement matches)
                    ? matches.GetString()
                    : null,
                ops));
        }

        return scenarios;
    }

    private static string? OptionalString(JsonElement element, string name) =>
        element.TryGetProperty(name, out JsonElement value)
            && value.ValueKind != JsonValueKind.Null
            ? value.GetString()
            : null;
}

/// <summary>One file's terminal identity: forward-slash path, size,
/// lowercase-hex SHA-256 — byte-exact content pinned by hash, line
/// endings never normalized (decision 9).</summary>
public sealed record TreeEntry(string Path, long Size, string Sha256);

public sealed class MutationDriverException(string message) : Exception(message);

public static class MutationDriver
{
    /// <summary>The fault names the scenario data may set; the driver
    /// owns the SLATE_TEST_FAULT_ prefix so the data stays short and
    /// an unknown name fails loudly instead of silently not
    /// faulting.</summary>
    private static readonly string[] FaultNames =
        ["PLANT_MARKERS", "PRE_WRITE", "AFTER_WRITE", "RECOVERY_FINALIZE"];

    /// <summary>Run every scenario and return (name, artifactJson)
    /// pairs in data order. Cross-scenario invariant 6
    /// (terminalTreeMatches) is enforced here.</summary>
    public static IReadOnlyList<(string Name, string Artifact)> RunAll(
        string scenariosPath)
    {
        IReadOnlyList<MutationScenario> scenarios =
            MutationScenarios.Load(scenariosPath);
        var artifacts = new List<(string, string)>();
        var terminalTrees = new Dictionary<string, IReadOnlyList<TreeEntry>>(
            StringComparer.Ordinal);
        foreach (MutationScenario scenario in scenarios)
        {
            (string artifact, IReadOnlyList<TreeEntry> terminal) = Run(scenario);
            artifacts.Add((scenario.Name, artifact));
            terminalTrees[scenario.Name] = terminal;
        }

        foreach (MutationScenario scenario in scenarios)
        {
            if (scenario.TerminalTreeMatches is not string twin)
            {
                continue;
            }

            if (!TreesEqual(terminalTrees[scenario.Name], terminalTrees[twin]))
            {
                throw new MutationDriverException(
                    $"{scenario.Name}: terminal tree differs from {twin} — "
                    + "the retry-after-conflict run is not idempotent "
                    + "(invariant 6).");
            }
        }

        return artifacts;
    }

    private static (string Artifact, IReadOnlyList<TreeEntry> Terminal) Run(
        MutationScenario scenario)
    {
        string vaultRoot = Path.Combine(
            Path.GetTempPath(), $"mutation-harness-{Guid.NewGuid():N}");
        Directory.CreateDirectory(vaultRoot);
        var activeFaults = new List<string>();
        try
        {
            foreach (MutationSeed seed in scenario.Seed)
            {
                string absolute = Path.Combine(
                    vaultRoot,
                    seed.Path.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
                // Byte-exact seeds: LF exactly as the data spells them.
                File.WriteAllBytes(absolute, Encoding.UTF8.GetBytes(seed.Content));
            }

            var session = VaultSession.OpenFilesystem(vaultRoot);
            try
            {
                using (var cancel = new CancelToken())
                {
                    session.ScanInitial(cancel);
                }

                var checkpoints = new List<(string Label, IReadOnlyList<TreeEntry> Tree)>();
                var reports = new List<(int OpIndex, string Body)>();
                var refusals = new List<(int OpIndex, string Kind, string? Path)>();
                var reportsByIndex = new Dictionary<int, object>();
                var opIdOrder = new List<long>();

                for (int index = 0; index < scenario.Ops.Count; index++)
                {
                    MutationOp op = scenario.Ops[index];
                    switch (op.Op)
                    {
                        case "checkpoint":
                            IReadOnlyList<TreeEntry> tree = CaptureTree(vaultRoot);
                            checkpoints.Add((op.Label!, tree));
                            continue;
                        case "fault":
                            SetFault(op.Name!, op.Value, activeFaults);
                            continue;
                        case "reopen":
                            session.Dispose();
                            session = VaultSession.OpenFilesystem(vaultRoot);
                            using (var cancel = new CancelToken())
                            {
                                session.ScanInitial(cancel);
                            }

                            continue;
                    }

                    try
                    {
                        object? report = Execute(session, op, reportsByIndex);
                        if (op.Expect is not null)
                        {
                            throw new MutationDriverException(
                                $"{scenario.Name} op {index} ({op.Op}) expected "
                                + $"{op.Expect} but succeeded.");
                        }

                        if (report is not null)
                        {
                            reportsByIndex[index] = report;
                            reports.Add((index, MutationSerializer.NormalizeReport(
                                report, opIdOrder)));
                        }
                    }
                    catch (VaultException exception)
                    {
                        string kind = exception.GetType().Name;
                        if (op.Expect is null)
                        {
                            throw new MutationDriverException(
                                $"{scenario.Name} op {index} ({op.Op}) threw "
                                + $"{kind} with no expect.");
                        }

                        if (kind != op.Expect)
                        {
                            throw new MutationDriverException(
                                $"{scenario.Name} op {index} ({op.Op}) expected "
                                + $"{op.Expect} but threw {kind}.");
                        }

                        refusals.Add((
                            index,
                            kind,
                            exception is VaultException.DestinationExists occupied
                                ? occupied.path
                                : null));
                    }
                }

                EnforceCheckpointInvariants(scenario, checkpoints);
                IReadOnlyList<TreeEntry> terminal = CaptureTree(vaultRoot);
                string artifact = MutationSerializer.Artifact(
                    scenario.Name,
                    checkpoints,
                    reports,
                    refusals,
                    terminal,
                    session);
                return (artifact, terminal);
            }
            finally
            {
                session.Dispose();
            }
        }
        finally
        {
            foreach (string fault in activeFaults.ToList())
            {
                SetFault(fault, null, activeFaults);
            }

            try
            {
                Directory.Delete(vaultRoot, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private static object? Execute(
        VaultSession session, MutationOp op, Dictionary<int, object> reportsByIndex) =>
        op.Op switch
        {
            "createExclusive" => session.CreateExclusive(op.Path!, op.Content!),
            "createFolder" => session.CreateFolder(op.Path!),
            "renameFile" => session.RenameFile(op.Path!, op.NewName!),
            "renameFolderWithNote" =>
                session.RenameFolderWithNote(op.Path!, op.NewName!),
            "moveFile" => session.MoveFile(op.Path!, op.NewParent!),
            "moveFolder" => session.MoveFolder(op.Path!, op.NewParent!),
            "batchMove" => session.BatchMove(new BatchMoveRequest(
                [.. op.Items!.Select(item => new StructuralBatchItem(item, false))],
                op.Destination!)),
            "deleteFile" => ExecuteVoid(() => session.DeleteFile(op.Path!)),
            "undoReport" => ExecuteUndoReport(session, op, reportsByIndex),
            "undoBatchMove" => session.UndoBatchMove(
                ((BatchMoveReport)reportsByIndex[op.OpRef!.Value]).OpId
                    ?? throw new MutationDriverException(
                        $"op {op.OpRef}: batch report has no OpId to undo")),
            _ => throw new MutationDriverException($"unknown op '{op.Op}'"),
        };

    private static object? ExecuteVoid(Action action)
    {
        action();
        return null;
    }

    /// <summary>Undo a structural report via its recorded
    /// <c>UndoOpIds</c> IN ORDER (newest first — FL6-1's compound
    /// folder+note rename journals two rows). Returns the LAST undo's
    /// report.</summary>
    private static object? ExecuteUndoReport(
        VaultSession session, MutationOp op, Dictionary<int, object> reportsByIndex)
    {
        var forward = (StructuralReport)reportsByIndex[op.OpRef!.Value];
        object? last = null;
        foreach (long opId in forward.UndoOpIds)
        {
            last = session.UndoStructural(opId);
        }

        return last;
    }

    private static void SetFault(string name, string? value, List<string> active)
    {
        if (!FaultNames.Contains(name))
        {
            throw new MutationDriverException($"unknown fault '{name}'");
        }

        Environment.SetEnvironmentVariable($"SLATE_TEST_FAULT_{name}", value);
        if (value is null)
        {
            active.Remove(name);
        }
        else if (!active.Contains(name))
        {
            active.Add(name);
        }
    }

    private static void EnforceCheckpointInvariants(
        MutationScenario scenario,
        IReadOnlyList<(string Label, IReadOnlyList<TreeEntry> Tree)> checkpoints)
    {
        IReadOnlyList<TreeEntry> Tree(string label)
        {
            foreach ((string candidate, IReadOnlyList<TreeEntry> tree) in checkpoints)
            {
                if (candidate == label)
                {
                    return tree;
                }
            }

            throw new MutationDriverException(
                $"{scenario.Name}: assertEqual names unknown checkpoint '{label}'");
        }

        foreach (IReadOnlyList<string> pair in scenario.AssertEqual)
        {
            if (!TreesEqual(Tree(pair[0]), Tree(pair[1])))
            {
                throw new MutationDriverException(
                    $"{scenario.Name}: checkpoints '{pair[0]}' and '{pair[1]}' "
                    + "differ — a rollback/undo was not byte-exact "
                    + "(invariants 3/7).");
            }
        }

        foreach (string path in scenario.Untouched)
        {
            string? hash = null;
            foreach ((string label, IReadOnlyList<TreeEntry> tree) in checkpoints)
            {
                TreeEntry? entry = tree.FirstOrDefault(item => item.Path == path);
                if (entry is null)
                {
                    throw new MutationDriverException(
                        $"{scenario.Name}: untouched file '{path}' missing at "
                        + $"checkpoint '{label}' (invariant 1).");
                }

                hash ??= entry.Sha256;
                if (entry.Sha256 != hash)
                {
                    throw new MutationDriverException(
                        $"{scenario.Name}: untouched file '{path}' changed by "
                        + $"checkpoint '{label}' (invariant 1).");
                }
            }
        }
    }

    internal static IReadOnlyList<TreeEntry> CaptureTree(string vaultRoot)
    {
        var entries = new List<TreeEntry>();
        foreach (string absolute in Directory
            .EnumerateFiles(vaultRoot, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(vaultRoot, absolute)
                .Replace(Path.DirectorySeparatorChar, '/');
            if (relative.StartsWith(".slate/", StringComparison.Ordinal))
            {
                // Cache bytes and oplog stems are legitimately
                // platform/run-variant (H2).
                continue;
            }

            byte[] bytes = File.ReadAllBytes(absolute);
            entries.Add(new TreeEntry(
                relative,
                bytes.LongLength,
                Convert.ToHexStringLower(SHA256.HashData(bytes))));
        }

        entries.Sort((left, right) =>
            string.CompareOrdinal(left.Path, right.Path));
        return entries;
    }

    private static bool TreesEqual(
        IReadOnlyList<TreeEntry> left, IReadOnlyList<TreeEntry> right) =>
        left.Count == right.Count
        && left.Zip(right).All(pair => pair.First == pair.Second);
}

public static class MutationSerializer
{
    /// <summary>Normalize one report (H2): OpIds sequence-relative via
    /// first-appearance interning; error strings reduced to kinds;
    /// SaveReport's NewMtimeMs dropped.</summary>
    public static string NormalizeReport(object report, List<long> opIdOrder)
    {
        var j = new CanonicalJson();
        switch (report)
        {
            case SaveReport save:
                j.Raw("{\"kind\":\"save\",\"newContentHash\":").Str(save.NewContentHash)
                 .Raw(",\"newSizeBytes\":").Num(save.NewSizeBytes)
                 .Raw("}");
                break;
            case StructuralReport structural:
                j.Raw("{\"kind\":\"structural\",\"opId\":")
                 .Num(RelativeOpId(structural.OpId, opIdOrder));
                j.Raw(",\"undoOpIds\":[");
                for (int i = 0; i < structural.UndoOpIds.Length; i++)
                {
                    if (i > 0)
                    {
                        j.Raw(",");
                    }

                    j.Num(RelativeOpId(structural.UndoOpIds[i], opIdOrder));
                }

                j.Raw("],\"moved\":[");
                for (int i = 0; i < structural.Moved.Length; i++)
                {
                    if (i > 0)
                    {
                        j.Raw(",");
                    }

                    j.Raw("{\"old\":").Str(structural.Moved[i].OldPath)
                     .Raw(",\"new\":").Str(structural.Moved[i].NewPath)
                     .Raw("}");
                }

                j.Raw("],\"rewritten\":[");
                for (int i = 0; i < structural.Rewritten.Length; i++)
                {
                    if (i > 0)
                    {
                        j.Raw(",");
                    }

                    j.Raw("{\"path\":").Str(structural.Rewritten[i].Path)
                     .Raw(",\"hashBefore\":").Str(structural.Rewritten[i].HashBefore)
                     .Raw(",\"hashAfter\":").Str(structural.Rewritten[i].HashAfter)
                     .Raw("}");
                }

                j.Raw("],\"failed\":[");
                for (int i = 0; i < structural.Failed.Length; i++)
                {
                    if (i > 0)
                    {
                        j.Raw(",");
                    }

                    // Kinds, not detail strings — platform io-text
                    // varies (H2).
                    j.Raw("{\"path\":").Str(structural.Failed[i].Path)
                     .Raw(",\"kind\":").Str(structural.Failed[i].Kind.Kind)
                     .Raw("}");
                }

                j.Raw("]}");
                break;
            case BatchMoveReport batch:
                j.Raw("{\"kind\":\"batchMove\",\"state\":").Str(batch.State.ToString())
                 .Raw(",\"opId\":");
                if (batch.OpId is long batchOpId)
                {
                    j.Num(RelativeOpId(batchOpId, opIdOrder));
                }
                else
                {
                    j.Null();
                }

                j.Raw(",\"standing\":[");
                for (int i = 0; i < batch.Standing.Length; i++)
                {
                    if (i > 0)
                    {
                        j.Raw(",");
                    }

                    j.Raw("{\"old\":").Str(batch.Standing[i].OldPath)
                     .Raw(",\"new\":").Str(batch.Standing[i].NewPath)
                     .Raw(",\"dir\":").Bool(batch.Standing[i].IsDirectory)
                     .Raw("}");
                }

                j.Raw("],\"rolledBack\":[");
                for (int i = 0; i < batch.RolledBack.Length; i++)
                {
                    if (i > 0)
                    {
                        j.Raw(",");
                    }

                    j.Raw("{\"old\":").Str(batch.RolledBack[i].OldPath)
                     .Raw(",\"new\":").Str(batch.RolledBack[i].NewPath)
                     .Raw(",\"dir\":").Bool(batch.RolledBack[i].IsDirectory)
                     .Raw("}");
                }

                // Failure stages only (kinds travel; message text is
                // platform io-text).
                j.Raw("],\"failureStage\":");
                if (batch.Failure is BatchItemFailure failure)
                {
                    j.Str(failure.Stage.ToString());
                }
                else
                {
                    j.Null();
                }

                j.Raw(",\"rollbackFailureStages\":[");
                for (int i = 0; i < batch.RollbackFailures.Length; i++)
                {
                    if (i > 0)
                    {
                        j.Raw(",");
                    }

                    j.Str(batch.RollbackFailures[i].Stage.ToString());
                }

                j.Raw("],\"requiresRescan\":").Bool(batch.RequiresRescan)
                 .Raw("}");
                break;
            default:
                throw new MutationDriverException(
                    $"unserializable report type {report.GetType().Name}");
        }

        return j.ToString();
    }

    private static long RelativeOpId(long opId, List<long> order)
    {
        int index = order.IndexOf(opId);
        if (index < 0)
        {
            order.Add(opId);
            index = order.Count - 1;
        }

        return index;
    }

    public static string Artifact(
        string scenarioName,
        IReadOnlyList<(string Label, IReadOnlyList<TreeEntry> Tree)> checkpoints,
        IReadOnlyList<(int OpIndex, string Body)> reports,
        IReadOnlyList<(int OpIndex, string Kind, string? Path)> refusals,
        IReadOnlyList<TreeEntry> terminal,
        VaultSession session)
    {
        var j = new CanonicalJson();
        j.Raw("{\"scenario\":").Str(scenarioName);

        j.Raw(",\"checkpoints\":[");
        for (int i = 0; i < checkpoints.Count; i++)
        {
            if (i > 0)
            {
                j.Raw(",");
            }

            j.Raw("{\"label\":").Str(checkpoints[i].Label).Raw(",\"tree\":");
            WriteTree(j, checkpoints[i].Tree);
            j.Raw("}");
        }

        j.Raw("],\"reports\":[");
        for (int i = 0; i < reports.Count; i++)
        {
            if (i > 0)
            {
                j.Raw(",");
            }

            j.Raw("{\"op\":").Num((long)reports[i].OpIndex)
             .Raw(",\"report\":").Raw(reports[i].Body)
             .Raw("}");
        }

        j.Raw("],\"refusals\":[");
        for (int i = 0; i < refusals.Count; i++)
        {
            if (i > 0)
            {
                j.Raw(",");
            }

            j.Raw("{\"op\":").Num((long)refusals[i].OpIndex)
             .Raw(",\"kind\":").Str(refusals[i].Kind)
             .Raw(",\"path\":");
            if (refusals[i].Path is string path)
            {
                j.Str(path);
            }
            else
            {
                j.Null();
            }

            j.Raw("}");
        }

        j.Raw("],\"tree\":");
        WriteTree(j, terminal);

        // Transactionally consistent op-logs (H2/H5-5): TimestampMs
        // dropped, payloads hashed, `.oplog` stems never compared,
        // UserActorId pinned to the FFI constant.
        j.Raw(",\"oplogs\":[");
        var markdownFiles = terminal
            .Where(entry => entry.Path.EndsWith(".md", StringComparison.Ordinal))
            .Select(entry => entry.Path)
            .ToList();
        for (int f = 0; f < markdownFiles.Count; f++)
        {
            if (f > 0)
            {
                j.Raw(",");
            }

            j.Raw("{\"path\":").Str(markdownFiles[f]).Raw(",\"entries\":[");
            OpLogEntry[] entries = session.ReadOplog(markdownFiles[f]);
            for (int i = 0; i < entries.Length; i++)
            {
                if (i > 0)
                {
                    j.Raw(",");
                }

                j.Raw("{\"opKind\":").Str(entries[i].OpKind.ToString())
                 .Raw(",\"actor\":").Str(entries[i].UserActorId)
                 .Raw(",\"hashBefore\":").Str(entries[i].ContentHashBefore)
                 .Raw(",\"hashAfter\":").Str(entries[i].ContentHashAfter)
                 .Raw(",\"payloadSha256\":").Str(
                     Convert.ToHexStringLower(
                         SHA256.HashData(entries[i].PayloadBytes)))
                 .Raw("}");
            }

            j.Raw("]}");
        }

        // The referential-integrity oracle (H5-4): the read harness's
        // links artifact re-emitted over the terminal vault.
        j.Raw("],\"links\":").Raw(
            SurfaceSerializer.LinksArtifact(session, markdownFiles));

        j.Raw("}\n");
        return j.ToString();
    }

    private static void WriteTree(CanonicalJson j, IReadOnlyList<TreeEntry> tree)
    {
        j.Raw("[");
        for (int i = 0; i < tree.Count; i++)
        {
            if (i > 0)
            {
                j.Raw(",");
            }

            j.Raw("{\"path\":").Str(tree[i].Path)
             .Raw(",\"size\":").Num(tree[i].Size)
             .Raw(",\"sha256\":").Str(tree[i].Sha256)
             .Raw("}");
        }

        j.Raw("]");
    }
}
