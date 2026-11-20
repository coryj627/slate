// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Globalization;
using System.Text;

namespace SlateWindows.Tests;

/// <summary>
/// #1610 (CB-29): the documents CB-29's step 0 decoded to measure what makes
/// an SVG slow, rebuilt byte for byte from a label such as
/// <c>"A_star_g R=3000 E=13000"</c>: a kind and its sizes. The calibration
/// that measured each kind is committed in
/// <c>docs/plans/47_windows_core_closeout/calibration/cb29/</c>
/// (<c>calibration.md</c> section 2 names every kind and its parameters).
/// No fact decodes a slow one: the facts ask the guard for its verdict, or
/// the decode path for its refusal, which starts no decoder.
/// </summary>
internal static class SlowSvgDocuments
{
    private const string Head =
        "<svg xmlns='http://www.w3.org/2000/svg' xmlns:xlink='http://www.w3.org/1999/xlink' width='400' height='400' viewBox='0 0 400 400'>";

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>The document a label names: its kind, then each size as
    /// <c>NAME=value</c>.</summary>
    internal static string Generate(string label)
    {
        string[] parts = label.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var parameters = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (string part in parts.Skip(1))
        {
            int equals = part.IndexOf('=', StringComparison.Ordinal);
            parameters[part[..equals]] = int.Parse(part[(equals + 1)..], Inv);
        }
        return Generate(parts[0], parameters);
    }

    private static string Hex(int k) => "#" + ((uint)(k * 2654435761u) & 0xFFFFFF).ToString("x6", Inv);

    private static string RectAt(int i, string extra) =>
        $"<rect x='{i % 100 * 4}' y='{i / 100 % 100 * 4}' width='3' height='3'{extra}/>";

    private static int P(IReadOnlyDictionary<string, int> p, string key, int fallback) =>
        p.TryGetValue(key, out int v) ? v : fallback;

    /// <summary>One kind of step 0's, at the sizes given: R rules, E
    /// elements, G elements in a used group, U uses, N points or nodes, D
    /// levels or declarations, K references or compounds, M marker
    /// elements, V vertices, and the rest as each kind says.</summary>
    internal static string Generate(string kind, IReadOnlyDictionary<string, int> p)
    {
        int r = P(p, "R", 10);
        int e = P(p, "E", 100);
        var s = new StringBuilder();
        s.Append(Head);
        switch (kind)
        {
            case "A_class":
                s.Append("<style>");
                for (int k = 0; k < r; k++)
                {
                    s.Append($".c{k}{{fill:{Hex(k)}}}");
                }
                s.Append("</style>");
                for (int i = 0; i < e; i++)
                {
                    s.Append(RectAt(i, $" class='c{i % r}'"));
                }
                break;
            case "A_unmatched":
                s.Append("<style>");
                for (int k = 0; k < r; k++)
                {
                    s.Append($".z{k}{{fill:{Hex(k)}}}");
                }
                s.Append("</style>");
                for (int i = 0; i < e; i++)
                {
                    s.Append(RectAt(i, ""));
                }
                break;
            case "A_unclassed":
                // Rules that match nothing over elements that carry a class.
                s.Append("<style>");
                for (int k = 0; k < r; k++)
                {
                    s.Append($".z{k}{{fill:{Hex(k)}}}");
                }
                s.Append("</style>");
                for (int i = 0; i < e; i++)
                {
                    s.Append(RectAt(i, $" class='c{i % 97}'"));
                }
                break;
            case "A_type":
                s.Append("<style>");
                for (int k = 0; k < r; k++)
                {
                    s.Append($"rect{{fill:{Hex(k)}}}");
                }
                s.Append("</style>");
                for (int i = 0; i < e; i++)
                {
                    s.Append(RectAt(i, ""));
                }
                break;
            case "A_attr":
                s.Append("<style>");
                for (int k = 0; k < r; k++)
                {
                    s.Append($"[data-k='{k}']{{fill:{Hex(k)}}}");
                }
                s.Append("</style>");
                for (int i = 0; i < e; i++)
                {
                    s.Append(RectAt(i, $" data-k='{i % r}'"));
                }
                break;
            case "A_compound":
                s.Append("<style>");
                for (int k = 0; k < r; k++)
                {
                    s.Append($"rect.c{k}{{fill:{Hex(k)}}}");
                }
                s.Append("</style>");
                for (int i = 0; i < e; i++)
                {
                    s.Append(RectAt(i, $" class='c{i % r}'"));
                }
                break;
            case "A_desc":
                s.Append("<style>");
                for (int k = 0; k < r; k++)
                {
                    s.Append($"g .c{k}{{fill:{Hex(k)}}}");
                }
                s.Append("</style><g>");
                for (int i = 0; i < e; i++)
                {
                    s.Append(RectAt(i, $" class='c{i % r}'"));
                }
                s.Append("</g>");
                break;
            case "A_list":
                {
                    int sel = P(p, "S", 4);
                    s.Append("<style>");
                    for (int k = 0; k < r; k++)
                    {
                        s.Append(string.Join(",", Enumerable.Range(0, sel).Select(j => $".c{k}_{j}")));
                        s.Append($"{{fill:{Hex(k)}}}");
                    }
                    s.Append("</style>");
                    for (int i = 0; i < e; i++)
                    {
                        s.Append(RectAt(i, $" class='c{i % r}_{i / r % sel}'"));
                    }
                    break;
                }
            case "A_decls":
                {
                    int d = P(p, "D", 4);
                    string[] names = ["fill", "stroke", "stroke-width", "opacity", "fill-opacity", "stroke-opacity", "stroke-dasharray", "stroke-linecap"];
                    s.Append("<style>");
                    for (int k = 0; k < r; k++)
                    {
                        s.Append($".c{k}{{");
                        for (int j = 0; j < d; j++)
                        {
                            string name = names[j % names.Length];
                            string value = name switch
                            {
                                "fill" or "stroke" => Hex(k + j),
                                "stroke-width" => "1",
                                "stroke-dasharray" => "1 1",
                                "stroke-linecap" => "round",
                                _ => "0.9",
                            };
                            s.Append($"{name}:{value};");
                        }
                        s.Append('}');
                    }
                    s.Append("</style>");
                    for (int i = 0; i < e; i++)
                    {
                        s.Append(RectAt(i, $" class='c{i % r}'"));
                    }
                    break;
                }
            case "A_use":
                {
                    // R class rules, a group of G rects, and U uses of it.
                    int g = P(p, "G", 4);
                    int u = P(p, "U", 100);
                    s.Append("<style>");
                    for (int k = 0; k < r; k++)
                    {
                        s.Append($".c{k}{{fill:{Hex(k)}}}");
                    }
                    s.Append("</style><defs><g id='s'>");
                    for (int j = 0; j < g; j++)
                    {
                        s.Append(RectAt(j, $" class='c{j % r}'"));
                    }
                    s.Append("</g></defs>");
                    for (int i = 0; i < u; i++)
                    {
                        s.Append($"<use href='#s' x='{i % 50 * 8}' y='{i / 50 % 50 * 8}'/>");
                    }
                    for (int i = 0; i < P(p, "X", 0); i++)
                    {
                        s.Append(RectAt(i, ""));
                    }
                    break;
                }
            case "G_scatter":
                {
                    // A guess at a plotting library's scatter export (written from
                    // memory of the format, not from a downloaded file): one global
                    // rule, a marker path in defs, a use per point.
                    int n = P(p, "N", 1000);
                    if (P(p, "nostyle", 0) == 0)
                    {
                        s.Append("<style type='text/css'>*{stroke-linejoin:round;stroke-linecap:butt}</style>");
                    }
                    s.Append("<defs><path id='m0' d='M 0 3 C 0.8 3 1.56 2.68 2.12 2.12 C 2.68 1.56 3 0.8 3 0 C 3 -0.8 2.68 -1.56 2.12 -2.12 C 1.56 -2.68 0.8 -3 0 -3 C -0.8 -3 -1.56 -2.68 -2.12 -2.12 C -2.68 -1.56 -3 -0.8 -3 0 C -3 0.8 -2.68 1.56 -2.12 2.12 C -1.56 2.68 -0.8 3 0 3 z' style='stroke:#1f77b4'/></defs><g id='PathCollection_1'>");
                    for (int i = 0; i < n; i++)
                    {
                        s.Append($"<use xlink:href='#m0' x='{(i * 37 % 400).ToString(Inv)}.5' y='{(i * 61 % 400).ToString(Inv)}.25' style='fill:#1f77b4;stroke:#1f77b4'/>");
                    }
                    s.Append("</g>");
                    break;
                }
            case "A_star_rect":
                s.Append("<style>");
                for (int k = 0; k < r; k++)
                {
                    s.Append($"*{{fill:{Hex(k)}}}");
                }
                s.Append("</style>");
                for (int i = 0; i < e; i++)
                {
                    s.Append("<rect width='3' height='3'/>");
                }
                break;
            case "A_use_nostyle":
                {
                    int g = P(p, "G", 4);
                    int u = P(p, "U", 100);
                    s.Append("<defs><g id='s'>");
                    for (int j = 0; j < g; j++)
                    {
                        s.Append(RectAt(j, $" fill='{Hex(j)}'"));
                    }
                    s.Append("</g></defs>");
                    for (int i = 0; i < u; i++)
                    {
                        s.Append($"<use href='#s' x='{i % 50 * 8}' y='{i / 50 % 50 * 8}'/>");
                    }
                    break;
                }
            case "A_plain":
                for (int i = 0; i < e; i++)
                {
                    s.Append(RectAt(i, $" fill='{Hex(i % 97)}'"));
                }
                break;
            case "C_root":
                // R custom properties on the root, R rules reading them by var(), E classed rects.
                s.Append("<style>svg{");
                for (int k = 0; k < r; k++)
                {
                    s.Append($"--v{k}:{Hex(k)};");
                }
                s.Append('}');
                for (int k = 0; k < r; k++)
                {
                    s.Append($".c{k}{{fill:var(--v{k})}}");
                }
                s.Append("</style>");
                for (int i = 0; i < e; i++)
                {
                    s.Append(RectAt(i, $" class='c{i % r}'"));
                }
                break;
            case "C_root_inline":
                // R custom properties on the root, each element reads one in its style.
                s.Append("<style>svg{");
                for (int k = 0; k < r; k++)
                {
                    s.Append($"--v{k}:{Hex(k)};");
                }
                s.Append("}</style>");
                for (int i = 0; i < e; i++)
                {
                    s.Append(RectAt(i, $" style='fill:var(--v{i % r})'"));
                }
                break;
            case "C_inline":
                for (int i = 0; i < e; i++)
                {
                    s.Append(RectAt(i, $" style='--a:{Hex(i % 97)};fill:var(--a)'"));
                }
                break;
            case "C_rule_all":
                // One rule declaring R custom properties on every rect, one reading the last.
                s.Append("<style>rect{");
                for (int k = 0; k < r; k++)
                {
                    s.Append($"--v{k}:{Hex(k)};");
                }
                s.Append($"}}rect{{fill:var(--v{r - 1})}}</style>");
                for (int i = 0; i < e; i++)
                {
                    s.Append(RectAt(i, ""));
                }
                break;
            case "C_per_rule":
                s.Append("<style>");
                for (int k = 0; k < r; k++)
                {
                    s.Append($".c{k}{{--v:{Hex(k)};fill:var(--v)}}");
                }
                s.Append("</style>");
                for (int i = 0; i < e; i++)
                {
                    s.Append(RectAt(i, $" class='c{i % r}'"));
                }
                break;
            case "C_typevar":
                // R rules, each declaring a custom property and reading it, over every rect.
                s.Append("<style>");
                for (int k = 0; k < r; k++)
                {
                    s.Append($"rect{{--v:{Hex(k)};fill:var(--v)}}");
                }
                s.Append("</style>");
                for (int i = 0; i < e; i++)
                {
                    s.Append(RectAt(i, ""));
                }
                break;
            case "C_typedecl":
                // R rules, each declaring its own custom property on every rect; one rule reads one.
                s.Append("<style>");
                for (int k = 0; k < r; k++)
                {
                    s.Append($"rect{{--v{k}:{Hex(k)}}}");
                }
                s.Append("rect{fill:var(--v0)}</style>");
                for (int i = 0; i < e; i++)
                {
                    s.Append(RectAt(i, ""));
                }
                break;
            case "C_chain":
                {
                    int d = P(p, "D", 10);
                    s.Append("<style>svg{--v0:#123456;");
                    for (int k = 1; k <= d; k++)
                    {
                        s.Append($"--v{k}:var(--v{k - 1});");
                    }
                    s.Append("}</style>");
                    for (int i = 0; i < e; i++)
                    {
                        s.Append(RectAt(i, $" style='fill:var(--v{d})'"));
                    }
                    break;
                }
            case "C_nest":
                {
                    // E rects at depth D under the root's custom property.
                    int d = P(p, "D", 10);
                    s.Append("<style>svg{--v0:#123456}</style>");
                    int per = Math.Max(1, e / Math.Max(1, P(p, "B", 10)));
                    for (int b = 0; b * per < e; b++)
                    {
                        for (int k = 0; k < d; k++)
                        {
                            s.Append("<g>");
                        }
                        for (int i = b * per; i < Math.Min(e, (b + 1) * per); i++)
                        {
                            s.Append(RectAt(i, " style='fill:var(--v0)'"));
                        }
                        for (int k = 0; k < d; k++)
                        {
                            s.Append("</g>");
                        }
                    }
                    break;
                }
            case "C_double":
                {
                    // Each property holds its predecessor twice: 2^D copies when resolved.
                    int d = P(p, "D", 8);
                    s.Append("<style>svg{--v0:1;");
                    for (int k = 1; k <= d; k++)
                    {
                        s.Append($"--v{k}:var(--v{k - 1}) var(--v{k - 1});");
                    }
                    s.Append("}</style>");
                    for (int i = 0; i < e; i++)
                    {
                        s.Append(RectAt(i, $" style='stroke-dasharray:var(--v{d})'"));
                    }
                    break;
                }
            case "N_desc":
                {
                    // R rules of K descendant-chained g compounds then rect, over a
                    // chain of D nested g (a rect in each), or (F=1) one g of N rects.
                    int k = P(p, "K", 2);
                    string sel = string.Join(" ", Enumerable.Repeat("g", k)) + " rect";
                    s.Append("<style>");
                    for (int j = 0; j < r; j++)
                    {
                        s.Append($"{sel}{{fill:{Hex(j)}}}");
                    }
                    s.Append("</style>");
                    if (P(p, "F", 0) == 1)
                    {
                        s.Append("<g>");
                        for (int i = 0; i < P(p, "N", 10000); i++)
                        {
                            s.Append(RectAt(i, ""));
                        }
                        s.Append("</g>");
                    }
                    else
                    {
                        int d = P(p, "D", 16);
                        for (int i = 0; i < d; i++)
                        {
                            s.Append("<g>").Append(RectAt(i, ""));
                        }
                        for (int i = 0; i < d; i++)
                        {
                            s.Append("</g>");
                        }
                    }
                    break;
                }
            case "N_sib":
                {
                    // R rules of K general-sibling-chained rect compounds over N rects.
                    int k = P(p, "K", 2);
                    string sel = string.Join(" ~ ", Enumerable.Repeat("rect", k));
                    s.Append("<style>");
                    for (int j = 0; j < r; j++)
                    {
                        s.Append($"{sel}{{fill:{Hex(j)}}}");
                    }
                    s.Append("</style>");
                    for (int i = 0; i < P(p, "N", 1000); i++)
                    {
                        s.Append(RectAt(i, ""));
                    }
                    break;
                }
            case "N_nth":
                {
                    // R rules of one pseudo-class (Q: 1 first-of-type, 2 nth-child,
                    // 3 nth-of-type) over N rects.
                    string sel = P(p, "Q", 1) switch
                    {
                        1 => "rect:first-of-type",
                        2 => "rect:nth-child(2n)",
                        _ => "rect:nth-of-type(3)",
                    };
                    s.Append("<style>");
                    for (int j = 0; j < r; j++)
                    {
                        s.Append($"{sel}{{fill:{Hex(j)}}}");
                    }
                    s.Append("</style>");
                    for (int i = 0; i < P(p, "N", 1000); i++)
                    {
                        s.Append(RectAt(i, ""));
                    }
                    break;
                }
            case "C_fb":
                {
                    // --vK: var(--v(K-1), fallback), the fallback naming --v(K-1) once (F=1) or twice (F=2).
                    int d = P(p, "D", 10);
                    int f = P(p, "F", 1);
                    s.Append("<style>svg{--v0:1;");
                    for (int level = 1; level <= d; level++)
                    {
                        string fallback = string.Join(" ", Enumerable.Repeat($"var(--v{level - 1})", f));
                        s.Append($"--v{level}:var(--v{level - 1}, {fallback});");
                    }
                    s.Append("}</style>");
                    for (int i = 0; i < e; i++)
                    {
                        s.Append(RectAt(i, $" style='stroke-dasharray:var(--v{d})'"));
                    }
                    break;
                }
            case "N_unstyled":
                {
                    // One rule of D declarations over N elements the guard's
                    // TakesStyleRules skips (T: 1 desc, 2 title, 3 stop); Q: 1 the
                    // same fill repeated, 2 distinct custom properties.
                    string tag = P(p, "T", 1) switch { 1 => "desc", 2 => "title", _ => "stop" };
                    int d = P(p, "D", 100);
                    s.Append($"<style>{tag}{{");
                    for (int j = 0; j < d; j++)
                    {
                        s.Append(P(p, "Q", 1) == 1 ? $"fill:{Hex(j)};" : $"--p{j}:{Hex(j)};");
                    }
                    s.Append("}</style>");
                    for (int i = 0; i < P(p, "N", 1000); i++)
                    {
                        s.Append($"<{tag}/>");
                    }
                    s.Append("<rect width='4' height='4'/>");
                    break;
                }
            case "D_nested":
                {
                    // An outer polyline (VO vertices, marker-mid) whose marker holds an
                    // inner polyline (VI vertices, marker-mid unless IM=0) whose marker
                    // holds M rects.
                    int m = P(p, "M", 10);
                    int vo = P(p, "VO", 100);
                    int vi = P(p, "VI", 100);
                    s.Append("<defs><marker id='mi' markerWidth='40' markerHeight='40' refX='0' refY='0' markerUnits='userSpaceOnUse'>");
                    for (int i = 0; i < m; i++)
                    {
                        s.Append($"<rect x='{i % 40}' y='{i / 40 % 40}' width='1' height='1' fill='red'/>");
                    }
                    s.Append("</marker><marker id='mo' markerWidth='400' markerHeight='400' refX='0' refY='0' markerUnits='userSpaceOnUse'>");
                    s.Append(P(p, "IM", 1) == 1
                        ? "<polyline fill='none' stroke='blue' marker-mid='url(#mi)' points='"
                        : "<polyline fill='none' stroke='blue' points='");
                    for (int i = 0; i < vi; i++)
                    {
                        s.Append(i % 40).Append(',').Append(i / 40 % 40).Append(' ');
                    }
                    s.Append("'/></marker></defs><polyline fill='none' stroke='black' marker-mid='url(#mo)' points='");
                    for (int i = 0; i < vo; i++)
                    {
                        s.Append(i % 400).Append(',').Append(i / 400 % 400).Append(' ');
                    }
                    s.Append("'/>");
                    break;
                }
            case "D_var":
            case "D_varinline":
                {
                    // A marker of M rects on a polyline of V vertices, set by
                    // marker-mid:var(--m) in a stylesheet rule or a style attribute.
                    int m = P(p, "M", 100);
                    int v = P(p, "V", 100);
                    if (kind == "D_var")
                    {
                        s.Append("<style>svg{--m:url(#m)}polyline{marker-mid:var(--m)}</style>");
                    }
                    s.Append("<defs><marker id='m' markerWidth='40' markerHeight='40' refX='0' refY='0' markerUnits='userSpaceOnUse'>");
                    for (int i = 0; i < m; i++)
                    {
                        s.Append($"<rect x='{i % 40}' y='{i / 40 % 40}' width='1' height='1' fill='red'/>");
                    }
                    s.Append("</marker></defs>");
                    string attr = kind == "D_varinline" ? " style='--m:url(#m);marker-mid:var(--m)'" : "";
                    s.Append($"<polyline fill='none' stroke='black'{attr} points='");
                    for (int i = 0; i < v; i++)
                    {
                        s.Append(i % 400).Append(',').Append(i / 400 % 400).Append(' ');
                    }
                    s.Append("'/>");
                    break;
                }
            case "C_kary":
                {
                    int d = P(p, "D", 8);
                    int k = P(p, "K", 2);
                    s.Append("<style>svg{--v0:1;");
                    for (int level = 1; level <= d; level++)
                    {
                        s.Append($"--v{level}:");
                        s.Append(string.Join(" ", Enumerable.Repeat($"var(--v{level - 1})", k)));
                        s.Append(';');
                    }
                    s.Append("}</style>");
                    for (int i = 0; i < e; i++)
                    {
                        s.Append(RectAt(i, $" style='stroke-dasharray:var(--v{d})'"));
                    }
                    break;
                }
            case "A_star_g":
                s.Append("<style>");
                for (int k = 0; k < r; k++)
                {
                    s.Append($"*{{fill:{Hex(k)}}}");
                }
                s.Append("</style>");
                for (int i = 0; i < e; i++)
                {
                    s.Append("<g/>");
                }
                break;
            case "C_triple":
                {
                    int d = P(p, "D", 8);
                    s.Append("<style>svg{--v0:1;");
                    for (int k = 1; k <= d; k++)
                    {
                        s.Append($"--v{k}:var(--v{k - 1}) var(--v{k - 1}) var(--v{k - 1});");
                    }
                    s.Append("}</style>");
                    for (int i = 0; i < e; i++)
                    {
                        s.Append(RectAt(i, $" style='stroke-dasharray:var(--v{d})'"));
                    }
                    break;
                }
            case "D_marker":
            case "D_css":
            case "D_shorthand":
            case "D_inherit":
            case "D_use":
            case "D_path":
            case "D_start":
                {
                    // A marker of M rects on L polylines (or paths) of V vertices,
                    // set by marker-mid unless the kind says otherwise.
                    int m = P(p, "M", 100);
                    int v = P(p, "V", 100);
                    int lines = P(p, "L", 1);
                    if (kind == "D_css")
                    {
                        s.Append("<style>polyline{marker-mid:url(#m)}</style>");
                    }
                    s.Append("<defs>");
                    if (kind == "D_use")
                    {
                        s.Append("<g id='mg'>");
                        for (int i = 0; i < m; i++)
                        {
                            s.Append($"<rect x='{i % 40}' y='{i / 40 % 40}' width='1' height='1' fill='red'/>");
                        }
                        s.Append("</g>");
                    }
                    s.Append("<marker id='m' markerWidth='40' markerHeight='40' refX='0' refY='0' markerUnits='userSpaceOnUse'>");
                    if (kind == "D_use")
                    {
                        s.Append("<use href='#mg'/>");
                    }
                    else
                    {
                        for (int i = 0; i < m; i++)
                        {
                            s.Append($"<rect x='{i % 40}' y='{i / 40 % 40}' width='1' height='1' fill='red'/>");
                        }
                    }
                    s.Append("</marker></defs>");
                    string attr = kind switch
                    {
                        "D_css" => "",
                        "D_shorthand" => " style='marker:url(#m)'",
                        "D_inherit" => "",
                        "D_start" => " marker-start='url(#m)' marker-end='url(#m)'",
                        _ => " marker-mid='url(#m)'",
                    };
                    if (kind == "D_inherit")
                    {
                        s.Append("<g marker-mid='url(#m)'>");
                    }
                    for (int l = 0; l < lines; l++)
                    {
                        if (kind == "D_path")
                        {
                            s.Append($"<path fill='none' stroke='black'{attr} d='M");
                            for (int i = 0; i < v; i++)
                            {
                                if (i == 1)
                                {
                                    s.Append(" L");
                                }
                                s.Append(' ').Append(i % 400).Append(',').Append((i / 400 + l) % 400);
                            }
                            s.Append("'/>");
                        }
                        else
                        {
                            s.Append($"<polyline fill='none' stroke='black'{attr} points='");
                            for (int i = 0; i < v; i++)
                            {
                                s.Append(i % 400).Append(',').Append((i / 400 + l) % 400).Append(' ');
                            }
                            s.Append("'/>");
                        }
                    }
                    if (kind == "D_inherit")
                    {
                        s.Append("</g>");
                    }
                    break;
                }
            case "D_paths":
                {
                    // N short two-vertex paths, each with marker-end, a marker of M rects.
                    int m = P(p, "M", 4);
                    int n = P(p, "N", 1000);
                    s.Append("<defs><marker id='m' markerWidth='8' markerHeight='8' refX='0' refY='0' markerUnits='userSpaceOnUse'>");
                    for (int i = 0; i < m; i++)
                    {
                        s.Append($"<rect x='{i % 8}' y='{i / 8 % 8}' width='1' height='1' fill='red'/>");
                    }
                    s.Append("</marker></defs>");
                    string mark = P(p, "nomark", 0) == 1 ? "" : " marker-end='url(#m)'";
                    int group = P(p, "group", 0);
                    for (int i = 0; i < n; i++)
                    {
                        if (group > 0 && i % group == 0)
                        {
                            s.Append("<g>");
                        }
                        s.Append($"<path d='M{i % 100 * 4} {i / 100 % 100 * 4}l3 3' stroke='black'{mark}/>");
                        if (group > 0 && (i % group == group - 1 || i == n - 1))
                        {
                            s.Append("</g>");
                        }
                    }
                    for (int i = 0; i < P(p, "X", 0); i++)
                    {
                        s.Append(RectAt(i, ""));
                    }
                    break;
                }
            case "D_rects_attr":
            case "D_circles_inherit":
                {
                    int m = P(p, "M", 100);
                    int n = P(p, "N", 100);
                    s.Append("<defs><marker id='m' markerWidth='40' markerHeight='40' refX='0' refY='0' markerUnits='userSpaceOnUse'>");
                    for (int i = 0; i < m; i++)
                    {
                        s.Append($"<rect x='{i % 40}' y='{i / 40 % 40}' width='1' height='1' fill='red'/>");
                    }
                    s.Append("</marker></defs>");
                    if (kind == "D_circles_inherit")
                    {
                        s.Append("<g marker-start='url(#m)' marker-mid='url(#m)' marker-end='url(#m)'>");
                    }
                    for (int i = 0; i < n; i++)
                    {
                        s.Append(kind == "D_circles_inherit"
                            ? $"<circle cx='{i % 100 * 4 + 2}' cy='{i / 100 % 100 * 4 + 2}' r='2'/>"
                            : RectAt(i, " marker-start='url(#m)' marker-mid='url(#m)' marker-end='url(#m)'"));
                    }
                    if (kind == "D_circles_inherit")
                    {
                        s.Append("</g>");
                    }
                    break;
                }
            case "D_use_poly":
                {
                    // A marked polyline of V vertices in defs, drawn by U uses.
                    int m = P(p, "M", 100);
                    int v = P(p, "V", 100);
                    int u = P(p, "U", 10);
                    s.Append("<defs><marker id='m' markerWidth='40' markerHeight='40' refX='0' refY='0' markerUnits='userSpaceOnUse'>");
                    for (int i = 0; i < m; i++)
                    {
                        s.Append($"<rect x='{i % 40}' y='{i / 40 % 40}' width='1' height='1' fill='red'/>");
                    }
                    s.Append("</marker><polyline id='pl' fill='none' stroke='black' marker-mid='url(#m)' points='");
                    for (int i = 0; i < v; i++)
                    {
                        s.Append(i % 400).Append(',').Append(i / 400 % 400).Append(' ');
                    }
                    s.Append("'/></defs>");
                    for (int i = 0; i < u; i++)
                    {
                        s.Append($"<use href='#pl' x='{i % 10}' y='{i / 10 % 10}'/>");
                    }
                    break;
                }
            case "D_rects_inherit":
                {
                    int m = P(p, "M", 100);
                    int n = P(p, "N", 100);
                    s.Append("<defs><marker id='m' markerWidth='40' markerHeight='40' refX='0' refY='0' markerUnits='userSpaceOnUse'>");
                    for (int i = 0; i < m; i++)
                    {
                        s.Append($"<rect x='{i % 40}' y='{i / 40 % 40}' width='1' height='1' fill='red'/>");
                    }
                    s.Append("</marker></defs><g marker-start='url(#m)' marker-mid='url(#m)' marker-end='url(#m)'>");
                    for (int i = 0; i < n; i++)
                    {
                        s.Append(RectAt(i, ""));
                    }
                    s.Append("</g>");
                    break;
                }
            case "Big_rects":
                {
                    // About 8 MiB of rects, under the guard's element cap.
                    int n = P(p, "N", 190_000);
                    for (int i = 0; i < n; i++)
                    {
                        s.Append($"<rect x='{i % 1000}' y='{i / 1000}' width='1' height='1' fill='#123'/>");
                    }
                    break;
                }
            case "Big_points":
                {
                    int n = P(p, "N", 1_000_000);
                    s.Append("<polyline fill='none' stroke='black' points='");
                    for (int i = 0; i < n; i++)
                    {
                        s.Append(i % 400).Append(',').Append(i / 400 % 400).Append(' ');
                    }
                    s.Append("'/>");
                    break;
                }
            case "Big_style":
                {
                    int n = P(p, "N", 400_000);
                    s.Append("<style>");
                    for (int k = 0; k < n; k++)
                    {
                        s.Append($".c{k}{{fill:{Hex(k)}}}");
                    }
                    s.Append("</style><rect width='4' height='4' class='c1'/>");
                    break;
                }
            case "T_varrule":
                {
                    // The red team's B1: one rule of B branches and V var()
                    // declarations, over one rect.
                    int b = P(p, "B", 100);
                    int v = P(p, "V", 10);
                    s.Append("<style>svg{--x:1}");
                    s.Append(string.Join(",", Enumerable.Repeat("a", b)));
                    s.Append('{');
                    for (int j = 0; j < v; j++)
                    {
                        s.Append($"p{j}:var(--x);");
                    }
                    s.Append("}</style><rect width='1' height='1'/>");
                    break;
                }
            case "T_dupid":
                {
                    // The red team's B1: N references to an id N elements share.
                    int n = P(p, "N", 100);
                    for (int i = 0; i < n; i++)
                    {
                        s.Append("<g fill='url(#x)'/>");
                    }
                    for (int i = 0; i < n; i++)
                    {
                        s.Append("<g id='x'/>");
                    }
                    break;
                }
            case "T_prelude":
                {
                    // One block whose prelude lists K classes and whose U
                    // declarations name eight ids, one of which a group carries.
                    int k = P(p, "K", 100);
                    int u = P(p, "U", 10);
                    s.Append("<style>");
                    s.Append(string.Join(",", Enumerable.Range(0, k).Select(i => $".k{i}")));
                    s.Append('{');
                    for (int j = 0; j < u; j++)
                    {
                        s.Append($"p{j}:url(#t{j % 8});");
                    }
                    s.Append("}</style><g id='t0'/>");
                    break;
                }
            case "T_gradvar":
                {
                    // Codex's round 2: N gradients sharing one id, and N
                    // distinct custom properties naming it.
                    int n = P(p, "N", 100);
                    s.Append("<style>svg{");
                    for (int i = 0; i < n; i++)
                    {
                        s.Append($"--p{i}:url(#x);");
                    }
                    s.Append("}</style><defs>");
                    for (int i = 0; i < n; i++)
                    {
                        s.Append("<linearGradient id='x'/>");
                    }
                    s.Append("</defs>");
                    break;
                }
            case "U_branches":
                {
                    // Codex's round 2: B class selectors, in one rule (S=1)
                    // or B rules (S=0), over a symbol of T rects that U uses
                    // draw.
                    int b = P(p, "B", 100);
                    int t = P(p, "T", 100);
                    int u = P(p, "U", 100);
                    s.Append("<style>");
                    if (P(p, "S", 1) == 1)
                    {
                        s.Append(string.Join(",", Enumerable.Range(0, b).Select(i => $".c{i}"))).Append("{fill:red}");
                    }
                    else
                    {
                        for (int i = 0; i < b; i++)
                        {
                            s.Append($".c{i}{{fill:red}}");
                        }
                    }
                    s.Append("</style><defs><symbol id='s'>");
                    for (int i = 0; i < t; i++)
                    {
                        s.Append(RectAt(i, ""));
                    }
                    s.Append("</symbol></defs>");
                    for (int i = 0; i < u; i++)
                    {
                        s.Append($"<use href='#s' x='{i % 40 * 10}' y='{i / 40 * 10}'/>");
                    }
                    break;
                }
            case "U_desc":
                {
                    // Codex's round 2: R rules of K descendant-chained g
                    // compounds then rect, over a symbol of D nested g (a rect
                    // in each) that U uses draw.
                    int k = P(p, "K", 4);
                    int d = P(p, "D", 32);
                    int u = P(p, "U", 100);
                    string sel = string.Join(" ", Enumerable.Repeat("g", k)) + " rect";
                    s.Append("<style>");
                    for (int j = 0; j < r; j++)
                    {
                        s.Append($"{sel}{{fill:{Hex(j)}}}");
                    }
                    s.Append("</style><defs><symbol id='s'>");
                    for (int i = 0; i < d; i++)
                    {
                        s.Append("<g>").Append(RectAt(i, ""));
                    }
                    for (int i = 0; i < d; i++)
                    {
                        s.Append("</g>");
                    }
                    s.Append("</symbol></defs>");
                    for (int i = 0; i < u; i++)
                    {
                        s.Append($"<use href='#s' x='{i % 40 * 10}' y='{i / 40 * 10}'/>");
                    }
                    break;
                }
            case "U_flat":
                {
                    // Codex's round 2: R rules of general siblings (Q=1,
                    // rect ~ rect) or first-of-type (Q=2) over a symbol of T
                    // sibling rects that U uses draw.
                    int t = P(p, "T", 300);
                    int u = P(p, "U", 20);
                    string sel = P(p, "Q", 1) == 1 ? "rect ~ rect" : "rect:first-of-type";
                    s.Append("<style>");
                    for (int j = 0; j < r; j++)
                    {
                        s.Append($"{sel}{{fill:{Hex(j)}}}");
                    }
                    s.Append("</style><defs><symbol id='s'>");
                    for (int i = 0; i < t; i++)
                    {
                        s.Append(RectAt(i, ""));
                    }
                    s.Append("</symbol></defs>");
                    for (int i = 0; i < u; i++)
                    {
                        s.Append($"<use href='#s' x='{i % 40 * 10}' y='{i / 40 * 10}'/>");
                    }
                    break;
                }
            case "T_urlnest":
                {
                    // Codex's round 3: one custom property whose quoted url()
                    // argument holds N more url( openings.
                    int n = P(p, "N", 100);
                    s.Append("<style>svg{--x:url(\"");
                    for (int i = 0; i < n; i++)
                    {
                        s.Append("url(");
                    }
                    s.Append("\")}</style><rect width='1' height='1'/>");
                    break;
                }
            case "M_use":
                {
                    // Codex's round 3: R class rules, E rects, and a polyline of
                    // V vertices whose marker holds a use of a rect (U=1) or
                    // the rect itself (U=0).
                    int e2 = P(p, "E", 1000);
                    int v = P(p, "V", 2000);
                    s.Append("<style>");
                    for (int j = 0; j < r; j++)
                    {
                        s.Append($".c{j}{{fill:{Hex(j)}}}");
                    }
                    // N=2: the marker's use draws a group that holds a use of
                    // the rect.
                    s.Append("</style><defs><rect id='r' width='1' height='1'/><g id='g'><use href='#r'/></g>");
                    s.Append("<marker id='m' markerWidth='4' markerHeight='4' refX='0' refY='0' markerUnits='userSpaceOnUse'>");
                    s.Append(P(p, "U", 1) == 0 ? "<rect width='1' height='1'/>"
                        : P(p, "N", 1) == 2 ? "<use href='#g'/>" : "<use href='#r'/>");
                    s.Append("</marker></defs>");
                    for (int i = 0; i < e2; i++)
                    {
                        s.Append(RectAt(i, ""));
                    }
                    s.Append("<polyline fill='none' stroke='black' marker-mid='url(#m)' points='");
                    for (int i = 0; i < v; i++)
                    {
                        s.Append(i % 400).Append(',').Append(i / 400 % 400).Append(' ');
                    }
                    s.Append("'/>");
                    break;
                }
            case "U_decls":
                {
                    // Codex's round 3: one rule of D custom properties over a
                    // symbol of T rects that U uses draw.
                    int d = P(p, "D", 100);
                    int t = P(p, "T", 100);
                    int u = P(p, "U", 100);
                    s.Append("<style>rect{");
                    for (int j = 0; j < d; j++)
                    {
                        s.Append($"--p{j}:{j};");
                    }
                    s.Append("}</style><defs><symbol id='s'>");
                    for (int i = 0; i < t; i++)
                    {
                        s.Append(RectAt(i, ""));
                    }
                    s.Append("</symbol></defs>");
                    for (int i = 0; i < u; i++)
                    {
                        s.Append($"<use href='#s' x='{i % 40 * 10}' y='{i / 40 * 10}'/>");
                    }
                    break;
                }
            case "P_use":
                {
                    // R class rules, and a pattern holding a use of a rect
                    // (U=1) or the rect (U=0) that K rects are painted with.
                    int k = P(p, "K", 100);
                    s.Append("<style>");
                    for (int j = 0; j < r; j++)
                    {
                        s.Append($".c{j}{{fill:{Hex(j)}}}");
                    }
                    s.Append("</style><defs><rect id='r' width='1' height='1'/>");
                    s.Append("<pattern id='p' width='4' height='4' patternUnits='userSpaceOnUse'>");
                    s.Append(P(p, "U", 1) == 1 ? "<use href='#r'/>" : "<rect width='1' height='1'/>");
                    s.Append("</pattern></defs>");
                    for (int i = 0; i < k; i++)
                    {
                        s.Append(RectAt(i, " fill='url(#p)'"));
                    }
                    break;
                }
            case "A_keyframes":
                {
                    // The red team's M2: an animation export's shape, one
                    // @keyframes of five stops per animated element, A of
                    // them, over E rects.
                    int a = P(p, "A", 100);
                    int total = P(p, "E", 1000);
                    s.Append("<style>");
                    for (int k = 0; k < a; k++)
                    {
                        s.Append($"#e{k}{{animation:k{k} 2s infinite}}@keyframes k{k}{{");
                        foreach (string stop in (string[])["0%", "25%", "50%", "75%", "100%"])
                        {
                            s.Append($"{stop}{{transform:translate({k % 7}px,0);opacity:0.5}}");
                        }
                        s.Append('}');
                    }
                    s.Append("</style>");
                    for (int i = 0; i < total; i++)
                    {
                        s.Append($"<rect id='e{i}' x='{i % 40}' y='{i / 40}' width='1' height='1'/>");
                    }
                    break;
                }
            case "P_paint":
                {
                    // The merge judge's register check (stage 7): K rects
                    // painted with a pattern swatch of ten children, by a fill
                    // each, by a group they inherit it from (I=1; with U, the
                    // uses that draw them carry it), by a
                    // stylesheet rule (S=1) or through var() (V=1, a
                    // stylesheet's definition; V=2, a group's style attribute
                    // around everything), drawn once, or held by a symbol
                    // that U uses draw; then X plain rects.
                    int k = P(p, "K", 1000);
                    int u = P(p, "U", 0);
                    int throughVar = P(p, "V", 0);
                    bool inherit = P(p, "I", 0) == 1;
                    string paint = inherit ? ""
                        : P(p, "S", 0) == 1 ? " class='p'"
                        : throughVar > 0 ? " style='fill:var(--p)'"
                        : " fill='url(#pat)'";
                    if (P(p, "S", 0) == 1)
                    {
                        s.Append("<style>rect.p{fill:url(#pat)}</style>");
                    }
                    if (throughVar == 1)
                    {
                        s.Append("<style>svg{--p:url(#pat)}</style>");
                    }
                    if (throughVar == 2)
                    {
                        s.Append("<g style='--p:url(#pat)'>");
                    }
                    var painted = new StringBuilder(inherit && u == 0
                        ? "<g fill='url(#pat)' stroke='#333' stroke-width='0.5'>"
                        : "<g stroke='#333' stroke-width='0.5'>");
                    for (int i = 0; i < k; i++)
                    {
                        painted.Append(RectAt(i, paint));
                    }
                    painted.Append("</g>");
                    s.Append("<defs><pattern id='pat' width='12' height='12' patternUnits='userSpaceOnUse'>")
                        .Append("<rect width='12' height='12' fill='#f4f1e8'/>")
                        .Append("<path d='M 0 12 L 12 0' stroke='#5b4a3a' stroke-width='0.8'/>")
                        .Append("<path d='M -3 3 L 3 -3' stroke='#5b4a3a' stroke-width='0.8'/>")
                        .Append("<path d='M 9 15 L 15 9' stroke='#5b4a3a' stroke-width='0.8'/>")
                        .Append("<path d='M 0 6 L 6 0' stroke='#8a7a6a' stroke-width='0.4'/>")
                        .Append("<circle cx='3' cy='9' r='1' fill='#2f6f8f'/><circle cx='9' cy='3' r='1' fill='#2f6f8f'/>")
                        .Append("<circle cx='9' cy='9' r='0.6' fill='#8f2f4f'/><circle cx='3' cy='3' r='0.6' fill='#8f2f4f'/>")
                        .Append("<polygon points='5,5 7,5 6,7' fill='#3a5b4a'/></pattern>");
                    if (u > 0)
                    {
                        s.Append("<symbol id='s'>").Append(painted).Append("</symbol></defs>");
                        for (int i = 0; i < u; i++)
                        {
                            s.Append(inherit ? "<use href='#s' fill='url(#pat)'/>" : "<use href='#s'/>");
                        }
                    }
                    else
                    {
                        s.Append("</defs>").Append(painted);
                    }
                    for (int i = 0; i < P(p, "X", 0); i++)
                    {
                        s.Append(RectAt(i, " fill='#cc9'"));
                    }
                    if (throughVar == 2)
                    {
                        s.Append("</g>");
                    }
                    break;
                }
            default:
                throw new ArgumentException("unknown kind " + kind, nameof(kind));
        }
        s.Append("</svg>");
        return s.ToString();
    }
}
