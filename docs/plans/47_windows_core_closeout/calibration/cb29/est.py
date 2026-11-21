# Copyright (C) 2026 Cory Joseph
# SPDX-License-Identifier: AGPL-3.0-or-later
# Committed copy of CB-29's step 0 estimator (#1610). It ran in the chunk's
# working notes, beside the step 0 CSVs, the real and mermaid SVGs and the
# generator tool it calls; those stay there. The generators are
# apps/slate-windows/tests/SlateWindows.Tests/Support/SlowSvgDocuments.cs.
"""CB-29 worker 1, follow-up: the one estimated decode time, computed the way
the guard could compute it, over every document decoded in step 0.

Reads the decoded rows from the CSVs, regenerates each generated document
with regen-tool (the probe's own generator code), reads the real documents
from real\\ and mermaid\\, and writes est.csv (one row per document) for
decision.py.
"""
import csv
import re
import statistics
import subprocess
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

W1 = Path(__file__).parent
REGEN = W1 / "regen"
CSVS = ["sweep-A.csv", "sweep-C.csv", "sweep-D.csv", "sweep-X.csv", "sweep-big.csv",
        "explore2.csv", "explore3.csv", "explore4.csv", "real.csv", "real-mermaid.csv"]

# The rates the principal fixed (microseconds).
RATE_QUERY = 0.0935
RATE_MATCH = 1.03
RATE_RULE = 4.5
RATE_USE = 0.2465
RATE_VAR = 0.192
RATE_MARKER = 2.548
PLACEMENT_WEIGHT = 20
VAR_LEVELS = 15

GEOMETRY = {"cx", "cy", "d", "height", "r", "rx", "ry", "transform-box", "transform-origin",
            "width", "x", "x1", "x2", "y", "y1", "y2"}
NOT_DRAWN_REFS = {"a", "animate", "animateMotion", "animateColor", "animateTransform", "set", "discard", "mpath"}
MARKABLE = {"path", "line", "polyline", "polygon", "rect", "circle", "ellipse"}


def local(tag):
    return tag.rsplit("}", 1)[-1] if isinstance(tag, str) else ""


def takes_style_rules(name):
    if len(name) > 2 and name.startswith("fe") and name[2].isupper():
        return False
    return name not in ("linearGradient", "radialGradient", "filter", "stop", "title", "desc", "metadata", "style")


def is_gradient(name):
    return name.lower() in ("lineargradient", "radialgradient")


# ---------------------------------------------------------------- CSS

def strip_comments(css):
    out = []
    i = 0
    n = len(css)
    while i < n:
        if css.startswith("/*", i):
            j = css.find("*/", i + 2)
            i = n if j < 0 else j + 2
            continue
        out.append(css[i])
        i += 1
    return "".join(out)


def skip_string(text, i):
    q = text[i]
    j = i + 1
    while j < len(text) and text[j] != q and text[j] not in "\n\r\f":
        if text[j] == "\\":
            j += 1
        j += 1
    return j + 1


def css_rules(css):
    """(prelude, body) for every style rule, recursing into at-rule blocks."""
    rules = []
    text = strip_comments(css)
    i = 0
    n = len(text)
    while i < n:
        # Find the next '{' outside strings.
        j = i
        while j < n and text[j] != "{":
            if text[j] in "\"'":
                j = skip_string(text, j)
                continue
            j += 1
        if j >= n:
            break
        prelude = text[i:j].strip()
        depth = 1
        k = j + 1
        while k < n and depth > 0:
            c = text[k]
            if c in "\"'":
                k = skip_string(text, k)
                continue
            if c == "{":
                depth += 1
            elif c == "}":
                depth -= 1
            k += 1
        body = text[j + 1:max(j + 1, k - 1)]
        semi = prelude.rfind(";")
        if semi >= 0:
            prelude = prelude[semi + 1:].strip()
        if prelude.startswith("@"):
            if "{" in body:
                rules.extend(css_rules(body))
        else:
            rules.append((prelude, body))
        i = k
    return rules


def split_top(text, sep):
    parts = []
    depth = 0
    start = 0
    i = 0
    n = len(text)
    while i < n:
        c = text[i]
        if c in "\"'":
            i = skip_string(text, i)
            continue
        if c in "([":
            depth += 1
        elif c in ")]":
            depth = max(0, depth - 1)
        elif c == sep and depth == 0:
            parts.append(text[start:i])
            start = i + 1
        i += 1
    parts.append(text[start:])
    return [p.strip() for p in parts if p.strip()]


def declarations(body):
    out = []
    for d in split_top(body, ";"):
        idx = d.find(":")
        if idx > 0:
            out.append((d[:idx].strip(), d[idx + 1:].strip()))
    return out


NAME_CHARS = re.compile(r"[A-Za-z0-9_\-\u0080-￿]")


def subject_key(selector):
    """The filing key of a selector's subject (its rightmost compound): its id,
    else its first class, else its type; '*' when none (attribute-only,
    pseudo-only or universal)."""
    s = selector.strip()
    # Split off the rightmost compound at a combinator outside brackets.
    depth = 0
    cut = 0
    i = 0
    while i < len(s):
        c = s[i]
        if c in "\"'":
            i = skip_string(s, i)
            continue
        if c in "([":
            depth += 1
        elif c in ")]":
            depth = max(0, depth - 1)
        elif depth == 0 and (c.isspace() or c in ">+~"):
            cut = i + 1
        i += 1
    comp = s[cut:].strip()
    i = 0
    typ = None
    first_class = None
    ident = None

    def read_name(j):
        k = j
        while k < len(comp) and NAME_CHARS.match(comp[k]):
            k += 1
        return comp[j:k], k

    if comp.startswith("*"):
        i = 1
    else:
        name, i2 = read_name(0)
        if name:
            typ = name
            i = i2
    while i < len(comp):
        c = comp[i]
        if c == ".":
            name, i = read_name(i + 1)
            if name and first_class is None:
                first_class = name
        elif c == "#":
            name, i = read_name(i + 1)
            if name:
                ident = name
        elif c == "[":
            depth = 1
            i += 1
            while i < len(comp) and depth:
                if comp[i] in "\"'":
                    i = skip_string(comp, i)
                    continue
                if comp[i] == "[":
                    depth += 1
                elif comp[i] == "]":
                    depth -= 1
                i += 1
        elif c == ":":
            i += 1
            if i < len(comp) and comp[i] == ":":
                i += 1
            _, i = read_name(i)
            if i < len(comp) and comp[i] == "(":
                depth = 1
                i += 1
                while i < len(comp) and depth:
                    if comp[i] == "(":
                        depth += 1
                    elif comp[i] == ")":
                        depth -= 1
                    i += 1
        else:
            i += 1
    if ident:
        return "#" + ident.lower()
    if first_class:
        return "." + first_class.lower()
    if typ:
        return typ.lower()
    return "*"


def rightmost_compound(selector):
    s = selector.strip()
    depth = 0
    cut = 0
    i = 0
    while i < len(s):
        c = s[i]
        if c in "\"'":
            i = skip_string(s, i)
            continue
        if c in "([":
            depth += 1
        elif c in ")]":
            depth = max(0, depth - 1)
        elif depth == 0 and (c.isspace() or c in ">+~"):
            cut = i + 1
        i += 1
    return s[cut:].strip(), cut > 0


def has_combinator(selector):
    return rightmost_compound(selector)[1]


ATTR = re.compile(r"\[\s*([A-Za-z_:][-A-Za-z0-9_:.]*)\s*(?:([~|^$*]?=)\s*(\"[^\"]*\"|'[^']*'|[^\]\s]+))?\s*\]")


def subject_key2(selector):
    """Variant 2: as subject_key, but an attribute-only subject files under
    "[name=value]" (an exact match) or "[name]" (any other operator)."""
    key = subject_key(selector)
    if key != "*":
        return key
    comp, _ = rightmost_compound(selector)
    m = ATTR.search(comp)
    if not m:
        return "*"
    name = m.group(1).lower()
    if m.group(2) == "=":
        value = m.group(3).strip("\"'")
        return f"[{name}={value}]"
    return f"[{name}]"


URL = re.compile(r"url\(\s*['\"]?\s*#([^)'\"\s]+)", re.IGNORECASE)
VAR = re.compile(r"var\(\s*(--[A-Za-z0-9_\-\u0080-￿]+)", re.IGNORECASE)


def numbers(text):
    return len(re.findall(r"[+-]?(?:\d+\.?\d*|\.\d+)(?:[eE][+-]?\d+)?", text))


def path_vertices(d):
    total = 0
    for cmd, args in re.findall(r"([MmLlHhVvCcSsQqTtAaZz])([^MmLlHhVvCcSsQqTtAaZz]*)", d):
        per = {"M": 2, "L": 2, "T": 2, "H": 1, "V": 1, "C": 6, "S": 4, "Q": 4, "A": 7, "Z": 0}[cmd.upper()]
        if per == 0:
            total += 1
        else:
            total += numbers(args) // per
    return total


def vertices(el, name):
    if name == "line":
        return 2
    if name in ("polyline", "polygon"):
        n = numbers(el.get("points", "")) // 2
        return n + (1 if name == "polygon" and n >= 1 else 0)
    if name == "path":
        return path_vertices(el.get("d", ""))
    if name in ("rect", "circle", "ellipse"):
        return 5
    return 0


# ---------------------------------------------------------------- estimate

def estimate(source):
    root = ET.fromstring(source)
    elements = []
    parent = {}
    for el in root.iter():
        elements.append(el)
        for ch in el:
            parent[ch] = el
    names = {el: local(el.tag) for el in elements}
    e_all = len(elements)
    index = {el: i for i, el in enumerate(elements)}
    by_id = {}
    for el in elements:
        i = el.get("id")
        if i is not None and i not in by_id:
            by_id[i] = el

    style_elements = [el for el in elements if names[el] == "style"]
    has_style = len(style_elements) > 0
    css = "\n".join("".join(el.itertext()) for el in style_elements)

    # Keys of each styled element.
    def keys_of(el):
        ks = {"*", names[el].lower()}
        if el.get("id") is not None:
            ks.add("#" + el.get("id").lower())
        cls = el.get("class")
        if cls:
            for c in set(x.lower() for x in cls.split()):
                ks.add("." + c)
        return ks

    styled = [el for el in elements if takes_style_rules(names[el])]
    el_keys = {el: keys_of(el) for el in styled}
    key_count = {}
    for el in styled:
        for k in el_keys[el]:
            key_count[k] = key_count.get(k, 0) + 1

    # Variant 2 keys: an element's attributes too, so an attribute-only
    # subject files under "[name=value]" or "[name]".
    def keys2_of(el):
        ks = set(el_keys[el])
        for an, av in el.attrib.items():
            a = local(an).lower()
            ks.add(f"[{a}]")
            ks.add(f"[{a}={av}]")
        return ks

    el_keys2 = {el: keys2_of(el) for el in styled}
    rules = css_rules(css)
    n_rules = 0
    query_units = 0
    query_units2 = 0
    decls_by_key = {}
    match2_by_key = {}
    rule_info = []  # (keys, decls)
    custom_defs = {}
    for prelude, body in rules:
        n_rules += 1
        decls = declarations(body)
        branches = split_top(prelude, ",")
        if not decls or not branches:
            continue
        has_custom = any(nm.startswith("--") for nm, _ in decls)
        has_geom = any(nm.lower() in GEOMETRY for nm, _ in decls)
        passes = 1 + int(has_custom) + int(has_geom)
        query_units += len(branches) * passes * e_all
        query_units2 += sum((2 if has_combinator(b) else 1) for b in branches) * passes * e_all
        keys = [subject_key(b) for b in branches]
        for k in keys:
            decls_by_key[k] = decls_by_key.get(k, 0) + len(decls)
        weight2 = sum(0.5 if (nm.startswith("--") or "var(" in v.lower()) else RATE_MATCH for nm, v in decls)
        for b in branches:
            k2 = subject_key2(b)
            match2_by_key[k2] = match2_by_key.get(k2, 0.0) + weight2
        rule_info.append((keys, decls))
        for nm, val in decls:
            if nm.startswith("--"):
                custom_defs.setdefault(nm, []).append(val)

    matched = 0
    for el in styled:
        for k in el_keys[el]:
            matched += decls_by_key.get(k, 0)
    match2_us = 0.0
    for el in styled:
        for k in el_keys2[el]:
            match2_us += match2_by_key.get(k, 0.0)
    key_count2 = {}
    for el in styled:
        for k in el_keys2[el]:
            key_count2[k] = key_count2.get(k, 0) + 1

    # Inline custom-property definitions.
    for el in elements:
        st = el.get("style")
        if st and "--" in st:
            for nm, val in declarations(st):
                if nm.startswith("--"):
                    custom_defs.setdefault(nm, []).append(val)

    memo = {}

    def exp_name(name, level):
        if level > VAR_LEVELS:
            return 1
        key = (name, level)
        if key in memo:
            return memo[key]
        memo[key] = 1  # cycle guard
        vals = custom_defs.get(name)
        best = 1
        if vals:
            best = max(copies_value(v, level) for v in vals)
        memo[key] = best
        return best

    def copies_value(value, level):
        refs = VAR.findall(value)
        if not refs:
            return 1
        return sum(exp_name(r, level + 1) for r in refs)

    # Variant 2: resolve as Svg.Custom does, left to right, giving up at the
    # first reference whose chain passes the level limit.
    memo2 = {}

    def resolve_name(name, level):
        # A name's value is resolved at depth 2 x level; Svg.Custom gives up past 32.
        if level > VAR_LEVELS + 1:
            return False, 1
        key = (name, level)
        if key in memo2:
            return memo2[key]
        memo2[key] = (False, 1)  # cycle guard
        vals = custom_defs.get(name)
        if not vals:
            result = (True, 1)
        else:
            outcomes = [resolve_value(v, level) for v in vals]
            result = max(outcomes, key=lambda o: o[1])
        memo2[key] = result
        return result

    def resolve_value(value, level):
        refs = VAR.findall(value)
        if not refs:
            return True, 1
        total = 0
        for r in refs:
            ok, c = resolve_name(r, level + 1)
            total += c
            if not ok:
                return False, total
        return True, total

    var_copies2 = 0
    for keys, decls in rule_info:
        landed = sum(key_count.get(k, 0) for k in keys)
        for nm, val in decls:
            if nm.startswith("--") or "var(" not in val.lower():
                continue
            var_copies2 += resolve_value(val, 0)[1] * landed
    for el in elements:
        st = el.get("style")
        if st and "var(" in st.lower():
            for nm, val in declarations(st):
                if not nm.startswith("--") and "var(" in val.lower():
                    var_copies2 += resolve_value(val, 0)[1]
        for an, av in el.attrib.items():
            if local(an) != "style" and "var(" in av.lower():
                var_copies2 += resolve_value(av, 0)[1]

    var_copies = 0
    for keys, decls in rule_info:
        landed = sum(key_count.get(k, 0) for k in keys)
        for nm, val in decls:
            if nm.startswith("--") or "var(" not in val.lower():
                continue
            var_copies += copies_value(val, 0) * landed
    for el in elements:
        st = el.get("style")
        if st and "var(" in st.lower():
            for nm, val in declarations(st):
                if not nm.startswith("--") and "var(" in val.lower():
                    var_copies += copies_value(val, 0)
        for an, av in el.attrib.items():
            if local(an) != "style" and "var(" in av.lower():
                var_copies += copies_value(av, 0)

    # Reference graph: parent edges, href edges, url() edges, stylesheet url()
    # targets through each element's keys.
    out = {el: [] for el in elements}
    for el in elements:
        for ch in el:
            out[el].append(ch)
    rule_urls_by_key = {}
    for keys, decls in rule_info:
        targets = [t for _, v in decls for t in URL.findall(v)]
        if targets:
            for k in keys:
                rule_urls_by_key.setdefault(k, []).extend(targets)

    def add_ref(el, target_id):
        t = by_id.get(target_id)
        if t is not None and not is_gradient(names[t]):
            out[el].append(t)

    for el in elements:
        nm = names[el]
        for an, av in el.attrib.items():
            a = local(an)
            if a.lower() == "href":
                v = av.strip()
                if v.startswith("#") and nm not in NOT_DRAWN_REFS:
                    add_ref(el, v[1:])
            else:
                for t in URL.findall(av):
                    add_ref(el, t)
        if el in el_keys:
            for k in el_keys[el]:
                for t in rule_urls_by_key.get(k, ()):
                    add_ref(el, t)

    # paths (Measure's render counts), by Kahn's order.
    indeg = {el: 0 for el in elements}
    for el in elements:
        for t in out[el]:
            indeg[t] += 1
    paths = {el: (1 if indeg[el] == 0 else 0) for el in elements}
    ready = [el for el in elements if indeg[el] == 0]
    reached = 0
    while ready:
        el = ready.pop()
        reached += 1
        for t in out[el]:
            paths[t] += paths[el]
            indeg[t] -= 1
            if indeg[t] == 0:
                ready.append(t)
    cycle = reached < len(elements)

    sys.setrecursionlimit(100000)
    renders_memo = {}

    def renders(el, stack=None):
        if el in renders_memo:
            return renders_memo[el]
        renders_memo[el] = 1
        total = 1
        for t in out[el]:
            total += renders(t)
        renders_memo[el] = total
        return total

    use_renders = sum(paths[el] for el in elements if names[el] == "use")

    # Markers: own declarations (presentation attribute, stylesheet rule
    # filed under the element's keys, style attribute), else inherited.
    marker_rules_by_key = {}
    for keys, decls in rule_info:
        props = [(nm.lower(), v) for nm, v in decls if nm.lower() in ("marker", "marker-start", "marker-mid", "marker-end")]
        if props:
            for k in keys:
                marker_rules_by_key.setdefault(k, []).extend(props)

    def marker_id(value):
        v = value.strip()
        if v.lower() == "none":
            return ""
        m = URL.search(v)
        return m.group(1) if m else None

    effective = {}
    marker_units = 0
    marked_elements = 0
    positions_total = 0
    for el in elements:  # document order: parents first
        inherited = effective.get(parent.get(el), {"start": None, "mid": None, "end": None})
        own = {"start": [], "mid": [], "end": []}

        def apply(nm, v):
            mid = marker_id(v)
            if mid is None:
                return
            slots = ["start", "mid", "end"] if nm == "marker" else [nm.split("-")[1]]
            for s in slots:
                own[s].append(mid)

        for an, av in el.attrib.items():
            a = local(an).lower()
            if a in ("marker", "marker-start", "marker-mid", "marker-end"):
                apply(a, av)
        if el in el_keys:
            for k in el_keys[el]:
                for nm, v in marker_rules_by_key.get(k, ()):
                    apply(nm, v)
        st = el.get("style")
        if st and "marker" in st:
            for nm, v in declarations(st):
                if nm.lower() in ("marker", "marker-start", "marker-mid", "marker-end"):
                    apply(nm.lower(), v)
        eff = {}
        for s in ("start", "mid", "end"):
            if own[s]:
                # The widest of the element's own declarations.
                best = None
                best_r = -1
                for mid in own[s]:
                    r = renders(by_id[mid]) if mid and mid in by_id else 0
                    if r > best_r:
                        best, best_r = mid, r
                eff[s] = best
            else:
                eff[s] = inherited[s]
        effective[el] = eff
        nm = names[el]
        if nm not in MARKABLE:
            continue
        rs = {s: (renders(by_id[eff[s]]) if eff[s] and eff[s] in by_id else 0) for s in ("start", "mid", "end")}
        if not any(rs.values()):
            continue
        v = vertices(el, nm)
        pos = {"start": 1 if v >= 1 else 0, "mid": max(0, v - 2), "end": 1 if v >= 1 else 0}
        per = sum(pos[s] * (rs[s] + PLACEMENT_WEIGHT) for s in rs if rs[s]) + max(rs.values())
        marker_units += paths[el] * per
        marked_elements += 1
        positions_total += paths[el] * sum(pos[s] for s in rs if rs[s])

    terms = {
        "query": RATE_QUERY * query_units,
        "match": RATE_MATCH * matched,
        "rules": RATE_RULE * n_rules,
        "use": RATE_USE * use_renders * (e_all + 15 * n_rules) if has_style else 0.0,
        "var": RATE_VAR * var_copies,
        "marker": RATE_MARKER * marker_units,
    }
    # Variant 2: the instance's own subtree per use render (22.2 us + 0.0666
    # us per rule, per instance element), only under a stylesheet.
    instance_us = 0.0
    if has_style:
        for el in elements:
            if names[el] != "use":
                continue
            href = None
            for an, av in el.attrib.items():
                if local(an).lower() == "href":
                    href = av.strip()
            if href and href.startswith("#") and href[1:] in by_id:
                instance_us += paths[el] * renders(by_id[href[1:]]) * (22.2 + 0.0666 * n_rules)
    terms2 = {
        "query": RATE_QUERY * query_units2,
        "match": match2_us,
        "rules": RATE_RULE * n_rules,
        "use": terms["use"] + instance_us,
        "var": RATE_VAR * var_copies2,
        "marker": RATE_MARKER * marker_units,
    }
    counts = {
        "elementsAll": e_all, "rules": n_rules, "queryUnits": query_units, "matched": matched,
        "hasStyle": int(has_style), "useRenders": use_renders, "varCopies": var_copies,
        "markerUnits": marker_units, "markedElements": marked_elements, "positions": positions_total,
        "cycle": int(cycle),
    }
    return terms, counts, terms2


# ---------------------------------------------------------------- driver

def num(x):
    try:
        return float(x)
    except (TypeError, ValueError):
        return None


def main():
    docs = {}
    for name in CSVS:
        p = W1 / name
        if not p.exists():
            continue
        with p.open(newline="") as f:
            for r in csv.DictReader(f):
                label = r["label"]
                d = docs.setdefault(label, {"label": label, "kind": r["kind"], "params": r["params"],
                                            "bytes": int(float(r["bytes"])), "decodes": [], "guard": r["guard"],
                                            "files": set()})
                d["files"].add(name)
                ms = num(r["decodeMs"])
                if ms is not None and ms > 0:
                    d["decodes"].append(ms)
    # Regenerate the generated documents.
    REGEN.mkdir(exist_ok=True)
    listing = []
    for i, d in enumerate(docs.values()):
        if d["kind"] == "real":
            continue
        d["file"] = f"doc{i:03d}"
        listing.append(f"{d['file']}\t{d['kind']}\t{d['params']}")
    (W1 / "regen-list.txt").write_text("\n".join(listing) + "\n")
    subprocess.run([str(W1 / "regen-tool" / "bin" / "regen.exe"), str(W1 / "regen-list.txt"), str(REGEN)], check=True)

    rows = []
    for d in docs.values():
        if d["kind"] == "real":
            fname = d["params"]
            path = W1 / "real" / fname
            if not path.exists():
                path = W1 / "mermaid" / fname
        else:
            path = REGEN / (d["file"] + ".svg")
        data = path.read_bytes()
        same = len(data) == d["bytes"]
        terms, counts, terms2 = estimate(data.decode("utf-8"))
        est_ms = sum(terms.values()) / 1000.0
        counts["est2Ms"] = f"{sum(terms2.values()) / 1000.0:.1f}"
        counts["sheet2Ms"] = f"{(terms2['query'] + terms2['match']) / 1000.0:.1f}"
        actual = statistics.median(d["decodes"]) if d["decodes"] else None
        row = {"label": d["label"], "kind": d["kind"], "bytes": d["bytes"], "bytesMatch": int(same),
               "guardToday": d["guard"], "decodes": len(d["decodes"]),
               "actualMs": f"{actual:.0f}" if actual is not None else "",
               "estMs": f"{est_ms:.1f}"}
        for k, v in terms.items():
            row[k + "Ms"] = f"{v / 1000.0:.1f}"
        row.update(counts)
        rows.append(row)
        if not same:
            print("BYTES DIFFER", d["label"], len(data), d["bytes"], file=sys.stderr)
    with (W1 / "est.csv").open("w", newline="") as f:
        w = csv.DictWriter(f, fieldnames=list(rows[0].keys()))
        w.writeheader()
        w.writerows(rows)
    print(f"{len(rows)} documents")


if __name__ == "__main__":
    main()
