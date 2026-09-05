// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// W6-1 §E TE-11d (E18): the canvas mode of the §W-A differential
// harness. The scenario scripts are DATA
// (crates/slate-core/tests/fixtures/canvas_scenario_golden/scenarios.json),
// shared verbatim with the Swift twin (CanvasScenarioTests.swift in
// SlateMacTests, landed by W6-1 §H), so identical CanvasAction
// sequences on both platforms are enforced by construction. Each
// scenario seeds ONE fixture into a fresh temp vault as board.canvas,
// applies each step through the REAL vault apply (the inverse-carrying
// receipt), then walks the inverses backward.
//
// Driver-enforced invariants:
// - Every scenario: the post-inverse bytes equal core's OWN canonical
//   serialization of the original content — `canvas_apply_detached`
//   with an empty action is the canonicalizer, so semantic equality is
//   core's judgment, never a host reimplementation.
// - Canonical fixtures (no `foreign` flag): post-inverse bytes equal
//   the ORIGINAL bytes verbatim (the corpus is canonical, so
//   apply-plus-invert is byte-identity — E18).
// - Foreign fixtures (`foreign: true`): the original bytes must DIFFER
//   from the round-trip — the foreign formatting must not survive the
//   first write (E18's rule said from the other side).

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using uniffi.slate_uniffi;

namespace ParityHarness;

public sealed record CanvasScenarioStep(string Name, IReadOnlyList<CanvasOp> Ops);

public sealed record CanvasScenario(
    string Name,
    string FixturePath,
    bool Foreign,
    IReadOnlyList<CanvasScenarioStep> Steps);

public sealed class CanvasScenarioDriverException(string message) : Exception(message);

public static class CanvasScenarioDriver
{
    public static IReadOnlyList<(string Name, string Artifact)> RunAll(
        string scenariosPath)
    {
        IReadOnlyList<CanvasScenario> scenarios = Load(scenariosPath);
        var artifacts = new List<(string, string)>();
        foreach (CanvasScenario scenario in scenarios)
        {
            artifacts.Add((scenario.Name, Run(scenario)));
        }

        return artifacts;
    }

    public static IReadOnlyList<CanvasScenario> Load(string scenariosPath)
    {
        string baseDir = Path.GetDirectoryName(Path.GetFullPath(scenariosPath))!;
        using JsonDocument document = JsonDocument.Parse(
            File.ReadAllText(scenariosPath));
        var scenarios = new List<CanvasScenario>();
        foreach (JsonElement scenario in document.RootElement
            .GetProperty("scenarios").EnumerateArray())
        {
            var steps = new List<CanvasScenarioStep>();
            foreach (JsonElement step in scenario.GetProperty("steps").EnumerateArray())
            {
                var ops = new List<CanvasOp>();
                foreach (JsonElement op in step.GetProperty("ops").EnumerateArray())
                {
                    ops.Add(ParseOp(op));
                }

                steps.Add(new CanvasScenarioStep(
                    step.GetProperty("name").GetString()!, ops));
            }

            scenarios.Add(new CanvasScenario(
                scenario.GetProperty("name").GetString()!,
                Path.GetFullPath(Path.Combine(
                    baseDir, scenario.GetProperty("fixture").GetString()!)),
                scenario.TryGetProperty("foreign", out JsonElement f) && f.GetBoolean(),
                steps));
        }

        return scenarios;
    }

    private static string Run(CanvasScenario scenario)
    {
        string vaultRoot = Path.Combine(
            Path.GetTempPath(), $"canvas-scenario-{Guid.NewGuid():N}");
        Directory.CreateDirectory(vaultRoot);
        try
        {
            string original = File.ReadAllText(scenario.FixturePath);
            string board = Path.Combine(vaultRoot, "board.canvas");
            File.WriteAllText(board, original);
            using VaultSession session = VaultSession.OpenFilesystem(vaultRoot);
            using (var cancel = new CancelToken())
            {
                session.ScanInitial(cancel);
            }

            CanvasOpenInfo info = session.OpenCanvas("board.canvas");
            var steps = new List<(string Name, string Hash)>();
            var inverses = new List<CanvasAction>();
            try
            {
                foreach (CanvasScenarioStep step in scenario.Steps)
                {
                    CanvasApplyResult result = session.CanvasApply(
                        info.Handle, new CanvasAction(step.Name, [.. step.Ops]));
                    steps.Add((step.Name, result.NewContentHash));
                    inverses.Add(result.Inverse);
                }

                string terminal = File.ReadAllText(board);
                for (int i = inverses.Count - 1; i >= 0; i--)
                {
                    _ = session.CanvasApply(info.Handle, inverses[i]);
                }

                string roundTrip = File.ReadAllText(board);
                // Core's own canonicalizer: the empty detached apply.
                string canonicalOfOriginal = SlateUniffiMethods.CanvasApplyDetached(
                    original, new CanvasAction("canonicalize", []));
                if (roundTrip != canonicalOfOriginal)
                {
                    throw new CanvasScenarioDriverException(
                        $"{scenario.Name}: the inverse walk did not restore core's "
                        + "canonical serialization of the original content.");
                }

                if (!scenario.Foreign && roundTrip != original)
                {
                    throw new CanvasScenarioDriverException(
                        $"{scenario.Name}: a canonical fixture must round-trip "
                        + "byte-identical (E18).");
                }

                if (scenario.Foreign && roundTrip == original)
                {
                    throw new CanvasScenarioDriverException(
                        $"{scenario.Name}: the foreign formatting survived the "
                        + "round-trip; the first write must canonicalize (E18).");
                }

                return Artifact(scenario, original, steps, terminal, roundTrip);
            }
            finally
            {
                session.CloseCanvas(info.Handle);
            }
        }
        finally
        {
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

    private static string Artifact(
        CanvasScenario scenario,
        string original,
        IReadOnlyList<(string Name, string Hash)> steps,
        string terminal,
        string roundTrip)
    {
        var j = new CanonicalJson();
        j.Raw("{\"scenario\":").Str(scenario.Name);
        j.Raw(",\"foreign\":").Raw(scenario.Foreign ? "true" : "false");
        j.Raw(",\"originalSha256\":").Str(Sha(original));
        j.Raw(",\"steps\":[");
        for (int i = 0; i < steps.Count; i++)
        {
            if (i > 0)
            {
                j.Raw(",");
            }

            j.Raw("{\"name\":").Str(steps[i].Name)
                .Raw(",\"contentHash\":").Str(steps[i].Hash).Raw("}");
        }

        j.Raw("],\"terminalSha256\":").Str(Sha(terminal));
        j.Raw(",\"terminalBytes\":").Str(terminal);
        j.Raw(",\"roundTripSha256\":").Str(Sha(roundTrip));
        j.Raw("}\n");
        return j.ToString();
    }

    private static string Sha(string text) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    private static CanvasOp ParseOp(JsonElement op) =>
        op.GetProperty("kind").GetString() switch
        {
            "createNode" => new CanvasOp.CreateNode(
                op.GetProperty("id").GetString()!,
                new CanvasNodeContent.Text(op.GetProperty("text").GetString()!),
                op.GetProperty("x").GetDouble(),
                op.GetProperty("y").GetDouble(),
                op.GetProperty("width").GetDouble(),
                op.GetProperty("height").GetDouble(),
                Optional(op, "color")),
            "createGroup" => new CanvasOp.CreateGroup(
                op.GetProperty("id").GetString()!,
                Optional(op, "label"),
                op.GetProperty("x").GetDouble(),
                op.GetProperty("y").GetDouble(),
                op.GetProperty("width").GetDouble(),
                op.GetProperty("height").GetDouble(),
                Optional(op, "color")),
            "updateNodeGeometry" => new CanvasOp.UpdateNodeGeometry(
                op.GetProperty("id").GetString()!,
                op.GetProperty("x").GetDouble(),
                op.GetProperty("y").GetDouble(),
                op.GetProperty("width").GetDouble(),
                op.GetProperty("height").GetDouble()),
            "setNodeColor" => new CanvasOp.SetNodeColor(
                op.GetProperty("id").GetString()!, Optional(op, "color")),
            "setNodeContent" => new CanvasOp.SetNodeContent(
                op.GetProperty("id").GetString()!,
                new CanvasNodeContent.Text(op.GetProperty("text").GetString()!)),
            "deleteNode" => new CanvasOp.DeleteNode(
                op.GetProperty("id").GetString()!),
            "addEdge" => new CanvasOp.AddEdge(
                op.GetProperty("id").GetString()!,
                op.GetProperty("fromNode").GetString()!,
                Side(op, "fromSide"),
                op.GetProperty("toNode").GetString()!,
                Side(op, "toSide"),
                End(op, "fromEnd"),
                End(op, "toEnd"),
                Optional(op, "label"),
                Optional(op, "color")),
            "updateEdge" => new CanvasOp.UpdateEdge(
                op.GetProperty("id").GetString()!,
                Side(op, "fromSide"),
                Side(op, "toSide"),
                End(op, "fromEnd"),
                End(op, "toEnd"),
                Optional(op, "label"),
                Optional(op, "color")),
            "deleteEdge" => new CanvasOp.DeleteEdge(
                op.GetProperty("id").GetString()!),
            var kind => throw new CanvasScenarioDriverException(
                $"unknown canvas op '{kind}'"),
        };

    private static string? Optional(JsonElement op, string name) =>
        op.TryGetProperty(name, out JsonElement value)
            && value.ValueKind != JsonValueKind.Null
            ? value.GetString()
            : null;

    private static CanvasSide? Side(JsonElement op, string name) =>
        Optional(op, name) switch
        {
            null => null,
            "top" => CanvasSide.Top,
            "right" => CanvasSide.Right,
            "bottom" => CanvasSide.Bottom,
            "left" => CanvasSide.Left,
            var side => throw new CanvasScenarioDriverException(
                $"unknown side '{side}'"),
        };

    private static CanvasEndStyle End(JsonElement op, string name) =>
        Optional(op, name) switch
        {
            "arrow" => CanvasEndStyle.Arrow,
            _ => CanvasEndStyle.None,
        };
}
