// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SlateWindows.Canvas;

namespace SlateWindows.Tests.Censuses;

/// <summary>
/// W6-1 PR C-unit, task T4: the model censuses — U13's arms at the
/// scale this PR owns, each from an authority independent of what it
/// checks, each with a discovery floor so an arm that finds nothing
/// fails rather than passes.
/// </summary>
/// <remarks>
/// <para>
/// THE CLOSED WORLD is the model's nine files, and it is closed BOTH
/// ways: every type the files declare is reflected over or censused as
/// stateless, and every reflected type is declared — the cross-check
/// below — so a member can hide from neither enumeration.
/// </para>
/// <para>
/// EVERY SOURCE ARM READS SYNTAX TREES, not strings — the house
/// helper's own #741 doctrine, applied here after the T4 review found
/// this census re-deriving the string matching that helper exists to
/// retire: structurally, a comment or a string literal can neither
/// trip a wall nor hide from one, an assignment is every compound kind
/// at once, and the alias-through-a-local bypass is chased the way the
/// helper chases it.
/// </para>
/// <para>
/// WHAT THE ARMS NARROW, said once: the writer arm censuses ORDINARY
/// SOURCE WRITES — reflection, unsafe accessors and serializer
/// hydration are beyond a source census, prohibited by nothing but the
/// closed world (no model type or member opts into serialization,
/// which one arm asserts) and caught at runtime by the slot's own
/// bypass detector. The purity arm walks each transform's OWN body,
/// not its transitive call graph; the compiler-grade walk is the
/// not-taken alternative recorded with obligation I4. The aliasing arm
/// is a wall, not an owned row type, and obligation I7's note records
/// that narrowing too.
/// </para>
/// </remarks>
public sealed class CanvasModelCensus
{
    /// <summary>The model's files — the closed world every source arm
    /// walks.</summary>
    private static readonly string[] ModelFiles =
    [
        "CanvasPublication.cs",
        "CanvasPublicationSlot.cs",
        "CanvasHandleLease.cs",
        "CanvasLeaseTransfer.cs",
        "CanvasPopulation.cs",
        "CanvasProjectionUnit.cs",
        "CanvasModelCopy.cs",
        "CanvasLoadPipeline.cs",
        "CanvasFilterMachine.cs",
    ];

    /// <summary>The model's types — the reflection arms' half of the
    /// same closed world.</summary>
    private static readonly Type[] ModelTypes =
    [
        typeof(CanvasPublication),
        typeof(CanvasLoaded),
        typeof(CanvasLoadSchedule),
        typeof(CanvasFilterSchedule),
        typeof(CanvasRequestIdentity),
        typeof(CanvasPublicationSlot),
        typeof(CanvasRepublishOutcome),
        typeof(CanvasPublicationInstallObserver),
        typeof(CanvasPublicationOutcome),
        typeof(CanvasPublicationRefusedException),
        typeof(CanvasHandleLease),
        typeof(CanvasLeaseViolationException),
        typeof(CanvasFaults),
        typeof(CanvasLeaseTransfer),
        typeof(CanvasPopulation),
        typeof(CanvasProjectionUnit),
        typeof(CanvasModelCopy),
        typeof(CanvasLoadPipeline),
        typeof(CanvasLoadRequest),
        typeof(CanvasLoadAcceptance),
        typeof(CanvasLoadFailure),
        typeof(CanvasLoadProbeForTests),
        typeof(CanvasFilterMachine),
        typeof(CanvasFilterProbeForTests),
    ];

    /// <summary>Declarations in the model files with no reflectable
    /// state, censused so the cross-check stays two-way.</summary>
    private static readonly Dictionary<string, string> NonReflectedDeclarations =
        new(StringComparer.Ordinal)
        {
            ["CanvasLoadDelivery"] = "enum — no storable state",
            ["CanvasLeaseRelease"] = "enum — no storable state",
            ["CanvasAnswerState"] = "enum — no storable state",
            ["CanvasPublicationRefusal"] = "enum — no storable state",
            ["CanvasLoadOutcome"] = "enum — no storable state",
            ["CanvasLoadPoint"] = "enum — no storable state",
            ["CanvasFilterPoint"] = "enum — no storable state",
            ["ICanvasLoadSource"] = "interface — declares no fields",
            ["ICanvasFilterSource"] = "interface — declares no fields",
        };

    /// <summary>Every live shell source, parsed ONCE per run on the
    /// house syntax-tree helper, with obj/ and bin/ excluded the way
    /// the sibling censuses exclude them — the T4 review's two
    /// confirmed findings, both against this census's first
    /// draft.</summary>
    private static readonly Lazy<IReadOnlyList<CSharpSource>> ShellSources = new(() =>
        [.. Directory
            .EnumerateFiles(SourceText.ShellSourceRoot(), "*.cs", SearchOption.AllDirectories)
            .Where(path =>
                !path.Contains(
                    $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                    StringComparison.Ordinal)
                && !path.Contains(
                    $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                    StringComparison.Ordinal))
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(CSharpSource.LoadPath)]);

    private static IEnumerable<CSharpSource> ModelSources() =>
        ShellSources.Value.Where(source =>
            ModelFiles.Contains(Path.GetFileName(source.Path), StringComparer.Ordinal));

    private static CSharpSource Model(string file) =>
        ModelSources().Single(source => Path.GetFileName(source.Path) == file);

    /// <summary>The closed world, closed both ways: a type the files
    /// declare that no census walks fails, and so does a census entry
    /// for a type the model no longer declares.</summary>
    [Fact]
    public void TheClosedWorldIsClosedBothWays()
    {
        var declared = new HashSet<string>(StringComparer.Ordinal);
        foreach (CSharpSource source in ModelSources())
        {
            foreach (BaseTypeDeclarationSyntax declaration in
                source.Root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>())
            {
                _ = declared.Add(declaration.Identifier.ValueText);
            }
        }

        var accounted = ModelTypes.Select(type => type.Name)
            .Concat(NonReflectedDeclarations.Keys)
            .ToHashSet(StringComparer.Ordinal);
        List<string> hiding = [.. declared.Except(accounted)];
        List<string> stale = [.. accounted.Except(declared)];
        Assert.True(
            hiding.Count == 0,
            "types the model files declare and no census walks:\n  "
            + string.Join("\n  ", hiding));
        Assert.True(
            stale.Count == 0,
            "census entries for types the model no longer declares:\n  "
            + string.Join("\n  ", stale));
    }

    // ---------------------------------------------------------------
    // Obligation I8 — the publication-writer arm
    // ---------------------------------------------------------------

    /// <summary>One writable field holds a publication anywhere in the
    /// model, and one method writes it: the constructor's seed and the
    /// single compare-and-swap, counted as SYNTAX — an assignment node
    /// and an invocation node — so dead text can neither satisfy nor
    /// trip the count. This is the census T1's bypass detector waited
    /// for: the branch is structural now, not defensive.</summary>
    [Fact]
    public void OneFieldHoldsThePublicationAndOneMethodWritesIt()
    {
        var holders = new List<string>();
        foreach (Type type in ModelTypes)
        {
            foreach (FieldInfo field in AllFields(type))
            {
                if (field.FieldType == typeof(CanvasPublication) && !field.IsInitOnly)
                {
                    holders.Add($"{type.Name}.{Normalize(field.Name)}");
                }
            }
        }

        Assert.True(
            holders.Count == 1 && holders[0] == "CanvasPublicationSlot._current",
            $"writable publication-typed fields: [{string.Join(", ", holders)}]; "
            + "the slot's one private field is the whole of U1's mutable model "
            + "state.");

        CSharpSource slot = Model("CanvasPublicationSlot.cs");
        int seeds = slot.Root.DescendantNodes()
            .OfType<AssignmentExpressionSyntax>()
            .Count(assignment => assignment.Left is IdentifierNameSyntax
            {
                Identifier.ValueText: "_current",
            });
        Assert.True(
            seeds == 1,
            $"{seeds} direct assignments to the slot's field; the constructor's "
            + "seed is the only one.");

        int swaps = slot.Root.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Count(invocation =>
                CSharpSource.Normalize(invocation.Expression) == "Interlocked.CompareExchange"
                && invocation.ArgumentList.Arguments.Count > 0
                && invocation.ArgumentList.Arguments[0].Expression is IdentifierNameSyntax
                {
                    Identifier.ValueText: "_current",
                });
        Assert.True(
            swaps == 1,
            $"{swaps} compare-and-swaps against the field; the install is exactly "
            + "one.");

        foreach (CSharpSource source in ModelSources())
        {
            Assert.True(
                Path.GetFileName(source.Path) == "CanvasPublicationSlot.cs"
                    || !CSharpSource.References(source.Root, "_current"),
                $"{Path.GetFileName(source.Path)} references the slot's field — "
                + "structurally, so a comment could not have tripped this and "
                + "cannot hide it.");
        }
    }

    /// <summary>No model type OR MEMBER opts into serialization:
    /// hydration is one of obligation I8's named bypasses, and the
    /// opt-in happens at the member as often as at the type — the T4
    /// review's point against the type-only first draft.</summary>
    [Fact]
    public void NoModelTypeOrMemberOptsIntoSerialization()
    {
        foreach (Type type in ModelTypes)
        {
            foreach ((string where, object attribute) in TypeAndMemberAttributes(type))
            {
                string name = attribute.GetType().FullName!;
                Assert.True(
                    !name.Contains("Serial", StringComparison.Ordinal)
                        && !name.Contains("DataContract", StringComparison.Ordinal)
                        && !name.Contains("DataMember", StringComparison.Ordinal)
                        && !name.Contains("Json", StringComparison.Ordinal),
                    $"{where} carries a serialization attribute: {name}.");
            }
        }
    }

    private static IEnumerable<(string Where, object Attribute)> TypeAndMemberAttributes(
        Type type)
    {
        foreach (object attribute in type.GetCustomAttributes(inherit: false))
        {
            yield return (type.Name, attribute);
        }

        foreach (MemberInfo member in type.GetMembers(
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public
                | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
        {
            if (member.Name.StartsWith('<'))
            {
                // The compiler stamps [Serializable] on its own closure
                // classes; synthesized members are not opt-ins.
                continue;
            }

            foreach (object attribute in member.GetCustomAttributes(inherit: false))
            {
                yield return ($"{type.Name}.{member.Name}", attribute);
            }
        }
    }

    /// <summary>The minting walls, construction half: a lease is
    /// created only by the pipeline, a population only by the pipeline
    /// and its own Empty — counted as creation NODES, so a name in a
    /// comment is nothing.</summary>
    [Theory]
    [InlineData("CanvasHandleLease", "CanvasLoadPipeline.cs")]
    [InlineData("CanvasPopulation", "CanvasLoadPipeline.cs|CanvasPopulation.cs")]
    public void TheMintingWallsHold(string typeName, string allowedFiles) =>
        AssertWall(
            $"new {typeName}(…)",
            allowedFiles,
            source => source.Root.DescendantNodes()
                .OfType<ObjectCreationExpressionSyntax>()
                .Count(creation => creation.Type.ToString() == typeName));

    /// <summary>The minting walls, transform half: the chain, terminal
    /// and un-name transforms are INVOKED only by their owners —
    /// invocation nodes, member-access and null-conditional forms
    /// both.</summary>
    [Theory]
    [InlineData("WithLoaded", "CanvasLeaseTransfer.cs")]
    [InlineData("WithUnit", "CanvasPublication.cs|CanvasFilterMachine.cs")]
    [InlineData("WithTerminal", "CanvasLeaseTransfer.cs")]
    [InlineData("WithUnloaded", "CanvasLoadPipeline.cs")]
    public void TheTransformWallsHold(string memberName, string allowedFiles) =>
        AssertWall(
            $".{memberName}(…)",
            allowedFiles,
            source => source.Root.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Count(invocation => InvokedName(invocation) == memberName));

    /// <summary>The one wall loop (the cleanup pass — the two theories
    /// above differed only in which syntax node they count): count the
    /// nodes, name the offenders, and refuse a wall that guards
    /// nothing.</summary>
    private static void AssertWall(
        string subject, string allowedFiles, Func<CSharpSource, int> countIn)
    {
        string[] allowed = allowedFiles.Split('|');
        var offenders = new List<string>();
        var found = 0;
        foreach (CSharpSource source in ShellSources.Value)
        {
            int count = countIn(source);
            if (count == 0)
            {
                continue;
            }

            found += count;
            string file = Path.GetFileName(source.Path);
            if (!allowed.Contains(file))
            {
                offenders.Add($"{file} ({count})");
            }
        }

        Assert.True(
            found > 0,
            $"the wall for {subject} guards nothing — did the name move?");
        Assert.True(
            offenders.Count == 0,
            $"{subject} is reached from outside its wall: "
            + string.Join(", ", offenders));
    }

    private static string? InvokedName(InvocationExpressionSyntax invocation) =>
        invocation.Expression switch
        {
            MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
            MemberBindingExpressionSyntax binding => binding.Name.Identifier.ValueText,
            _ => null,
        };

    // ---------------------------------------------------------------
    // Obligation I4 — the transform-purity arm
    // ---------------------------------------------------------------

    /// <summary>Every transform handed to the gate ANYWHERE in the
    /// shell — the fixed file list was the T4 review's point — walked
    /// structurally for the finding's own list: no session, no FFI, no
    /// clock, no scheduler, no announcer, no lease close, no live UI
    /// state, no locks or interlocked operations. Identifier and
    /// member-name queries over the syntax tree, so a probe line, a
    /// comment or a string can neither hide an impurity nor invent one;
    /// the probes stay sanctioned simply because <c>Reached</c> is not
    /// a forbidden name.</summary>
    /// <remarks>
    /// One level deep on purpose, and obligation I4's ledger note says
    /// so: a transform's callees are the model's own pure surface, and
    /// the compiler-grade transitive walk is the not-taken alternative.
    /// </remarks>
    [Fact]
    public void EveryPublishedTransformIsPureAtItsOwnLevel()
    {
        string[] forbiddenIdentifiers =
        [
            "_session", "_announcer", "_machine", "_pipeline", "_applied",
            "_filterText", "_rows", "_outline", "_targets", "_subpaths",
            "_neighbors", "Selection", "DateTime", "Stopwatch", "Environment",
            "Task", "Thread", "Random", "File", "Directory", "Console",
            "Interlocked", "Volatile",
        ];
        string[] forbiddenMembers =
        [
            "Close", "Invoke", "Post", "Speak", "StartWork", "Match",
            "Now", "UtcNow", "Sleep", "Wait", "Publish",
        ];
        var sites = 0;
        var offences = new List<string>();
        foreach (CSharpSource source in ShellSources.Value)
        {
            string file = Path.GetFileName(source.Path);
            foreach (InvocationExpressionSyntax invocation in
                source.Root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (InvokedName(invocation) != "Publish")
                {
                    continue;
                }

                sites++;
                int line = invocation.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                SyntaxNode region = invocation.ArgumentList;
                foreach (string identifier in forbiddenIdentifiers)
                {
                    if (CSharpSource.References(region, identifier))
                    {
                        offences.Add($"{file}:{line} reads '{identifier}'");
                    }
                }

                var members = CSharpSource.MemberNames(region)
                    .ToHashSet(StringComparer.Ordinal);
                foreach (string member in forbiddenMembers)
                {
                    if (members.Contains(member))
                    {
                        offences.Add($"{file}:{line} calls '{member}'");
                    }
                }

                if (region.DescendantNodes().OfType<LockStatementSyntax>().Any())
                {
                    offences.Add($"{file}:{line} takes a lock");
                }
            }
        }

        Assert.True(
            sites >= 10,
            $"only {sites} transforms found across the shell; the arm would be "
            + "scanning almost nothing — did the publish helper move?");
        Assert.True(
            offences.Count == 0,
            "a transform reaches outside its snapshot:\n  "
            + string.Join("\n  ", offences));
    }

    // ---------------------------------------------------------------
    // Obligation I7 — the aliasing analyzer, structural half
    // ---------------------------------------------------------------

    /// <summary>Nothing in the shell writes INTO a row's group path —
    /// as SYNTAX: every assignment kind at once (compound included,
    /// which the first draft's regex missed), increments, ref-argument
    /// captures, and the alias-through-a-local bypass chased the way
    /// the house helper chases it. The owned-row alternative was not
    /// taken; obligation I7's note records the narrowing.</summary>
    [Fact]
    public void NothingWritesIntoARowsGroupPath()
    {
        var offenders = new List<string>();
        var reads = 0;
        foreach (CSharpSource source in ShellSources.Value)
        {
            foreach (ElementAccessExpressionSyntax element in
                source.Root.DescendantNodes().OfType<ElementAccessExpressionSyntax>())
            {
                if (!TargetsGroupPath(element, source.Root))
                {
                    continue;
                }

                reads++;
                if (IsWrittenThrough(element))
                {
                    offenders.Add(
                        $"{Path.GetFileName(source.Path)}: {element.Parent}");
                }
            }
        }

        Assert.True(
            reads >= 2,
            $"the wall guards {reads} group-path element accesses — did the "
            + "member move?");
        Assert.True(
            offenders.Count == 0,
            "a group path is written through a retained row: "
            + string.Join("; ", offenders));
    }

    private static bool TargetsGroupPath(
        ElementAccessExpressionSyntax element, SyntaxNode scope)
    {
        ExpressionSyntax target = element.Expression;
        if (target is IdentifierNameSyntax)
        {
            // The alias bypass: a local assigned the array satisfies any
            // check made against the member as written — resolved the
            // way the house helper resolves it.
            target = CSharpSource.Resolve(target, scope);
        }

        return target switch
        {
            MemberAccessExpressionSyntax access =>
                access.Name.Identifier.ValueText == "GroupPath",
            MemberBindingExpressionSyntax binding =>
                binding.Name.Identifier.ValueText == "GroupPath",
            IdentifierNameSyntax identifier =>
                identifier.Identifier.ValueText == "GroupPath",
            _ => false,
        };
    }

    private static bool IsWrittenThrough(ElementAccessExpressionSyntax element) =>
        element.Parent switch
        {
            AssignmentExpressionSyntax assignment => assignment.Left == element,
            PrefixUnaryExpressionSyntax prefix =>
                prefix.IsKind(SyntaxKind.PreIncrementExpression)
                || prefix.IsKind(SyntaxKind.PreDecrementExpression),
            PostfixUnaryExpressionSyntax postfix =>
                postfix.IsKind(SyntaxKind.PostIncrementExpression)
                || postfix.IsKind(SyntaxKind.PostDecrementExpression),
            ArgumentSyntax argument => argument.RefOrOutKeyword != default,
            _ => false,
        };

    /// <summary>Rows are re-materialised at ONE site: the group-path
    /// `with` copy exists only in the model-copy helper — as WITH
    /// EXPRESSIONS, so mentioning the idiom is nothing.</summary>
    [Fact]
    public void RowsAreRematerialisedAtOneSite()
    {
        foreach (CSharpSource source in ShellSources.Value)
        {
            int count = source.Root.DescendantNodes()
                .OfType<WithExpressionSyntax>()
                .Count(with => with.Initializer.Expressions
                    .OfType<AssignmentExpressionSyntax>()
                    .Any(assignment => assignment.Left is IdentifierNameSyntax
                    {
                        Identifier.ValueText: "GroupPath",
                    }));
            if (Path.GetFileName(source.Path) == "CanvasModelCopy.cs")
            {
                Assert.True(
                    count == 2,
                    $"the helper re-materialises {count} row kinds; the model's "
                    + "two are the outline's and the table's.");
            }
            else
            {
                Assert.True(
                    count == 0,
                    $"{Path.GetFileName(source.Path)} re-materialises rows outside "
                    + "the one construction site.");
            }
        }
    }

    /// <summary>Immutable collections are BUILT at two files only — the
    /// copy helper and the population's own indexes — measured as
    /// invoked member names, not text.</summary>
    [Fact]
    public void CollectionsAreBuiltAtTheSanctionedSites()
    {
        string[] builders =
        [
            "ToImmutable", "ToImmutableDictionary", "CreateBuilder",
            "CreateRange", "Create",
        ];
        foreach (CSharpSource source in ModelSources())
        {
            string file = Path.GetFileName(source.Path);
            if (file is "CanvasModelCopy.cs" or "CanvasPopulation.cs")
            {
                continue;
            }

            var members = CSharpSource.MemberNames(source.Root)
                .ToHashSet(StringComparer.Ordinal);
            foreach (string builder in builders)
            {
                Assert.True(
                    !members.Contains(builder),
                    $"{file} builds a collection with {builder} outside the "
                    + "sanctioned sites.");
            }
        }
    }

    // ---------------------------------------------------------------
    // U4's carry-forward manifest
    // ---------------------------------------------------------------

    /// <summary>Every publication member classified, as a TOTAL table
    /// keyed by reflection — instance properties in the manifest,
    /// statics and bare fields walled separately (the T4 review's
    /// point: a cached static instance is the exact I5 shape, and
    /// GetProperties alone would never see it). A new member fails BY
    /// NAME until the rebase accounts for it.</summary>
    [Fact]
    public void TheCarryForwardManifestCoversEveryPublicationMember()
    {
        var manifest = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Retired"] = "DOCUMENT — read by every admission; the terminal transitions write it",
            ["LoadState"] = "DOCUMENT — the acceptance writes Ready; a failure writes its state with the release",
            ["LoadMessage"] = "DOCUMENT — written with LoadState",
            ["ActiveSurface"] = "DOCUMENT intent — CARRIED unchanged, round 5's member",
            ["SelectedIntent"] = "DOCUMENT intent — carried; REBASED into the unit's resolved selection",
            ["MarkedIntent"] = "DOCUMENT intent — carried; resolved per query via the population",
            ["NeedleIntent"] = "DOCUMENT intent — carried; RESEEDS the filter machine at acceptance",
            ["Loads"] = "schedule — the acceptance publishes consumed in its own swap",
            ["Filters"] = "schedule — retired and reseeded by the acceptance; retired by a failure",
            ["Loaded"] = "the chain — replaced whole by the acceptance, cleared by the un-name and the terminal",
            ["CommittedUnpresented"] = "DOCUMENT — the funnel writes it at a failed refresh (IE-10); carried unchanged; cleared only by the refresh-only recovery",
            ["Lease"] = "derived getter over Loaded",
            ["Population"] = "derived getter over Loaded",
            ["Unit"] = "derived getter over Loaded",
        };
        PropertyInfo[] members = typeof(CanvasPublication).GetProperties(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        var names = members.Select(member => member.Name).ToHashSet(StringComparer.Ordinal);
        foreach (PropertyInfo member in members)
        {
            Assert.True(
                manifest.ContainsKey(member.Name),
                $"CanvasPublication.{member.Name} is not in the carry-forward "
                + "manifest; a new member is a DECISION about the rebase, not a "
                + "silent field.");
        }

        foreach (string name in manifest.Keys)
        {
            Assert.True(
                names.Contains(name),
                $"the manifest names {name}, which the publication no longer has.");
        }

        // The static and field walls, so the total table cannot be
        // walked around: NO static properties — a cached instance is
        // exactly the shape obligation I5 forbids, and Seed is a METHOD
        // that allocates per call, which the sentinel facts pin — and
        // the only fields are the compiler's own property backings.
        string[] statics = [.. typeof(CanvasPublication)
            .GetProperties(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .Select(member => member.Name)];
        Assert.True(
            statics.Length == 0,
            $"the publication grew static properties [{string.Join(", ", statics)}]; "
            + "a cached instance is exactly the shape obligation I5 forbids.");
        string[] bareFields = [.. typeof(CanvasPublication)
            .GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public
                | BindingFlags.NonPublic)
            .Select(field => field.Name)
            .Where(name => !name.StartsWith('<'))];
        Assert.True(
            bareFields.Length == 0,
            $"the publication declares bare fields [{string.Join(", ", bareFields)}]; "
            + "every member goes through the manifest's properties.");
    }

    // ---------------------------------------------------------------
    // The authority census, and its five-shape premise
    // ---------------------------------------------------------------

    /// <summary>The record's dispositions, keyed the way the derivation
    /// reports: an authority is where the mutable state LIVES, and the
    /// published values reach mutability only through the lease they
    /// name — naming it is the currency fact (U5), and the lease's own
    /// state is dispositioned on the lease.</summary>
    private static readonly Dictionary<string, string> AuthorityDispositions =
        new(StringComparer.Ordinal)
        {
            ["CanvasPublicationSlot._current"] = "THE one currency authority (U1)",
            ["CanvasPublicationSlot._publishing"] = "operational — the reentrancy refusal (T1)",
            ["CanvasPublicationSlot._observer"] = "instrument (T1)",
            ["CanvasPublicationInstallObserver._seen"] = "instrument (T1)",
            ["CanvasPublicationInstallObserver.Installs"] = "instrument counter (T1)",
            ["CanvasPublicationInstallObserver.RepeatedInstalls"] = "instrument counter (T1)",
            ["CanvasLoaded.Lease"] = "the chain NAMING the lease — the currency fact itself (U5); the lease's own mutable state is dispositioned on the lease",
            ["CanvasPublication.CommittedUnpresented"] = "published VALUE, not a live authority — an immutable operation id (§E TE-2, IE-10); written at a failed refresh and cleared by the refresh-only recovery, both through Publish on the one currency authority",
            ["CanvasHandleLease._closed"] = "the close-once record, non-currency operational (T2)",
            ["CanvasHandleLease._close"] = "source-free closing capability (T2)",
            ["CanvasLoadProbeForTests.OnPoint"] = "instrument, test seam by name (T3)",
            ["CanvasFilterProbeForTests.OnPoint"] = "instrument, test seam by name (T6)",
            ["CanvasLoadPipeline._slot"] = "the writer's handle to the one authority (T3)",
            ["CanvasLoadPipeline._source"] = "source-free FFI seam (T3)",
            ["CanvasLoadPipeline._onReseeded"] = "source-free effect hand-off (T6)",
            ["CanvasLoadPipeline._probe"] = "instrument (T3)",
            ["CanvasFilterMachine._slot"] = "the writer's handle to the one authority (T6)",
            ["CanvasFilterMachine._source"] = "source-free FFI seam (T6)",
            ["CanvasFilterMachine._run"] = "source-free runner (T6)",
            ["CanvasFilterMachine._probe"] = "instrument (T6)",
        };

    /// <summary>The derivation against the dispositions, TWO-WAY: an
    /// undispositioned authority fails, and so does a disposition whose
    /// authority is gone — the ledger cannot drift from the code in
    /// either direction.</summary>
    [Fact]
    public void EveryAuthorityTheDerivationFindsIsDispositioned()
    {
        var found = ModelTypes
            .SelectMany(Derive)
            .Select(authority => authority.Location)
            .ToHashSet(StringComparer.Ordinal);
        List<string> undispositioned = [.. found.Except(AuthorityDispositions.Keys)];
        List<string> stale = [.. AuthorityDispositions.Keys.Except(found)];
        Assert.True(
            undispositioned.Count == 0,
            "authorities with no disposition:\n  " + string.Join("\n  ", undispositioned));
        Assert.True(
            stale.Count == 0,
            "dispositions whose authority is gone:\n  " + string.Join("\n  ", stale));
    }

    /// <summary>The premise, because this predicate has been wrong
    /// twice on the way here: the five shapes the design's probes found
    /// are planted in one type, and the derivation must find all
    /// five.</summary>
    [Fact]
    public void TheDerivationFindsAllFivePlantedShapes()
    {
        var found = Derive(typeof(PlantedShapes))
            .Select(authority => authority.Location)
            .ToHashSet(StringComparer.Ordinal);
        string[] expected =
        [
            "PlantedShapes.MutableStatic",
            "PlantedShapes.Planted",
            "PlantedShapes.AutoProperty",
            "PlantedShapes._contents",
            "PlantedShapes._facade",
        ];
        foreach (string shape in expected)
        {
            Assert.True(
                found.Contains(shape),
                $"the derivation missed the planted shape {shape} — the predicate "
                + "has gone wrong a third time.");
        }
    }

    private readonly record struct Authority(string Location, string Kind);

    /// <summary>The derivation: every field of a type — auto-property
    /// backing fields and event backing delegates included, statics
    /// included — classified as an IDENTITY authority when it can be
    /// reassigned, a CONTENTS authority when it is readonly over
    /// something that can move (delegates and interfaces included, by
    /// round 5's corrected predicate), and nothing when it is a lock or
    /// a published value.</summary>
    private static IEnumerable<Authority> Derive(Type type)
    {
        foreach (FieldInfo field in AllFields(type))
        {
            string location = $"{type.Name}.{Normalize(field.Name)}";
            if (!field.IsInitOnly)
            {
                yield return new Authority(location, "identity");
                continue;
            }

            if (IsLock(field) || IsValueLike(field.FieldType))
            {
                continue;
            }

            yield return new Authority(location, "contents");
        }
    }

    private static IEnumerable<FieldInfo> AllFields(Type type) =>
        type.GetFields(
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public
                | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

    private static string Normalize(string fieldName) =>
        fieldName.StartsWith('<') ? fieldName[1..fieldName.IndexOf('>')] : fieldName;

    /// <summary>A lock is a readonly bare object, and the model names
    /// its locks two ways — the derivation's first run taught this
    /// predicate the second one.</summary>
    private static bool IsLock(FieldInfo field) =>
        field.FieldType == typeof(object)
        && (Normalize(field.Name).Contains("gate", StringComparison.OrdinalIgnoreCase)
            || Normalize(field.Name).EndsWith("Lock", StringComparison.Ordinal));

    /// <summary>The published-value whitelist: immutable by their own
    /// scans — this census walks each of them and the two-way check
    /// proves it finds no authorities there — reaching mutability only
    /// through the lease they NAME.</summary>
    private static bool IsValueLike(Type type)
    {
        Type target = Nullable.GetUnderlyingType(type) ?? type;
        if (target.IsPrimitive || target.IsEnum
            || target == typeof(string) || target == typeof(decimal))
        {
            return true;
        }

        if (target.IsGenericType)
        {
            Type generic = target.GetGenericTypeDefinition();
            if (generic == typeof(ImmutableArray<>)
                || generic == typeof(ImmutableDictionary<,>)
                || generic == typeof(ImmutableHashSet<>))
            {
                return true;
            }
        }

        return target == typeof(CanvasPublication)
            || target == typeof(CanvasLoaded)
            || target == typeof(CanvasLoadSchedule)
            || target == typeof(CanvasFilterSchedule)
            || target == typeof(CanvasRequestIdentity)
            || target == typeof(CanvasPopulation)
            || target == typeof(CanvasProjectionUnit)
            || target == typeof(CanvasLoadFailure)
            || target == typeof(CanvasPublicationOutcome);
    }

    /// <summary>Round 5's five shapes, planted — the premise that keeps
    /// the predicate honest, because it has been wrong twice: a mutable
    /// static, an event, a settable auto-property, a readonly field
    /// with mutable contents, and the read-only interface over a
    /// caller-retained list that passed the four-shape battery.</summary>
#pragma warning disable CS0067, CS0414, IDE0051, IDE0052
    private sealed class PlantedShapes(List<string> retained)
    {
        public static int MutableStatic = 1;

        private readonly List<string> _contents = [];

        private readonly IReadOnlyList<string> _facade = retained;

        public event EventHandler? Planted;

        public int AutoProperty { get; set; }
    }
#pragma warning restore CS0067, CS0414, IDE0051, IDE0052
}
