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

    /// <summary>
    /// Contract B7 (DoD §H): a shared grid under <c>Canvas/</c> has its
    /// <c>Announce</c> seam SWAPPED onto the canvas relay.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The guard above cannot see this one. The substrate's default seam
    /// posts straight through <c>AccessibilityNotificationDispatcher</c>
    /// — but it does so inside <c>Grids/AccessibleDataGrid.cs</c>, which
    /// is not a canvas source, so a canvas surface that simply FORGOT to
    /// swap the seam would announce outside the canvas funnel with no
    /// canvas file naming the dispatcher at all. That is the whole
    /// bypass this PR's §W-D claim rests on not existing.
    /// </para>
    /// <para>
    /// Structural, and paired with a behavioural fact
    /// (<c>TheGridsOwnAnnouncementsComeOutOfTheCanvasFunnel</c>) that
    /// drives a real sort through the production surface and reads the
    /// funnel's post seam: a guard may not exercise the mechanism it is
    /// guarding, so neither of the two supplies the other's.
    /// </para>
    /// </remarks>
    [Fact]
    public void EveryGridUnderCanvasRidesTheRelay()
    {
        var offenders = new List<string>();
        var grids = 0;
        foreach (string file in CanvasSources())
        {
            string label = Path.GetRelativePath(CanvasSourceRoot(), file);
            CSharpSource source = CSharpSource.Load("Canvas", label);
            bool buildsAGrid = source.Root.DescendantNodes()
                .OfType<ObjectCreationExpressionSyntax>()
                .Any(creation => creation.Type.ToString() == "AccessibleDataGrid");
            if (!buildsAGrid)
            {
                continue;
            }
            grids++;

            // Every assignment to the seam in this file, and what it
            // assigns — the object-initializer form included, since that
            // is an assignment node too. A grid may legitimately be
            // muted before its document arrives (`_ => { }`); what it
            // may never be is left on the substrate's dispatcher-backed
            // default, so at least one assignment has to be the relay.
            string[] seatings = source.Root.DescendantNodes()
                .OfType<AssignmentExpressionSyntax>()
                .Where(assignment => assignment.Left.ToString()
                    .EndsWith("Announce", StringComparison.Ordinal))
                .Select(assignment => assignment.Right.ToString())
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            if (!seatings.Any(right =>
                right.Contains("Announcer.Relay", StringComparison.Ordinal)))
            {
                offenders.Add(
                    $"{label}: builds an AccessibleDataGrid but never assigns its "
                    + "Announce seam to the canvas announcer's Relay "
                    + $"(assignments seen: {(seatings.Length == 0 ? "none" : string.Join(" | ", seatings))})");
            }
        }

        // The guard's own premise: PR B ships exactly one such grid, and
        // a scan that found none would pass over nothing.
        Assert.True(
            grids >= 1,
            "no canvas source builds an AccessibleDataGrid; the seam-swap guard "
            + "would be scanning nothing.");
        Assert.True(
            offenders.Count == 0,
            "a shared grid under Canvas/ must announce through CanvasAnnouncer.Relay "
            + "(contract B7/DoD §H) — the substrate's default seam posts through the "
            + "canonical dispatcher from a file this census does not scan, so a "
            + "forgotten swap is a silent bypass of the canvas funnel:\n"
            + string.Join("\n", offenders));
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

    /// <summary>
    /// Contract C4: the snapshot-visibility predicate is DERIVED from
    /// what the surface renders, not from a state's name — so changing
    /// which states show rows forces the predicate to follow.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The mac twin (<c>the_snapshot_visibility_predicate_matches_the_container_switch</c>)
    /// exists because writing the condition out by hand made the gate
    /// miss a state whose retained rows the container DOES render — the
    /// curated-condition class. The Windows surface's answer is one
    /// expression in <c>Render</c>, so this parses THAT and requires the
    /// predicate to say the same thing.
    /// </para>
    /// <para>
    /// The VIEW is the authority: a future state that renders a
    /// projection has to appear on both sides or this fails naming the
    /// disagreement.
    /// </para>
    /// </remarks>
    [Fact]
    public void TheSnapshotVisibilityPredicateMatchesTheSurfaceRender()
    {
        MethodDeclarationSyntax render =
            CSharpSource.Load("Canvas", "CanvasSurfaceView.cs").Method("Render");
        string[] readyDeclarations = render.DescendantNodes()
            .OfType<VariableDeclaratorSyntax>()
            .Where(declarator => declarator.Identifier.ValueText == "ready")
            .Select(declarator => CSharpSource.Normalize(declarator.Initializer!.Value))
            .ToArray();
        string surfaceCondition = Assert.Single(readyDeclarations);

        PropertyDeclarationSyntax predicate = CSharpSource
            .Load("Canvas", "CanvasDocumentViewModel.cs")
            .Root.DescendantNodes()
            .OfType<PropertyDeclarationSyntax>()
            .Single(property =>
                property.Identifier.ValueText == "RendersRetainedSnapshot");
        string predicateCondition =
            CSharpSource.Normalize(predicate.ExpressionBody!.Expression);

        // The surface says `model.State == …`; the document says
        // `State == …`. Compare the STATE TEST, which is the part that
        // can drift.
        Assert.Equal(
            surfaceCondition.Replace("model.", string.Empty, StringComparison.Ordinal),
            predicateCondition);
        Assert.Contains(
            "CanvasLoadState.Ready",
            predicateCondition,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Contract C10/A17: the filter's whole-model query runs OFF the
    /// dispatcher.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It used to run inside the <c>Filter</c> getter, on the UI thread,
    /// taking the lock a LOAD body holds across <c>open_canvas</c> plus
    /// three whole-model projections — so a keystroke during a load
    /// stalled the dispatcher for the length of the load. The fix is the
    /// scheduler A17 already mandates for every other whole-model read
    /// in that class.
    /// </para>
    /// <para>
    /// Guarded in the SOURCE because the alternative cannot see it: a
    /// behavioural fact runs in synchronous test mode, where a scheduled
    /// body and an inline one are indistinguishable by construction — so
    /// the thing that regressed would be invisible to exactly the tests
    /// that would be written for it. The claim is structural, so the
    /// guard is too.
    /// </para>
    /// </remarks>
    [Fact]
    public void TheFilterQueryIsScheduledOffTheDispatcher()
    {
        // Scanned over the WHOLE canvas directory, not one file (m8): a
        // `canvas_filter` call added anywhere else under `Canvas/` would
        // have slipped past a one-file guard silently, and the guard's
        // whole claim is that there is exactly ONE caller.
        var offenders = new List<string>();
        foreach (string file in CanvasSources())
        {
            string label = Path.GetRelativePath(CanvasSourceRoot(), file);
            if (label == "CanvasDocumentViewModel.cs")
            {
                continue;
            }
            if (CSharpSource.Load("Canvas", label).Root
                .DescendantNodes()
                .OfType<MemberAccessExpressionSyntax>()
                .Any(access => access.Name.Identifier.ValueText == "CanvasFilter"))
            {
                offenders.Add(label);
            }
        }
        Assert.True(
            offenders.Count == 0,
            "`canvas_filter` is called outside CanvasDocumentViewModel.cs, so the "
            + "one-caller guard below is scanning the wrong file and the query is "
            + "somewhere it was never scheduled: " + string.Join(", ", offenders));

        CSharpSource source = CSharpSource.Load("Canvas", "CanvasDocumentViewModel.cs");

        // Walked UP from each call site to its enclosing MEMBER, not down
        // from the methods: the bug this guards lived in a PROPERTY
        // GETTER, and a scan that enumerated `MethodDeclarationSyntax`
        // could not see one — it would have reported the single scheduler
        // body and passed while a "fast path" in the getter put the query
        // straight back on the dispatcher. `MemberDeclarationSyntax`
        // covers properties, constructors and field initialisers too, so
        // "one caller" means one caller.
        string[] callers = source.Root
            .DescendantNodes()
            .OfType<MemberAccessExpressionSyntax>()
            .Where(access => access.Name.Identifier.ValueText == "CanvasFilter")
            .Select(access => access.FirstAncestorOrSelf<MemberDeclarationSyntax>())
            .Select(NameOfMember)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        // Compared as a SET with the members in the message: a bare
        // `Assert.Single` reports "the collection contained 2 items" and
        // leaves the reader to go find which member re-introduced the
        // call, which is exactly the thing a guard is for.
        Assert.True(
            callers.Length == 1 && callers[0] == "FilterBody",
            "`canvas_filter` must be called from exactly one member, the scheduler "
            + "body — it is a whole-model read and belongs off the dispatcher "
            + "(contract C10/A17), not wherever a property getter happens to run. "
            + $"Callers found: {string.Join(", ", callers)}.");

        // …and that body is reached only through the scheduler. A method
        // named FilterBody that somebody also calls directly would put
        // the query straight back on the dispatcher.
        InvocationExpressionSyntax[] invocations = source.Root
            .DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(invocation => invocation.Expression is IdentifierNameSyntax
            {
                Identifier.ValueText: "FilterBody",
            })
            .ToArray();

        InvocationExpressionSyntax scheduled = Assert.Single(invocations);
        Assert.True(
            scheduled.Ancestors().OfType<InvocationExpressionSyntax>().Any(outer =>
                outer.Expression is IdentifierNameSyntax { Identifier.ValueText: "StartWork" }),
            "FilterBody is invoked outside a StartWork(...) argument, so the filter "
            + "query is back on the dispatcher holding the FFI lock.");
    }

    /// <summary>
    /// The enclosing member's name, whatever KIND of member it is — a
    /// call in a property getter must report the property, or the scan
    /// above would name nothing and read as "no caller".
    /// </summary>
    private static string NameOfMember(MemberDeclarationSyntax? member) => member switch
    {
        MethodDeclarationSyntax method => method.Identifier.ValueText,
        PropertyDeclarationSyntax property => property.Identifier.ValueText,
        ConstructorDeclarationSyntax constructor => constructor.Identifier.ValueText,
        IndexerDeclarationSyntax => "this[]",
        FieldDeclarationSyntax field =>
            field.Declaration.Variables[0].Identifier.ValueText,
        null => "(no enclosing member)",
        _ => member.Kind().ToString(),
    };

    /// <summary>
    /// Contract C8: M4's one keep-alive arm depends on the shell
    /// answering "is an overlay open", and the answer is a static seam
    /// with a safe default. A default left installed would silently turn
    /// every palette open into a mode cancellation — which is the exact
    /// behaviour the arm exists to prevent — and nothing in-process can
    /// see it, because <c>MainWindow</c> is not reachable from a unit
    /// fact.
    /// </summary>
    [Fact]
    public void TheShellInstallsTheModalOverlayAnswerForModeCancellation()
    {
        ConstructorDeclarationSyntax constructor = CSharpSource
            .Load("MainWindow.xaml.cs")
            .Root.DescendantNodes()
            .OfType<ConstructorDeclarationSyntax>()
            .Single(declaration => declaration.Identifier.ValueText == "MainWindow");

        AssignmentExpressionSyntax[] installs = constructor.DescendantNodes()
            .OfType<AssignmentExpressionSyntax>()
            .Where(assignment => CSharpSource.Normalize(assignment.Left)
                .EndsWith("CanvasSurfaceView.ShellOverlayIsOpen", StringComparison.Ordinal))
            .ToArray();

        AssignmentExpressionSyntax install = Assert.Single(installs);
        Assert.Contains(
            "OpenModalSurface",
            CSharpSource.Normalize(install.Right),
            StringComparison.Ordinal);
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
