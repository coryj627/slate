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
        public static string? NameCalled(ExpressionSyntax callee) => callee switch
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
        public static bool StandsInAPoll(SyntaxNode node)
        {
            foreach (SyntaxNode ancestor in node.Ancestors())
            {
                switch (ancestor)
                {
                    case WhileStatementSyntax or DoStatementSyntax or CommonForEachStatementSyntax:
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
            if (loop.Condition is not BinaryExpressionSyntax comparison
                || !(comparison.Kind() is SyntaxKind.LessThanExpression or SyntaxKind.LessThanOrEqualExpression
                    or SyntaxKind.GreaterThanExpression or SyntaxKind.GreaterThanOrEqualExpression
                    or SyntaxKind.NotEqualsExpression or SyntaxKind.EqualsExpression))
            {
                return true;
            }
            HashSet<string> own = [.. loop.Declaration?.Variables.Select(variable => variable.Identifier.ValueText) ?? []];
            own.UnionWith(loop.Initializers
                .OfType<AssignmentExpressionSyntax>()
                .Select(assignment => assignment.Left)
                .OfType<IdentifierNameSyntax>()
                .Select(name => name.Identifier.ValueText));
            bool readsItsOwn = new[] { comparison.Left, comparison.Right }
                .Any(side => side is IdentifierNameSyntax name && own.Contains(name.Identifier.ValueText));
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
            while (expression.Parent is ParenthesizedExpressionSyntax or CastExpressionSyntax)
            {
                expression = (ExpressionSyntax)expression.Parent;
            }
            return expression;
        }

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
        "using System.Diagnostics;\nnamespace SlateWindows.Tests.Planted;\n"
        + "sealed class PlantedSeam { public Action? BeforeForTests; public Func<Task>? PublicationForTests; "
        + "public event Action? Fired; public PlantedSeam Context => this; public void Await(Task work, string what) { } "
        + "public Task AwaitAsync(Task work) => work; "
        + "public void PumpFor(TimeSpan span) { } public void Fire() => Fired?.Invoke(); "
        + "public void Decode(Func<int, bool> progress) { } public TimeSpan Elapsed() => TimeSpan.Zero; }\n"
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
            "void F() { _ = SpinWait.SpinUntil(() => { Thread.Sleep(5); return done; }, 1000); }",
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
    // A delay that is a lambda's whole body is no statement, so no site. A
    // lambda handed to anything else, a fact's RunSta(() => { ... }) included,
    // is searched like a method body.
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
    //   a wait on a gate nobody sets (gate.Wait(500)), which sleeps under
    //     another name;
    //   how often a delay runs: one in a helper is one site, however many
    //     facts call the helper;
    //   a hold inside a callback the product is handed as an argument, or
    //     inside a lambda kept in a local or a parameter. Both read as
    //     sites: nothing in the source tells either from a fact's own body
    //     in a lambda. Such an entry says deliberate-hold or
    //     timeout-under-test;
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
    /// on 2026-11-16: 67 Thread.Sleep, 46 Task.Delay, 35 PumpFor and 8
    /// RunDispatcherFor in the unit project, and 51 Thread.Sleep in the
    /// gate's, which calls none of the other three. Fewer means the rule
    /// lost sight of that name, not that the project waits less.</summary>
    private static readonly (string Project, string Name, int Calls)[] DelayCallsAtLeast =
    [
        (UnitProject, "Thread.Sleep", 62),
        (UnitProject, "Task.Delay", 42),
        (UnitProject, "PumpFor", 31),
        (UnitProject, "RunDispatcherFor", 6),
        (GateProject, "Thread.Sleep", 46),
    ];

    /// <summary>The unit project's fixed delays as they stand, each with the
    /// word that says why it is not a wait on a condition and, after the
    /// colon, what it holds. 80 sites on 2026-11-16: 27 Thread.Sleep, 13
    /// Task.Delay and 40 fixed pumps. An entry that says a wait "would do"
    /// is a positive wait written as a fixed delay, kept under the nearest
    /// word until it is one.</summary>
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
        ("Censuses/AvalonDocumentBufferCensus.cs", "FlagFreeSameLengthDrift_IsFoundOnIdleAndAgainBeforeSave", 1,
            "timeout-under-test: the pump outlasts the session's 10 ms integrity delay; a wait on the recovery count would do"),
        ("Censuses/AvalonDocumentBufferCensus.cs", "FlagFreeSameLengthDrift_CannotBypassDirtySaveOrCloseGates", 1,
            "timeout-under-test: the pump outlasts the 300 ms integrity delay, whose check must leave the tab dirty"),
        ("Censuses/CallbackConcurrencyCensus.cs", "RegisterUnregisterOnce", 1,
            "deliberate-hold: a millisecond for events to land between the register and the unregister"),
        ("Censuses/CanonicalHighlightCensus.cs", "LoadedCoordinatorQuiescesAndRefreshesForEditScrollAndResize", 1,
            "quiesce: the shown editor finishes loading before its coordinator is read; a wait on the coordinator would do"),
        ("Censuses/CanonicalHighlightCensus.cs", "LoadedCoordinatorQuiescesAndRefreshesForEditScrollAndResize", 2,
            "late-arrival: 160 ms after a refresh settles no further refresh comes"),
        ("CitationAsyncInterleavingTests.cs", "QuiesceAsync", 1, "quiesce: 40 drains of the seed and both leaves"),
        ("CitationAsyncInterleavingTests.cs", "AReloadMidJumpRetargetsTheParkedPressInsteadOfSilencingIt", 1, "quiesce: 40 drains of the leaf"),
        ("CitationAsyncInterleavingTests.cs", "AFailedSeedStopsBothLeavesReadingCoreNotJustTheBibliography", 1, "quiesce: 40 drains of the leaf"),
        ("CitationAsyncInterleavingTests.cs", "ReloadBibliographyRunsTheSeedOffTheCallersThread", 1, "quiesce: 40 awaits of the seed's work"),
        ("CitationAsyncInterleavingTests.cs", "ASecondReloadIsRefusedWhileTheFirstIsStillRunning", 1, "quiesce: 40 awaits of the seed's work"),
        ("CitationAsyncInterleavingTests.cs", "AReloadPublishesTheClearBeforeTheNewReadLands", 1, "quiesce: 40 drains of the leaf"),
        ("CitationAsyncInterleavingTests.cs", "AGatedBodyRunsOffTheCallersThreadEvenWhenTheSeedAlreadySettled", 1, "quiesce: 40 drains of the leaf"),
        ("CitationAsyncInterleavingTests.cs", "ShutdownReleasesParkedBodiesWithoutRunningThem", 1,
            "late-arrival: 20 ms after the drain no parked body has run"),
        ("HistoryAsyncInterleavingTests.cs", "QuiesceAsync", 1, "quiesce: 10 drains, since a drained body can queue follow-up work"),
        ("HotExitTests.cs", "AChangedSnapshotIsWrittenAfterTheDebounce", 1, "late-arrival: inside the debounce the second edit is not written"),
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
        ("HotExitTests.cs", "ARetriedRetirementNeverDisplacesASuccessor", 1, "late-arrival: the errored retirement stays parked"),
        ("HotExitTests.cs", "AnErroredClaimIsRetriedWithItsOwnIncarnation", 1, "quiesce: the errored claim's outcome lands before the next edit"),
        ("HotExitTests.cs", "AnAdoptingClaimTakenByAnotherClaimIsNotKept", 1, "quiesce: the taken claim's outcome lands before the close"),
        ("HotExitTests.cs", "AnOldClaimsLateOutcomeNeverCountsForANewOne", 1,
            "deliberate-hold: a gap between the episode's end and the next edit while the old write is held"),
        ("HotExitTests.cs", "AnOldClaimsLateOutcomeNeverCountsForANewOne", 1,
            "late-arrival: the old write's outcome arrives and the new text stays stored"),
        ("HotExitTests.cs", "ARefusedDiscardKeepsTheWriterFencedUntilItResolves", 1, "late-arrival: the fenced writer stores no later edit"),
        ("HotExitTests.cs", "AWriteThatLandsAfterItsTabTurnedCleanIsRetired", 1, "quiesce: the released write lands and is retired before the crash"),
        ("HotExitTests.cs", "ADiscardedUnkeptTabEditedLaterIsKept", 1, "late-arrival: after the discard resolves no entry is stored"),
        ("HotExitTests.cs", "ASessionEndThatIgnoresCancelAbandons", 1, "late-arrival: after the session ends no retirement runs"),
        ("HotExitTests.cs", "RestoreThenDiscardThenReopenGetsNothing", 1, "quiesce: the adoption's outcome lands before the next tab opens"),
        ("HotExitTests.cs", "DivergentPeersAreReconciledIntoARecoveredNote", 1, "distinct-timestamp: the two kept entries need different times"),
        ("HotExitTests.cs", "AStalledWriteInOneVaultNeverHoldsAnother", 1,
            "quiesce: the first vault's claim lands and its write reaches the held call"),
        ("HotExitTests.cs", "ADamagedRetirementResolvesWithItsReason", 1, "late-arrival: the damaged retirement is not retried"),
        ("PanelWorkSchedulerTests.cs", "Run", 1, "deliberate-hold: the compute body is held for the delay its caller asks"),
        ("RescanTests.cs", "TheHistoryPanelReloadsAfterARescan", 1, "quiesce: 20 turns of both queues, then no history load has run"),
        ("RescanTests.cs", "Settle", 2, "quiesce: counted turns of both queues, in one fact's own Settle and in the harness's"),
        ("RescanTests.cs", "DependentsHarness", 1, "quiesce: 20 turns of both queues before the harness is handed over"),
        ("RescanTests.Round5.cs", "TheGraphAndConnectionsProbesAreTwoOperationsAtTheSeam", 1,
            "quiesce: 20 turns of both queues after the graph tab opens"),
        ("SaveChainAdmissionTests.cs", "Settle", 1, "quiesce: 20 pumped turns, for a fact that asserts nothing happened"),
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
        // waits only where something takes its time: an await, a block on
        // it, or a call it is handed to. One raced by Task.WhenAny is a
        // bound on other work, and one returned or kept is not followed.
        ExpressionSyntax waits = call;
        bool taken = kind != DelayKind.TaskDelay;
        bool handedOn = false;
        while (true)
        {
            switch (waits.Parent)
            {
                case ParenthesizedExpressionSyntax parenthesized:
                    waits = parenthesized;
                    continue;
                case AwaitExpressionSyntax awaited:
                    waits = awaited;
                    taken = true;
                    continue;
                case ConditionalAccessExpressionSyntax conditional when conditional.WhenNotNull == waits:
                    waits = conditional;
                    continue;
                case MemberAccessExpressionSyntax { Parent: InvocationExpressionSyntax made } access when access.Expression == waits:
                    switch (access.Name.Identifier.ValueText)
                    {
                        case "ConfigureAwait":
                            waits = made;
                            continue;
                        case "Wait":
                            waits = made;
                            taken = true;
                            continue;
                        case "GetAwaiter" when made.Parent is MemberAccessExpressionSyntax
                        {
                            Name.Identifier.ValueText: "GetResult",
                            Parent: InvocationExpressionSyntax result,
                        }:
                            waits = result;
                            taken = true;
                            continue;
                    }
                    break;
                case ArgumentSyntax when !handedOn && ClockSyntax.TakenBy(waits) is InvocationExpressionSyntax taking:
                    if (ClockSyntax.NameCalled(taking.Expression) == "WhenAny"
                        && ClockSyntax.IsMemberOf(taking, model, "Task", "System.Threading.Tasks"))
                    {
                        return Standing.NotADelay;
                    }
                    waits = taking;
                    taken = true;
                    handedOn = true;
                    continue;
            }
            break;
        }
        if (!taken)
        {
            return Standing.NotADelay;
        }
        if (waits.Parent is not ExpressionStatementSyntax statement)
        {
            return Standing.NotAStatement;
        }
        if (ClockSyntax.StandsInAPoll(statement))
        {
            return Standing.Poll;
        }
        if (ClockSyntax.FunctionAround(statement) is AnonymousFunctionExpressionSyntax lambda)
        {
            ExpressionSyntax handed = ClockSyntax.Outermost(lambda);
            ExpressionSyntax? taking = ClockSyntax.TakenBy(handed);
            if (taking is InvocationExpressionSyntax condition
                && ClockSyntax.NameCalled(condition.Expression) is "SpinUntil" or "PumpUntil" or "RunUntil" or "WaitFor" or "WaitUntil")
            {
                return Standing.WaitCondition;
            }
            if (handed.Parent is AssignmentExpressionSyntax assignment && assignment.Right == handed && IsASeam(assignment, model))
            {
                return Standing.Hold;
            }
            bool startsWork = taking switch
            {
                InvocationExpressionSyntax start => ClockSyntax.NameCalled(start.Expression) switch
                {
                    "Run" => ClockSyntax.IsMemberOf(start, model, "Task", "System.Threading.Tasks"),
                    "StartNew" => ClockSyntax.IsMemberOf(start, model, "TaskFactory", "System.Threading.Tasks"),
                    "QueueUserWorkItem" => ClockSyntax.IsMemberOf(start, model, "ThreadPool", "System.Threading"),
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

    // ---- Rule 2: an elapsed ceiling goes through WallClockBudget or is a benchmark ----
    //
    // A reading is a member named Elapsed, ElapsedMilliseconds or
    // ElapsedTicks, a call to Stopwatch.GetElapsedTime, or the wall clock:
    // DateTime.UtcNow, DateTime.Now, DateTimeOffset.UtcNow,
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
    // Not a ceiling: a floor (elapsed >= x; load only lengthens a
    // reading), a comparison of two timed values (an order or a ratio), an
    // assertion inside a loop rule 1 calls a poll (the wait's bound), and
    // anything in a member or a class marked [Trait("kind", "benchmark")].
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
    //     whose comparison is read in the assertion's own sense.

    /// <summary>The unit project's elapsed-time ceilings as they stand: 19
    /// on 2026-11-16. "None shown yet" is followed by the state that would
    /// show the call did not wait; the ceiling leaves this list when a
    /// mutation has been seen red at that state. "Nearest word" marks a
    /// budget or an oracle the word does not describe.</summary>
    private static readonly (string File, string Member, int Count, string Reason)[] UnitElapsedCeilings =
    [
        ("ConnectionsLeafTests.Rescan.cs", "AClosesTeardownCancelsTheProbesTreeQueryAndDrainsAtOnce", 1,
            "no-wait-witness: none shown yet; the candidate is observed(), read straight after Dispose"),
        ("ConnectionsLeafTests.Rescan.cs", "AClosesTeardownCancelsTheProbesGraphQueryAndDrainsAtOnce", 1,
            "no-wait-witness: none shown yet; the candidate is observed(), read straight after Dispose"),
        ("HeldVaultStoreFileTests.cs", "ASaveThatStaysHeldFailsAfterAShortWaitAndLeavesTheFile", 1,
            "no-wait-witness: AnchoredVaultStore has no seam that observes what a held save's backoff really waited"),
        ("HotExitTests.cs", "AClosedOpenStopsWaitingForTheLastSession", 1,
            "no-wait-witness: none found yet; nothing read here tells a cancelled open from one that ran out its wait"),
        ("HotExitTests.cs", "ADiscardWaitNeverDeadlocksTheDispatcher", 1,
            "no-wait-witness: none shown yet; the candidate is the discard prompt count, which a wait that ran out its budget raises"),
        ("PumpedDispatcherTests.cs", "ADrainTheDispatcherNeverRunsFailsWithinItsBound", 1,
            "no-wait-witness: none shown yet; the candidate is the TimeoutException and the runner's own bound"),
        ("SingleInstanceCoordinatorTests.cs", "ConnectedPrimaryThatStopsReadingCannotExceedTheSendDeadline", 1,
            "no-wait-witness: none shown yet; the candidate is send.WaitAsync's bound and the refused delivery"),
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
                    ExpressionSyntax first = assertion.ArgumentList.Arguments[0].Expression;
                    // Assert.False refuses its condition, and a ! over the
                    // whole condition refuses it once more.
                    bool turned = name == "False";
                    while (first is ParenthesizedExpressionSyntax or PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.LogicalNotExpression })
                    {
                        if (first is PrefixUnaryExpressionSyntax negated)
                        {
                            turned = !turned;
                            first = negated.Operand;
                        }
                        else
                        {
                            first = ((ParenthesizedExpressionSyntax)first).Expression;
                        }
                    }
                    int count = name == "InRange"
                        ? (readings.Holds(first) ? 1 : 0)
                        : first.DescendantNodesAndSelf().OfType<BinaryExpressionSyntax>()
                            .Count(comparison => readings.IsACeiling(comparison, turned));
                    // An assertion inside a poll is the wait's bound.
                    if (count == 0 || ClockSyntax.StandsInAPoll(assertion))
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

        public Readings(MemberDeclarationSyntax member, SemanticModel model)
        {
            _member = member;
            _model = model;

            // A local is timed when its initialiser or any assignment to it
            // holds a reading or a timed local, to a fixed point.
            List<(ILocalSymbol Local, ExpressionSyntax Value)> flows = [];
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
                    return access.Name.Identifier.ValueText switch
                    {
                        "Elapsed" or "ElapsedMilliseconds" or "ElapsedTicks" => true,
                        "GetElapsedTime" => ClockSyntax.IsMemberOf(access, _model, "Stopwatch", "System.Diagnostics"),
                        "UtcNow" => ClockSyntax.IsMemberOf(access, _model, "DateTime", "System")
                            || ClockSyntax.IsMemberOf(access, _model, "DateTimeOffset", "System"),
                        "Now" => ClockSyntax.IsMemberOf(access, _model, "DateTime", "System"),
                        "TickCount" or "TickCount64" => ClockSyntax.IsMemberOf(access, _model, "Environment", "System"),
                        _ => false,
                    };
                default:
                    return false;
            }
        }
    }
}
