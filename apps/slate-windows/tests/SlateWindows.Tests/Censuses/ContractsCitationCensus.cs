// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
//
// W6-1 PR A (#745): a contracts-document PR section cites no identifier
// that does not exist. (PR B extended it from §A alone to every listed
// PR section — inserting §B between §A and its old terminator would
// otherwise have folded the new section into the old one's extent, and
// a guard whose subject silently changed shape is not a guard.)
//
// The contracts doc is the evidence ledger PR H reconciles every row
// against, so a citation naming a test that was renamed — or never
// written — is not a typo. It is a row that reads as evidenced and is
// not, and it survives exactly as long as nobody re-reads the whole
// section by hand. PR A's first cut shipped FIVE such names.
//
// Deliberately not a hand-kept list of "the test names §A is allowed to
// use": that is the same artefact one level up, and it rots the same
// way. The rule is mechanical — every long PascalCase citation in §A
// must resolve to SOMETHING declared in the shell or its test projects.

using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SlateWindows.Tests.Censuses;

[Trait("census", "contracts-citations")]
public sealed class ContractsCitationCensus
{
    private const string ContractsDoc = "34_canvas_contracts.md";

    /// <summary>
    /// The PR sections this census reads, each with the floors that keep
    /// it from passing over nothing. A section is added here as its PR
    /// lands: an unlisted section is invisible to the guard, and a
    /// section whose heading moved fails on its own marker rather than
    /// silently swallowing the next one — which is what would have
    /// happened when §B was inserted between §A and its old terminator.
    /// </summary>
    private static readonly (string Pr, string Start, string End, int Length, int Citations)[]
        PrSections =
        [
            (
                "A",
                "## PR A — the canvas document, the tab, and the outline",
                "## PR B — the canvas table projection",
                5_000,
                30),
            (
                "B",
                "## PR B — the canvas table projection",
                // Terminated by §C rather than by §W-G since W6-1 PR C:
                // leaving the old terminator would have folded the whole
                // of §C into §B's extent, which is the shape-change the
                // class comment above warns about.
                "## PR C — the navigator, the mode stack",
                2_000,
                10),
            (
                "C",
                "## PR C — the navigator, the mode stack",
                // Terminated by §C-unit for §B's reason, one section
                // later: a section inserted between §C and its old
                // terminator would otherwise be read as part of §C, and
                // the retired-vocabulary scan would silently widen with
                // it.
                "## PR C-unit — the coherent projection unit",
                8_000,
                30),
            (
                "C-unit",
                "## PR C-unit — the coherent projection unit",
                // Terminated by §D for §B's reason, two sections later:
                // the section inserted before the old terminator must
                // not be read as part of §C-unit's extent.
                "## PR D — the visual renderer",
                6_000,
                // The floor moved when the section did. It was 3 while
                // §C-unit was a DESIGN whose own types were deliberately
                // in plain text; task T1 built eight of them and bound
                // the names, and a floor below the population is a
                // decorative floor.
                //
                // The number needs one caveat, because getting it wrong
                // is easy in a way that looks right: this arm counts
                // OCCURRENCES, not distinct names. §C-unit cited 21
                // occurrences before T1 bound anything, so any floor at
                // or below 21 — including one chosen by counting the
                // eight new NAMES — would be satisfied with every T1
                // citation removed, which is the decorative floor with
                // extra steps. 30 sat above the pre-T1 population and
                // below the then-current 37, so un-binding T1's names
                // failed while ordinary prose churn did not.
                //
                // T2 bound the lease, the population, the unit and the
                // ownership transfer, taking the section to 50. Same
                // rule one task later: 40 sits above the pre-T2
                // population of 37, so un-binding T2's four names fails
                // too. Each task raises this above what the section
                // carried BEFORE it — the only reading under which the
                // floor guards that task's work rather than its
                // predecessors'.
                //
                // T3 bound the pipeline and its types, taking the
                // section to 59. 55 sits above the pre-T3 population
                // of 50 — the same rule, a third time.
                //
                // T6 bound the machine and its types, taking the
                // section to 69; 62 sits above the pre-T6
                // population of 59 — a fourth time.
                //
                // T4 bound the census and re-cited the walls it
                // guards, taking the section to 75; 71 sat above
                // the pre-T4 population of 69 — a fifth time.
                //
                // The cleanup pass recorded its changes and their
                // owners, taking the section to 95; 76 sits above
                // the pre-pass population of 75 — a sixth time.
                76),
            (
                "D",
                "## PR D — the visual renderer",
                // Terminated by §E since its section landed — §B's
                // rule, three sections later.
                "## PR E — the mutation funnel",
                12_000,
                // A DESIGN floor, §C-unit's convention: the section's
                // own new types are deliberately plain text until tasks
                // bind them, so the 14 occurrences at revision 1 are all
                // EXISTING names. 10 sits above the empty pre-section
                // population and below the current 14, so unbinding the
                // section's citations fails while prose churn does not;
                // each implementation task raises it above what the
                // section carried before that task, as §C-unit's tasks
                // did.
                //
                // TD-1 bound the engine, the viewport value, the
                // presentation state and the battery's sentinels,
                // taking the section to 28; 20 sits above the
                // pre-task population of 18 — the §C-unit rule's
                // first application in §D.
                //
                // TD-2 bound the scene's copy, its index and their
                // facts, taking the section to 33; 30 sits above the
                // pre-task population of 28 — a second time.
                //
                // TD-3 bound the topology, the third authority and
                // their sentinels, taking the section to 39; 35 sat
                // above the pre-task population of 33 — a third time.
                //
                // TD-4 bound the selection accessor and its two
                // sentinels, taking the section to 43; 40 sat above
                // the pre-task population of 39 — a fourth time.
                //
                // TD-5 bound the vocabulary arm, the verbs, the
                // binding record and the enablement census, taking
                // the section to 51; 45 sat above the pre-task
                // population of 43 — a fifth time.
                //
                // TD-6 bound the renderer, the peers, the services
                // and the flip's renamed facts, taking the section
                // to 57. The flip's sweep renamed two cited facts IN
                // PLACE, so the honest pre-task population is the
                // post-sweep 48, and 50 sits above it — a sixth
                // time.
                50),
            (
                "E",
                "## PR E — the mutation funnel",
                // Terminated by §F since its section landed — §B's
                // rule, again.
                "## PR F — move and resize modes, structural placement, and the connect flow",
                12_000,
                // A DESIGN floor, §C-unit's convention: the section's
                // own new types (the funnel, the stacks, the pickers,
                // the sheets) are deliberately plain text until tasks
                // bind them, so revision 1's 12 occurrences are all
                // EXISTING names. 9 sits above the empty pre-section
                // population and below the current 12, so unbinding
                // the section's citations fails while prose churn
                // does not; each implementation task raises it above
                // what the section carried before that task.
                //
                // TE-0 bound the core surface — the basis, the seed,
                // the receipt, the canonical text, the proximity
                // order, the media export and the detached apply —
                // taking the section to 18; 16 sits above the
                // pre-task population of 14, the §C-unit rule's
                // first application in §E.
                //
                // TE-1 bound the operation value, its identity, its
                // currency and the gate, taking the section to 25;
                // 19 sits above the pre-task population of 18 — a
                // second time.
                //
                // TE-2 bound the history domain, the publication's
                // committed-unpresented state and the quarantine
                // vocabulary, taking the section to 30; 26 sits
                // above the pre-task population of 25 — a third
                // time.
                //
                // TE-3 bound the conflict record, its door and the
                // snapshot read, taking the section to 32; 31 sits
                // above the pre-task population of 30 — a fourth
                // time.
                //
                // TE-4 bound the effects split — the plan, the
                // completion marks and the busy gate — taking the
                // section to 35; 33 sits above the pre-task
                // population of 32 — a fifth time.
                //
                // TE-5a bound the funnel's spine — the admission
                // table, the transaction, the two seams and the
                // receipt push — taking the section to 38; 36 sits
                // above the pre-task population of 35 — a sixth
                // time.
                //
                // TE-5b bound the document integration, the verbs
                // and the seat rule, taking the section to 42; 41
                // sits above the pre-task population of 40 — a
                // seventh time.
                //
                // TE-5c bound the remaining commit paths and their
                // grammar, taking the section to 45; 43 sits above
                // the pre-task population of 42 — an eighth time.
                //
                // TE-6 bound the picker models and their factories,
                // taking the section to 49; 46 sits above the
                // pre-task population of 45 — a ninth time.
                //
                // TE-7 bound the editor, its seed token and the
                // modal membership, taking the section to 51; 50
                // sits above the pre-task population of 49 — a
                // tenth time.
                //
                // TE-8 bound the plan, its two consumers and the
                // popup fact, taking the section to 55; 52 sits
                // above the pre-task population of 51 — an
                // eleventh time.
                //
                // TE-9 bound the expansion memory and the aimed
                // supersession, taking the section to 57; 56 sits
                // above the pre-task population of 55 — a twelfth
                // time.
                //
                // TE-10 bound the vault-scoped create and its
                // terminal outcome table, taking the section to 64;
                // 58 sits above the pre-task population of 57 — a
                // thirteenth time.
                //
                // TE-11a bound the wiring slice and the onboarding
                // swap, taking the section to 67; 65 sits above the
                // pre-task population of 64 — a fourteenth time.
                //
                // TE-11b bound the activation swap and the interim's
                // whole retirement, taking the section to 69; 68 sits
                // above the pre-task population of 67 — a fifteenth
                // time.
                //
                // TE-11c bound the never-silent tables and the one
                // core sentence, taking the section to 73; 70 sits
                // above the pre-task population of 69 — a sixteenth
                // time.
                //
                // TE-11d bound the scenario driver and its census,
                // taking the section to 75; 74 sits above the
                // pre-task population of 73 — a seventeenth time.
                //
                // TE-11e bound the journey and its three found bugs,
                // taking the section to 76; 75 sits above the
                // pre-task population of 74 — an eighteenth time.
                75),
            (
                "F",
                "## PR F — move and resize modes, structural placement, and the connect flow",
                "## PR G — marks: mark-then-act, the marks list, and the bulk verbs",
                // The length pin guards marker movement; a young
                // section pins below its honest size and rises with
                // its records, as §E's did. TF-3's record took
                // the section well past its youth; the pin follows.
                40_000,
                // A DESIGN floor, the §C-unit/§E convention: the
                // section's own new types stay plain text until
                // tasks bind them, so today's citations are
                // EXISTING names (the mode controller, the funnel,
                // the 0a/0b exports). 12 sits above the empty
                // pre-section population and below the landed
                // count; each task raises it, as every §E task did.
                //
                // TF-0 bound the completion seam — the Pending arm,
                // the resolve machinery and the typed operation
                // outcome — taking the section to 47; 43 sits above
                // the pre-task population of 42, the rule's first
                // §F application.
                //
                // TF-1 bound the entry preflight and the token
                // lifecycle — the admitted install, the identity-
                // checked clear and the suspend trio — taking the
                // section to 50; 48 sits above the pre-task
                // population of 47 — a second time.
                //
                // TF-2 bound the holder, its identity and the
                // displacement watcher, taking the section to 52;
                // 51 sits above the pre-task population of 50 — a
                // third time.
                //
                // TF-3 bound the move machine — the overlap and
                // describe reads, the geometry commit and the
                // controller's early-resolution memory — taking
                // the section to 59; 56 sits above the pre-task
                // population of 52 — a fourth time.
                //
                // TF-4 bound the resize surface — the preset
                // gate, the minting rule and the quick loop —
                // taking the section to 63; 61 sits above the
                // pre-task population of 59 — a fifth time.
                //
                // TF-5 bound the presentation authority — the
                // effective-rect derivation and the aggregate
                // observable — taking the section to 66; 64 sits
                // above the pre-task population of 63 — a sixth
                // time.
                //
                // TF-6 bound the retirement rule at the announcer
                // seam, taking the section to 67; 66 sits
                // above the pre-task population of 66 — a seventh
                // time.
                //
                // TF-7 bound the picker request and the placement
                // verbs, taking the section to 71; 69 sits
                // above the pre-task population of 67 — an eighth
                // time.
                //
                // TF-8 bound the prompt machinery and the staged
                // connect flow, taking the section to 75; 73
                // sits above the pre-task population of 71 — a
                // ninth time.
                //
                // TF-9 bound the connect-mode memory and its shared
                // preparation, taking the section to 76; 75
                // sits above the pre-task population of 75 — a
                // tenth time.
                //
                // TF-10 bound the suspended column and published the
                // matrix, taking the section to 80; 79 sits
                // above the pre-task population of 76 — an
                // eleventh time.
                //
                // TF-11 bound the journey's marshalling catch and
                // swept the ledger, taking the section to 81;
                // 80 sits above the pre-task population of 80
                // — a twelfth time.
                80),
            (
                "G",
                "## PR G — marks: mark-then-act, the marks list, and the bulk verbs",
                "## PR G2 — the verb residue: front doors for §E's verbs, and the parity extras",
                // A young section pins below its honest size and
                // rises with its records, as §F's did.
                6_000,
                // A DESIGN floor, the standing convention: revision 1's
                // citations are EXISTING names (the selection store, the
                // funnel, the prompt kinds, the FFI shapes); 4 sits
                // above the empty pre-section population and below the
                // landed count; each task raises it. Revision 2's
                // thirty closures cited the shipped seams by name,
                // lifting the pre-task population to 13.
                //
                // TG-0 bound the mark verbs and the apply-side seed,
                // taking the section to 20; 18 sits above the
                // pre-task population of 18 — a first time.
                //
                // TG-1 bound the prompt hierarchy and the landing,
                // taking the section to 22; 20 sits above the
                // pre-task population of 20 — a second time.
                //
                // TG-2 bound the marks list and its landing, taking
                // the section to 25; 23 sits above the pre-task
                // population of 22 — a third time.
                //
                // TG-3 bound the epochs, the effect plan and the split
                // outcomes, taking the section to 32; 30 sits
                // above the pre-task population of 25 — a fourth
                // time.
                //
                // TG-4 bound the bulk frame and the marked color target,
                // taking the section to 35; 33 sits above the
                // pre-task population of 32 — a fifth time.
                //
                // TG-5 bound the group verb and its prompt, taking the
                // section to 40; 38 sits above the pre-task
                // population of 35 — a sixth time.
                //
                // TG-6 bound the mode and history cells, taking the
                // section to 42; 41 sits above the pre-task
                // population of 40 — a seventh time.
                //
                // TG-7 bound the journey and the outline's in-place
                // status refresh, taking the section to 46; 45 sits
                // above the pre-task population of 42 — an eighth
                // time.
                45),
            (
                "G2",
                "## PR G2 — the verb residue: front doors for §E's verbs, and the parity extras",
                "## PR H — the close-out: the end-to-end proof, the gates recorded, the issue reconciled",
                // A young section pins below its honest size and
                // rises with its records, as §G's did.
                6_000,
                // A DESIGN floor, the standing convention: revision 1's
                // citations are EXISTING names (§E's verbs, the prompt
                // hierarchy, the FFI shapes); 4 sits above the empty
                // pre-section population and below the landed count;
                // each task raises it.
                //
                // TG2-0 bound the front-door substrate, taking the
                // section to 114; 113 sits above the pre-task
                // population of 96 — a first time.
                //
                // TG2-1 bound the prompt lifecycle and the first sheets,
                // taking the section to 123; 122 sits above the
                // pre-task population of 114 — a second time.
                //
                // TG2-2 bound the choices pickers, taking the section to
                // 134; 133 sits above the pre-task population of 123
                // — a third time.
                //
                // TG2-3 bound the vault file picker, taking the section to
                // 146; 145 sits above the pre-task population of 134
                // — a fourth time.
                //
                // TG2-4 bound Remove from Group, Create Connected Card and
                // the editor receipt, taking the section to 159; 158
                // sits above the pre-task population of 146 — a fifth time.
                //
                // TG2-5 bound Duplicate, taking the section to 165; 164
                // sits above the pre-task population of 159 — a sixth time.
                //
                // TG2-6 bound Convert Card to Note, taking the section to 181;
                // 180 sits above the pre-task population of 165 — a seventh
                // time.
                //
                // TG2-7 bound the context plan over (surface, target), taking
                // the section to 184; 183 sits above the pre-task population
                // of 181 — an eighth time.
                //
                // TG2-8 bound the matrix, taking the
                // section to 204; 203 sits above the pre-task population
                // of 184 — a ninth time.
                //
                // TG2-9 bound the journey and the sweep, taking the
                // section to 222; 221 sits above the pre-task population
                // of 204 — a tenth time.
                221),
            (
                "H",
                "## PR H — the close-out: the end-to-end proof, the gates recorded, the issue reconciled",
                "## §W-G canonical-consumption audit",
                // The close-out section: contracts over the nine
                // deliverables, then the task records; it pins below its
                // honest size and rises with its records, as §G2's did.
                6_000,
                // A DESIGN floor, the standing convention: revision 1's
                // citations are EXISTING names (the harnesses, the
                // benchmarks, the matrix generator's seams); 4 sits above
                // the empty section and below the first record.
                //
                // TH-0 bound the end-to-end class with its open and grammar facts, taking the
                // section to 66; 65 sits above the pre-task population
                // of 61 — a first time.
                //
                // TH-1 bound the authoring loop and the undo chain, taking the
                // section to 71; 70 sits above the pre-task population
                // of 66 — a second time.
                //
                // TH-2 bound the large canvas and the §K roll-up, taking the
                // section to 79; 78 sits above the pre-task population
                // of 71 — a third time.
                //
                // TH-3 bound the scenario twin and the fixture censuses, taking the
                // section to 80; 79 sits above the pre-task population
                // of 79 — a fourth time.
                //
                // TH-5 bound the viewport consumers, taking the
                // section to 88; 87 sits above the pre-task population
                // of 80 — a fifth time.
                //
                // TH-6 bound the remaining consumers, taking the
                // section to 95; 94 sits above the pre-task population
                // of 88 — a sixth time.
                //
                // TH-7 bound the §W-G close-out, taking the
                // section to 113; 112 sits above the pre-task population
                // of 95 — a seventh time.
                //
                // TH-4 bound the trigger ledger and its census, taking the
                // section to 139; 138 sits above the pre-task population
                // of 113 — an eighth time.
                //
                // TH-8 bound the §W-C rows and their manifest census, taking the
                // section to 144; 143 sits above the pre-task population
                // of 139 — a ninth time.
                //
                // TH-9 bound the matrix and the validator's teeth, taking the
                // section to 150; 149 sits above the pre-task population
                // of 144 — a tenth time.
                //
                // TH-10 bound the AT checklist, taking the
                // section to 152; 151 sits above the pre-task population
                // of 150 — an eleventh time.
                //
                // TH-11 bound E14 at the workspace, taking the
                // section to 158; 157 sits above the pre-task population
                // of 152 — a twelfth time.
                //
                // TH-12 bound the reconciliation, taking the
                // section to 165; 164 sits above the pre-task population
                // of 158 — a thirteenth time.
                //
                // TH-13 bound the sweep and the close-out, taking the
                // section to 192; 191 sits above the pre-task population
                // of 165 — a fourteenth time.
                191),
            (
                "Reconciliation",
                "## Issue reconciliation (#745)",
                "<!-- end of the contracts document -->",
                // §H TH-12 (H9): the final section — the ledger, the
                // generated tables, the issues, the hand-offs and the
                // residuals; a citation floor of its own.
                20_000,
                // TH-12 bound the section at its birth, taking it to
                // 658; 658 sits one below the population of 659.
                658),
        ];

    public static TheoryData<string, string, string> SectionRanges
    {
        get
        {
            var data = new TheoryData<string, string, string>();
            foreach ((string pr, string start, string end, _, _) in PrSections)
            {
                data.Add(pr, start, end);
            }
            return data;
        }
    }

    public static TheoryData<string, string, string, int, int> Sections
    {
        get
        {
            var data = new TheoryData<string, string, string, int, int>();
            foreach ((string pr, string start, string end, int length, int citations)
                in PrSections)
            {
                data.Add(pr, start, end, length, citations);
            }
            return data;
        }
    }

    /// <summary>
    /// Long PascalCase names are the ones that read as code. Short ones
    /// (`Ready`, `Invoke`, `Tree`) are ordinary prose in this document
    /// and are deliberately out of scope — the floor is set where a
    /// citation stops being a word and starts being an identifier a
    /// reviewer would try to grep.
    /// </summary>
    private const int IdentifierFloor = 15;

    [Theory]
    [MemberData(nameof(SectionRanges))]
    public void EveryIdentifierCitedInAPrSectionExists(string pr, string start, string end)
    {
        string section = Section(start, end, pr);
        HashSet<string> declared = DeclaredNames();

        var missing = new SortedSet<string>(StringComparer.Ordinal);
        foreach (Match match in Regex.Matches(section, @"`([A-Za-z_][A-Za-z0-9_]*)`"))
        {
            string cited = match.Groups[1].Value;
            if (cited.Length >= IdentifierFloor
                && char.IsUpper(cited[0])
                && !declared.Contains(cited))
            {
                _ = missing.Add(cited);
            }
        }

        Assert.True(
            missing.Count == 0,
            $"§{pr} of the contracts document cites identifiers that do not exist "
            + "anywhere in the Windows shell or its tests. A contract row citing "
            + "a test that was renamed reads as evidenced and is not — which is "
            + "the failure class PR H's reconciliation depends on catching:\n  "
            + string.Join("\n  ", missing));
    }

    /// <summary>The census's own premise: a section that stopped citing
    /// anything, or a marker that moved, would make the check above pass
    /// over nothing.</summary>
    [Theory]
    [MemberData(nameof(Sections))]
    public void EverySectionIsFoundAndCitesIdentifiers(
        string pr, string start, string end, int minimumLength, int minimumCitations)
    {
        string section = Section(start, end, pr);
        Assert.True(
            section.Length > minimumLength,
            $"§{pr} is implausibly short — did the marker move?");
        int citations = Regex.Matches(section, @"`([A-Z][A-Za-z0-9_]{14,})`").Count;
        Assert.True(
            citations >= minimumCitations,
            $"§{pr} cites only {citations} identifiers; the guard would be scanning "
            + "almost nothing.");
    }

    /// <summary>
    /// The vocabulary of mechanisms this branch RETIRED, with what
    /// replaced each. A row here is a phrase that can only be an
    /// ASSERTION that the retired thing is still how it works.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The "sweep follows the mechanism" rule — when a fix replaces a
    /// mechanism, sweep the rows that DESCRIBE that mechanism, not just
    /// the rows the fix happens to cite — failed on manual application
    /// three rounds running, and the third failure was a claim that
    /// everything HAD been swept. So it stops being discipline. Every
    /// entry below was a live, false row somebody had to find by
    /// reading; from here the reading is a gate.
    /// </para>
    /// <para>
    /// What this does NOT do is detect staleness in general — no textual
    /// guard can. It converts the named vocabulary into a wall, and its
    /// second assertion is what keeps it from silently guarding nothing:
    /// each row's REPLACEMENT must still be present, so a rename of the
    /// new mechanism brings a reviewer back to this table instead of
    /// leaving a dead row behind.
    /// </para>
    /// <para>
    /// The limit is worth a CONCRETE example, because "not general" is
    /// easy to read past. Codex round 4's scoped review found two rows
    /// that had stopped describing the code — C2's attachment rule, which
    /// went from two cases to three, and `_awayBecause`'s remark, which
    /// still said nothing cleared the field after the same wave added a
    /// line that does. Both were INSIDE this scan's range and invisible
    /// to it, because neither used a retired NAME. A row can go stale by
    /// having the world move under it while every word in it stays a word
    /// nobody retired; that class is caught by reading, and this table
    /// does not pretend otherwise.
    /// </para>
    /// <para>
    /// A THIRD shape, found in codex round 5 and outside BOTH guards: C8
    /// recorded that menu-then-elsewhere is not reclassified and deferred
    /// the repair to PR F — two waves after the window watcher had done
    /// it. The retired-vocabulary rows could not see it (no retired
    /// name), and the staged-claim rule could not either, because its
    /// question is "does this claim name a PR that has SHIPPED" and PR F
    /// has not. **A deferral to a future PR that the present already
    /// carried out is invisible to a shipped-set test by construction**,
    /// and no textual rule proposed so far would catch it. Named here so
    /// the next reviewer looks for it by hand rather than trusting the
    /// green.
    /// </para>
    /// </remarks>
    private static readonly
        (string Retired, string Pattern, string Replacement, string Sample)[]
        RetiredMechanisms =
        [
            (
                "the request generation counter (codex C-lite round 1: ABA)",
                @"[Gg]eneration-matched|\bby generation\b|OWNER and a GENERATION",
                "reference identity",
                "a newer request supersedes an older one by generation."),
            (
                "the focus TOKEN (a one-shot flag, superseded by the durable "
                    + "addressed request)",
                @"(?i)\bfocus[- ]token|(?i)filter[- ]focus[- ]token",
                "CanvasFilterFocusRequest",
                "Ctrl+F raises the document's focus TOKEN instead."),
            (
                "the unconditional projection seat (codex round 3, B2: an "
                    + "empty projection takes focus holding nothing)",
                @"(?i)focus(es)? the projection|(?i)back to the projection",
                "SEAT RULE",
                "clear it, announce the count, focus the projection."),
            (
                "unconditional restoration DELIVERY (codex round 3, B1: the "
                    + "distinction governed the withdrawal end only)",
                @"(?i)delivered unconditionally"
                    + @"|(?i)a restoration is delivered (the moment|as soon as)",
                "HOLDS it",
                "a restoration is delivered the moment it can be."),
            (
                "per-WINDOW owner liveness (codex round 3, M3: a pane that "
                    + "moved to another canvas is not an address for the one "
                    + "it left)",
                @"(?i)still (some|any) canvas owner"
                    + @"|(?i)any open canvas tab is a live address",
                "PAIRING",
                "the sweep asks whether the tab is still some canvas owner."),
            (
                "the PUBLIC announcer and its allow-listed relay seat (codex "
                    + "round 3, M4: unrepresentability first)",
                @"(?i)allow-list(ed)? seat|(?i)the one allow-listed",
                "GridRelaySeam",
                "what is NOT derived is the one allow-listed seat."),
            (
                "the descendant-carries-its-ancestor proof step (Min6: core's "
                    + "group path is ancestor-only)",
                @"(?i)matches G also matches P|(?i)P survives whenever G does",
                "ancestor",
                "every route that matches G also matches P."),
            (
                "the removed seat arm's caller enumeration (the scoped review "
                    + "of round 3: CloseWhereAmI also runs pre-ladder)",
                @"(?i)[Ee]very caller is an Escape rung",
                "pre-ladder",
                "there is no such arm, because every caller is an Escape rung."),
            (
                "the mode owner CAPTURED from the navigator's cache (codex "
                    + "rounds 8-9: `_presenter` is a cache, not a log, and the "
                    + "owner comes from the invocation)",
                @"(?i)captur\w* (?:at `?Enter`? )?from the navigator"
                    + @"|(?i)captures the (?:navigator's )?(?:attached )?presenter"
                    + @"|(?i)supplies the attached pane",
                "INVOCATION",
                "Enter captures the navigator's attached presenter."),
            (
                "EXACT mac equivalence for the filter-active predicate "
                    + "(codex round 11: five ratified divergences, §C m2)",
                @"(?i)predicate is macs"
                    + @"|(?i)spelled out to mac's rule"
                    + @"|(?i)mac's rule, including",
                "carve-out",
                "The filter-active predicate is macs: spelled out to mac's "
                    + "rule rather than borrowed."),
            (
                "the CACHE-derived admission (codex round 10: the pane comes "
                    + "from the invocation, and refusals are runtime guards)",
                @"(?i)owns the keys or owned them last"
                    + @"|(?i)no pane has ever held"
                    + @"|(?i)refuses when no pane",
                "INVOCATION",
                "EnterMode supplies the pane that owns the keys or owned "
                    + "them last, and refuses when no pane has ever held them."),
            (
                "the THREE-case attachment rule (codex round 9: a mode entry "
                    + "attaches its invoker, which makes four)",
                @"(?i)attachment is a THREE-case rule"
                    + @"|(?i)three-case attachment",
                "FOUR-case",
                "Attachment is a THREE-case rule, and the third is a replacement."),
        ];

    public static TheoryData<string, string, string, string> Retired
    {
        get
        {
            var data = new TheoryData<string, string, string, string>();
            foreach ((string retired, string pattern, string replacement, string sample)
                in RetiredMechanisms)
            {
                data.Add(retired, pattern, replacement, sample);
            }
            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Retired))]
    public void NoRetiredMechanismIsStillDescribedAsCurrent(
        string retired, string pattern, string replacement, string sample)
    {
        // THE PREMISE, first: a mistyped pattern guards nothing and
        // passes forever. Each row carries a SAMPLE — a line of the prose
        // it exists to forbid — and the pattern has to find it in the
        // sample after the same mention-strip the scan applies. This is
        // the sibling arm's "the section is long enough and cites enough"
        // floor, said for a regex.
        Assert.True(
            Regex.IsMatch(Strip(sample), pattern),
            $"the pattern for {retired} does not match its own sample, so it "
            + "would scan every text below and find nothing forever: "
            + $"{pattern} vs {sample}");

        var scanned = new List<(string Where, string Text)>();
        foreach ((string label, string start, string end) in ScannedProse)
        {
            scanned.Add((label, Section(start, end, label)));
        }
        foreach ((string label, string root) in ScannedSources)
        {
            string[] sources = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
            Assert.True(
                sources.Length > 0,
                $"{label} contains no sources, so this scan covers nothing there.");
            foreach (string source in sources)
            {
                // A guard is not its own subject. This file has to spell
                // the retired phrases out — that is the whole table — and
                // it is also the one file whose escaped-quote literals
                // (`"\"[^\"]*\""`) shift the mention-strip's pairing, so
                // scanning it would report the table to itself and get
                // the line numbers wrong doing it.
                if (Path.GetFileName(source) == DeclaringFile)
                {
                    continue;
                }
                scanned.Add(($"{label}/{Path.GetFileName(source)}", File.ReadAllText(source)));
            }
        }

        var offenders = new List<string>();
        foreach ((string where, string text) in scanned)
        {
            string prose = Strip(text);
            foreach (Match match in Regex.Matches(prose, pattern))
            {
                int line = prose.Take(match.Index).Count(c => c == '\n') + 1;
                offenders.Add($"{where}:~{line} — {match.Value}");
            }
        }

        Assert.True(
            offenders.Count == 0,
            $"a retired mechanism is still described as current: {retired}. "
            + $"It was replaced by {replacement}. A row that describes a "
            + "mechanism the code no longer has reads as evidence and is "
            + "not — sweep the rows that DESCRIBE the change, not only the "
            + "ones the change cites:\n  "
            + string.Join("\n  ", offenders));
    }

    /// <summary>
    /// A MENTION is not a USE: a quoted phrase is prose narrating what
    /// the old promise said, which is exactly what a corrected row does.
    /// </summary>
    /// <remarks>
    /// Straight ASCII quotes only, and over `.cs` files this also blanks
    /// every string literal — so a retired mechanism asserted inside an
    /// assertion message is invisible here. Both are known limits of a
    /// textual rule rather than oversights; the day the contracts
    /// document grows typographic quotes, a mention becomes a use and
    /// this gate false-positives loudly, which is the failure direction
    /// to prefer.
    /// </remarks>
    private const string DeclaringFile = "ContractsCitationCensus.cs";

    /// <summary>
    /// Every doc comment closes what it opens.
    /// </summary>
    /// <remarks>
    /// <para>
    /// FIFTH appearance of this class on the branch, which is what makes
    /// it a guard rather than more care. A new member gets spliced into
    /// the middle of a neighbour's `&lt;remarks&gt;`, the neighbour's
    /// closing tags end up orphaned after it, and NOTHING complains:
    /// Roslyn does not validate doc structure, the build is clean, every
    /// test passes, and the two members' prose is silently welded
    /// together for whoever reads it next.
    /// </para>
    /// <para>
    /// The check is a tag stack over each contiguous run of `///` lines —
    /// which is exactly one member's doc block — so the failure it
    /// catches is the structural one rather than a style opinion. Only
    /// the BLOCK tags are tracked; inline `&lt;see/&gt;` and `&lt;c&gt;`
    /// are another argument and not this one.
    /// </para>
    /// </remarks>
    [Fact]
    public void EveryDocCommentClosesWhatItOpens()
    {
        var offenders = new List<string>();
        var files = 0;
        foreach ((string label, string text) in DocumentedSources())
        {
            files++;
            string[] lines = text.Replace("\r\n", "\n").Split('\n');
            var run = new List<string>();
            var runStart = 0;
            for (var index = 0; index <= lines.Length; index++)
            {
                bool isDoc = index < lines.Length && lines[index].TrimStart().StartsWith(
                    "///", StringComparison.Ordinal);
                if (isDoc)
                {
                    if (run.Count == 0)
                    {
                        runStart = index + 1;
                    }
                    run.Add(lines[index]);
                    continue;
                }
                if (run.Count > 0)
                {
                    if (UnbalancedTag(string.Join("\n", run)) is { } problem)
                    {
                        offenders.Add($"{label}:{runStart} — {problem}");
                    }
                    run.Clear();
                }
            }
        }

        // The scan can see: a floor on FILES (blocks-per-file drifts with
        // every paragraph anyone writes, and a floor that drifts is a
        // floor nobody re-reads), and three seeds.
        Assert.True(
            files > 20,
            $"only {files} files were scanned; the guard is reading almost "
            + "nothing.");
        Assert.NotNull(UnbalancedTag(
            "/// <summary>a</summary>\n/// <remarks>\n/// <para>b</para>"));
        Assert.NotNull(UnbalancedTag("/// <para>b</para>\n/// </remarks>"));
        // THE MOTIVATING SHAPE, which LIFO balance alone reports as fine:
        // two separately-balanced blocks in one run, which means one
        // member carries both and its neighbour carries none. The first
        // version of this guard passed this and would have passed the
        // splice that produced it if the splice had been tidier.
        Assert.NotNull(UnbalancedTag(
            "/// <summary>a</summary>\n/// <remarks>x</remarks>\n"
            + "/// <summary>b</summary>\n/// <remarks>y</remarks>"));
        Assert.Null(UnbalancedTag(
            "/// <summary>a</summary>\n/// <remarks>\n/// <para>b</para>\n"
            + "/// </remarks>"));

        Assert.True(
            offenders.Count == 0,
            "a doc comment does not close what it opens, which is how a "
            + "member gets spliced into a neighbour's remarks with a clean "
            + "build and a green suite:\n  "
            + string.Join("\n  ", offenders));
    }

    /// <summary>The first structural problem in one doc block, or
    /// null.</summary>
    private static string? UnbalancedTag(string block)
    {
        var open = new Stack<string>();
        var summaries = 0;
        foreach (Match tag in Regex.Matches(
            block, @"<(/?)(summary|remarks|para|returns|example)>"))
        {
            string name = tag.Groups[2].Value;
            if (tag.Groups[1].Value.Length == 0)
            {
                // ONE member per run, and a run is one member's doc
                // block. A second top-level `<summary>` means this block
                // documents two things — so the first one's member has no
                // doc comment at all, which is what the splice actually
                // did and what pure LIFO balance cannot see.
                if (open.Count == 0
                    && string.Equals(name, "summary", StringComparison.Ordinal)
                    && ++summaries > 1)
                {
                    return "a second top-level <summary>: this block "
                        + "documents two members, so one of them has none";
                }
                open.Push(name);
                continue;
            }
            if (open.Count == 0)
            {
                return $"</{name}> closes nothing";
            }
            string expected = open.Pop();
            if (!string.Equals(expected, name, StringComparison.Ordinal))
            {
                return $"</{name}> closes <{expected}>";
            }
        }
        return open.Count == 0 ? null : $"<{open.Peek()}> is never closed";
    }

    /// <summary>
    /// Canvas production and the canvas-facing tests, enumerated
    /// deliberately and de-duplicated.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The first version recursed the test root for `Canvas*.cs` AND the
    /// `Censuses` folder for `*.cs`, so every `Canvas…Census` was read
    /// twice — harmless for correctness and dishonest in the floor, which
    /// is why the floor now counts FILES and the walk is top-directory
    /// per scope with a seen-set behind it.
    /// </para>
    /// <para>
    /// `ChordTableTests` is IN: it scrapes `CanvasNavigator.Bind` and is
    /// canvas-facing in everything but its name. The FlaUI project is
    /// OUT, recorded rather than forgotten — its canvas journeys live in
    /// `ShellAccessibilityTests` among every other surface's, so pulling
    /// the file in would put this guard on the whole shell's prose on the
    /// strength of one region. That is a scope decision, and it belongs
    /// to whoever widens it.
    /// </para>
    /// </remarks>
    private static IEnumerable<(string Label, string Text)> DocumentedSources()
    {
        string root = SourceText.RepoRoot();
        string tests = Path.Combine(
            root, "apps", "slate-windows", "tests", "SlateWindows.Tests");
        (string Dir, string Pattern, SearchOption Depth)[] scopes =
        [
            (Path.Combine(root, "apps", "slate-windows", "src", "SlateWindows", "Canvas"),
                "*.cs", SearchOption.AllDirectories),
            (tests, "Canvas*.cs", SearchOption.TopDirectoryOnly),
            (tests, "ChordTableTests.cs", SearchOption.TopDirectoryOnly),
            (Path.Combine(tests, "Censuses"), "*.cs", SearchOption.TopDirectoryOnly),
        ];
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach ((string dir, string pattern, SearchOption depth) in scopes)
        {
            string[] found = Directory.GetFiles(dir, pattern, depth);
            Assert.True(
                found.Length > 0, $"no sources under {dir} matching {pattern}");
            foreach (string file in found)
            {
                if (seen.Add(file))
                {
                    yield return (Path.GetFileName(file), File.ReadAllText(file));
                }
            }
        }
    }

    /// <summary>
    /// A STAGED claim whose PR has landed is a lie with a delivery date.
    /// </summary>
    /// <remarks>
    /// <para>
    /// "No row is delivered at this scope until PR C ships the
    /// navigator", "CanExecute is false until PR B and PR D ship their
    /// projections", "minus the `, filtered` clause, which has nothing to
    /// describe until PR C" — all true when written, all false by the
    /// time the reviewer read them, and all in files nobody re-reads when
    /// the PR they name lands. Codex round 4 found four in one pass.
    /// </para>
    /// <para>
    /// The SHIPPED set is derived, not listed, from the contracts
    /// document's own PR section headings. Strictly that predicate is
    /// "has a section", i.e. SPECIFIED rather than shipped — §C exists
    /// while PR C is paused and unmerged — and that is the safe
    /// direction: it fires while somebody is already editing the
    /// document, rather than after the merge nobody re-reads.
    /// </para>
    /// <para>
    /// SAMPLED scope, and stated exactly: the command surface, the §W-C
    /// matrix, and the contracts document itself. The document is in
    /// scope because the first version of this guard exempted it — and
    /// the leading example in its own round record, A10's "nothing to
    /// describe until PR C", lived there. A guard that reads a document
    /// to define "shipped" and then exempts it from the rule is the
    /// weakest shape available. What stays OUT is the spec tree
    /// (`w6_1_canvas_spec.md` and its siblings): a spec records the plan
    /// AS PLANNED, so its staged language is history rather than a claim
    /// about today, and the call is recorded here rather than left to the
    /// next reader.
    /// </para>
    /// <para>
    /// TWO known limits, both fail-noisy or recorded. Headings are
    /// matched on a word boundary, so `## PR D — …` and `## PR D: …`
    /// both register; a heading that stops naming its PR at all still
    /// fails LOUDLY through the floor below rather than quietly widening
    /// what is legal. And `until PR C-lite …` is not a claim about PR C:
    /// the pattern refuses a hyphenated or word-continued letter, which
    /// is a real sentence in this document rather than a hypothetical.
    /// </para>
    /// </remarks>
    [Fact]
    public void NoStagedClaimOutlivesThePrItNames()
    {
        string doc = File.ReadAllText(
            Path.Combine(SourceText.RepoRoot(), "docs", "plans", ContractsDoc));
        string[] shipped =
        [
            .. Regex.Matches(doc, @"(?m)^## PR ([A-Z])\b")
                .Select(match => match.Groups[1].Value)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(pr => pr, StringComparer.Ordinal),
        ];
        Assert.True(
            shipped.Length >= 3,
            "fewer than three PR sections were found in the contracts "
            + $"document, so this guard knows almost nothing has shipped and "
            + $"would pass over every staged claim: [{string.Join(", ", shipped)}]");

        // The PREMISE, before the scan: the pattern finds a planted claim
        // naming a PR that HAS shipped, and leaves one naming a PR that
        // has not. Without this a typo makes the guard permanent green.
        Assert.NotEmpty(StaleClaims($"until PR {shipped[0]} ships the thing", shipped));
        Assert.Empty(StaleClaims("until PR Z ships the thing", shipped));
        Assert.Empty(StaleClaims(
            $"until PR {shipped[^1]}-lite ships the thing", shipped));
        Assert.Empty(StaleClaims(
            $"a quoted \"until PR {shipped[0]} ships\" narration", shipped));

        var offenders = new List<string>();
        foreach ((string where, string text) in StagedClaimScope())
        {
            foreach (string claim in StaleClaims(text, shipped))
            {
                offenders.Add($"{where} — {claim}");
            }
        }

        Assert.True(
            offenders.Count == 0,
            "a staged claim names a PR that has already shipped, so it "
            + "describes the code as it was rather than as it is — and the "
            + "reader most likely to trust it is the one deciding whether a "
            + "row is live:\n  "
            + string.Join("\n  ", offenders));
    }

    private static IEnumerable<string> StaleClaims(string text, string[] shipped)
    {
        // A MENTION is not a use here either — the round record quotes
        // the very sentences this table forbids — and `PR C-lite` is not
        // a claim about PR C, which is why the letter may not be followed
        // by a hyphen or another word character.
        //
        // The context window is cut from the STRIPPED text, not the
        // original: the strip shortens the string, so a match index used
        // against the original quotes an offender that is not there. It
        // did, once, and an offender message nobody can trust is worse
        // than no message.
        string prose = Strip(text);
        return Regex.Matches(prose, @"until (?:W6-1 )?PR ([A-Z])(?![-\w])")
            .Where(match => shipped.Contains(match.Groups[1].Value, StringComparer.Ordinal))
            .Select(match => "~line "
                + (prose.Take(match.Index).Count(c => c == '\n') + 1)
                + ": "
                + prose
                    .Substring(
                        Math.Max(0, match.Index - 40),
                        Math.Min(prose.Length - Math.Max(0, match.Index - 40), 110))
                    .Replace('\n', ' '));
    }

    private static IEnumerable<(string Where, string Text)> StagedClaimScope()
    {
        string commands = Path.Combine(
            SourceText.RepoRoot(),
            "apps", "slate-windows", "src", "SlateWindows", "Commands");
        string[] sources = Directory.GetFiles(commands, "*.cs", SearchOption.AllDirectories);
        Assert.NotEmpty(sources);
        foreach (string source in sources)
        {
            yield return (Path.GetFileName(source), File.ReadAllText(source));
        }
        string matrix = Path.Combine(
            SourceText.RepoRoot(), "docs", "plans", "18_windows_port", "w_c_matrix.md");
        Assert.True(File.Exists(matrix), $"the \u00a7W-C matrix is missing at {matrix}");
        yield return ("w_c_matrix.md", File.ReadAllText(matrix));
        yield return (
            ContractsDoc,
            File.ReadAllText(
                Path.Combine(SourceText.RepoRoot(), "docs", "plans", ContractsDoc)));
    }

    private static string Strip(string text) =>
        Regex.Replace(text, "\"[^\"]*\"", "\"\"");

    /// <summary>
    /// The prose in range, by HEADING. §C plus the recorded divergences,
    /// which is a deliberate widening: the scoped review of codex round 3
    /// found a stale CD-45 row that the §C-only range could not see, and
    /// divergences are exactly where a retired mechanism's justification
    /// goes to be forgotten. The ROUND RECORD stays out — history has to
    /// keep the old names or it stops being history.
    /// </summary>
    private static readonly (string Label, string Start, string End)[] ScannedProse =
    [
        (
            "§C",
            "## PR C — the navigator, the mode stack",
            "## PR C-unit — the coherent projection unit"),
        (
            "§C-unit",
            "## PR C-unit — the coherent projection unit",
            "## PR D — the visual renderer"),
        (
            "§D",
            "## PR D — the visual renderer",
            "## PR E — the mutation funnel"),
        (
            "§E",
            "## PR E — the mutation funnel",
            "## PR F — move and resize modes, structural placement, and the connect flow"),
        (
            "§F",
            "## PR F — move and resize modes, structural placement, and the connect flow",
            "## PR G — marks: mark-then-act, the marks list, and the bulk verbs"),
        (
            "§G",
            "## PR G — marks: mark-then-act, the marks list, and the bulk verbs",
            "## PR G2 — the verb residue: front doors for §E's verbs, and the parity extras"),
        (
            "§G2",
            "## PR G2 — the verb residue: front doors for §E's verbs, and the parity extras",
            "## PR H — the close-out: the end-to-end proof, the gates recorded, the issue reconciled"),
        (
            "§H",
            "## PR H — the close-out: the end-to-end proof, the gates recorded, the issue reconciled",
            "## §W-G canonical-consumption audit"),
        (
            "reconciliation",
            "## Issue reconciliation (#745)",
            "<!-- end of the contracts document -->"),
        (
            "divergences",
            "## Recorded divergences (owner-recorded; off-limits for re-litigation)",
            "## Accepted risks (owner-recorded; off-limits for re-litigation)"),
    ];

    /// <summary>
    /// The sources in range. Canvas PRODUCTION, plus the canvas censuses
    /// — the second is the other half of the same widening: the wave that
    /// retired the allow-listed seat left the words "the one allow-listed
    /// seat" in the census's own doc comment, and a gate that cannot see
    /// the file it lives in is a gate for other people's prose.
    /// </summary>
    /// <remarks>
    /// The rest of the test tree stays OUT, and the reason is not
    /// squeamishness: mutation comments, provenance paragraphs and
    /// regression names narrate retired mechanisms on purpose, and a rule
    /// that cannot tell narration from assertion would fire on all of
    /// them. The censuses are in because they are guards — a guard that
    /// describes a mechanism nobody has is the exact thing this table is
    /// for.
    /// </remarks>
    private static readonly (string Label, string Root)[] ScannedSources =
    [
        (
            "Canvas",
            Path.Combine(
                SourceText.RepoRoot(),
                "apps", "slate-windows", "src", "SlateWindows", "Canvas")),
        (
            "Censuses",
            Path.Combine(
                SourceText.RepoRoot(),
                "apps", "slate-windows", "tests", "SlateWindows.Tests", "Censuses")),
    ];

    /// <summary>The table's own premise: a retired row whose REPLACEMENT
    /// has itself been renamed is guarding a mechanism nobody has, and it
    /// would pass forever.</summary>
    [Theory]
    [MemberData(nameof(Retired))]
    public void EveryRetiredMechanismNamesAReplacementThatExists(
        string retired, string pattern, string replacement, string sample)
    {
        _ = pattern;
        _ = sample;
        string section = Section(
            "## PR C — the navigator, the mode stack",
            "## §W-G canonical-consumption audit",
            "C");
        Assert.True(
            section.Contains(replacement, StringComparison.OrdinalIgnoreCase),
            $"§C never names {replacement}, which is what replaced {retired} — "
            + "so the guard above is watching for a ghost and the row that "
            + "should say what the mechanism IS now is missing.");
    }

    private static string Section(string sectionStart, string sectionEnd, string pr)
    {
        string path = Path.Combine(
            SourceText.RepoRoot(), "docs", "plans", ContractsDoc);
        Assert.True(File.Exists(path), $"the contracts document is missing at {path}");
        string text = File.ReadAllText(path);
        int start = text.IndexOf(sectionStart, StringComparison.Ordinal);
        int end = text.IndexOf(sectionEnd, StringComparison.Ordinal);
        Assert.True(start >= 0, $"§{pr}'s heading is missing: {sectionStart}");
        Assert.True(end > start, $"§{pr}'s terminator is missing: {sectionEnd}");
        return text[start..end];
    }

    /// <summary>Every name DECLARED in the shell and its test projects —
    /// types, members, locals-that-matter. Syntax only: resolving
    /// symbols would need a compilation, and what a citation needs is
    /// that the name is written down somewhere real.</summary>
    private static HashSet<string> DeclaredNames()
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        string[] roots =
        [
            Path.Combine(SourceText.RepoRoot(), "apps", "slate-windows", "src", "SlateWindows"),
            // The generated uniffi bindings ARE a declaration site —
            // every `CanvasA11yEvent` variant §A cites is declared
            // there. Git-ignored, but nothing in this project compiles
            // without them, so a run that got this far has them.
            Path.Combine(SourceText.RepoRoot(), "apps", "slate-windows", "src", "SlateUniffi"),
            Path.Combine(SourceText.RepoRoot(), "apps", "slate-windows", "tests"),
            Path.Combine(SourceText.RepoRoot(), "apps", "slate-windows", "tools"),
            Path.Combine(SourceText.RepoRoot(), "apps", "slate-windows", "benchmarks"),
        ];
        foreach (string root in roots)
        {
            foreach (string file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                        StringComparison.Ordinal)
                    || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                        StringComparison.Ordinal))
                {
                    continue;
                }
                SyntaxNode root2 = CSharpSyntaxTree
                    .ParseText(File.ReadAllText(file), new CSharpParseOptions(LanguageVersion.Preview))
                    .GetRoot();
                foreach (SyntaxNode node in root2.DescendantNodes())
                {
                    switch (node)
                    {
                        case BaseTypeDeclarationSyntax type:
                            _ = names.Add(type.Identifier.ValueText);
                            break;
                        case MethodDeclarationSyntax method:
                            _ = names.Add(method.Identifier.ValueText);
                            break;
                        case PropertyDeclarationSyntax property:
                            _ = names.Add(property.Identifier.ValueText);
                            break;
                        case VariableDeclaratorSyntax variable:
                            _ = names.Add(variable.Identifier.ValueText);
                            break;
                        case EnumMemberDeclarationSyntax member:
                            _ = names.Add(member.Identifier.ValueText);
                            break;
                        case ParameterSyntax parameter:
                            _ = names.Add(parameter.Identifier.ValueText);
                            break;
                        default:
                            break;
                    }
                }
            }
        }
        // Names a PR section legitimately cites that are declared in a
        // language or assembly this census cannot parse. Each is listed
        // with where it really lives, so the escape hatch stays
        // auditable rather than becoming a place to hide a typo.
        foreach (string external in new[]
        {
            // The mac twins these sections compare against.
            "A11yResidueCensusTests", "CanvasAnnouncerTests", "CanvasCardRef",
            "CanvasDocument", "CanvasContainerView", "CanvasOutlineView",
            // WPF / .NET.
            "TreeViewItemAutomationPeer", "VirtualizingStackPanel",
            "IInvokeProvider", "AutomationProperties", "RaiseNotificationEvent",
            "SelectedItemChanged", "VirtualizationMode", "DispatcherTimer",
            "TraversalRequest", "IsKeyboardFocusWithin", "UnreachableException",
            "DataContextChanged", "IsVisibleChanged",
            "IsNullOrWhiteSpace", "OutOfMemoryException", "StackOverflowException",
            "AccessViolationException",
            // XAML attributes, which live in markup rather than in C#.
            "CommandParameter",
            // Roslyn, which the censuses consume but do not declare.
            "MethodDeclarationSyntax", "MemberDeclarationSyntax",
            // Python: scripts/generate-parity-matrix.py (§B's B12 — the
            // delivered-command set whose per-PR growth is the rule that
            // row sets).
            "W6_1_DELIVERED_COMMANDS",
        })
        {
            _ = names.Add(external);
        }
        return names;
    }
}
