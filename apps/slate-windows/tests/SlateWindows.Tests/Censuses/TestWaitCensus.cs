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
/// The rules bind; they do not read spellings. The walk is the two test
/// projects' own compilations as their builds recorded them
/// (<see cref="TestEvidenceCompilation"/>), so a gate is a gate by its type,
/// whether it is a field, a <c>var</c>, an element of a list or a pattern
/// variable, and a session is a session by its type.
/// </para>
/// <para>
/// Rule 3 (#1673) is the first. A later rule is appended as a block of its
/// own at the end of the file.
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
    //     a drain between the two, so the released work ends while the
    //     session lives.
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

    /// <summary>Fixtures rule 3b names whose teardown is right as it stands:
    /// the product's own lifecycle waits for the released work before it
    /// lets the session go, so the fixture names no drain of its own. Rule
    /// 3a has no such list: there is no right way to dispose a gate a worker
    /// may still reach.</summary>
    private static readonly (string File, string Type, string Reason, string Dated)[] DrainedByTheLifecycle =
    [
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
            DrainedByTheLifecycle,
            "These release held work and then dispose a session with no drain between, so the released work can "
            + "run after the session is gone. Wait for it first: TeardownWitness.DrainBeforeTheSessionGoes over the "
            + "product's own drain (WhenRefreshWorkDrained, WhenAllWorkDrained, WhenIdle), then the session:");
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
    /// the first release and that teardown.</summary>
    internal static IReadOnlyList<Violation> ReleasesWithNoDrainBeforeTheSession(
        CSharpCompilation compilation, IReadOnlyList<SyntaxTree> sources, Func<SyntaxTree, string> fileOf)
    {
        var teardowns = new Teardowns(compilation);
        List<Violation> found = [];
        foreach ((INamedTypeSymbol type, MethodDeclarationSyntax method) in teardowns.DisposeMethods(sources))
        {
            IReadOnlyList<Step> steps = teardowns.StepsOf(method);
            int released = IndexOfFirst(steps, 0, StepKind.Release);
            if (released < 0)
            {
                continue;
            }
            int gone = IndexOfFirst(steps, released + 1, StepKind.Teardown);
            if (gone < 0)
            {
                continue;
            }
            bool drained = false;
            for (int index = released + 1; index < gone; index++)
            {
                drained |= steps[index].Kind == StepKind.Drain;
            }
            if (!drained)
            {
                found.Add(new Violation(
                    fileOf(method.SyntaxTree),
                    type.Name,
                    LineOf(steps[gone].Site),
                    $"{method.Identifier.ValueText} releases {steps[released].Text} (line {LineOf(steps[released].Site)}) "
                    + $"and then tears the session down through {steps[gone].Text}, with no drain between"));
            }
        }
        return found;
    }

    private static int IndexOfFirst(IReadOnlyList<Step> steps, int from, StepKind kind)
    {
        for (int index = from; index < steps.Count; index++)
        {
            if (steps[index].Kind == kind)
            {
                return index;
            }
        }
        return -1;
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
                        foreach (ExpressionSyntax resource in ResourcesOf(used))
                        {
                            Disposed(used, used.Span.End, resource, model.GetTypeInfo(resource).Type, null, "using " + resource);
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
                // The receiver as declared, under any cast: ((IDisposable)gate)
                // is still the gate.
                ExpressionSyntax? receiver = ReceiverOf(callee) is { } reached ? Uncast(reached) : null;
                ITypeSymbol? type = receiver is not null
                    ? model.GetTypeInfo(receiver).Type
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

        private static IEnumerable<ExpressionSyntax> ResourcesOf(UsingStatementSyntax used)
        {
            if (used.Expression is { } expression)
            {
                yield return expression;
            }
            foreach (VariableDeclaratorSyntax variable in used.Declaration?.Variables ?? [])
            {
                if (variable.Initializer?.Value is { } value)
                {
                    yield return value;
                }
            }
        }

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
                                && IsGate(model.GetTypeInfo(access.Expression).Type):
                            yield return IdentityOf(access.Expression, model, 0);
                            break;
                        case MemberBindingExpressionSyntax binding
                            when !handedOn
                                && GateUses.Contains(binding.Name.Identifier.ValueText)
                                && ReceiverOf(binding) is { } receiver
                                && IsGate(model.GetTypeInfo(receiver).Type):
                            yield return IdentityOf(receiver, model, 0);
                            break;
                        case ArgumentSyntax { Expression: { } passed }
                            when handedOn && IsGate(model.GetTypeInfo(passed).Type):
                            yield return IdentityOf(passed, model, 0);
                            break;
                        case AssignmentExpressionSyntax { Right: { } assigned } assignment
                            when handedOn
                                && IsGate(model.GetTypeInfo(assigned).Type)
                                && model.GetSymbolInfo(assignment.Left).Symbol is not ILocalSymbol:
                            yield return IdentityOf(assigned, model, 0);
                            break;
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
}
