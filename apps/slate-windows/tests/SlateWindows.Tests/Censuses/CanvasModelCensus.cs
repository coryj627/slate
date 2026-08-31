// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Immutable;
using System.Reflection;
using System.Text.RegularExpressions;
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
/// THE CLOSED WORLD is the model's nine files, named in one list this
/// class owns: a file added to the model without joining the list is
/// invisible to every source arm. The reflection arms walk the same
/// world as TYPES, so the model has two independent enumerations of
/// itself and a member can hide from neither.
/// </para>
/// <para>
/// WHAT THESE ARMS NARROW, said once: the writer arm censuses ORDINARY
/// SOURCE WRITES — reflection, unsafe accessors and serializer
/// hydration are beyond a source census, prohibited by nothing but the
/// closed world (no model type opts into serialization, which one arm
/// asserts) and caught at runtime by the slot's own SlotBypassed
/// detector. The purity arm walks each transform's OWN body, not its
/// transitive call graph: a transform's callees are the model's pure
/// surface, and the compiler-grade walk is the not-taken alternative
/// recorded with obligation I4. The aliasing arm is a wall, not an
/// owned row type, and obligation I7's note records that too.
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
        typeof(CanvasPublicationInstallObserver),
        typeof(CanvasPublicationOutcome),
        typeof(CanvasHandleLease),
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

    private static string ModelRoot =>
        Path.Combine(SourceText.ShellSourceRoot(), "Canvas");

    private static string Read(string file) =>
        File.ReadAllText(Path.Combine(ModelRoot, file));

    private static IEnumerable<(string File, string Text)> ModelSources() =>
        ModelFiles.Select(file => (file, Read(file)));

    // ---------------------------------------------------------------
    // Obligation I8 — the publication-writer arm
    // ---------------------------------------------------------------

    /// <summary>One writable field holds a publication anywhere in the
    /// model, and one method writes it: the constructor's seed and the
    /// single compare-and-swap. The token appears in no other model
    /// file, so "no other write" is a claim about the closed world
    /// rather than about the lines somebody remembered. This is the
    /// census T1's bypass detector waited for — the branch is
    /// structural now, not defensive.</summary>
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

        string slot = Read("CanvasPublicationSlot.cs");
        Assert.True(
            CountOf(slot, "_current = ") == 1,
            "the seed assignment is no longer exactly one.");
        Assert.True(
            CountOf(slot, "Interlocked.CompareExchange(ref _current") == 1,
            "the compare-and-swap is no longer exactly one.");
        foreach ((string file, string text) in ModelSources())
        {
            Assert.True(
                file == "CanvasPublicationSlot.cs"
                    || !text.Contains("_current", StringComparison.Ordinal),
                $"{file} mentions the slot's field.");
        }
    }

    /// <summary>No model type opts into serialization: hydration is one
    /// of obligation I8's named bypasses, and the closed world's answer
    /// is that nothing here invites it.</summary>
    [Fact]
    public void NoModelTypeOptsIntoSerialization()
    {
        foreach (Type type in ModelTypes)
        {
            foreach (object attribute in type.GetCustomAttributes(inherit: false))
            {
                string name = attribute.GetType().FullName!;
                Assert.True(
                    !name.Contains("Serial", StringComparison.Ordinal)
                        && !name.Contains("DataContract", StringComparison.Ordinal)
                        && !name.Contains("Json", StringComparison.Ordinal),
                    $"{type.Name} carries a serialization attribute: {name}.");
            }
        }
    }

    /// <summary>The minting walls — the call-site half of "the writer
    /// set is closed by construction": a lease is minted only by the
    /// pipeline, a population only by the pipeline and its own Empty,
    /// and the chain and terminal transforms are reached only by their
    /// owners.</summary>
    [Theory]
    [InlineData("new CanvasHandleLease(", "CanvasLoadPipeline.cs")]
    [InlineData("new CanvasPopulation(", "CanvasLoadPipeline.cs|CanvasPopulation.cs")]
    [InlineData(".WithLoaded(", "CanvasLeaseTransfer.cs")]
    [InlineData(".WithUnit(", "CanvasPublication.cs|CanvasLeaseTransfer.cs|CanvasFilterMachine.cs")]
    [InlineData(".WithTerminal(", "CanvasLeaseTransfer.cs")]
    [InlineData(".WithUnloaded(", "CanvasLoadPipeline.cs")]
    public void TheMintingWallsHold(string token, string allowedFiles)
    {
        string[] allowed = allowedFiles.Split('|');
        var offenders = new List<string>();
        var found = 0;
        foreach (string path in Directory.EnumerateFiles(
            SourceText.ShellSourceRoot(), "*.cs", SearchOption.AllDirectories))
        {
            int count = CountOf(File.ReadAllText(path), token);
            if (count == 0)
            {
                continue;
            }

            found += count;
            string file = Path.GetFileName(path);
            if (!allowed.Contains(file))
            {
                offenders.Add($"{file} ({count})");
            }
        }

        Assert.True(found > 0, $"the wall for {token} guards nothing — did the name move?");
        Assert.True(
            offenders.Count == 0,
            $"{token} is reached from outside its wall: {string.Join(", ", offenders)}.");
    }

    // ---------------------------------------------------------------
    // Obligation I4 — the transform-purity arm
    // ---------------------------------------------------------------

    /// <summary>Every transform handed to the gate, walked: no session,
    /// no FFI, no clock, no scheduler, no announcer, no lease close, no
    /// live UI state — the executable purity predicate round 7 asked
    /// for, at source level. The probes are the one sanctioned callout
    /// and are stripped by name, because they are dispositioned
    /// instruments production never constructs.</summary>
    /// <remarks>
    /// One level deep on purpose, and obligation I4's ledger note says
    /// so: a transform's callees are the model's own pure surface —
    /// transforms and resolvers over immutable values — and the
    /// compiler-grade transitive walk is the not-taken alternative.
    /// </remarks>
    [Fact]
    public void EveryPublishedTransformIsPureAtItsOwnLevel()
    {
        string[] forbidden =
        [
            "_session", "DateTime", "Stopwatch", "Environment.", "Task.",
            "Thread", "StartWork", "Post(", "Speak(", "_announcer",
            ".Close(", ".Invoke(", "Random", "File.", "Console.",
            "_machine", "_pipeline", "Selection.", "_filterText",
            "_applied", "_rows", "_outline", "Interlocked.", "Volatile.",
            "lock (", "_source", ".Match(",
        ];
        string[] files = [.. ModelFiles, "CanvasDocumentViewModel.cs"];
        var sites = 0;
        var offences = new List<string>();
        foreach (string file in files)
        {
            string text = File.ReadAllText(Path.Combine(ModelRoot, file));
            foreach ((int line, string body) in TransformBodies(text))
            {
                sites++;
                string cleaned = string.Join(
                    "\n",
                    body.Split('\n')
                        .Where(bodyLine =>
                            !bodyLine.Contains("?.Reached(", StringComparison.Ordinal))
                        .Select(bodyLine =>
                        {
                            int comment = bodyLine.IndexOf("//", StringComparison.Ordinal);
                            return comment < 0 ? bodyLine : bodyLine[..comment];
                        }));
                foreach (string token in forbidden)
                {
                    if (cleaned.Contains(token, StringComparison.Ordinal))
                    {
                        offences.Add($"{file}:{line} uses '{token}'");
                    }
                }
            }
        }

        Assert.True(
            sites >= 10,
            $"only {sites} transforms found; the arm would be scanning almost "
            + "nothing — did the publish helper move?");
        Assert.True(
            offences.Count == 0,
            "a transform reaches outside its snapshot:\n  "
            + string.Join("\n  ", offences));
    }

    /// <summary>The argument region of every <c>.Publish(</c> call, by
    /// balanced parentheses; the caller strips comments and the
    /// sanctioned probe lines.</summary>
    private static IEnumerable<(int Line, string Body)> TransformBodies(string text)
    {
        var index = 0;
        while ((index = text.IndexOf(".Publish(", index, StringComparison.Ordinal)) >= 0)
        {
            int line = text[..index].Count(character => character == '\n') + 1;
            int start = index + ".Publish".Length;
            var depth = 0;
            int end = start;
            for (; end < text.Length; end++)
            {
                if (text[end] == '(')
                {
                    depth++;
                }
                else if (text[end] == ')' && --depth == 0)
                {
                    break;
                }
            }

            yield return (line, text[start..end]);
            index = end;
        }
    }

    // ---------------------------------------------------------------
    // Obligation I7 — the aliasing analyzer, structural half
    // ---------------------------------------------------------------

    /// <summary>Nothing in the shell writes INTO a row's group path —
    /// the one mutable field a core row carries, and the alias T2's
    /// re-materialisation closed from the construction side. This wall
    /// is the consumer side; the owned-row alternative was not taken,
    /// and obligation I7's note records the narrowing.</summary>
    [Fact]
    public void NothingWritesIntoARowsGroupPath()
    {
        var offenders = new List<string>();
        var reads = 0;
        foreach (string path in Directory.EnumerateFiles(
            SourceText.ShellSourceRoot(), "*.cs", SearchOption.AllDirectories))
        {
            string text = File.ReadAllText(path);
            reads += CountOf(text, "GroupPath[");
            foreach (Match write in Regex.Matches(text, @"GroupPath\[[^\]]*\]\s*=(?!=)"))
            {
                offenders.Add($"{Path.GetFileName(path)}: {write.Value.Trim()}");
            }
        }

        Assert.True(
            reads >= 2,
            $"the wall guards {reads} group-path indexings — did the member move?");
        Assert.True(
            offenders.Count == 0,
            "a group path is written through a retained row: "
            + string.Join("; ", offenders));
    }

    /// <summary>Rows are re-materialised at ONE site: the group-path
    /// copy exists only in the model-copy helper, so a reviewer greps
    /// one file to know every place an array enters the model's
    /// ownership.</summary>
    [Fact]
    public void RowsAreRematerialisedAtOneSite()
    {
        foreach (string path in Directory.EnumerateFiles(
            SourceText.ShellSourceRoot(), "*.cs", SearchOption.AllDirectories))
        {
            int count = CountOf(File.ReadAllText(path), "with { GroupPath");
            if (Path.GetFileName(path) == "CanvasModelCopy.cs")
            {
                Assert.True(
                    count == 2,
                    $"the helper re-materialises {count} row kinds; the model's two "
                    + "are the outline's and the table's.");
            }
            else
            {
                Assert.True(
                    count == 0,
                    $"{Path.GetFileName(path)} re-materialises rows outside the one "
                    + "construction site.");
            }
        }
    }

    /// <summary>Immutable collections are BUILT in two files only — the
    /// copy helper and the population's own indexes — which keeps
    /// "collections enter the model through the helper" a greppable
    /// claim rather than a habit.</summary>
    [Fact]
    public void CollectionsAreBuiltAtTheSanctionedSites()
    {
        string[] tokens =
        [
            "ToImmutable", "CreateBuilder", "CreateRange",
            "ImmutableArray.Create", "ImmutableHashSet.Create",
        ];
        foreach ((string file, string text) in ModelSources())
        {
            if (file is "CanvasModelCopy.cs" or "CanvasPopulation.cs")
            {
                continue;
            }

            foreach (string token in tokens)
            {
                Assert.True(
                    !text.Contains(token, StringComparison.Ordinal),
                    $"{file} builds a collection with {token} outside the "
                    + "sanctioned sites.");
            }
        }
    }

    // ---------------------------------------------------------------
    // U4's carry-forward manifest
    // ---------------------------------------------------------------

    /// <summary>Every publication member classified, as a TOTAL table
    /// keyed by reflection: a new member fails here BY NAME until the
    /// acceptance's carry-forward accounts for it — the derivation U4
    /// demands instead of a list that can go short, which was round 5's
    /// blocker 5 and is a gate now.</summary>
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

    private static int CountOf(string text, string token)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += token.Length;
        }

        return count;
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
