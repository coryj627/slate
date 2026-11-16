// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SlateWindows.Tests.Censuses;

/// <summary>
/// How the Windows tests wait, kept by rule over both test projects (the
/// unit project and the shell gate's). Each rule is a fact of its own with
/// its own allow-list, over one shared walk, and each is a pure function of
/// the sources it is handed, so a planted source proves the rule sees what
/// it forbids.
/// </summary>
/// <remarks>
/// <para>
/// The walk is the two test projects' own compilations as their builds
/// recorded them (<see cref="TestEvidenceCompilation"/>). Rule 3 binds; it
/// does not read spellings: a gate is a gate by its type, whether it is a
/// field, a <c>var</c>, an element of a list or a pattern variable, and a
/// session is a session by its type. Rules 1 and 2 read the shape of the
/// source (a statement, a loop, a lambda) and bind the names the framework
/// owns; each says in its header which names it reads as spelled.
/// </para>
/// <para>
/// Rule 3 (#1673) is the first. A later rule is appended as a block of its
/// own at the end of the file: rules 1 and 2 (plan 47 CB-6) stand after it.
/// </para>
/// </remarks>
[Trait("census", "test-wait")]
public sealed class TestWaitCensus
{
    // ---- Shared: the walk -------------------------------------------------

    /// <summary>One test project: its compilation and its own source files
    /// (nothing under <c>bin</c> or <c>obj</c>).</summary>
    internal sealed record TestProject(string Name, CSharpCompilation Compilation, IReadOnlyList<SyntaxTree> Sources);

    /// <summary>One thing a rule refuses: the file (relative to the tests
    /// folder), the type, the line and what is wrong there.</summary>
    internal sealed record Violation(string File, string Type, int Line, string Text)
    {
        public override string ToString() => $"{File}:{Line} ({Type}): {Text}";
    }

    private const string UnitProject = "SlateWindows.Tests";
    private const string GateProject = "SlateWindows.AccessibilityTests";

    /// <summary>The files counted on 2026-11-16: 422 in the unit project and
    /// 33 in the gate's. Fewer means the walk missed files, not that the
    /// projects shrank.</summary>
    private static readonly (string Project, int Files)[] FilesAtLeast = [(UnitProject, 400), (GateProject, 30)];

    private static readonly Lazy<IReadOnlyList<TestProject>> Walk = new(ReadBothTestProjects);

    private static string TestsRoot() => Path.Combine(SourceText.RepoRoot(), "apps", "slate-windows", "tests");

    private static string FileOf(SyntaxTree tree) =>
        Path.GetRelativePath(TestsRoot(), tree.FilePath).Replace('\\', '/');

    private static IReadOnlyList<TestProject> ReadBothTestProjects()
    {
        string root = TestsRoot();
        return [.. TestEvidenceCompilation.Projects.Select(evidence =>
        {
            CSharpCompilation compilation = evidence.Compilation;
            string name = compilation.AssemblyName ?? string.Empty;
            return new TestProject(
                name,
                compilation,
                [.. compilation.SyntaxTrees.Where(tree => IsOwnSource(Path.Combine(root, name), tree.FilePath))]);
        })];
    }

    private static bool IsOwnSource(string projectRoot, string path)
    {
        string relative = Path.GetRelativePath(projectRoot, path);
        string[] segments = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return !Path.IsPathRooted(relative)
            && segments[0] != ".."
            && !segments.Any(segment => segment is "bin" or "obj");
    }

    [Fact]
    public void TheWalkReadsBothTestProjects()
    {
        IReadOnlyList<TestProject> projects = Walk.Value;
        foreach ((string project, int files) in FilesAtLeast)
        {
            TestProject? read = projects.SingleOrDefault(candidate => candidate.Name == project);
            Assert.True(read is not null, $"the walk did not read {project}");
            Assert.True(
                read.Sources.Count >= files,
                $"the walk read {read.Sources.Count} files of {project}; it held at least {files} when the census "
                + "was written, so the walk is partial.");
        }
        Assert.Equal(FilesAtLeast.Length, projects.Count);
    }

    /// <summary>Fails on a hit no entry explains, on an entry with no reason
    /// or date, and on an entry that matches no hit.</summary>
    private static void AssertOnlyTheListed(
        IReadOnlyList<Violation> found,
        (string File, string Type, string Reason, string Dated)[] allowed,
        string remedy)
    {
        string[] unexplained = [.. allowed
            .Where(entry => string.IsNullOrWhiteSpace(entry.Reason) || string.IsNullOrWhiteSpace(entry.Dated))
            .Select(entry => $"{entry.File} ({entry.Type})")];
        Assert.True(
            unexplained.Length == 0,
            "Allow-list entries with no reason or no date: " + string.Join(", ", unexplained));

        string[] offenders = [.. found
            .Where(violation => !allowed.Any(entry => entry.File == violation.File && entry.Type == violation.Type))
            .Select(violation => violation.ToString())];
        Assert.True(offenders.Length == 0, remedy + "\n" + string.Join("\n", offenders));

        string[] stale = [.. allowed
            .Where(entry => !found.Any(violation => violation.File == entry.File && violation.Type == entry.Type))
            .Select(entry => $"{entry.File} ({entry.Type})")];
        Assert.True(stale.Length == 0, "Allow-list entries that match no hit: " + string.Join(", ", stale));
    }

    // ---- Rule 3 (#1673): no fixture tears down under work it released ----
    //
    // 3a: a type's Dispose never disposes a gate the type sets, releases or
    //     waits on. A worker that has not reached the gate yet, or is still
    //     waking on it, meets a disposed event.
    // 3b: a Dispose that releases a gate and later tears a session down has
    //     a drain between its last release and the session, so all the
    //     released work ends while the session lives.
    //
    // What rule 3 does not prove. It reads a teardown in source order, not
    // in the order a run takes through its branches. It follows a test
    // method the teardown calls one level deep, and another fixture's
    // Dispose by what that Dispose does as a whole. 3b sees that a drain is
    // named between the release and the session, not that it is the drain of
    // the released work: each fixture's forced fact proves that
    // (TeardownWitness). A park and the host that owns its session are two
    // objects a fact disposes one after the other, and no rule joins them. A
    // gate or a session reached through a variable of a wider type
    // (IDisposable, object) is not followed. The using-declared gates in
    // fact bodies are outside the rule.

    /// <summary>Fixtures rule 3b names whose teardown is right as it stands.
    /// In two the product's own lifecycle waits for the released work before
    /// it lets the session go, so the fixture names no drain of its own; in
    /// one the release the rule sees after the drain lets nothing go. Rule
    /// 3a has no such list: there is no right way to dispose a gate a worker
    /// may still reach.</summary>
    private static readonly (string File, string Type, string Reason, string Dated)[] RightAsTheyStand =
    [
        (
            "SlateWindows.Tests/ConnectionsLeafTests.Model.cs",
            "ModelCaseResources",
            "its park's Dispose stands after Host.Settle's drain and only sets a gate every path has set already: "
            + "DisposeModelWorkspace, or the line that sets the gate, releases the fetch before the drain",
            "2026-11-16"),
        (
            "SlateWindows.Tests/RescanTests.cs",
            "Harness",
            "its gates park the open's session load and a rescan's core call; Lifecycle.Dispose drains every admitted "
            + "rescan core call and waits for the session load before it disposes the session",
            "2026-11-16"),
        (
            "SlateWindows.Tests/PumpedSaveReentrancyTests.cs",
            "Host",
            "its gates park a save's write and the sidebar's tree reads; Lifecycle.Dispose joins the sidebar's work and "
            + "the workspace's save workers before it disposes the session",
            "2026-11-16"),
    ];

    private static readonly HashSet<string> GateTypes = new(StringComparer.Ordinal)
    {
        "ManualResetEventSlim",
        "ManualResetEvent",
        "AutoResetEvent",
        "SemaphoreSlim",
        "CountdownEvent",
        "EventWaitHandle",
        "Semaphore",
    };

    /// <summary>What a type does with a gate besides disposing it: it lets
    /// waiters go, or it waits.</summary>
    private static readonly HashSet<string> GateUses = new(StringComparer.Ordinal)
    {
        "Set",
        "Reset",
        "Release",
        "Signal",
        "AddCount",
        "TryAddCount",
        "Wait",
        "WaitOne",
        "WaitAsync",
        "WaitHandle",
        "AvailableWaitHandle",
    };

    private static readonly HashSet<string> GateReleases = new(StringComparer.Ordinal) { "Set", "Release", "Signal" };

    private static readonly HashSet<string> CompletionReleases = new(StringComparer.Ordinal)
    {
        "SetResult",
        "TrySetResult",
        "SetException",
        "TrySetException",
        "SetCanceled",
        "TrySetCanceled",
        "SetFromTask",
        "TrySetFromTask",
    };

    private static readonly HashSet<string> SessionTypes = new(StringComparer.Ordinal)
    {
        "VaultSession",
        "VaultLifecycleViewModel",
    };

    /// <summary>The calls that wait for work on the pool. Not
    /// <c>PumpedDispatcher.Drain()</c>: it runs what is queued on the
    /// dispatcher and waits for no pool work.</summary>
    private static readonly HashSet<string> Drains = new(StringComparer.Ordinal)
    {
        "PumpUntilDrained",
        "WhenRefreshWorkDrained",
        "WhenAllWorkDrained",
        "WhenIdle",
        "DrainBeforeTheSessionGoes",
    };

    [Fact]
    public void NoFixtureDisposesAGateItReleasesOrWaitsOn()
    {
        List<Violation> found = [];
        foreach (TestProject project in Walk.Value)
        {
            found.AddRange(GatesDisposedInTeardown(project.Compilation, project.Sources, FileOf));
        }
        Assert.True(
            found.Count == 0,
            "These dispose a gate their own type sets, releases or waits on. A worker that has not reached the gate "
            + "yet, or is still waking on it, meets a disposed event. Release the gate and leave it to the collector "
            + "(GraphDiagramTests.Lineage's park), or hold with HeldWork, which has nothing to dispose. This rule has "
            + "no allow-list:\n" + string.Join("\n", found));
    }

    [Fact]
    public void AFixtureThatReleasesAGateDrainsBeforeItsSessionGoes()
    {
        List<Violation> found = [];
        foreach (TestProject project in Walk.Value)
        {
            found.AddRange(ReleasesWithNoDrainBeforeTheSession(project.Compilation, project.Sources, FileOf));
        }
        AssertOnlyTheListed(
            found,
            RightAsTheyStand,
            "These release held work and then dispose a session with no drain after the last release, so the "
            + "released work can run after the session is gone. Wait for it first: "
            + "TeardownWitness.DrainBeforeTheSessionGoes over the product's own drain (WhenRefreshWorkDrained, "
            + "WhenAllWorkDrained, WhenIdle), then the session:");
    }

    /// <summary>Rule 3a on planted sources and rule 3b on planted sources:
    /// each forbidden form is seen, and each right form is left alone.</summary>
    [Theory]
    [MemberData(nameof(PlantedFixtures))]
    public void TheTeardownRulesSeeAPlantedFixture(string form, string planted, string expected)
    {
        CSharpCompilation unit = Walk.Value.Single(project => project.Name == UnitProject).Compilation;
        SyntaxTree tree = CSharpSyntaxTree.ParseText(
            "using uniffi.slate_uniffi;\nnamespace SlateWindows.Tests.Planted;\n" + planted,
            (CSharpParseOptions)unit.SyntaxTrees.First().Options,
            path: "Planted.cs");
        Assert.DoesNotContain(tree.GetDiagnostics(), diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        CSharpCompilation compilation = unit.AddSyntaxTrees(tree);
        Diagnostic[] unbound = [.. compilation.GetSemanticModel(tree).GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)];
        Assert.True(unbound.Length == 0, $"{form}: the planted source does not bind:\n" + string.Join("\n", unbound.Select(d => d.ToString())));

        bool disposesAGate = GatesDisposedInTeardown(compilation, [tree], _ => "Planted.cs").Count > 0;
        bool hasNoDrain = ReleasesWithNoDrainBeforeTheSession(compilation, [tree], _ => "Planted.cs").Count > 0;
        string seen = (disposesAGate, hasNoDrain) switch
        {
            (true, true) => "3a+3b",
            (true, false) => "3a",
            (false, true) => "3b",
            _ => "clean",
        };
        Assert.True(expected == seen, $"{form}: expected {expected}, the rules answered {seen}");
    }

    public static TheoryData<string, string, string> PlantedFixtures() => new()
    {
        {
            "sets and disposes one field",
            "sealed class P : IDisposable { private readonly ManualResetEventSlim _release = new(false); "
            + "public void Hook() => _release.Wait(); public void Dispose() { _release.Set(); _release.Dispose(); } }",
            "3a"
        },
        {
            "sets through its own Release(), then disposes",
            "sealed class P : IDisposable { private readonly ManualResetEventSlim _release = new(false); "
            + "public void Release() => _release.Set(); public void Dispose() { Release(); _release.Dispose(); } }",
            "3a"
        },
        {
            "disposes through its own helper",
            "sealed class P : IDisposable { private readonly ManualResetEventSlim _release = new(false); "
            + "private void Free() => _release.Dispose(); public void Dispose() { _release.Set(); Free(); } }",
            "3a"
        },
        {
            "sets in one loop, disposes in another",
            "sealed class P : IDisposable { private readonly List<ManualResetEventSlim> _gates = []; "
            + "public void Dispose() { foreach (ManualResetEventSlim gate in _gates) { gate.Set(); } "
            + "foreach (var gate in _gates) { gate.Dispose(); } } }",
            "3a"
        },
        {
            "disposes a gate its hook waits on",
            "sealed class P : IDisposable { private readonly ManualResetEventSlim _gate = new(false); public Action Hook; "
            + "public P() { Hook = () => _gate.Wait(TimeSpan.FromSeconds(30)); } public void Dispose() { _gate.Dispose(); } }",
            "3a"
        },
        {
            "disposes by method group",
            "sealed class P : IDisposable { private readonly ManualResetEventSlim _gate = new(false); "
            + "static void CleanUp(Action action) => action(); public void Park() => _gate.Wait(); "
            + "public void Dispose() { CleanUp(_gate.Dispose); } }",
            "3a"
        },
        {
            "disposes by a using in Dispose",
            "sealed class P : IDisposable { private readonly SemaphoreSlim _gate = new(0); public void Park() => _gate.Wait(); "
            + "public void Dispose() { using (_gate) { _gate.Release(); } } }",
            "3a"
        },
        {
            "disposes a gate a using statement declares, with a worker on it",
            "sealed class P : IDisposable { public void Dispose() { using (var gate = new ManualResetEventSlim(false)) "
            + "{ _ = Task.Run(() => gate.Wait()); gate.Set(); } } }",
            "3a"
        },
        {
            "disposes the second gate of a using statement",
            "sealed class P : IDisposable { public void Dispose() { using (ManualResetEventSlim first = new(false), second = new(false)) "
            + "{ _ = Task.Run(() => second.Wait()); second.Set(); } } }",
            "3a"
        },
        {
            "disposes a private gate a getter answers",
            "sealed class P : IDisposable { private readonly ManualResetEventSlim _gate = new(false); "
            + "public ManualResetEventSlim Gate => _gate; public void Dispose() { _gate.Dispose(); } } "
            + "sealed class User { void Use(P p) => p.Gate.Wait(); }",
            "3a"
        },
        {
            "disposes a private gate a method returns",
            "sealed class P : IDisposable { private readonly ManualResetEventSlim _gate = new(false); "
            + "public ManualResetEventSlim Park() { return _gate; } public void Dispose() { _gate.Dispose(); } }",
            "3a"
        },
        {
            "disposes a private gate an iterator yields",
            "sealed class P : IDisposable { private readonly ManualResetEventSlim _gate = new(false); "
            + "public IEnumerable<ManualResetEventSlim> Gates() { yield return _gate; } public void Dispose() { _gate.Dispose(); } }",
            "3a"
        },
        {
            "disposes a private gate it put in a new array",
            "sealed class P : IDisposable { private readonly ManualResetEventSlim _gate = new(false); "
            + "public ManualResetEventSlim[] Gates() { ManualResetEventSlim[] all = [_gate]; return all; } public void Dispose() { _gate.Dispose(); } }",
            "3a"
        },
        {
            "disposes through a null check and a pattern variable",
            "sealed class P : IDisposable { private ManualResetEventSlim? _gate = new(false); "
            + "ManualResetEventSlim? Held() => _gate; public void Park() => _gate?.Wait(); "
            + "public void Dispose() { if (Held() is { } held) { held.Dispose(); } } }",
            "3a"
        },
        {
            "disposes a gate a public member hands out",
            "sealed class P : IDisposable { public AutoResetEvent Gate { get; } = new(false); "
            + "public void Dispose() { Gate?.Dispose(); } } "
            + "sealed class User { void Use(P p) => p.Gate.WaitOne(); }",
            "3a"
        },
        {
            "disposes through a cast",
            "sealed class P : IDisposable { private readonly ManualResetEventSlim _gate = new(false); public void Park() => _gate.Wait(); "
            + "public void Dispose() { _gate.Set(); ((IDisposable)_gate).Dispose(); } }",
            "3a"
        },
        {
            "disposes a kernel event",
            "sealed class P : IDisposable { private readonly EventWaitHandle _gate = new(false, EventResetMode.ManualReset); "
            + "public void Park() => _gate.WaitOne(); public void Dispose() { _gate.Set(); _gate.Dispose(); } }",
            "3a"
        },
        {
            "disposes through a downcast",
            "sealed class P : IDisposable { private readonly object _gate = new ManualResetEventSlim(false); "
            + "public void Park() => ((ManualResetEventSlim)_gate).Wait(); "
            + "public void Dispose() { ((ManualResetEventSlim)_gate).Dispose(); } }",
            "3a"
        },
        {
            "disposes the gates of a list it only adds to",
            "sealed class P : IDisposable { private readonly List<ManualResetEventSlim> _gates = []; "
            + "public Action Park() { var gate = new ManualResetEventSlim(false); _gates.Add(gate); return () => gate.Wait(); } "
            + "public void Dispose() { foreach (ManualResetEventSlim gate in _gates) { gate.Dispose(); } } }",
            "3a"
        },
        {
            "disposes a gate kept in a field after its hook took the local",
            "sealed class P : IDisposable { private ManualResetEventSlim? _gate; "
            + "public Action Park() { var gate = new ManualResetEventSlim(false); _gate = gate; return () => gate.Wait(); } "
            + "public void Dispose() { _gate?.Dispose(); } }",
            "3a"
        },
        {
            "a second release after the drain",
            "sealed class P : IDisposable { private readonly ManualResetEventSlim _first = new(false); "
            + "private readonly ManualResetEventSlim _second = new(false); private readonly VaultSession _session; "
            + "public P(VaultSession session) { _session = session; } public Task Work = Task.CompletedTask; "
            + "public void Dispose() { _first.Set(); PumpedDispatcher.PumpUntilDrained(Work); _second.Set(); _session.Dispose(); } }",
            "3b"
        },
        {
            "a second session after a release the first drain did not cover",
            "sealed class P : IDisposable { private readonly ManualResetEventSlim _first = new(false); "
            + "private readonly ManualResetEventSlim _second = new(false); private readonly VaultSession _a; private readonly VaultSession _b; "
            + "public P(VaultSession a, VaultSession b) { _a = a; _b = b; } public Task Work = Task.CompletedTask; "
            + "public void Dispose() { _first.Set(); PumpedDispatcher.PumpUntilDrained(Work); _a.Dispose(); _second.Set(); _b.Dispose(); } }",
            "3b"
        },
        {
            "a second session inside a nested fixture, after an undrained release",
            "sealed class P : IDisposable { private readonly ManualResetEventSlim _first = new(false); "
            + "private readonly ManualResetEventSlim _second = new(false); private readonly VaultSession _a; private readonly Inner _inner; "
            + "public P(VaultSession a, VaultSession b) { _a = a; _inner = new Inner(b); } public Task Work = Task.CompletedTask; "
            + "public void Dispose() { _first.Set(); PumpedDispatcher.PumpUntilDrained(Work); _a.Dispose(); _second.Set(); _inner.Dispose(); } "
            + "private sealed class Inner(VaultSession session) : IDisposable { public void Dispose() => session.Dispose(); } }",
            "3b"
        },
        {
            "two sessions after one drained release",
            "sealed class P : IDisposable { private readonly ManualResetEventSlim _release = new(false); private readonly VaultSession _a; private readonly VaultSession _b; "
            + "public P(VaultSession a, VaultSession b) { _a = a; _b = b; } public Task Work = Task.CompletedTask; "
            + "public void Dispose() { _release.Set(); PumpedDispatcher.PumpUntilDrained(Work); _a.Dispose(); _b.Dispose(); } }",
            "clean"
        },
        {
            "a session disposed through a cast",
            "sealed class P : IDisposable { private readonly ManualResetEventSlim _release = new(false); private readonly VaultSession _session; "
            + "public P(VaultSession session) { _session = session; } public void Dispose() { _release.Set(); ((IDisposable)_session).Dispose(); } }",
            "3b"
        },
        {
            "sets a gate, then disposes a session",
            "sealed class P : IDisposable { private readonly ManualResetEventSlim _release = new(false); private readonly VaultSession _session; "
            + "public P(VaultSession session) { _session = session; } public void Dispose() { _release.Set(); _session.Dispose(); } }",
            "3b"
        },
        {
            "the drain after the session",
            "sealed class P : IDisposable { private readonly ManualResetEventSlim _release = new(false); private readonly VaultSession _session; "
            + "public P(VaultSession session) { _session = session; } public Task Work = Task.CompletedTask; "
            + "public void Dispose() { _release.Set(); _session.Dispose(); PumpedDispatcher.PumpUntilDrained(Work); } }",
            "3b"
        },
        {
            "only PumpedDispatcher.Drain() between",
            "sealed class P : IDisposable { private readonly ManualResetEventSlim _release = new(false); private readonly VaultSession _session; "
            + "public P(VaultSession session) { _session = session; } "
            + "public void Dispose() { _release.Set(); PumpedDispatcher.Drain(); _session?.Dispose(); } }",
            "3b"
        },
        {
            "the session inside a nested fixture",
            "sealed class P : IDisposable { private readonly ManualResetEventSlim _release = new(false); private readonly Inner _inner; "
            + "public P(VaultSession session) { _inner = new Inner(session); } public void Dispose() { _release.Set(); _inner.Dispose(); } "
            + "private sealed class Inner(VaultSession session) : IDisposable { public void Dispose() => session.Dispose(); } }",
            "3b"
        },
        {
            "a completion source released, then a session",
            "sealed class P : IDisposable { private readonly TaskCompletionSource _release = new(); private readonly VaultSession _session; "
            + "public P(VaultSession session) { _session = session; } public void Dispose() { _ = _release.TrySetResult(); _session.Dispose(); } }",
            "3b"
        },
        {
            "held work released, then a session",
            "sealed class P : IDisposable { private readonly HeldWork _held; private readonly VaultSession _session; "
            + "public P(VaultSession session, HeldWork held) { _session = session; _held = held; } "
            + "public void Dispose() { _held.Release(); _session.Dispose(); } }",
            "3b"
        },
        {
            "a park's own Dispose releases, then a session",
            "sealed class P : IDisposable { private readonly Park _park = new(); private readonly VaultSession _session; "
            + "public P(VaultSession session) { _session = session; } public void Dispose() { _park.Dispose(); _session.Dispose(); } "
            + "private sealed class Park : IDisposable { private readonly ManualResetEventSlim _release = new(false); public void Dispose() => _release.Set(); } }",
            "3b"
        },
        {
            "released through a helper, the session through another, a lifecycle",
            "sealed class P : IDisposable { private readonly ManualResetEventSlim _release = new(false); private readonly VaultLifecycleViewModel _lifecycle; "
            + "public P(VaultLifecycleViewModel lifecycle) { _lifecycle = lifecycle; } void Release() => _release.Set(); void Close() => _lifecycle.Dispose(); "
            + "public void Dispose() { Release(); Close(); } }",
            "3b"
        },
        {
            "Dispose() => _release.Set()",
            "sealed class P : IDisposable { private readonly ManualResetEventSlim _release = new(false); "
            + "public void Hook() => _release.Wait(); public void Dispose() => _release.Set(); }",
            "clean"
        },
        {
            "a TaskCompletionSource release",
            "sealed class P : IDisposable { private readonly TaskCompletionSource _released = new(); "
            + "public void Hook() => _released.Task.Wait(); public void Dispose() => _released.TrySetResult(); }",
            "clean"
        },
        {
            "release, PumpUntilDrained, session",
            "sealed class P : IDisposable { private readonly ManualResetEventSlim _release = new(false); private readonly VaultSession _session; "
            + "public P(VaultSession session) { _session = session; } public Task Work = Task.CompletedTask; "
            + "public void Dispose() { _release.Set(); PumpedDispatcher.PumpUntilDrained(Work); _session.Dispose(); } }",
            "clean"
        },
        {
            "release, the witness's drain, session",
            "sealed class P : IDisposable { private readonly ManualResetEventSlim _release = new(false); private readonly VaultSession _session; "
            + "private readonly TeardownWitness _teardown = new(); public P(VaultSession session) { _session = session; } "
            + "public void Dispose() { _release.Set(); _teardown.DrainBeforeTheSessionGoes(() => Task.CompletedTask); _session.Dispose(); _teardown.SessionGone(); } }",
            "clean"
        },
        {
            "a session with no release",
            "sealed class P : IDisposable { private readonly VaultSession _session; "
            + "public P(VaultSession session) { _session = session; } public void Dispose() { _session.Dispose(); } }",
            "clean"
        },
        {
            "a using event in a fact body",
            "sealed class P { public void AFact() { using var reached = new ManualResetEventSlim(false); reached.Set(); "
            + "Assert.True(reached.Wait(TimeSpan.FromSeconds(1))); } }",
            "clean"
        },
        {
            "a gate nobody sets or waits on",
            "sealed class P : IDisposable { private readonly ManualResetEventSlim _unused = new(false); "
            + "public void Dispose() { _unused.Dispose(); } }",
            "clean"
        },
    };

    /// <summary>Rule 3a: every <c>Dispose</c> or <c>DisposeAsync</c> that
    /// disposes a gate its type sets, releases or waits on.</summary>
    internal static IReadOnlyList<Violation> GatesDisposedInTeardown(
        CSharpCompilation compilation, IReadOnlyList<SyntaxTree> sources, Func<SyntaxTree, string> fileOf)
    {
        var teardowns = new Teardowns(compilation);
        List<Violation> found = [];
        foreach ((INamedTypeSymbol type, MethodDeclarationSyntax method) in teardowns.DisposeMethods(sources))
        {
            foreach (Step step in teardowns.StepsOf(method).Where(step => step.Kind == StepKind.GateDispose))
            {
                if (teardowns.TypeUses(type, step.Gate!))
                {
                    found.Add(new Violation(
                        fileOf(method.SyntaxTree),
                        type.Name,
                        LineOf(step.Site),
                        $"{method.Identifier.ValueText} disposes {step.Text}, a gate the type sets, releases or waits on"));
                }
            }
        }
        return found;
    }

    /// <summary>Rule 3b: every <c>Dispose</c> or <c>DisposeAsync</c> that
    /// releases a gate and later tears a session down with no drain between
    /// the last release before that teardown and the teardown itself. Every
    /// teardown in the method is checked, each against the release nearest
    /// before it.</summary>
    internal static IReadOnlyList<Violation> ReleasesWithNoDrainBeforeTheSession(
        CSharpCompilation compilation, IReadOnlyList<SyntaxTree> sources, Func<SyntaxTree, string> fileOf)
    {
        var teardowns = new Teardowns(compilation);
        List<Violation> found = [];
        foreach ((INamedTypeSymbol type, MethodDeclarationSyntax method) in teardowns.DisposeMethods(sources))
        {
            IReadOnlyList<Step> steps = teardowns.StepsOf(method);
            // Every release wants a drain after it and before the next
            // session that goes: walking in order, a release clears the
            // drain, a drain sets it, and a teardown asks for it.
            int released = -1;
            bool drained = false;
            for (int index = 0; index < steps.Count; index++)
            {
                switch (steps[index].Kind)
                {
                    case StepKind.Release:
                        released = index;
                        drained = false;
                        break;
                    case StepKind.Drain:
                        drained = true;
                        break;
                    case StepKind.Teardown when released >= 0 && !drained:
                        found.Add(new Violation(
                            fileOf(method.SyntaxTree),
                            type.Name,
                            LineOf(steps[index].Site),
                            $"{method.Identifier.ValueText} releases {steps[released].Text} (line {LineOf(steps[released].Site)}) "
                            + $"and then tears the session down through {steps[index].Text}, with no drain between the two"));
                        break;
                }
            }
        }
        return found;
    }

    private static int LineOf(SyntaxNode node) => node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

    private enum StepKind
    {
        /// <summary>A gate is set or released, a completion source is
        /// completed, or held work is let go.</summary>
        Release,

        /// <summary>A gate is disposed.</summary>
        GateDispose,

        /// <summary>A session, or a type that owns one, is disposed.</summary>
        Teardown,

        /// <summary>A call that waits for work on the pool.</summary>
        Drain,
    }

    /// <summary>One thing a teardown does, where the teardown's own source
    /// does it (the call site, for a step made inside a helper).</summary>
    private sealed record Step(StepKind Kind, int Position, SyntaxNode Site, string Text, Identity? Gate = null);

    /// <summary>Which gate an expression is, as far as binding can say: the
    /// members, locals and parameters it can come from, or unknown.</summary>
    private sealed class Identity
    {
        public HashSet<ISymbol> Symbols { get; } = new(SymbolEqualityComparer.Default);

        public bool Unknown { get; set; }

        public void Add(Identity other)
        {
            Symbols.UnionWith(other.Symbols);
            Unknown |= other.Unknown;
        }

        public bool Meets(Identity other) =>
            Unknown || other.Unknown || Symbols.Overlaps(other.Symbols);
    }

    /// <summary>What the teardowns of one compilation do, bound.</summary>
    private sealed class Teardowns(CSharpCompilation compilation)
    {
        private readonly Dictionary<SyntaxTree, SemanticModel> _models = [];
        private readonly Dictionary<INamedTypeSymbol, (bool Releases, bool TearsDown)> _summaries =
            new(SymbolEqualityComparer.Default);
        private readonly HashSet<INamedTypeSymbol> _summarizing = new(SymbolEqualityComparer.Default);
        private readonly Dictionary<INamedTypeSymbol, List<Identity>> _uses = new(SymbolEqualityComparer.Default);
        private readonly Dictionary<INamedTypeSymbol, List<Identity>> _handedOn = new(SymbolEqualityComparer.Default);

        private SemanticModel ModelOf(SyntaxNode node)
        {
            if (!_models.TryGetValue(node.SyntaxTree, out SemanticModel? model))
            {
                model = compilation.GetSemanticModel(node.SyntaxTree);
                _models[node.SyntaxTree] = model;
            }
            return model;
        }

        /// <summary>Every method named <c>Dispose</c> or <c>DisposeAsync</c>
        /// with a body, and the type it belongs to.</summary>
        public IEnumerable<(INamedTypeSymbol Type, MethodDeclarationSyntax Method)> DisposeMethods(
            IEnumerable<SyntaxTree> sources)
        {
            foreach (SyntaxTree tree in sources)
            {
                foreach (MethodDeclarationSyntax method in tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>())
                {
                    if (IsDisposeName(method.Identifier.ValueText)
                        && BodyOf(method) is not null
                        && ModelOf(method).GetDeclaredSymbol(method) is { ContainingType: { } type })
                    {
                        yield return (type, method);
                    }
                }
            }
        }

        private static bool IsDisposeName(string name) => name is "Dispose" or "DisposeAsync";

        private static SyntaxNode? BodyOf(SyntaxNode method) => method switch
        {
            BaseMethodDeclarationSyntax declared => (SyntaxNode?)declared.Body ?? declared.ExpressionBody,
            LocalFunctionStatementSyntax local => (SyntaxNode?)local.Body ?? local.ExpressionBody,
            _ => null,
        };

        /// <summary>The steps of one teardown, in source order, with what a
        /// test method it calls does put where the teardown calls it (one
        /// level; a local function of the teardown is part of it).</summary>
        public IReadOnlyList<Step> StepsOf(MethodDeclarationSyntax method)
        {
            List<Step> steps = [];
            if (BodyOf(method) is { } body)
            {
                Collect(body, site: null, position: null, levels: 1, [method], steps);
            }
            return [.. steps
                .Select((step, index) => (step, index))
                .OrderBy(pair => pair.step.Position)
                .ThenBy(pair => pair.index)
                .Select(pair => pair.step)];
        }

        private void Collect(
            SyntaxNode body,
            SyntaxNode? site,
            int? position,
            int levels,
            HashSet<SyntaxNode> entered,
            List<Step> steps)
        {
            SemanticModel model = ModelOf(body);
            foreach (SyntaxNode node in body.DescendantNodesAndSelf(inner => inner is not LocalFunctionStatementSyntax))
            {
                switch (node)
                {
                    case InvocationExpressionSyntax invocation:
                        Classify(invocation, invocation.Expression);
                        break;
                    // A method group handed on (CleanUp(gate.Dispose),
                    // CleanUp(Release)) does what its call would.
                    case MemberAccessExpressionSyntax { Parent: not InvocationExpressionSyntax } group
                        when !IsInsideNameof(group) && model.GetSymbolInfo(group).Symbol is IMethodSymbol:
                        Classify(group, group);
                        break;
                    case IdentifierNameSyntax { Parent: not MemberAccessExpressionSyntax and not MemberBindingExpressionSyntax and not InvocationExpressionSyntax } name
                        when !IsInsideNameof(name) && model.GetSymbolInfo(name).Symbol is IMethodSymbol:
                        Classify(name, name);
                        break;
                    case UsingStatementSyntax used:
                        if (used.Expression is { } resource)
                        {
                            Disposed(used, used.Span.End, resource, TypeReachedOn(resource, model), null, "using " + resource);
                        }
                        foreach (VariableDeclaratorSyntax variable in used.Declaration?.Variables ?? [])
                        {
                            if (variable.Initializer?.Value is { } value
                                && model.GetDeclaredSymbol(variable) is ILocalSymbol local)
                            {
                                Disposed(used, used.Span.End, value, local.Type, local, "using " + variable.Identifier.ValueText);
                            }
                        }
                        break;
                    case LocalDeclarationStatementSyntax { UsingKeyword.RawKind: not 0 } declared:
                        foreach (VariableDeclaratorSyntax variable in declared.Declaration.Variables)
                        {
                            if (variable.Initializer?.Value is { } value
                                && model.GetDeclaredSymbol(variable) is ILocalSymbol local)
                            {
                                int end = declared.Parent?.Span.End ?? declared.Span.End;
                                Disposed(declared, end, value, local.Type, local, "using " + variable.Identifier.ValueText);
                            }
                        }
                        break;
                }
            }

            void Add(StepKind kind, SyntaxNode at, int? natural, string text, Identity? gate = null) =>
                steps.Add(new Step(kind, position ?? natural ?? at.SpanStart, site ?? at, text, gate));

            void Disposed(SyntaxNode at, int? natural, ExpressionSyntax target, ITypeSymbol? type, ILocalSymbol? local, string text)
            {
                if (IsGate(type))
                {
                    Identity identity = IdentityOf(target, ModelOf(target), 0);
                    if (local is not null)
                    {
                        _ = identity.Symbols.Add(local);
                    }
                    Add(StepKind.GateDispose, at, natural, target.ToString(), identity);
                }
                else if (IsSession(type))
                {
                    Add(StepKind.Teardown, at, natural, text);
                }
                else if (type is INamedTypeSymbol named && SummaryOf(named) is var summary)
                {
                    if (summary.TearsDown)
                    {
                        Add(StepKind.Teardown, at, natural, text);
                    }
                    else if (summary.Releases)
                    {
                        Add(StepKind.Release, at, natural, text);
                    }
                }
            }

            void Classify(SyntaxNode at, ExpressionSyntax callee)
            {
                ISymbol? bound = model.GetSymbolInfo(at).Symbol ?? model.GetSymbolInfo(at).CandidateSymbols.FirstOrDefault();
                var symbol = bound as IMethodSymbol;
                string? name = symbol?.Name ?? callee switch
                {
                    MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
                    MemberBindingExpressionSyntax binding => binding.Name.Identifier.ValueText,
                    IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
                    _ => null,
                };
                if (name is null)
                {
                    return;
                }
                ExpressionSyntax? receiver = ReceiverOf(callee);
                ITypeSymbol? type = receiver is not null
                    ? TypeReachedOn(receiver, model)
                    : symbol is { IsStatic: false } ? symbol.ContainingType : null;
                string text = string.Join(' ', at.ToString().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
                if (text.Length > 80)
                {
                    text = text[..80] + "...";
                }

                if (Drains.Contains(name))
                {
                    Add(StepKind.Drain, at, null, text);
                }
                if (IsDisposeName(name) && receiver is not null)
                {
                    // Another object's Dispose stands for what it does as a
                    // whole; its own teardown is judged where it is declared.
                    Disposed(at, null, receiver, type, null, text);
                    return;
                }
                if (IsRelease(name, type))
                {
                    Add(StepKind.Release, at, null, text);
                }

                // What a test method does stands where it is called: one
                // level, and a local function of the teardown is part of it.
                if (symbol is null || symbol.DeclaringSyntaxReferences.IsDefaultOrEmpty)
                {
                    return;
                }
                bool isLocal = symbol.MethodKind == MethodKind.LocalFunction;
                if (!isLocal && levels == 0)
                {
                    return;
                }
                foreach (SyntaxReference reference in symbol.DeclaringSyntaxReferences)
                {
                    SyntaxNode declaration = reference.GetSyntax();
                    if (BodyOf(declaration) is { } inner && entered.Add(declaration))
                    {
                        Collect(inner, site ?? at, position ?? at.SpanStart, isLocal ? levels : levels - 1, entered, steps);
                        _ = entered.Remove(declaration);
                    }
                }
            }
        }

        /// <summary>What an expression is, for a call made on it: the type it
        /// is written as when that names a gate, a session or a test type,
        /// and otherwise the type under its casts. So
        /// <c>((IDisposable)gate)</c> is the gate, and so is
        /// <c>((ManualResetEventSlim)handle)</c>.</summary>
        private static ITypeSymbol? TypeReachedOn(ExpressionSyntax written, SemanticModel model)
        {
            ITypeSymbol? stated = model.GetTypeInfo(written).Type;
            if (SaysWhatItIs(stated))
            {
                return stated;
            }
            ITypeSymbol? under = model.GetTypeInfo(Uncast(written)).Type;
            return SaysWhatItIs(under) ? under : stated;
        }

        private static bool SaysWhatItIs(ITypeSymbol? type) =>
            IsGate(type) || IsSession(type) || type is INamedTypeSymbol { DeclaringSyntaxReferences.IsDefaultOrEmpty: false };

        /// <summary>The expression under parentheses, casts, <c>as</c> and
        /// the null-forgiving mark.</summary>
        private static ExpressionSyntax Uncast(ExpressionSyntax expression)
        {
            while (true)
            {
                switch (expression)
                {
                    case ParenthesizedExpressionSyntax parenthesized:
                        expression = parenthesized.Expression;
                        continue;
                    case PostfixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.SuppressNullableWarningExpression } forgiven:
                        expression = forgiven.Operand;
                        continue;
                    case CastExpressionSyntax cast:
                        expression = cast.Expression;
                        continue;
                    case BinaryExpressionSyntax { RawKind: (int)SyntaxKind.AsExpression } converted:
                        expression = converted.Left;
                        continue;
                    default:
                        return expression;
                }
            }
        }

        private static bool IsInsideNameof(SyntaxNode node) => node.Ancestors()
            .OfType<InvocationExpressionSyntax>()
            .Any(call => call.Expression is IdentifierNameSyntax { Identifier.ValueText: "nameof" });

        /// <summary>The expression a member is reached on: <c>a</c> in
        /// <c>a.B</c> and in <c>a?.B</c>; nothing for a bare name.</summary>
        private static ExpressionSyntax? ReceiverOf(ExpressionSyntax callee) => callee switch
        {
            MemberAccessExpressionSyntax access => access.Expression,
            MemberBindingExpressionSyntax binding => binding.Ancestors()
                .OfType<ConditionalAccessExpressionSyntax>()
                .FirstOrDefault(conditional => conditional.WhenNotNull.Span.Contains(binding.Span))?.Expression,
            _ => null,
        };

        private static bool Inherits(ITypeSymbol? type, Func<INamedTypeSymbol, bool> test)
        {
            for (ITypeSymbol? current = type; current is not null; current = current.BaseType)
            {
                if (current is INamedTypeSymbol named && test(named))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool IsGate(ITypeSymbol? type) => Inherits(
            type,
            named => GateTypes.Contains(named.Name) && named.ContainingNamespace?.ToDisplayString() == "System.Threading");

        private static bool IsSession(ITypeSymbol? type) => Inherits(type, named => SessionTypes.Contains(named.Name));

        private static bool IsRelease(string name, ITypeSymbol? type) =>
            (GateReleases.Contains(name) && IsGate(type))
            || (CompletionReleases.Contains(name) && Inherits(type, named => named.Name == "TaskCompletionSource"))
            || (name == "Release" && Inherits(type, named => named.Name == "HeldWork"));

        /// <summary>What disposing a test type does, by its own
        /// <c>Dispose</c>: lets held work go, tears a session down.</summary>
        private (bool Releases, bool TearsDown) SummaryOf(INamedTypeSymbol type)
        {
            type = type.OriginalDefinition;
            if (_summaries.TryGetValue(type, out (bool Releases, bool TearsDown) known))
            {
                return known;
            }
            if (!_summarizing.Add(type))
            {
                return (false, false);
            }
            bool releases = false;
            bool tearsDown = false;
            for (INamedTypeSymbol? declaring = type; declaring is not null; declaring = declaring.BaseType?.OriginalDefinition)
            {
                foreach (ISymbol member in declaring.GetMembers())
                {
                    if (member is not IMethodSymbol { MethodKind: MethodKind.Ordinary or MethodKind.ExplicitInterfaceImplementation } method)
                    {
                        continue;
                    }
                    foreach (SyntaxReference reference in method.DeclaringSyntaxReferences)
                    {
                        if (reference.GetSyntax() is MethodDeclarationSyntax declared && IsDisposeName(declared.Identifier.ValueText))
                        {
                            IReadOnlyList<Step> steps = StepsOf(declared);
                            releases |= steps.Any(step => step.Kind == StepKind.Release);
                            tearsDown |= steps.Any(step => step.Kind == StepKind.Teardown);
                        }
                    }
                }
            }
            _ = _summarizing.Remove(type);
            _summaries[type] = (releases, tearsDown);
            return (releases, tearsDown);
        }

        /// <summary>Whether <paramref name="type"/> (with the test class
        /// around it, whose facts use the fixture) sets, releases or waits
        /// on <paramref name="gate"/>, or hands it to code that can.</summary>
        public bool TypeUses(INamedTypeSymbol type, Identity gate)
        {
            // A gate a member hands out is set or waited on by whoever takes it.
            if (gate.Symbols.Any(symbol =>
                symbol is IFieldSymbol or IPropertySymbol && symbol.DeclaredAccessibility != Microsoft.CodeAnalysis.Accessibility.Private))
            {
                return true;
            }
            INamedTypeSymbol outermost = type;
            while (outermost.ContainingType is { } containing)
            {
                outermost = containing;
            }
            if (!_uses.TryGetValue(outermost, out List<Identity>? uses))
            {
                uses = [.. UsesIn(outermost, handedOn: false)];
                _uses[outermost] = uses;
            }
            if (!_handedOn.TryGetValue(type, out List<Identity>? handedOn))
            {
                handedOn = [.. UsesIn(type, handedOn: true)];
                _handedOn[type] = handedOn;
            }
            return uses.Any(gate.Meets) || handedOn.Any(gate.Meets);
        }

        /// <summary>The gates the declarations of <paramref name="type"/> set,
        /// release or wait on; or, with <paramref name="handedOn"/>, the
        /// gates they hand to other code (an argument, a seam's property),
        /// which can then wait on them.</summary>
        private IEnumerable<Identity> UsesIn(INamedTypeSymbol type, bool handedOn)
        {
            foreach (SyntaxReference reference in type.DeclaringSyntaxReferences)
            {
                SyntaxNode declaration = reference.GetSyntax();
                SemanticModel model = ModelOf(declaration);
                foreach (SyntaxNode node in declaration.DescendantNodes())
                {
                    switch (node)
                    {
                        // gate.Set(), gate.Wait(...), gate.Set as a method group.
                        case MemberAccessExpressionSyntax access
                            when !handedOn
                                && GateUses.Contains(access.Name.Identifier.ValueText)
                                && IsGate(TypeReachedOn(access.Expression, model)):
                            yield return IdentityOf(access.Expression, model, 0);
                            break;
                        case MemberBindingExpressionSyntax binding
                            when !handedOn
                                && GateUses.Contains(binding.Name.Identifier.ValueText)
                                && ReceiverOf(binding) is { } receiver
                                && IsGate(TypeReachedOn(receiver, model)):
                            yield return IdentityOf(receiver, model, 0);
                            break;
                        case ArgumentSyntax { Expression: { } passed } argument
                            when handedOn && IsGate(TypeReachedOn(passed, model)):
                            {
                                Identity handed = IdentityOf(passed, model, 0);
                                // A gate put into a collection (_gates.Add(gate))
                                // is one of that collection's elements from
                                // then on.
                                if (argument.Parent?.Parent is InvocationExpressionSyntax into
                                    && ReceiverOf(into.Expression) is { } holder
                                    && !IsGate(TypeReachedOn(holder, model)))
                                {
                                    handed.Add(IdentityOf(holder, model, 0));
                                }
                                yield return handed;
                                break;
                            }
                        // A gate a member answers (a getter, a method's
                        // return, a yield) or puts in a new collection goes
                        // to whoever asks.
                        case ReturnStatementSyntax { Expression: { } returned }
                            when handedOn && IsGate(TypeReachedOn(returned, model)):
                            yield return IdentityOf(returned, model, 0);
                            break;
                        case YieldStatementSyntax { Expression: { } yielded }
                            when handedOn && IsGate(TypeReachedOn(yielded, model)):
                            yield return IdentityOf(yielded, model, 0);
                            break;
                        case ArrowExpressionClauseSyntax { Expression: { } answered }
                            when handedOn && IsGate(TypeReachedOn(answered, model)):
                            yield return IdentityOf(answered, model, 0);
                            break;
                        case ExpressionSyntax { Parent: InitializerExpressionSyntax or ExpressionElementSyntax } element
                            when handedOn && IsGate(TypeReachedOn(element, model)):
                            yield return IdentityOf(element, model, 0);
                            break;
                        case AssignmentExpressionSyntax { Right: { } assigned } assignment
                            when handedOn
                                && IsGate(TypeReachedOn(assigned, model))
                                && model.GetSymbolInfo(assignment.Left).Symbol is not ILocalSymbol:
                            {
                                // A gate kept in a member is that member from
                                // then on.
                                Identity kept = IdentityOf(assigned, model, 0);
                                kept.Add(IdentityOf(assignment.Left, model, 0));
                                yield return kept;
                                break;
                            }
                    }
                }
            }
        }

        /// <summary>Which gate <paramref name="expression"/> is: a member,
        /// an element of a member collection, or a local followed back to
        /// where it came from. Anything binding cannot follow is unknown,
        /// which meets every gate.</summary>
        private Identity IdentityOf(ExpressionSyntax expression, SemanticModel model, int depth)
        {
            var identity = new Identity();
            if (depth > 6)
            {
                identity.Unknown = true;
                return identity;
            }
            expression = Uncast(expression);

            switch (expression)
            {
                case BaseObjectCreationExpressionSyntax:
                case LiteralExpressionSyntax:
                case DefaultExpressionSyntax:
                    return identity;
                case ConditionalExpressionSyntax conditional:
                    identity.Add(IdentityOf(conditional.WhenTrue, model, depth + 1));
                    identity.Add(IdentityOf(conditional.WhenFalse, model, depth + 1));
                    return identity;
                case BinaryExpressionSyntax { RawKind: (int)SyntaxKind.CoalesceExpression } coalesce:
                    identity.Add(IdentityOf(coalesce.Left, model, depth + 1));
                    identity.Add(IdentityOf(coalesce.Right, model, depth + 1));
                    return identity;
                case AssignmentExpressionSyntax assignment:
                    return IdentityOf(assignment.Right, model, depth + 1);
                case ElementAccessExpressionSyntax element:
                    return IdentityOf(element.Expression, model, depth + 1);
                case ConditionalAccessExpressionSyntax conditionalAccess:
                    // a?.B and a?[i] are reached on a.
                    if (conditionalAccess.WhenNotNull is MemberBindingExpressionSyntax member
                        && model.GetSymbolInfo(member).Symbol is IFieldSymbol or IPropertySymbol)
                    {
                        _ = identity.Symbols.Add(model.GetSymbolInfo(member).Symbol!);
                        return identity;
                    }
                    return IdentityOf(conditionalAccess.Expression, model, depth + 1);
                case InvocationExpressionSyntax invocation:
                    return IdentityOfCall(invocation, model, depth);
            }

            switch (model.GetSymbolInfo(expression).Symbol)
            {
                case IFieldSymbol field:
                    _ = identity.Symbols.Add(field);
                    return identity;
                case IPropertySymbol property:
                    _ = identity.Symbols.Add(property);
                    foreach (ExpressionSyntax returned in ReturnsOf(property))
                    {
                        identity.Add(IdentityOf(returned, ModelOf(returned), depth + 1));
                    }
                    return identity;
                case ILocalSymbol local:
                    _ = identity.Symbols.Add(local);
                    identity.Unknown |= !FollowsItsSource(local);
                    foreach (ExpressionSyntax source in SourcesOf(local))
                    {
                        identity.Add(IdentityOf(source, ModelOf(source), depth + 1));
                    }
                    return identity;
                case IParameterSymbol parameter:
                    // A lambda's parameter over a collection of gates is one of
                    // its elements; any other parameter is whatever the caller
                    // passed.
                    if (parameter.ContainingSymbol is IMethodSymbol { MethodKind: MethodKind.AnonymousFunction }
                        && parameter.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is { } declared
                        && declared.Ancestors().OfType<AnonymousFunctionExpressionSyntax>().FirstOrDefault() is { Parent: ArgumentSyntax { Parent.Parent: InvocationExpressionSyntax over } }
                        && ReceiverOf(over.Expression) is { } collection)
                    {
                        return IdentityOf(collection, model, depth + 1);
                    }
                    identity.Unknown = true;
                    return identity;
                default:
                    identity.Unknown = true;
                    return identity;
            }
        }

        private Identity IdentityOfCall(InvocationExpressionSyntax invocation, SemanticModel model, int depth)
        {
            var identity = new Identity();
            if (model.GetSymbolInfo(invocation).Symbol is IMethodSymbol { DeclaringSyntaxReferences.IsDefaultOrEmpty: false } method)
            {
                // A test method that answers a gate: whatever it returns.
                bool any = false;
                foreach (SyntaxReference reference in method.DeclaringSyntaxReferences)
                {
                    SyntaxNode declaration = reference.GetSyntax();
                    foreach (ExpressionSyntax returned in ReturnsIn(BodyOf(declaration)))
                    {
                        any = true;
                        identity.Add(IdentityOf(returned, ModelOf(returned), depth + 1));
                    }
                }
                identity.Unknown |= !any;
                return identity;
            }
            // An element taken from a collection of gates (First, a query).
            if (ReceiverOf(invocation.Expression) is { } receiver && !IsGate(model.GetTypeInfo(receiver).Type))
            {
                return IdentityOf(receiver, model, depth + 1);
            }
            identity.Unknown = true;
            return identity;
        }

        private static IEnumerable<ExpressionSyntax> ReturnsIn(SyntaxNode? body)
        {
            switch (body)
            {
                case ArrowExpressionClauseSyntax arrow:
                    yield return arrow.Expression;
                    break;
                case null:
                    break;
                default:
                    foreach (ReturnStatementSyntax returned in body
                        .DescendantNodes(inner => inner is not (LocalFunctionStatementSyntax or AnonymousFunctionExpressionSyntax))
                        .OfType<ReturnStatementSyntax>())
                    {
                        if (returned.Expression is { } expression)
                        {
                            yield return expression;
                        }
                    }
                    break;
            }
        }

        private static IEnumerable<ExpressionSyntax> ReturnsOf(IPropertySymbol property)
        {
            foreach (SyntaxReference reference in property.DeclaringSyntaxReferences)
            {
                if (reference.GetSyntax() is not PropertyDeclarationSyntax declared)
                {
                    continue;
                }
                if (declared.ExpressionBody is { } arrow)
                {
                    yield return arrow.Expression;
                }
                foreach (AccessorDeclarationSyntax getter in declared.AccessorList?.Accessors
                    .Where(accessor => accessor.Keyword.ValueText == "get") ?? [])
                {
                    foreach (ExpressionSyntax returned in ReturnsIn((SyntaxNode?)getter.ExpressionBody ?? getter.Body))
                    {
                        yield return returned;
                    }
                }
            }
        }

        /// <summary>Whether <see cref="SourcesOf"/> names where the local's
        /// first value comes from. A deconstruction, an <c>out</c> variable
        /// or a switch pattern is not followed.</summary>
        private static bool FollowsItsSource(ILocalSymbol local) =>
            local.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() switch
            {
                VariableDeclaratorSyntax => true,
                ForEachStatementSyntax => true,
                SingleVariableDesignationSyntax designation =>
                    designation.Ancestors().OfType<IsPatternExpressionSyntax>().Any(),
                _ => false,
            };

        /// <summary>Where a local's value comes from: its initializer, the
        /// collection its loop walks, the expression its pattern matched,
        /// and every later assignment to it.</summary>
        private IEnumerable<ExpressionSyntax> SourcesOf(ILocalSymbol local)
        {
            if (local.DeclaringSyntaxReferences.Length == 0)
            {
                yield break;
            }
            SyntaxNode declaration = local.DeclaringSyntaxReferences[0].GetSyntax();
            switch (declaration)
            {
                case VariableDeclaratorSyntax { Initializer.Value: { } value }:
                    yield return value;
                    break;
                case ForEachStatementSyntax loop:
                    yield return loop.Expression;
                    break;
                case SingleVariableDesignationSyntax designation
                    when designation.Ancestors().OfType<IsPatternExpressionSyntax>().FirstOrDefault() is { } matched:
                    yield return matched.Expression;
                    break;
            }
            SyntaxNode? scope = declaration.Ancestors().FirstOrDefault(node =>
                node is BaseMethodDeclarationSyntax or LocalFunctionStatementSyntax or AnonymousFunctionExpressionSyntax or AccessorDeclarationSyntax);
            if (scope is null)
            {
                yield break;
            }
            SemanticModel model = ModelOf(scope);
            foreach (AssignmentExpressionSyntax assignment in scope.DescendantNodes().OfType<AssignmentExpressionSyntax>())
            {
                if (SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(assignment.Left).Symbol, local))
                {
                    yield return assignment.Right;
                }
            }
        }
    }

    // ---- Rules 1 and 2: shared -------------------------------------------
    //
    // Rule 1 pins every fixed delay that stands before an assertion; rule 2
    // pins every elapsed-time ceiling a fact asserts. A busy runner turns
    // either red with the code right. Each rule keeps a list per project of
    // what stands today, by file, member, count and reason, so a new one
    // fails the lane and a fixed one must leave its list.
    //
    // A site's member is the method, local function, constructor or property
    // nearest around it, as StaThreadCensus names one. Two members of one
    // file with one name share a key, and their counts add.
    //
    // What a list keyed by member does not see: a site moved, lengthened or
    // swapped for another inside one member, and which of two rows of one
    // member a site belongs to. The count is all it holds.

    /// <summary>One place a clock rule names: the file (relative to its
    /// project's folder), the member, the line and the source there.</summary>
    internal sealed record ClockSite(string File, string Member, int Line, string Text)
    {
        public override string ToString() => $"{File}:{Line} ({Member}): {Text}";
    }

    /// <summary>The words a fixed delay may be kept under in either
    /// project.</summary>
    private static readonly string[] DelayReasons =
    [
        // Asserts that nothing more happens: no signal ends a wait for an
        // event that must not come.
        "late-arrival",
        // The delay is the scenario: it holds a body, a seam or a gap open.
        "deliberate-hold",
        // The fact must outlast a timeout or a debounce it tests.
        "timeout-under-test",
        // Two writes need different clock ticks.
        "distinct-timestamp",
        // A counted drain-and-yield loop.
        "quiesce",
        // A harness timer that is the feature.
        "watchdog",
    ];

    /// <summary>The gate's own words: a listener's registration settles, and
    /// an entry as it stood when the census first read the gate.</summary>
    private static readonly string[] GateOnlyReasons = ["registration", "unswept"];

    /// <summary>Rule 2's own word: an elapsed ceiling kept because no seam
    /// observes what the call really waited. Its entry names the seam that
    /// would.</summary>
    private const string NoWaitWitness = "no-wait-witness";

    private static readonly HashSet<string> UnitDelayReasons = new(DelayReasons, StringComparer.Ordinal);
    private static readonly HashSet<string> GateDelayReasons = new([.. DelayReasons, .. GateOnlyReasons], StringComparer.Ordinal);
    private static readonly HashSet<string> UnitCeilingReasons = new([.. DelayReasons, NoWaitWitness], StringComparer.Ordinal);
    private static readonly HashSet<string> GateCeilingReasons =
        new([.. DelayReasons, .. GateOnlyReasons, NoWaitWitness], StringComparer.Ordinal);

    private static Func<SyntaxTree, string> FileIn(string project) =>
        tree => Path.GetRelativePath(Path.Combine(TestsRoot(), project), tree.FilePath).Replace('\\', '/');

    /// <summary>What stands between what a rule found and its pinned list.
    /// An entry is <c>(File, Member, Count, Reason)</c>; a reason is one word
    /// of <paramref name="words"/>, or that word, a colon and what it holds.
    /// Two entries may share a member when its sites stay for different
    /// reasons; their counts add.</summary>
    internal static IReadOnlyList<string> PinnedListFaults(
        IReadOnlyList<ClockSite> found,
        IReadOnlyList<(string File, string Member, int Count, string Reason)> pinned,
        IReadOnlySet<string> words)
    {
        List<string> faults = [];
        foreach ((string file, string member, int count, string reason) in pinned)
        {
            string word = reason.Split(':')[0].Trim();
            if (word.Length == 0)
            {
                faults.Add($"reason: {file} ({member}) gives none");
            }
            else if (!words.Contains(word))
            {
                faults.Add($"reason: {file} ({member}) says \"{word}\", which is not one of this list's words "
                    + $"({string.Join(", ", words.Order(StringComparer.Ordinal))})");
            }
            else if (word == NoWaitWitness && (!reason.Contains(':') || reason[(reason.IndexOf(':') + 1)..].Trim().Length == 0))
            {
                faults.Add($"reason: {file} ({member}) says {NoWaitWitness} and names no seam after it");
            }
            if (count < 1)
            {
                faults.Add($"count: {file} ({member}) holds {count}; an entry holds at least one");
            }
        }

        var allowed = pinned
            .GroupBy(entry => (entry.File, entry.Member))
            .ToDictionary(group => group.Key, group => group.Sum(entry => entry.Count));
        var seen = found
            .GroupBy(site => (site.File, site.Member))
            .ToDictionary(group => group.Key, group => group.OrderBy(site => site.Line).ToArray());
        foreach (((string file, string member), ClockSite[] sites) in seen.OrderBy(pair => pair.Key))
        {
            int holds = allowed.GetValueOrDefault((file, member));
            if (sites.Length > holds)
            {
                faults.Add($"new: {file} ({member}) has {sites.Length} and the list holds {holds}:"
                    + string.Concat(sites.Select(site => $"\n    {site.File}:{site.Line}: {site.Text}")));
            }
        }
        foreach (((string file, string member), int holds) in allowed.OrderBy(pair => pair.Key))
        {
            int has = seen.TryGetValue((file, member), out ClockSite[]? sites) ? sites.Length : 0;
            if (has < holds)
            {
                faults.Add($"stale: {file} ({member}) has {has} and the list holds {holds}");
            }
        }
        return faults;
    }

    /// <summary>The loop statements, lambdas and names both clock rules
    /// read the same way.</summary>
    private static class ClockSyntax
    {
        /// <summary>The name a call is made by: <c>Sleep</c> in
        /// <c>Thread.Sleep(...)</c>, <c>PumpFor</c> in <c>host?.PumpFor(...)</c>,
        /// <c>RunDispatcherFor</c> in <c>RunDispatcherFor(...)</c>.</summary>
        public static string? NameCalled(ExpressionSyntax callee) => Bare(callee) switch
        {
            MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
            MemberBindingExpressionSyntax binding => binding.Name.Identifier.ValueText,
            SimpleNameSyntax name => name.Identifier.ValueText,
            _ => null,
        };

        /// <summary>Whether <paramref name="node"/> (a call, a member access
        /// or a creation) is a member of the named type. Bound where the
        /// compilation binds it; where it does not, the receiver's
        /// spelling.</summary>
        public static bool IsMemberOf(SyntaxNode node, SemanticModel model, string type, string inNamespace)
        {
            SymbolInfo info = model.GetSymbolInfo(node);
            if ((info.Symbol ?? info.CandidateSymbols.FirstOrDefault()) is { } symbol)
            {
                return symbol.ContainingType is { } owner
                    && owner.Name == type
                    && owner.ContainingNamespace?.ToDisplayString() == inNamespace;
            }
            ExpressionSyntax? callee = node switch
            {
                InvocationExpressionSyntax call => call.Expression,
                ObjectCreationExpressionSyntax creation => creation.Type,
                ExpressionSyntax expression => expression,
                _ => null,
            };
            return callee switch
            {
                MemberAccessExpressionSyntax { Expression: MemberAccessExpressionSyntax qualified } => qualified.Name.Identifier.ValueText == type,
                MemberAccessExpressionSyntax { Expression: SimpleNameSyntax receiver } => receiver.Identifier.ValueText == type,
                QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText == type,
                SimpleNameSyntax name => node is ObjectCreationExpressionSyntax && name.Identifier.ValueText == type,
                _ => false,
            };
        }

        public static string MemberOf(SyntaxNode node) =>
            node.Ancestors().FirstOrDefault(ancestor =>
                ancestor is BaseMethodDeclarationSyntax or LocalFunctionStatementSyntax or PropertyDeclarationSyntax) switch
            {
                MethodDeclarationSyntax method => method.Identifier.ValueText,
                LocalFunctionStatementSyntax local => local.Identifier.ValueText,
                ConstructorDeclarationSyntax constructor => constructor.Identifier.ValueText,
                PropertyDeclarationSyntax property => property.Identifier.ValueText,
                _ => "<member>",
            };

        /// <summary>The function a node's statements run in: the nearest
        /// lambda, anonymous method, local function, method or accessor.</summary>
        public static SyntaxNode? FunctionAround(SyntaxNode node) => node.Ancestors().FirstOrDefault(IsFunction);

        private static bool IsFunction(SyntaxNode node) =>
            node is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax or BaseMethodDeclarationSyntax or AccessorDeclarationSyntax;

        /// <summary>Whether a loop that can end early stands between
        /// <paramref name="node"/> and the function it runs in.</summary>
        public static bool StandsInAPoll(SyntaxNode node, bool foreachIsAPoll = true)
        {
            foreach (SyntaxNode ancestor in node.Ancestors())
            {
                switch (ancestor)
                {
                    case WhileStatementSyntax or DoStatementSyntax:
                        return true;
                    case CommonForEachStatementSyntax when foreachIsAPoll:
                        return true;
                    case ForStatementSyntax loop when CanEndEarly(loop):
                        return true;
                    case var function when IsFunction(function):
                        return false;
                }
            }
            return false;
        }

        /// <summary>A <c>for</c> cannot end early when its condition is one
        /// comparison of its own loop variable and nothing in its body
        /// leaves it: no <c>break</c> of its own, and no <c>return</c>,
        /// <c>throw</c> or <c>goto</c> outside a nested function.</summary>
        private static bool CanEndEarly(ForStatementSyntax loop)
        {
            if (loop.Condition is null || Bare(loop.Condition) is not BinaryExpressionSyntax comparison
                || !(comparison.Kind() is SyntaxKind.LessThanExpression or SyntaxKind.LessThanOrEqualExpression
                    or SyntaxKind.GreaterThanExpression or SyntaxKind.GreaterThanOrEqualExpression
                    or SyntaxKind.NotEqualsExpression or SyntaxKind.EqualsExpression))
            {
                return true;
            }
            HashSet<string> own = [.. loop.Declaration?.Variables.Select(variable => variable.Identifier.ValueText) ?? []];
            own.UnionWith(loop.Initializers
                .OfType<AssignmentExpressionSyntax>()
                .Select(assignment => Bare(assignment.Left))
                .OfType<IdentifierNameSyntax>()
                .Select(name => name.Identifier.ValueText));
            bool readsItsOwn = new[] { comparison.Left, comparison.Right }
                .Any(side => Bare(side) is IdentifierNameSyntax name && own.Contains(name.Identifier.ValueText));
            if (!readsItsOwn)
            {
                return true;
            }
            return loop.Statement
                .DescendantNodesAndSelf(inner => inner is not (AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax))
                .Any(inner => inner switch
                {
                    ReturnStatementSyntax or ThrowStatementSyntax or ThrowExpressionSyntax or GotoStatementSyntax => true,
                    BreakStatementSyntax leave => leave.Ancestors().First(around =>
                        around is SwitchStatementSyntax or WhileStatementSyntax or DoStatementSyntax
                            or CommonForEachStatementSyntax or ForStatementSyntax) == loop,
                    _ => false,
                });
        }

        /// <summary>The expression a lambda is handed on as: itself, under
        /// its parentheses and casts.</summary>
        public static ExpressionSyntax Outermost(ExpressionSyntax expression)
        {
            while (expression.Parent is ParenthesizedExpressionSyntax or CastExpressionSyntax or CheckedExpressionSyntax
                or PostfixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.SuppressNullableWarningExpression }
                or ConditionalExpressionSyntax
                or BinaryExpressionSyntax { RawKind: (int)SyntaxKind.CoalesceExpression })
            {
                expression = (ExpressionSyntax)expression.Parent;
            }
            return expression;
        }

        /// <summary>The expression under its parentheses, casts,
        /// <c>checked</c> and null-forgiving mark: what a rule matches the
        /// shape of, wherever it matches one.</summary>
        public static ExpressionSyntax Bare(ExpressionSyntax expression)
        {
            while (true)
            {
                switch (expression)
                {
                    case ParenthesizedExpressionSyntax parenthesized:
                        expression = parenthesized.Expression;
                        continue;
                    case CastExpressionSyntax cast:
                        expression = cast.Expression;
                        continue;
                    case CheckedExpressionSyntax @checked:
                        expression = @checked.Expression;
                        continue;
                    case PostfixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.SuppressNullableWarningExpression } forgiven:
                        expression = forgiven.Operand;
                        continue;
                    default:
                        return expression;
                }
            }
        }

        /// <summary>The argument a call binds to the named parameter,
        /// whatever its position; where the call does not bind, the first
        /// positional one.</summary>
        public static ExpressionSyntax? ArgumentFor(InvocationExpressionSyntax call, SemanticModel model, string parameter)
        {
            SeparatedSyntaxList<ArgumentSyntax> arguments = call.ArgumentList.Arguments;
            if (arguments.FirstOrDefault(argument => argument.NameColon?.Name.Identifier.ValueText == parameter) is { } named)
            {
                return named.Expression;
            }
            SymbolInfo info = model.GetSymbolInfo(call);
            int position = (info.Symbol ?? info.CandidateSymbols.FirstOrDefault()) is IMethodSymbol method
                ? Math.Max(0, method.Parameters.Select(declared => declared.Name).ToList().IndexOf(parameter))
                : 0;
            return position < arguments.Count && arguments[position].NameColon is null ? arguments[position].Expression : null;
        }

        /// <summary>Whether a node is a lambda handed to a call named
        /// SpinUntil, PumpUntil, RunUntil, WaitFor or WaitUntil: what a wait
        /// keeps asking, not what a fact asserts.</summary>
        public static bool IsAWaitCondition(SyntaxNode node) =>
            node is AnonymousFunctionExpressionSyntax lambda && IsHandedToAWait(Outermost(lambda));

        /// <summary>Whether an expression is an argument of a call named
        /// SpinUntil, PumpUntil, RunUntil, WaitFor or WaitUntil.</summary>
        public static bool IsHandedToAWait(ExpressionSyntax handed) =>
            TakenBy(handed) is InvocationExpressionSyntax wait
            && NameCalled(wait.Expression) is "SpinUntil" or "PumpUntil" or "RunUntil" or "WaitFor" or "WaitUntil";

        /// <summary>The call or creation an expression is a direct argument
        /// of, if it is one.</summary>
        public static ExpressionSyntax? TakenBy(ExpressionSyntax argument) =>
            argument.Parent is ArgumentSyntax { Parent: ArgumentListSyntax { Parent: ExpressionSyntax taking } } ? taking : null;
    }

    /// <summary>The pinned-list comparison on planted lists: each way a list
    /// can be wrong is named, and an exact list is left alone.</summary>
    [Fact]
    public void ThePinnedListsAreExactAndGiveAReason()
    {
        ClockSite[] found =
        [
            new("A.cs", "Waits", 10, "Thread.Sleep(100);"),
            new("A.cs", "Waits", 14, "Thread.Sleep(100);"),
            new("B.cs", "Holds", 7, "host.PumpFor(span);"),
        ];
        (string File, string Member, int Count, string Reason) holds = ("B.cs", "Holds", 1, "deliberate-hold: the gap is the scenario");

        Assert.Empty(PinnedListFaults(found, [("A.cs", "Waits", 2, "late-arrival"), holds], UnitDelayReasons));
        Assert.Empty(PinnedListFaults(
            found, [("A.cs", "Waits", 1, "late-arrival"), ("A.cs", "Waits", 1, "quiesce"), holds], UnitDelayReasons));

        string over = Assert.Single(PinnedListFaults(found, [("A.cs", "Waits", 1, "late-arrival"), holds], UnitDelayReasons));
        Assert.StartsWith("new: A.cs (Waits)", over);
        Assert.Contains("A.cs:14", over);

        string unlisted = Assert.Single(PinnedListFaults(found, [("A.cs", "Waits", 2, "late-arrival")], UnitDelayReasons));
        Assert.StartsWith("new: B.cs (Holds)", unlisted);

        string under = Assert.Single(PinnedListFaults(found, [("A.cs", "Waits", 3, "late-arrival"), holds], UnitDelayReasons));
        Assert.StartsWith("stale: A.cs (Waits)", under);

        string gone = Assert.Single(PinnedListFaults(
            found, [("A.cs", "Waits", 2, "late-arrival"), holds, ("C.cs", "Gone", 1, "quiesce")], UnitDelayReasons));
        Assert.StartsWith("stale: C.cs (Gone)", gone);

        string silent = Assert.Single(PinnedListFaults(found, [("A.cs", "Waits", 2, " "), holds], UnitDelayReasons));
        Assert.StartsWith("reason: A.cs (Waits)", silent);

        string unknown = Assert.Single(PinnedListFaults(found, [("A.cs", "Waits", 2, "flaky: it settles"), holds], UnitDelayReasons));
        Assert.StartsWith("reason: A.cs (Waits)", unknown);
        Assert.Contains("flaky", unknown);

        string unswept = Assert.Single(PinnedListFaults(found, [("A.cs", "Waits", 2, "unswept"), holds], UnitDelayReasons));
        Assert.StartsWith("reason: A.cs (Waits)", unswept);
        Assert.Empty(PinnedListFaults(found, [("A.cs", "Waits", 2, "unswept"), holds], GateDelayReasons));

        string otherRule = Assert.Single(PinnedListFaults(found, [("A.cs", "Waits", 2, NoWaitWitness + ": a seam"), holds], UnitDelayReasons));
        Assert.StartsWith("reason: A.cs (Waits)", otherRule);
        Assert.Empty(PinnedListFaults(found, [("A.cs", "Waits", 2, NoWaitWitness + ": a seam"), holds], UnitCeilingReasons));

        string seamless = Assert.Single(PinnedListFaults(found, [("A.cs", "Waits", 2, NoWaitWitness + ": "), holds], UnitCeilingReasons));
        Assert.StartsWith("reason: A.cs (Waits)", seamless);
        Assert.Contains("seam", seamless);

        string empty = Assert.Single(PinnedListFaults(found, [("A.cs", "Waits", 2, "late-arrival"), holds, ("A.cs", "Waits", 0, "quiesce")], UnitDelayReasons));
        Assert.StartsWith("count: A.cs (Waits)", empty);
    }

    /// <summary>Rules 1 and 2 on planted facts: each delay is read as the
    /// site, the poll, the wait condition or the hold it is, each ceiling is
    /// seen, and each right form is left alone.</summary>
    [Theory]
    [MemberData(nameof(PlantedClockFacts))]
    public void TheClockRulesSeeAPlantedFact(string form, string planted, string expected)
    {
        CSharpCompilation unit = Walk.Value.Single(project => project.Name == UnitProject).Compilation;
        SyntaxTree tree = CSharpSyntaxTree.ParseText(
            PlantedClockOpening + planted + "\n}\n",
            (CSharpParseOptions)unit.SyntaxTrees.First().Options,
            path: "Planted.cs");
        Assert.DoesNotContain(tree.GetDiagnostics(), diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        CSharpCompilation compilation = unit.AddSyntaxTrees(tree);
        Diagnostic[] unbound = [.. compilation.GetSemanticModel(tree).GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)];
        Assert.True(unbound.Length == 0, $"{form}: the planted source does not bind:\n" + string.Join("\n", unbound.Select(d => d.ToString())));

        IEnumerable<string> delays = DelayCalls(compilation, [tree], _ => "Planted.cs")
            .Select(call => $"{call.At.Member}: {Said(call.Standing)}");
        IEnumerable<string> ceilings = ElapsedCeilings(compilation, [tree], _ => "Planted.cs")
            .Select(ceiling => $"{ceiling.Member}: ceiling");
        string seen = string.Join("; ", delays.Concat(ceilings));
        if (seen.Length == 0)
        {
            seen = "clean";
        }
        Assert.True(expected == seen, $"{form}: expected {expected}, the rules answered {seen}");
    }

    /// <summary>What every planted fact stands in: a class with a flag, a
    /// seam, a pump and a helper of each kind the rules name.</summary>
    private const string PlantedClockOpening =
        "using System.Diagnostics;\nusing static System.DateTime;\nusing static System.Diagnostics.Stopwatch;\n"
        + "using static System.Environment;\nusing static System.Threading.Tasks.Task;\nusing static System.Threading.Thread;\n"
        + "namespace SlateWindows.Tests.Planted;\n"
        + "sealed class PlantedSeam { public Action? BeforeForTests; public Func<Task>? PublicationForTests; "
        + "public event Action? Fired; public PlantedSeam Context => this; public void Await(Task work, string what) { } "
        + "public Task AwaitAsync(Task work) => work; "
        + "public void PumpFor(TimeSpan span) { } public void Fire() => Fired?.Invoke(); "
        + "public void Decode(Func<int, bool> progress) { } public TimeSpan Elapsed() => TimeSpan.Zero; }\n"
        + "static class PlantedExtensions { public static Task Later(this Task work) => work; }\n"
        + "static class Nap { public static void Sleep(int milliseconds) { } public static Task Delay(int milliseconds) => Task.CompletedTask; }\n"
        + "sealed class P { private bool done; private readonly PlantedSeam host = new(); private readonly PlantedSeam h = new(); "
        + "private readonly PlantedSeam x = new(); private readonly ManualResetEventSlim release = new(false); "
        + "private static readonly TimeSpan span = TimeSpan.FromMilliseconds(750); "
        + "private static readonly TimeSpan limit = TimeSpan.FromSeconds(3); "
        + "private static void RunSta(Action body) => body(); private static void Drain() { } "
        + "private static void RunDispatcherFor(TimeSpan duration) { } "
        + "private static bool WaitUntil(Func<bool> condition) => condition(); "
        + "private static TimeSpan Took() => Stopwatch.StartNew().Elapsed; private Action? kept;\n";

    public static TheoryData<string, string, string> PlantedClockFacts() => new()
    {
        // Rule 1: sites.
        { "a sleep, then an assertion", "void F() { Thread.Sleep(100); Assert.True(done); }", "F: site" },
        { "an awaited delay, then an assertion", "async Task F() { await Task.Delay(50); Assert.True(done); }", "F: site" },
        { "a sleep inside RunSta's lambda", "void F() { RunSta(() => { Thread.Sleep(100); Assert.True(done); }); }", "F: site" },
        {
            "the counted for",
            "void F() { for (int turn = 0; turn < 20; turn++) { Drain(); Thread.Sleep(5); } Assert.True(done); }",
            "F: site"
        },
        {
            "a delay handed to a call",
            "void F() { h.Context.Await(Task.Delay(TimeSpan.FromSeconds(10)), \"quiet\"); Assert.True(done); }",
            "F: site"
        },
        {
            "a delay handed to a call that is itself awaited",
            "async Task F() { await h.AwaitAsync(Task.Delay(50)); Assert.True(done); }",
            "F: site"
        },
        {
            "a delay handed to a call inside a larger expression",
            "async Task F() { Task settled = h.AwaitAsync(Task.Delay(50)); await settled; Assert.True(done); }",
            "F: not a statement"
        },
        {
            "a delay under WaitAsync with a token",
            "async Task F(CancellationToken token) { await Task.Delay(500).WaitAsync(token); Assert.True(done); }",
            "F: site"
        },
        {
            "a delay under WaitAsync with a timeout, keeping no context",
            "async Task F() { await Task.Delay(500).WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false); Assert.True(done); }",
            "F: site"
        },
        {
            "a delay under WaitAsync with a timeout and a token",
            "async Task F(CancellationToken token) { await Task.Delay(500).WaitAsync(TimeSpan.FromSeconds(5), token); Assert.True(done); }",
            "F: site"
        },
        {
            "a delay under WaitAsync with a time provider, blocked on",
            "void F() { Task.Delay(500).WaitAsync(TimeSpan.FromSeconds(5), TimeProvider.System).Wait(); Assert.True(done); }",
            "F: site"
        },
        {
            "a delay with a continuation, awaited",
            "async Task F() { await Task.Delay(500).ContinueWith(_ => Drain()); Assert.True(done); }",
            "F: site"
        },
        {
            "a delay with a continuation nobody waits on",
            "void F() { _ = Task.Delay(500).ContinueWith(_ => Drain()); Assert.True(done); }",
            "F: not a delay"
        },
        {
            "a delay handed to Task.WhenAll and awaited",
            "async Task F() { await Task.WhenAll(Task.Delay(500)); Assert.True(done); }",
            "F: site"
        },
        {
            "a delay handed to Task.WaitAll",
            "void F() { Task.WaitAll(Task.Delay(500)); Assert.True(done); }",
            "F: site"
        },
        {
            "a delay raced against nothing",
            "async Task F() { await Task.WhenAny(Task.Delay(500)); Assert.True(done); }",
            "F: site"
        },
        { "a sleep by its bare name", "void F() { Sleep(100); Assert.True(done); }", "F: site" },
        { "a delay by its bare name", "async Task F() { await Delay(50); Assert.True(done); }", "F: site" },
        {
            "a delay under a continuation, unwrapped and awaited",
            "async Task F() { await Task.Delay(500).ContinueWith(async _ => { await Task.Yield(); }).Unwrap(); Assert.True(done); }",
            "F: site"
        },
        {
            "the same, keeping no context",
            "async Task F() { await Task.Delay(500).ContinueWith(async _ => { await Task.Yield(); }).Unwrap().ConfigureAwait(false); }",
            "F: site"
        },
        {
            "the same, blocked on with Wait",
            "void F() { Task.Delay(500).ContinueWith(async _ => { await Task.Yield(); }).Unwrap().Wait(); Assert.True(done); }",
            "F: site"
        },
        {
            "the same, blocked on through its awaiter",
            "void F() { Task.Delay(500).ContinueWith(async _ => { await Task.Yield(); }).Unwrap().GetAwaiter().GetResult(); }",
            "F: site"
        },
        {
            "a delay whose continuation's Result is read into a discard",
            "void F() { _ = Task.Delay(500).ContinueWith(_ => 1).Result; Assert.True(done); }",
            "F: site"
        },
        {
            "a delay blocked on with a bounded Wait, its answer discarded",
            "void F() { _ = Task.Delay(500).Wait(1000); Assert.True(done); }",
            "F: site"
        },
        {
            "a delay made a ValueTask and awaited",
            "async Task F() { await new ValueTask(Task.Delay(500)); Assert.True(done); }",
            "F: site"
        },
        {
            "a delay under an extension method, awaited",
            "async Task F() { await Task.Delay(500).Later(); Assert.True(done); }",
            "F: site"
        },
        {
            "a delay handed to an extension method by its full name",
            "async Task F() { await PlantedExtensions.Later(Task.Delay(500)); Assert.True(done); }",
            "F: site"
        },
        {
            "a counted for whose condition stands in parentheses",
            "void F() { for (int turn = 0; (turn < 20); turn++) { Thread.Sleep(5); } Assert.True(done); }",
            "F: site"
        },
        {
            "a counted for whose loop variable stands in parentheses",
            "void F() { for (int turn = 0; ((turn) < 20); turn++) { Thread.Sleep(5); } Assert.True(done); }",
            "F: site"
        },
        {
            "a sleep in Task.Run's lambda, handed by name",
            "void F() { _ = Task.Run(action: () => { Thread.Sleep(300); release.Set(); }); }",
            "F: hold"
        },
        {
            "a sleep in Task.Run's lambda under a cast and parentheses",
            "void F() { _ = Task.Run((Action)(() => { Thread.Sleep(300); release.Set(); })); }",
            "F: hold"
        },
        {
            "a sleep in an unsafe pool work item",
            "void F() { _ = ThreadPool.UnsafeQueueUserWorkItem(_ => { Thread.Sleep(300); release.Set(); }, null); }",
            "F: hold"
        },
        {
            "a sleep in a SpinUntil lambda handed by name",
            "void F() { _ = System.Threading.SpinWait.SpinUntil(millisecondsTimeout: 1000, condition: () => { Thread.Sleep(5); return done; }); }",
            "F: wait condition"
        },
        {
            "a delay raced by Task.WhenAny, both handed by name",
            "async Task F() { Task work = Task.CompletedTask; _ = await Task.WhenAny(task2: Task.Delay(5000), task1: work); }",
            "F: not a delay"
        },
        {
            "a delay inside a call handed to another call, awaited",
            "async Task F() { Task work = Task.CompletedTask; await h.AwaitAsync(Task.WhenAll(work, Task.Delay(500))); Assert.True(done); }",
            "F: site"
        },
        {
            "a delay inside a blocked WhenAll an assertion is handed",
            "void F() { Task work = Task.CompletedTask; Assert.True(Task.WhenAll(work, Task.Delay(500)).Wait(1000)); }",
            "F: site"
        },
        {
            "a delay inside a call handed to a creation, awaited",
            "async Task F() { Task work = Task.CompletedTask; await new ValueTask(Task.WhenAll(work, Task.Delay(500))); }",
            "F: site"
        },
        {
            "a delay in a collection expression handed to WhenAll",
            "async Task F() { Task work = Task.CompletedTask; await Task.WhenAll([work, Task.Delay(500)]); Assert.True(done); }",
            "F: site"
        },
        {
            "a delay in a new array handed to WhenAll and blocked on",
            "void F() { Task work = Task.CompletedTask; Task.WhenAll(new[] { work, Task.Delay(500) }).Wait(); Assert.True(done); }",
            "F: site"
        },
        {
            "a delay in a new list handed to WhenAll",
            "async Task F() { Task work = Task.CompletedTask; await Task.WhenAll(new List<Task> { work, Task.Delay(500) }); }",
            "F: site"
        },
        {
            "a delay three calls deep",
            "async Task F() { Task work = Task.CompletedTask; await h.AwaitAsync(Task.WhenAll(work, h.AwaitAsync(Task.Delay(500)))); }",
            "F: site"
        },
        {
            "two delays raced with each other and nothing else",
            "async Task F() { await Task.WhenAny(Task.Delay(100), Task.Delay(200)); Assert.True(done); }",
            "F: site; F: site"
        },
        {
            "a delay alone in a collection handed to WhenAny",
            "async Task F() { await Task.WhenAny([Task.Delay(500)]); Assert.True(done); }",
            "F: site"
        },
        {
            "a delay as one branch of a conditional, awaited",
            "async Task F() { await (done ? Task.Delay(500) : Task.CompletedTask); Assert.True(done); }",
            "F: site"
        },
        {
            "a delay raced with other work at an outer level",
            "async Task F() { Task work = Task.CompletedTask; _ = await Task.WhenAny(work, Task.WhenAll(work, Task.Delay(500))); }",
            "F: not a delay"
        },
        {
            "a delay raced with other work at an inner level",
            "async Task F() { Task work = Task.CompletedTask; await Task.WhenAll(work, Task.WhenAny(work, Task.Delay(500))); }",
            "F: not a delay"
        },
        {
            "a delay raced with other work in a collection",
            "async Task F() { Task work = Task.CompletedTask; _ = await Task.WhenAny([work, Task.Delay(500)]); }",
            "F: not a delay"
        },
        {
            "a taken delay as an if's condition, which is no statement",
            "void F() { Task work = Task.CompletedTask; if (Task.WhenAll(work, Task.Delay(500)).Wait(1000)) { Drain(); } }",
            "F: not a statement"
        },
        {
            "a taken delay returned",
            "Task F() { return h.AwaitAsync(Task.Delay(500)); }",
            "F: not a statement"
        },
        {
            "a sleep in a new thread's lambda under a delegate made for it",
            "void F() { new Thread(new ThreadStart(() => { Thread.Sleep(300); release.Set(); })).Start(); }",
            "F: hold"
        },
        {
            "a sleep in Task.Run's lambda under a delegate made for it",
            "void F() { _ = Task.Run(new Action(() => { Thread.Sleep(300); release.Set(); })); }",
            "F: hold"
        },
        {
            "a sleep in a WaitUntil lambda under a delegate made for it",
            "void F() { _ = WaitUntil(new Func<bool>(() => { Thread.Sleep(5); return done; })); }",
            "F: wait condition"
        },
        {
            "a sleep in a lambda a seam is given on one branch",
            "void F() { x.BeforeForTests = done ? () => { Thread.Sleep(600); } : null; }",
            "F: hold"
        },
        {
            "two delays raced with each other, one under a continuation",
            "async Task F() { await Task.WhenAny(Task.Delay(100).ContinueWith(_ => { }), Task.Delay(200)); Assert.True(done); }",
            "F: site; F: site"
        },
        {
            "two delays raced with each other, one inside a WhenAll",
            "async Task F() { await Task.WhenAny(Task.WhenAll(Task.Delay(100)), Task.Delay(200)); Assert.True(done); }",
            "F: site; F: site"
        },
        {
            "a delay raced with work whose continuation makes a delay",
            "async Task F() { Task work = Task.CompletedTask; _ = await Task.WhenAny(work.ContinueWith(_ => Task.Delay(1)), Task.Delay(500)); }",
            "F: not a delay; F: not a delay"
        },
        { "a fixed pump", "void F() { host.PumpFor(span); Assert.True(done); }", "F: site" },
        { "the other fixed pump, by its bare name", "void F() { RunDispatcherFor(span); Assert.True(done); }", "F: site" },
        { "a fixed pump on a seam that may be absent", "void F(PlantedSeam? maybe) { maybe?.PumpFor(span); Assert.True(done); }", "F: site" },
        { "a delay blocked on with Wait", "void F() { Task.Delay(50).Wait(); Assert.True(done); }", "F: site" },
        {
            "a delay blocked on through its awaiter",
            "void F() { Task.Delay(50).GetAwaiter().GetResult(); Assert.True(done); }",
            "F: site"
        },
        {
            "an awaited delay that keeps no context",
            "async Task F() { await Task.Delay(50).ConfigureAwait(false); Assert.True(done); }",
            "F: site"
        },
        {
            "a sleep in a local function, named by it",
            "void F() { void Settle() { Thread.Sleep(5); } Settle(); Assert.True(done); }",
            "Settle: site"
        },
        { "a sleep in a constructor", "P() { Thread.Sleep(5); }", "P: site" },
        {
            "a sleep that is a helper's arrow body, a statement of that helper",
            "void Settle() => Thread.Sleep(200); void F() { Settle(); Assert.True(done); }",
            "Settle: site"
        },
        {
            "a pump that is a local function's arrow body",
            "void F() { void Settle() => host.PumpFor(span); Settle(); Assert.True(done); }",
            "Settle: site"
        },
        { "a sleep in a property's getter", "bool Settled { get { Thread.Sleep(5); return done; } }", "Settled: site" },
        {
            "a counted for whose only break ends a switch inside it",
            "void F(int kind) { for (int turn = 0; turn < 20; turn++) { switch (kind) { case 1: Drain(); break; } Thread.Sleep(5); } }",
            "F: site"
        },
        {
            "a counted for whose only return is a lambda's",
            "void F() { for (int turn = 0; turn < 20; turn++) { Func<bool> read = () => { return done; }; _ = read(); Thread.Sleep(5); } }",
            "F: site"
        },
        {
            "a lambda's sleep inside a while is the lambda's, not the while's poll",
            "void F() { while (!done) { RunSta(() => { Thread.Sleep(5); }); } }",
            "F: site"
        },
        {
            "a sleep in a callback the product is handed, a hold the rule reads as a site",
            "void F() { x.Decode(_ => { release.Set(); Thread.Sleep(300); return true; }); Assert.True(done); }",
            "F: site"
        },
        {
            "a sleep in a lambda kept in a local",
            "void F() { Action settle = () => { Thread.Sleep(5); }; settle(); Assert.True(done); }",
            "F: site"
        },
        {
            "a sleep in a lambda assigned to a local",
            "void F() { Action later; later = () => { Thread.Sleep(100); Assert.True(done); }; later(); }",
            "F: site"
        },
        {
            "a sleep in a lambda assigned to a parameter",
            "void F(Action later) { later = () => { Thread.Sleep(100); Assert.True(done); }; later(); }",
            "F: site"
        },

        // Rule 1: polls.
        { "a sleep in while (!done)", "void F() { while (!done) { Thread.Sleep(5); } }", "F: poll" },
        { "a sleep in a do loop", "void F() { do { Thread.Sleep(5); } while (!done); }", "F: poll" },
        { "a sleep in a foreach", "void F(int[] turns) { foreach (int turn in turns) { Thread.Sleep(5); } }", "F: poll" },
        {
            "a sleep in a for with && !done",
            "void F() { for (int turn = 0; turn < 20 && !done; turn++) { Thread.Sleep(5); } }",
            "F: poll"
        },
        {
            "a sleep in a for with a return",
            "bool F() { for (int turn = 0; turn < 20; turn++) { if (done) { return true; } Thread.Sleep(5); } return false; }",
            "F: poll"
        },
        {
            "a sleep in a for with a break",
            "void F() { for (int turn = 0; turn < 20; turn++) { if (done) { break; } Thread.Sleep(5); } }",
            "F: poll"
        },
        {
            "a sleep in a for with a throw",
            "void F() { for (int turn = 0; turn < 20; turn++) { if (done) { throw new InvalidOperationException(); } Thread.Sleep(5); } }",
            "F: poll"
        },
        {
            "a sleep in a for with no condition",
            "void F() { for (; ; ) { if (done) { break; } Thread.Sleep(5); } }",
            "F: poll"
        },
        {
            "a sleep in a for whose condition reads another variable",
            "void F(int turns) { int seen = 0; for (int turn = 0; seen < turns; turn++) { seen += done ? 1 : 0; Thread.Sleep(5); } }",
            "F: poll"
        },

        // Rule 1: wait conditions and holds.
        {
            "a sleep in a SpinUntil lambda",
            "void F() { _ = System.Threading.SpinWait.SpinUntil(() => { Thread.Sleep(5); return done; }, 1000); }",
            "F: wait condition"
        },
        {
            "a sleep in a WaitUntil lambda",
            "void F() { _ = WaitUntil(() => { Thread.Sleep(5); return done; }); }",
            "F: wait condition"
        },
        { "a sleep in a lambda assigned to a seam", "void F() { x.BeforeForTests = () => { Thread.Sleep(600); }; }", "F: hold" },
        { "a sleep in a lambda added to an event", "void F() { x.Fired += () => { Thread.Sleep(600); }; }", "F: hold" },
        { "a sleep in a lambda assigned to the fixture's own field", "void F() { kept = () => { Thread.Sleep(600); }; kept(); }", "F: hold" },
        {
            "a sleep in an anonymous method assigned to a seam",
            "void F() { x.BeforeForTests = delegate { Thread.Sleep(600); }; }",
            "F: hold"
        },
        {
            "a sleep in a lambda a seam is initialised with",
            "void F() { var seam = new PlantedSeam { BeforeForTests = () => { Thread.Sleep(600); } }; seam.Fire(); }",
            "F: hold"
        },
        {
            "an awaited delay in an async lambda assigned to a seam",
            "void F() { x.PublicationForTests = async () => { if (done) { await Task.Delay(150); } await Task.Yield(); }; }",
            "F: hold"
        },
        {
            "a sleep in Task.Run's lambda",
            "void F() { _ = Task.Run(() => { Thread.Sleep(300); release.Set(); }); }",
            "F: hold"
        },
        {
            "an awaited delay in Task.Run's async lambda",
            "Task F() => Task.Run(async () => { await Task.Delay(300); release.Set(); });",
            "F: hold"
        },
        {
            "a sleep in Task.Factory.StartNew's lambda",
            "void F() { _ = Task.Factory.StartNew(() => { Thread.Sleep(300); release.Set(); }); }",
            "F: hold"
        },
        {
            "a sleep in a new thread's lambda",
            "void F() { new Thread(() => { Thread.Sleep(300); release.Set(); }).Start(); }",
            "F: hold"
        },
        {
            "a sleep in a pool work item",
            "void F() { _ = ThreadPool.QueueUserWorkItem(_ => { Thread.Sleep(300); release.Set(); }); }",
            "F: hold"
        },

        // Rule 1: not a statement, not a delay, not a call.
        { "a sleep that is a lambda's whole body", "void F() { RunSta(() => Thread.Sleep(600)); }", "F: not a statement" },
        {
            "a delay raced by Task.WhenAny",
            "async Task F() { Task work = Task.CompletedTask; _ = await Task.WhenAny(work, Task.Delay(5000)); }",
            "F: not a delay"
        },
        {
            "a delay returned, never ending",
            "Task F(CancellationToken token) { return Task.Delay(Timeout.InfiniteTimeSpan, token); }",
            "F: not a delay"
        },
        {
            "a delay kept in a local and awaited later",
            "async Task F() { Task settle = Task.Delay(50); await settle; Assert.True(done); }",
            "F: not a delay"
        },
        {
            "the calls in a comment and in a string",
            "void F() { // Thread.Sleep(100); host.PumpFor(span);\n"
            + "string said = \"Thread.Sleep(100); await Task.Delay(50); host.PumpFor(span);\"; Assert.True(done || said.Length > 0); }",
            "clean"
        },
        {
            "another type's Sleep and Delay",
            "async Task F() { Nap.Sleep(100); await Nap.Delay(50); Assert.True(done); }",
            "clean"
        },

        // Rule 2: ceilings.
        {
            "a stopwatch reading under a ceiling",
            "void F() { var clock = Stopwatch.StartNew(); Drain(); Assert.True(clock.Elapsed < TimeSpan.FromSeconds(3)); }",
            "F: ceiling"
        },
        {
            "a local divided from a reading",
            "void F() { var clock = Stopwatch.StartNew(); Drain(); double ms = clock.Elapsed.TotalMilliseconds / 10; Assert.True(ms < 100); }",
            "F: ceiling"
        },
        {
            "a difference of two wall-clock readings",
            "void F() { DateTime began = DateTime.UtcNow; Drain(); Assert.True(DateTime.UtcNow - began < limit); }",
            "F: ceiling"
        },
        {
            "a reading in Assert.InRange",
            "void F() { var clock = Stopwatch.StartNew(); Drain(); Assert.InRange(clock.ElapsedMilliseconds, 0, 500); }",
            "F: ceiling"
        },
        {
            "the ceiling written from its other side",
            "void F() { var clock = Stopwatch.StartNew(); Drain(); Assert.True(limit >= clock.Elapsed, \"slow\"); }",
            "F: ceiling"
        },
        {
            "a ceiling beside another condition",
            "void F() { var clock = Stopwatch.StartNew(); Drain(); Assert.True(done && clock.ElapsedTicks <= 500); }",
            "F: ceiling"
        },
        {
            "a local timed only at the fixed point",
            "void F() { var clock = Stopwatch.StartNew(); TimeSpan took = TimeSpan.Zero; TimeSpan kept = TimeSpan.Zero; "
            + "for (int turn = 0; turn < 2; turn++) { kept = took; took = clock.Elapsed; } Assert.True(kept < limit); }",
            "F: ceiling"
        },
        {
            "Stopwatch.GetElapsedTime under a ceiling",
            "void F() { long began = Stopwatch.GetTimestamp(); Drain(); Assert.True(Stopwatch.GetElapsedTime(began) < limit); }",
            "F: ceiling"
        },
        {
            "Environment.TickCount64 under a ceiling",
            "void F() { long began = Environment.TickCount64; Drain(); Assert.True(Environment.TickCount64 - began < 500); }",
            "F: ceiling"
        },
        {
            "DateTimeOffset.UtcNow under a ceiling",
            "void F() { DateTimeOffset began = DateTimeOffset.UtcNow; Drain(); Assert.True(DateTimeOffset.UtcNow - began < limit); }",
            "F: ceiling"
        },
        {
            "a ceiling in a counted for",
            "void F() { var clock = Stopwatch.StartNew(); for (int turn = 0; turn < 3; turn++) { Drain(); Assert.True(clock.Elapsed < limit); } }",
            "F: ceiling"
        },
        {
            "a timed local read in a local function, named by it",
            "void F() { var clock = Stopwatch.StartNew(); Drain(); TimeSpan took = clock.Elapsed; void Check() { Assert.True(took < limit); } Check(); }",
            "Check: ceiling"
        },
        {
            "a ceiling written as a relational pattern",
            "void F() { var clock = Stopwatch.StartNew(); Drain(); Assert.True(clock.ElapsedMilliseconds is < 500); }",
            "F: ceiling"
        },
        {
            "a floor and a ceiling joined in one pattern",
            "void F() { var clock = Stopwatch.StartNew(); Drain(); Assert.True(clock.Elapsed.TotalMilliseconds is >= 0 and < 100); }",
            "F: ceiling"
        },
        {
            "a refused floor written as a pattern",
            "void F() { var clock = Stopwatch.StartNew(); Drain(); Assert.False(clock.ElapsedMilliseconds is > 500); }",
            "F: ceiling"
        },
        {
            "a ceiling on each case of a foreach",
            "void F(int[] cases) { foreach (int c in cases) { var clock = Stopwatch.StartNew(); Drain(); Assert.True(clock.Elapsed < limit); } }",
            "F: ceiling"
        },
        {
            "DateTimeOffset.Now under a ceiling",
            "void F() { DateTimeOffset began = DateTimeOffset.Now; Drain(); Assert.True(DateTimeOffset.Now - began < limit); }",
            "F: ceiling"
        },
        {
            "arithmetic on raw timestamps under a ceiling",
            "void F() { long began = Stopwatch.GetTimestamp(); Drain(); long ticks = Stopwatch.GetTimestamp() - began; Assert.True(ticks < 5_000_000); }",
            "F: ceiling"
        },
        {
            "a ceiling kept in a bool and asserted by name",
            "void F() { var clock = Stopwatch.StartNew(); Drain(); bool fast = clock.Elapsed < limit; Assert.True(fast, \"slow\"); }",
            "F: ceiling"
        },
        {
            "a floor kept in a bool and refused by name",
            "void F() { var clock = Stopwatch.StartNew(); Drain(); bool slow = clock.Elapsed > limit; Assert.False(slow); }",
            "F: ceiling"
        },
        {
            "Stopwatch's clock by its bare names",
            "void F() { long began = GetTimestamp(); Drain(); Assert.True(GetElapsedTime(began) < limit); }",
            "F: ceiling"
        },
        {
            "raw timestamps by their bare name",
            "void F() { long began = GetTimestamp(); Drain(); long ticks = GetTimestamp() - began; Assert.True(ticks < 5_000_000); }",
            "F: ceiling"
        },
        {
            "the wall clock by its bare name",
            "void F() { DateTime began = UtcNow; Drain(); Assert.True(Now - began < limit); }",
            "F: ceiling"
        },
        {
            "the tick count by its bare name",
            "void F() { long began = TickCount64; Drain(); Assert.True(TickCount - began < 500); }",
            "F: ceiling"
        },
        {
            "the wall clock by its full name",
            "void F() { System.DateTime began = System.DateTime.UtcNow; Drain(); Assert.True(global::System.DateTime.UtcNow - began < limit); }",
            "F: ceiling"
        },
        {
            "a time provider's timestamps",
            "void F() { long began = TimeProvider.System.GetTimestamp(); Drain(); Assert.True(TimeProvider.System.GetElapsedTime(began) < limit); }",
            "F: ceiling"
        },
        {
            "a time provider's wall clock",
            "void F() { DateTimeOffset began = TimeProvider.System.GetUtcNow(); Drain(); "
            + "Assert.True(TimeProvider.System.GetUtcNow() - began < limit); }",
            "F: ceiling"
        },
        {
            "a ceiling kept in a bool beside another condition",
            "void F() { var clock = Stopwatch.StartNew(); Drain(); bool fast = clock.Elapsed < limit; Assert.True(done && fast); }",
            "F: ceiling"
        },
        {
            "a ceiling kept in a bool and handed on to another",
            "void F() { var clock = Stopwatch.StartNew(); Drain(); bool fast = clock.Elapsed < limit; bool ok = fast; Assert.True(ok); }",
            "F: ceiling"
        },
        {
            "a ceiling kept in a bool and compared with true",
            "void F() { var clock = Stopwatch.StartNew(); Drain(); bool fast = clock.Elapsed < limit; Assert.True(fast == true); }",
            "F: ceiling"
        },
        {
            "a floor kept in a bool and negated beside another condition",
            "void F() { var clock = Stopwatch.StartNew(); Drain(); bool slow = clock.Elapsed > limit; Assert.True(done && !slow); }",
            "F: ceiling"
        },
        {
            "a floor kept in a bool and compared with false",
            "void F() { var clock = Stopwatch.StartNew(); Drain(); bool slow = clock.Elapsed > limit; Assert.True(slow == false); }",
            "F: ceiling"
        },
        {
            "a floor kept in a bool and not true",
            "void F() { var clock = Stopwatch.StartNew(); Drain(); bool slow = clock.Elapsed > limit; Assert.True(slow != true); }",
            "F: ceiling"
        },
        {
            "a floor kept in a bool and matched against false",
            "void F() { var clock = Stopwatch.StartNew(); Drain(); bool slow = clock.Elapsed > limit; Assert.True(slow is false); }",
            "F: ceiling"
        },
        {
            "a floor kept in a bool and refused beside another condition",
            "void F() { var clock = Stopwatch.StartNew(); Drain(); bool slow = clock.Elapsed > limit; Assert.False(done || slow); }",
            "F: ceiling"
        },
        {
            "a ceiling handed to Assert.True by name, after its message",
            "void F() { var clock = Stopwatch.StartNew(); Drain(); Assert.True(userMessage: \"slow\", condition: clock.Elapsed < limit); }",
            "F: ceiling"
        },
        {
            "a refused floor handed to Assert.False by name, after its message",
            "void F() { var clock = Stopwatch.StartNew(); Drain(); Assert.False(userMessage: \"slow\", condition: clock.Elapsed > limit); }",
            "F: ceiling"
        },
        {
            "a reading handed to Assert.InRange by name, last",
            "void F() { var clock = Stopwatch.StartNew(); Drain(); Assert.InRange(low: 0, high: 500, actual: clock.ElapsedMilliseconds); }",
            "F: ceiling"
        },
        {
            "a ceiling kept in a nullable bool",
            "void F() { var clock = Stopwatch.StartNew(); Drain(); bool? fast = clock.Elapsed < limit; Assert.True(fast); }",
            "F: ceiling"
        },
        {
            "a floor kept in a nullable bool and refused",
            "void F() { var clock = Stopwatch.StartNew(); Drain(); bool? slow = clock.Elapsed > limit; Assert.False(slow); }",
            "F: ceiling"
        },
        {
            "a nullable bool's Value",
            "void F() { var clock = Stopwatch.StartNew(); Drain(); bool? fast = clock.Elapsed < limit; Assert.True(fast.Value); }",
            "F: ceiling"
        },
        {
            "a nullable bool compared with true",
            "void F() { var clock = Stopwatch.StartNew(); Drain(); bool? fast = clock.Elapsed < limit; Assert.True(fast == true); }",
            "F: ceiling"
        },
        {
            "a nullable bool compared with false",
            "void F() { var clock = Stopwatch.StartNew(); Drain(); bool? slow = clock.Elapsed > limit; Assert.True(slow == false); }",
            "F: ceiling"
        },
        {
            "a nullable bool matched against true",
            "void F() { var clock = Stopwatch.StartNew(); Drain(); bool? fast = clock.Elapsed < limit; Assert.True(fast is true); }",
            "F: ceiling"
        },
        {
            "a nullable bool matched against not true",
            "void F() { var clock = Stopwatch.StartNew(); Drain(); bool? slow = clock.Elapsed > limit; Assert.True(slow is not true); }",
            "F: ceiling"
        },
        {
            "a nullable bool with a fallback",
            "void F() { var clock = Stopwatch.StartNew(); Drain(); bool? fast = clock.Elapsed < limit; Assert.True(fast ?? false); }",
            "F: ceiling"
        },
        {
            "a nullable bool's GetValueOrDefault",
            "void F() { var clock = Stopwatch.StartNew(); Drain(); bool? fast = clock.Elapsed < limit; Assert.True(fast.GetValueOrDefault()); }",
            "F: ceiling"
        },
        {
            "a ceiling in a counted for whose condition stands in parentheses",
            "void F() { var clock = Stopwatch.StartNew(); for (int turn = 0; (turn < 3); turn++) { Drain(); Assert.True(clock.Elapsed < limit); } }",
            "F: ceiling"
        },
        {
            "a ceiling under parentheses throughout",
            "void F() { var clock = Stopwatch.StartNew(); Drain(); Assert.True((((clock.Elapsed)) < (limit))); }",
            "F: ceiling"
        },
        {
            "a ceiling asserted inside Assert.Multiple",
            "void F() { var clock = Stopwatch.StartNew(); Drain(); Assert.Multiple(() => Assert.True(clock.Elapsed < limit)); }",
            "F: ceiling"
        },
        {
            "a ceiling asserted in a lambda handed to a helper",
            "void F() { var clock = Stopwatch.StartNew(); RunSta(() => { Drain(); Assert.True(clock.Elapsed < limit); }); }",
            "F: ceiling"
        },
        {
            "a ceiling written with CompareTo",
            "void F() { var clock = Stopwatch.StartNew(); Drain(); Assert.True(clock.Elapsed.CompareTo(limit) < 0); }",
            "F: ceiling"
        },
        {
            "a ceiling on a reading named by a property pattern",
            "void F() { var clock = Stopwatch.StartNew(); Drain(); Assert.True(clock is { ElapsedMilliseconds: < 500 }); }",
            "F: ceiling"
        },
        {
            "a ceiling on a member of a reading, by a property pattern",
            "void F() { var clock = Stopwatch.StartNew(); Drain(); Assert.True(clock.Elapsed is { TotalMilliseconds: < 500 }); }",
            "F: ceiling"
        },
        {
            "a ceiling on a reading named by an extended property pattern",
            "void F() { var clock = Stopwatch.StartNew(); Drain(); Assert.True(clock is { Elapsed.TotalMilliseconds: < 500 }); }",
            "F: ceiling"
        },
        {
            "a ceiling on a reading named by a nested property pattern",
            "void F() { var clock = Stopwatch.StartNew(); Drain(); Assert.True(clock is { Elapsed: { TotalMilliseconds: < 500 } }); }",
            "F: ceiling"
        },
        {
            "a negated floor in a property pattern",
            "void F() { var clock = Stopwatch.StartNew(); Drain(); Assert.True(clock is not { ElapsedMilliseconds: >= 500 }); }",
            "F: ceiling"
        },
        {
            "a refused floor in a property pattern",
            "void F() { var clock = Stopwatch.StartNew(); Drain(); Assert.False(clock is { ElapsedMilliseconds: > 500 }); }",
            "F: ceiling"
        },
        {
            "a ceiling on a timed local's member, by a property pattern",
            "void F() { var clock = Stopwatch.StartNew(); Drain(); TimeSpan took = clock.Elapsed; Assert.True(took is { TotalMilliseconds: < 500 }); }",
            "F: ceiling"
        },
        {
            "a ceiling beside an untimed property in one pattern",
            "void F() { var clock = Stopwatch.StartNew(); Drain(); Assert.True(clock is { IsRunning: true, ElapsedMilliseconds: < 500 }); }",
            "F: ceiling"
        },
        {
            "a ceiling as one branch of an or pattern",
            "void F() { var clock = Stopwatch.StartNew(); Drain(); Assert.True(clock is { ElapsedMilliseconds: < 500 or > 100_000 }); }",
            "F: ceiling"
        },
        {
            "a reading bound by a var pattern, then held under a ceiling",
            "void F() { var clock = Stopwatch.StartNew(); Drain(); Assert.True(clock.Elapsed is var took && took < limit); }",
            "F: ceiling"
        },
        {
            "a reading bound by a property pattern's variable, then held under a ceiling",
            "void F() { var clock = Stopwatch.StartNew(); Drain(); Assert.True(clock is { Elapsed: var took } && took < limit); }",
            "F: ceiling"
        },
        {
            "a ceiling asked through a conditional",
            "void F() { var clock = Stopwatch.StartNew(); Drain(); Assert.True(clock.Elapsed < limit ? true : false); }",
            "F: ceiling"
        },
        {
            "a ceiling on a reading reached through ?.",
            "void F(Stopwatch? maybe) { Drain(); Assert.True(maybe?.Elapsed < limit); }",
            "F: ceiling"
        },
        {
            "a timed local under a cast as Assert.InRange's actual, by name",
            "void F() { var clock = Stopwatch.StartNew(); Drain(); TimeSpan took = clock.Elapsed; "
            + "Assert.InRange(actual: (long)took.TotalMilliseconds, low: 0, high: 500); }",
            "F: ceiling"
        },
        {
            "a reading from a seam's own Elapsed()",
            "void F() { TimeSpan sinceFirstFrame = x.Elapsed(); Assert.True(sinceFirstFrame < limit); }",
            "F: ceiling"
        },
        {
            "a ceiling under another trait",
            "[Trait(\"kind\", \"slow\")] void F() { var clock = Stopwatch.StartNew(); Drain(); Assert.True(clock.Elapsed < limit); }",
            "F: ceiling"
        },
        {
            "a refused floor, which is a ceiling",
            "void F() { var clock = Stopwatch.StartNew(); Drain(); Assert.False(clock.Elapsed > limit, \"slow\"); }",
            "F: ceiling"
        },
        {
            "a refused floor from its other side",
            "void F() { var clock = Stopwatch.StartNew(); Drain(); Assert.False(limit <= clock.Elapsed); }",
            "F: ceiling"
        },
        {
            "a negated floor under Assert.True",
            "void F() { var clock = Stopwatch.StartNew(); Drain(); Assert.True(!(clock.Elapsed >= limit)); }",
            "F: ceiling"
        },
        {
            "a negated ceiling under Assert.False",
            "void F() { var clock = Stopwatch.StartNew(); Drain(); Assert.False(!(clock.Elapsed < limit)); }",
            "F: ceiling"
        },

        // Rule 2: not ceilings.
        {
            "a median from WallClockBudget",
            "void F() { TimeSpan median = WallClockBudget.WarmMedian(() => { var clock = Stopwatch.StartNew(); Drain(); return clock.Elapsed; }); "
            + "Assert.True(median < TimeSpan.FromMilliseconds(500)); }",
            "clean"
        },
        {
            "an awaited median from WallClockBudget",
            "async Task F() { TimeSpan median = await WallClockBudget.WarmMedianAsync(async () => { var clock = Stopwatch.StartNew(); "
            + "await Task.Yield(); return clock.Elapsed; }); Assert.True(median < TimeSpan.FromMilliseconds(500)); }",
            "clean"
        },
        {
            "a ceiling in a benchmark fact",
            "[Trait(\"kind\", \"benchmark\")] void F() { var clock = Stopwatch.StartNew(); Drain(); Assert.True(clock.Elapsed < limit); }",
            "clean"
        },
        {
            "a ceiling in a benchmark class",
            "[Trait(\"kind\", \"benchmark\")] sealed class Bench { void F() { var clock = Stopwatch.StartNew(); Drain(); "
            + "Assert.True(clock.Elapsed < TimeSpan.FromSeconds(3)); } }",
            "clean"
        },
        {
            "a floor",
            "void F() { var clock = Stopwatch.StartNew(); Drain(); Assert.True(clock.Elapsed >= TimeSpan.FromMilliseconds(100)); }",
            "clean"
        },
        {
            "a refused ceiling, which is a floor",
            "void F() { var clock = Stopwatch.StartNew(); Drain(); Assert.False(clock.Elapsed < TimeSpan.FromMilliseconds(100)); }",
            "clean"
        },
        {
            "a negated ceiling under Assert.True, which is a floor",
            "void F() { var clock = Stopwatch.StartNew(); Drain(); Assert.True(!(clock.Elapsed < TimeSpan.FromMilliseconds(100))); }",
            "clean"
        },
        {
            "a negated floor under Assert.False, which is a floor",
            "void F() { var clock = Stopwatch.StartNew(); Drain(); Assert.False(!(clock.Elapsed >= TimeSpan.FromMilliseconds(100))); }",
            "clean"
        },
        {
            "a floor written as a relational pattern",
            "void F() { var clock = Stopwatch.StartNew(); Drain(); Assert.True(clock.ElapsedMilliseconds is >= 100); }",
            "clean"
        },
        {
            "a negated ceiling pattern, which is a floor",
            "void F() { var clock = Stopwatch.StartNew(); Drain(); Assert.True(clock.ElapsedMilliseconds is not < 500); }",
            "clean"
        },
        {
            "a floor kept in a bool and asserted by name",
            "void F() { var clock = Stopwatch.StartNew(); Drain(); bool waited = clock.Elapsed >= limit; Assert.True(waited); }",
            "clean"
        },
        {
            "a wait's own condition on a reading, asserted to have held",
            "void F() { long pressed = Stopwatch.GetTimestamp(); long heard = 0; Drain(); "
            + "Assert.True(System.Threading.SpinWait.SpinUntil(() => Volatile.Read(ref heard) > pressed, 2000)); }",
            "clean"
        },
        {
            "a wait's own condition kept in a bool",
            "void F() { var clock = Stopwatch.StartNew(); bool quiet = WaitUntil(() => clock.Elapsed < limit); Assert.True(quiet); }",
            "clean"
        },
        {
            "a floor kept in a bool beside another condition",
            "void F() { var clock = Stopwatch.StartNew(); Drain(); bool slowEnough = clock.Elapsed >= limit; Assert.True(done && slowEnough); }",
            "clean"
        },
        {
            "a ceiling kept in a bool and negated, which is a floor",
            "void F() { var clock = Stopwatch.StartNew(); Drain(); bool fast = clock.Elapsed < limit; Assert.True(done && !fast); }",
            "clean"
        },
        {
            "a bool given two values, which is not followed",
            "void F() { var clock = Stopwatch.StartNew(); Drain(); bool fast = clock.Elapsed < limit; fast = fast && done; Assert.True(fast); }",
            "clean"
        },
        {
            "a reading only in a message handed by name, first",
            "void F() { var clock = Stopwatch.StartNew(); Drain(); Assert.True(userMessage: $\"under {clock.Elapsed < limit}\", condition: done); }",
            "clean"
        },
        {
            "a reading as Assert.InRange's low bound, which is no ceiling on what is asserted",
            "void F() { var clock = Stopwatch.StartNew(); Drain(); Assert.InRange(high: long.MaxValue, low: clock.ElapsedMilliseconds, actual: 5L); }",
            "clean"
        },
        {
            "a floor kept in a nullable bool",
            "void F() { var clock = Stopwatch.StartNew(); Drain(); bool? waited = clock.Elapsed >= limit; Assert.True(waited ?? false); }",
            "clean"
        },
        {
            "a relational property pattern on a value that is not timed",
            "void F() { Assert.True(limit is { TotalMilliseconds: < 5000 }); }",
            "clean"
        },
        {
            "a property pattern that asks nothing of time",
            "void F() { var clock = Stopwatch.StartNew(); Drain(); Assert.True(clock is { IsRunning: true }); }",
            "clean"
        },
        {
            "a floor in a property pattern",
            "void F() { var clock = Stopwatch.StartNew(); Drain(); Assert.True(clock is { ElapsedMilliseconds: >= 100 }); }",
            "clean"
        },
        {
            "a negated ceiling in a property pattern, which is a floor",
            "void F() { var clock = Stopwatch.StartNew(); Drain(); Assert.True(clock is not { ElapsedMilliseconds: < 500 }); }",
            "clean"
        },
        {
            "a nullable bool's HasValue, which says nothing of time",
            "void F() { var clock = Stopwatch.StartNew(); Drain(); bool? fast = clock.Elapsed < limit; Assert.True(fast.HasValue); }",
            "clean"
        },
        {
            "two timed values compared",
            "void F() { var clock = Stopwatch.StartNew(); TimeSpan first = clock.Elapsed; Drain(); TimeSpan second = clock.Elapsed; "
            + "Assert.True(first < second); }",
            "clean"
        },
        {
            "a ratio of two timed values",
            "void F() { var clock = Stopwatch.StartNew(); double with = clock.Elapsed.TotalMilliseconds; Drain(); "
            + "double without = clock.Elapsed.TotalMilliseconds; Assert.True(with < without / 2); }",
            "clean"
        },
        {
            "the ceiling inside a while (true) poll",
            "void F() { var clock = Stopwatch.StartNew(); while (true) { if (done) { break; } "
            + "Assert.True(clock.Elapsed < limit, \"timed out\"); Thread.Sleep(5); } }",
            "F: poll"
        },
        {
            "a while condition",
            "void F() { var clock = Stopwatch.StartNew(); while (clock.Elapsed < limit) { Drain(); } Assert.True(done); }",
            "clean"
        },
        {
            "a reading only in the assertion's message",
            "void F() { var clock = Stopwatch.StartNew(); Drain(); Assert.True(done, $\"after {clock.Elapsed}, under {clock.Elapsed < limit}\"); }",
            "clean"
        },
        {
            "a reading kept in an array element, which carries nothing",
            "void F() { var clock = Stopwatch.StartNew(); var readings = new TimeSpan[1]; Drain(); readings[0] = clock.Elapsed; "
            + "Assert.True(readings[0] < limit); }",
            "clean"
        },
        {
            "a reading a helper returns, which carries nothing",
            "void F() { TimeSpan took = Took(); Assert.True(took < limit); }",
            "clean"
        },
    };

    // ---- Rule 1: no new fixed delay stands before an assertion ------------
    //
    // A delay is Thread.Sleep(...); a Task.Delay(...) that is awaited, blocked
    // on (.Wait(), .GetAwaiter().GetResult()) or handed as a direct argument
    // to any call but Task.WhenAny; or a call to a method named PumpFor or
    // RunDispatcherFor. A fixed pump is a fixed sleep on a dispatcher thread.
    // A Task.Delay is followed up to its statement through whatever it
    // stands in, in any order and to any depth: a chain it is the receiver
    // of (WaitAsync, ContinueWith, Unwrap, ConfigureAwait, an extension
    // method, a Result), a call or a creation it is handed to, a collection
    // or an array it is an element of, a branch of a conditional. So
    // Assert.True(Task.WhenAll(work, Task.Delay(500)).Wait(bound)); is a
    // site. A Task.WhenAny that races it against other work, at any level
    // on the way up, makes it a bound and no delay; a Task.WhenAny of
    // delays only is a delay. _ = <a delay something took>; is a statement
    // of its own. A lambda under a delegate made for it, or on one branch
    // of a conditional, is handed on as it is.
    //
    // A delay is a site when it is the whole of an expression statement and
    // is none of these:
    //   a poll: a while, a do, a foreach, or a for that can end early stands
    //     between it and the function it runs in. A for cannot end early
    //     when its condition is one comparison of its own loop variable and
    //     nothing in its body leaves it, so a counted loop of drains and
    //     sleeps is a site: a fixed sleep written long.
    //   a wait condition: the function it runs in is a lambda handed to a
    //     call named SpinUntil, PumpUntil, RunUntil, WaitFor or WaitUntil.
    //   a hold: the function it runs in is a lambda or an anonymous method
    //     that is the right side of an assignment or a += to a field, a
    //     property or an event (a seam the product calls; a local or a
    //     parameter is the fact's own to call, so it is no seam), or an
    //     argument to Task.Run, Task.Factory.StartNew, new Thread(...) or
    //     ThreadPool.QueueUserWorkItem.
    // A delay that is a lambda's whole body is no statement, so no site. The
    // arrow body of a method, a local function or an accessor is that
    // member's one statement, and is read like any other. A lambda handed
    // to anything else, a fact's RunSta(() => { ... }) included, is searched
    // like a method body.
    //
    // What binds and what is read as spelled. Thread.Sleep, Task.Delay,
    // Task.WhenAny, Task.Run, Task.Factory.StartNew,
    // ThreadPool.QueueUserWorkItem and new Thread(...) are read by their bound
    // symbols, so another type's Sleep is no delay; where a call does not
    // bind, its receiver's spelling stands in. PumpFor, RunDispatcherFor and
    // the five wait-condition names belong to test helpers of several types,
    // so they are read as spelled. Statements, loops and lambdas are syntax.
    //
    // What rule 1 does not see:
    //   a while on a clock with no other exit, which is a sleep written as a
    //     poll;
    //   a foreach over a fixed list, which it reads as a poll;
    //   a pump helper with another name;
    //   a Task.Delay kept in a local or a field and awaited later;
    //   a wait that cannot succeed (SpinWait.SpinUntil(() => false, 500)) and
    //     a join on the running thread (Thread.CurrentThread.Join(500));
    //   a delay a local declaration takes (var _ = Task.Delay(500).Wait(1000);)
    //     and Thread.Sleep taken as a method group;
    //   a delay something took inside a larger expression that is no
    //     statement: the condition of an if or a while, a returned value,
    //     an initialiser, a switch expression's arm;
    //   a delay that is a for's incrementor;
    //   a delay made in a lambda handed to a query
    //     (tasks.Select(_ => Task.Delay(5))), which is the lambda's value;
    //   a hold in a timer's callback or in work a dispatcher is posted,
    //     which reads as a site like any callback;
    //   a member read from a delay's chain that is no Result
    //     (Task.Delay(50).IsCompleted handed to a call), which reads as the
    //     delay handed on;
    //   a wait on a gate nobody sets (gate.Wait(500)), which sleeps under
    //     another name;
    //   how often a delay runs: one in a helper is one site, however many
    //     facts call the helper;
    //   a hold inside a callback the product is handed as an argument, or
    //     inside a lambda kept in a local or a parameter. Both read as
    //     sites: nothing in the source tells either from a fact's own body
    //     in a lambda. Such an entry says deliberate-hold or
    //     timeout-under-test;
    //   a delay in a named method handed to a seam as a method group, which
    //     reads as a site of that method. Its entry says what it holds;
    //   a hold that is none: a lambda assigned to the fixture's own field or
    //     property and called by the fact reads as a hold, and so does a
    //     body the fact awaits at once
    //     (await Task.Run(() => { Thread.Sleep(500); }));
    //   FlaUI's Wait.UntilInputIsProcessed in the gate, which plan 47 CB-7
    //     weighs.

    /// <summary>How rule 1 reads one call to a delay's name.</summary>
    internal enum Standing
    {
        Site,
        Poll,
        WaitCondition,
        Hold,
        NotAStatement,
        NotADelay,
    }

    internal enum DelayKind
    {
        Sleep,
        TaskDelay,
        FixedPump,
    }

    /// <summary>One call to a delay's name (<c>Thread.Sleep</c>,
    /// <c>Task.Delay</c>, <c>PumpFor</c> or <c>RunDispatcherFor</c>), and how
    /// rule 1 reads it.</summary>
    internal sealed record DelayCall(ClockSite At, string Name, DelayKind Kind, Standing Standing);

    private static string Said(Standing standing) => standing switch
    {
        Standing.Site => "site",
        Standing.Poll => "poll",
        Standing.WaitCondition => "wait condition",
        Standing.Hold => "hold",
        Standing.NotAStatement => "not a statement",
        _ => "not a delay",
    };

    /// <summary>A floor for each name a project calls, sites or not. Counted
    /// on 2026-11-16, after the unit project's sweep (plan 47 CB-6): 64
    /// Thread.Sleep, 42 Task.Delay, 32 PumpFor and 5 RunDispatcherFor in the
    /// unit project, and 51 Thread.Sleep in the gate's, which calls none of
    /// the other three. Fewer means the rule lost sight of that name, not
    /// that the project waits less.</summary>
    private static readonly (string Project, string Name, int Calls)[] DelayCallsAtLeast =
    [
        (UnitProject, "Thread.Sleep", 60),
        (UnitProject, "Task.Delay", 39),
        (UnitProject, "PumpFor", 30),
        (UnitProject, "RunDispatcherFor", 4),
        (GateProject, "Thread.Sleep", 46),
    ];

    /// <summary>The unit project's fixed delays as they stand, each with the
    /// word that says why it is not a wait on a condition and, after the
    /// colon, what it holds. The census first read 80 sites here, on
    /// 2026-11-16, and the sweep that followed (plan 47 CB-6) left 67. Read
    /// with arrow-bodied helpers counted, the list held 68 on 2026-11-16:
    /// 26 Thread.Sleep, 8 Task.Delay and 34 fixed pumps. An entry that names
    /// a missing seam stays until the product has one.</summary>
    private static readonly (string File, string Member, int Count, string Reason)[] UnitFixedDelays =
    [
        ("AccessibilityNotificationDispatcherTests.cs", "AfterExpiryALineWaitsForTheProviderInsteadOfBeingLost", 1,
            "late-arrival: 750 ms of ticks after the drain raise nothing more"),
        ("AccessibilityNotificationDispatcherTests.cs", "AProviderThatConnectsFirstHoldsTheQueueUntilTheAdvise", 1,
            "late-arrival: 750 ms of ticks after the drain raise nothing more"),
        ("AccessibilityNotificationDispatcherTests.cs", "AProviderThatIsNeverAdvisedDrainsThreeSecondsAfterItConnects", 1,
            "late-arrival: 750 ms of ticks after the drain raise nothing more"),
        ("AccessibilityNotificationDispatcherTests.cs", "LinesPostedDuringTheHoldAreQueuedAndNoneIsRaised", 1,
            "late-arrival: 750 ms of ticks after the drain raise nothing more"),
        ("AccessibilityNotificationDispatcherTests.cs", "AHoldThatRunsOutWhileAClientListensDeliversAndSaysSo", 1,
            "late-arrival: 750 ms of ticks after the drain raise nothing more"),
        ("AccessibilityNotificationDispatcherTests.cs", "ARestartedReaderWaitsForItsOwnAdviseNotTheOldHold", 1,
            "late-arrival: 750 ms of ticks after the drain raise nothing more"),
        ("AccessibilityNotificationDispatcherTests.cs", "AfterDoneALineWaitsWhileReadinessRegressesAndOneTickDeliversIt", 1,
            "late-arrival: 750 ms of ticks after the drain raise nothing more"),
        ("AccessibilityNotificationDispatcherTests.cs", "AQueuedLineIsDroppedThirtySecondsAfterItsPostNeverRaisedLate", 1,
            "late-arrival: the stopped poll ticks no more"),
        ("AccessibilityNotificationDispatcherTests.cs", "EachQueuedLineExpiresAtItsOwnDeadlineNeverTheWholeQueue", 1,
            "late-arrival: 750 ms of ticks after the drain raise nothing more"),
        ("AccessibilityNotificationDispatcherTests.cs", "TheLaunchWindowStartsAtTheFirstFrameNotAtConstruction", 1,
            "deliberate-hold: 250 ms of start-up pass before the first frame, and the launch clock still reads zero"),
        ("BasesExportTests.cs", "AnExportInsideTheVaultIsAnEchoAndSpeaksOnce", 1,
            "late-arrival: core's quiet window and its spacing pass with no call from the watch"),
        ("BasesRedTeamRegressionTests.cs", "DashboardEditorRefusesAStaleSaveWithTheCanonicalSentence", 1,
            "distinct-timestamp: the dashboard's second write needs a later modified_at_ms"),
        ("CanvasDocumentTests.cs", "QuiesceAsync", 1, "quiesce: 20 drains, since a drained body can queue follow-up work"),
        ("Censuses/CallbackConcurrencyCensus.cs", "RegisterUnregisterOnce", 1,
            "deliberate-hold: a millisecond for events to land between the register and the unregister"),
        ("Censuses/CanonicalHighlightCensus.cs", "LoadedCoordinatorQuiescesAndRefreshesForEditScrollAndResize", 2,
            "late-arrival: 160 ms after a refresh settles no further refresh comes"),
        ("CitationAsyncInterleavingTests.cs", "QuiesceAsync", 1, "quiesce: 40 drains of the seed and both leaves"),
        ("CitationAsyncInterleavingTests.cs", "AFailedSeedStopsBothLeavesReadingCoreNotJustTheBibliography", 1,
            "quiesce: 40 drains of the leaf, then no row has been resolved"),
        ("CitationAsyncInterleavingTests.cs", "AReloadPublishesTheClearBeforeTheNewReadLands", 1,
            "quiesce: 40 drains of the leaf, so the first load's publications are all in before they are counted"),
        ("HistoryAsyncInterleavingTests.cs", "QuiesceAsync", 1, "quiesce: 10 drains, since a drained body can queue follow-up work"),
        ("HotExitTests.cs", "AChangedSnapshotIsWrittenAfterTheDebounce", 1,
            "late-arrival: 200 ms into the fact's own 5 s debounce the second edit is not written"),
        ("HotExitTests.cs", "ALateDiscardNeverReKeepsTheTextItDiscarded", 1, "late-arrival: after the discard resolves nothing keeps the text again"),
        ("HotExitTests.cs", "AFenceHoldsUntilTheCoveredTabsAreClassified", 2,
            "late-arrival: with the fence held, and after its release, the discarded text is not stored"),
        ("HotExitTests.cs", "AStaleEntryEqualToTheFileRestoresUnsavedAndStaysKept", 1, "late-arrival: the restored entry is not retired"),
        ("HotExitTests.cs", "AnOlderClaimsOutcomeNeverForgetsTheNewerClaim", 1,
            "deliberate-hold: a gap between the episode's end and the next edit while the first claim is held"),
        ("HotExitTests.cs", "AnOlderClaimsOutcomeNeverForgetsTheNewerClaim", 1,
            "timeout-under-test: past the writer's debounce, so the second claim is issued"),
        ("HotExitTests.cs", "AnOlderClaimsOutcomeNeverForgetsTheNewerClaim", 1,
            "late-arrival: the older claim's outcome arrives and the newer claim stays"),
        ("HotExitTests.cs", "ASessionEndFlushClaimsAsTheTabsOwnClaim", 1, "late-arrival: a refused claim stores nothing"),
        ("HotExitTests.cs", "AClosedOpenStopsWaitingForTheLastSession", 1, "late-arrival: a second on, the open still waits for the held retirement"),
        ("HotExitTests.cs", "PeersSharingAnOlderTextMakeOneRecoveredNoteUnderAFreeName", 2,
            "distinct-timestamp: the three kept entries need different times"),
        ("HotExitTests.cs", "ALateClaimNeverDisplacesANewerEpisode", 1,
            "deliberate-hold: a gap between the clean tab and the next edit while the first claim is held"),
        ("HotExitTests.cs", "ALateClaimNeverDisplacesANewerEpisode", 1, "late-arrival: no second claim starts while the first is held"),
        ("HotExitTests.cs", "AClaimForAReaderEndedEpisodeIsRetired", 1, "deliberate-hold: the claim stays held while the reader ends the episode"),
        ("HotExitTests.cs", "AnErroredClaimIsRetriedWithItsOwnIncarnation", 1,
            "deliberate-hold: a gap for the errored claim's outcome to reach the writer before the next edit; "
            + "the writer has no seam that says an outcome was applied"),
        ("HotExitTests.cs", "AnAdoptingClaimTakenByAnotherClaimIsNotKept", 1,
            "deliberate-hold: a gap for the taken claim's outcome to reach the writer before the close; "
            + "the writer has no seam that says an outcome was applied"),
        ("HotExitTests.cs", "AnOldClaimsLateOutcomeNeverCountsForANewOne", 1,
            "deliberate-hold: a gap between the episode's end and the next edit while the old write is held"),
        ("HotExitTests.cs", "AnOldClaimsLateOutcomeNeverCountsForANewOne", 1,
            "late-arrival: the old write's outcome arrives and the new text stays stored"),
        ("HotExitTests.cs", "ARefusedDiscardKeepsTheWriterFencedUntilItResolves", 1, "late-arrival: the fenced writer stores no later edit"),
        ("HotExitTests.cs", "ADiscardedUnkeptTabEditedLaterIsKept", 1, "late-arrival: after the discard resolves no entry is stored"),
        ("HotExitTests.cs", "ASessionEndThatIgnoresCancelAbandons", 1, "late-arrival: after the session ends no retirement runs"),
        ("HotExitTests.cs", "RestoreThenDiscardThenReopenGetsNothing", 1,
            "deliberate-hold: a gap for the adoption's outcome to reach the writer before the next tab opens; "
            + "the writer has no seam that says an outcome was applied"),
        ("HotExitTests.cs", "DivergentPeersAreReconciledIntoARecoveredNote", 1, "distinct-timestamp: the two kept entries need different times"),
        ("HotExitTests.cs", "ADamagedRetirementResolvesWithItsReason", 1, "late-arrival: the damaged retirement is not retried"),
        ("ExitWatchTests.cs", "SlowCleanUp", 1,
            "timeout-under-test: an exit handler that outlasts ExitWatch.SlowHandler, so the watch names it"),
        ("PanelWorkSchedulerTests.cs", "Run", 1, "deliberate-hold: the compute body is held for the delay its caller asks"),
        ("RescanTests.cs", "TheHistoryPanelReloadsAfterARescan", 1, "quiesce: 20 turns of both queues, then no history load has run"),
        ("RescanTests.cs", "Settle", 2, "quiesce: counted turns of both queues, in one fact's own Settle and in the harness's"),
        ("RescanTests.cs", "DependentsHarness", 1, "quiesce: 20 turns of both queues before the harness is handed over"),
        ("RescanTests.Round5.cs", "TheGraphAndConnectionsProbesAreTwoOperationsAtTheSeam", 1,
            "quiesce: 20 turns of both queues after the graph tab opens"),
        ("SaveChainAdmissionTests.cs", "Settle", 1, "quiesce: 20 pumped turns, for a fact that asserts nothing happened"),
        ("StaThreadTests.cs", "Late", 1,
            "deliberate-hold: the runner starts its wait a second late, through the after-start seam"),
        ("Support/ExitWatch.cs", "WatchTheExit", 1, "watchdog: the exit watch notes a process still alive after its wait"),
        ("SyncDiagnosticsAsyncTests.cs", "QuiesceAsync", 1, "quiesce: 10 drains of the panel"),
        ("SyncDiagnosticsLifecycleTests.cs", "ABlockingUiHopNeverStallsTheWatcherTeardown", 1,
            "timeout-under-test: past the injected 150 ms marker debounce, so the fire is in flight"),
        ("SyncMarkerWatcherTests.cs", "ARootMarkerCreationFiresOnceAfterTheQuietPeriod", 1,
            "late-arrival: three quiet periods pass with no second fire; SyncMarkerWatcher has no idle witness"),
        ("SyncMarkerWatcherTests.cs", "ABurstOfEntryChurnCoalescesIntoOneFire", 1,
            "late-arrival: past the quiet period with no second fire; SyncMarkerWatcher has no idle witness"),
        ("SyncMarkerWatcherTests.cs", "AnObsidianTreeCreatedAfterStartIsArmedByItsParent", 1,
            "late-arrival: the last burst's fires end before the baseline is read; SyncMarkerWatcher has no idle witness"),
        ("SyncMarkerWatcherTests.cs", "DeletingAndRecreatingObsidianKeepsItsWatchLive", 1,
            "late-arrival: the last burst's fires end before the baseline is read; SyncMarkerWatcher has no idle witness"),
        ("SyncMarkerWatcherTests.cs", "RenamingObsidianAwayAndRecreatingItKeepsTheChainLive", 1,
            "late-arrival: the last burst's fires end before the baseline is read; SyncMarkerWatcher has no idle witness"),
        ("SyncMarkerWatcherTests.cs", "ADroppedEventBurstRebuildsTheWatchAndFires", 1,
            "late-arrival: the last burst's fires end before the baseline is read; SyncMarkerWatcher has no idle witness"),
        ("SyncMarkerWatcherTests.cs", "StopIsIdempotentAndSuppressesEveryLaterFire", 1, "late-arrival: a stopped watch fires nothing"),
        ("SyncMarkerWatcherTests.cs", "EventsLandingDuringStopNeverFireAfterItReturns", 1,
            "late-arrival: a stopped watch fires nothing while the churn goes on"),
        ("SyncMarkerWatcherTests.cs", "ChurnBelowTheWatchedScopeNeverFires", 1, "late-arrival: churn below the watched scope fires nothing"),
        ("W1HardeningTests.cs", "StalledConnectionTimesOutAndTheNextActivationIsAccepted", 1,
            "timeout-under-test: past the stalled connection's read timeout"),
        ("W2EditorInteractionRedTeamTests.cs", "SvgPreview_TheBudgetEndsOnlyAfterACallbackInProgressReturns", 1,
            "deliberate-hold: the progress callback is still running when the budget ends"),
    ];

    /// <summary>The gate's fixed delays as the census first read them: 31
    /// sites on 2026-11-16, none weighed yet. Plan 47 CB-7 fixes or names
    /// each.</summary>
    private static readonly (string File, string Member, int Count, string Reason)[] GateFixedDelays =
    [
        ("FocusSettleWatchTests.cs", "SlowReadsThatAllSeeTheRowPass", 1, "unswept"),
        ("ShellAccessibilityTests.Announcements.cs", "Announcements_ReachADesktopScopedListenerFromLaunch", 1, "unswept"),
        ("ShellAccessibilityTests.Announcements.cs", "AwaitHeard", 1, "unswept"),
        ("ShellAccessibilityTests.AppMenu.cs", "AppMenu_APointerRestingWhereItOpensLeavesTheKeysOnTheFirstRow", 1, "unswept"),
        ("ShellAccessibilityTests.HotExit.cs", "HotExit_ClosingAndReopeningKeepsAnUnsavedNote", 1, "unswept"),
        ("ShellAccessibilityTests.HotExit.cs", "HotExit_AKilledProcessKeepsAnUnsavedNote", 1, "unswept"),
        ("ShellAccessibilityTests.Palette.cs", "AssertHeardOneCountAndOnlyTheFinalSelection", 1, "unswept"),
        ("ShellAccessibilityTests.ReadingSelection.cs", "ReadingView_ASelectionStandsOutAndKeepsItsText", 2, "unswept"),
        ("ShellAccessibilityTests.RegionStops.cs", "ClickFilterFromOutsideTheGroup", 2, "unswept"),
        ("ShellAccessibilityTests.RegionStops.cs", "AssertFocusStays", 1, "unswept"),
        ("ShellAccessibilityTests.Rescan.cs", "ExternalFiles_TheReadersRowRemovedUnderThemLandsTheKeysBeside", 2, "unswept"),
        ("ShellAccessibilityTests.Rescan.cs", "AssertAWatcherRunKeepsTheReadersRow", 2, "unswept"),
        ("ShellAccessibilityTests.ShellRegions.cs", "AssertKeepsFocus", 1, "unswept"),
        ("ShellAccessibilityTests.ThemeReadability.cs", "ThemedShell_TheMenuBarAndTheOpenTabAreReadable", 3, "unswept"),
        ("ShellAccessibilityTests.ThemeReadability.cs", "ThemedShell_APalettesTextKeepsItsClearType", 1, "unswept"),
        ("ShellAccessibilityTests.ThemeReadability.cs", "ThemedShell_TheEditorSelectionStandsOutAndKeepsItsText", 2, "unswept"),
        ("ShellAccessibilityTests.ThemeReadability.cs", "ThemedShell_TheCommandPaletteListIsReadable", 1, "unswept"),
        ("ShellAccessibilityTests.ThemeReadability.cs", "ThemedShell_AFocusedPanelRowShowsItsWholeRing", 1, "unswept"),
        ("ShellAccessibilityTests.ThemeReadability.cs", "ThemedShell_ADisabledGridStaysOnItsSurface", 3, "unswept"),
        ("ShellAccessibilityTests.ThemeReadability.cs", "ThemedShell_AnEmptyFieldShowsItsPurpose", 1, "unswept"),
        ("ShellAccessibilityTests.ThemeReadability.cs", "ThemedShell_TheSelectedDropdownRowIsNotBlue", 2, "unswept"),
    ];

    [Fact]
    public void NoNewFixedDelayStandsBeforeAnAssertion()
    {
        foreach (TestProject read in Walk.Value)
        {
            string project = read.Name;
            IReadOnlyList<DelayCall> calls = DelayCalls(read.Compilation, read.Sources, FileIn(project));
            foreach ((string _, string name, int floor) in DelayCallsAtLeast.Where(entry => entry.Project == project))
            {
                int seen = calls.Count(call => call.Name == name);
                Assert.True(
                    seen >= floor,
                    $"rule 1 saw {seen} calls to {name} in {project}; it saw at least {floor} when the census was written, so "
                    + "it has lost sight of the name.");
            }

            IReadOnlyList<string> faults = PinnedListFaults(
                [.. calls.Where(call => call.Standing == Standing.Site).Select(call => call.At)],
                project == UnitProject ? UnitFixedDelays : GateFixedDelays,
                project == UnitProject ? UnitDelayReasons : GateDelayReasons);
            Assert.True(
                faults.Count == 0,
                $"{project}: a fixed delay stands before an assertion, and the pinned list does not hold it as it is.\n"
                + "new: wait on the condition, or add an entry and say in the PR why none could be waited on.\n"
                + "stale: lower or remove it.\n"
                + string.Join("\n", faults));
        }
    }

    /// <summary>Rule 1's reading of every call to a delay's name in
    /// <paramref name="sources"/>.</summary>
    internal static IReadOnlyList<DelayCall> DelayCalls(
        CSharpCompilation compilation, IReadOnlyList<SyntaxTree> sources, Func<SyntaxTree, string> fileOf)
    {
        List<DelayCall> calls = [];
        foreach (SyntaxTree tree in sources)
        {
            SemanticModel? model = null;
            foreach (InvocationExpressionSyntax call in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                string? name = ClockSyntax.NameCalled(call.Expression);
                if (name is not ("Sleep" or "Delay" or "PumpFor" or "RunDispatcherFor"))
                {
                    continue;
                }
                model ??= compilation.GetSemanticModel(tree);
                DelayKind? kind = name switch
                {
                    "Sleep" => ClockSyntax.IsMemberOf(call, model, "Thread", "System.Threading") ? DelayKind.Sleep : null,
                    "Delay" => ClockSyntax.IsMemberOf(call, model, "Task", "System.Threading.Tasks") ? DelayKind.TaskDelay : null,
                    _ => DelayKind.FixedPump,
                };
                if (kind is not { } known)
                {
                    continue;
                }
                var at = new ClockSite(fileOf(tree), ClockSyntax.MemberOf(call), LineOf(call), TextOf(call));
                string called = known switch
                {
                    DelayKind.Sleep => "Thread.Sleep",
                    DelayKind.TaskDelay => "Task.Delay",
                    _ => name,
                };
                calls.Add(new DelayCall(at, called, known, StandingOf(call, known, model)));
            }
        }
        return calls;
    }

    private static string TextOf(SyntaxNode node)
    {
        SyntaxNode shown = node.AncestorsAndSelf().OfType<StatementSyntax>().FirstOrDefault() is ExpressionStatementSyntax statement
            ? statement
            : node;
        string text = string.Join(' ', shown.ToString().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return text.Length > 100 ? text[..100] + "..." : text;
    }

    /// <summary>Whether an assignment's left side is a seam the product can
    /// call: a field, a property or an event of some object, not a local or
    /// a parameter the fact calls itself. Bound; where the left side does
    /// not bind, a member access or an object initialiser's member stands
    /// in.</summary>
    private static bool IsASeam(AssignmentExpressionSyntax assignment, SemanticModel model)
    {
        SymbolInfo info = model.GetSymbolInfo(assignment.Left);
        return (info.Symbol ?? info.CandidateSymbols.FirstOrDefault()) is { } symbol
            ? symbol is IFieldSymbol or IPropertySymbol or IEventSymbol
            : assignment.Left is MemberAccessExpressionSyntax or MemberBindingExpressionSyntax
                || assignment.Parent is InitializerExpressionSyntax;
    }

    private static Standing StandingOf(InvocationExpressionSyntax call, DelayKind kind, SemanticModel model)
    {
        // What waits: the call itself for a sleep or a pump. A Task.Delay
        // waits only where something takes its time, so it is followed up
        // to its statement through whatever it stands in, in any order and
        // to any depth: a chain it is the receiver of (WaitAsync,
        // ContinueWith, Unwrap, an extension, a member read), a call or a
        // creation it is handed to, a collection or an array it is an
        // element of, a branch of a conditional. An await, a Wait, a
        // GetResult or a Result takes it, and so does being handed on. One
        // raced by Task.WhenAny against other work, at any level on the way
        // up, bounds that work. One returned, kept in a local or a field, or
        // made in a lambda is not followed past it.
        ExpressionSyntax waits = call;
        ExpressionSyntax cameFrom = call;
        bool taken = kind != DelayKind.TaskDelay;
        while (true)
        {
            switch (waits.Parent)
            {
                case ParenthesizedExpressionSyntax or CastExpressionSyntax or CheckedExpressionSyntax
                    or PostfixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.SuppressNullableWarningExpression }
                    or BinaryExpressionSyntax { RawKind: (int)SyntaxKind.CoalesceExpression }:
                    waits = (ExpressionSyntax)waits.Parent;
                    continue;
                case ConditionalExpressionSyntax choice when choice.Condition != waits:
                    waits = choice;
                    continue;
                case AwaitExpressionSyntax awaited:
                    waits = awaited;
                    taken = true;
                    continue;
                case ConditionalAccessExpressionSyntax conditional when conditional.WhenNotNull == waits:
                    waits = conditional;
                    continue;
                case MemberAccessExpressionSyntax access when access.Expression == waits:
                    taken |= access.Name.Identifier.ValueText is "Wait" or "GetResult" or "Result";
                    waits = access.Parent is InvocationExpressionSyntax made && made.Expression == access ? made : access;
                    continue;
                // A collection or an array the chain is an element of goes
                // where the element would.
                case ExpressionElementSyntax { Parent: CollectionExpressionSyntax collection }:
                    cameFrom = waits;
                    waits = collection;
                    continue;
                case InitializerExpressionSyntax { Parent: ExpressionSyntax holder } elements
                    when elements.Kind() is SyntaxKind.ArrayInitializerExpression or SyntaxKind.CollectionInitializerExpression:
                    cameFrom = waits;
                    waits = holder;
                    continue;
                case ArgumentSyntax when ClockSyntax.TakenBy(waits) is { } taking:
                    // A race with other work bounds that work. A race with
                    // nothing, or with other delays only, is a delay.
                    if (taking is InvocationExpressionSyntax race
                        && ClockSyntax.NameCalled(race.Expression) == "WhenAny"
                        && ClockSyntax.IsMemberOf(race, model, "Task", "System.Threading.Tasks")
                        && RacesOtherWork(race, waits, cameFrom, model))
                    {
                        return Standing.NotADelay;
                    }
                    cameFrom = waits;
                    waits = taking;
                    taken = true;
                    continue;
                // _ = <a delay something took>; is that delay's own statement.
                case AssignmentExpressionSyntax { Left: IdentifierNameSyntax { Identifier.ValueText: "_" } } discarded
                    when discarded.Right == waits:
                    waits = discarded;
                    continue;
            }
            break;
        }
        if (!taken)
        {
            return Standing.NotADelay;
        }
        // A statement of its own, or the arrow body of a method, a local
        // function or an accessor, which is that member's one statement. A
        // lambda's whole body is neither, and neither is a condition, a
        // returned value or an initialiser the delay stands in.
        SyntaxNode? statement = waits.Parent switch
        {
            ExpressionStatementSyntax whole => whole,
            ArrowExpressionClauseSyntax
            {
                Parent: BaseMethodDeclarationSyntax or LocalFunctionStatementSyntax or AccessorDeclarationSyntax,
            } arrow => arrow,
            _ => null,
        };
        if (statement is null)
        {
            return Standing.NotAStatement;
        }
        if (ClockSyntax.StandsInAPoll(statement))
        {
            return Standing.Poll;
        }
        if (ClockSyntax.FunctionAround(statement) is AnonymousFunctionExpressionSyntax lambda)
        {
            // A delegate made around the lambda (new Action(...), new
            // ThreadStart(...)) hands it on as it is.
            ExpressionSyntax handed = ClockSyntax.Outermost(lambda);
            while (ClockSyntax.TakenBy(handed) is BaseObjectCreationExpressionSyntax wrapper
                && model.GetTypeInfo(wrapper).Type?.TypeKind == TypeKind.Delegate)
            {
                handed = ClockSyntax.Outermost(wrapper);
            }
            if (ClockSyntax.IsHandedToAWait(handed))
            {
                return Standing.WaitCondition;
            }
            if (handed.Parent is AssignmentExpressionSyntax assignment && assignment.Right == handed && IsASeam(assignment, model))
            {
                return Standing.Hold;
            }
            bool startsWork = ClockSyntax.TakenBy(handed) switch
            {
                InvocationExpressionSyntax start => ClockSyntax.NameCalled(start.Expression) switch
                {
                    "Run" => ClockSyntax.IsMemberOf(start, model, "Task", "System.Threading.Tasks"),
                    "StartNew" => ClockSyntax.IsMemberOf(start, model, "TaskFactory", "System.Threading.Tasks"),
                    "QueueUserWorkItem" or "UnsafeQueueUserWorkItem" => ClockSyntax.IsMemberOf(start, model, "ThreadPool", "System.Threading"),
                    _ => false,
                },
                BaseObjectCreationExpressionSyntax creation => ClockSyntax.IsMemberOf(creation, model, "Thread", "System.Threading"),
                _ => false,
            };
            if (startsWork)
            {
                return Standing.Hold;
            }
        }
        return Standing.Site;
    }

    /// <summary>Whether a <c>Task.WhenAny</c> races the chain that reached it
    /// against work that is no fixed delay: its other arguments, or the other
    /// elements of the one collection or array it is handed. A spread element
    /// is other work.</summary>
    private static bool RacesOtherWork(
        InvocationExpressionSyntax race, ExpressionSyntax arrived, ExpressionSyntax cameFrom, SemanticModel model)
    {
        IEnumerable<ExpressionSyntax?> tasks = race.ArgumentList.Arguments.Select(argument => (ExpressionSyntax?)argument.Expression);
        if (race.ArgumentList.Arguments is [{ Expression: var only }])
        {
            tasks = ClockSyntax.Bare(only) switch
            {
                CollectionExpressionSyntax collection => collection.Elements.Select(element => (element as ExpressionElementSyntax)?.Expression),
                ArrayCreationExpressionSyntax { Initializer: { } elements } => elements.Expressions,
                ImplicitArrayCreationExpressionSyntax { Initializer: { } elements } => elements.Expressions,
                BaseObjectCreationExpressionSyntax { Initializer: { } elements } => elements.Expressions,
                _ => tasks,
            };
        }
        return tasks.Any(task => task is null || (task != arrived && task != cameFrom && !HoldsOnlyDelays(task, model)));
    }

    /// <summary>Whether a task handed to a race is fixed delays and nothing
    /// else, however wrapped: a <c>Task.Delay</c>, a chain one is the
    /// receiver of, or a call, a creation, a collection or an array handed
    /// such tasks only. Work whose continuation makes a delay is other
    /// work.</summary>
    private static bool HoldsOnlyDelays(ExpressionSyntax task, SemanticModel model)
    {
        switch (ClockSyntax.Bare(task))
        {
            case InvocationExpressionSyntax made:
                if (ClockSyntax.NameCalled(made.Expression) == "Delay" && ClockSyntax.IsMemberOf(made, model, "Task", "System.Threading.Tasks"))
                {
                    return true;
                }
                if (made.Expression is MemberAccessExpressionSyntax { Expression: var receiver } && HoldsOnlyDelays(receiver, model))
                {
                    return true;
                }
                return made.ArgumentList.Arguments.Count > 0
                    && made.ArgumentList.Arguments.All(argument => HoldsOnlyDelays(argument.Expression, model));
            case CollectionExpressionSyntax collection:
                return collection.Elements.Count > 0
                    && collection.Elements.All(element => element is ExpressionElementSyntax one && HoldsOnlyDelays(one.Expression, model));
            case ArrayCreationExpressionSyntax { Initializer: { } elements }:
                return elements.Expressions.Count > 0 && elements.Expressions.All(element => HoldsOnlyDelays(element, model));
            case ImplicitArrayCreationExpressionSyntax { Initializer: { } elements }:
                return elements.Expressions.Count > 0 && elements.Expressions.All(element => HoldsOnlyDelays(element, model));
            case BaseObjectCreationExpressionSyntax { Initializer: { } elements }:
                return elements.Expressions.Count > 0 && elements.Expressions.All(element => HoldsOnlyDelays(element, model));
            case BaseObjectCreationExpressionSyntax { ArgumentList.Arguments: { Count: > 0 } handed }:
                return handed.All(argument => HoldsOnlyDelays(argument.Expression, model));
            default:
                return false;
        }
    }

    // ---- Rule 2: an elapsed ceiling goes through WallClockBudget or is a benchmark ----
    //
    // A reading is a member named Elapsed, ElapsedMilliseconds or
    // ElapsedTicks, a call to Stopwatch.GetElapsedTime or
    // Stopwatch.GetTimestamp, or the wall clock: DateTime.UtcNow,
    // DateTime.Now, DateTimeOffset.UtcNow, DateTimeOffset.Now,
    // Environment.TickCount, Environment.TickCount64. A local is timed when
    // its initialiser or any assignment to it holds a reading or a timed
    // local, to a fixed point, over the whole member with its lambdas and
    // local functions. What a WallClockBudget call is handed carries
    // nothing, so a local initialised from one is not timed. Array elements
    // and helper returns carry nothing.
    //
    // A ceiling is a < b, a <= b, b > a or b >= a, where a holds a reading
    // or a timed local and b holds neither, inside the first argument of
    // Assert.True; or an Assert.InRange whose first argument is timed. Under
    // Assert.False the comparison is turned round: a > b and a >= b are the
    // ceiling, and a < b is a floor. A ! over the whole first argument turns
    // it round once more, under either assertion.
    // A pattern is walked whole. What a relational pattern is matched
    // against is timed when the input is, or when the property path that
    // leads to it names a reading: clock is { ElapsedMilliseconds: < x },
    // clock is { Elapsed.TotalMilliseconds: < x } and
    // took is { TotalMilliseconds: < x } are ceilings, to any depth of
    // nesting. An untimed property in the same pattern counts for nothing,
    // an or that holds a ceiling is one, and a variable a pattern binds is
    // timed when what it was matched against is.
    // A reading matched against a relational pattern is read the same way:
    // elapsed is < x is a ceiling, elapsed is >= x a floor, not turns the
    // pattern round, and is >= 0 and < x is a ceiling. A bool local given
    // one value stands for that value wherever the condition names it:
    // beside another condition, under !, == false or is false, and through
    // another bool it was handed to. What stands
    // inside a wait condition (a lambda handed to SpinUntil and its kin) is
    // what the wait keeps asking, not a ceiling the fact asserts.
    // Not a ceiling: a floor (elapsed >= x; load only lengthens a
    // reading), a comparison of two timed values (an order or a ratio), an
    // assertion inside a while, a do or a for that can end early (the
    // wait's bound; a foreach is no wait here, so a ceiling on each case of
    // a list is a ceiling), and anything in a member or a class marked
    // [Trait("kind", "benchmark")].
    //
    // What binds and what is read as spelled. Elapsed, ElapsedMilliseconds
    // and ElapsedTicks are read as spelled on any receiver, so a test seam's
    // own Elapsed() is a reading. Stopwatch.GetElapsedTime, the wall-clock
    // properties, Assert's methods, WallClockBudget, the trait and every
    // local are bound.
    //
    // What rule 2 does not see:
    //   a reading returned from a helper (WeakEventExitTests) or handed to
    //     one as an argument;
    //   a reading kept in a field, an array element, a tuple or a
    //     deconstructed variable. A fact that medians its own samples
    //     (AvalonDocumentBufferCensus) is of this kind, and is already the
    //     warm-median shape;
    //   a ceiling that is not an assertion of these three: an if on a
    //     reading that throws, fails or collects a line for a later
    //     assertion (the gate's reading-list bound is one);
    //   a ! deeper than the whole first argument (done && !(elapsed > x)),
    //     whose comparison is read in the assertion's own sense;
    //   a ceiling inside a while on something other than a wait
    //     (while (reader.Read())), which is read as a wait's bound;
    //   a bool assigned more than once, or kept in a field, before it is
    //     asserted by name;
    //   a comparison made by CompareTo or TimeSpan.Compare, which is read
    //     by its comparison with zero: elapsed.CompareTo(limit) < 0 is seen,
    //     and limit.CompareTo(elapsed) > 0 is read as a floor;
    //   a reading as Assert.InRange's low or high: only what is asserted
    //     (actual) is judged, so a timed bound is neither ceiling nor floor
    //     here;
    //   a benchmark trait a base class carries;
    //   a list pattern, and a switch expression's arms: a comparison in an
    //     arm is read in the assertion's sense, a relational pattern in one
    //     is not read;
    //   a member of a bool local other than Value and GetValueOrDefault
    //     (HasValue), which is read as saying nothing.

    /// <summary>The unit project's elapsed-time ceilings as they stand. The
    /// census first read 19 here, on 2026-11-16; the sweep that followed
    /// (plan 47 CB-6) gave 17 of them a witness or a warm median and left
    /// these two, each kept because no seam observes what the call really
    /// waited. Its entry names the seam that would.</summary>
    private static readonly (string File, string Member, int Count, string Reason)[] UnitElapsedCeilings =
    [
        ("HeldVaultStoreFileTests.cs", "ASaveThatStaysHeldFailsAfterAShortWaitAndLeavesTheFile", 1,
            "no-wait-witness: AnchoredVaultStore has no delay seam, so nothing observes what a held save's backoff really waited"),
        ("HotExitTests.cs", "AClosedOpenStopsWaitingForTheLastSession", 1,
            "no-wait-witness: the hot-exit open answers HotExitReasonEarlierWorkPending whether the close cancelled its wait or "
            + "HotExitReopenWait ran out; a seam that says which ended the wait would be the witness"),
    ];

    /// <summary>The gate's one elapsed-time ceiling, as it stands. Plan 47
    /// CB-7 weighs it.</summary>
    private static readonly (string File, string Member, int Count, string Reason)[] GateElapsedCeilings =
    [
        ("FocusSettleWatchTests.cs", "AReadThatSeesFocusLeaveFailsAtOnce", 1, "unswept"),
    ];

    [Fact]
    public void AnElapsedCeilingGoesThroughWallClockBudgetOrIsABenchmark()
    {
        foreach (TestProject read in Walk.Value)
        {
            bool unit = read.Name == UnitProject;
            IReadOnlyList<string> faults = PinnedListFaults(
                ElapsedCeilings(read.Compilation, read.Sources, FileIn(read.Name)),
                unit ? UnitElapsedCeilings : GateElapsedCeilings,
                unit ? UnitCeilingReasons : GateCeilingReasons);
            Assert.True(
                faults.Count == 0,
                $"{read.Name}: a fact asserts that a wall-clock reading stayed under a ceiling, and the pinned list does not "
                + "hold it as it is. A loaded runner lengthens every reading.\n"
                + "new: assert the state that shows the call did not wait, or take the reading through "
                + "WallClockBudget.WarmMedian, or mark a benchmark [Trait(\"kind\", \"benchmark\")]; failing those, add an entry "
                + "and say in the PR why.\n"
                + "stale: lower or remove it.\n"
                + string.Join("\n", faults));
        }
    }

    /// <summary>Rule 2: every elapsed-time ceiling a fact asserts in
    /// <paramref name="sources"/>.</summary>
    internal static IReadOnlyList<ClockSite> ElapsedCeilings(
        CSharpCompilation compilation, IReadOnlyList<SyntaxTree> sources, Func<SyntaxTree, string> fileOf)
    {
        List<ClockSite> ceilings = [];
        foreach (SyntaxTree tree in sources)
        {
            SemanticModel? model = null;
            foreach (MemberDeclarationSyntax member in tree.GetRoot().DescendantNodes().OfType<MemberDeclarationSyntax>())
            {
                // A member that spells no reading has no timed local and no
                // ceiling, so it is not bound.
                if (member is BaseTypeDeclarationSyntax or BaseNamespaceDeclarationSyntax or DelegateDeclarationSyntax
                    || !member.DescendantNodes().OfType<SimpleNameSyntax>().Any(name => ReadingNames.Contains(name.Identifier.ValueText)))
                {
                    continue;
                }
                model ??= compilation.GetSemanticModel(tree);
                var readings = new Readings(member, model);
                if (readings.IsABenchmark())
                {
                    continue;
                }
                foreach (InvocationExpressionSyntax assertion in member.DescendantNodes().OfType<InvocationExpressionSyntax>())
                {
                    string? name = ClockSyntax.NameCalled(assertion.Expression);
                    if (name is not ("True" or "False" or "InRange")
                        || assertion.ArgumentList.Arguments.Count == 0
                        || !ClockSyntax.IsMemberOf(assertion, model, "Assert", "Xunit"))
                    {
                        continue;
                    }
                    // The condition is found by the parameter it binds to,
                    // so a named argument in any order is the one read.
                    if (ClockSyntax.ArgumentFor(assertion, model, name == "InRange" ? "actual" : "condition") is not { } first)
                    {
                        continue;
                    }
                    // Assert.False refuses its condition.
                    int count = name == "InRange"
                        ? (readings.Holds(first) ? 1 : 0)
                        : readings.Ceilings(first, turned: name == "False");
                    // An assertion inside a wait's loop is the wait's bound.
                    // A foreach is no wait here: a ceiling on each case of a
                    // list is a ceiling.
                    if (count == 0 || ClockSyntax.StandsInAPoll(assertion, foreachIsAPoll: false))
                    {
                        continue;
                    }
                    var at = new ClockSite(fileOf(tree), ClockSyntax.MemberOf(assertion), LineOf(assertion), TextOf(assertion));
                    ceilings.AddRange(Enumerable.Repeat(at, count));
                }
            }
        }
        return ceilings;
    }

    /// <summary>Every name a reading is spelled with. A member that holds
    /// none of them is skipped unbound.</summary>
    private static readonly HashSet<string> ReadingNames = new(StringComparer.Ordinal)
    {
        "Elapsed",
        "ElapsedMilliseconds",
        "ElapsedTicks",
        "GetElapsedTime",
        "GetTimestamp",
        "GetUtcNow",
        "GetLocalNow",
        "UtcNow",
        "Now",
        "TickCount",
        "TickCount64",
    };

    /// <summary>The readings of one member, its lambdas and local functions
    /// included: which expressions hold one, and which locals are timed.</summary>
    private sealed class Readings
    {
        private readonly MemberDeclarationSyntax _member;
        private readonly SemanticModel _model;
        private readonly HashSet<ILocalSymbol> _timed = new(SymbolEqualityComparer.Default);
        private readonly HashSet<string> _timedNames = new(StringComparer.Ordinal);
        private readonly List<(ILocalSymbol Local, ExpressionSyntax Value)> _flows = [];

        public Readings(MemberDeclarationSyntax member, SemanticModel model)
        {
            _member = member;
            _model = model;

            // A local is timed when its initialiser or any assignment to it
            // holds a reading or a timed local, to a fixed point.
            List<(ILocalSymbol Local, ExpressionSyntax Value)> flows = _flows;
            foreach (SyntaxNode node in member.DescendantNodes())
            {
                switch (node)
                {
                    case VariableDeclaratorSyntax { Initializer.Value: { } value } declarator
                        when model.GetDeclaredSymbol(declarator) is ILocalSymbol local:
                        flows.Add((local, value));
                        break;
                    case AssignmentExpressionSyntax assignment
                        when model.GetSymbolInfo(assignment.Left).Symbol is ILocalSymbol local:
                        flows.Add((local, assignment.Right));
                        break;
                    // A pattern's variable holds what it was matched against:
                    // a reading where its property path names one, and
                    // otherwise the input or a member of it.
                    case SingleVariableDesignationSyntax designation
                        when designation.Ancestors().OfType<IsPatternExpressionSyntax>().FirstOrDefault() is { } matched
                            && matched.Pattern.Span.Contains(designation.Span)
                            && model.GetDeclaredSymbol(designation) is ILocalSymbol local:
                        if (designation.Ancestors().TakeWhile(ancestor => ancestor != matched).OfType<SubpatternSyntax>().Any(NamesAReading))
                        {
                            _ = _timed.Add(local);
                            _ = _timedNames.Add(local.Name);
                        }
                        else
                        {
                            flows.Add((local, matched.Expression));
                        }
                        break;
                }
            }
            bool grew = true;
            while (grew)
            {
                grew = false;
                foreach ((ILocalSymbol local, ExpressionSyntax value) in flows)
                {
                    if (!_timed.Contains(local) && Holds(value))
                    {
                        _ = _timed.Add(local);
                        _ = _timedNames.Add(local.Name);
                        grew = true;
                    }
                }
            }
        }

        /// <summary>Whether the member, or a type around it, carries
        /// <c>[Trait("kind", "benchmark")]</c>.</summary>
        public bool IsABenchmark()
        {
            // By symbol, so a trait on another part of a partial class
            // counts. A field declares its symbols one level down, so it
            // starts at its type.
            ISymbol? declared = _member is BaseFieldDeclarationSyntax
                ? _member.Ancestors().OfType<BaseTypeDeclarationSyntax>().Select(type => _model.GetDeclaredSymbol(type)).FirstOrDefault()
                : _model.GetDeclaredSymbol(_member);
            for (ISymbol? symbol = declared; symbol is not null; symbol = symbol.ContainingType)
            {
                if (symbol.GetAttributes().Any(attribute =>
                    attribute.AttributeClass?.Name == "TraitAttribute"
                    && attribute.ConstructorArguments.Select(argument => argument.Value).ToArray() is ["kind", "benchmark"]))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>A ceiling is <c>a &lt; b</c>, <c>a &lt;= b</c>,
        /// <c>b &gt; a</c> or <c>b &gt;= a</c> where <c>a</c> holds a reading
        /// or a timed local and <c>b</c> holds neither. Where the assertion
        /// refuses its condition (<paramref name="turned"/>), the ceiling is
        /// the comparison's other sense: <c>a &gt; b</c>, <c>a &gt;= b</c>,
        /// <c>b &lt; a</c> or <c>b &lt;= a</c>.</summary>
        public bool IsACeiling(BinaryExpressionSyntax comparison, bool turned)
        {
            bool? leftIsLess = comparison.Kind() switch
            {
                SyntaxKind.LessThanExpression or SyntaxKind.LessThanOrEqualExpression => true,
                SyntaxKind.GreaterThanExpression or SyntaxKind.GreaterThanOrEqualExpression => false,
                _ => null,
            };
            if (leftIsLess is not { } less)
            {
                return false;
            }
            (ExpressionSyntax under, ExpressionSyntax limit) = less != turned
                ? (comparison.Left, comparison.Right)
                : (comparison.Right, comparison.Left);
            return Holds(under) && !Holds(limit);
        }

        /// <summary>The same for a reading matched against a relational
        /// pattern: <c>elapsed is &lt; x</c> is a ceiling and
        /// <c>elapsed is &gt;= x</c> a floor, <c>not</c> turns the pattern
        /// round, and a ceiling joined to a floor (<c>is &gt;= 0 and &lt; x</c>)
        /// is a ceiling.</summary>
        public int Ceilings(IsPatternExpressionSyntax matched, bool turned) =>
            Ceilings(matched.Pattern, Holds(matched.Expression), turned);

        /// <summary>The ceilings a pattern holds, walked whole. What a
        /// relational pattern is matched against is timed when the input
        /// is, or when the property path that leads to it names a reading
        /// (<c>{ ElapsedMilliseconds: &lt; x }</c>,
        /// <c>{ Elapsed.TotalMilliseconds: &lt; x }</c>), to any depth. An
        /// untimed property beside it counts for nothing, and an <c>or</c>
        /// that holds a ceiling is one.</summary>
        private static int Ceilings(PatternSyntax pattern, bool timed, bool turned) => pattern switch
        {
            ParenthesizedPatternSyntax inner => Ceilings(inner.Pattern, timed, turned),
            UnaryPatternSyntax negated => Ceilings(negated.Pattern, timed, !turned),
            BinaryPatternSyntax both => Ceilings(both.Left, timed, turned) + Ceilings(both.Right, timed, turned),
            RecursivePatternSyntax recursive =>
                (recursive.PropertyPatternClause?.Subpatterns.Sum(part => Ceilings(part.Pattern, timed || NamesAReading(part), turned)) ?? 0)
                + (recursive.PositionalPatternClause?.Subpatterns.Sum(part => Ceilings(part.Pattern, timed, turned)) ?? 0),
            RelationalPatternSyntax relational when timed => relational.OperatorToken.Kind() switch
            {
                SyntaxKind.LessThanToken or SyntaxKind.LessThanEqualsToken => turned ? 0 : 1,
                SyntaxKind.GreaterThanToken or SyntaxKind.GreaterThanEqualsToken => turned ? 1 : 0,
                _ => 0,
            },
            _ => 0,
        };

        /// <summary>Whether a property subpattern's path (<c>Name:</c> or
        /// <c>A.B:</c>) names <c>Elapsed</c>, <c>ElapsedMilliseconds</c> or
        /// <c>ElapsedTicks</c>, read as spelled like any member of those
        /// names.</summary>
        private static bool NamesAReading(SubpatternSyntax part) =>
            ((SyntaxNode?)part.NameColon ?? part.ExpressionColon)?.DescendantNodes().OfType<IdentifierNameSyntax>()
                .Any(name => name.Identifier.ValueText is "Elapsed" or "ElapsedMilliseconds" or "ElapsedTicks") ?? false;

        /// <summary>The ceilings an asserted condition holds, each read in
        /// the sense it is asserted in: <c>!</c>, <c>== false</c>,
        /// <c>!= true</c> and <c>is false</c> turn what they cover round. A
        /// bool local given one value (<c>bool fast = elapsed &lt; x;</c>)
        /// stands for that value wherever it is named, another bool it was
        /// handed to included. A bool assigned more than once is not
        /// followed, and a wait condition is not read.</summary>
        public int Ceilings(SyntaxNode condition, bool turned) => Ceilings(condition, turned, new(SymbolEqualityComparer.Default));

        private int Ceilings(SyntaxNode node, bool turned, HashSet<ISymbol> following)
        {
            switch (node)
            {
                case AnonymousFunctionExpressionSyntax when ClockSyntax.IsAWaitCondition(node):
                    return 0;
                case PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.LogicalNotExpression } negated:
                    return Ceilings(negated.Operand, !turned, following);
                case BinaryExpressionSyntax { RawKind: (int)SyntaxKind.EqualsExpression or (int)SyntaxKind.NotEqualsExpression } compared
                    when (BoolLiteral(compared.Left) ?? BoolLiteral(compared.Right)) is { } literal:
                    {
                        // x == false and x != true refuse x.
                        ExpressionSyntax other = BoolLiteral(compared.Left) is null ? compared.Left : compared.Right;
                        bool refuses = compared.IsKind(SyntaxKind.EqualsExpression) != literal;
                        return Ceilings(other, turned != refuses, following);
                    }
                case IsPatternExpressionSyntax matched when BoolPattern(matched.Pattern) is { } expected:
                    return Ceilings(matched.Expression, turned == expected, following);
                // A member of a bool local: Value and GetValueOrDefault are
                // the bool itself; any other (HasValue, ToString) says
                // nothing the bool's value said.
                case MemberAccessExpressionSyntax { Expression: var owner } member
                    when ClockSyntax.Bare(owner) is IdentifierNameSyntax named
                        && _model.GetSymbolInfo(named).Symbol is ILocalSymbol owned
                        && IsABool(owned.Type):
                    return member.Name.Identifier.ValueText is "Value" or "GetValueOrDefault"
                        ? Ceilings(owner, turned, following)
                        : 0;
                case IdentifierNameSyntax name
                    when _model.GetSymbolInfo(name).Symbol is ILocalSymbol local && IsABool(local.Type):
                    {
                        ExpressionSyntax[] values = [.. _flows
                            .Where(flow => SymbolEqualityComparer.Default.Equals(flow.Local, local))
                            .Select(flow => flow.Value)];
                        if (values.Length != 1 || !following.Add(local))
                        {
                            return 0;
                        }
                        int behind = Ceilings(values[0], turned, following);
                        _ = following.Remove(local);
                        return behind;
                    }
            }
            int count = node switch
            {
                BinaryExpressionSyntax comparison when IsACeiling(comparison, turned) => 1,
                IsPatternExpressionSyntax matched => Ceilings(matched, turned),
                _ => 0,
            };
            foreach (SyntaxNode child in node.ChildNodes())
            {
                count += Ceilings(child, turned, following);
            }
            return count;
        }

        /// <summary>A bool, or a nullable one: its Value, its
        /// GetValueOrDefault and its ?? fallback are read through, as the
        /// walk reads through any member or operator.</summary>
        private static bool IsABool(ITypeSymbol type) =>
            type.SpecialType == SpecialType.System_Boolean
            || type is INamedTypeSymbol
            {
                OriginalDefinition.SpecialType: SpecialType.System_Nullable_T,
                TypeArguments: [{ SpecialType: SpecialType.System_Boolean }],
            };

        private static bool? BoolLiteral(ExpressionSyntax expression)
        {
            expression = ClockSyntax.Bare(expression);
            return expression.Kind() switch
            {
                SyntaxKind.TrueLiteralExpression => true,
                SyntaxKind.FalseLiteralExpression => false,
                _ => null,
            };
        }

        /// <summary>What a pattern asks of a bool: <c>is true</c>,
        /// <c>is false</c>, and either under <c>not</c>.</summary>
        private static bool? BoolPattern(PatternSyntax pattern) => pattern switch
        {
            ParenthesizedPatternSyntax inner => BoolPattern(inner.Pattern),
            UnaryPatternSyntax negated => !BoolPattern(negated.Pattern),
            ConstantPatternSyntax constant => BoolLiteral(constant.Expression),
            _ => null,
        };

        /// <summary>Whether an expression holds a reading or a timed local.
        /// What a <c>WallClockBudget</c> call is handed carries nothing: the
        /// call answers a warm median.</summary>
        public bool Holds(ExpressionSyntax expression)
        {
            foreach (SyntaxNode node in expression.DescendantNodesAndSelf(inner => !IsABudgetCall(inner)))
            {
                if (IsAReading(node))
                {
                    return true;
                }
                if (node is IdentifierNameSyntax name
                    && _timedNames.Contains(name.Identifier.ValueText)
                    && _model.GetSymbolInfo(name).Symbol is ILocalSymbol local
                    && _timed.Contains(local))
                {
                    return true;
                }
            }
            return false;
        }

        private bool IsABudgetCall(SyntaxNode node)
        {
            if (node is not InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Expression: var receiver } } call
                || !receiver.ToString().EndsWith("WallClockBudget", StringComparison.Ordinal))
            {
                return false;
            }
            SymbolInfo info = _model.GetSymbolInfo(call);
            return (info.Symbol ?? info.CandidateSymbols.FirstOrDefault()) is not { } symbol
                || symbol.ContainingType?.Name == "WallClockBudget";
        }

        /// <summary>A reading: a member named <c>Elapsed</c>,
        /// <c>ElapsedMilliseconds</c> or <c>ElapsedTicks</c> on anything, a
        /// call to <c>Stopwatch.GetElapsedTime</c>, or the wall clock
        /// (<c>DateTime.UtcNow</c>, <c>DateTime.Now</c>,
        /// <c>DateTimeOffset.UtcNow</c>, <c>Environment.TickCount</c>,
        /// <c>Environment.TickCount64</c>).</summary>
        private bool IsAReading(SyntaxNode node)
        {
            switch (node)
            {
                case MemberBindingExpressionSyntax binding:
                    return binding.Name.Identifier.ValueText is "Elapsed" or "ElapsedMilliseconds" or "ElapsedTicks";
                case MemberAccessExpressionSyntax access:
                    return access.Name.Identifier.ValueText is "Elapsed" or "ElapsedMilliseconds" or "ElapsedTicks"
                        || IsAClock(access, access.Name.Identifier.ValueText);
                // A clock reached without its type's name (using static):
                // GetElapsedTime(began), UtcNow. Bound, or it is no clock.
                case IdentifierNameSyntax bare
                    when bare.Parent is not MemberBindingExpressionSyntax
                        && !(bare.Parent is MemberAccessExpressionSyntax owner && owner.Name == bare):
                    return _model.GetSymbolInfo(bare).Symbol is IMethodSymbol or IPropertySymbol
                        && IsAClock(bare, bare.Identifier.ValueText);
                default:
                    return false;
            }
        }

        /// <summary>The framework's clocks, by the type that owns each.</summary>
        private static readonly (string Type, string Namespace, string[] Members)[] Clocks =
        [
            ("Stopwatch", "System.Diagnostics", ["GetElapsedTime", "GetTimestamp"]),
            ("DateTime", "System", ["UtcNow", "Now"]),
            ("DateTimeOffset", "System", ["UtcNow", "Now"]),
            ("Environment", "System", ["TickCount", "TickCount64"]),
            ("TimeProvider", "System", ["GetUtcNow", "GetLocalNow", "GetTimestamp", "GetElapsedTime"]),
        ];

        private bool IsAClock(SyntaxNode node, string member) => Clocks.Any(clock =>
            clock.Members.Contains(member) && ClockSyntax.IsMemberOf(node, _model, clock.Type, clock.Namespace));
    }

    // ---- Rule 4 (#1788): the gate reads the app's files through GateFile only ----
    //
    // The app saves a file by renaming a staged one over it, with Windows'
    // classic replacing rename. Windows refuses that rename while any handle
    // is open on the target, whatever the handle shares. So a gate read that
    // is open at the instant of a save refuses the save, and nothing about
    // how the read opens the file changes that. What keeps it rare is how
    // briefly and how seldom the gate holds the file: GateFile opens, reads
    // and closes, and its poll pauses between two reads. A read made another
    // way, from a wait that spins above all, is how the gate lost a save for
    // the product it was measuring (#1865, #1788).
    //
    // In the gate's project, outside GateFile.cs and GateFileTests.cs (the
    // reader and its own facts, whose holders are the controls):
    //   a call to File.ReadAllText, ReadAllBytes, ReadAllLines, ReadLines,
    //     OpenRead, OpenText or Open, or an Async form of one, is refused
    //     unless its path is built from AppContext.BaseDirectory: a fixture
    //     that travels with the binaries, which the app never writes;
    //   a new FileStream(...) is refused unless it names FileShare.Delete:
    //     the log and evidence readers, and one journey's deliberate holder
    //     of a lock file.
    // There is no allow-list. Plan 47 worded the rule as "inside a poll";
    // two of the gate's ten polls reached their read through a local
    // function, which syntax cannot follow, so every direct read is refused
    // wherever it stands.
    //
    // What binds and what is read as spelled. File's methods and FileStream
    // are bound (System.IO), so another type's ReadAllText is no read; where
    // one does not bind, the spelling of its type stands in.
    // AppContext.BaseDirectory and FileShare.Delete are read as spelled,
    // anywhere inside the path argument and the creation's arguments.
    //
    // What rule 4 does not see:
    //   a path built from AppContext.BaseDirectory in a local and handed on
    //     by name, which it refuses: write the path in the call;
    //   a read through another type: a StreamReader or an XDocument made
    //     from a path, a File.Copy out of a file the app writes;
    //   a FileStream that names FileShare.Delete and is then held across a
    //     save, and how long a caller keeps the stream GateFile.Open gave it.
    //     Sharing deletion lets the app delete the file; it does not let a
    //     rename through;
    //   a read in GateFile.cs or GateFileTests.cs.

    /// <summary>The reader and its own facts: the two files of the gate's
    /// project rule 4 does not read.</summary>
    private static readonly string[] TheReaderAndItsFacts = ["GateFile.cs", "GateFileTests.cs"];

    private static readonly HashSet<string> DirectFileReads = new(StringComparer.Ordinal)
    {
        "ReadAllText",
        "ReadAllBytes",
        "ReadAllLines",
        "ReadLines",
        "OpenRead",
        "OpenText",
        "Open",
        "ReadAllTextAsync",
        "ReadAllBytesAsync",
        "ReadAllLinesAsync",
        "ReadLinesAsync",
    };

    /// <summary>A floor under the calls on GateFile outside the reader's two
    /// files: 20 on 2026-11-16 (the journeys' own, and the helpers every
    /// paced wait and log read goes through). Fewer means the rule lost
    /// sight of the reader, not that the gate reads less.</summary>
    private const int GateFileCallsAtLeast = 16;

    [Fact]
    public void TheGateReadsAppFilesThroughGateFileOnly()
    {
        TestProject gate = Walk.Value.Single(project => project.Name == GateProject);
        IReadOnlyList<ClockSite> refused = DirectReadsOfFiles(gate.Compilation, gate.Sources, FileIn(GateProject));
        Assert.True(
            refused.Count == 0,
            $"{refused.Count} reads in the gate's project open a file without GateFile. The app replaces a file it saves "
            + "by a rename Windows refuses while any handle is open on the target, so a read costs the app its save when "
            + "it is open at that instant. Read through GateFile: PollText for a wait (it pauses between reads), ReadText "
            + "for one read, TryReadText for a failure message. A fixture's path is built from AppContext.BaseDirectory "
            + "inside the call. This rule has no allow-list:\n" + string.Join("\n", refused));

        int calls = GateFileCalls(gate.Sources, FileIn(GateProject));
        Assert.True(
            calls >= GateFileCallsAtLeast,
            $"rule 4 saw {calls} calls on GateFile in {GateProject}; it saw at least {GateFileCallsAtLeast} when the census "
            + "was written, so it has lost sight of the reader.");
    }

    /// <summary>Rule 4 on planted sources: a direct read is reported
    /// wherever it stands, and a fixture's read, a stream that shares
    /// deletion and another type's method of the same name are left
    /// alone.</summary>
    [Theory]
    [MemberData(nameof(PlantedFileReads))]
    public void TheFileReadRuleSeesAPlantedRead(string form, string planted, string expected)
    {
        CSharpCompilation unit = Walk.Value.Single(project => project.Name == UnitProject).Compilation;
        SyntaxTree tree = CSharpSyntaxTree.ParseText(
            "namespace SlateWindows.Tests.Planted;\n"
            + "static class Notes { public static string ReadAllText(string path) => path; }\n"
            + "sealed class P {\n" + planted + "\n}\n",
            (CSharpParseOptions)unit.SyntaxTrees.First().Options,
            path: "Planted.cs");
        Assert.DoesNotContain(tree.GetDiagnostics(), diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        CSharpCompilation compilation = unit.AddSyntaxTrees(tree);
        Diagnostic[] unbound = [.. compilation.GetSemanticModel(tree).GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)];
        Assert.True(unbound.Length == 0, $"{form}: the planted source does not bind:\n" + string.Join("\n", unbound.Select(d => d.ToString())));

        string seen = string.Join("; ", DirectReadsOfFiles(compilation, [tree], _ => "Planted.cs").Select(site => $"{site.Member}: refused"));
        if (seen.Length == 0)
        {
            seen = "clean";
        }
        Assert.True(expected == seen, $"{form}: expected {expected}, the rule answered {seen}");
    }

    public static TheoryData<string, string, string> PlantedFileReads() => new()
    {
        {
            "a read in a wait's lambda",
            "void F(string path) { Assert.True(SpinWait.SpinUntil(() => File.ReadAllText(path).Length > 0, 1000)); }",
            "F: refused"
        },
        {
            "a read in a loop",
            "void F(string path) { while (File.ReadAllBytes(path).Length == 0) { Thread.Yield(); } }",
            "F: refused"
        },
        {
            "a read in a local function a wait calls",
            "void F(string path) { string Read() => File.ReadAllText(path); Assert.True(SpinWait.SpinUntil(() => Read().Length > 0, 1000)); }",
            "Read: refused"
        },
        { "a single read", "void F(string path) { Assert.Equal(\"old\", File.ReadAllText(path)); }", "F: refused" },
        { "lines, read lazily", "void F(string path) { Assert.Empty(File.ReadLines(path)); }", "F: refused" },
        { "every line", "void F(string path) { Assert.Empty(File.ReadAllLines(path)); }", "F: refused" },
        { "an awaited read", "async Task F(string path) { Assert.Empty(await File.ReadAllTextAsync(path)); }", "F: refused" },
        { "File.OpenRead", "void F(string path) { using FileStream held = File.OpenRead(path); }", "F: refused" },
        { "File.OpenText", "void F(string path) { using StreamReader held = File.OpenText(path); }", "F: refused" },
        {
            "File.Open, sharing deletion or not",
            "void F(string path) { using FileStream held = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete); }",
            "F: refused"
        },
        {
            "a stream that does not share deletion",
            "void F(string path) { using var held = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite); }",
            "F: refused"
        },
        {
            "a stream made by its target's type",
            "void F(string path) { using FileStream held = new(path, FileMode.Open); }",
            "F: refused"
        },
        {
            "a path from the base directory kept in a local first",
            "void F() { string fixture = Path.Combine(AppContext.BaseDirectory, \"fixtures\", \"chords.json\"); _ = File.ReadAllText(fixture); }",
            "F: refused"
        },
        {
            "two reads in one member",
            "void F(string path) { _ = File.ReadAllText(path); _ = File.ReadAllBytes(path); }",
            "F: refused; F: refused"
        },
        {
            "a fixture's read",
            "void F() { _ = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, \"fixtures\", \"chords.json\")); }",
            "clean"
        },
        {
            "a stream that shares deletion",
            "void F(string path) { using var log = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete); }",
            "clean"
        },
        { "another type's ReadAllText", "void F(string path) { _ = Notes.ReadAllText(path); }", "clean" },
        { "a write, a delete and a look", "void F(string path) { File.WriteAllText(path, \"new\"); _ = File.Exists(path); File.Delete(path); }", "clean" },
    };

    /// <summary>Rule 4: every read of a file in <paramref name="sources"/>
    /// that does not go through GateFile.</summary>
    internal static IReadOnlyList<ClockSite> DirectReadsOfFiles(
        CSharpCompilation compilation, IReadOnlyList<SyntaxTree> sources, Func<SyntaxTree, string> fileOf)
    {
        List<ClockSite> refused = [];
        foreach (SyntaxTree tree in sources)
        {
            string file = fileOf(tree);
            if (TheReaderAndItsFacts.Contains(file))
            {
                continue;
            }
            SemanticModel? model = null;
            foreach (SyntaxNode node in tree.GetRoot().DescendantNodes())
            {
                switch (node)
                {
                    case InvocationExpressionSyntax call
                        when ClockSyntax.NameCalled(call.Expression) is { } name && DirectFileReads.Contains(name):
                        model ??= compilation.GetSemanticModel(tree);
                        if (ClockSyntax.IsMemberOf(call, model, "File", "System.IO")
                            && !(ClockSyntax.ArgumentFor(call, model, "path") is { } path && Names(path, "AppContext", "BaseDirectory")))
                        {
                            refused.Add(new ClockSite(file, ClockSyntax.MemberOf(call), LineOf(call), TextOf(call)));
                        }
                        break;
                    case BaseObjectCreationExpressionSyntax creation:
                        model ??= compilation.GetSemanticModel(tree);
                        if (IsAFileStream(creation, model)
                            && creation.ArgumentList?.Arguments.Any(argument => Names(argument.Expression, "FileShare", "Delete")) != true)
                        {
                            refused.Add(new ClockSite(file, ClockSyntax.MemberOf(creation), LineOf(creation), TextOf(creation)));
                        }
                        break;
                }
            }
        }
        return refused;
    }

    private static bool IsAFileStream(BaseObjectCreationExpressionSyntax creation, SemanticModel model) =>
        model.GetTypeInfo(creation).Type is { } type && type.TypeKind != TypeKind.Error
            ? type.Name == "FileStream" && type.ContainingNamespace?.ToDisplayString() == "System.IO"
            : creation is ObjectCreationExpressionSyntax { Type: var written }
                && written.DescendantNodesAndSelf().OfType<SimpleNameSyntax>().LastOrDefault()?.Identifier.ValueText == "FileStream";

    /// <summary>Whether an expression spells <c>type.member</c> anywhere
    /// inside it.</summary>
    private static bool Names(ExpressionSyntax expression, string type, string member) => expression
        .DescendantNodesAndSelf()
        .OfType<MemberAccessExpressionSyntax>()
        .Any(access => access.Name.Identifier.ValueText == member
            && access.Expression.DescendantNodesAndSelf().OfType<SimpleNameSyntax>().LastOrDefault()?.Identifier.ValueText == type);

    /// <summary>The calls made on GateFile outside the reader's two
    /// files.</summary>
    private static int GateFileCalls(IReadOnlyList<SyntaxTree> sources, Func<SyntaxTree, string> fileOf) => sources
        .Where(tree => !TheReaderAndItsFacts.Contains(fileOf(tree)))
        .Sum(tree => tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().Count(call =>
            call.Expression is MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.ValueText: "GateFile" } }));
}
