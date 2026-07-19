// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// W0-1 binding-spike runtime probe (#714). Exercises the w0_spec §W0-1
// rule-1 fixed probe surface plus the rule-3 §W-E stress patterns against
// the uniffi-bindgen-cs generated binding, printing PASS/FAIL per section
// and a findings block that feeds the spec's §Decision evidence.
//
// Exit code = number of failed sections (0 = all green).

namespace SlateProbe;

internal static class Program
{
    private static int Main(string[] args)
    {
        string? filter = null;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--filter" && i + 1 < args.Length) filter = args[i + 1];
        }

        var sections = new (string Name, Func<Probe, bool> Run)[]
        {
            ("session-lifetime", CoreSections.SessionLifetime),
            ("doc-buffer", CoreSections.DocBuffer),
            ("scan-progress", EventSections.ScanProgressSection),
            ("vault-events", EventSections.VaultEvents),
            ("cancellation", CoreSections.Cancellation),
            ("commands", CoreSections.Commands),
            ("error-mapping", CoreSections.ErrorMapping),
            ("stress-gc", StressSections.GcPressure),
            ("stress-callback-concurrency", StressSections.CallbackConcurrency),
            ("stress-listener-lifetime", StressSections.ListenerLifetime),
        };

        Console.WriteLine($"slate csharp-probe | {Environment.OSVersion} | " +
                          $"{System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture} | " +
                          $".NET {Environment.Version}");
        Console.WriteLine();

        int failed = 0;
        var results = new List<(string Name, bool Pass, TimeSpan Elapsed)>();
        foreach (var (name, run) in sections)
        {
            if (filter != null && !name.Contains(filter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            var probe = new Probe(name);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            bool pass;
            try
            {
                pass = run(probe);
            }
            catch (Exception ex)
            {
                probe.Note($"unhandled: {ex.GetType().Name}: {ex.Message}");
                pass = false;
            }
            sw.Stop();
            results.Add((name, pass, sw.Elapsed));
            if (!pass) failed++;
            Console.WriteLine($"[{(pass ? "PASS" : "FAIL")}] {name} ({sw.Elapsed.TotalSeconds:F1}s)");
            foreach (var line in probe.Lines)
            {
                Console.WriteLine($"       {line}");
            }
        }

        Console.WriteLine();
        Console.WriteLine("== findings (decision-record evidence) ==");
        foreach (var f in Probe.Findings)
        {
            Console.WriteLine($"  - {f}");
        }
        Console.WriteLine();
        Console.WriteLine(failed == 0
            ? $"all {results.Count} sections passed"
            : $"{failed}/{results.Count} sections FAILED");
        return failed;
    }
}

/// <summary>
/// Per-section context: detail lines (printed under the section header)
/// and a global findings sink whose entries become §Decision evidence.
/// </summary>
internal sealed class Probe
{
    public static readonly List<string> Findings = new();
    public readonly List<string> Lines = new();
    private readonly string _section;

    public Probe(string section) => _section = section;

    public void Note(string line) => Lines.Add(line);

    public void Finding(string line)
    {
        Lines.Add(line);
        lock (Findings) Findings.Add($"[{_section}] {line}");
    }

    /// <summary>Assert helper: false marks the check failed and notes why.</summary>
    public bool Check(bool condition, string label)
    {
        Lines.Add($"{(condition ? "ok" : "MISS")}: {label}");
        return condition;
    }
}
