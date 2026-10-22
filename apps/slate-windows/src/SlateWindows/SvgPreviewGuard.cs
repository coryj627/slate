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

    internal static SvgRefusal Check(string source, int maxCharacters)
    {
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
        return document.Measure();
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
            return ReadStylesheet(styleText.ToString());
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
            int reached = 0;
            long rendered = 0;
            while (ready.TryDequeue(out int node))
            {
                reached++;
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
