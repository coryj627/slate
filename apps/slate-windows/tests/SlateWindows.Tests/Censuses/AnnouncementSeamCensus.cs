// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// W6-1 PR A (#745): the production announcement wiring, asserted as a
// CHAIN.
//
// Two seams reach a canvas announcement — the event seam every other
// surface uses, and the rendered-pair seam the canvas coalescer needs
// (contract A5) — and every hop between MainWindow and CanvasAnnouncer
// defaults to a no-op when its argument is absent. That default is
// correct for headless facts and catastrophic in production: PR A
// shipped its first cut with `announceRendered` missing from
// MainWindow's construction, so every canvas announcement fell into
// `?? (_ => { })` and died silently, while every unit fact stayed green
// because each one injects its own sink.
//
// A test-injected sink can never catch that. This census reads the
// SHIPPING call expressions instead.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SlateWindows.Tests.Censuses;

[Trait("census", "announcement-seams")]
public sealed class AnnouncementSeamCensus
{
    /// <summary>
    /// Hop 1 — <c>MainWindow</c> hands the dispatcher to the vault
    /// lifecycle on BOTH seams. Mutation-verified: deleting the
    /// <c>announceRendered:</c> argument fails here naming it.
    /// </summary>
    [Fact]
    public void MainWindowThreadsBothAnnouncementSeamsFromTheDispatcher()
    {
        CSharpSource source = CSharpSource.Load("MainWindow.xaml.cs");
        InvocationOrCreationArguments construction = ConstructionOf(
            source, "VaultLifecycleViewModel");

        foreach (string seam in new[] { "announce", "announceRendered" })
        {
            ArgumentSyntax argument = construction.Named(seam);
            // The VALUE, not just the presence: a `_ => { }` passed here
            // would satisfy a name check and post nothing.
            Assert.True(
                argument.Expression is MemberAccessExpressionSyntax access
                    && access.Expression is IdentifierNameSyntax { Identifier.ValueText: "_announcer" }
                    && access.Name.Identifier.ValueText == "Post",
                $"MainWindow must pass `{seam}: _announcer.Post` — the canonical "
                + $"dispatcher — and passes `{argument.Expression}` instead.");
        }
    }

    /// <summary>
    /// Hop 2 — the vault lifecycle forwards the rendered seam it was
    /// given to the workspace. A lifecycle that stored the seam and
    /// then built the workspace without it would leave hop 1 true and
    /// the chain broken.
    /// </summary>
    [Fact]
    public void TheVaultLifecycleForwardsTheRenderedSeamToTheWorkspace()
    {
        CSharpSource source = CSharpSource.Load("VaultLifecycleViewModel.cs");
        InvocationOrCreationArguments construction = ConstructionOf(
            source, "WorkspaceViewModel");
        ArgumentSyntax argument = construction.Named("announceRendered");
        Assert.True(
            argument.Expression is IdentifierNameSyntax
            {
                Identifier.ValueText: "_announceRendered",
            },
            "the vault lifecycle must forward its own stored rendered seam; it "
            + $"forwards `{argument.Expression}`.");
    }

    /// <summary>
    /// Hop 2b — the vault lifecycle STORES the parameter it was given.
    /// </summary>
    /// <remarks>
    /// The one assignment between two syntax-checked hops, and the last
    /// place a seam could be dropped without any other fact noticing:
    /// <c>_announceRendered = announceRendered ?? (_ => { })</c>. Replace
    /// the left of that <c>??</c> with a bare <c>_ => { }</c> and hops 1,
    /// 2 and 3 all still pass — every argument is still passed, every
    /// field is still forwarded, and nothing is ever spoken. The
    /// workspace's twin assignment needs no equivalent: it is covered at
    /// RUNTIME by <c>ACanvasLoadPostsThroughARealDispatcher</c>, which
    /// constructs the workspace with a real sink and hears the line come
    /// back.
    /// </remarks>
    [Fact]
    public void TheVaultLifecycleStoresTheRenderedSeamItWasGiven()
    {
        CSharpSource source = CSharpSource.Load("VaultLifecycleViewModel.cs");
        AssignmentExpressionSyntax[] assignments = source.Root
            .DescendantNodes()
            .OfType<AssignmentExpressionSyntax>()
            .Where(assignment => assignment.Left is IdentifierNameSyntax
            {
                Identifier.ValueText: "_announceRendered",
            })
            .ToArray();
        AssignmentExpressionSyntax assignment = Assert.Single(assignments);
        Assert.True(
            assignment.Right is BinaryExpressionSyntax
            {
                RawKind: (int)SyntaxKind.CoalesceExpression,
                Left: IdentifierNameSyntax { Identifier.ValueText: "announceRendered" },
            },
            "the vault lifecycle must store the rendered seam it was handed, "
            + "falling back to a no-op only when it was handed none; it stores "
            + $"`{assignment.Right}`.");
    }

    /// <summary>
    /// Hop 3 — the canvas registry builds every announcer over the
    /// workspace's rendered seam, and over nothing else. This is the
    /// hop that would otherwise be satisfied by a fresh
    /// <c>new CanvasAnnouncer(_ => { })</c>.
    /// </summary>
    [Fact]
    public void TheCanvasRegistryBuildsEveryAnnouncerOverTheWorkspaceSeam()
    {
        CSharpSource source = CSharpSource.Load("Canvas", "WorkspaceViewModel.Canvas.cs");
        ObjectCreationExpressionSyntax[] announcers = source.Root
            .DescendantNodes()
            .OfType<ObjectCreationExpressionSyntax>()
            .Where(creation => creation.Type.ToString() == "CanvasAnnouncer")
            .ToArray();

        Assert.True(
            announcers.Length == 1,
            $"the canvas registry constructs {announcers.Length} announcers; one "
            + "construction site is what makes the seam checkable at all.");
        ArgumentSyntax argument = Assert.IsType<ArgumentSyntax>(
            announcers[0].ArgumentList?.Arguments.FirstOrDefault());
        Assert.True(
            argument.Expression is IdentifierNameSyntax
            {
                Identifier.ValueText: "_announceRendered",
            },
            "every canvas announcer must post through the workspace's rendered "
            + $"seam; this one posts through `{argument.Expression}`.");
    }

    /// <summary>
    /// And the runtime half: a canvas load drives a REAL
    /// <see cref="AccessibilityNotificationDispatcher"/> over a real,
    /// shown element — the production sink, raising a real UIA
    /// notification, not a lambda standing in for one.
    /// </summary>
    /// <remarks>
    /// What this proves and what it does not, stated exactly. The
    /// recording wrapper is an OBSERVER in front of the dispatcher, not
    /// a replacement for it: the rendered line reaches
    /// <c>Post(RenderedAnnouncement)</c>, which renders no second time,
    /// resolves the peer and raises. What no in-process fact can assert
    /// is that a screen reader HEARD it — `RaiseNotificationEvent` is a
    /// no-op without a listening UIA client, which is the FlaUI
    /// journeys' job. So this fact pins the chain end to end through
    /// production types, and the three syntax facts above pin that the
    /// shipping code builds that chain.
    /// </remarks>
    [Fact]
    public void ACanvasLoadPostsThroughARealDispatcher() => RunSta(() =>
    {
        string root = Path.Combine(
            Path.GetTempPath(), $"slate-seam-census-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            // Entries core preserves but cannot show: the one canvas
            // load that speaks without any user interaction (A4).
            File.WriteAllText(
                Path.Combine(root, "skipped.canvas"),
                "{\"nodes\":["
                + "{\"id\":\"kept\",\"type\":\"text\",\"text\":\"kept\","
                + "\"x\":0,\"y\":0,\"width\":100,\"height\":50},"
                + "{\"id\":\"no-x\",\"type\":\"text\",\"text\":\"no x\","
                + "\"y\":0,\"width\":100,\"height\":50}],\"edges\":[]}");
            using var session = uniffi.slate_uniffi.VaultSession.OpenFilesystem(root);
            using (var cancel = new uniffi.slate_uniffi.CancelToken())
            {
                session.ScanInitial(cancel);
            }

            var element = new System.Windows.Controls.TextBlock();
            var host = new System.Windows.Window
            {
                Content = element,
                Width = 400,
                Height = 300,
                ShowInTaskbar = false,
                WindowStyle = System.Windows.WindowStyle.None,
                ShowActivated = false,
            };
            var raised = new List<string>();
            try
            {
                host.Show();
                var dispatcher = new AccessibilityNotificationDispatcher(element);
                using var workspace = new WorkspaceViewModel(
                    session,
                    root,
                    () => [],
                    dispatcher.Post,
                    startInteractionBackgroundWork: false,
                    announceRendered: rendered =>
                    {
                        raised.Add(rendered.Text);
                        dispatcher.Post(rendered);
                    });
                workspace.OpenPath("skipped.canvas");
                Canvas.CanvasDocumentViewModel document =
                    Assert.IsType<Canvas.CanvasDocumentViewModel>(
                        workspace.ActiveGroup.ActiveTab!.Canvas);
                document.AnnouncerForTests.FlushForTests();

                string spoken = Assert.Single(raised);
                Assert.Equal(document.DegradedBannerText, spoken);
            }
            finally
            {
                host.Close();
            }
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    });

    /// <summary>The arguments of the one construction of
    /// <paramref name="typeName"/> in this file. Ambiguity fails: this
    /// query reads one, so a second would go unchecked.</summary>
    private static InvocationOrCreationArguments ConstructionOf(
        CSharpSource source, string typeName)
    {
        ObjectCreationExpressionSyntax[] creations = source.Root
            .DescendantNodes()
            .OfType<ObjectCreationExpressionSyntax>()
            .Where(creation => creation.Type.ToString() == typeName)
            .ToArray();
        Assert.True(
            creations.Length == 1,
            $"{source.Path} constructs {typeName} {creations.Length} times; this "
            + "census reads one, so the rest would go unchecked.");
        return new InvocationOrCreationArguments(
            typeName,
            creations[0].ArgumentList?.Arguments
                ?? throw new Xunit.Sdk.XunitException(
                    $"the {typeName} construction has no argument list."));
    }

    private sealed class InvocationOrCreationArguments(
        string typeName, SeparatedSyntaxList<ArgumentSyntax> arguments)
    {
        internal ArgumentSyntax Named(string name)
        {
            ArgumentSyntax? match = arguments.FirstOrDefault(
                argument => argument.NameColon?.Name.Identifier.ValueText == name);
            Assert.True(
                match is not null,
                $"the {typeName} construction passes no `{name}:` argument. Every "
                + "announcement seam defaults to a no-op when its argument is "
                + "absent, so the omission is silent at run time and green in "
                + "every fact that injects its own sink.");
            return match!;
        }
    }

    private static void RunSta(Action body)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                body();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(120)), "STA census body timed out.");
        if (failure is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    /// <summary>
    /// Contract A5/C7: no canvas code reaches the announcer's
    /// compose-and-post surface except through the ONE boundary.
    /// </summary>
    /// <remarks>
    /// <para>
    /// PROVENANCE, because this guard is not hypothetical — it is four
    /// incidents old. The canvas has one announce boundary
    /// (<c>CanvasDocumentViewModel.Speak</c>) so that a retired document
    /// composes nothing, and until this arm existed that was a
    /// CONVENTION: a new <c>Announcer.Announce(…)</c> written anywhere
    /// under <c>Canvas/</c> passed every census, every fact and the
    /// build.
    /// </para>
    /// <para>
    /// It would have caught both instances that actually happened, and
    /// both were re-run against it rather than argued. The mode stack
    /// composed its confirmation on a retired document; it called an
    /// injected delegate, so the offending line is the WIRING —
    /// restoring <c>new CanvasModeController(Announcer.Announce)</c>
    /// fails this arm, naming that line. And <c>AdmitStructuralRead</c>,
    /// the never-silent mapping itself, announced a refusal through a
    /// retired funnel from a direct call — restoring
    /// <c>Announcer.Announce(new CanvasA11yEvent.CanvasStatus(note))</c>
    /// fails it too. A third mutation (a new direct call in the
    /// navigator) and a fourth (pointing a named seam somewhere that is
    /// not the announcer) close the arm's own two halves.
    /// </para>
    /// <para>
    /// EVERYTHING here is derived; there is no allow-list any more. The
    /// forbidden surface is every announcer member that reaches
    /// <c>Emit</c>, read out of `CanvasAnnouncer` itself, so a third
    /// poster added tomorrow joins without anyone editing this file. The
    /// scanned set is every production source under <c>Canvas/</c>, the
    /// same walk the other canvas censuses take. The exempt SEAMS are
    /// members of the document, found by name with their reasons
    /// attached, and each must be found — an exemption for a member that
    /// no longer touches the announcer is an exemption for nothing, and
    /// fails here rather than quietly widening the boundary.
    /// </para>
    /// <para>
    /// SCOPE, stated because the guarantee is narrower than the sentence
    /// "production cannot reach the announcer" suggests: this scan is
    /// <c>Canvas/</c> only. `AnnouncerForTests` is <c>internal</c>, so a
    /// shell file outside that directory could acquire it unpoliced.
    /// Nothing does — the residue's whole defence is that its NAME reads
    /// wrong in shipping code — but the wall is the directory, and the
    /// rest is the name.
    /// </para>
    /// </remarks>
    [Fact]
    public void NoCanvasCodeReachesTheAnnouncerExceptThroughTheBoundary()
    {
        CSharpSource announcer = CSharpSource.Load("Canvas", "CanvasAnnouncer.cs");
        // DERIVED: the compose-and-post surface is whatever reaches
        // `Emit`. `RenderLabel` renders and posts nothing; `Shutdown`,
        // `IsRetired` and the test seams say nothing either.
        string[] posters =
        [
            .. announcer.Root.DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .Where(method => method.DescendantNodes()
                    .OfType<InvocationExpressionSyntax>()
                    .Any(call => call.Expression
                        is IdentifierNameSyntax { Identifier.ValueText: "Emit" }))
                .Select(method => method.Identifier.ValueText)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(name => name, StringComparer.Ordinal),
        ];
        Assert.NotEmpty(posters);
        Assert.Contains("Announce", posters);

        // The ONE boundary, found rather than assumed: if it is renamed
        // or removed, every call below becomes an offender and this
        // assertion says why first. `Method` also fails on an ambiguous
        // name, which is the decoy shape a scrape has to refuse.
        CSharpSource document = CSharpSource.Load("Canvas", "CanvasDocumentViewModel.cs");
        _ = document.Method("Speak");
        const string BoundaryFile = "CanvasDocumentViewModel.cs";

        // DERIVED: the RESIDUE — the member that hands out the announcer
        // itself. `internal` cannot separate this assembly's tests from
        // its production code, so one handle survives privatisation, and
        // the census's first rule is about that member by name. Read out
        // of the document (an expression-bodied property whose whole body
        // IS the announcer field) so a rename brings this file with it
        // rather than leaving it guarding a name nobody uses.
        string[] residue =
        [
            .. document.Root.DescendantNodes()
                .OfType<PropertyDeclarationSyntax>()
                .Where(property => property.ExpressionBody?.Expression
                        is IdentifierNameSyntax field
                    && field.Identifier.ValueText
                        .EndsWith("announcer", StringComparison.OrdinalIgnoreCase))
                .Select(property => property.Identifier.ValueText),
        ];
        Assert.True(
            residue.Length > 0,
            "no member of CanvasDocumentViewModel hands out the announcer "
            + "itself, so either the residue is gone — delete the acquisition "
            + "rule with it — or this census is watching for a name that no "
            + "longer exists.");

        // The TWO named seams, each with its reason. They are members of
        // the document rather than an allow-list of call sites now — the
        // announcer is private, so this is the whole surface, and both
        // must be FOUND or the census is exempting nothing.
        (string Member, string Why)[] seams =
        [
            ("Speak",
                "the ONE announce boundary (contracts A5/C7): a retired "
                + "document composes nothing, and the check has to live in "
                + "the speaker rather than the funnel."),
            ("GridRelaySeam",
                "contract B7: the substrate raises CANONICAL grid events "
                + "(sort, row move, cell move) and they ride the canvas "
                + "funnel uncoalesced, carrying core's own priority through. "
                + "Not a canvas sentence, so it gets a named member rather "
                + "than a hole in the boundary."),
        ];
        var seamsFound = new HashSet<string>(StringComparer.Ordinal);

        var offenders = new List<string>();
        string[] files = [.. CanvasSources()];
        Assert.NotEmpty(files);
        foreach (string file in files)
        {
            CSharpSource source = CSharpSource.LoadPath(file);
            string name = Path.GetFileName(file);
            var reaches = new List<SyntaxNode>();
            foreach (MemberAccessExpressionSyntax access in source.Root
                .DescendantNodes()
                .OfType<MemberAccessExpressionSyntax>())
            {
                // TWO rules, and the first is the one that closes the
                // evasions. ACQUIRING the funnel is forbidden outright:
                // the residue is the only handle production can still
                // obtain (the field is private), so an alias, a captured
                // lambda, a conditional access or a transitive helper all
                // fail HERE, at the point they get it, rather than at a
                // call site a receiver-shaped scan has to recognise.
                bool acquires = residue.Contains(
                    access.Name.Identifier.ValueText, StringComparer.Ordinal);
                // And the older rule stays as the belt: a poster called
                // on anything announcer-shaped. Both an invocation and a
                // bare method group count — the seam that let the mode
                // stack past the boundary was a method group handed to a
                // constructor.
                bool posts = posters.Contains(access.Name.Identifier.ValueText)
                    && CSharpSource.Normalize(access.Expression)
                        // Case-insensitive on the suffix, so the private
                        // FIELD (`_announcer`) is matched as readily as a
                        // property — the boundary's own call is what
                        // proves this scan reaches anything at all.
                        .EndsWith("announcer", StringComparison.OrdinalIgnoreCase);
                if (acquires || posts)
                {
                    reaches.Add(access);
                }
            }
            foreach (IdentifierNameSyntax bare in source.Root
                .DescendantNodes()
                .OfType<IdentifierNameSyntax>())
            {
                // The spelling both rules above miss, because there is no
                // receiver TEXT to match: an implicit `this` inside the
                // declaring file. `AnnouncerForTests.Announce(e)` written
                // in `CanvasDocumentViewModel.cs` reads as a bare name —
                // and that file is where a new announce site is most
                // likely to be written in the first place. A qualified
                // `x.AnnouncerForTests` is skipped here because the
                // acquisition rule above already has it.
                if (!residue.Contains(bare.Identifier.ValueText, StringComparer.Ordinal)
                    || (bare.Parent is MemberAccessExpressionSyntax qualified
                        && qualified.Name == bare))
                {
                    continue;
                }
                reaches.Add(bare);
            }

            foreach (SyntaxNode reach in reaches)
            {
                // The enclosing MEMBER, method or property: the relay
                // seam is expression-bodied, and a scan that only knew
                // methods would have reported it as top-level noise.
                MemberDeclarationSyntax? owner = reach.Ancestors()
                    .OfType<MemberDeclarationSyntax>()
                    .FirstOrDefault(member =>
                        member is MethodDeclarationSyntax or PropertyDeclarationSyntax);
                string ownerName = owner switch
                {
                    MethodDeclarationSyntax method => method.Identifier.ValueText,
                    PropertyDeclarationSyntax property => property.Identifier.ValueText,
                    _ => "<top level>",
                };
                if (name == BoundaryFile
                    && seams.Any(seam => seam.Member == ownerName))
                {
                    _ = seamsFound.Add(ownerName);
                    continue;
                }
                offenders.Add(
                    $"{name}:{ownerName} — {CSharpSource.Normalize(reach)}");
            }
        }

        Assert.True(
            offenders.Count == 0,
            "canvas code reaches the announcer's post surface without going "
            + "through `CanvasDocumentViewModel.Speak`, so a retired document "
            + "can compose a sentence for a closed funnel — the defect this "
            + $"branch fixed twice: {string.Join("; ", offenders)}");
        string[] unusedSeams =
            [.. seams.Where(seam => !seamsFound.Contains(seam.Member))
                .Select(seam => $"{seam.Member} ({seam.Why})")];
        Assert.True(
            unusedSeams.Length == 0,
            "a named seam never reached the announcer, so this census is "
            + "exempting nothing there and either the seam is stale or the "
            + $"scan is broken: {string.Join("; ", unusedSeams)}");
    }

    private static IEnumerable<string> CanvasSources()
    {
        string root = Path.Combine(SourceText.ShellSourceRoot(), "Canvas");
        Assert.True(Directory.Exists(root), $"the canvas source root is missing: {root}");
        return Directory
            .EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .OrderBy(file => file, StringComparer.Ordinal);
    }

    /// <summary>
    /// W6-2 PR A (#746), contract A-10: the graph twin of the canvas arm
    /// above, keyed on <c>Graph/</c>. The residue is the document's
    /// <c>AnnouncerForTests</c>; the posters are the relay's members that
    /// reach <c>Emit</c>; the named seams are the document's own — the
    /// status the workspace asks for, the effective-gated announce, the
    /// grid's relay seam and the surface's adoption line — and each must
    /// be hit or the census exempts nothing.
    /// </summary>
    [Fact]
    public void NoGraphCodeReachesTheAnnouncerExceptThroughTheBoundary()
    {
        CSharpSource announcer = CSharpSource.Load("Graph", "GraphAnnouncer.cs");
        string[] posters =
        [
            .. announcer.Root.DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .Where(method => method.DescendantNodes()
                    .OfType<InvocationExpressionSyntax>()
                    .Any(call => call.Expression
                        is IdentifierNameSyntax { Identifier.ValueText: "Emit" }))
                .Select(method => method.Identifier.ValueText)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(name => name, StringComparer.Ordinal),
        ];
        Assert.NotEmpty(posters);
        Assert.Contains("Announce", posters);
        Assert.Contains("Relay", posters);

        CSharpSource document = CSharpSource.Load("Graph", "GraphDocumentViewModel.cs");
        const string BoundaryFile = "GraphDocumentViewModel.cs";
        string[] residue =
        [
            .. document.Root.DescendantNodes()
                .OfType<PropertyDeclarationSyntax>()
                .Where(property => property.ExpressionBody?.Expression
                        is IdentifierNameSyntax field
                    && field.Identifier.ValueText
                        .EndsWith("announcer", StringComparison.OrdinalIgnoreCase))
                .Select(property => property.Identifier.ValueText),
        ];
        Assert.Equal(["AnnouncerForTests"], residue);

        (string Member, string Why)[] seams =
        [
            ("AnnounceStatus", "rule L's status the workspace asks for (Term 6)"),
            ("AnnounceIfEffective", "the effective-gated announce (Term 2: speech at EFFECTIVE)"),
            ("GridRelaySeam", "the grid's canonical events ride the relay uncoalesced"),
            ("RelayGridEvent", "the surface's GridSorted on adoption (contract A-5)"),
        ];
        var seamsFound = new HashSet<string>(StringComparer.Ordinal);
        var offenders = new List<string>();
        string root = Path.Combine(SourceText.ShellSourceRoot(), "Graph");
        Assert.True(Directory.Exists(root), $"the graph source root is missing: {root}");
        string[] files = [.. Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal)];
        Assert.NotEmpty(files);
        foreach (string file in files)
        {
            CSharpSource source = CSharpSource.LoadPath(file);
            // Every file exemption is by NORMALIZED path at the root of
            // Graph/ — never by basename (IPA-7, IPB-5; IGA-32's nested-name
            // bypass): the relay, and the document whose named seams are
            // exempted below.
            string name = Path.GetRelativePath(root, file).Replace('\\', '/');
            if (name == "GraphAnnouncer.cs")
            {
                continue;
            }
            var reaches = new List<SyntaxNode>();
            foreach (MemberAccessExpressionSyntax access in source.Root
                .DescendantNodes()
                .OfType<MemberAccessExpressionSyntax>())
            {
                bool acquires = residue.Contains(access.Name.Identifier.ValueText, StringComparer.Ordinal);
                bool posts = posters.Contains(access.Name.Identifier.ValueText)
                    && CSharpSource.Normalize(access.Expression)
                        .EndsWith("announcer", StringComparison.OrdinalIgnoreCase);
                if (acquires || posts)
                {
                    reaches.Add(access);
                }
            }
            foreach (IdentifierNameSyntax bare in source.Root
                .DescendantNodes()
                .OfType<IdentifierNameSyntax>())
            {
                if (!residue.Contains(bare.Identifier.ValueText, StringComparer.Ordinal)
                    || (bare.Parent is MemberAccessExpressionSyntax qualified && qualified.Name == bare))
                {
                    continue;
                }
                reaches.Add(bare);
            }
            foreach (SyntaxNode reach in reaches)
            {
                MemberDeclarationSyntax? owner = reach.Ancestors()
                    .OfType<MemberDeclarationSyntax>()
                    .FirstOrDefault(member =>
                        member is MethodDeclarationSyntax or PropertyDeclarationSyntax);
                string ownerName = owner switch
                {
                    MethodDeclarationSyntax method => method.Identifier.ValueText,
                    PropertyDeclarationSyntax property => property.Identifier.ValueText,
                    _ => "<top level>",
                };
                if (name == BoundaryFile && seams.Any(seam => seam.Member == ownerName))
                {
                    _ = seamsFound.Add(ownerName);
                    continue;
                }
                if (name == BoundaryFile && ownerName == "AnnouncerForTests")
                {
                    // The residue's own declaration.
                    continue;
                }
                offenders.Add($"{name}:{ownerName} — {CSharpSource.Normalize(reach)}");
            }
        }
        Assert.True(
            offenders.Count == 0,
            "graph code reaches the announcer's post surface outside the document's named seams: "
            + string.Join("; ", offenders));
        string[] unusedSeams =
            [.. seams.Where(seam => !seamsFound.Contains(seam.Member)).Select(seam => $"{seam.Member} ({seam.Why})")];
        Assert.True(
            unusedSeams.Length == 0,
            "a named graph seam never reached the announcer, so this census is exempting nothing there: "
            + string.Join("; ", unusedSeams));

        // AD-8 (IPA-7): the ONE graph-family posting site outside Graph/ —
        // the create funnel's workspace completion, in a ROOT partial —
        // named here and asserted to be the only one in the shell.
        string shellRoot = SourceText.ShellSourceRoot();
        var outsideSites = new List<string>();
        foreach (string file in Directory.EnumerateFiles(shellRoot, "*.cs", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(shellRoot, file).Replace('\\', '/');
            if (relative.StartsWith("Graph/", StringComparison.Ordinal)
                || relative.StartsWith("obj/", StringComparison.Ordinal)
                || relative.Contains("/obj/", StringComparison.Ordinal))
            {
                continue;
            }
            CSharpSource source = CSharpSource.LoadPath(file);
            foreach (ObjectCreationExpressionSyntax creation in source.Root
                .DescendantNodes()
                .OfType<ObjectCreationExpressionSyntax>())
            {
                if (!creation.Type.ToString().EndsWith("A11yEvent.Graph", StringComparison.Ordinal))
                {
                    continue;
                }
                MethodDeclarationSyntax? owner = creation.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
                outsideSites.Add($"{relative}:{owner?.Identifier.ValueText}");
            }
            // IPB-5: the shapes the textual rule cannot see are forbidden
            // outright across the shell — a `using` alias (which could name
            // the graph family under another word) and a target-typed or
            // pre-built argument handed to an announce delegate.
            foreach (UsingDirectiveSyntax directive in source.Root.DescendantNodes().OfType<UsingDirectiveSyntax>())
            {
                // An alias OR a static import naming the family (IPC-3):
                // either lets a bare `Graph` name the event.
                if ((directive.Alias is not null || directive.StaticKeyword.RawKind != 0)
                    && directive.NamespaceOrType.ToString().Contains("A11yEvent", StringComparison.Ordinal))
                {
                    outsideSites.Add($"{relative}:using directive {directive.NamespaceOrType}");
                }
            }
            foreach (InvocationExpressionSyntax call in source.Root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                // The callee normalised to its terminal identifier (IPC-3).
                string? calleeName = GraphAnnouncerCensus.TerminalIdentifier(call.Expression);
                if (calleeName is null
                    || !calleeName.EndsWith("announce", StringComparison.OrdinalIgnoreCase)
                    || call.ArgumentList.Arguments.Count != 1)
                {
                    continue;
                }
                if (call.ArgumentList.Arguments[0].Expression is ImplicitObjectCreationExpressionSyntax)
                {
                    MethodDeclarationSyntax? owner = call.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
                    outsideSites.Add($"{relative}:{owner?.Identifier.ValueText} (target-typed new)");
                }
            }
        }
        Assert.Equal(
            ["WorkspaceViewModel.GraphCreate.cs:GraphNoteCreationCompleted"],
            outsideSites.Distinct().OrderBy(site => site, StringComparer.Ordinal));
    }
}
