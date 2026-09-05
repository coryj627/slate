// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// W6-2 PR B, slice B1 (#746), contract B-19 (iii)–(v): the leaf's load and
// speech entry points have exactly the callers rule C names; every writer
// of the pane's visibility reaches the pending mount's consume; the
// constructor seeds the initial mount; the graph document enqueues its
// filter count through the relay's GATED entry and nothing enqueues one
// ungated; `SyncPanels` is the only recorder of the leaf's root and the
// mutation boundary its only reconciler; and the depth's DATAFLOW — every
// write to the storage is the FFI clamp's result, and only the named
// producers reach the clamp's argument.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SlateWindows.Tests.Censuses;

[Trait("census", "connections-leaf")]
public sealed class ConnectionsLeafCensus
{
    private static string ShellRoot => SourceText.ShellSourceRoot();

    private static IEnumerable<(string Relative, CSharpSource Source)> ShellSources()
    {
        foreach (string file in Directory.EnumerateFiles(ShellRoot, "*.cs", SearchOption.AllDirectories)
            .OrderBy(f => f, StringComparer.Ordinal))
        {
            string relative = Path.GetRelativePath(ShellRoot, file).Replace('\\', '/');
            if (relative.StartsWith("obj/", StringComparison.Ordinal) || relative.Contains("/obj/", StringComparison.Ordinal))
            {
                continue;
            }
            yield return (relative, CSharpSource.LoadPath(file));
        }
    }

    private static string OwnerOf(SyntaxNode node)
    {
        foreach (SyntaxNode ancestor in node.Ancestors())
        {
            switch (ancestor)
            {
                case MethodDeclarationSyntax method:
                    return method.Identifier.ValueText;
                case ConstructorDeclarationSyntax:
                    return "<ctor>";
                case PropertyDeclarationSyntax property:
                    return property.Identifier.ValueText;
            }
        }
        return "<top level>";
    }

    /// <summary>Invocations of a member on the leaf — <c>Connections.X(</c>
    /// in the shell, or <c>X(</c> / <c>this.X(</c> inside the leaf itself.</summary>
    private static IEnumerable<(string Relative, string Owner, string Member)> LeafCalls(string member)
    {
        foreach ((string relative, CSharpSource source) in ShellSources())
        {
            foreach (InvocationExpressionSyntax call in source.Root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (call.Expression is MemberAccessExpressionSyntax access
                    && access.Name.Identifier.ValueText == member
                    && (CSharpSource.Normalize(access.Expression).EndsWith("Connections", StringComparison.Ordinal)
                        || CSharpSource.Normalize(access.Expression).EndsWith("leaf", StringComparison.Ordinal)))
                {
                    yield return (relative, OwnerOf(call), member);
                }
            }
        }
    }

    /// <summary>B-19 (iii): the trigger entry points and their one owner each.</summary>
    [Fact]
    public void EveryTriggerEntryPointHasExactlyTheCallersRuleCNames()
    {
        (string Member, string[] Owners)[] expected =
        [
            ("Mounted", ["WorkspaceViewModel.Connections.cs:ConsumePendingMount"]),
            ("Shown", ["WorkspaceViewModel.Connections.cs:OnLeafShown"]),
            ("Show", ["WorkspaceViewModel.Connections.cs:ShowConnections"]),
            ("NoteChanged", ["WorkspaceViewModel.Connections.cs:ReconcileConnectionsRootTo"]),
            ("Probe", ["WorkspaceViewModel.Connections.cs:ProbeConnections"]),
            ("Deeper", ["WorkspaceViewModel.Connections.cs:ConnectionsDeeperCommand"]),
            ("Shallower", ["WorkspaceViewModel.Connections.cs:ConnectionsShallowerCommand"]),
            ("ViewCollapsed", ["WorkspaceViewModel.Connections.cs:OnRightPaneVisibilityChanged"]),
            ("AnnounceStatus", ["WorkspaceViewModel.Connections.cs:ShowConnections"]),
        ];
        var failures = new List<string>();
        foreach ((string member, string[] owners) in expected)
        {
            string[] found = [.. LeafCalls(member).Select(c => $"{c.Relative}:{c.Owner}").OrderBy(s => s, StringComparer.Ordinal)];
            if (!found.SequenceEqual(owners.OrderBy(s => s, StringComparer.Ordinal)))
            {
                failures.Add($"{member}: callers [{string.Join(", ", found)}], expected [{string.Join(", ", owners)}]");
            }
        }
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    /// <summary>B-19 (iii): the root's writers — <c>SyncPanels</c> records,
    /// the boundary reconciles, nothing else reaches the leaf's root.</summary>
    [Fact]
    public void SyncPanelsIsTheOnlyRecorderAndTheBoundaryTheOnlyReconciler()
    {
        var recorders = new List<string>();
        var reconcilers = new List<string>();
        foreach ((string relative, CSharpSource source) in ShellSources())
        {
            foreach (InvocationExpressionSyntax call in source.Root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                string callee = call.Expression is MemberAccessExpressionSyntax access
                    ? access.Name.Identifier.ValueText
                    : call.Expression is IdentifierNameSyntax bare ? bare.Identifier.ValueText : string.Empty;
                if (callee == "SyncConnectionsRoot")
                {
                    recorders.Add($"{relative}:{OwnerOf(call)}");
                }
                if (callee == "ReconcileConnectionsRoot")
                {
                    reconcilers.Add($"{relative}:{OwnerOf(call)}");
                }
            }
        }
        Assert.Equal(["WorkspaceViewModel.cs:SyncPanels"], recorders);
        Assert.Equal(["WorkspaceViewModel.Persistence.cs:RunWorkspaceMutation"], reconcilers);
    }

    /// <summary>B-19 (iii): every writer of <c>IsRightPaneVisible = true</c>
    /// in the shell reaches the pending mount's consume at its route's end
    /// — the consume call in the same method, or the outermost mutation
    /// boundary (a <c>RunWorkspaceMutation</c> ancestor) — and the
    /// constructor seeds the initial mount.</summary>
    [Fact]
    public void EveryPaneRevealConsumesThePendingMountAndTheConstructorSeedsIt()
    {
        var offenders = new List<string>();
        int writers = 0;
        bool seeded = false;
        foreach ((string relative, CSharpSource source) in ShellSources())
        {
            foreach (AssignmentExpressionSyntax assignment in source.Root.DescendantNodes().OfType<AssignmentExpressionSyntax>())
            {
                string target = CSharpSource.Normalize(assignment.Left);
                if (!target.EndsWith("IsRightPaneVisible", StringComparison.Ordinal))
                {
                    continue;
                }
                string value = CSharpSource.Normalize(assignment.Right);
                if (value == "false")
                {
                    continue;
                }
                writers++;
                SyntaxNode? scope = assignment.Ancestors().FirstOrDefault(a => a is MethodDeclarationSyntax or ConstructorDeclarationSyntax or PropertyDeclarationSyntax or AccessorDeclarationSyntax);
                bool consumes = scope is not null && scope.DescendantNodes().OfType<InvocationExpressionSyntax>()
                    .Any(call => CSharpSource.Normalize(call.Expression).EndsWith("ConsumePendingMount", StringComparison.Ordinal));
                bool insideMutation = assignment.Ancestors().OfType<InvocationExpressionSyntax>()
                    .Any(call => CSharpSource.Normalize(call.Expression).EndsWith("RunWorkspaceMutation", StringComparison.Ordinal));
                // The setter itself is the arm, not a route.
                bool isTheSetter = scope is AccessorDeclarationSyntax && relative == "WorkspaceViewModel.cs";
                if (!consumes && !insideMutation && !isTheSetter)
                {
                    offenders.Add($"{relative}:{OwnerOf(assignment)} — {target} = {value}");
                }
            }
            foreach (InvocationExpressionSyntax call in source.Root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (CSharpSource.Normalize(call.Expression).EndsWith("SeedInitialConnectionsMount", StringComparison.Ordinal)
                    && relative == "WorkspaceViewModel.cs"
                    && OwnerOf(call) == "<ctor>")
                {
                    seeded = true;
                }
            }
        }
        Assert.True(writers >= 8, $"the shell writes IsRightPaneVisible at {writers} sites; the census expected the known eight or more");
        Assert.True(offenders.Count == 0, "a pane reveal reaches no consume:\n" + string.Join("\n", offenders));
        Assert.True(seeded, "the constructor does not seed the initial mount (Term 3(a), IGH-7)");
    }

    /// <summary>Term 10 / B-19 (iii): the graph document enqueues its filter
    /// count through the relay's GATED entry, with the effective predicate,
    /// and no graph source enqueues a <c>GraphFilterCount</c> ungated.</summary>
    [Fact]
    public void TheFilterCountIsEnqueuedThroughTheGatedEntryAndNowhereUngated()
    {
        var gated = new List<string>();
        var ungated = new List<string>();
        foreach ((string relative, CSharpSource source) in ShellSources())
        {
            if (!relative.StartsWith("Graph/", StringComparison.Ordinal) || relative == "Graph/GraphAnnouncer.cs")
            {
                continue;
            }
            foreach (InvocationExpressionSyntax call in source.Root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                string callee = CSharpSource.Normalize(call.Expression);
                bool carriesCount = call.ArgumentList.Arguments.Any(a => a.Expression.ToString().Contains("GraphFilterCount", StringComparison.Ordinal));
                if (callee.EndsWith("AnnounceGatedFilterCount", StringComparison.Ordinal))
                {
                    gated.Add($"{relative}:{OwnerOf(call)}");
                }
                else if (carriesCount && (callee.EndsWith("Announce", StringComparison.Ordinal) || callee.EndsWith("AnnounceIfEffective", StringComparison.Ordinal)))
                {
                    ungated.Add($"{relative}:{OwnerOf(call)}");
                }
            }
        }
        Assert.Equal(["Graph/GraphDocumentViewModel.cs:AnnounceFilterCountIfEffective"], gated);
        Assert.Empty(ungated);
    }

    /// <summary>B-13: the leaf body is gated on the leaf id, hosts the view
    /// bound to the workspace's public document, and the generic placeholder
    /// collapses for the leaf beside the leaves it names.</summary>
    [Fact]
    public void TheLeafBodyIsGatedOnItsIdAndThePlaceholderRetiresForIt()
    {
        string xaml = File.ReadAllText(Path.Combine(ShellRoot, "MainWindow.xaml"));
        int body = xaml.IndexOf("AutomationProperties.AutomationId=\"ConnectionsLeafBody\"", StringComparison.Ordinal);
        Assert.True(body >= 0, "the leaf body is not in MainWindow.xaml");
        string bodyBlock = xaml.Substring(body, Math.Min(2000, xaml.Length - body));
        Assert.Contains("<DataTrigger Binding=\"{Binding ActiveLeaf.Id}\" Value=\"connections\">", bodyBlock, StringComparison.Ordinal);
        Assert.Contains("<graph:ConnectionsLeafView", bodyBlock, StringComparison.Ordinal);
        Assert.Contains("Model=\"{Binding Connections}\"", bodyBlock, StringComparison.Ordinal);
        // The placeholder's style: a collapse trigger for the leaf id.
        int placeholder = xaml.IndexOf("This panel is docked and ready for its feature surface.", StringComparison.Ordinal);
        Assert.True(placeholder >= 0, "the generic placeholder is not in MainWindow.xaml");
        string placeholderStyle = xaml.Substring(Math.Max(0, placeholder - 4000), Math.Min(4000, placeholder));
        int trigger = placeholderStyle.LastIndexOf("Value=\"connections\"", StringComparison.Ordinal);
        Assert.True(trigger >= 0, "the placeholder does not collapse for the leaf (B-13)");
        Assert.Contains("<Setter Property=\"Visibility\" Value=\"Collapsed\" />", placeholderStyle.Substring(trigger), StringComparison.Ordinal);
        // The right pane's landmark is the named focus boundary (Term 9).
        Assert.Contains("x:Name=\"RightPaneBorder\"", xaml, StringComparison.Ordinal);
        string window = File.ReadAllText(Path.Combine(ShellRoot, "MainWindow.xaml.cs"));
        Assert.Contains("RightPaneBorder.IsKeyboardFocusWithin", window, StringComparison.Ordinal);
        Assert.Contains("ConnectionsLeafSurface.FocusAnchor()", window, StringComparison.Ordinal);
    }

    /// <summary>B-20: the parity matrix carries the three command rows at
    /// the W6-2 status the generator names, and the W-C matrix carries the
    /// leaf's row (its cells the evidence census's).</summary>
    [Fact]
    public void TheParityMatrixCarriesTheThreeRowsAtTheW62Status()
    {
        string repo = SourceText.RepoRoot();
        string script = File.ReadAllText(Path.Combine(repo, "scripts", "generate-parity-matrix.py"));
        var status = System.Text.RegularExpressions.Regex.Match(
            script, "W6_2_STATUS = \\(\\s*\"([^\"]+)\"\\s*\"([^\"]+)\"\\s*\\)");
        Assert.True(status.Success, "the generator's W6_2_STATUS is not in its two-string shape");
        string expected = status.Groups[1].Value + status.Groups[2].Value;
        string matrix = File.ReadAllText(Path.Combine(repo, "docs", "plans", "18_windows_port", "parity_matrix.md"));
        foreach (string id in new[] { "slate.graph.showConnections", "slate.graph.connectionsDeeper", "slate.graph.connectionsShallower" })
        {
            string? row = matrix.Split('\n').FirstOrDefault(line => line.StartsWith("| `" + id + "`", StringComparison.Ordinal));
            Assert.True(row is not null, $"{id} has no parity row");
            Assert.EndsWith("| " + expected + " |", row.TrimEnd(), StringComparison.Ordinal);
        }
        string wc = File.ReadAllText(Path.Combine(repo, "docs", "plans", "18_windows_port", "w_c_matrix.md"));
        Assert.Contains("| Graph connections leaf (W6-2 PR B) |", wc, StringComparison.Ordinal);
    }

    /// <summary>B-19 (v): the depth's dataflow, across the whole shell —
    /// every write to the leaf's depth storage takes the FFI clamp's result
    /// (through the counting wrapper whose body IS the FFI call), and every
    /// value reaching the clamp is one of the named producers.</summary>
    [Fact]
    public void EveryDepthWriteIsTheFfiClampsResultAndOnlyTheNamedProducersReachIt()
    {
        CSharpSource leaf = CSharpSource.Load("Graph", "ConnectionsLeafViewModel.cs");
        // (1) The wrapper's body is the FFI clamp of its own parameter.
        MethodDeclarationSyntax wrapper = leaf.Root.DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Single(m => m.Identifier.ValueText == "ClampDepth");
        string wrapperReturn = CSharpSource.Normalize(
            wrapper.DescendantNodes().OfType<ReturnStatementSyntax>().Single().Expression!);
        Assert.Equal("SlateUniffiMethods.GraphClampConnectionsDepth(requested)", wrapperReturn);

        // (2) Every assignment to `_depth` takes `ClampDepth(...)`'s result —
        // directly or through a local initialised by it in the same method.
        foreach (AssignmentExpressionSyntax assignment in leaf.Root.DescendantNodes().OfType<AssignmentExpressionSyntax>()
            .Where(a => CSharpSource.Normalize(a.Left) == "_depth"))
        {
            string right = CSharpSource.Normalize(assignment.Right);
            bool direct = right.StartsWith("ClampDepth(", StringComparison.Ordinal);
            bool viaLocal = assignment.Ancestors().OfType<MethodDeclarationSyntax>().First()
                .DescendantNodes().OfType<VariableDeclaratorSyntax>()
                .Any(v => v.Identifier.ValueText == right
                    && v.Initializer is not null
                    && CSharpSource.Normalize(v.Initializer.Value).StartsWith("ClampDepth(", StringComparison.Ordinal));
            Assert.True(direct || viaLocal, $"_depth is written from `{right}`, not the clamp's result");
        }

        // (3) The clamp's only caller is SetDepth, with its own parameter.
        var clampCalls = leaf.Root.DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Where(c => CSharpSource.Normalize(c.Expression) == "ClampDepth")
            .Select(c => (Owner: OwnerOf(c), Argument: CSharpSource.Normalize(c.ArgumentList.Arguments.Single().Expression)))
            .ToList();
        Assert.Equal([("SetDepth", "requested")], clampCalls);

        // (4) SetDepth's callers, across the shell, pass exactly the named
        // producers: core's minimum in the constructor, `_depth + 1` in
        // Deeper, `_depth - 1` in Shallower, and the view's index
        // conversion; nothing else, and no comparison, conditional,
        // `Math.*` or helper on the way.
        var producers = new List<string>();
        foreach ((string relative, CSharpSource source) in ShellSources())
        {
            foreach (InvocationExpressionSyntax call in source.Root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                string callee = CSharpSource.Normalize(call.Expression);
                if (!(callee == "SetDepth" || callee.EndsWith(".SetDepth", StringComparison.Ordinal)))
                {
                    continue;
                }
                string argument = CSharpSource.Normalize(call.ArgumentList.Arguments.Single().Expression);
                producers.Add($"{relative}:{OwnerOf(call)}({argument})");
                Assert.False(
                    argument.Contains("Math.", StringComparison.Ordinal) || argument.Contains('?') || argument.Contains('<') || argument.Contains('>'),
                    $"a host clamp or comparison reaches the depth: {argument}");
            }
        }
        string[] allowed =
        [
            "Graph/ConnectionsLeafViewModel.cs:<ctor>(GraphCoreConstants.Once.ConnectionsDepthMin)",
            // Normalised text carries no spaces.
            "Graph/ConnectionsLeafViewModel.cs:Deeper(_depth+1)",
            "Graph/ConnectionsLeafViewModel.cs:Shallower(_depth-1)",
        ];
        string[] unexpected = [.. producers.Where(p => !allowed.Contains(p) && !p.StartsWith("Graph/ConnectionsLeafView.cs:", StringComparison.Ordinal))];
        Assert.True(unexpected.Length == 0, "an unnamed producer reaches the depth: " + string.Join("; ", unexpected));
        foreach (string name in allowed)
        {
            Assert.Contains(name, producers);
        }
        // No `Math.Clamp`/`Min`/`Max` on a depth in the graph sources or the
        // leaf's partial (the reading view's LIST depth is another word).
        foreach ((string relative, CSharpSource source) in ShellSources())
        {
            if (!relative.StartsWith("Graph/", StringComparison.Ordinal) && relative != "WorkspaceViewModel.Connections.cs")
            {
                continue;
            }
            foreach (InvocationExpressionSyntax call in source.Root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                string callee = CSharpSource.Normalize(call.Expression);
                if (callee is "Math.Clamp" or "Math.Min" or "Math.Max"
                    && call.ArgumentList.ToString().Contains("Depth", StringComparison.OrdinalIgnoreCase))
                {
                    Assert.Fail($"{relative}:{OwnerOf(call)} clamps a depth host-side: {call}");
                }
            }
        }
    }
}
