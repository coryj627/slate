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

    /// <summary>The largest term's name, for the log: <c>stylesheet</c>,
    /// <c>use</c>, <c>var</c> or <c>marker</c>.</summary>
    internal string LargestTerm
    {
        get
        {
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
/// expand to, reading the document once with an XmlReader BEFORE
/// Svg.Custom parses it.
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
    /// first, so a document past one still says which.</summary>
    internal static SvgRefusal Check(string source, int maxCharacters, out SvgDecodeEstimate estimate)
    {
        estimate = default;
        if (ForbiddenTokens.Any(token =>
                source.Contains(token, StringComparison.OrdinalIgnoreCase)))
        {
            return SvgRefusal.Markup;
        }

        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = maxCharacters,
            IgnoreComments = true,
            IgnoreProcessingInstructions = false,
        };
        var document = new DocumentShape();
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
        estimate = new DecodeCostEstimator(document, source, settings).Estimate();
        return estimate.Milliseconds > MaxEstimatedDecodeMilliseconds
            ? SvgRefusal.DecodeCost
            : SvgRefusal.None;
    }

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
    private sealed class DocumentShape
    {
        private readonly List<int> parents = [];
        private readonly List<string> names = [];
        private readonly List<string?> ids = [];
        private readonly List<string?> classLists = [];
        private readonly List<(int From, string Id)> references = [];
        private readonly List<(int From, int To)> filterInputs = [];
        private readonly Dictionary<string, List<string>> ruleTargets = new(StringComparer.Ordinal);
        private readonly List<int> open = [];
        private readonly List<FilterInputs?> openFilters = [];
        private readonly StringBuilder styleText = new();
        private int styleDepth = -1;

        // #1610 (CB-29): what the decode estimate reads, collected in the
        // same pass, and Measure's graph, kept for it.
        private readonly List<int> vertices = [];
        private readonly List<MarkerDeclaration> elementMarkers = [];
        private readonly List<(string Name, string Value)> customDefinitions = [];
        private readonly List<string> attributeVarUses = [];
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
        /// custom-property definitions aside.</summary>
        internal IReadOnlyList<string> AttributeVarUses => attributeVarUses;

        /// <summary>Every use element and the id its href names.</summary>
        internal IReadOnlyList<(int Element, string? Target)> Uses => uses;

        internal bool HasStyleElement { get; private set; }

        /// <summary>The joined stylesheet, as Svg.Custom reads it.</summary>
        internal string Stylesheet { get; private set; } = string.Empty;

        /// <summary>Measure's graph: the elements, then one node per rule
        /// key, and each node's edges.</summary>
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
            return ReadStylesheet(Stylesheet);
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
            if (attribute == "points" && element is "polyline" or "polygon")
            {
                int points = NumberCount(value, 0, value.Length) / 2;
                vertexCount = points + (element == "polygon" && points >= 1 ? 1 : 0);
            }
            else if (attribute == "d" && element == "path")
            {
                vertexCount = PathVertices(value);
            }
            if (attribute == "style")
            {
                ReadStyleAttribute(index, value);
                return;
            }
            if (MarkerSlots(attribute) is { } slots)
            {
                AddMarker(index, slots, value);
            }
            if (value.Contains("var(", StringComparison.OrdinalIgnoreCase))
            {
                attributeVarUses.Add(value);
            }
            if (element == "use" && attribute.Equals("href", StringComparison.OrdinalIgnoreCase)
                && value.Trim() is { Length: > 1 } target && target[0] == '#')
            {
                useTarget = target[1..];
            }
        }

        private void ReadStyleAttribute(int index, string value)
        {
            if (!value.Contains("--", StringComparison.Ordinal)
                && !value.Contains("var(", StringComparison.OrdinalIgnoreCase)
                && !value.Contains("marker", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
            foreach ((string property, string declared) in Declarations(Unescape(value)))
            {
                if (property.StartsWith("--", StringComparison.Ordinal))
                {
                    customDefinitions.Add((property, declared));
                    continue;
                }
                if (MarkerSlots(property) is { } slots)
                {
                    AddMarker(index, slots, declared);
                }
                if (declared.Contains("var(", StringComparison.OrdinalIgnoreCase))
                {
                    attributeVarUses.Add(declared);
                }
            }
        }

        private void AddMarker(int index, int slots, string value)
        {
            if (ReadMarkerValue(value, out string? target, out bool throughVar))
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
            int urls = 0;
            int depth = 0;
            int boundary = 0;
            List<string?> preludes = [];
            for (int i = 0; i < text.Length; i++)
            {
                if (tokens && text[i] == '/' && i + 1 < text.Length && text[i + 1] == '*')
                {
                    int close = text.IndexOf("*/", i + 2, StringComparison.Ordinal);
                    i = close < 0 ? text.Length : close + 1;
                    continue;
                }
                if (tokens && text[i] is '"' or '\'')
                {
                    int end = StringEnd(text, i);
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
        /// when the prelude is not a list of simple selectors.</summary>
        private void AddRuleTarget(string? prelude, string target)
        {
            List<string> keys = [];
            foreach (string selector in (prelude ?? string.Empty).Split(','))
            {
                if (SimpleSelectorKey(selector) is not { } key)
                {
                    keys = [AnyElementKey];
                    break;
                }
                keys.Add(key);
            }
            foreach (string key in keys)
            {
                if (!ruleTargets.TryGetValue(key, out List<string>? targets))
                {
                    ruleTargets[key] = targets = [];
                }
                targets.Add(target);
            }
        }

        /// <summary>Walks the graph once in dependency order: the longest
        /// chain, the number of renders and whether every element was
        /// reached, which fails exactly when there is a cycle.</summary>
        internal SvgRefusal Measure()
        {
            int elements = names.Count;
            var keyNodes = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (string key in ruleTargets.Keys)
            {
                keyNodes[key] = elements + keyNodes.Count;
            }
            int nodes = elements + keyNodes.Count;
            var next = new List<int>?[nodes];
            var indegree = new int[nodes];
            void Edge(int from, int to)
            {
                (next[from] ??= []).Add(to);
                indegree[to]++;
            }

            var byId = new Dictionary<string, List<int>>(StringComparer.Ordinal);
            for (int i = 0; i < elements; i++)
            {
                if (parents[i] >= 0)
                {
                    Edge(parents[i], i);
                }
                if (ids[i] is { } id)
                {
                    if (!byId.TryGetValue(id, out List<int>? named))
                    {
                        byId[id] = named = [];
                    }
                    named.Add(i);
                }
            }
            void Reference(int from, string id)
            {
                if (byId.TryGetValue(id, out List<int>? targets))
                {
                    foreach (int target in targets.Where(target => !IsGradient(names[target])))
                    {
                        Edge(from, target);
                    }
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
            foreach ((string key, List<string> targets) in ruleTargets)
            {
                foreach (string id in targets.Distinct(StringComparer.Ordinal))
                {
                    Reference(keyNodes[key], id);
                }
            }
            bool anyClassKey = keyNodes.Keys.Any(key => key.StartsWith('.'));
            for (int i = 0; i < elements; i++)
            {
                if (!TakesStyleRules(names[i]))
                {
                    continue;
                }
                if (keyNodes.TryGetValue(AnyElementKey, out int any))
                {
                    Edge(i, any);
                }
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
    /// neither for <c>none</c>, or a value read through <c>var()</c>.</summary>
    private readonly record struct MarkerDeclaration(int Element, int Slots, string? Target, bool ThroughVar);

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
    /// <c>var()</c>, which Svg.Custom resolves. Any other value is
    /// ignored.</summary>
    private static bool ReadMarkerValue(string value, out string? target, out bool throughVar)
    {
        target = null;
        throughVar = false;
        string text = value.Trim();
        if (text.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        if (text.Contains("var(", StringComparison.OrdinalIgnoreCase))
        {
            throughVar = true;
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
            total += per == 0 ? 1 : NumberCount(data, start, i) / per * (command == 'A' ? 4 : 1);
        }
        return (int)Math.Min(total, int.MaxValue);
    }

    private static bool IsPathCommand(char c) =>
        char.ToUpperInvariant(c) is 'M' or 'L' or 'T' or 'H' or 'V' or 'C' or 'S' or 'Q' or 'A' or 'Z';

    /// <summary>The <c>name: value</c> declarations of a declaration block,
    /// split at the semicolons outside strings and brackets.</summary>
    private static List<(string Name, string Value)> Declarations(string block)
    {
        var declarations = new List<(string Name, string Value)>();
        foreach (string declaration in SplitTop(block, ';'))
        {
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
    /// computed iteratively and saturates; nothing recurses, and the guard's
    /// own time stays linear in the document, but for the <c>var()</c> table,
    /// 17 lookup depths per custom property.
    /// </summary>
    private sealed class DecodeCostEstimator(DocumentShape document, string source, XmlReaderSettings settings)
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
        private readonly List<(string Value, List<KeyStats> Subjects)> ruleVarUses = [];
        private readonly List<(string Name, string Value)> ruleDefinitions = [];
        private readonly HashSet<string> attributeNames = new(StringComparer.Ordinal);
        private double queriedPasses;
        private int rules;
        private double[] renders = [];

        internal SvgDecodeEstimate Estimate()
        {
            int elements = document.Names.Count;
            ReadRules(Unescape(document.Stylesheet));
            renders = Renders();
            Family family = Families(elements);
            double markerUnits = VisitElements(family.Siblings, ReadAttributeKeys());
            return new SvgDecodeEstimate(
                Cap(StylesheetMicroseconds(elements, family)),
                Cap(UseMicroseconds(elements)),
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

        /// <summary>Reads every qualified rule of the joined stylesheet, in
        /// <c>@media</c> and every other block at-rule too (Svg.Custom reads
        /// <c>@media</c> only; counting the rest errs wide).</summary>
        private void ReadRules(string stylesheet)
        {
            string text = StripComments(stylesheet);
            var blocks = new Stack<(int Start, int End)>();
            blocks.Push((0, text.Length));
            while (blocks.TryPop(out (int Start, int End) block))
            {
                int i = block.Start;
                while (i < block.End)
                {
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
                    string prelude = text[i..open];
                    int semicolon = prelude.LastIndexOf(';');
                    prelude = (semicolon >= 0 ? prelude[(semicolon + 1)..] : prelude).Trim();
                    int bodyEnd = Math.Max(open + 1, k - 1);
                    if (prelude.StartsWith('@'))
                    {
                        if (text.AsSpan(open + 1, bodyEnd - (open + 1)).Contains('{'))
                        {
                            blocks.Push((open + 1, bodyEnd));
                        }
                    }
                    else
                    {
                        ReadRule(prelude, text[(open + 1)..bodyEnd]);
                    }
                    i = k;
                }
            }
        }

        private void ReadRule(string prelude, string body)
        {
            rules++;
            List<(string Name, string Value)> declarations = Distinct(Declarations(body));
            List<string> branches = SplitTop(prelude, ',');
            if (declarations.Count == 0 || branches.Count == 0)
            {
                return;
            }
            bool custom = false;
            bool geometry = false;
            double landing = 0;
            bool holdsVarUse = false;
            List<MarkerDeclaration>? markers = null;
            foreach ((string name, string value) in declarations)
            {
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
                holdsVarUse |= usesVar;
                if (MarkerSlots(name) is { } slots
                    && ReadMarkerValue(value, out string? target, out bool throughVar))
                {
                    (markers ??= []).Add(new MarkerDeclaration(-1, slots, target, throughVar));
                }
            }
            double passes = 1 + (custom ? 1 : 0) + (geometry ? 1 : 0);
            List<KeyStats>? subjects = holdsVarUse ? new(branches.Count) : null;
            foreach (string branch in branches)
            {
                (KeyStats subject, int descendants, int siblings, List<KeyStats>? ofType) = ReadBranch(branch);
                subject.DeclarationMicroseconds += landing;
                if (markers is not null)
                {
                    (subject.Markers ??= []).AddRange(markers);
                }
                subjects?.Add(subject);
                queriedPasses += passes;
                if (descendants > 0 || siblings > 0 || ofType is not null)
                {
                    selectorBranches.Add(new SelectorBranch(passes, descendants, siblings, ofType));
                }
            }
            if (subjects is not null)
            {
                foreach ((string name, string value) in declarations)
                {
                    if (!name.StartsWith("--", StringComparison.Ordinal)
                        && value.Contains("var(", StringComparison.OrdinalIgnoreCase))
                    {
                        ruleVarUses.Add((value, subjects));
                    }
                }
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
                if (seen.Add(name.StartsWith("--", StringComparison.Ordinal) ? name : name.ToLowerInvariant()))
                {
                    kept.Add(declarations[i]);
                }
            }
            return kept;
        }

        /// <summary>
        /// A selector branch: the key its subject (the rightmost compound) is
        /// filed under, its descendant steps and its general-sibling steps
        /// (child and adjacent-sibling steps read one level or one sibling,
        /// and add nothing past the branch's own query), and the keys of the
        /// compounds carrying an of-type pseudo-class (or one whose position
        /// counts from the end), which recomputes the parent's children per
        /// element, or null for none.
        /// </summary>
        private (KeyStats Subject, int Descendants, int Siblings, List<KeyStats>? OfType) ReadBranch(string branch)
        {
            int descendants = 0;
            int siblings = 0;
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
                    string compound = branch[start..i];
                    if (previous is not null)
                    {
                        descendants += combinator == '\0' ? 1 : 0;
                        siblings += combinator == '~' ? 1 : 0;
                    }
                    if (RecomputesFamily(compound))
                    {
                        (ofType ??= []).Add(KeyOf(CompoundKey(compound)));
                    }
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
            return (KeyOf(CompoundKey(previous ?? AnyElementKey)), descendants, siblings, ofType);
        }

        /// <summary>Whether a compound carries a pseudo-class that re-reads
        /// the parent's children for each element it tests: the of-type
        /// ones (measured), and, unmeasured, those counting from the end.
        /// nth-child and first-child read an index and stay linear (the
        /// follow-up measured nth-child).</summary>
        private static bool RecomputesFamily(string compound) =>
            compound.Contains(':', StringComparison.Ordinal)
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
        /// Every branch's query walks every element once per pass. A branch
        /// whose query is not linear (#1610, Design 1b, with the follow-up's
        /// measured counts) adds, per pass: for its descendant steps, the
        /// depth bound; for its general-sibling steps, the sibling-list
        /// reads; for each compound with an of-type pseudo-class, the
        /// sibling-list sizes of the elements filed under its key. Then the
        /// declarations each key's elements take, and each rule.
        /// </summary>
        private double StylesheetMicroseconds(int elements, Family family)
        {
            var depthBounds = new Dictionary<int, double>();
            var siblingReads = new Dictionary<int, double>();
            double selectors = 0;
            foreach (SelectorBranch branch in selectorBranches)
            {
                double work = 0;
                if (branch.Descendants > 0)
                {
                    if (!depthBounds.TryGetValue(branch.Descendants, out double bound))
                    {
                        depthBounds[branch.Descendants] = bound = DepthBound(family, branch.Descendants);
                    }
                    work = Cap(work + (SvgDecodeRates.DescendantChainUnit * bound));
                }
                if (branch.Siblings > 0)
                {
                    if (!siblingReads.TryGetValue(branch.Siblings, out double reads))
                    {
                        siblingReads[branch.Siblings] = reads = SiblingReads(family, branch.Siblings);
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
        /// counted by depth, so it costs at most 257 x 256 steps.
        /// </summary>
        private static double DepthBound(Family family, int steps)
        {
            double total = 0;
            for (int depth = 1; depth < family.AtDepth.Length; depth++)
            {
                if (family.AtDepth[depth] > 0)
                {
                    total = Cap(total + (family.AtDepth[depth] * Choices(depth, steps)));
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
        private static double SiblingReads(Family family, int steps)
        {
            double total = 0;
            foreach ((int children, int parents) in family.Parents)
            {
                total = Cap(total + ((double)parents * children * Choices(children, steps)));
            }
            return total;
        }

        /// <summary>C(n, 1) + ... + C(n, k), saturating.</summary>
        private static double Choices(int n, int k)
        {
            double sum = 0;
            double term = 1;
            for (int s = 1; s <= Math.Min(n, k) && sum < Saturated; s++)
            {
                term = term * (n - s + 1) / s;
                sum = Cap(sum + term);
            }
            return sum;
        }

        /// <summary>The family counts the selector work reads: how many
        /// elements sit at each depth (proper ancestors, the root at 0), how
        /// many parents have each number of children, and each element's
        /// sibling-list size (its parent's children, the root's 0).</summary>
        private Family Families(int elements)
        {
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
            return new Family(atDepth, families, siblings);
        }

        /// <summary>Elements by depth, parents by their number of children
        /// (children, parents), and each element's sibling-list
        /// size.</summary>
        private sealed record Family(double[] AtDepth, Dictionary<int, int> Parents, double[] Siblings);

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
            using var textReader = new StringReader(source);
            using XmlReader reader = XmlReader.Create(textReader, settings);
            int index = -1;
            while (reader.Read())
            {
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
                    string name = reader.LocalName.ToLowerInvariant();
                    if (!attributeNames.Contains(name))
                    {
                        continue;
                    }
                    if (keys.TryGetValue("[" + name + "]", out KeyStats? present))
                    {
                        found.Add((index, present));
                    }
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
        /// One walk in document order, parents first: each element is
        /// counted under each key it carries (every element, the descriptive
        /// ones too: a rule's query walks them all and applies its
        /// declarations to each match), and each marked shape adds its
        /// placements. An element's marker in each slot is its own (from its
        /// attributes, its style attribute or the rules filed under its keys,
        /// the one with the most renders), else its parent's.
        /// </summary>
        private double VisitElements(double[] siblings, List<(int Element, KeyStats Key)> attributeKeys)
        {
            int elements = document.Names.Count;
            int throughVar = WidestVarMarker();
            foreach (KeyStats key in keys.Values)
            {
                key.ResolveMarkers(this, throughVar);
            }
            var effective = new int[elements * Slots];
            var best = new int[Slots];
            var bestRenders = new double[Slots];
            var set = new bool[Slots];
            IReadOnlyList<MarkerDeclaration> declared = document.ElementMarkers;
            int nextDeclaration = 0;
            int nextAttributeKey = 0;
            bool anyKey = keys.Count > 0;
            double units = 0;
            for (int i = 0; i < elements; i++)
            {
                Array.Fill(best, -1);
                Array.Fill(bestRenders, -1);
                Array.Clear(set);
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
                    stats.Count++;
                    stats.FamilySum += siblings[i];
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
                    int marker = Resolve(declaration, throughVar);
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
                int parent = document.Parents[i];
                for (int slot = 0; slot < Slots; slot++)
                {
                    effective[(i * Slots) + slot] = set[slot]
                        ? (bestRenders[slot] > 0 ? best[slot] : -1)
                        : parent >= 0 ? effective[(parent * Slots) + slot] : -1;
                }
                units = Cap(units + Placements(i, effective));
            }
            return units;
        }

        /// <summary>What a marked shape's placements weigh, times the
        /// shape's own renders: each placement its marker's renders and
        /// <see cref="SvgDecodeRates.MarkerPlacementWeight"/>, and the widest
        /// marker once more. Start and end are one placement each, mid every
        /// vertex between.</summary>
        private double Placements(int element, int[] effective)
        {
            int vertexCount = document.Vertices[element];
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
            }
            return widest == 0 ? 0 : Cap((placed + widest) * document.Paths[element]);
        }

        /// <summary>The element a marker declaration places: the first
        /// element with its id, the widest marker any custom property names
        /// when it is read through <c>var()</c>, or none.</summary>
        private int Resolve(MarkerDeclaration declaration, int throughVar) =>
            declaration.ThroughVar ? throughVar
            : declaration.Target is { } target ? FirstWithId(target)
            : -1;

        private int FirstWithId(string id) =>
            document.ElementsById.TryGetValue(id, out List<int>? named) ? named[0] : -1;

        /// <summary>The widest marker a custom property's value names, which
        /// a marker property read through <c>var()</c> may resolve to
        /// (#1610, the red team's M7).</summary>
        private int WidestVarMarker()
        {
            int widest = -1;
            foreach ((string _, string value) in document.CustomDefinitions.Concat(ruleDefinitions))
            {
                for (int url = value.IndexOf("url(", StringComparison.OrdinalIgnoreCase);
                    url >= 0;
                    url = value.IndexOf("url(", url + 4, StringComparison.OrdinalIgnoreCase))
                {
                    int end = UrlArgumentEnd(value, url + 4);
                    if (Fragment(value, url + 4, end < value.Length && value[end] == ')' ? end : value.Length) is { } id
                        && FirstWithId(id) is var marker and >= 0
                        && (widest < 0 || renders[marker] > renders[widest]))
                    {
                        widest = marker;
                    }
                }
            }
            return widest;
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

        /// <summary>Under any <c>&lt;style&gt;</c>, each use render
        /// re-applies the stylesheet to the document and to its instance's
        /// elements.</summary>
        private double UseMicroseconds(int elements)
        {
            if (!document.HasStyleElement)
            {
                return 0;
            }
            double useRenders = 0;
            double instanceElements = 0;
            foreach ((int element, string? target) in document.Uses)
            {
                double paths = document.Paths[element];
                useRenders = Cap(useRenders + paths);
                if (target is not null && FirstWithId(target) is var instance and >= 0)
                {
                    instanceElements = Cap(instanceElements + (paths * renders[instance]));
                }
            }
            return Cap(SvgDecodeRates.UseRenderElement * useRenders
                    * (elements + (SvgDecodeRates.UseRenderRuleWeight * rules)))
                + Cap(instanceElements
                    * (SvgDecodeRates.UseInstanceElement + (SvgDecodeRates.UseInstanceElementRule * rules)));
        }

        // ------------------------------------------------------------------
        // var()
        // ------------------------------------------------------------------

        /// <summary>
        /// The lookups Svg.Custom makes resolving every <c>var()</c> use: an
        /// attribute's or a style attribute's once, a rule's on each element
        /// it is filed under. A property's value is resolved from left to
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
            var table = new VarTable(document.CustomDefinitions.Concat(ruleDefinitions));
            double lookups = 0;
            foreach (string value in document.AttributeVarUses)
            {
                lookups = Cap(lookups + table.Resolve(value));
            }
            foreach ((string value, List<KeyStats> subjects) in ruleVarUses)
            {
                double landed = subjects.Sum(subject => subject.Count);
                lookups = Cap(lookups + (table.Resolve(value) * landed));
            }
            return lookups;
        }

        private sealed record SelectorBranch(double Passes, int Descendants, int Siblings, List<KeyStats>? OfType);

        /// <summary>What the elements filed under one key add up to, and
        /// what the rules filed there apply to each.</summary>
        private sealed class KeyStats
        {
            internal double Count;
            internal double FamilySum;
            internal double DeclarationMicroseconds;
            internal List<MarkerDeclaration>? Markers;
            internal bool[]? MarkerSet;
            internal int[]? MarkerBest;

            /// <summary>Each slot's widest marker among the rules filed
            /// here.</summary>
            internal void ResolveMarkers(DecodeCostEstimator estimator, int throughVar)
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
                    int marker = estimator.Resolve(declaration, throughVar);
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
    /// each of Svg.Custom's lookup levels: whether its value resolves there,
    /// and the lookups that takes.
    /// </summary>
    private sealed class VarTable
    {
        private const int Levels = 16;

        private readonly Dictionary<string, int> names = new(StringComparer.Ordinal);
        private readonly List<int> tableRow = [];
        private readonly float[] lookups;
        private readonly bool[] resolves;
        private readonly List<Frame> frames = [];

        internal VarTable(IEnumerable<(string Name, string Value)> definitions)
        {
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
            lookups = new float[rows * (Levels + 1)];
            resolves = new bool[rows * (Levels + 1)];
            for (int level = Levels; level >= 1; level--)
            {
                for (int id = 0; id < parsed.Length; id++)
                {
                    int row = tableRow[id];
                    if (row < 0)
                    {
                        continue;
                    }
                    double widest = 0;
                    bool any = false;
                    foreach (VarReference[] references in parsed[id])
                    {
                        (bool ok, double count) = Evaluate(references, level);
                        widest = Math.Max(widest, count);
                        any |= ok;
                    }
                    lookups[(row * (Levels + 1)) + level] = (float)Math.Min(widest, 1e18);
                    resolves[(row * (Levels + 1)) + level] = any;
                }
            }
        }

        private static readonly VarReference[] Literal = [];

        /// <summary>The lookups resolving a declaration's value
        /// takes.</summary>
        internal double Resolve(string value) => Evaluate(Parse(value), 0).Lookups;

        /// <summary>A name's lookup at <paramref name="level"/>: past the
        /// deepest level, or for a name nothing defines, one lookup that
        /// fails; for a name defined only by literals, one that
        /// resolves.</summary>
        private (bool Resolves, double Lookups) Lookup(int name, int level)
        {
            if (name < 0 || level > Levels)
            {
                return (false, 1);
            }
            int row = tableRow[name];
            return row < 0
                ? (true, 1)
                : (resolves[(row * (Levels + 1)) + level], lookups[(row * (Levels + 1)) + level]);
        }

        /// <summary>A value's references resolved at
        /// <paramref name="level"/>, from left to right, with an explicit
        /// stack for the fallbacks: a value with none is one
        /// lookup.</summary>
        private (bool Resolves, double Lookups) Evaluate(VarReference[] references, int level)
        {
            if (references.Length == 0)
            {
                return (true, 1);
            }
            frames.Clear();
            frames.Add(new Frame(0, references.Length, level, 0));
            (bool Resolves, double Lookups)? delivered = null;
            while (true)
            {
                int top = frames.Count - 1;
                Frame frame = frames[top];
                bool? ended = null;
                if (delivered is { } fallback)
                {
                    // A fallback ended; it served the reference at Position.
                    delivered = null;
                    frame.Lookups = Math.Min(frame.Lookups + fallback.Lookups, 1e18);
                    if (fallback.Resolves)
                    {
                        frame.Position = references[frame.Position].End;
                    }
                    else
                    {
                        ended = false;
                    }
                }
                else if (frame.Position >= frame.End)
                {
                    ended = true;
                }
                else
                {
                    VarReference reference = references[frame.Position];
                    (bool resolves, double count) = Lookup(reference.Name, frame.Level + 1);
                    frame.Lookups = Math.Min(frame.Lookups + count, 1e18);
                    if (resolves)
                    {
                        frame.Position = reference.End;
                    }
                    else if (!reference.HasFallback)
                    {
                        ended = false;
                    }
                    else if (frame.Position + 1 == reference.End)
                    {
                        // A fallback with no reference is a literal.
                        frame.Lookups = Math.Min(frame.Lookups + 1, 1e18);
                        frame.Position = reference.End;
                    }
                    else
                    {
                        frames[top] = frame;
                        frames.Add(new Frame(frame.Position + 1, reference.End, frame.Level + 1, 0));
                        continue;
                    }
                }
                if (ended is not { } resolved)
                {
                    frames[top] = frame;
                    continue;
                }
                frames.RemoveAt(top);
                if (frames.Count == 0)
                {
                    return (resolved, frame.Lookups);
                }
                delivered = (resolved, frame.Lookups);
            }
        }

        /// <summary>The <c>var()</c> references of a value in document
        /// order, each with the end of the references its fallback
        /// holds.</summary>
        private VarReference[] Parse(string value)
        {
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
                        while (end < value.Length && IsNameCharacter(value[end]))
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

        private record struct Frame(int Position, int End, int Level, double Lookups);
    }

    /// <summary>A stylesheet without its comments, which count for
    /// nothing; an unclosed one runs to the end.</summary>
    private static string StripComments(string css)
    {
        if (!css.Contains("/*", StringComparison.Ordinal))
        {
            return css;
        }
        var text = new StringBuilder(css.Length);
        int i = 0;
        while (i < css.Length)
        {
            if (css[i] == '/' && i + 1 < css.Length && css[i + 1] == '*')
            {
                int close = css.IndexOf("*/", i + 2, StringComparison.Ordinal);
                i = close < 0 ? css.Length : close + 2;
                continue;
            }
            text.Append(css[i++]);
        }
        return text.ToString();
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
        return end < 0 ? text.Length - 1 : end;
    }

    /// <summary>The element id the url argument in [<paramref
    /// name="start"/>, <paramref name="end"/>) names (<c>#id</c>, quoted
    /// or not), or null.</summary>
    private static string? Fragment(string text, int start, int end)
    {
        int from = Math.Min(start, text.Length);
        int to = Math.Clamp(end, from, text.Length);
        string argument = text[from..to].Trim().Trim('"', '\'').Trim();
        return argument.StartsWith('#') && argument.Length > 1 ? argument[1..] : null;
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
        return i;
    }

    /// <summary>
    /// Resolves CSS escapes: <c>\</c> and up to six hex digits (and one
    /// whitespace after them), or <c>\</c> and any other character. An
    /// escaped character that would otherwise be CSS syntax becomes
    /// U+0001, which is neither syntax nor part of a name: it can neither
    /// open, close, separate nor start a comment, and a selector holding
    /// one is not a simple selector.
    /// </summary>
    private static string Unescape(string text)
    {
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
                resolved = code is > 0 and < 0xD800 ? (char)code : '\u0001';
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
