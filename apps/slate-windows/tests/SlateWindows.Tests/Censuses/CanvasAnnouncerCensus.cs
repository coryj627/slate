// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// W6-1 PR A (#745): the Windows twin of mac's DoD §H funnel guard
// (`CanvasAnnouncerTests.testNoDirectAnnouncementsUnderCanvas`), plus
// the attach-funnel doc-comment twin contract A2 requires.
//
// Windows has no residue census of its own — `A11yResidueCensusTests`
// has no twin here — so this is the first, and it is scoped to
// `Canvas/` deliberately rather than pretending to be the general one.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SlateWindows.Tests.Censuses;

[Trait("census", "canvas-funnel")]
public sealed class CanvasAnnouncerCensus
{
    private static string CanvasSourceRoot() =>
        Path.Combine(SourceText.ShellSourceRoot(), "Canvas");

    /// <summary>The one file allowed to reach the dispatcher and the
    /// renderer: it IS the funnel.</summary>
    private const string TheRelay = "CanvasAnnouncer.cs";

    /// <summary>
    /// Contract A6/R-C: no canvas source announces on its own.
    ///
    /// Syntax, not <c>Contains</c> (the #1108 rationale <c>CSharpSource</c>
    /// exists for): a comment naming a bypass is trivia and cannot trip
    /// the guard, and a string literal spelling one is a literal node,
    /// not a call. The walk is RECURSIVE — <c>Canvas/CanvasPickers/</c>
    /// arrives in PR E, and mac's round-1 m-F was exactly a
    /// one-level walk.
    /// </summary>
    [Fact]
    public void NoCanvasSourceAnnouncesOutsideTheRelay()
    {
        var offenders = new List<string>();
        foreach (string file in CanvasSources())
        {
            if (Path.GetFileName(file) == TheRelay)
            {
                continue;
            }
            string label = Path.GetRelativePath(CanvasSourceRoot(), file);
            CSharpSource source = CSharpSource.Load("Canvas", label);

            foreach (string name in source.Root.DescendantNodes()
                .OfType<IdentifierNameSyntax>()
                .Select(identifier => identifier.Identifier.ValueText))
            {
                if (name is "AccessibilityNotificationDispatcher")
                {
                    offenders.Add($"{label}: {name}");
                }
            }

            foreach (MemberAccessExpressionSyntax access in source.Root
                .DescendantNodes()
                .OfType<MemberAccessExpressionSyntax>())
            {
                string member = access.Name.Identifier.ValueText;
                if (member is "RaiseNotificationEvent" or "A11yRender")
                {
                    offenders.Add($"{label}: {member}");
                }
            }

            foreach (ObjectCreationExpressionSyntax creation in source.Root
                .DescendantNodes()
                .OfType<ObjectCreationExpressionSyntax>())
            {
                if (creation.Type.ToString().EndsWith("HostComposed", StringComparison.Ordinal))
                {
                    offenders.Add($"{label}: A11yEvent.HostComposed");
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "canvas code must announce through CanvasAnnouncer (DoD §H, "
            + "contract A5/A6), never directly:\n" + string.Join("\n", offenders));
    }

    /// <summary>The relay is the only file that needs the exemption, so
    /// the exemption must actually be load-bearing: if
    /// <c>CanvasAnnouncer.cs</c> ever stopped rendering, this guard
    /// would be scanning for a symbol nothing uses and would pass for
    /// the wrong reason.</summary>
    [Fact]
    public void TheRelayIsTheOneFileThatRenders()
    {
        CSharpSource relay = CSharpSource.Load("Canvas", TheRelay);
        Assert.Contains(
            relay.Root.DescendantNodes().OfType<MemberAccessExpressionSyntax>(),
            access => access.Name.Identifier.ValueText == "A11yRender");
    }

    /// <summary>
    /// Contract A2: the attach funnel's doc comment enumerates its call
    /// sites, and the enumeration is DERIVED, not trusted. It listed
    /// four of five from W4-6 until this PR, which is the same failure
    /// the "two sweeps missed sites" lesson names, in slower motion.
    /// </summary>
    [Fact]
    public void TheAttachFunnelDocCommentNamesEveryCallSite()
    {
        const string funnel = "AttachTabDocumentsIfNeeded";

        // The file set is DERIVED, not listed: a call site moving into a
        // new partial (say the canvas one) would have escaped a
        // hardcoded list while the count floor still passed.
        string[] partials = Directory
            .EnumerateFiles(
                SourceText.ShellSourceRoot(),
                "WorkspaceViewModel*.cs",
                SearchOption.AllDirectories)
            .OrderBy(file => file, StringComparer.Ordinal)
            .ToArray();
        Assert.True(
            partials.Length >= 5,
            $"only {partials.Length} WorkspaceViewModel partials were found; the "
            + "scrape would be reading almost nothing.");

        var callSites = new HashSet<string>(StringComparer.Ordinal);
        foreach (string partial in partials)
        {
            CSharpSource source = CSharpSource.Load(
                Path.GetRelativePath(SourceText.ShellSourceRoot(), partial)
                    .Split(Path.DirectorySeparatorChar));
            foreach (InvocationExpressionSyntax invocation in source.Root
                .DescendantNodes()
                .OfType<InvocationExpressionSyntax>())
            {
                if (invocation.Expression is not IdentifierNameSyntax identifier
                    || identifier.Identifier.ValueText != funnel)
                {
                    continue;
                }
                MethodDeclarationSyntax? owner = invocation.Ancestors()
                    .OfType<MethodDeclarationSyntax>()
                    .FirstOrDefault();
                Assert.NotNull(owner);
                _ = callSites.Add(owner.Identifier.ValueText);
            }
        }

        // The guard's own premise: a rename that made the scrape find
        // nothing would otherwise pass with two empty sets.
        Assert.True(
            callSites.Count >= 5,
            $"only {callSites.Count} call sites of {funnel} were found; the W4-6 "
            + "lesson is that sweeps miss sites, and a scrape finding fewer than "
            + "the recorded five is either a real removal or a broken query.");

        // The comment's own list is SCRAPED, not probed against a fixed
        // set of names — a stale comment naming a sixth, nonexistent
        // site used to pass, because nothing ever read what it said.
        MethodDeclarationSyntax declaration =
            CSharpSource.Load("WorkspaceViewModel.Bases.cs").Method(funnel);
        string docComment = declaration.GetLeadingTrivia().ToFullString();
        // Only the <summary>: that is where the enumeration lives, and
        // the <remarks> paragraph is prose about the history and the
        // guard, which cites names that are not call sites.
        System.Text.RegularExpressions.Match summary =
            System.Text.RegularExpressions.Regex.Match(
                docComment,
                @"<summary>(.*?)</summary>",
                System.Text.RegularExpressions.RegexOptions.Singleline);
        Assert.True(
            summary.Success,
            $"{funnel} has no <summary> for the call-site list to live in.");
        var named = new HashSet<string>(
            System.Text.RegularExpressions.Regex
                .Matches(summary.Groups[1].Value, @"<c>([A-Za-z_][A-Za-z0-9_]*)</c>")
                .Select(match => match.Groups[1].Value),
            StringComparer.Ordinal);

        Assert.True(
            named.SetEquals(callSites),
            $"{funnel}'s doc comment and its real call sites disagree. "
            + $"Named but not a call site: {Join(named.Except(callSites))}. "
            + $"A call site but not named: {Join(callSites.Except(named))}.");
    }

    private static string Join(IEnumerable<string> names)
    {
        string joined = string.Join(", ", names.OrderBy(n => n, StringComparer.Ordinal));
        return joined.Length == 0 ? "(none)" : joined;
    }

    /// <summary>
    /// Contract A14's one-authority rule, asserted in the source
    /// because no in-process fact can reach it: MainWindow's
    /// <c>FocusEditorPane</c> must return early for a canvas tab.
    /// </summary>
    /// <remarks>
    /// It is queued at Input priority, i.e. it runs AFTER the canvas
    /// surface has placed focus on a realized row, and its fallbacks
    /// (the TabItem, then the TabControl) would take focus straight back
    /// — deterministically, every time. The canvas surface has a landing
    /// place for every one of its states, so standing aside never leaves
    /// focus nowhere.
    /// </remarks>
    [Fact]
    public void TheEditorPaneFocusFallbackStandsAsideForACanvasTab()
    {
        MethodDeclarationSyntax method =
            CSharpSource.Load("MainWindow.xaml.cs").Method("FocusEditorPane");
        IfStatementSyntax? guard = method.Body?.Statements
            .OfType<IfStatementSyntax>()
            .FirstOrDefault(statement =>
                statement.Condition.ToString().Contains("IsCanvas", StringComparison.Ordinal));

        Assert.True(
            guard is not null,
            "FocusEditorPane must short-circuit for a canvas tab (contract A14); "
            + "without it the pane fallback takes focus back off the outline row "
            + "the canvas surface just realized, every time.");
        Assert.True(
            guard!.Statement.DescendantNodesAndSelf().OfType<ReturnStatementSyntax>().Any(),
            "the canvas arm of FocusEditorPane must RETURN — falling through would "
            + "reach the TabItem fallback anyway.");
        // TWO-SIDED (Major-2): a BARE return stands aside AND strands the
        // seven dismissal routes that reach FocusEditorPane as their
        // last resort. The canvas arm must also RAISE the focus request,
        // so those routes land on the outline row. MainWindow is not
        // reachable in-process, so this is asserted in the source; the
        // one-sided version (return only) went green while the raise was
        // missing, which is the supplies-its-own-mechanism class a third
        // time.
        Assert.True(
            guard.Statement.DescendantNodesAndSelf()
                .OfType<InvocationExpressionSyntax>()
                .Any(invocation => invocation.Expression
                    is MemberAccessExpressionSyntax
                {
                    Name.Identifier.ValueText: "RequestFocusLanding",
                }),
            "the canvas arm of FocusEditorPane must RAISE a focus request "
            + "(`canvas.RequestFocusLanding(activeTab)`), or the palette/search/"
            + "properties/template dismissal routes strand focus on the window "
            + "root — the very thing their own fallback comment exists to prevent.");
        // And it has to come before the fallbacks it is protecting the
        // canvas from, not after them.
        Assert.True(
            method.Body!.Statements.IndexOf(guard) <= 1,
            "the canvas short-circuit must run before FocusEditorPane's own "
            + "focus attempts, or it protects nothing.");
    }

    private static IEnumerable<string> CanvasSources()
    {
        string root = CanvasSourceRoot();
        Assert.True(Directory.Exists(root), $"the canvas source root is missing: {root}");
        string[] files = Directory
            .EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .OrderBy(file => file, StringComparer.Ordinal)
            .ToArray();
        Assert.NotEmpty(files);
        return files;
    }
}
