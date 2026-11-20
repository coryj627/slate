// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.IO;
using System.Text;
using System.Xml;

namespace SlateWindows;

/// <summary>Why <see cref="SvgPreviewGuard"/> refused a document.</summary>
internal enum SvgRefusal
{
    None,

    /// <summary>A DOCTYPE, entity, script, foreignObject or <c>@import</c>,
    /// or an xml-stylesheet processing instruction.</summary>
    Markup,

    /// <summary>An href that leaves the document: anything but
    /// <c>#id</c> or <c>data:image/</c>.</summary>
    ExternalReference,

    /// <summary>An feImage that draws another element of the document.</summary>
    ElementImage,

    /// <summary>More <c>url(</c> references in one value or declaration
    /// than <see cref="SvgPreviewGuard.MaxUrlReferencesPerValue"/>.</summary>
    UrlReferences,

    /// <summary>Brackets nested deeper than
    /// <see cref="SvgPreviewGuard.MaxValueNesting"/>.</summary>
    ValueNesting,

    /// <summary>A chain of nesting and references longer than
    /// <see cref="SvgPreviewGuard.MaxRenderDepth"/>.</summary>
    RenderDepth,

    /// <summary>More element renders than
    /// <see cref="SvgPreviewGuard.MaxRenderedElements"/>.</summary>
    RenderedElements,

    /// <summary>An element that reaches itself through references.</summary>
    ReferenceCycle,

    /// <summary>Not well-formed XML, or over the size cap.</summary>
    Malformed,

    /// <summary>#1610 (CB-29): an estimated decode longer than
    /// <see cref="SvgPreviewGuard.MaxEstimatedDecodeMilliseconds"/>
    /// (<see cref="SvgDecodeEstimate"/>), for a document every other check
    /// admits.</summary>
    DecodeCost,
}

/// <summary>
/// #1610 (CB-29): what Svg.Custom 5.2.3 and Svg.Skia 5.2.3 spend on the four
/// costs that make an SVG slow to decode, in microseconds per unit. CB-29's
/// step 0 measured each on one desktop (an AMD Ryzen 7 9800X3D, .NET 10); the
/// calibration is committed in
/// <c>docs/plans/47_windows_core_closeout/calibration/cb29/</c>, and CB-29's
/// spec names each rate.
/// </summary>
internal static class SvgDecodeRates
{
    /// <summary>A stylesheet selector branch's query walks every element of
    /// the document: per branch, per pass, per element.</summary>
    internal const double QueryPerBranchElement = 0.0935;

    /// <summary>A branch's descendant steps, per unit of the follow-up's
    /// depth bound (each element times the ways to pick 1 to K of its
    /// ancestors; fitted 0.05985).</summary>
    internal const double DescendantChainUnit = 0.060;

    /// <summary>A branch's general-sibling steps, per sibling-list read
    /// (fitted 0.01437).</summary>
    internal const double SiblingListRead = 0.0144;

    /// <summary>An of-type pseudo-class, per element filed under its
    /// compound's key times its sibling-list size (fitted 0.01993).</summary>
    internal const double OfTypeSiblingRead = 0.0199;

    /// <summary>A declaration applied to an element a rule matched.</summary>
    internal const double MatchedDeclaration = 1.03;

    /// <summary>A matched custom-property definition, or a matched
    /// declaration whose value holds <c>var()</c>.</summary>
    internal const double MatchedCustomDeclaration = 0.5;

    /// <summary>Each rule of the stylesheet, parsed on its own.</summary>
    internal const double Rule = 4.5;

    /// <summary>Under any <c>&lt;style&gt;</c>, each <c>&lt;use&gt;</c>
    /// render re-applies the stylesheet: per use render, per element of the
    /// document and per <see cref="UseRenderRuleWeight"/> elements a rule
    /// costs.</summary>
    internal const double UseRenderElement = 0.2465;

    /// <summary>What one rule weighs in a use render, in elements.</summary>
    internal const double UseRenderRuleWeight = 15;

    /// <summary>Under any <c>&lt;style&gt;</c>, each element of a use's
    /// instance, per use render.</summary>
    internal const double UseInstanceElement = 22.2;

    /// <summary>What each rule adds to <see cref="UseInstanceElement"/>.</summary>
    internal const double UseInstanceElementRule = 0.0666;

    /// <summary>Each lookup a <c>var()</c> resolution makes.</summary>
    internal const double VarCopy = 0.192;

    /// <summary>A marker unit: a placement's marker renders and its
    /// <see cref="MarkerPlacementWeight"/>, or a marked element's widest
    /// marker.</summary>
    internal const double MarkerUnit = 2.548;

    /// <summary>What one marker placement weighs beyond its marker's
    /// renders.</summary>
    internal const double MarkerPlacementWeight = 20;
}

/// <summary>#1610 (CB-29): the decode time <see cref="SvgPreviewGuard"/>
/// estimates for a document, one term per cost, in microseconds at
/// <see cref="SvgDecodeRates"/>.</summary>
internal readonly record struct SvgDecodeEstimate(
    double StylesheetMicroseconds,
    double UseMicroseconds,
    double VarMicroseconds,
    double MarkerMicroseconds)
{
    internal double Microseconds =>
        StylesheetMicroseconds + UseMicroseconds + VarMicroseconds + MarkerMicroseconds;

    internal double Milliseconds => Microseconds / 1000;

    /// <summary>Whether the guard spent its step budget before it finished
    /// counting (#1610, the red team's B1): the document is refused for its
    /// cost whatever the terms say.</summary>
    internal bool StepsSpent { get; init; }

    /// <summary>The steps the check took, out of
    /// <see cref="SvgPreviewGuard.MaxGuardSteps"/>.</summary>
    internal long Steps { get; init; }

    /// <summary>The largest term's name, for the log: <c>stylesheet</c>,
    /// <c>use</c>, <c>var</c> or <c>marker</c>, or <c>guard</c> when the
    /// guard spent its step budget.</summary>
    internal string LargestTerm
    {
        get
        {
            if (StepsSpent)
            {
                return "guard";
            }
            (string Name, double Value)[] terms =
            [
                ("stylesheet", StylesheetMicroseconds),
                ("use", UseMicroseconds),
                ("var", VarMicroseconds),
                ("marker", MarkerMicroseconds),
            ];
            return terms.MaxBy(term => term.Value).Name;
        }
    }
}

/// <summary>
/// What an SVG preview may hand Svg.Skia (D4). Rendering walks a document
/// recursively: through its nesting, through every element it reaches by
/// reference (<c>href</c>, <c>url(#id)</c>, a filter input), through each
/// <c>url()</c> fallback of a paint and through nested CSS. The decode
/// runs on a thread with a large stack
/// (<c>EditorInteractionCoordinator.DecodeSvgImage</c>); this guard
/// bounds the depth that stack must hold and the work a document can
/// expand to, reading the document with an XmlReader BEFORE Svg.Custom
/// parses it: once, and a second time when a stylesheet rule is filed under
/// an attribute (#1610). Its own work is bounded by
/// <see cref="MaxGuardSteps"/>.
/// </summary>
/// <remarks>
/// The reference graph errs wide wherever the guard cannot know what
/// applies: a stylesheet rule whose selector is not a simple type, class
/// or id selector applies to every element, and a compound selector
/// applies to everything one of its parts names. Two kinds of edge are
/// left out because a render does not follow them into content: a
/// gradient's (its only children are stops), and the targets of links and
/// animations, which name an element without drawing it.
/// </remarks>
internal static class SvgPreviewGuard
{
    /// <summary>Longest chain of nesting and references a preview may
    /// render. Real documents stay in the tens.</summary>
    internal const int MaxRenderDepth = 256;

    /// <summary>Most element renders a preview may expand to, counting an
    /// element once per path that reaches it: a symbol used ten times
    /// renders ten times.</summary>
    internal const long MaxRenderedElements = 200_000;

    /// <summary>Most <c>url(</c> references in one attribute value or CSS
    /// declaration. A paint takes one reference and a fallback.</summary>
    internal const int MaxUrlReferencesPerValue = 8;

    /// <summary>Deepest bracket nesting in one attribute value or one
    /// stylesheet.</summary>
    internal const int MaxValueNesting = 16;

    /// <summary>#1610 (CB-29): the longest decode a preview admits, as
    /// <see cref="SvgDecodeEstimate"/> estimates it at
    /// <see cref="SvgDecodeRates"/>. 2,000 ms is a PLACEHOLDER: the
    /// recommendation of CB-29's calibration table, built in until the owner
    /// picks the limit from that table (CD20's second step). It is not the
    /// owner's pick.</summary>
    internal const int MaxEstimatedDecodeMilliseconds = 2_000;

    /// <summary>
    /// #1610 (the red team's B1, closed in stage 6): the most steps one
    /// check may take across its read, Measure and the estimate. A step is
    /// one element, attribute, edge, rule, branch, declaration, key visit
    /// or var() lookup, or <see cref="GuardSteps.CharactersPerStep"/>
    /// characters a scan touches, its rescans and copies included, so no
    /// work of the guard goes uncounted. The guard runs on the caller before
    /// any decoder slot, so past this budget it stops and refuses the
    /// document for its cost. Linear work is at most about 3.4 steps an
    /// input byte (measured: 28.3 M steps on the 8 MiB maximum, one var()
    /// definition of a million references; 1.6 on the largest calibration
    /// document), so within the 8 MiB cap only a product of counts reaches
    /// the budget. 64,000,000 is 4.9 times the most steps any real or
    /// calibration document takes (12,958,636, 8 MiB of stylesheet).
    /// </summary>
    internal const long MaxGuardSteps = 64_000_000;

    [ThreadStatic]
    private static long? stepBudgetForTests;

    /// <summary>Test seam: the guard's step budget for checks on this
    /// thread (#1610, the red team's B1), in place of the app's.</summary>
    internal static long? StepBudgetForTests
    {
        get => stepBudgetForTests;
        set => stepBudgetForTests = value;
    }

    /// <summary>The key of a stylesheet rule that applies to every element.</summary>
    private const string AnyElementKey = "*";

    private static readonly string[] ForbiddenTokens =
    [
        "<!doctype",
        "<!entity",
        "<script",
        "<foreignobject",
        "@import",
    ];

    internal static SvgRefusal Check(string source, int maxCharacters) =>
        Check(source, maxCharacters, out _);

    /// <summary>The verdict, and for a document every other check admits,
    /// the decode time it is estimated at (#1610): the caps are checked
    /// first, so a document past one still says which. The steps the check
    /// took are given whatever the verdict.</summary>
    internal static SvgRefusal Check(string source, int maxCharacters, out SvgDecodeEstimate estimate)
    {
        estimate = default;
        var steps = new GuardSteps(StepBudgetForTests ?? MaxGuardSteps);
        GuardSteps? outer = scanning;
        scanning = steps;
        SvgRefusal refusal;
        try
        {
            // The token scans and the reader's pass read the source, up to
            // the reader's cap.
            long read = Math.Min(source.Length, (long)maxCharacters);
            Scanned((ForbiddenTokens.Length + 1) * read);
            refusal = ForbiddenTokens.Any(token => source.Contains(token, StringComparison.OrdinalIgnoreCase))
                ? SvgRefusal.Markup
                : Judge(source, maxCharacters, steps, out estimate);
        }
        catch (GuardStepsSpentException)
        {
            estimate = new SvgDecodeEstimate(0, 0, 0, 0) { StepsSpent = true };
            refusal = SvgRefusal.DecodeCost;
        }
        finally
        {
            scanning = outer;
        }
        estimate = estimate with { Steps = steps.Spent };
        return refusal;
    }

    /// <summary>The check under way on this thread, which every scan
    /// charges (<see cref="Scanned"/>).</summary>
    [ThreadStatic]
    private static GuardSteps? scanning;

    /// <summary>
    /// Charges <paramref name="characters"/> a scan reads or copies to the
    /// check under way (the lead's closure of the guard-time class, stage
    /// 6). Every character-level scan charges what it touches, its rescans
    /// and copies included, so no scan does work the step budget does not
    /// see: the guard's own time is linear in what it touches, and what it
    /// touches is bounded by <see cref="MaxGuardSteps"/> times
    /// <see cref="GuardSteps.CharactersPerStep"/>.
    /// </summary>
    private static void Scanned(long characters) => scanning?.Touch(characters);

    /// <summary><see cref="Scanned"/> in an expression: true.</summary>
    private static bool Charged(long characters)
    {
        Scanned(characters);
        return true;
    }

    /// <summary>The read, Measure and the estimate, each step spent from
    /// <paramref name="steps"/>.</summary>
    private static SvgRefusal Judge(
        string source, int maxCharacters, GuardSteps steps, out SvgDecodeEstimate estimate)
    {
        estimate = default;
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = maxCharacters,
            IgnoreComments = true,
            IgnoreProcessingInstructions = false,
        };
        var document = new DocumentShape(steps);
        try
        {
            using var textReader = new StringReader(source);
            using XmlReader reader = XmlReader.Create(textReader, settings);
            SvgRefusal read = document.Read(reader);
            if (read != SvgRefusal.None)
            {
                return read;
            }
        }
        catch (XmlException)
        {
            return SvgRefusal.Malformed;
        }
        SvgRefusal measured = document.Measure();
        if (measured != SvgRefusal.None)
        {
            return measured;
        }
        estimate = new DecodeCostEstimator(document, source, settings, steps).Estimate();
        return estimate.Milliseconds > MaxEstimatedDecodeMilliseconds
            ? SvgRefusal.DecodeCost
            : SvgRefusal.None;
    }

    /// <summary>The steps one check has left (#1610, B1); spending past
    /// them throws <see cref="GuardStepsSpentException"/>, which
    /// <see cref="Check(string, int, out SvgDecodeEstimate)"/> turns into a
    /// refusal for the cost.</summary>
    private sealed class GuardSteps(long budget)
    {
        private long left = budget;

        internal long Spent => Budget - Math.Max(0, left);

        private long Budget { get; } = budget;

        internal void Spend(long count)
        {
            left -= count;
            if (left < 0)
            {
                throw new GuardStepsSpentException();
            }
        }

        /// <summary>Characters a scan touches per step: a step's other
        /// kinds (an edge, a lookup, a key visit) cost about what 16
        /// characters of scanning cost.</summary>
        internal const int CharactersPerStep = 16;

        private long characters;

        /// <summary>Spends a step per <see cref="CharactersPerStep"/>
        /// characters touched.</summary>
        internal void Touch(long count)
        {
            characters += count;
            if (characters >= CharactersPerStep)
            {
                long whole = characters / CharactersPerStep;
                characters -= whole * CharactersPerStep;
                Spend(whole);
            }
        }
    }

    private sealed class GuardStepsSpentException : Exception;

    /// <summary>#1610 (CB-29): the decode time <see cref="Check"/> compares
    /// with <see cref="MaxEstimatedDecodeMilliseconds"/>, for a document every
    /// other check admits; zero for any other.</summary>
    internal static SvgDecodeEstimate Estimate(string source, int maxCharacters)
    {
        Check(source, maxCharacters, out SvgDecodeEstimate estimate);
        return estimate;
    }

    /// <summary>The elements of a document and the edges a render can
    /// follow between them, collected in one pass.</summary>
    private sealed class DocumentShape(GuardSteps steps)
    {
        private readonly List<int> parents = [];
        private readonly List<string> names = [];
        private readonly List<string?> ids = [];
        private readonly List<string?> classLists = [];
        private readonly List<(int From, string Id)> references = [];
        private readonly List<(int From, int To)> filterInputs = [];
        private readonly List<RuleBlock> ruleBlocks = [];
        private readonly List<int> open = [];
        private readonly List<FilterInputs?> openFilters = [];
        private readonly StringBuilder styleText = new();
        private int styleDepth = -1;
        private string? keyedPrelude;

        // #1610 (CB-29): what the decode estimate reads, collected in the
        // same pass, and Measure's graph, kept for it.
        private readonly List<int> vertices = [];
        private readonly List<MarkerDeclaration> elementMarkers = [];
        private readonly List<(string Name, string Value)> customDefinitions = [];
        private readonly List<(int Element, string Value)> attributeVarUses = [];
        private readonly List<(int Element, string Value)> pathVarUses = [];
        private readonly List<(int Element, string? Target)> uses = [];
        private readonly List<int> order = [];

        internal IReadOnlyList<string> Names => names;

        internal IReadOnlyList<int> Parents => parents;

        internal IReadOnlyList<string?> Ids => ids;

        internal IReadOnlyList<string?> ClassLists => classLists;

        /// <summary>Each element's marker vertices: what its points or path
        /// data give, 2 for a line, 5 for a rect, circle or ellipse, 0 for
        /// anything that takes no markers.</summary>
        internal IReadOnlyList<int> Vertices => vertices;

        /// <summary>The marker properties elements set in attributes and
        /// style attributes, in document order.</summary>
        internal IReadOnlyList<MarkerDeclaration> ElementMarkers => elementMarkers;

        /// <summary>The custom properties style attributes define.</summary>
        internal IReadOnlyList<(string Name, string Value)> CustomDefinitions => customDefinitions;

        /// <summary>Attribute and style declaration values holding var(),
        /// custom-property definitions aside, and the element each is
        /// on.</summary>
        internal IReadOnlyList<(int Element, string Value)> AttributeVarUses => attributeVarUses;

        /// <summary>A path's style-attribute <c>d</c> values read through
        /// <c>var()</c>, in document order.</summary>
        internal IReadOnlyList<(int Element, string Value)> PathVarUses => pathVarUses;

        /// <summary>The stylesheet's rules, read once for Measure and the
        /// estimate.</summary>
        internal List<StyleRule> Rules { get; private set; } = [];

        /// <summary>The url() targets each custom property can resolve to
        /// (Codex's review, stage 4), built by Measure.</summary>
        internal VarReach? Reach { get; private set; }

        /// <summary>Every use element and the id its href names.</summary>
        internal IReadOnlyList<(int Element, string? Target)> Uses => uses;

        internal bool HasStyleElement { get; private set; }

        /// <summary>The joined stylesheet, as Svg.Custom reads it.</summary>
        internal string Stylesheet { get; private set; } = string.Empty;

        /// <summary>Measure's graph: the elements, then one node per rule
        /// key, one node per id a reference names, one node per component
        /// of custom properties that reaches one, and one node per rule
        /// block several keys share, and each node's edges.</summary>
        internal List<int>?[] Next { get; private set; } = [];

        /// <summary>Each node's render count, as Measure counts it.</summary>
        internal long[] Paths { get; private set; } = [];

        /// <summary>The nodes in the order Measure reached them: each before
        /// every node it has an edge to.</summary>
        internal IReadOnlyList<int> Order => order;

        internal Dictionary<string, List<int>> ElementsById { get; private set; } = new(StringComparer.Ordinal);

        internal SvgRefusal Read(XmlReader reader)
        {
            while (reader.Read())
            {
                SvgRefusal refusal = reader.NodeType switch
                {
                    XmlNodeType.ProcessingInstruction when reader.Name.Equals(
                        "xml-stylesheet", StringComparison.OrdinalIgnoreCase) => SvgRefusal.Markup,
                    XmlNodeType.Element => ReadElement(reader),
                    XmlNodeType.Text or XmlNodeType.CDATA or XmlNodeType.Whitespace
                        or XmlNodeType.SignificantWhitespace when styleDepth >= 0
                        && reader.Depth > styleDepth => AppendStyle(reader.Value),
                    XmlNodeType.EndElement when reader.Depth == styleDepth => EndStyle(),
                    _ => SvgRefusal.None,
                };
                if (refusal != SvgRefusal.None)
                {
                    return refusal;
                }
            }
            // Svg.Custom joins every <style> element into one stylesheet
            // before ExCSS parses it, so a block one element leaves open
            // nests what the next one holds: the joined text is the one
            // that is checked.
            Stylesheet = styleText.ToString();
            SvgRefusal sheet = ReadStylesheet(Stylesheet);
            if (sheet == SvgRefusal.None)
            {
                Rules = StyleRules(CssText(Stylesheet), steps);
            }
            return sheet;
        }

        private SvgRefusal AppendStyle(string text)
        {
            styleText.Append(text);
            return SvgRefusal.None;
        }

        private SvgRefusal EndStyle()
        {
            styleDepth = -1;
            styleText.Append('\n');
            return SvgRefusal.None;
        }

        private SvgRefusal ReadElement(XmlReader reader)
        {
            steps.Spend(1);
            int index = names.Count;
            int depth = reader.Depth;
            if (index >= MaxRenderedElements)
            {
                return SvgRefusal.RenderedElements;
            }
            // The root is the first level of every chain.
            if (depth >= MaxRenderDepth)
            {
                return SvgRefusal.RenderDepth;
            }
            string name = reader.LocalName;
            if (name.Equals("script", StringComparison.OrdinalIgnoreCase)
                || name.Equals("foreignObject", StringComparison.OrdinalIgnoreCase))
            {
                return SvgRefusal.Markup;
            }

            while (open.Count <= depth)
            {
                open.Add(-1);
                openFilters.Add(null);
            }
            int parent = depth > 0 ? open[depth - 1] : -1;
            FilterInputs? enclosing = depth > 0 ? openFilters[depth - 1] : null;
            open[depth] = index;
            openFilters[depth] = name.Equals("filter", StringComparison.Ordinal)
                ? new FilterInputs(index)
                : enclosing;

            string? id = null;
            string? classList = null;
            string? input = null;
            string? input2 = null;
            string? result = null;
            string? useTarget = null;
            int vertexCount = name switch
            {
                "line" => 2,
                "rect" or "circle" or "ellipse" => 5,
                _ => 0,
            };
            if (reader.MoveToFirstAttribute())
            {
                do
                {
                    steps.Spend(1);
                    if (reader.Prefix == "xmlns"
                        || (reader.Prefix.Length == 0 && reader.LocalName == "xmlns"))
                    {
                        continue;
                    }
                    string attribute = reader.LocalName;
                    string value = reader.Value;
                    switch (attribute)
                    {
                        case "id":
                            id = value;
                            break;
                        case "class":
                            classList = value;
                            break;
                        case "in":
                            input = value;
                            break;
                        case "in2":
                            input2 = value;
                            break;
                        case "result":
                            result = value;
                            break;
                    }
                    ReadCostAttribute(index, name, attribute, value, ref vertexCount, ref useTarget);
                    SvgRefusal refusal = attribute.Equals("href", StringComparison.OrdinalIgnoreCase)
                        ? ReadHref(index, name, value)
                        : SvgRefusal.None;
                    bool declarations = attribute == "style";
                    if (refusal == SvgRefusal.None)
                    {
                        refusal = ScanCss(value, index, declarations, tokens: false);
                    }
                    if (refusal == SvgRefusal.None)
                    {
                        refusal = ScanCss(value, index, declarations, tokens: true);
                    }
                    if (refusal != SvgRefusal.None)
                    {
                        reader.MoveToElement();
                        return refusal;
                    }
                }
                while (reader.MoveToNextAttribute());
                reader.MoveToElement();
            }

            names.Add(name);
            parents.Add(parent);
            ids.Add(id);
            classLists.Add(classList);
            vertices.Add(vertexCount);
            if (name == "use")
            {
                uses.Add((index, useTarget));
            }
            if (name.Equals("style", StringComparison.OrdinalIgnoreCase))
            {
                HasStyleElement = true;
            }
            AddFilterInputs(index, parent, enclosing, input, input2, result);

            if (styleDepth < 0
                && !reader.IsEmptyElement
                && name.Equals("style", StringComparison.OrdinalIgnoreCase))
            {
                styleDepth = depth;
            }
            return SvgRefusal.None;
        }

        /// <summary>An href may name an element of this document or carry
        /// an image inline; nothing else. An feImage may not name an
        /// element: Svg.Skia copies the element it draws, and a chain of
        /// them costs more than its length.</summary>
        private SvgRefusal ReadHref(int index, string element, string value)
        {
            Scanned(3L * value.Length);
            string target = value.Trim();
            if (target.StartsWith('#'))
            {
                if (element.Equals("feImage", StringComparison.OrdinalIgnoreCase))
                {
                    return SvgRefusal.ElementImage;
                }
                if (DrawsWhatItNames(element))
                {
                    references.Add((index, target[1..]));
                }
                return SvgRefusal.None;
            }
            return target.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase)
                ? SvgRefusal.None
                : SvgRefusal.ExternalReference;
        }

        /// <summary>#1610 (CB-29): what one attribute adds to the decode
        /// estimate: a shape's marker vertices, a marker property, a style
        /// attribute's custom properties, markers and var() uses, a var()
        /// use in any other attribute, and the id a use draws.</summary>
        private void ReadCostAttribute(
            int index, string element, string attribute, string value, ref int vertexCount, ref string? useTarget)
        {
            // The var() and use checks below each read the value.
            Scanned(3L * value.Length);
            if (attribute == "points" && element is "polyline" or "polygon")
            {
                Scanned(value.Length);
                int points = NumberCount(value, 0, value.Length) / 2;
                vertexCount = points + (element == "polygon" && points >= 1 ? 1 : 0);
            }
            else if (attribute == "d" && element == "path")
            {
                vertexCount = Math.Max(vertexCount, PathVertices(value));
            }
            if (attribute == "style")
            {
                ReadStyleAttribute(index, element, value, ref vertexCount);
                return;
            }
            if (MarkerSlots(attribute) is { } slots)
            {
                AddMarker(index, slots, value);
            }
            if (value.Contains("var(", StringComparison.OrdinalIgnoreCase))
            {
                attributeVarUses.Add((index, value));
            }
            if (element == "use" && attribute.Equals("href", StringComparison.OrdinalIgnoreCase)
                && value.Trim() is { Length: > 1 } target && target[0] == '#')
            {
                useTarget = target[1..];
            }
        }

        /// <summary>A style attribute's declarations, read as ExCSS reads
        /// them (escapes resolved, comments gone, the red team's M1): its
        /// custom properties, markers, var() uses, and a path's CSS
        /// <c>d:path()</c> vertices, those a <c>d</c> read through
        /// <c>var()</c> reaches left to the estimate (Codex's review,
        /// stage 4).</summary>
        private void ReadStyleAttribute(int index, string element, string value, ref int vertexCount)
        {
            string text = CssText(value);
            Scanned(3L * text.Length);
            bool path = element == "path";
            if (!path
                && !text.Contains("--", StringComparison.Ordinal)
                && !text.Contains("var(", StringComparison.OrdinalIgnoreCase)
                && !text.Contains("marker", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
            foreach ((string property, string declared) in Declarations(text))
            {
                if (property.StartsWith("--", StringComparison.Ordinal))
                {
                    customDefinitions.Add((property, declared));
                    continue;
                }
                if (path && property.Equals("d", StringComparison.OrdinalIgnoreCase))
                {
                    vertexCount = Math.Max(vertexCount, CssPathVertices(declared));
                    if (declared.Contains("var(", StringComparison.OrdinalIgnoreCase))
                    {
                        pathVarUses.Add((index, declared));
                    }
                }
                if (MarkerSlots(property) is { } slots)
                {
                    AddMarker(index, slots, declared);
                }
                if (declared.Contains("var(", StringComparison.OrdinalIgnoreCase))
                {
                    attributeVarUses.Add((index, declared));
                }
            }
        }

        private void AddMarker(int index, int slots, string value)
        {
            if (ReadMarkerValue(value, out string? target, out string? throughVar))
            {
                elementMarkers.Add(new MarkerDeclaration(index, slots, target, throughVar));
            }
        }

        /// <summary>A filter primitive draws its inputs: the results it
        /// names, or the previous primitive's result when it names
        /// none.</summary>
        private void AddFilterInputs(
            int index, int parent, FilterInputs? filter, string? input, string? input2, string? result)
        {
            if (filter is null)
            {
                return;
            }
            bool primitive = parent == filter.Owner;
            if (input is not null)
            {
                if (filter.Results.TryGetValue(input, out int producer))
                {
                    filterInputs.Add((index, producer));
                }
            }
            else if (primitive && filter.Last >= 0)
            {
                filterInputs.Add((index, filter.Last));
            }
            if (input2 is not null && filter.Results.TryGetValue(input2, out int producer2))
            {
                filterInputs.Add((index, producer2));
            }
            if (primitive)
            {
                if (result is not null)
                {
                    filter.Results[result] = index;
                }
                filter.Last = index;
            }
        }

        /// <summary>Checks a stylesheet against the caps, and records each
        /// element it references under the rules that reference it.</summary>
        private SvgRefusal ReadStylesheet(string stylesheet) =>
            ScanCss(stylesheet, from: -1, declarations: true, tokens: true, stylesheet: true);

        /// <summary>
        /// One scan for every CSS-like text: an attribute value (one
        /// value), a style attribute (declarations) or a stylesheet (rules
        /// of declarations).
        /// </summary>
        /// <remarks>
        /// <para>
        /// Escapes are resolved first, as a CSS tokenizer resolves them, so
        /// an escaped <c>url(</c> still counts and an escaped separator
        /// never separates. A <c>url(</c> argument is opaque, as in CSS:
        /// what it holds neither separates declarations nor opens a block.
        /// </para>
        /// <para>
        /// With <paramref name="tokens"/> the text is read as ExCSS reads
        /// it: a comment counts for nothing, and a string's brackets and
        /// separators are its own (a string ends at its quote or, left
        /// open, at a line end), though a <c>url(</c> in a string still
        /// counts, in case anything unquotes it. Without, every character
        /// counts, as a parser that knows no CSS sees it. An attribute
        /// value is read both ways and refused if either way passes a cap;
        /// a stylesheet, which only ExCSS reads, only as tokens.
        /// References are collected once: from an attribute's plain read,
        /// and from a stylesheet's token read.
        /// </para>
        /// </remarks>
        private SvgRefusal ScanCss(
            string raw, int from, bool declarations, bool tokens, bool stylesheet = false)
        {
            string text = Unescape(raw);
            Scanned(text.Length);
            int urls = 0;
            int depth = 0;
            int boundary = 0;
            List<string?> preludes = [];
            for (int i = 0; i < text.Length; i++)
            {
                if (tokens && text[i] == '/' && i + 1 < text.Length && text[i + 1] == '*')
                {
                    int close = text.IndexOf("*/", i + 2, StringComparison.Ordinal);
                    Scanned((close < 0 ? text.Length : close + 2) - i);
                    i = close < 0 ? text.Length : close + 1;
                    continue;
                }
                if (tokens && text[i] is '"' or '\'')
                {
                    int end = StringEnd(text, i);
                    Scanned(end - i);
                    for (int k = i + 1; k < end; k++)
                    {
                        if (!IsUrlAt(text, k))
                        {
                            continue;
                        }
                        if (++urls > MaxUrlReferencesPerValue)
                        {
                            return SvgRefusal.UrlReferences;
                        }
                        int argumentEnd = k + 4 < end ? text.IndexOf(')', k + 4, end - (k + 4)) : -1;
                        Scanned((argumentEnd < 0 ? end : argumentEnd) - k);
                        if (stylesheet && Fragment(text, k + 4, argumentEnd < 0 ? end : argumentEnd) is { } quoted)
                        {
                            AddRuleTarget(preludes.Count > 0 ? preludes[^1] : null, quoted);
                        }
                    }
                    // Past the closing quote; a line end that cut the string
                    // short is read on.
                    i = end < text.Length && text[end] == text[i] ? end : end - 1;
                    continue;
                }
                switch (text[i])
                {
                    case '{' when stylesheet:
                        if (++depth > MaxValueNesting)
                        {
                            return SvgRefusal.ValueNesting;
                        }
                        Scanned(i - boundary);
                        preludes.Add(text[boundary..i].Trim());
                        urls = 0;
                        boundary = i + 1;
                        break;
                    case '}' when stylesheet:
                        depth = Math.Max(0, depth - 1);
                        if (preludes.Count > 0)
                        {
                            preludes.RemoveAt(preludes.Count - 1);
                        }
                        urls = 0;
                        boundary = i + 1;
                        break;
                    case ';' when declarations:
                        urls = 0;
                        boundary = i + 1;
                        break;
                    case '(' or '[' or '{':
                        if (++depth > MaxValueNesting)
                        {
                            return SvgRefusal.ValueNesting;
                        }
                        break;
                    case ')' or ']' or '}':
                        depth = Math.Max(0, depth - 1);
                        break;
                    case 'u' or 'U' when IsUrlAt(text, i):
                        if (++urls > MaxUrlReferencesPerValue)
                        {
                            return SvgRefusal.UrlReferences;
                        }
                        int end = UrlArgumentEnd(text, i + 4);
                        int argumentEnd = end < text.Length && text[end] == ')' ? end : text.Length;
                        if (Fragment(text, i + 4, argumentEnd) is { } target)
                        {
                            if (stylesheet)
                            {
                                AddRuleTarget(preludes.Count > 0 ? preludes[^1] : null, target);
                            }
                            else if (!tokens)
                            {
                                references.Add((from, target));
                            }
                        }
                        i = end;
                        break;
                }
            }
            return SvgRefusal.None;
        }

        /// <summary>Files <paramref name="target"/> under the key of every
        /// selector in <paramref name="prelude"/>, or under every element
        /// when the prelude is not a list of simple selectors: in the
        /// block the prelude opens, whose keys are read once, each key
        /// once.</summary>
        private void AddRuleTarget(string? prelude, string target)
        {
            if (ruleBlocks.Count == 0 || !ReferenceEquals(prelude, keyedPrelude))
            {
                ruleBlocks.Add(new RuleBlock(KeysOf(prelude)));
                keyedPrelude = prelude;
            }
            steps.Spend(1);
            ruleBlocks[^1].Targets.Add(target);
        }

        /// <summary>The key of every selector in <paramref name="prelude"/>,
        /// each once, or every element's when the prelude is not a list of
        /// simple selectors.</summary>
        private List<string> KeysOf(string? prelude)
        {
            var keys = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            Scanned(2L * (prelude?.Length ?? 0));
            foreach (string selector in (prelude ?? string.Empty).Split(','))
            {
                steps.Spend(1);
                if (SimpleSelectorKey(selector) is not { } key)
                {
                    return [AnyElementKey];
                }
                if (seen.Add(key))
                {
                    keys.Add(key);
                }
            }
            return keys;
        }

        /// <summary>A stylesheet block's keys, the ids its <c>url()</c>
        /// references name, and the custom-property components its
        /// <c>var()</c> references reach (Codex's review, stage 4).</summary>
        private sealed class RuleBlock(List<string> keys)
        {
            internal List<string> Keys { get; } = keys;

            internal HashSet<string> Targets { get; } = new(StringComparer.Ordinal);

            internal HashSet<int> Components { get; } = [];
        }

        /// <summary>
        /// Walks the graph once in dependency order: the longest chain, the
        /// number of renders and whether every element was reached, which
        /// fails exactly when there is a cycle. A reference reaches the
        /// elements an id names through one node per id (the red team's
        /// B1), as a rule's elements reach its targets through one node per
        /// key, and the keys of a block that no other block files reach its
        /// targets through one node per block, so the edges stay linear in
        /// the document when many elements share an id or many selectors
        /// share a block. The render counts are the same as an edge from
        /// each key to each distinct target filed under it. An element or a
        /// rule that names a custom property reaches what the property can
        /// resolve to, as if its value named those targets (Codex's review,
        /// stage 4).
        /// </summary>
        internal SvgRefusal Measure()
        {
            int elements = names.Count;
            var byId = new Dictionary<string, List<int>>(StringComparer.Ordinal);
            // The ids an element that draws carries, found once while the
            // index is built (Codex's round 2): an id only gradients carry
            // reaches nothing, and no later question rescans its elements.
            var drawing = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < elements; i++)
            {
                steps.Spend(1);
                if (ids[i] is { } id)
                {
                    if (!byId.TryGetValue(id, out List<int>? named))
                    {
                        byId[id] = named = [];
                    }
                    named.Add(i);
                    if (!IsGradient(names[i]))
                    {
                        drawing.Add(id);
                    }
                }
            }
            // Custom properties (Codex's review, stage 4): an element or a
            // rule whose value names one reaches every url() target the
            // property can resolve to, through one node per component of
            // properties that reaches a target that draws.
            bool Draws(string id) => drawing.Contains(id);
            var reach = new VarReach(
                customDefinitions.Concat(Rules
                    .Where(rule => rule.Prelude is not null)
                    .SelectMany(rule => rule.Declarations)
                    .Where(declaration => declaration.Name.StartsWith("--", StringComparison.Ordinal))),
                steps);
            Reach = reach;
            var reaches = new bool[reach.Count];
            for (int c = 0; c < reach.Count; c++)
            {
                steps.Spend(1 + reach.Urls[c].Count + reach.Named[c].Count);
                reaches[c] = reach.Urls[c].Exists(Draws) || reach.Named[c].Exists(other => reaches[other]);
            }
            HashSet<int> Reached(string value)
            {
                var found = new HashSet<int>();
                foreach (string name in NamesIn(value))
                {
                    steps.Spend(1);
                    if (reach.ComponentOf(name) is var c and >= 0 && reaches[c])
                    {
                        found.Add(c);
                    }
                }
                return found;
            }
            foreach (StyleRule rule in Rules)
            {
                if (rule.Prelude is null)
                {
                    continue;
                }
                RuleBlock? block = null;
                foreach ((string name, string value) in rule.Declarations)
                {
                    steps.Spend(1);
                    Scanned(value.Length);
                    if (name.StartsWith("--", StringComparison.Ordinal)
                        || !value.Contains("var(", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    foreach (int c in Reached(value))
                    {
                        block ??= new RuleBlock(KeysOf(rule.Prelude));
                        block.Components.Add(c);
                    }
                }
                if (block is not null)
                {
                    ruleBlocks.Add(block);
                }
            }
            var keyNodes = new Dictionary<string, int>(StringComparer.Ordinal);
            var keyBlocks = new List<int>();
            foreach (RuleBlock block in ruleBlocks)
            {
                foreach (string key in block.Keys)
                {
                    steps.Spend(1);
                    if (!keyNodes.TryGetValue(key, out int node))
                    {
                        keyNodes[key] = node = elements + keyNodes.Count;
                        keyBlocks.Add(0);
                    }
                    keyBlocks[node - elements]++;
                }
            }
            // One node per id a reference or a rule names that draws
            // something: an id only gradients carry reaches nothing.
            var idNodes = new Dictionary<string, int>(StringComparer.Ordinal);
            var namedNothing = new HashSet<string>(StringComparer.Ordinal);
            void NameNode(string id)
            {
                steps.Spend(1);
                if (idNodes.ContainsKey(id) || namedNothing.Contains(id))
                {
                    return;
                }
                if (Draws(id))
                {
                    idNodes[id] = elements + keyNodes.Count + idNodes.Count;
                }
                else
                {
                    namedNothing.Add(id);
                }
            }
            foreach ((int _, string id) in references)
            {
                NameNode(id);
            }
            foreach (RuleBlock block in ruleBlocks)
            {
                foreach (string id in block.Targets)
                {
                    NameNode(id);
                }
            }
            for (int c = 0; c < reach.Count; c++)
            {
                if (reaches[c])
                {
                    foreach (string id in reach.Urls[c])
                    {
                        NameNode(id);
                    }
                }
            }
            int firstComponentNode = elements + keyNodes.Count + idNodes.Count;
            var componentNodes = new int[reach.Count];
            int componentNodeCount = 0;
            for (int c = 0; c < reach.Count; c++)
            {
                componentNodes[c] = reaches[c] ? firstComponentNode + componentNodeCount++ : -1;
            }
            // One node per block that two or more keys file alone.
            int firstRuleNode = firstComponentNode + componentNodeCount;
            var ruleNodes = new int[ruleBlocks.Count];
            int ruleNodeCount = 0;
            for (int b = 0; b < ruleBlocks.Count; b++)
            {
                int alone = ruleBlocks[b].Keys.Count(key => keyBlocks[keyNodes[key] - elements] == 1);
                ruleNodes[b] = alone >= 2 ? firstRuleNode + ruleNodeCount++ : -1;
            }
            int nodes = firstRuleNode + ruleNodeCount;
            var next = new List<int>?[nodes];
            var indegree = new int[nodes];
            void Edge(int from, int to)
            {
                steps.Spend(1);
                (next[from] ??= []).Add(to);
                indegree[to]++;
            }

            for (int i = 0; i < elements; i++)
            {
                if (parents[i] >= 0)
                {
                    Edge(parents[i], i);
                }
            }
            foreach ((string id, int node) in idNodes)
            {
                steps.Spend(byId[id].Count);
                foreach (int target in byId[id])
                {
                    if (!IsGradient(names[target]))
                    {
                        Edge(node, target);
                    }
                }
            }
            void Reference(int from, string id)
            {
                if (idNodes.TryGetValue(id, out int node))
                {
                    Edge(from, node);
                }
            }
            foreach ((int from, string id) in references)
            {
                Reference(from, id);
            }
            foreach ((int from, int to) in filterInputs)
            {
                Edge(from, to);
            }
            for (int c = 0; c < reach.Count; c++)
            {
                if (componentNodes[c] < 0)
                {
                    continue;
                }
                foreach (string id in reach.Urls[c])
                {
                    Reference(componentNodes[c], id);
                }
                foreach (int other in reach.Named[c])
                {
                    if (componentNodes[other] >= 0)
                    {
                        Edge(componentNodes[c], componentNodes[other]);
                    }
                }
            }
            foreach ((int element, string value) in attributeVarUses)
            {
                foreach (int c in Reached(value))
                {
                    Edge(element, componentNodes[c]);
                }
            }
            List<int> TargetNodes(RuleBlock block)
            {
                var found = new List<int>(block.Targets.Count + block.Components.Count);
                foreach (string id in block.Targets)
                {
                    if (idNodes.TryGetValue(id, out int node))
                    {
                        found.Add(node);
                    }
                }
                foreach (int c in block.Components)
                {
                    found.Add(componentNodes[c]);
                }
                return found;
            }
            // A key filed in several blocks reaches each distinct target of
            // its blocks once.
            var keyTargets = new Dictionary<int, HashSet<int>>();
            for (int b = 0; b < ruleBlocks.Count; b++)
            {
                RuleBlock block = ruleBlocks[b];
                int ruleNode = ruleNodes[b];
                List<int> targets = TargetNodes(block);
                if (ruleNode >= 0)
                {
                    foreach (int node in targets)
                    {
                        Edge(ruleNode, node);
                    }
                }
                foreach (string key in block.Keys)
                {
                    int keyNode = keyNodes[key];
                    if (keyBlocks[keyNode - elements] > 1)
                    {
                        if (!keyTargets.TryGetValue(keyNode, out HashSet<int>? filed))
                        {
                            keyTargets[keyNode] = filed = [];
                        }
                        foreach (int node in targets)
                        {
                            steps.Spend(1);
                            if (filed.Add(node))
                            {
                                Edge(keyNode, node);
                            }
                        }
                    }
                    else if (ruleNode >= 0)
                    {
                        Edge(keyNode, ruleNode);
                    }
                    else
                    {
                        foreach (int node in targets)
                        {
                            Edge(keyNode, node);
                        }
                    }
                }
            }
            bool anyClassKey = keyNodes.Keys.Any(key => key.StartsWith('.'));
            for (int i = 0; i < elements; i++)
            {
                steps.Spend(1);
                if (!TakesStyleRules(names[i]))
                {
                    continue;
                }
                if (keyNodes.TryGetValue(AnyElementKey, out int any))
                {
                    Edge(i, any);
                }
                Scanned((2L * names[i].Length) + (3L * ((ids[i]?.Length ?? 0) + (classLists[i]?.Length ?? 0))));
                if (keyNodes.TryGetValue(names[i].ToLowerInvariant(), out int byType))
                {
                    Edge(i, byType);
                }
                if (ids[i] is { } id && keyNodes.TryGetValue("#" + id.ToLowerInvariant(), out int byIdKey))
                {
                    Edge(i, byIdKey);
                }
                if (!anyClassKey || classLists[i] is not { } classList)
                {
                    continue;
                }
                foreach (string name in classList
                    .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                    .Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    if (keyNodes.TryGetValue("." + name.ToLowerInvariant(), out int byClass))
                    {
                        Edge(i, byClass);
                    }
                }
            }

            // Only elements are levels of a chain and renders; a key node is
            // the "every element this rule matches" fan-in, free to pass.
            var longest = new int[nodes];
            var paths = new long[nodes];
            var ready = new Queue<int>();
            for (int node = 0; node < nodes; node++)
            {
                if (indegree[node] == 0)
                {
                    ready.Enqueue(node);
                    if (node < elements)
                    {
                        longest[node] = 1;
                        paths[node] = 1;
                    }
                }
            }
            Next = next;
            Paths = paths;
            ElementsById = byId;
            int reached = 0;
            long rendered = 0;
            while (ready.TryDequeue(out int node))
            {
                steps.Spend(1 + (next[node]?.Count ?? 0));
                reached++;
                order.Add(node);
                if (node < elements)
                {
                    if (longest[node] > MaxRenderDepth)
                    {
                        return SvgRefusal.RenderDepth;
                    }
                    rendered += paths[node];
                    if (rendered > MaxRenderedElements)
                    {
                        return SvgRefusal.RenderedElements;
                    }
                }
                if (next[node] is not { } outgoing)
                {
                    continue;
                }
                foreach (int to in outgoing)
                {
                    longest[to] = Math.Max(longest[to], longest[node] + (to < elements ? 1 : 0));
                    paths[to] = Math.Min(paths[to] + paths[node], MaxRenderedElements + 1);
                    if (--indegree[to] == 0)
                    {
                        ready.Enqueue(to);
                    }
                }
            }
            return reached < nodes ? SvgRefusal.ReferenceCycle : SvgRefusal.None;
        }

        private sealed class FilterInputs(int owner)
        {
            internal int Owner { get; } = owner;

            internal Dictionary<string, int> Results { get; } = new(StringComparer.Ordinal);

            internal int Last { get; set; } = -1;
        }
    }

    /// <summary>#1610 (CB-29): a marker property an element or a rule sets:
    /// its slots (1 start, 2 mid, 4 end), the marker id it names, or
    /// neither for <c>none</c>, or the value when it is read through
    /// <c>var()</c>.</summary>
    private readonly record struct MarkerDeclaration(int Element, int Slots, string? Target, string? ThroughVar);

    /// <summary>The slots a marker property sets, or null for any other
    /// property.</summary>
    private static int? MarkerSlots(string property) =>
        property.Equals("marker", StringComparison.OrdinalIgnoreCase) ? 7
        : property.Equals("marker-start", StringComparison.OrdinalIgnoreCase) ? 1
        : property.Equals("marker-mid", StringComparison.OrdinalIgnoreCase) ? 2
        : property.Equals("marker-end", StringComparison.OrdinalIgnoreCase) ? 4
        : null;

    /// <summary>Whether a marker property's value places or clears a
    /// marker: <c>none</c>, a <c>url(#id)</c>, or anything through
    /// <c>var()</c>, which Svg.Custom resolves (the value is kept, to read
    /// every <c>url()</c> it can reach). Any other value is
    /// ignored.</summary>
    private static bool ReadMarkerValue(string value, out string? target, out string? throughVar)
    {
        Scanned(4L * value.Length);
        target = null;
        throughVar = null;
        string text = value.Trim();
        if (text.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        if (text.Contains("var(", StringComparison.OrdinalIgnoreCase))
        {
            throughVar = text;
            return true;
        }
        int url = text.IndexOf("url(", StringComparison.OrdinalIgnoreCase);
        if (url < 0)
        {
            return false;
        }
        int end = UrlArgumentEnd(text, url + 4);
        target = Fragment(text, url + 4, end < text.Length && text[end] == ')' ? end : text.Length);
        return target is not null;
    }

    /// <summary>Every element id a value's <c>url()</c> references name, in
    /// its own text: a <c>var()</c> fallback's, at any depth, among
    /// them.</summary>
    private static List<string> UrlsIn(string value)
    {
        Scanned(value.Length);
        var found = new List<string>();
        int url = value.IndexOf("url(", StringComparison.OrdinalIgnoreCase);
        while (url >= 0)
        {
            int end = UrlArgumentEnd(value, url + 4);
            if (Fragment(value, url + 4, end < value.Length && value[end] == ')' ? end : value.Length) is { } id)
            {
                found.Add(id);
            }
            // On past the argument just read: a url( inside it is part of
            // the argument, not a reference, and is never read again
            // (Codex's round 3).
            url = end + 1 < value.Length ? value.IndexOf("url(", end + 1, StringComparison.OrdinalIgnoreCase) : -1;
        }
        return found;
    }

    /// <summary>The custom properties a value's <c>var()</c> references
    /// name, fallbacks' among them.</summary>
    private static List<string> NamesIn(string value)
    {
        Scanned(value.Length);
        var found = new List<string>();
        for (int at = value.IndexOf("var(", StringComparison.OrdinalIgnoreCase);
            at >= 0;
            at = value.IndexOf("var(", at + 4, StringComparison.OrdinalIgnoreCase))
        {
            int start = at + 4;
            while (start < value.Length && char.IsWhiteSpace(value[start]))
            {
                start++;
            }
            int end = start;
            if (end + 1 < value.Length && value[end] == '-' && value[end + 1] == '-')
            {
                end += 2;
                while (end < value.Length && IsVarNameCharacter(value[end]))
                {
                    end++;
                }
                found.Add(value[start..end]);
            }
        }
        return found;
    }

    /// <summary>The numbers in <paramref name="text"/>[start, end), as an
    /// SVG number list or path data writes them: a sign, digits, a point,
    /// digits and an exponent, with "1.5.5" two numbers.</summary>
    private static int NumberCount(string text, int start, int end)
    {
        int count = 0;
        int i = start;
        while (i < end)
        {
            char c = text[i];
            bool signed = (c == '+' || c == '-')
                && i + 1 < end
                && (char.IsAsciiDigit(text[i + 1])
                    || (text[i + 1] == '.' && i + 2 < end && char.IsAsciiDigit(text[i + 2])));
            bool unsigned = char.IsAsciiDigit(c)
                || (c == '.' && i + 1 < end && char.IsAsciiDigit(text[i + 1]));
            if (!signed && !unsigned)
            {
                i++;
                continue;
            }
            if (signed)
            {
                i++;
            }
            if (char.IsAsciiDigit(text[i]))
            {
                while (i < end && char.IsAsciiDigit(text[i]))
                {
                    i++;
                }
                if (i < end && text[i] == '.')
                {
                    i++;
                }
            }
            else
            {
                i++;
            }
            while (i < end && char.IsAsciiDigit(text[i]))
            {
                i++;
            }
            if (i < end && text[i] is 'e' or 'E')
            {
                int exponent = i + 1;
                if (exponent < end && text[exponent] is '+' or '-')
                {
                    exponent++;
                }
                if (exponent < end && char.IsAsciiDigit(text[exponent]))
                {
                    i = exponent;
                    while (i < end && char.IsAsciiDigit(text[i]))
                    {
                        i++;
                    }
                }
            }
            count++;
        }
        return count;
    }

    /// <summary>The marker vertices of path data: per command, its numbers
    /// over the numbers one segment takes (M, L, T 2; H, V 1; C 6; S, Q 4;
    /// A 7, counting 4 vertices, since Svg.Skia may cut an arc into several
    /// segments), and a vertex per Z.</summary>
    private static int PathVertices(string data)
    {
        Scanned(data.Length);
        long total = 0;
        int i = 0;
        while (i < data.Length)
        {
            char command = char.ToUpperInvariant(data[i]);
            int per = command switch
            {
                'M' or 'L' or 'T' => 2,
                'H' or 'V' => 1,
                'C' => 6,
                'S' or 'Q' => 4,
                'A' => 7,
                'Z' => 0,
                _ => -1,
            };
            if (per < 0)
            {
                i++;
                continue;
            }
            int start = ++i;
            while (i < data.Length && !IsPathCommand(data[i]))
            {
                i++;
            }
            total += per == 0 ? 1
                : command == 'A' ? ArcCount(data, start, i) * 4L
                : NumberCount(data, start, i) / per;
        }
        return (int)Math.Min(total, int.MaxValue);
    }

    private static bool IsPathCommand(char c) =>
        char.ToUpperInvariant(c) is 'M' or 'L' or 'T' or 'H' or 'V' or 'C' or 'S' or 'Q' or 'A' or 'Z';

    /// <summary>The arcs in an A command's arguments: three numbers, two
    /// flags, two numbers each. A flag is one character, so it may run into
    /// what follows (the red team's m4: <c>A1 1 0 001 1</c> is one
    /// arc).</summary>
    private static int ArcCount(string text, int start, int end)
    {
        int arcs = 0;
        int i = start;
        while (ReadNumber(text, ref i, end) && ReadNumber(text, ref i, end) && ReadNumber(text, ref i, end)
            && ReadFlag(text, ref i, end) && ReadFlag(text, ref i, end)
            && ReadNumber(text, ref i, end) && ReadNumber(text, ref i, end))
        {
            arcs++;
        }
        return arcs;
    }

    private static void SkipSeparators(string text, ref int i, int end)
    {
        while (i < end && (char.IsWhiteSpace(text[i]) || text[i] == ','))
        {
            i++;
        }
    }

    private static bool ReadFlag(string text, ref int i, int end)
    {
        SkipSeparators(text, ref i, end);
        if (i < end && text[i] is '0' or '1')
        {
            i++;
            return true;
        }
        return false;
    }

    /// <summary>Reads one number as <see cref="NumberCount"/> counts
    /// them, after any separators.</summary>
    private static bool ReadNumber(string text, ref int i, int end)
    {
        SkipSeparators(text, ref i, end);
        int at = i;
        if (at < end && text[at] is '+' or '-')
        {
            at++;
        }
        int digits = at;
        while (at < end && char.IsAsciiDigit(text[at]))
        {
            at++;
        }
        if (at < end && text[at] == '.')
        {
            at++;
            while (at < end && char.IsAsciiDigit(text[at]))
            {
                at++;
            }
        }
        if (at == digits || (at == digits + 1 && text[digits] == '.'))
        {
            return false;
        }
        if (at < end && text[at] is 'e' or 'E')
        {
            int exponent = at + 1;
            if (exponent < end && text[exponent] is '+' or '-')
            {
                exponent++;
            }
            if (exponent < end && char.IsAsciiDigit(text[exponent]))
            {
                at = exponent;
                while (at < end && char.IsAsciiDigit(text[at]))
                {
                    at++;
                }
            }
        }
        i = at;
        return true;
    }

    /// <summary>The most vertices any CSS <c>path("...")</c> in a value's
    /// own text gives (the red team's m4), a <c>var()</c> fallback's among
    /// them (Codex's review, stage 4), or 0.</summary>
    private static int CssPathVertices(string value)
    {
        Scanned(value.Length);
        int most = 0;
        for (int open = value.IndexOf("path(", StringComparison.OrdinalIgnoreCase);
            open >= 0;
            open = value.IndexOf("path(", open + 5, StringComparison.OrdinalIgnoreCase))
        {
            int i = open + 5;
            while (i < value.Length && char.IsWhiteSpace(value[i]))
            {
                i++;
            }
            if (i >= value.Length || value[i] is not ('"' or '\''))
            {
                continue;
            }
            int close = value.IndexOf(value[i], i + 1);
            Scanned((close < 0 ? value.Length : close) - i);
            most = Math.Max(most, PathVertices(close < 0 ? value[(i + 1)..] : value[(i + 1)..close]));
            if (close < 0)
            {
                break;
            }
            open = close - 4;
        }
        return most;
    }

    /// <summary>
    /// CSS text as ExCSS reads it (the red team's M1): escapes resolved
    /// first, as <see cref="Unescape"/> does for the guard's own scan, then
    /// every comment removed, except where <c>/*</c> sits inside a string or
    /// a <c>url()</c> argument, which a comment cannot open.
    /// </summary>
    private static string CssText(string raw)
    {
        string css = Unescape(raw);
        Scanned(css.Length);
        if (!css.Contains("/*", StringComparison.Ordinal))
        {
            return css;
        }
        Scanned(css.Length);
        var text = new StringBuilder(css.Length);
        int i = 0;
        while (i < css.Length)
        {
            char c = css[i];
            if (c is '"' or '\'')
            {
                int end = CssStringEnd(css, i);
                text.Append(css, i, end - i);
                i = end;
            }
            else if (c is 'u' or 'U' && IsUrlAt(css, i))
            {
                int end = Math.Min(css.Length, UrlArgumentEnd(css, i + 4) + 1);
                text.Append(css, i, end - i);
                i = end;
            }
            else if (c == '/' && i + 1 < css.Length && css[i + 1] == '*')
            {
                int close = css.IndexOf("*/", i + 2, StringComparison.Ordinal);
                int after = close < 0 ? css.Length : close + 2;
                Scanned(after - i);
                i = after;
            }
            else
            {
                text.Append(c);
                i++;
            }
        }
        return text.ToString();
    }

    /// <summary>A stylesheet rule: its prelude and declarations, or a null
    /// prelude for a block at-rule other than <c>@media</c>.</summary>
    private sealed record StyleRule(string? Prelude, List<(string Name, string Value)> Declarations);

    /// <summary>Every qualified rule of a stylesheet, as <see cref="CssText"/>
    /// gives it, and those of each <c>@media</c> block, which Svg.Custom
    /// applies. Any other block at-rule (an animation export's
    /// <c>@keyframes</c>, the red team's M2) is one rule ExCSS parses and
    /// Svg.Custom does not apply.</summary>
    private static List<StyleRule> StyleRules(string text, GuardSteps steps)
    {
        var found = new List<StyleRule>();
        var blocks = new Stack<(int Start, int End)>();
        blocks.Push((0, text.Length));
        while (blocks.TryPop(out (int Start, int End) block))
        {
            int i = block.Start;
            while (i < block.End)
            {
                steps.Spend(1);
                int open = i;
                while (open < block.End && text[open] != '{')
                {
                    open = text[open] is '"' or '\'' ? CssStringEnd(text, open) : open + 1;
                }
                if (open >= block.End)
                {
                    break;
                }
                int depth = 1;
                int k = open + 1;
                while (k < block.End && depth > 0)
                {
                    char c = text[k];
                    if (c is '"' or '\'')
                    {
                        k = CssStringEnd(text, k);
                        continue;
                    }
                    depth += c == '{' ? 1 : c == '}' ? -1 : 0;
                    k++;
                }
                int bodyEnd = Math.Max(open + 1, k - 1);
                // The read to the block's end, and the prelude's and the
                // body's copies.
                Scanned((k - i) + (3L * (open - i)) + (bodyEnd - open));
                string prelude = text[i..open];
                int semicolon = prelude.LastIndexOf(';');
                prelude = (semicolon >= 0 ? prelude[(semicolon + 1)..] : prelude).Trim();
                if (prelude.StartsWith("@media", StringComparison.OrdinalIgnoreCase))
                {
                    Scanned(bodyEnd - open);
                    if (text.AsSpan(open + 1, bodyEnd - (open + 1)).Contains('{'))
                    {
                        blocks.Push((open + 1, bodyEnd));
                    }
                }
                else if (prelude.StartsWith('@'))
                {
                    found.Add(new StyleRule(null, []));
                }
                else
                {
                    found.Add(new StyleRule(prelude, Declarations(text[(open + 1)..bodyEnd])));
                }
                i = k;
            }
        }
        return found;
    }

    /// <summary>
    /// #1610 (Codex's review, stage 4): the <c>url()</c> targets each custom
    /// property can resolve to. A property reaches the ids its definitions'
    /// values name in their own text (a <c>var()</c> fallback's at any
    /// depth among them, which Svg.Custom resolves when a lookup fails) and
    /// those every property it names reaches, through any chain. Properties
    /// that name each other in a cycle form one component, so a cycle of
    /// names is one node of Measure's graph, never a render cycle.
    /// Components are numbered so that each names only lower ones.
    /// </summary>
    private sealed class VarReach
    {
        private readonly Dictionary<string, int> names = new(StringComparer.Ordinal);
        private readonly int[] component;

        internal VarReach(IEnumerable<(string Name, string Value)> definitions, GuardSteps steps)
        {
            var urls = new List<List<string>>();
            var vertices = new List<int>();
            var values = new List<(int Name, string Value)>();
            foreach ((string name, string value) in definitions)
            {
                steps.Spend(1);
                if (!names.TryGetValue(name, out int id))
                {
                    names[name] = id = names.Count;
                    urls.Add([]);
                    vertices.Add(0);
                }
                List<string> named = UrlsIn(value);
                steps.Spend(named.Count);
                urls[id].AddRange(named);
                Scanned(value.Length);
                if (value.Contains("path(", StringComparison.OrdinalIgnoreCase))
                {
                    vertices[id] = Math.Max(vertices[id], CssPathVertices(value));
                }
                values.Add((id, value));
            }
            int count = names.Count;
            var refs = new List<int>[count];
            for (int id = 0; id < count; id++)
            {
                refs[id] = [];
            }
            foreach ((int id, string value) in values)
            {
                List<string> named = NamesIn(value);
                steps.Spend(named.Count);
                foreach (string other in named)
                {
                    if (names.TryGetValue(other, out int target))
                    {
                        refs[id].Add(target);
                    }
                }
            }
            component = Components(refs, steps, out int components);
            Count = components;
            Urls = new List<string>[components];
            Named = new List<int>[components];
            PathVertices = new int[components];
            var urlSets = new HashSet<string>[components];
            var namedSets = new HashSet<int>[components];
            for (int c = 0; c < components; c++)
            {
                urlSets[c] = new HashSet<string>(StringComparer.Ordinal);
                namedSets[c] = [];
                Urls[c] = [];
                Named[c] = [];
            }
            for (int id = 0; id < count; id++)
            {
                int c = component[id];
                PathVertices[c] = Math.Max(PathVertices[c], vertices[id]);
                foreach (string url in urls[id])
                {
                    if (urlSets[c].Add(url))
                    {
                        Urls[c].Add(url);
                    }
                }
                foreach (int target in refs[id])
                {
                    int other = component[target];
                    if (other != c && namedSets[c].Add(other))
                    {
                        Named[c].Add(other);
                    }
                }
            }
            for (int c = 0; c < components; c++)
            {
                steps.Spend(1 + Named[c].Count);
                foreach (int other in Named[c])
                {
                    PathVertices[c] = Math.Max(PathVertices[c], PathVertices[other]);
                }
            }
        }

        /// <summary>The most CSS <c>path()</c> vertices a <c>d</c> value
        /// gives: its own text's, a fallback's among them, and those of
        /// every custom property it names.</summary>
        internal int PathVerticesOf(string value)
        {
            int most = CssPathVertices(value);
            foreach (string name in NamesIn(value))
            {
                if (ComponentOf(name) is var c and >= 0)
                {
                    most = Math.Max(most, PathVertices[c]);
                }
            }
            return most;
        }

        internal int Count { get; }

        /// <summary>Each component's most CSS <c>path()</c> vertices, in its
        /// definitions' own text or any component it names (Codex's review,
        /// stage 4).</summary>
        internal int[] PathVertices { get; }

        /// <summary>Each component's ids, in its definitions' own text.</summary>
        internal List<string>[] Urls { get; }

        /// <summary>The other components each component names, all lower
        /// than it.</summary>
        internal List<int>[] Named { get; }

        internal int ComponentOf(string name) =>
            names.TryGetValue(name, out int id) ? component[id] : -1;

        /// <summary>Tarjan's strongly connected components, with an explicit
        /// stack: a component is numbered once every component it reaches
        /// is.</summary>
        private static int[] Components(List<int>[] refs, GuardSteps steps, out int components)
        {
            int count = refs.Length;
            var index = new int[count];
            var low = new int[count];
            var onStack = new bool[count];
            var found = new int[count];
            Array.Fill(index, -1);
            var open = new Stack<int>();
            var calls = new Stack<(int Node, int Edge)>();
            int next = 0;
            components = 0;
            for (int root = 0; root < count; root++)
            {
                if (index[root] >= 0)
                {
                    continue;
                }
                index[root] = low[root] = next++;
                open.Push(root);
                onStack[root] = true;
                calls.Push((root, 0));
                while (calls.TryPop(out (int Node, int Edge) call))
                {
                    steps.Spend(1);
                    int node = call.Node;
                    if (call.Edge < refs[node].Count)
                    {
                        calls.Push((node, call.Edge + 1));
                        int target = refs[node][call.Edge];
                        if (index[target] < 0)
                        {
                            index[target] = low[target] = next++;
                            open.Push(target);
                            onStack[target] = true;
                            calls.Push((target, 0));
                        }
                        else if (onStack[target])
                        {
                            low[node] = Math.Min(low[node], index[target]);
                        }
                        continue;
                    }
                    if (low[node] == index[node])
                    {
                        int member;
                        do
                        {
                            member = open.Pop();
                            onStack[member] = false;
                            found[member] = components;
                        }
                        while (member != node);
                        components++;
                    }
                    if (calls.TryPeek(out (int Node, int Edge) parent))
                    {
                        low[parent.Node] = Math.Min(low[parent.Node], low[node]);
                    }
                }
            }
            return found;
        }
    }

    /// <summary>The <c>name: value</c> declarations of a declaration block,
    /// split at the semicolons outside strings and brackets.</summary>
    private static List<(string Name, string Value)> Declarations(string block)
    {
        var declarations = new List<(string Name, string Value)>();
        foreach (string declaration in SplitTop(block, ';'))
        {
            Scanned(2L * declaration.Length);
            int colon = declaration.IndexOf(':', StringComparison.Ordinal);
            if (colon > 0)
            {
                declarations.Add((declaration[..colon].Trim(), declaration[(colon + 1)..].Trim()));
            }
        }
        return declarations;
    }

    /// <summary>The trimmed, non-empty pieces of <paramref name="text"/>
    /// between the separators outside strings and brackets.</summary>
    private static List<string> SplitTop(string text, char separator)
    {
        Scanned(text.Length);
        var pieces = new List<string>();
        int depth = 0;
        int start = 0;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c is '"' or '\'')
            {
                i = CssStringEnd(text, i) - 1;
            }
            else if (c is '(' or '[')
            {
                depth++;
            }
            else if (c is ')' or ']')
            {
                depth = Math.Max(0, depth - 1);
            }
            else if (c == separator && depth == 0)
            {
                AddPiece(text, start, i, pieces);
                start = i + 1;
            }
        }
        AddPiece(text, start, text.Length, pieces);
        return pieces;
    }

    private static void AddPiece(string text, int start, int end, List<string> pieces)
    {
        Scanned(end - start);
        string piece = text[start..end].Trim();
        if (piece.Length > 0)
        {
            pieces.Add(piece);
        }
    }

    /// <summary>Just past the CSS string opening at <paramref name="open"/>:
    /// past its closing quote, or at the line or text end that leaves it
    /// open.</summary>
    private static int CssStringEnd(string text, int open)
    {
        int end = StringEnd(text, open);
        return end < text.Length && text[end] == text[open] ? end + 1 : end;
    }

    /// <summary>
    /// #1610 (CB-29): the time Svg.Custom and Svg.Skia would take to decode a
    /// document every other check admits, from its shape and Measure's graph,
    /// summed from the four costs CB-29's step 0 measured, at
    /// <see cref="SvgDecodeRates"/>:
    /// <list type="bullet">
    /// <item>the stylesheet: each selector branch queries every element
    /// once per pass, plus the follow-up's measured counts for descendant
    /// chains, general siblings and of-type pseudo-classes, whose queries are
    /// not linear; each declaration lands on every element its rule is filed
    /// under, and each rule is parsed on its own;</item>
    /// <item>under any <c>&lt;style&gt;</c>, each use render re-applies the
    /// stylesheet to the whole document and to its instance;</item>
    /// <item>each lookup a <c>var()</c> resolution makes, which Svg.Custom
    /// does not memoise;</item>
    /// <item>each marker placement, which compiles the marker's subtree
    /// again.</item>
    /// </list>
    /// Every count errs wide where the guard cannot know what applies. It is
    /// computed iteratively and saturates; nothing recurses. Its work is
    /// linear in the document for the shapes the red team made super-linear
    /// (a rule's var() uses are counted once per rule, a reference reaches
    /// an id's elements through one node), the <c>var()</c> table takes 16
    /// lookup levels per custom property, and the selector counts read a
    /// table of choices built once. Each step is spent from
    /// <see cref="MaxGuardSteps"/>, which ends whatever is left.
    /// </summary>
    private sealed class DecodeCostEstimator(
        DocumentShape document, string source, XmlReaderSettings settings, GuardSteps steps)
    {
        /// <summary>Where every count stops growing.</summary>
        private const double Saturated = 1e18;

        /// <summary>The deepest lookup Svg.Custom makes: it resolves a
        /// property's value at twice its depth and gives up past 32.</summary>
        private const int VarLevels = 16;

        private const int Slots = 3;

        private static readonly HashSet<string> GeometryProperties = new(StringComparer.OrdinalIgnoreCase)
        {
            "cx", "cy", "d", "height", "r", "rx", "ry", "transform-box", "transform-origin",
            "width", "x", "x1", "x2", "y", "y1", "y2",
        };

        private readonly Dictionary<string, KeyStats> keys = new(StringComparer.Ordinal);
        private readonly List<SelectorBranch> selectorBranches = [];
        private readonly List<(List<string> Values, List<KeyStats> Subjects)> ruleVarUses = [];
        private readonly List<(string Name, string Value)> ruleDefinitions = [];
        private readonly HashSet<string> attributeNames = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> widestById = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> widestThroughVar = new(ReferenceEqualityComparer.Instance);
        private int[]? widestByComponent;
        private readonly Dictionary<int, double[]> choiceRows = [];
        private double queriedPasses;
        private int queriedRules;
        private double[]? ofTypeShare;
        private double[]? ownMatched;
        private double[]? placements;
        private int rules;
        private double[] renders = [];

        internal SvgDecodeEstimate Estimate()
        {
            int elements = document.Names.Count;
            ReadRules();
            renders = Renders();
            Family family = Families(elements);
            foreach (SelectorBranch branch in selectorBranches)
            {
                foreach (KeyStats key in branch.OfType ?? [])
                {
                    key.OfTypeWeight += branch.Passes * SvgDecodeRates.OfTypeSiblingRead;
                }
            }
            double markerUnits = VisitElements(family, ReadAttributeKeys());
            return new SvgDecodeEstimate(
                Cap(StylesheetMicroseconds(elements, family)),
                Cap(UseMicroseconds(elements, family)),
                Cap(SvgDecodeRates.VarCopy * VarLookups()),
                Cap(SvgDecodeRates.MarkerUnit * markerUnits));
        }

        private static double Cap(double value) => value < Saturated ? value : Saturated;

        private KeyStats KeyOf(string key)
        {
            if (!keys.TryGetValue(key, out KeyStats? stats))
            {
                keys[key] = stats = new KeyStats();
            }
            return stats;
        }

        // ------------------------------------------------------------------
        // The stylesheet
        // ------------------------------------------------------------------

        /// <summary>Reads every rule <see cref="DocumentShape.Rules"/>
        /// holds.</summary>
        private void ReadRules()
        {
            foreach (StyleRule rule in document.Rules)
            {
                if (rule.Prelude is null)
                {
                    rules++;
                    continue;
                }
                ReadRule(rule.Prelude, rule.Declarations);
            }
        }

        private void ReadRule(string prelude, List<(string Name, string Value)> read)
        {
            rules++;
            List<(string Name, string Value)> declarations = Distinct(read);
            List<string> branches = SplitTop(prelude, ',');
            steps.Spend(declarations.Count + branches.Count);
            if (declarations.Count == 0 || branches.Count == 0)
            {
                return;
            }
            queriedRules++;
            bool custom = false;
            bool geometry = false;
            double landing = 0;
            List<string>? varValues = null;
            int pathVertices = 0;
            List<MarkerDeclaration>? markers = null;
            foreach ((string name, string value) in declarations)
            {
                Scanned((2L * name.Length) + value.Length);
                bool definition = name.StartsWith("--", StringComparison.Ordinal);
                bool usesVar = value.Contains("var(", StringComparison.OrdinalIgnoreCase);
                custom |= definition;
                geometry |= GeometryProperties.Contains(name);
                landing += definition || usesVar
                    ? SvgDecodeRates.MatchedCustomDeclaration
                    : SvgDecodeRates.MatchedDeclaration;
                if (definition)
                {
                    ruleDefinitions.Add((name, value));
                    continue;
                }
                if (usesVar)
                {
                    (varValues ??= []).Add(value);
                }
                if (name.Equals("d", StringComparison.OrdinalIgnoreCase))
                {
                    pathVertices = Math.Max(pathVertices, document.Reach?.PathVerticesOf(value) ?? CssPathVertices(value));
                }
                if (MarkerSlots(name) is { } slots
                    && ReadMarkerValue(value, out string? target, out string? throughVar))
                {
                    (markers ??= []).Add(new MarkerDeclaration(-1, slots, target, throughVar));
                }
            }
            double passes = 1 + (custom ? 1 : 0) + (geometry ? 1 : 0);
            List<KeyStats>? subjects = varValues is null ? null : new(branches.Count);
            foreach (string branch in branches)
            {
                ParsedBranch parsed = ReadBranch(branch);
                KeyStats subject = parsed.Subject;
                subject.DeclarationMicroseconds += landing;
                subject.PathVertices = Math.Max(subject.PathVertices, pathVertices);
                if (markers is not null)
                {
                    (subject.Markers ??= []).AddRange(markers);
                }
                subjects?.Add(subject);
                queriedPasses += passes * (1 + parsed.Walks);
                if (parsed.Descendants > 0 || parsed.Siblings > 0 || parsed.OfType is not null)
                {
                    selectorBranches.Add(new SelectorBranch(
                        passes, parsed.Descendants, parsed.Siblings, parsed.OfType, parsed.Left));
                }
            }
            // One entry per rule (the red team's B1): its var() values
            // resolve once each, on every element its branches land on.
            if (varValues is not null)
            {
                ruleVarUses.Add((varValues, subjects!));
            }
        }

        /// <summary>A declaration block keeps one declaration of each
        /// property, the last (the follow-up measured 1,000 fills in one rule
        /// over 10,000 elements at 70 ms).</summary>
        private static List<(string Name, string Value)> Distinct(List<(string Name, string Value)> declarations)
        {
            if (declarations.Count < 2)
            {
                return declarations;
            }
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var kept = new List<(string Name, string Value)>(declarations.Count);
            for (int i = declarations.Count - 1; i >= 0; i--)
            {
                string name = declarations[i].Name;
                Scanned(2L * name.Length);
                if (seen.Add(name.StartsWith("--", StringComparison.Ordinal) ? name : name.ToLowerInvariant()))
                {
                    kept.Add(declarations[i]);
                }
            }
            return kept;
        }

        /// <summary>
        /// A selector branch: the key its subject (the rightmost compound) is
        /// filed under, its descendant steps, its general-sibling steps, its
        /// child and adjacent-sibling steps (each one more walk of the
        /// document, which reads one level or one sibling per element, the
        /// red team's m1), the keys of the compounds carrying an of-type
        /// pseudo-class (or one whose position counts from the end), which
        /// recomputes the parent's children per element, or null for none,
        /// and, for a branch of one descendant or general-sibling step, the
        /// key its left compound is filed under (the red team's m2).
        /// </summary>
        private ParsedBranch ReadBranch(string branch)
        {
            // The read, and each compound's copy.
            Scanned(2L * branch.Length);
            int descendants = 0;
            int siblings = 0;
            int walks = 0;
            int compounds = 0;
            string? first = null;
            List<KeyStats>? ofType = null;
            string? previous = null;
            char combinator = '\0';
            int depth = 0;
            int start = -1;
            for (int i = 0; i <= branch.Length; i++)
            {
                char c = i < branch.Length ? branch[i] : ' ';
                if (start >= 0
                    && (i == branch.Length || (depth == 0 && (char.IsWhiteSpace(c) || c is '>' or '+' or '~'))))
                {
                    steps.Spend(1);
                    string compound = branch[start..i];
                    if (previous is not null)
                    {
                        descendants += combinator == '\0' ? 1 : 0;
                        siblings += combinator == '~' ? 1 : 0;
                        walks += combinator is '>' or '+' ? 1 : 0;
                    }
                    if (RecomputesFamily(compound))
                    {
                        (ofType ??= []).Add(KeyOf(CompoundKey(compound)));
                    }
                    first ??= compound;
                    compounds++;
                    previous = compound;
                    combinator = '\0';
                    start = -1;
                }
                if (i == branch.Length)
                {
                    break;
                }
                if (start < 0 && depth == 0)
                {
                    if (c is '>' or '+' or '~')
                    {
                        combinator = c;
                        continue;
                    }
                    if (char.IsWhiteSpace(c))
                    {
                        continue;
                    }
                    start = i;
                }
                if (c is '"' or '\'')
                {
                    i = CssStringEnd(branch, i) - 1;
                }
                else if (c is '(' or '[')
                {
                    depth++;
                }
                else if (c is ')' or ']')
                {
                    depth = Math.Max(0, depth - 1);
                }
            }
            KeyStats? left = compounds == 2 && walks == 0 ? KeyOf(CompoundKey(first!)) : null;
            return new ParsedBranch(
                KeyOf(CompoundKey(previous ?? AnyElementKey)), descendants, siblings, walks, ofType, left);
        }

        private readonly record struct ParsedBranch(
            KeyStats Subject, int Descendants, int Siblings, int Walks, List<KeyStats>? OfType, KeyStats? Left);

        /// <summary>Whether a compound carries a pseudo-class that re-reads
        /// the parent's children for each element it tests: the of-type
        /// ones (measured), and, unmeasured, those counting from the end.
        /// nth-child and first-child read an index and stay linear (the
        /// follow-up measured nth-child).</summary>
        private static bool RecomputesFamily(string compound) =>
            Charged(5L * compound.Length)
            && compound.Contains(':', StringComparison.Ordinal)
            && (compound.Contains("of-type", StringComparison.OrdinalIgnoreCase)
                || compound.Contains(":nth-last-", StringComparison.OrdinalIgnoreCase)
                || compound.Contains(":last-child", StringComparison.OrdinalIgnoreCase)
                || compound.Contains(":only-child", StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// The key a compound selector is filed under: its id, else its first
        /// class, else its type, else its first attribute (<c>[name=value]</c>
        /// for an exact match, <c>[name]</c> for any other), else
        /// <c>*</c>; lower-cased but for an attribute's value, so a match
        /// only ever widens. Anything the guard cannot read files it under
        /// <c>*</c>.
        /// </summary>
        private string CompoundKey(string compound)
        {
            Scanned(3L * compound.Length);
            int i = 0;
            string? type = null;
            string? firstClass = null;
            string? id = null;
            string? attribute = null;
            if (compound.StartsWith('*'))
            {
                i = 1;
            }
            else if (compound.Length > 0 && IsNameCharacter(compound[0]))
            {
                type = ReadName(compound, ref i);
            }
            while (i < compound.Length)
            {
                char marker = compound[i++];
                switch (marker)
                {
                    case '.' or '#':
                        {
                            string name = ReadName(compound, ref i);
                            if (name.Length == 0)
                            {
                                return AnyElementKey;
                            }
                            if (marker == '.')
                            {
                                firstClass ??= name;
                            }
                            else
                            {
                                id = name;
                            }
                            break;
                        }
                    case '[':
                        {
                            int close = i;
                            while (close < compound.Length && compound[close] != ']')
                            {
                                close = compound[close] is '"' or '\'' ? CssStringEnd(compound, close) : close + 1;
                            }
                            attribute ??= AttributeKey(compound[i..Math.Min(close, compound.Length)]);
                            i = close + 1;
                            break;
                        }
                    case ':':
                        {
                            if (i < compound.Length && compound[i] == ':')
                            {
                                i++;
                            }
                            ReadName(compound, ref i);
                            if (i < compound.Length && compound[i] == '(')
                            {
                                int nesting = 0;
                                do
                                {
                                    if (compound[i] is '"' or '\'')
                                    {
                                        i = CssStringEnd(compound, i);
                                        continue;
                                    }
                                    nesting += compound[i] == '(' ? 1 : compound[i] == ')' ? -1 : 0;
                                    i++;
                                }
                                while (i < compound.Length && nesting > 0);
                            }
                            break;
                        }
                    default:
                        return AnyElementKey;
                }
            }
            string? attributeName = attribute is null ? null : AttributeName(attribute);
            string key = id is not null ? "#" + id.ToLowerInvariant()
                : firstClass is not null ? "." + firstClass.ToLowerInvariant()
                : type is not null ? type.ToLowerInvariant()
                : attribute ?? AnyElementKey;
            if (key == attribute && attributeName is not null)
            {
                attributeNames.Add(attributeName);
            }
            return key;
        }

        /// <summary><c>[name=value]</c> for an exact match, <c>[name]</c>
        /// for any other, or null when the name cannot be read.</summary>
        private static string? AttributeKey(string inside)
        {
            Scanned(4L * inside.Length);
            string text = inside.Trim();
            int i = 0;
            while (i < text.Length && (IsNameCharacter(text[i]) || text[i] is ':' or '.'))
            {
                i++;
            }
            if (i == 0)
            {
                return null;
            }
            string name = text[..i].ToLowerInvariant();
            string rest = text[i..].TrimStart();
            if (rest.Length == 0)
            {
                return "[" + name + "]";
            }
            if (rest[0] != '=')
            {
                return rest.Length > 1 && rest[0] is '~' or '|' or '^' or '$' or '*' && rest[1] == '='
                    ? "[" + name + "]"
                    : null;
            }
            // Anything after the value (a case flag) files it under the
            // attribute alone: errs wide.
            string value = rest[1..].Trim();
            if (value.Length > 0 && value[0] is '"' or '\'')
            {
                int close = value.IndexOf(value[0], 1);
                return close > 0 && value[(close + 1)..].Trim().Length == 0
                    ? "[" + name + "=" + value[1..close] + "]"
                    : "[" + name + "]";
            }
            return value.Length == 0 || value.Any(char.IsWhiteSpace)
                ? "[" + name + "]"
                : "[" + name + "=" + value + "]";
        }

        private static string? AttributeName(string key)
        {
            int end = key.IndexOf('=', StringComparison.Ordinal);
            return key[1..(end < 0 ? key.Length - 1 : end)];
        }

        /// <summary>
        /// Every branch's query walks every element once per pass, and once
        /// more for each child or adjacent-sibling step. A branch whose
        /// query is not linear (#1610, Design 1b, with the follow-up's
        /// measured counts) adds, per pass: for its descendant steps, the
        /// depth bound; for its general-sibling steps, the sibling-list
        /// reads; for each compound with an of-type pseudo-class, the
        /// sibling-list sizes of the elements filed under its key. A branch
        /// of one such step counts only what its left compound's matches
        /// read (the red team's m2): their descendants, or their sibling
        /// lists. Then the declarations each key's elements take, and each
        /// rule.
        /// </summary>
        private double StylesheetMicroseconds(int elements, Family family)
        {
            var depthBounds = new Dictionary<int, double>();
            var siblingReads = new Dictionary<int, double>();
            double selectors = 0;
            foreach (SelectorBranch branch in selectorBranches)
            {
                steps.Spend(1);
                double work = 0;
                if (branch.Descendants > 0)
                {
                    if (!depthBounds.TryGetValue(branch.Descendants, out double bound))
                    {
                        depthBounds[branch.Descendants] = bound = DepthBound(family, branch.Descendants);
                    }
                    if (branch.Left is { } left)
                    {
                        bound = Math.Min(bound, left.DescendantSum);
                    }
                    work = Cap(work + (SvgDecodeRates.DescendantChainUnit * bound));
                }
                if (branch.Siblings > 0)
                {
                    if (!siblingReads.TryGetValue(branch.Siblings, out double reads))
                    {
                        siblingReads[branch.Siblings] = reads = SiblingReads(family, branch.Siblings);
                    }
                    if (branch.Left is { } left)
                    {
                        reads = Math.Min(reads, left.FamilySum);
                    }
                    work = Cap(work + (SvgDecodeRates.SiblingListRead * reads));
                }
                foreach (KeyStats key in branch.OfType ?? [])
                {
                    work = Cap(work + (SvgDecodeRates.OfTypeSiblingRead * key.FamilySum));
                }
                selectors = Cap(selectors + (branch.Passes * work));
            }
            double matched = 0;
            foreach (KeyStats key in keys.Values)
            {
                matched = Cap(matched + (key.Count * key.DeclarationMicroseconds));
            }
            return Cap(SvgDecodeRates.QueryPerBranchElement * queriedPasses * elements)
                + selectors
                + matched
                + (SvgDecodeRates.Rule * rules);
        }

        /// <summary>
        /// The follow-up's depth bound for a branch of
        /// <paramref name="steps"/> descendant steps: over every element, the
        /// ways to pick 1 to <paramref name="steps"/> of its ancestors, the
        /// work ExCSS's composed query does when every ancestor matches every
        /// compound (duplicates are removed only at the end). Elements are
        /// counted by depth, so it reads at most 256 rows of
        /// <see cref="Choices"/>.
        /// </summary>
        private double DepthBound(Family family, int chain)
        {
            steps.Spend(family.AtDepth.Length);
            double total = 0;
            for (int depth = 1; depth < family.AtDepth.Length; depth++)
            {
                if (family.AtDepth[depth] > 0)
                {
                    total = Cap(total + (family.AtDepth[depth] * Choices(depth, chain)));
                }
            }
            return total;
        }

        /// <summary>
        /// The follow-up's sibling-list reads for a branch of
        /// <paramref name="steps"/> general-sibling steps: each step reads
        /// the whole sibling list for every current match, duplicates
        /// included, so a parent of n children costs n x the ways to pick 1 to
        /// <paramref name="steps"/> of them when every child matches.
        /// </summary>
        private double SiblingReads(Family family, int chain)
        {
            steps.Spend(family.Parents.Count);
            double total = 0;
            foreach ((int children, int parents) in family.Parents)
            {
                total = Cap(total + ((double)parents * children * Choices(children, chain)));
            }
            return total;
        }

        /// <summary>C(n, 1) + ... + C(n, k), saturating, read from a row of
        /// sums per n built once, up to where it saturates: no row holds more
        /// than 64 sums, and any number of branches reads the same
        /// rows.</summary>
        private double Choices(int n, int k)
        {
            if (n <= 0 || k <= 0)
            {
                return 0;
            }
            if (!choiceRows.TryGetValue(n, out double[]? row))
            {
                var sums = new List<double>();
                double sum = 0;
                double term = 1;
                for (int s = 1; s <= n && sum < Saturated; s++)
                {
                    term = term * (n - s + 1) / s;
                    sum = Cap(sum + term);
                    sums.Add(sum);
                }
                steps.Spend(sums.Count);
                choiceRows[n] = row = [.. sums];
            }
            return row[Math.Min(k, row.Length) - 1];
        }

        /// <summary>The family counts the selector work reads: how many
        /// elements sit at each depth (proper ancestors, the root at 0), how
        /// many parents have each number of children, each element's
        /// sibling-list size (its parent's children, the root's 0) and its
        /// descendants.</summary>
        private Family Families(int elements)
        {
            steps.Spend(elements);
            IReadOnlyList<int> parents = document.Parents;
            var depths = new int[elements];
            var children = new int[elements];
            var atDepth = new double[MaxRenderDepth + 1];
            for (int i = 0; i < elements; i++)
            {
                int parent = parents[i];
                if (parent >= 0)
                {
                    depths[i] = depths[parent] + 1;
                    children[parent]++;
                }
                atDepth[Math.Min(depths[i], MaxRenderDepth)]++;
            }
            var siblings = new double[elements];
            var descendants = new double[elements];
            var families = new Dictionary<int, int>();
            for (int i = 0; i < elements; i++)
            {
                if (parents[i] >= 0)
                {
                    siblings[i] = children[parents[i]];
                }
                if (children[i] > 0)
                {
                    families[children[i]] = families.GetValueOrDefault(children[i]) + 1;
                }
            }
            // Children follow their parent in document order.
            for (int i = elements - 1; i >= 0; i--)
            {
                if (parents[i] >= 0)
                {
                    descendants[parents[i]] += descendants[i] + 1;
                }
            }
            return new Family(atDepth, families, siblings, descendants, depths);
        }

        /// <summary>Elements by depth, parents by their number of children
        /// (children, parents), and each element's sibling-list size and
        /// descendants.</summary>
        private sealed record Family(
            double[] AtDepth, Dictionary<int, int> Parents, double[] Siblings, double[] Descendants, int[] Depths);

        // ------------------------------------------------------------------
        // Elements: their keys, and the markers they place
        // ------------------------------------------------------------------

        /// <summary>The second read (#1610, Design 2): when a rule is filed
        /// under an attribute, the elements that carry the attributes those
        /// keys name, in document order.</summary>
        private List<(int Element, KeyStats Key)> ReadAttributeKeys()
        {
            var found = new List<(int Element, KeyStats Key)>();
            if (attributeNames.Count == 0)
            {
                return found;
            }
            Scanned(source.Length);
            using var textReader = new StringReader(source);
            using XmlReader reader = XmlReader.Create(textReader, settings);
            int index = -1;
            while (reader.Read())
            {
                steps.Spend(1);
                if (reader.NodeType != XmlNodeType.Element)
                {
                    continue;
                }
                index++;
                if (!reader.MoveToFirstAttribute())
                {
                    continue;
                }
                do
                {
                    steps.Spend(1);
                    string name = reader.LocalName.ToLowerInvariant();
                    Scanned(2L * name.Length);
                    if (!attributeNames.Contains(name))
                    {
                        continue;
                    }
                    if (keys.TryGetValue("[" + name + "]", out KeyStats? present))
                    {
                        found.Add((index, present));
                    }
                    Scanned(2L * reader.Value.Length);
                    if (keys.TryGetValue("[" + name + "=" + reader.Value + "]", out KeyStats? exact))
                    {
                        found.Add((index, exact));
                    }
                }
                while (reader.MoveToNextAttribute());
                reader.MoveToElement();
            }
            return found;
        }

        /// <summary>
        /// One walk in document order: each element is counted under each
        /// key it carries (every element, the descriptive ones too: a rule's
        /// query walks them all and applies its declarations to each match),
        /// and its own marker in each slot is found (from its attributes,
        /// its style attribute or the rules filed under its keys, the one
        /// with the most renders). A path's vertices are the most its data
        /// or any CSS <c>d:path()</c> filed under its keys or read through
        /// <c>var()</c> gives (the red team's m4, Codex's review, stage 4).
        /// Then one walk in Measure's order, in which an element's parent
        /// and every use that draws it come first: its marker in each slot
        /// is its own, else the widest its parent's and those of the uses
        /// drawing it (an instance inherits from its use, Codex's review,
        /// stage 4), and each marked shape adds its placements.
        /// </summary>
        private double VisitElements(Family family, List<(int Element, KeyStats Key)> attributeKeys)
        {
            int elements = document.Names.Count;
            double[] siblings = family.Siblings;
            double[] descendants = family.Descendants;

            foreach (KeyStats key in keys.Values)
            {
                steps.Spend(1 + (key.Markers?.Count ?? 0));
                key.ResolveMarkers(this);
            }
            const int NotSet = -2;
            var own = new int[elements * Slots];
            var vertexCounts = new int[elements];
            var best = new int[Slots];
            var bestRenders = new double[Slots];
            var set = new bool[Slots];
            IReadOnlyList<MarkerDeclaration> declared = document.ElementMarkers;
            IReadOnlyList<(int Element, string Value)> pathVarUses = document.PathVarUses;
            int nextDeclaration = 0;
            int nextAttributeKey = 0;
            int nextPathVarUse = 0;
            bool anyKey = keys.Count > 0;
            for (int i = 0; i < elements; i++)
            {
                steps.Spend(1);
                Array.Fill(best, -1);
                Array.Fill(bestRenders, -1);
                Array.Clear(set);
                int vertexCount = document.Vertices[i];
                bool path = document.Names[i] == "path";
                while (nextPathVarUse < pathVarUses.Count && pathVarUses[nextPathVarUse].Element == i)
                {
                    steps.Spend(1);
                    vertexCount = Math.Max(
                        vertexCount, document.Reach?.PathVerticesOf(pathVarUses[nextPathVarUse++].Value) ?? 0);
                }
                void Candidate(int slot, int marker)
                {
                    set[slot] = true;
                    double markerRenders = marker >= 0 ? renders[marker] : 0;
                    if (markerRenders > bestRenders[slot])
                    {
                        bestRenders[slot] = markerRenders;
                        best[slot] = marker;
                    }
                }
                void Visit(string key)
                {
                    if (keys.TryGetValue(key, out KeyStats? stats))
                    {
                        VisitStats(stats);
                    }
                }
                void VisitStats(KeyStats stats)
                {
                    steps.Spend(1);
                    stats.Count++;
                    stats.FamilySum += siblings[i];
                    stats.DescendantSum += descendants[i];
                    if (stats.DeclarationMicroseconds > 0)
                    {
                        (ownMatched ??= new double[elements])[i] += stats.DeclarationMicroseconds;
                    }
                    if (stats.OfTypeWeight > 0)
                    {
                        (ofTypeShare ??= new double[elements])[i] += stats.OfTypeWeight * siblings[i];
                    }
                    if (path)
                    {
                        vertexCount = Math.Max(vertexCount, stats.PathVertices);
                    }
                    if (stats.MarkerSet is { } markerSet)
                    {
                        for (int slot = 0; slot < Slots; slot++)
                        {
                            if (markerSet[slot])
                            {
                                Candidate(slot, stats.MarkerBest![slot]);
                            }
                        }
                    }
                }
                while (nextDeclaration < declared.Count && declared[nextDeclaration].Element == i)
                {
                    MarkerDeclaration declaration = declared[nextDeclaration++];
                    int marker = Resolve(declaration);
                    for (int slot = 0; slot < Slots; slot++)
                    {
                        if ((declaration.Slots & (1 << slot)) != 0)
                        {
                            Candidate(slot, marker);
                        }
                    }
                }
                if (anyKey)
                {
                    Scanned((2L * document.Names[i].Length)
                        + (3L * ((document.Ids[i]?.Length ?? 0) + (document.ClassLists[i]?.Length ?? 0))));
                    Visit(AnyElementKey);
                    Visit(document.Names[i].ToLowerInvariant());
                    if (document.Ids[i] is { } id)
                    {
                        Visit("#" + id.ToLowerInvariant());
                    }
                    if (document.ClassLists[i] is { } classList)
                    {
                        foreach (string name in classList
                            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                            .Distinct(StringComparer.OrdinalIgnoreCase))
                        {
                            Visit("." + name.ToLowerInvariant());
                        }
                    }
                    while (nextAttributeKey < attributeKeys.Count && attributeKeys[nextAttributeKey].Element == i)
                    {
                        VisitStats(attributeKeys[nextAttributeKey++].Key);
                    }
                }
                for (int slot = 0; slot < Slots; slot++)
                {
                    own[(i * Slots) + slot] = set[slot] ? (bestRenders[slot] > 0 ? best[slot] : -1) : NotSet;
                }
                vertexCounts[i] = vertexCount;
            }

            var usesById = new Dictionary<string, List<int>>(StringComparer.Ordinal);
            foreach ((int use, string? target) in document.Uses)
            {
                if (target is not null)
                {
                    if (!usesById.TryGetValue(target, out List<int>? drawing))
                    {
                        usesById[target] = drawing = [];
                    }
                    drawing.Add(use);
                }
            }
            var fromUses = new Dictionary<string, int[]>(StringComparer.Ordinal);
            var effective = new int[elements * Slots];
            double units = 0;
            foreach (int i in document.Order)
            {
                if (i >= elements)
                {
                    continue;
                }
                steps.Spend(1);
                int parent = document.Parents[i];
                int[]? viaUses = null;
                if (document.Ids[i] is { } id && usesById.TryGetValue(id, out List<int>? drawing)
                    && !fromUses.TryGetValue(id, out viaUses))
                {
                    // Every use naming the id comes before every element it
                    // names in Measure's order.
                    viaUses = [-1, -1, -1];
                    steps.Spend(drawing.Count);
                    foreach (int use in drawing)
                    {
                        for (int slot = 0; slot < Slots; slot++)
                        {
                            viaUses[slot] = Wider(viaUses[slot], effective[(use * Slots) + slot]);
                        }
                    }
                    fromUses[id] = viaUses;
                }
                for (int slot = 0; slot < Slots; slot++)
                {
                    int mine = own[(i * Slots) + slot];
                    int inherited = parent >= 0 ? effective[(parent * Slots) + slot] : -1;
                    if (viaUses is not null)
                    {
                        inherited = Wider(inherited, viaUses[slot]);
                    }
                    effective[(i * Slots) + slot] = mine != NotSet ? mine : inherited;
                }
                units = Cap(units + Placements(i, vertexCounts[i], effective));
            }
            return units;
        }

        /// <summary>Of two markers (-1 for none), the one with the most
        /// renders.</summary>
        private int Wider(int first, int second) =>
            second >= 0 && (first < 0 || renders[second] > renders[first]) ? second : first;

        /// <summary>What a marked shape's placements weigh, times the
        /// shape's own renders: each placement its marker's renders and
        /// <see cref="SvgDecodeRates.MarkerPlacementWeight"/>, and the widest
        /// marker once more. Start and end are one placement each, mid every
        /// vertex between.</summary>
        private double Placements(int element, int vertexCount, int[] effective)
        {
            if (vertexCount <= 0)
            {
                return 0;
            }
            double placed = 0;
            double widest = 0;
            for (int slot = 0; slot < Slots; slot++)
            {
                int marker = effective[(element * Slots) + slot];
                if (marker < 0)
                {
                    continue;
                }
                double markerRenders = renders[marker];
                double positions = slot == 1 ? Math.Max(0, vertexCount - 2) : 1;
                placed += positions * (markerRenders + SvgDecodeRates.MarkerPlacementWeight);
                widest = Math.Max(widest, markerRenders);
                (placements ??= new double[renders.Length])[marker] =
                    Cap(placements[marker] + (positions * document.Paths[element]));
            }
            return widest == 0 ? 0 : Cap((placed + widest) * document.Paths[element]);
        }

        /// <summary>The element a marker declaration places: the element
        /// its id names, the widest marker a value read through
        /// <c>var()</c> can reach, or none.</summary>
        private int Resolve(MarkerDeclaration declaration) =>
            declaration.ThroughVar is { } value ? WidestThroughVar(value)
            : declaration.Target is { } target ? WidestWithId(target)
            : -1;

        /// <summary>The element an id names, or -1: of several elements
        /// sharing it, the one with the most renders (the red team's m4),
        /// found once per id.</summary>
        private int WidestWithId(string id)
        {
            if (widestById.TryGetValue(id, out int found))
            {
                return found;
            }
            int widest = -1;
            if (document.ElementsById.TryGetValue(id, out List<int>? named))
            {
                steps.Spend(named.Count);
                foreach (int element in named)
                {
                    if (widest < 0 || renders[element] > renders[widest])
                    {
                        widest = element;
                    }
                }
            }
            widestById[id] = widest;
            return widest;
        }

        /// <summary>
        /// The widest marker a marker property read through <c>var()</c> can
        /// place (#1610, the red team's M7 and Codex's review, stage 4):
        /// of every <c>url()</c> its own text names, a fallback's at any
        /// depth among them (Svg.Custom resolves a fallback when a lookup
        /// fails), and every one a custom property it names reaches, through
        /// any chain. Found once per value: a rule's value is shared by its
        /// branches.
        /// </summary>
        private int WidestThroughVar(string value)
        {
            if (widestThroughVar.TryGetValue(value, out int found))
            {
                return found;
            }
            int widest = -1;
            void Consider(int marker)
            {
                if (marker >= 0 && (widest < 0 || renders[marker] > renders[widest]))
                {
                    widest = marker;
                }
            }
            List<string> urls = UrlsIn(value);
            steps.Spend(1 + urls.Count);
            foreach (string id in urls)
            {
                Consider(WidestWithId(id));
            }
            if (document.Reach is { } reach)
            {
                int[] byComponent = WidestByComponent(reach);
                List<string> named = NamesIn(value);
                steps.Spend(named.Count);
                foreach (string name in named)
                {
                    if (reach.ComponentOf(name) is var c and >= 0)
                    {
                        Consider(byComponent[c]);
                    }
                }
            }
            widestThroughVar[value] = widest;
            return widest;
        }

        /// <summary>Each component of custom properties' widest reachable
        /// marker, lower components first.</summary>
        private int[] WidestByComponent(VarReach reach)
        {
            if (widestByComponent is { } done)
            {
                return done;
            }
            var widest = new int[reach.Count];
            for (int c = 0; c < reach.Count; c++)
            {
                int best = -1;
                steps.Spend(1 + reach.Urls[c].Count + reach.Named[c].Count);
                foreach (int marker in reach.Urls[c].Select(WidestWithId).Concat(reach.Named[c].Select(other => widest[other])))
                {
                    if (marker >= 0 && (best < 0 || renders[marker] > renders[best]))
                    {
                        best = marker;
                    }
                }
                widest[c] = best;
            }
            return widestByComponent = widest;
        }

        /// <summary>Each node's renders: itself (a key node is not drawn)
        /// and everything its edges reach, a node once per path, counted in
        /// the reverse of Measure's order.</summary>
        private double[] Renders()
        {
            int elements = document.Names.Count;
            List<int>?[] next = document.Next;
            var counts = new double[next.Length];
            IReadOnlyList<int> order = document.Order;
            for (int k = order.Count - 1; k >= 0; k--)
            {
                int node = order[k];
                double total = node < elements ? 1 : 0;
                steps.Spend(1 + (next[node]?.Count ?? 0));
                if (next[node] is { } outgoing)
                {
                    foreach (int to in outgoing)
                    {
                        total = Cap(total + counts[to]);
                    }
                }
                counts[node] = total;
            }
            return counts;
        }

        // ------------------------------------------------------------------
        // Uses
        // ------------------------------------------------------------------

        /// <summary>
        /// Under any <c>&lt;style&gt;</c>, each use render re-applies the
        /// stylesheet to its instance (Svg.Custom's scoped application,
        /// <c>ApplyScoped</c>, from the scene compiler's use node). One
        /// function gives the work of one render (<see cref="InstanceWork"/>)
        /// and one multiplier gives each use's renders
        /// (<see cref="Instantiations"/>), so every construct that clones
        /// content composes: a use drawn by another use, a use in a marker's
        /// content (compiled again at every placement), a use in a pattern
        /// (cloned for each element it paints, measured), and their
        /// nesting.
        /// </summary>
        private double UseMicroseconds(int elements, Family family)
        {
            if (!document.HasStyleElement || document.Uses.Count == 0)
            {
                return 0;
            }
            double[] instantiations = Instantiations();
            double[] subtreeWork = SubtreeInstanceWork(family);
            double perRender = SvgDecodeRates.UseRenderElement * (elements + (SvgDecodeRates.UseRenderRuleWeight * rules));
            double perInstanceElement = SvgDecodeRates.UseInstanceElement
                + (SvgDecodeRates.UseInstanceElementRule * rules)
                + (SvgDecodeRates.QueryPerBranchElement * Math.Max(0, queriedPasses - queriedRules));
            double total = 0;
            foreach ((int element, string? target) in document.Uses)
            {
                steps.Spend(1);
                double work = target is not null && WidestWithId(target) is var instance and >= 0
                    ? InstanceWork(instance, perRender, perInstanceElement, subtreeWork)
                    : perRender;
                total = Cap(total + (instantiations[element] * work));
            }
            return total;
        }

        /// <summary>
        /// The work of one use render over its instance rooted at
        /// <paramref name="instance"/> (Codex's rounds 2 and 3): the fitted
        /// per-render cost (the stylesheet over the document's elements and
        /// rules), the fitted per-element cost over the instance's renders,
        /// which covers one branch query of each rule, every further branch
        /// query over those elements, and the instance's own subtree's
        /// combinator work and matched declarations.
        /// </summary>
        private double InstanceWork(int instance, double perRender, double perInstanceElement, double[] subtreeWork) =>
            perRender + Cap(renders[instance] * perInstanceElement) + subtreeWork[instance];

        /// <summary>
        /// How many times each node is compiled (Codex's round 3): its
        /// renders as Measure counts them (once per path through nesting and
        /// references, a use's and a pattern's included), and, for a marker,
        /// once more per placement, carried down its content and through
        /// every reference the content makes, in Measure's order. Placements
        /// take the marked shape's renders, so a marker on a shape inside a
        /// use counts each instance; markers inside marker content are not
        /// multiplied again (departure D4: measured, not replayed).
        /// </summary>
        private double[] Instantiations()
        {
            List<int>?[] next = document.Next;
            var counts = new double[next.Length];
            for (int node = 0; node < counts.Length; node++)
            {
                counts[node] = document.Paths[node];
            }
            if (placements is null)
            {
                return counts;
            }
            var carried = new double[next.Length];
            for (int node = 0; node < placements.Length; node++)
            {
                carried[node] = placements[node];
            }
            foreach (int node in document.Order)
            {
                steps.Spend(1 + (next[node]?.Count ?? 0));
                if (carried[node] <= 0 || next[node] is not { } outgoing)
                {
                    continue;
                }
                foreach (int to in outgoing)
                {
                    carried[to] = Cap(carried[to] + carried[node]);
                }
            }
            for (int node = 0; node < counts.Length; node++)
            {
                counts[node] = Cap(counts[node] + carried[node]);
            }
            return counts;
        }
        /// <summary>
        /// Each element's subtree's work in a scoped application (Codex's
        /// rounds 2 and 3): an element's own is what the stylesheet term
        /// counts for it, its combinator work (the ways to pick 1 to k of its
        /// ancestors for each descendant branch, its sibling list's reads for
        /// each general-sibling branch, its sibling list for each of-type key
        /// it is filed under; a left compound's narrowing aside, which errs
        /// wide) and the declarations the rules filed under its keys land on
        /// it; a subtree's is the sum.
        /// </summary>
        private double[] SubtreeInstanceWork(Family family)
        {
            int elements = document.Names.Count;
            var descendantWeights = new Dictionary<int, double>();
            var siblingWeights = new Dictionary<int, double>();
            foreach (SelectorBranch branch in selectorBranches)
            {
                steps.Spend(1);
                if (branch.Descendants > 0)
                {
                    descendantWeights[branch.Descendants] = descendantWeights.GetValueOrDefault(branch.Descendants)
                        + (branch.Passes * SvgDecodeRates.DescendantChainUnit);
                }
                if (branch.Siblings > 0)
                {
                    siblingWeights[branch.Siblings] = siblingWeights.GetValueOrDefault(branch.Siblings)
                        + (branch.Passes * SvgDecodeRates.SiblingListRead);
                }
            }
            var share = new double[elements];
            for (int i = 0; i < elements; i++)
            {
                steps.Spend(1 + descendantWeights.Count + siblingWeights.Count);
                double own = (ofTypeShare?[i] ?? 0) + (ownMatched?[i] ?? 0);
                int depth = Math.Min(family.Depths[i], MaxRenderDepth);
                foreach ((int chain, double weight) in descendantWeights)
                {
                    own = Cap(own + (weight * Choices(depth, chain)));
                }
                int siblings = (int)family.Siblings[i];
                foreach ((int chain, double weight) in siblingWeights)
                {
                    own = Cap(own + (weight * Choices(siblings, chain)));
                }
                share[i] = own;
            }
            // Children follow their parent in document order.
            for (int i = elements - 1; i >= 0; i--)
            {
                if (document.Parents[i] is var parent and >= 0)
                {
                    share[parent] = Cap(share[parent] + share[i]);
                }
            }
            return share;
        }

        // ------------------------------------------------------------------
        // var()
        // ------------------------------------------------------------------

        /// <summary>
        /// The lookups Svg.Custom makes resolving every <c>var()</c> use: an
        /// attribute's or a style attribute's once, a rule's on each element
        /// its branches are filed under, those elements summed once per rule
        /// (the red team's B1). A property's value is resolved from left to
        /// right, each reference one level deeper; a lookup past
        /// <see cref="VarLevels"/> levels, or of a name nothing defines, costs
        /// one and fails, and a failed reference with a fallback resolves the
        /// fallback one level deeper (Svg.Custom does, after any failure),
        /// without one fails its value. A name defined more than once takes
        /// its widest definition. The table holds each property at each level,
        /// filled from the deepest level up, so nothing recurses and each
        /// value is resolved once per level.
        /// </summary>
        private double VarLookups()
        {
            if (document.AttributeVarUses.Count == 0 && ruleVarUses.Count == 0)
            {
                return 0;
            }
            var table = new VarTable(document.CustomDefinitions.Concat(ruleDefinitions), steps);
            double lookups = 0;
            foreach ((int _, string value) in document.AttributeVarUses)
            {
                lookups = Cap(lookups + table.Resolve(value));
            }
            foreach ((List<string> values, List<KeyStats> subjects) in ruleVarUses)
            {
                steps.Spend(subjects.Count);
                double landed = 0;
                foreach (KeyStats subject in subjects)
                {
                    landed += subject.Count;
                }
                foreach (string value in values)
                {
                    lookups = Cap(lookups + (table.Resolve(value) * landed));
                }
            }
            return lookups;
        }

        private sealed record SelectorBranch(
            double Passes, int Descendants, int Siblings, List<KeyStats>? OfType, KeyStats? Left);

        /// <summary>What the elements filed under one key add up to, and
        /// what the rules filed there apply to each.</summary>
        private sealed class KeyStats
        {
            internal double Count;
            internal double FamilySum;
            internal double DescendantSum;
            internal double OfTypeWeight;
            internal int PathVertices;
            internal double DeclarationMicroseconds;
            internal List<MarkerDeclaration>? Markers;
            internal bool[]? MarkerSet;
            internal int[]? MarkerBest;

            /// <summary>Each slot's widest marker among the rules filed
            /// here.</summary>
            internal void ResolveMarkers(DecodeCostEstimator estimator)
            {
                if (Markers is null)
                {
                    return;
                }
                MarkerSet = new bool[Slots];
                MarkerBest = [-1, -1, -1];
                var widest = new double[] { -1, -1, -1 };
                foreach (MarkerDeclaration declaration in Markers)
                {
                    int marker = estimator.Resolve(declaration);
                    double markerRenders = marker >= 0 ? estimator.renders[marker] : 0;
                    for (int slot = 0; slot < Slots; slot++)
                    {
                        if ((declaration.Slots & (1 << slot)) == 0)
                        {
                            continue;
                        }
                        MarkerSet[slot] = true;
                        if (markerRenders > widest[slot])
                        {
                            widest[slot] = markerRenders;
                            MarkerBest[slot] = marker;
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// #1610 (CB-29): the custom properties of a document, each resolved at
    /// each of Svg.Custom's lookup levels. A lookup has two outcomes, each
    /// with the most lookups it takes: it resolves, or it fails and its
    /// fallback is read. A name defined more than once keeps both outcomes
    /// of its alternatives (Codex's review, stage 4): which definition an
    /// element sees is the cascade's choice, so a literal defined anywhere
    /// never erases another definition's failure and the fallbacks that
    /// failure reads.
    /// </summary>
    private sealed class VarTable
    {
        private const int Levels = 16;

        private readonly Dictionary<string, int> names = new(StringComparer.Ordinal);
        private readonly List<int> tableRow = [];
        private readonly float[] resolveCost;
        private readonly float[] failCost;
        private readonly bool[] canResolve;
        private readonly bool[] canFail;
        private readonly List<Frame> frames = [];
        private readonly GuardSteps steps;

        internal VarTable(IEnumerable<(string Name, string Value)> definitions, GuardSteps steps)
        {
            this.steps = steps;
            List<(string Name, string Value)> all = [.. definitions];
            foreach ((string name, string _) in all)
            {
                names.TryAdd(name, names.Count);
            }
            // Parsed once every name has its id, since a value may name a
            // property defined after it. A row of the table per property
            // whose value holds var(); one defined only by literals resolves
            // in one lookup at every level.
            var parsed = new List<VarReference[]>[names.Count];
            for (int id = 0; id < parsed.Length; id++)
            {
                parsed[id] = [];
                tableRow.Add(-1);
            }
            int rows = 0;
            foreach ((string name, string value) in all)
            {
                int id = names[name];
                if (!value.Contains("var(", StringComparison.OrdinalIgnoreCase))
                {
                    parsed[id].Add(Literal);
                    continue;
                }
                parsed[id].Add(Parse(value));
                if (tableRow[id] < 0)
                {
                    tableRow[id] = rows++;
                }
            }
            resolveCost = new float[rows * (Levels + 1)];
            failCost = new float[rows * (Levels + 1)];
            canResolve = new bool[rows * (Levels + 1)];
            canFail = new bool[rows * (Levels + 1)];
            for (int level = Levels; level >= 1; level--)
            {
                for (int id = 0; id < parsed.Length; id++)
                {
                    steps.Spend(1 + parsed[id].Count);
                    int row = tableRow[id];
                    if (row < 0)
                    {
                        continue;
                    }
                    int at = (row * (Levels + 1)) + level;
                    foreach (VarReference[] references in parsed[id])
                    {
                        Outcome outcome = Evaluate(references, level);
                        if (outcome.Resolves)
                        {
                            canResolve[at] = true;
                            resolveCost[at] = Math.Max(resolveCost[at], (float)Math.Min(outcome.ResolveCost, 1e18));
                        }
                        if (outcome.Fails)
                        {
                            canFail[at] = true;
                            failCost[at] = Math.Max(failCost[at], (float)Math.Min(outcome.FailCost, 1e18));
                        }
                    }
                }
            }
        }

        private static readonly VarReference[] Literal = [];

        /// <summary>The lookups resolving a declaration's value takes, the
        /// more of its two outcomes.</summary>
        internal double Resolve(string value) => Evaluate(Parse(value), 0).Widest;

        /// <summary>A name's lookup at <paramref name="level"/>: past the
        /// deepest level, or for a name nothing defines, one lookup that
        /// fails; for a name defined only by literals, one that
        /// resolves.</summary>
        private Outcome Lookup(int name, int level)
        {
            if (name < 0 || level > Levels)
            {
                return new Outcome(false, 0, true, 1);
            }
            int row = tableRow[name];
            if (row < 0)
            {
                return new Outcome(true, 1, false, 0);
            }
            int at = (row * (Levels + 1)) + level;
            return new Outcome(canResolve[at], resolveCost[at], canFail[at], failCost[at]);
        }

        /// <summary>
        /// A value's references resolved at <paramref name="level"/>, from
        /// left to right, with an explicit stack for the fallbacks: a value
        /// with none is one lookup. A frame follows every outcome at once:
        /// the most lookups along which it goes on resolving, and the most
        /// along which it has failed.
        /// </summary>
        private Outcome Evaluate(VarReference[] references, int level)
        {
            if (references.Length == 0)
            {
                return new Outcome(true, 1, false, 0);
            }
            static double Cap(double value) => Math.Min(value, 1e18);
            frames.Clear();
            frames.Add(Frame.Start(0, references.Length, level));
            Outcome? delivered = null;
            while (true)
            {
                steps.Spend(1);
                int top = frames.Count - 1;
                Frame frame = frames[top];
                if (delivered is { } fallback)
                {
                    // A fallback ended; it served the failed reference at
                    // Position, whose lookup may also have resolved.
                    delivered = null;
                    if (fallback.Fails)
                    {
                        frame.Failed = true;
                        frame.FailedCost = Math.Max(frame.FailedCost, Cap(frame.FallbackBase + fallback.FailCost));
                    }
                    frame.Going = frame.PendingResolves || fallback.Resolves;
                    frame.GoingCost = Math.Max(
                        frame.PendingResolves ? frame.PendingResolveCost : 0,
                        fallback.Resolves ? Cap(frame.FallbackBase + fallback.ResolveCost) : 0);
                    frame.Position = references[frame.Position].End;
                    frames[top] = frame;
                    continue;
                }
                if (!frame.Going || frame.Position >= frame.End)
                {
                    var ended = new Outcome(frame.Going, frame.GoingCost, frame.Failed, frame.FailedCost);
                    frames.RemoveAt(top);
                    if (frames.Count == 0)
                    {
                        return ended;
                    }
                    delivered = ended;
                    continue;
                }
                VarReference reference = references[frame.Position];
                Outcome lookup = Lookup(reference.Name, frame.Level + 1);
                double resolved = Cap(frame.GoingCost + lookup.ResolveCost);
                double failed = Cap(frame.GoingCost + lookup.FailCost);
                if (!lookup.Fails)
                {
                    frame.GoingCost = resolved;
                    frame.Position = reference.End;
                }
                else if (!reference.HasFallback)
                {
                    frame.Failed = true;
                    frame.FailedCost = Math.Max(frame.FailedCost, failed);
                    frame.Going = lookup.Resolves;
                    frame.GoingCost = resolved;
                    frame.Position = reference.End;
                }
                else if (frame.Position + 1 == reference.End)
                {
                    // A fallback with no reference is a literal.
                    frame.GoingCost = Math.Max(lookup.Resolves ? resolved : 0, Cap(failed + 1));
                    frame.Position = reference.End;
                }
                else
                {
                    frame.PendingResolves = lookup.Resolves;
                    frame.PendingResolveCost = resolved;
                    frame.FallbackBase = failed;
                    frames[top] = frame;
                    frames.Add(Frame.Start(frame.Position + 1, reference.End, frame.Level + 1));
                    continue;
                }
                frames[top] = frame;
            }
        }

        /// <summary>A lookup's or a value's outcomes: whether it can
        /// resolve and the most lookups that takes, whether it can fail and
        /// the most lookups that takes.</summary>
        private readonly record struct Outcome(bool Resolves, double ResolveCost, bool Fails, double FailCost)
        {
            internal double Widest => Math.Max(Resolves ? ResolveCost : 0, Fails ? FailCost : 0);
        }
        /// <summary>The <c>var()</c> references of a value in document
        /// order, each with the end of the references its fallback
        /// holds.</summary>
        private VarReference[] Parse(string value)
        {
            Scanned(value.Length);
            var references = new List<VarReference>();
            var open = new Stack<(int Reference, int Depth)>();
            int depth = 0;
            for (int i = 0; i < value.Length; i++)
            {
                if (i + 4 <= value.Length && value.AsSpan(i, 4).Equals("var(", StringComparison.OrdinalIgnoreCase))
                {
                    depth++;
                    int start = i + 4;
                    while (start < value.Length && char.IsWhiteSpace(value[start]))
                    {
                        start++;
                    }
                    int end = start;
                    if (end + 1 < value.Length && value[end] == '-' && value[end + 1] == '-')
                    {
                        end += 2;
                        while (end < value.Length && IsVarNameCharacter(value[end]))
                        {
                            end++;
                        }
                    }
                    int name = end > start && names.TryGetValue(value[start..end], out int id) ? id : -1;
                    int after = end;
                    while (after < value.Length && char.IsWhiteSpace(value[after]))
                    {
                        after++;
                    }
                    bool fallback = after < value.Length && value[after] == ',';
                    steps.Spend(1);
                    references.Add(new VarReference(name, fallback, references.Count + 1));
                    open.Push((references.Count - 1, depth));
                    i = fallback ? after : end - 1;
                    continue;
                }
                if (value[i] == '(')
                {
                    depth++;
                }
                else if (value[i] == ')')
                {
                    if (open.TryPeek(out (int Reference, int Depth) top) && top.Depth == depth)
                    {
                        open.Pop();
                        references[top.Reference] = references[top.Reference] with { End = references.Count };
                    }
                    depth = Math.Max(0, depth - 1);
                }
            }
            while (open.TryPop(out (int Reference, int Depth) unclosed))
            {
                references[unclosed.Reference] = references[unclosed.Reference] with { End = references.Count };
            }
            return [.. references];
        }

        /// <summary>A <c>var()</c> reference: the name's id (-1 for one
        /// nothing defines), whether a fallback follows it, and the index
        /// past the references its fallback holds.</summary>
        private readonly record struct VarReference(int Name, bool HasFallback, int End);

        /// <summary>A value being resolved: its next reference, its end,
        /// its level, the outcome so far along each way, and, while a
        /// fallback is read, what the failed lookup left.</summary>
        private record struct Frame(
            int Position,
            int End,
            int Level,
            bool Going,
            double GoingCost,
            bool Failed,
            double FailedCost,
            bool PendingResolves,
            double PendingResolveCost,
            double FallbackBase)
        {
            internal static Frame Start(int position, int end, int level) =>
                new(position, end, level, true, 0, false, 0, false, 0, 0);
        }
    }

    /// <summary>Links and animations name an element without drawing it.</summary>
    private static bool DrawsWhatItNames(string element) => element switch
    {
        "a" or "animate" or "animateMotion" or "animateColor" or "animateTransform"
            or "set" or "discard" or "mpath" => false,
        _ => true,
    };

    private static bool IsGradient(string element) =>
        element.Equals("linearGradient", StringComparison.OrdinalIgnoreCase)
        || element.Equals("radialGradient", StringComparison.OrdinalIgnoreCase);

    /// <summary>Filter primitives, gradients and their stops, and
    /// descriptive elements are never drawn as elements, so a stylesheet's
    /// references do not reach through them.</summary>
    private static bool TakesStyleRules(string element) =>
        !(element.Length > 2 && element.StartsWith("fe", StringComparison.Ordinal) && char.IsUpper(element[2]))
        && !IsGradient(element)
        && element is not ("filter" or "stop" or "title" or "desc" or "metadata" or "style");

    /// <summary>The key a simple selector (an optional type or <c>*</c>,
    /// then classes and ids) is filed under: its id, else its first class,
    /// else its type, lower-cased so the match only ever widens. Null for
    /// anything else.</summary>
    private static string? SimpleSelectorKey(string selector)
    {
        Scanned(3L * selector.Length);
        string text = selector.Trim();
        if (text.Length == 0)
        {
            return null;
        }
        int i = 0;
        string? type = null;
        string? firstClass = null;
        string? id = null;
        if (text[0] == '*')
        {
            i = 1;
        }
        else if (IsNameCharacter(text[0]))
        {
            type = ReadName(text, ref i);
        }
        while (i < text.Length)
        {
            char marker = text[i++];
            string name = ReadName(text, ref i);
            if (name.Length == 0)
            {
                return null;
            }
            switch (marker)
            {
                case '.':
                    firstClass ??= name;
                    break;
                case '#':
                    id = name;
                    break;
                default:
                    return null;
            }
        }
        return id is not null ? "#" + id.ToLowerInvariant()
            : firstClass is not null ? "." + firstClass.ToLowerInvariant()
            : type?.ToLowerInvariant() ?? AnyElementKey;
    }

    private static string ReadName(string text, ref int i)
    {
        int start = i;
        while (i < text.Length && IsNameCharacter(text[i]))
        {
            i++;
        }
        return text[start..i];
    }

    private static bool IsNameCharacter(char c) =>
        char.IsAsciiLetterOrDigit(c) || c is '-' or '_' || c >= '\u0080';

    /// <summary>A character of a custom property's name as
    /// <see cref="Unescape"/> leaves it: a name character, or U+0001 for an
    /// escaped syntax character, which a name may hold (Codex's review,
    /// stage 4).</summary>
    private static bool IsVarNameCharacter(char c) => IsNameCharacter(c) || c == '\u0001';

    private static bool IsUrlAt(string text, int i) =>
        i + 4 <= text.Length && text.AsSpan(i, 4).Equals("url(", StringComparison.OrdinalIgnoreCase);

    /// <summary>Index of the <c>)</c> that closes the url argument starting
    /// at <paramref name="start"/>, or the text's last index.</summary>
    private static int UrlArgumentEnd(string text, int start)
    {
        int i = start;
        while (i < text.Length && char.IsWhiteSpace(text[i]))
        {
            i++;
        }
        if (i < text.Length && text[i] is '"' or '\'')
        {
            int close = text.IndexOf(text[i], i + 1);
            i = close < 0 ? text.Length : close + 1;
        }
        int end = i < text.Length ? text.IndexOf(')', i) : -1;
        int argumentEnd = end < 0 ? text.Length - 1 : end;
        Scanned(Math.Max(0, argumentEnd - start + 1));
        return argumentEnd;
    }

    /// <summary>The element id the url argument in [<paramref
    /// name="start"/>, <paramref name="end"/>) names (<c>#id</c>, quoted
    /// or not), or null. Only the id is copied (Codex's round 3: an
    /// argument that names no id is never copied).</summary>
    private static string? Fragment(string text, int start, int end)
    {
        int from = Math.Min(start, text.Length);
        int to = Math.Clamp(end, from, text.Length);
        Scanned(to - from);
        while (from < to && char.IsWhiteSpace(text[from]))
        {
            from++;
        }
        while (to > from && char.IsWhiteSpace(text[to - 1]))
        {
            to--;
        }
        while (from < to && text[from] is '"' or '\'')
        {
            from++;
        }
        while (to > from && text[to - 1] is '"' or '\'')
        {
            to--;
        }
        while (from < to && char.IsWhiteSpace(text[from]))
        {
            from++;
        }
        while (to > from && char.IsWhiteSpace(text[to - 1]))
        {
            to--;
        }
        return to - from > 1 && text[from] == '#' ? text[(from + 1)..to] : null;
    }

    /// <summary>Where the CSS string opening at <paramref name="open"/>
    /// ends: its closing quote, or the line end or text end that leaves it
    /// open (a CSS tokenizer ends an unclosed string there).</summary>
    private static int StringEnd(string text, int open)
    {
        int i = open + 1;
        while (i < text.Length && text[i] != text[open] && text[i] is not ('\n' or '\r' or '\f'))
        {
            i++;
        }
        Scanned(i - open + 1);
        return i;
    }

    /// <summary>
    /// Resolves CSS escapes: <c>\</c> and up to six hex digits (and one
    /// whitespace after them), or <c>\</c> and any other character. A hex
    /// escape gives its code point, a supplementary one as its surrogate
    /// pair, and U+FFFD for zero, a surrogate or one past U+10FFFF, as CSS
    /// does (Codex's review, stage 4: a valid escape names the character it
    /// gives). An escaped character that would otherwise be CSS syntax
    /// becomes U+0001, which is no syntax: it can neither open, close,
    /// separate nor start a comment, a selector holding one is not a simple
    /// selector, and a custom property's name may hold it.
    /// </summary>
    private static string Unescape(string text)
    {
        Scanned(text.Length);
        if (!text.Contains('\\'))
        {
            return text;
        }
        var builder = new StringBuilder(text.Length);
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] != '\\')
            {
                builder.Append(text[i]);
                continue;
            }
            int digits = 0;
            int code = 0;
            while (digits < 6 && i + 1 < text.Length && char.IsAsciiHexDigit(text[i + 1]))
            {
                char hex = text[++i];
                code = (code * 16) + (hex <= '9' ? hex - '0' : (hex | 0x20) - 'a' + 10);
                digits++;
            }
            char resolved;
            if (digits > 0)
            {
                if (i + 1 < text.Length && char.IsWhiteSpace(text[i + 1]))
                {
                    i++;
                }
                if (code is 0 or (>= 0xD800 and <= 0xDFFF) or > 0x10FFFF)
                {
                    resolved = '�';
                }
                else if (code > 0xFFFF)
                {
                    builder.Append(char.ConvertFromUtf32(code));
                    continue;
                }
                else
                {
                    resolved = (char)code;
                }
            }
            else
            {
                resolved = i + 1 < text.Length ? text[++i] : '\u0001';
            }
            builder.Append(resolved is ';' or '{' or '}' or '(' or ')' or '[' or ']' or '"' or '\''
                or '\\' or '/' or '*' or ',' ? '\u0001' : resolved);
        }
        return builder.ToString();
    }
}
