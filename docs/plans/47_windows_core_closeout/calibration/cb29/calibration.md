# CB-29 step 0: SVG decode cost calibration on main (worker 1)

> This is the committed copy of CB-29's step 0 record (#1610, CB-29's spec, Call 12), with its local paths removed. Four of its files are kept here: this one, `decision-tables.md`, `est.py` and `est.csv` (the `est2Ms` column is the estimate `SvgPreviewGuardTests.EstimatesTrackTheCalibration` holds the guard to). The other files this record names (the raw CSVs and logs, the plans, the probe, the IL lister and its listings, the generated SVGs) stayed in the chunk's working notes; the generators are `apps/slate-windows/tests/SlateWindows.Tests/Support/SlowSvgDocuments.cs`, which rebuilds every generated document byte for byte. `est.py` ran in that folder, beside the CSVs and the generator tool it reads.

Issue #1610, plan 47 wave C8, chunk CB-29. Measured 2026-11-19 between 12:35 and 13:34 local time, on `main` 16ad788d in the chunk's own worktree (branch `claude/cb29-svg-cost-guard`, no commits), with a Cargo target directory of its own. No product code was changed. The probe was an untracked test file, `apps/slate-windows/tests/SlateWindows.Tests/Cb29Step0Probe.cs`, deleted from the worktree at the end; its copy is `Cb29Step0Probe.cs` in the working notes.

## Summary

- The issue's two products are each one term of a larger cost, and they miss three costs that dominate other slow documents.
- **Stylesheet.** Two terms, not one. Every comma-separated selector is a whole-tree query: about **0.093 us per (selector x element)**, the element count being every element of the document. Every element a rule actually matches costs about **1.03 us per matched paint declaration** (0.29 us for a custom-property declaration). "Rules x elements" alone mispredicts by up to 11x between class rules (one match per element) and type or universal rules (every rule matches every element).
- **Use instances under any stylesheet** (not in the issue). With even a one-rule `<style>`, Svg.Skia re-applies the stylesheet to every `<use>` instance it draws and drops the computed-style cache. Cost about **0.25 us x use renders x all elements + 3.7 us x use renders x rules**. A one-rule stylesheet over 10,000 uses decodes in 30 s; a plotting-library-shaped scatter of 10,000 points (my generated guess at the shape, not a downloaded file) in 17 s, and in 1.0 s without the rule.
- **var() expansion** (not in the issue). Svg.Custom resolves `var()` with no memo, so a custom property naming another k times expands k^L, cut off at 15 levels. Measured **0.19 us per resolved copy**: 2^15 copies cost about 7 ms per element, and 5,000 such elements decode in 35 s at 396 KB. Three references per level reach 0.8 s for a single element of a 746-byte document at 14 levels. The guard sees none of it.
- **Markers.** Svg.Skia compiles the marker's whole subtree anew at every placement. **2.65 us per (placement x marker render)** on long polylines. Many small markers on many elements cost more than that product says: the fit across all marker shapes is **2.55 us per unit of positions x (renders + 20) + marked elements x renders** (geometric sd 1.37). Markers set by a stylesheet rule, the `marker` shorthand and inheritance from a `<g>` cost the same as the attribute, and Svg.Skia also draws markers on rect, circle and ellipse.
- **#1581's four rows are reproduced** within a factor of two, each at about the issue's size (table below).
- **Real documents sit far below every limit.** Core renders five mermaid dialects; none of their outputs has a `<style>`, a `<use>` or a `var()`. The largest marker count, a 1,093-message sequence diagram, is 1/13 of the 1 s marker count with the +20 placement weight (1/140 with the plain product).
- **The guard's own cost** is small: at most 184 ms on an 8 MiB document (150,092 rects), 65 ms on an 8 MiB stylesheet, 38 ms on an 8 MiB polyline, and 17 ms or less on the largest document of every sweep. That is `main`'s guard; the guard CB-29 ships, with its estimate and its step budget, is measured in section 6 too.

## Context

- CPU: AMD Ryzen 7 9800X3D, 8 cores, 16 logical processors, 4.7 GHz max. RAM: 61.6 GiB. Windows 11 Pro 10.0.26300.
- .NET SDK 10.0.404. The test host targets `Microsoft.NETCore.App` 10.0.0 and rolls forward to the highest installed 10.0 patch, 10.0.15 (from `dotnet --list-runtimes`; not logged from inside the process).
- Resolved packages (the test build's `SlateWindows.Tests.deps.json`): Svg.Skia 5.2.3, Svg.Custom 5.2.3, Svg.Model 5.2.3, Svg.SceneGraph 5.2.3, Svg.Animation 5.2.3, ShimSkiaSharp 5.2.3, ExCSS 4.3.1, SkiaSharp 4.153.1 (SkiaSharp.NativeAssets.Win32 4.153.1), HarfBuzzSharp 14.2.1.301.
- Build: `./apps/slate-windows/generate-bindings.ps1 -Locked` (exit 0), `dotnet build apps/slate-windows/SlateWindows.slnx --configuration Release` (0 errors, 8 warnings), then the test project rebuilt Release after each probe edit.
- CI: `gh variable get WINDOWS_HOME_STATE` and `gh run list --status in_progress` were read before and after every probe run (26 entries in `ci-state.log`). Every entry shows `"running":[]` and no run in progress; the pool mode is `namespace`. No home-pool job ran during any measurement.
- Desktop variation: two reference documents were decoded at the start and end of every sweep. `D_marker M=500 V=1000`: 9 medians from 1,120 to 1,434 ms (1.28x). `A_type R=300 E=1000`: 8 medians from 308 to 376 ms (1.22x). Inside one median of three, single runs varied up to 1.6x (for example 889, 1,240 and 1,411 ms). This matches CB-28's "about a quarter".

## Method

- A decode is `EditorInteractionCoordinator.DecodeImage(bytes, "image/svg+xml", out reason)`, the app's path: a decoder thread with the 256 MB stack, `SvgPreviewGuard.Check`, Svg.Custom's parse (`SvgService.FromSvg` with the app's `SecureStatic` options), Svg.Skia's picture, the raster at 1,120 px, the PNG encode and WPF's PNG decode. Each decode used its own `SvgDecodeSlots` and a per-thread budget of 130,000 ms. A decode past the budget is abandoned by the app's path; the probe then waited for its decoder thread to end and recorded the time to that end. One document did this: A_star_rect R=15000 E=8000 (135.6 s, abandoned at 130 s, its thread ended 5.6 s later). Every other decode returned an image inside its budget. One decode at a time; never two in parallel.
- Decodes under 10 s: median of three runs. Longer: one run. A warm-up (five small documents decoded twice each) ran before every sweep.
- Phases were timed once per document (when the decode was under 40 s) on a thread with the same stack: guard, parse, picture (scene compile and record), raster plus PNG encode.
- `SvgPreviewGuard.Check(source, 8 MiB)` is the median of five runs (three above 2 MB), timed on the test thread.
- Counts come from the probe's own analyzer (`Analyze`, an XDocument walk, not the guard). "All elements" is every element including the root; "styled elements" is what the guard's `TakesStyleRules` admits. "Matched declarations" is computed the way the guard could: each selector filed under its id, first class, type or `*` key (the guard's `SimpleSelectorKey` rule), and each styled element summing the declarations of every rule filed under its keys; a selector that is not simple matches every element.
- Fits minimise relative error ((measured - model) / measured), so a 0.1 s point weighs as much as a 60 s one. "Ratio" is measured over predicted; "gsd" is the geometric standard deviation of that ratio.

## 1. The cost model, from the assemblies

The NuGet cache holds only the compiled assemblies (`.nuget/packages/svg.custom/5.2.3/lib/net10.0/Svg.Custom.dll` in the user's package cache, and its siblings), no sources and no symbol packages. No decompiler is installed and I did not download one. I wrote a small IL lister (`ildump\Program.cs` in this folder: reflection over the cached assemblies, `MethodBody.GetILAsByteArray`, member tokens resolved to names). Every claim below cites the type, method and IL offset I read; the listings are in `il\`.

### Stylesheet application (Svg.Custom 5.2.3, through Svg.Model 5.2.3)

1. `Svg.Model.Services.SvgService.FromSvg(String, Nullable<SvgParameters>, Boolean)` opens a stream and calls `SvgService.Open(Stream, ...)`, which calls `Svg.SvgDocumentCompatibilityLoader.Open(Stream, SvgOptions, SvgDocumentLoadOptions, Boolean)` (IL_0058).
2. `SvgDocumentCompatibilityLoader.Create(XmlReader, String css, Uri, SvgDocumentLoadOptions, Boolean, Boolean, Boolean)` builds the tree, then calls `SvgCssCompatibilityProcessor.Apply(...)` (IL_00B0) when the document has style sources, then `SvgElement.FlushStyles(true)` (IL_00BC).
3. `SvgCssCompatibilityProcessor.ApplyCore` joins the sources (`ExpandImportedStyles`, IL_0014), builds one `StylesheetParser`, and runs three passes over the joined text: `ApplyCustomPropertyRules` (IL_005F), `ApplyRawSvgStaticPropertyRules` (IL_006F) and `ApplyStyleRules` (IL_007F).
4. `ApplyStyleRules`: for each top-level statement (`TryReadNextTopLevelStatement`; `@media` blocks recurse, IL_006C), it parses that one rule with ExCSS (`StylesheetParser.Parse(selector + "{" + declarations + "}")`, IL_0099), builds its declarations (`CreateAppliedDeclarations`), and for **each selector branch** (`EnumerateSelectorBranches`, one per comma-separated selector) runs `QuerySelectorAllIncludingSvgRoot` (IL_0157), `Distinct`, `ToArray`, then applies every declaration to every matched element (`<ApplyStyleRules>b__0` calls `ApplyDeclaration`).
5. `ApplyDeclaration` validates each application (`SvgCssPaintDeclarationValidator.ShouldIgnoreInvalidPaintDeclaration`, `SvgComputedStyleMetadata.ShouldIgnoreInvalidDeclaration`) and stores it (`SvgElement.AddCompatibilityStyle`, a `Dictionary<string, SortedDictionary<int, string>>` per element, IL_000F to IL_002F). `FlushStyles()` then sets each property once from its highest specificity (`Enumerable.Last`, `SvgElementFactory.SetPropertyValue`). So the per-match cost is the validation and the store, once per (matched element x declaration).
6. `ApplyCustomPropertyRules` runs only for rules holding a custom-property declaration (`CreateCustomPropertyDeclarations` non-empty, IL_0063 to IL_0071): it parses the selector (`selector + "{fill:inherit}"`, IL_0077 to IL_0081), runs the same query per branch (IL_00D4), and calls `SvgCssVariableResolver.AddCustomProperty` per element per custom declaration (IL_0116). `ApplyRawSvgStaticPropertyRules` does the same for rules declaring one of `cx, cy, d, height, r, rx, ry, transform-box, transform-origin, width, x, x1, x2, y, y1, y2` (`IsRawSvgStaticPropertyName`). So such a rule costs two or three whole-tree queries per branch.
7. `Svg.Css.ExCssQuery.QuerySelectorAll(SvgElement, ISelector, SvgElementFactory)` builds the selector's filter (`GetFunc(selector, ops, ops.Universal())`, IL_001C) and applies it to `ops.Descendant()` of the root (IL_0024), then `Distinct` (IL_0048). `ExSvgElementOps.Descendant` is `SelectMany(Descendants)`, and the iterator `<Descendants>d__33` walks every child and recurses through `SvgElement.Descendants()`: **every query visits every element of the document**, whatever the selector.
8. A class selector is `ExSvgElementOps.Class` = `AttributeIncludes("class", c)`: per element `TryGetAttribute("class")`, `String.Split`, `Contains` (`<AttributeIncludes>b__1`). An element with no class attribute is cheaper (measured 0.059 against 0.085 us per pair). A descendant combinator (`ComplexSelector`) adds a second walk (measured 0.187 us per pair).
9. A `style` attribute is parsed per element by its own ExCSS parse (`SvgInlineStyleAttributeParser.ApplyStyles`, `"#a{" + style + "}"`); that is linear in the elements and measured cheap (10,000 inline-styled rects: 129 ms).

**Formula:** t_stylesheet = c_q x sum over rules of (selector branches x passes) x all elements + c_m x sum over elements of matched declarations + c_r x rules, with passes = 1 + [rule declares a custom property] + [rule declares a geometry property]. Measured c_q = 0.093 us, c_m = 1.03 us for paint declarations (0.29 us for custom properties), c_r about 4.5 us per rule (8 MiB of 383,126 rules over three elements decoded in 1.83 s).

### Use instances (Svg.SceneGraph 5.2.3, Svg.Custom 5.2.3)

10. `Svg.Skia.SvgSceneCompiler.TryCompileDirectUseNode` compiles each `<use>` inside `WithUseInstanceStyleScope`, which reaches `SvgDocument.BeginUseInstanceStyleScope(SvgElement, SvgUse)`. When the document has style sources (`_compatibilityStyleSources` non-empty, IL_0001 to IL_000E) it snapshots every element of the instance (`EnumerateElements`, `CreateCompatibilityStyleSnapshot`, `SvgCssVariableResolver.CreateSnapshot`), calls `InvalidateComputedStyleCache()` (IL_0090), re-runs the whole stylesheet on the instance (`SvgCssCompatibilityProcessor.ApplyScoped`, IL_00A9, which is `ApplyCore`: every rule re-parsed, every branch re-queried), applies inline styles, flushes, and invalidates the cache again (IL_00D5) when the scope closes.
11. The source accounts for the rules x uses term. It does not show where the uses x all-elements term comes from; I measured it (section 4) and suspect the document-wide computed-style invalidation per use, unverified.

**Formula (measured):** t_use = 0.246 us x use renders x (all elements + 15 x rules), only when the document has a `<style>` (inline `style` attributes do not open the scope: 10,000 uses with style attributes and no `<style>` decode in 1.0 s).

### var() resolution (Svg.Custom 5.2.3)

12. `SvgElementFactory.SetPropertyValue` resolves any value holding `var(` through `SvgCssVariableResolver.TryResolveValue`, which calls `TryResolveValueCore(element, value, resolving, depth)`. That returns false once `depth > 32` (IL_0004 to IL_000A: `ldarg.3; ldc.i4.s 32; ble.s`), and for each `var(` found (`TryFindNextVarFunction`) calls `TryResolveVariableFunction(depth + 1)` (IL_004F), which calls `TryGetCustomPropertyValue(element, name, resolving, depth)`.
13. `TryGetCustomPropertyValue` looks the name up in the element's rules (a `ConditionalWeakTable<SvgElement, Dictionary<string, List<CustomPropertyRule>>>`, `s_customPropertyRules`), falls back to the parent (`get_Parent`, IL_0074, so a lookup walks ancestors), takes the winning rule (`GetWinningRule`) and resolves its value again with `TryResolveValueCore(depth + 1)`. The `HashSet` it adds to and removes from only stops cycles. Nothing is cached, so a property referenced k times is resolved k times at every level, and two depth units are spent per level: the expansion is k^L for L up to 15 (measured: L = 15 resolves, L = 16 fails fast and costs nothing).

**Formula:** t_var = 0.19 us x sum over elements of the resolved copies (k^L per var() use, L at most 15). The result string has the same length, so memory grows with it: k = 4 at L = 15 would be about 1.07e9 copies of the leaf value (gigabytes); I did not run it. `RenderSvgToPng`'s catch filter excludes `OutOfMemoryException` (`EditorInteractions.cs:3840-3843`), and the decoder thread's own catch captures it for the caller to rethrow (`EditorInteractions.cs:3600-3603`, `:3705`); I did not exercise that path.

### Markers (Svg.SceneGraph 5.2.3)

14. `SvgSceneCompiler.TryCompileDirectElementNode` calls `AppendDirectMarkers(node, element, path, ...)` when `HasMarkerReference(element)` (or the context's marker declaration candidate) and the element's path is not empty.
15. `HasMarkerReference` asks `GetEffectiveMarkerReferenceUri` for `marker-start`, `marker-mid` and `marker-end`. That reads the element's own cascaded declaration (`TryGetOwnMarkerReferenceDeclaration`, first the long name, then the `marker` shorthand, IL_000E), then `ComputedStyle.TryGetPropertyValue` (inherited values), then the `SvgMarkerElement` property, then the raw attribute. So an attribute, a `style` declaration, a stylesheet rule, the `marker` shorthand and a value inherited from an ancestor all place markers (measured: each at 0.90 to 1.02 of the attribute form's time).
16. `AppendDirectMarkers` builds the vertex list with `CreateMarkerVertices(path)`, which reads every path command (`MoveTo`, `LineTo`, `QuadTo`, `CubicTo`, `ArcTo`, `Close`, `AddPoly`, `AddRect`, `AddRoundRect`, `AddOval`, `AddCircle`). It compiles one marker at vertex 0 for `marker-start`, one at each vertex 1 to count-2 for `marker-mid` (IL_018C to IL_01E4), and one at the last vertex for `marker-end`. Because rects (`AppendRectMarkerVertices`: a closed four-point poly, 5 vertices) and ovals (`AppendOvalMarkerVertices`: a start and four cubic segments, 5 vertices) have paths, **rect, circle and ellipse take markers too** (measured).
17. `TryCompileDirectMarkerNode(SvgMarker, ..., Single angle, ...)` creates a new `SvgSceneNode` for the placement (IL_01BA) and calls `CompileElementNode` for each child of the marker (IL_02D2). There is no instancing: **the marker's subtree, references included (a `<use>` inside compiles its target, with its own use-instance style scope), is compiled again at every placement**, then drawn.

**Formula:** t_marker = sum over drawn marked elements of [positions x (renders + k0) + k1 x renders] x c, with positions = [start] + max(0, vertices - 2) x [mid] + [end], renders = the marker's subtree with references expanded (as `Measure` counts renders), and "drawn" multiplying by the element's own render count (a marked polyline drawn by U uses costs U times). Measured c = 2.55 us, k0 = 20, k1 = 1 (section 4).

## 2. Generators

All in `Cb29Step0Probe.Generate(kind, params)`. Each is a 400 x 400 SVG; rects are `<rect x y width='3' height='3' .../>` (about 45 to 55 bytes).

| kind | shape | parameters |
|---|---|---|
| A_class | R rules `.cK{fill:#hex}` over E rects, each `class='cK'` (one match per element) | R, E |
| A_type | R rules `rect{fill:#hex}` over E rects (every rule matches every rect) | R, E |
| A_star_g / A_star_rect | R rules `*{fill:#hex}` over E `<g/>` / E `<rect width='3' height='3'/>` | R, E |
| A_unclassed / A_unmatched | R class rules matching nothing, over E rects with / without a class | R, E |
| A_attr | R rules `[data-k='K']{fill}` (attribute selectors; the guard files them under `*`) | R, E |
| A_compound | R rules `rect.cK{fill}` | R, E |
| A_desc | R rules `g .cK{fill}` (descendant combinator; the guard files them under `*`) | R, E |
| A_list | R rules of S selectors `.cK_0,...,.cK_(S-1){fill}` | R, S, E |
| A_decls | R class rules of D declarations each | R, D, E |
| A_use / A_use_nostyle | R class rules, a `<g id='s'>` of G rects in defs, U `<use href='#s'>`, X extra plain rects / the same without the `<style>` | R, G, U, X |
| G_scatter | A plotting-library-shaped scatter, written from my memory of that format (one `*{stroke-linejoin:round;stroke-linecap:butt}` rule, a marker path in defs, one `<use xlink:href>` with a `style` per point); `nostyle=1` drops the rule | N, nostyle |
| C_root / C_root_inline | `svg{--v0..--vR}` and R rules `.cK{fill:var(--vK)}` / each rect reads `var()` in its style attribute | R, E |
| C_inline | each rect `style='--a:#hex;fill:var(--a)'` | E |
| C_per_rule | R rules `.cK{--v:#hex;fill:var(--v)}` | R, E |
| C_rule_all | one rule `rect{--v0..--v(R-1)}` and `rect{fill:var(--v(R-1))}` | R, E |
| C_typevar / C_typedecl | R rules `rect{--v:#hex;fill:var(--v)}` / R rules `rect{--vK:#hex}` | R, E |
| C_chain / C_nest | `--vK:var(--v(K-1))` chain of depth D / rects nested D groups deep | D, E, B |
| C_double / C_kary | `--vL: var(--v(L-1))` repeated 2 (C_double) or K times per level, D levels; E rects read `var(--vD)` | D, K, E |
| D_marker | a marker of M 1x1 rects, `marker-mid` on L polylines of V vertices (CB-28's shape) | M, V, L |
| D_css / D_shorthand / D_inherit / D_path / D_start | the same marker set by a stylesheet rule / by `style='marker:url(#m)'` / inherited from a `<g>` / on a path's `d` / as `marker-start` and `marker-end` only | M, V |
| D_use | the marker's content is one `<use>` of a defs `<g>` of M rects | M, V |
| D_paths | N two-vertex paths each with `marker-end`, a marker of M rects; `nomark=1`, `group=K`, X extra rects | M, N |
| D_rects_attr / D_rects_inherit / D_circles_inherit | N rects with marker attributes / N rects or circles inside a `<g>` carrying all three markers | M, N |
| D_use_poly | one marked polyline in defs drawn by U uses | M, V, U |
| Big_rects / Big_points / Big_style | about 8.3 MB of rects / one polyline / one stylesheet | N |

### #1581's four rows

| #1581 row | generated document | size | decode | within 2x? |
|---|---|---|---|---|
| Stylesheet of many simple rules over many elements, about 150 KB, 40 s | A_star_g R=3000 E=13000 (`*` rules over `<g/>`) | 97 KB | 40.5 s | yes (size 0.65x, time 1.0x) |
| | A_type R=4200 E=1700 (type rules over rects) | 148 KB | 6.5 s | no (rects are larger per element) |
| | A_class R=300 E=3000 (class rules over rects) | 172 KB | 0.21 s | no |
| The same, larger, about 450 KB, over 2.5 min | A_star_rect R=15000 E=8000 | 449 KB | 135.6 s | yes (0.9x of 150 s) |
| | A_type R=12500 E=5000 | 441 KB | 57.0 s | no (0.38x) |
| Many CSS custom properties applied to many elements, about 400 KB, 29 s | C_double D=15 E=5000 (two references per level, 15 levels) | 396 KB | 34.8 s | yes (1.2x) |
| | C_rule_all R=5000 E=10000 (5,000 custom properties declared on every rect) | 513 KB | 15.7 s | borderline (time 0.54x, size 1.28x) |
| | C_root R=3000 E=5000 / C_per_rule R=3000 E=5000 | 403 KB / 382 KB | 1.3 s / 2.6 s | no |
| A marker with a large subtree on a polyline with many vertices, about 220 KB, 43 s | D_marker M=2000 V=10000 | 168 KB | 50.0 s | yes (1.16x) |
| | D_marker M=3000 V=8000 | 208 KB | 61.8 s | yes (1.44x) |

The four rows are reproduced, but the size alone says little: at 150 KB a stylesheet document decodes anywhere from 0.2 s (class rules) to 40 s (universal rules over small elements). The saved documents are in `svg\`.

## 3. The sweeps

Full tables, one row per document with every count, the guard's time, the decode runs and the phases: `tables.md` (generated from the CSVs by `tables.py`). Raw data: `sweep-A.csv` (stylesheets and uses), `sweep-C.csv` (custom properties), `sweep-D.csv` (markers), `sweep-X.csv` (uses with one rule, the scatter shape, marker placement shapes, #1581 row B), `sweep-big.csv` (8 MiB shapes), `explore*.csv` (single-run exploration, not used in the fits). Logs beside each.

Where the time goes (phases): stylesheet and var() costs are in the parse phase (Svg.Custom); use and marker costs are in the picture phase (the scene compile); the raster plus PNG is under 20 ms except for markers, where it is about 9% of the decode (2.6 s of 31 s for D_marker M=2000 V=6000).

### Stylesheet, class rules (A_class): selectors x all elements

| R x E | selectors x all elements | decode ms |
|---|---|---|
| 300 x 3,000 | 0.90 M | 206 |
| 1,000 x 3,000 | 3.0 M | 293 |
| 2,000 x 5,000 | 10.0 M | 857 |
| 5,000 x 6,000 | 30.0 M | 2,622 |
| 20,000 x 5,000 | 100.0 M | 8,475 |
| 10,000 x 10,000 | 100.0 M | 9,223 |
| 5,000 x 20,000 | 100.0 M | 10,087 |
| 15,000 x 20,000 | 300.0 M | 28,740 |
| 25,000 x 25,000 | 625.1 M | 61,842 |

Linear in the product, and the same for every split of 100 M (8.5 to 10.1 s).

### Stylesheet, every rule matches every element (A_type, A_star_*): matched declarations

| R x E | matched declarations | decode ms |
|---|---|---|
| 100 x 1,000 | 0.10 M | 141 |
| 300 x 1,000 | 0.30 M | 308 to 376 |
| 1,000 x 1,000 | 1.0 M | 1,008 |
| 1,000 x 3,000 | 3.0 M | 2,499 |
| 3,000 x 3,000 | 9.0 M | 7,954 |
| 300 x 30,000 | 9.0 M | 10,863 |
| 2,000 x 10,000 | 20.0 M | 18,332 |
| 4,000 x 12,000 | 48.0 M | 49,278 |
| 12,500 x 5,000 | 62.5 M | 57,041 |
| `*` 3,000 x 13,000 `<g/>` | 39.0 M | 40,513 |
| `*` 15,000 x 8,000 rects | 120.0 M | 135,593 |

### Stylesheet selector variants (rate per selector x element, from the fits)

| kind | us per (selector x element) | points |
|---|---|---|
| A_class (one match per element) | 0.095 | 9 |
| A_unclassed (class attribute, no match) | 0.085 | 2 |
| A_unmatched (no class attribute) | 0.059 | 2 |
| A_attr (attribute selectors) | 0.075 | 3 |
| A_compound (`rect.cK`) | 0.099 | 2 |
| A_desc (`g .cK`) | 0.187 | 2 |
| A_list (4 selectors per rule) | 0.095 per selector, 0.38 per rule | 2 |
| C_per_rule (custom property in each rule: two passes) | 0.090 per (pass x element), 0.18 per rule | 3 |
| C_root (custom properties on the root, R reading rules) | 0.100 | 3 |

### Use instances under a stylesheet (A_use, G_scatter)

| document | rules | use renders | all elements | decode ms |
|---|---|---|---|---|
| A_use R=300 G=4 U=300 | 300 | 300 | 308 | 392 |
| A_use R=1000 G=4 U=1000 | 1,000 | 1,000 | 1,008 | 3,941 |
| A_use R=1000 G=40 U=1000 | 1,000 | 1,000 | 1,044 | 7,134 |
| A_use R=2000 G=4 U=2000 | 2,000 | 2,000 | 2,008 | 13,964 |
| A_use R=3000 G=4 U=3000 | 3,000 | 3,000 | 3,008 | 33,397 |
| A_use R=10000 G=4 U=100 | 10,000 | 100 | 108 | 3,381 |
| A_use R=100 G=4 U=10000 | 100 | 10,000 | 10,008 | 32,478 |
| A_use R=1 G=4 U=1000 | 1 | 1,000 | 1,008 | 429 |
| A_use R=1 G=4 U=3000 | 1 | 3,000 | 3,008 | 2,759 |
| A_use R=1 G=40 U=3000 | 1 | 3,000 | 3,044 | 5,159 |
| A_use R=1 G=4 U=3000 X=20000 | 1 | 3,000 | 23,008 | 16,568 |
| A_use R=1 G=4 U=10000 | 1 | 10,000 | 10,008 | 30,305 |
| A_use_nostyle G=4 U=3000 / U=10000 | 0 | 3,000 / 10,000 | 3,007 / 10,007 | 180 / 2,020 |
| G_scatter N=1000 / 3000 / 10000 | 1 | N | N + 5 | 265 / 1,752 / 17,438 |
| G_scatter N=10000 nostyle=1 | 0 | 10,000 | 10,004 | 998 |

The X=20000 row shows the per-use cost follows all elements of the document, not the uses alone.

### var() expansion (C_double, C_kary)

| document | resolved copies | decode ms |
|---|---|---|
| C_double D=15 E=1 / 10 / 100 / 1000 / 5000 | 2^15 x E | 14 / 106 / 868 / 7,432 / 34,785 |
| C_double D=14 E=1000 | 2^14 x 1000 | 3,187 |
| C_double D=16 E=1000 | (cut off) | 31 |
| C_kary K=3 D=12 / D=14 / D=16, E=1 | 3^12 / 3^14 / cut off | 102 / 823 / 6 |
| C_kary K=4 D=10 / D=11, E=1 | 4^10 / 4^11 | 190 / 600 |

The non-exponential custom-property shapes stay at stylesheet rates: C_chain D=100 E=5000 185 ms, C_nest D=100 E=5000 158 ms, C_inline E=10000 129 ms, C_root_inline R=3000 E=5000 121 ms.

### Markers (D_*)

| document | positions (drawn) | positions x renders | decode ms |
|---|---|---|---|
| D_marker M=100 V=1000 | 998 | 0.10 M | 239 |
| D_marker M=500 V=1000 (reference, 9 medians) | 998 | 0.50 M | 1,120 to 1,434 |
| D_marker M=1000 V=2000 | 1,998 | 2.0 M | 4,708 |
| D_marker M=5000 V=500 | 498 | 2.49 M | 5,716 |
| D_marker M=200 V=12500 | 12,498 | 2.51 M | 6,434 |
| D_marker M=1500 V=4000 | 3,998 | 6.0 M | 14,985 |
| D_marker M=2000 V=6000 | 5,998 | 12.0 M | 30,960 |
| D_marker M=2000 V=10000 | 9,998 | 20.0 M | 50,028 |
| D_marker M=3000 V=8000 | 7,998 | 24.0 M | 61,804 |
| D_css / D_shorthand / D_inherit / D_path, M=500 V=1000 | 998 | 0.50 M | 1,377 / 1,219 / 1,231 / 1,276 |
| D_start M=500 V=1000 (start and end only) | 2 | 1,002 | 10 |
| D_use M=500 V=1000 / M=1500 V=2000 | 998 / 1,998 | 0.50 M / 3.0 M | 1,674 / 11,797 |
| D_use_poly M=100 V=1000 U=10 / U=30 | 9,980 / 29,940 | 1.0 M / 3.0 M | 2,983 / 8,772 |
| D_marker M=10 V=10 L=1000 / M=10 V=8002 L=1 | 8,000 / 8,000 | 88 K / 88 K | 553 / 411 |
| D_marker M=100 V=10 L=1000 / M=100 V=8002 L=1 | 8,000 / 8,000 | 808 K / 808 K | 5,219 / 2,541 |
| D_paths M=1 N=1000 / 3000 / 5000 / 10000 / 20000 | N | 2N | 52 / 172 / 362 / 864 / 2,672 |
| D_paths M=1 N=20000 nomark=1 (no marker) | 0 | 0 | 317 |
| D_paths M=10 N=1000 / 3000 / 10000 | N | 11N | 91 / 427 / 3,785 |
| D_paths M=40 N=1000 / 3000; M=100 N=1000 | N | 41N; 101N | 228 / 1,409; 603 |
| D_rects_inherit / D_circles_inherit M=100 N=1000 | 5,000 | 0.505 M | 2,594 / 2,577 |
| D_rects_inherit / D_circles_inherit M=10 N=3000 | 15,000 | 0.165 M | 1,726 / 1,757 |
| D_rects_attr M=100 N=1000 | 5,000 | 0.505 M | refused by the guard today (RenderedElements: 1,000 x 3 edges x 101) |

D_paths grows faster than its count: per marked path the cost is 52 us at N = 1,000 and 134 us at N = 20,000 with the same one-rect marker (the explore run measured 1.7 s at N = 20,000, this run 2.7 s). The source reading does not explain it; grouping the paths 100 to a `<g>` did not remove it (explore4: 2.4 s at N = 20,000). Spreading the same 8,000 placements over 1,000 elements costs 1.3 to 2.1x what one element costs.

## 4. Candidate counts and fits

Full tables: `fits.md` (from `analyze.py`); threshold arithmetic: `thresholds.py`.

### Stylesheet

Per kind, every single count fits its own kind well (gsd 1.03 to 1.34), because within a kind the counts are proportional. Pooled across kinds they do not:

| count (pooled: 52 stylesheet documents of at least 250 ms) | us per unit | ratio range | gsd |
|---|---|---|---|
| rules x styled elements | 0.097 | 0.60 to 8,120 | 6.99 |
| selectors x styled elements | 0.096 | 0.61 to 8,236 | 7.15 |
| (selectors + custom-property declarations) x styled elements | 0.083 | 0.52 to 15.1 | 3.22 |
| rules x all elements | 0.097 | 0.60 to 8,120 | 6.99 |
| selectors x all elements | 0.096 | 0.61 to 8,236 | 7.15 |
| query passes x all elements | 0.094 | 0.62 to 5,604 | 6.75 |

(The huge upper ratios are C_rule_all, one rule declaring 500 to 5,000 custom properties on every rect, whose cost is all in the matched declarations.)

Two terms, fitted on the 48 documents whose selectors are simple (A_attr and A_desc scored separately):

- **t = 0.0913 us x (selectors x all elements) + 0.39 us x matched declarations**: ratio 0.51 to 2.61, gsd 1.59. The residual is the declaration's kind: paint declarations cost 1.03 us per match (A_type, under-predicted about 2x), custom-property declarations 0.24 to 0.30 us (C_typevar, C_rule_all, over-predicted about 2x).
- **One weighted count: selectors x all elements + 5 x matched declarations at 0.0853 us per unit**: ratio 0.47 to 2.45, gsd 1.55. With weight 11 or 12 (the paint rate over the query rate) the count is conservative for every shape: 0.044 to 0.048 us per unit at the fit, ratio 0.40 to 4.2; at a fixed 0.093 us per unit it never under-predicts a paint document and over-predicts custom-property documents by up to 4x.
- The guard's wide filing errs in the safe direction and by a lot: under the two-term model, A_attr documents decode at 0.15 of the prediction and A_desc at 0.38-0.40, because every rule is counted as matching every element. Filing a complex selector under its subject (the rightmost compound) instead of `*` would make A_desc exact; an attribute-only selector stays wide.

Natural counts per shape (these are what the thresholds below use):

| shape | best count | us per unit | ratio range | gsd | points |
|---|---|---|---|---|---|
| class-like rules (A_class, A_unclassed, A_compound, A_list, C_root) | selectors x all elements | 0.0935 | 0.89 to 1.55 | 1.13 | 17 |
| every-element rules (A_type, A_star_g, A_star_rect) | matched declarations | 1.032 | 0.81 to 1.22 | 1.13 | 18 |
| custom properties on every element (C_rule_all, C_typedecl, C_typevar) | matched declarations | 0.288 | 0.77 to 3.37 | 1.76 | 7 |
| custom property in each rule (C_per_rule) | query passes x all elements | 0.090 | 0.95 to 1.05 | 1.04 | 3 |

### Uses under a stylesheet

| count (15 documents) | us per unit | ratio range | gsd |
|---|---|---|---|
| rules x use renders | 3.99 | 0.85 to 1,383 | 16.4 |
| use renders x all elements | 0.258 | 0.68 to 1,214 | 7.19 |
| use renders x (all elements + rules) | 0.267 | 0.65 to 13.1 | 2.91 |
| **use renders x (all elements + 15 x rules)** | **0.2465** | 0.71 to 2.28 | **1.36** |

The two-term fit gives 3.72 us x rules x use renders + 0.246 us x use renders x all elements (gsd 1.36). The worst residuals are the G=40 symbols (2.3x): the instance's own subtree adds a term this count leaves out. From G=4 against G=40 at U=3000, R=1 (2.76 s against 5.16 s) and at U=1000, R=1000 (3.94 s against 7.13 s), each extra element in an instance costs about 22 us + 0.067 us x rules per use render, which fits the per-element snapshots and the scoped queries of `BeginUseInstanceStyleScope`. A guard that wants it adds use renders x instance elements x (90 + 0.27 x rules) to the use count (in its 0.2465 us units).

### var() expansion

| count (9 documents of at least 50 ms) | us per unit | ratio range | gsd |
|---|---|---|---|
| sum over var() uses of k^L (L at most 15) | 0.192 | 0.74 to 1.68 | 1.25 |

### Markers

| count (45 marker documents) | us per unit | ratio range | gsd |
|---|---|---|---|
| positions x renders (drawn; rects, circles, ellipses counted) | 2.90 | 0.77 to 23.0 | 2.72 |
| positions x renders as the guard sees today (attribute only) | 2.87 | 0.78 to 903,719 | 105 |
| vertices x subtree (plain, no references, no drawn multiplicity) | 2.88 | 0.78 to 1,024 | 4.49 |
| positions alone | 89.7 | 0.57 to 128 | 4.22 |
| positions x (renders + 10) + 1 x marked elements x renders | 2.80 | 0.77 to 4.23 | 1.50 |
| **positions x (renders + 20) + 1 x marked elements x renders** | **2.548** | 0.65 to 3.54 | **1.37** |
| positions x (renders + 30) + 2 x marked elements x renders | 2.14 | 0.59 to 2.81 | 1.34 |
| long polylines only (D_marker, D_css, D_shorthand, D_inherit, D_path, D_use_poly): positions x renders | 2.657 | 0.84 to 2.43 | 1.31 |

The plain product (the issue's "vertices x subtree") predicts one long polyline well and many small markers badly (D_paths M=1 N=20000: 23x). The +20 per placement and the per-element term bring the worst case to 3.5x (D_paths at 20,000 paths, the superlinear shape).

### Thresholds: the count at which a decode takes about 0.5, 1, 2 and 5 s here

| kind and count | us per unit | 0.5 s | 1 s | 2 s | 5 s |
|---|---|---|---|---|---|
| class-like stylesheet: selectors x all elements | 0.0935 | 5.3 M | 10.7 M | 21.4 M | 53.5 M |
| every-element stylesheet: matched (paint) declarations | 1.032 | 0.48 M | 0.97 M | 1.94 M | 4.84 M |
| pooled stylesheet: selectors x all elements + 5 x matched declarations | 0.0853 | 5.9 M | 11.7 M | 23.4 M | 58.6 M |
| uses under a stylesheet: use renders x (all elements + 15 x rules) | 0.2465 | 2.0 M | 4.1 M | 8.1 M | 20.3 M |
| var() expansion: resolved copies | 0.192 | 2.6 M | 5.2 M | 10.4 M | 26 M |
| markers: positions x (renders + 20) + marked elements x renders | 2.548 | 196 K | 392 K | 785 K | 1.96 M |
| markers, plain: positions x renders | 2.90 | 172 K | 345 K | 689 K | 1.72 M |

These are this desktop's numbers (a fast 2025 desktop CPU); the CI runner was not measured. The base cost of a large plain document is separate: 8 MiB of 150,092 rects decodes in 2.4 s with no stylesheet, use or marker, and 2.9 MB of 50,000 rects in 0.65 s.

### Whether the guard can compute each count in its one XmlReader pass plus Measure

- **Selector branches x all elements**: yes. `ReadStylesheet` already scans the joined stylesheet (`ScanCss` with `stylesheet: true`) and records each rule's prelude at its `{`; counting the top-level commas of each prelude gives the branches. All elements is `names.Count`. It must count rules inside `@media` (Svg.Custom recurses into them) and nothing else is hidden (`@import` and xml-stylesheet are refused). Query passes need each rule's declaration names (custom property, geometry property), also in the same scan.
- **Matched declarations**: yes, with the guard's own filing keys. `Measure` already visits every styled element and looks up its `*`, type, `#id` and `.class` keys (`SvgPreviewGuard.cs:568-599`); summing per-key declaration counts there gives the count. It errs wide for any selector that is not simple (filed under `*`); filing a complex selector under its rightmost compound would keep most of them exact. It cannot know the declaration's kind (paint against custom property) without reading names, which the scan can do.
- **Use renders x (all elements + rules)**: yes. `Measure` computes `paths[node]`, the render count of every element; the sum over `use` elements is the use renders. It applies only when the document has a non-empty `<style>`.
- **var() expansion**: not today; it needs a small new graph. Custom-property declarations (name, the names their values reference, with multiplicity) come from the stylesheet scan and from `style` attributes; the expansion of a name is the product of reference counts along its chains, capped at 15 levels, and is a longest-path style count like `Measure`'s. Erring wide (take the largest expansion of any declaration of a name) is cheap.
- **Markers, positions x (renders + 20) + marked elements x renders**: mostly. Vertices are readable in the pass (`points` numbers / 2, plus 1 for polygon; `d` per command: M, L, T 2 numbers, H, V 1, C 6, S, Q 4, A 7, Z adds 1; line 2; rect, circle and ellipse 5). The marker's renders are a bottom-up subtree count over the reference graph `Measure` builds (after its cycle check), and the element's own multiplicity is `paths[element]`. What it misses today, and must add: the inherited value (a `<g marker-mid>` reaches every shape below it; the guard has one edge from the `<g>`), the `marker` shorthand in a stylesheet or `style` (today only its `url()` is collected, as an edge), a stylesheet rule's property name (today only its `url()` target is filed; without the name, count the rule as `marker-mid`, the widest), shapes that are not path, line, polyline or polygon (Svg.Skia marks rect, circle and ellipse), and a marked element drawn through `<use>`. The superlinear growth across many marked elements (D_paths) is not captured by any count; the +20 weight covers it to 3.5x at 20,000 paths.

## 5. Real documents

Core renders five mermaid dialects: `classify_mermaid_kind` accepts flowchart or graph, sequenceDiagram, classDiagram, stateDiagram and erDiagram (`crates/slate-core/src/diagram.rs:489-506`) and gives every other first line `UnsupportedDialect` (`:598-605`), before mermaid-rs-renderer runs. The pinned renderer (0.3.1) knows 23 kinds (`ir.rs:12-36`); the other 18 (gantt, pie, mindmap, journey, timeline, gitGraph, requirement, quadrant, sankey, xychart, C4, block, packet, kanban, architecture, radar, treemap, ZenUML) never reach it through core (measured: each returned "unrecognized Mermaid diagram type").

Sources were generated per dialect at a typical size (n = 20) and grown by 1.6x until the render failed or the source passed core's 64 KiB cap, then bisected four times. Renders went through `VaultSession.GetDiagramBlocks` (the Release DLL, core's 5 s budget and 2 MiB output cap). `mermaid-renders.csv` has every attempt; the SVGs are in `mermaid\`.

| document | source | render | SVG | elements | `<style>` rules | uses | marked elements | positions x renders | positions x (renders + 20) + marked x renders | decode ms | guard ms |
|---|---|---|---|---|---|---|---|---|---|---|---|
| flowchart n=20 | 911 B | 670 ms | 20 KB | 142 | 0 | 0 | 0 | 0 | 0 | 10 | 0.2 |
| flowchart n=643 (largest; n=663 timed out) | 32.6 KB | 4,944 ms | 628 KB | 4,252 | 0 | 0 | 0 | 0 | 0 | 212 | 2.6 |
| flowchart with classDef and style, n=20 | 1.3 KB | 134 ms | 18 KB | 130 | 0 | 0 | 0 | 0 | 0 | 7 | 0.1 |
| flowchart with classDef and style, n=904 (largest under the 64 KiB source cap) | 63.9 KB | 4,096 ms | 775 KB | 5,434 | 0 | 0 | 0 | 0 | 0 | 293 | 5.7 |
| sequence n=20 | 589 B | 5 ms | 25 KB | 163 | 0 | 0 | 22 | 44 | 528 | 51 | 0.1 |
| sequence n=1093 (largest; n=1124 timed out) | 28.7 KB | 4,565 ms | 1,082 KB | 6,298 | 0 | 0 | 1,249 | 2,498 | 29,976 | 434 | 6.5 |
| class n=20 | 1.8 KB | 12 ms | 35 KB | 266 | 0 | 0 | 19 | 38 | 456 | 70 | 0.9 |
| class n=476 (largest; n=488 timed out) | 45.6 KB | 4,124 ms | 823 KB | 6,042 | 0 | 0 | 475 | 950 | 11,400 | 731 | 19.0 |
| state n=20 | 602 B | 16 ms | 32 KB | 202 | 0 | 0 | 27 | 54 | 648 | 16 | 0.2 |
| state n=501 (largest; n=526 timed out) | 16.4 KB | 4,705 ms | 746 KB | 4,650 | 0 | 0 | 628 | 1,256 | 15,072 | 322 | 3.8 |
| ER n=20 | 1.6 KB | 19 ms | 93 KB | 711 | 0 | 0 | 0 | 0 | 0 | 24 | 0.8 |
| ER n=451 (largest; n=463 exceeded the 2 MiB output cap) | 38.4 KB | 1,140 ms | 2,081 KB | 15,796 | 0 | 0 | 0 | 0 | 0 | 755 | 53.6 |
| `demo-vault/attachments/pie.svg` (the repository's only SVG) | | | 1.0 KB | 6 | 0 | 0 | 0 | 0 | 0 | 2 | 0.0 |
| the 13 shapes `AcceptsTheShapesRealDocumentsUse` accepts | | | 0.15 to 40 KB | 2 to 1,005 | 0 to 2 | 0 or 300 (no `<style>`) | 0 or 1 | 0 or 2 | 0 or 24 | 0.4 to 16 | up to 1.3 |

mermaid-rs-renderer 0.3.1 writes no `<style>` for a rendered diagram: the only one in `render.rs` is in the error diagram (`render.rs:2224`, three rules). Flowchart and ER arrowheads are polygons; the defined markers are unused. Class, state and sequence edges carry `marker-start` or `marker-end` with a one-path marker (renders 2). `classDef` and `style` statements become attributes, not rules.

Ratios to the 1 s count:

| product | largest real count | 1 s count | ratio |
|---|---|---|---|
| stylesheet, selectors x all elements + 5 x matched declarations | 28 (guard test shape: 2 rules x 9 elements + 5 x 2); every mermaid output 0 | 11.7 M | 2.4e-6 |
| uses under a stylesheet | 0 (no real document has both) | 4.1 M | 0 |
| var() expansion | 0 | 5.2 M | 0 |
| markers, positions x (renders + 20) + marked x renders | 29,976 (sequence n=1093) | 392 K | 0.076 (1/13) |
| markers, plain positions x renders | 2,498 (sequence n=1093) | 345 K | 0.0072 (1/140) |

Real-world classes missing (no SVG downloaded; these are guesses, marked as such):

- **A plotting library's scatter or line export** (my guess at the matplotlib shape, generated as G_scatter): one global `*` rule plus a `<use>` per data point. By the measured model, 3,000 points is about 1.8 s and 10,000 about 17 s today. This is the one class I expect to be at risk from a use term limit and also already slow; it is worth one real export, with the owner's permission, before the limits are set.
- **Illustrator** (guess): 20 to 200 `.stN` class rules over 100 to 5,000 elements: at most about 1 M selector x element pairs (0.1 s) and one match per element.
- **Inkscape** (guess): inline styles, no `<style>`; arrowhead markers on a few hundred paths with few vertices: positions x renders in the hundreds to low thousands.
- **mermaid.js exports** (guess): with HTML labels they hold `foreignObject` and the guard refuses them today (Markup); with plain labels, about 100 to 150 rules of `#id .class tag` descendant selectors over 500 to 2,000 elements: 0.1 to 0.3 M selector x element pairs, but the guard's wide filing would count 0.3 to 1 M matched declarations (all rules on all elements), which at weight 5 is 1.5 to 5 M, under the 1 s count (11.7 M) but not by 10x. Filing complex selectors by their subject removes that.
- **draw.io** (guess): `foreignObject` labels, refused today.

## 6. The guard's own cost

`SvgPreviewGuard.Check` (median of five runs, three above 2 MB):

| document | bytes | Check ms |
|---|---|---|
| Big_rects N=150092 (8 MiB of rects) | 8,278,686 | 183.7 (179.0 in a second run) |
| Big_style N=383126 (8 MiB stylesheet) | 8,317,852 | 64.9 (74.9) |
| Big_points N=1119023 (8 MiB polyline) | 8,336,588 | 37.7 (28.6) |
| mermaid ER n=451 (largest real) | 2,080,917 | 53.6 |
| A_class R=25000 E=25000 (largest stylesheet sweep) | 1,962,531 | 13.5 |
| A_use R=1 U=3000 X=20000 | 962,950 | 16.1 |
| C_root R=10000 E=10000 | 980,116 | 17.2 |
| D_paths M=1 N=10000 X=20000 | 1,463,504 | 16.0 |
| G_scatter N=10000 | 795,025 | 5.8 |
| D_marker M=5000 V=500 (largest marker sweep) | 265,454 | 1.9 |

Every other sweep document: under 10 ms. The probe's naive guard-shaped counter (`ReaderCounts`: one XmlReader pass counting elements, `points` and `d` vertices, rules, selectors and `--` declarations) took 14 ms on 8 MiB of rects, 22 ms on the 8 MiB polyline and 99 ms on the 8 MiB stylesheet (it re-splits the stylesheet; folded into `ScanCss` the count is a few comparisons per character already visited). Today `Check` runs inside the decode thread (`RenderSvgToPng`, `EditorInteractions.cs:3783`), so moving it before the slot is taken moves at most 0.2 s at the 8 MiB cap, and under 20 ms for any document below 2 MB, onto the caller's worker.

### The guard CB-29 ships

The table above measured `main`'s guard, before CB-29 added its estimate. The red team's review of CB-29's diff (B1) found that guard's own work super-linear in a document's shape, on the caller's thread: one rule of 320,000 branches and 32,000 `var()` declarations (1.1 MB) took it 23.3 s, because each declaration summed the rule's branches again; 16,000 references to an id 16,000 elements share (480 KB) took 3.1 s, because each reference had an edge to each element; and, found on the way, one block of 20,000 classes whose 1,000 declarations name `url()` targets (163 KB) took 6.0 s, because each target was filed under each class. CB-29 now sums a rule's elements once for all its `var()` values, reaches the elements an id names through one node per id, and reaches a block's targets through one node per block. Those three shapes take 51, 18 and 8 ms in the table below (`TheGuardsOwnTimeStaysBounded` holds each under a second).

Every check also spends from a step budget, `SvgPreviewGuard.MaxGuardSteps`: a step is one element, attribute, edge, rule, branch, declaration, key visit or `var()` lookup, across the read, `Measure` and the estimate. Past the budget the guard stops and refuses the document for its cost, and the caller logs `term=guard`. **The budget is 10,000,000 steps.** The most steps any document of this record or follow-up 2 takes, of the core outputs and the repository's pie, is 2,251,392 (Big_rects, 8 MiB of rects); the largest real document takes 239,647 (mermaid ER n=451). So the budget is 4.4 times the most. Of the 8 MiB shapes below, one passes it: one `var()` definition of a million references (14.9 M steps without the budget), refused for its cost in 90 ms. The step that costs most was measured at about 40 ns (the `var()` table, from the guard's time at budgets of 4, 8 and 16 million), so the budget adds at most about 0.4 s to reading an 8 MiB document.

`SvgPreviewGuard.Check` on CB-29's head, median of three (of five for the first three rows), on this desktop:

| document | bytes | verdict | steps | Check ms |
|---|---|---|---|---|
| T_varrule B=320000 V=32000 | 1,141,080 | None | 1,088,059 | 50.7 |
| T_dupid N=16000 | 480,136 | RenderedElements | 288,022 | 18.3 |
| T_prelude K=20000 U=1000 | 162,944 | None | 202,053 | 7.5 |
| Big_rects N=150092 | 8,278,686 | None | 2,251,392 | 88.8 |
| Big_style N=383126 | 8,317,852 | None | 1,915,667 | 228.8 |
| Big_points N=1119023 | 8,336,588 | None | 25 | 34.9 |
| A_class R=105720 E=105720 | 8,386,552 | DecodeCost | 2,220,143 | 153.8 |
| A_type R=136514 E=136514 | 8,386,582 | DecodeCost | 2,593,790 | 107.7 |
| A_attr R=95093 E=95093 | 8,386,580 | DecodeCost | 2,567,544 | 161.1 |
| A_desc R=103194 E=103194 | 8,386,538 | DecodeCost | 2,373,757 | 182.1 |
| A_use G=4 X=0 R=2654 U=265434 | 8,386,572 | RenderedElements | 800,003 | 89.9 |
| G_scatter N=105551 | 8,386,549 | RenderedElements | 1,372,204 | 106.2 |
| C_root R=82320 E=82320 | 8,386,589 | DecodeCost | 3,457,468 | 257.1 |
| C_double D=8 E=106924 | 8,386,600 | DecodeCost | 1,925,141 | 104.7 |
| C_kary K=4 D=15 E=105576 | 8,386,576 | DecodeCost | 1,901,448 | 94.6 |
| C_fb F=1 D=16 E=105579 | 8,386,550 | DecodeCost | 1,795,840 | 101.7 |
| D_marker M=66192 V=661927 | 8,386,589 | DecodeCost | 992,942 | 34.1 |
| D_paths M=4 X=0 N=141104 | 8,386,578 | RenderedElements | 1,693,318 | 141.9 |
| D_varinline M=66192 V=661927 | 8,386,608 | DecodeCost | 992,962 | 36.2 |
| N_desc K=4 D=200 R=100679 | 2,627,549 | DecodeCost | 915,351 | 53.1 |
| N_sib K=2 R=1919 N=191994 | 8,386,608 | DecodeCost | 2,891,453 | 72.6 |
| N_nth Q=1 R=1916 N=191680 | 8,386,527 | DecodeCost | 2,884,804 | 75.3 |
| N_unstyled T=1 Q=1 D=101040 N=1010408 | 8,386,561 | RenderedElements | 200,006 | 14.2 |
| A_keyframes A=10226 E=102269 | 8,386,320 | DecodeCost | 1,605,640 | 108.0 |
| T_varrule B=2296622 V=229662 | 8,386,578 | None | 7,808,573 | 403.7 |
| T_dupid N=279548 | 8,386,576 | RenderedElements | 400,005 | 69.2 |
| T5 8 MiB of 'g g g g g g rect{fill:red}' rules | 8,388,079 | DecodeCost | 3,554,420 | 185.8 |
| T5 8 MiB of rects, *{fill:red} | 8,386,685 | RenderedElements | 600,000 | 48.6 |
| T5 8 MiB of rects with style var() | 8,386,717 | RenderedElements | 400,002 | 85.4 |
| T5 8 MiB of attribute rules over 2,000 rects | 8,367,528 | DecodeCost | 1,590,559 | 270.0 |
| T5 rules of 1, 2, 3, ... descendant steps to 8 MiB | 8,379,567 | DecodeCost | 4,931,415 | 111.3 |
| T5 rules of 1, 2, 3, ... sibling steps, 600 families | 8,128,577 | DecodeCost | 1,448,024 | 66.7 |
| T6 8 MiB of var() definitions in one rule | 8,386,671 | None | 5,677,352 | 250.2 |
| T6 8 MiB of class rules defining and using var() | 8,386,679 | None | 9,140,903 | 428.7 |
| T6 8 MiB of marker rules over 20,000 groups | 8,466,645 | Malformed | 28,855 | 13.5 |
| T2a 4 MiB of url(#x) class rules, elements sharing id x | 6,382,320 | DecodeCost | 5,525,151 | 466.6 |
| Prelude of 300,000 classes naming url() targets | 8,386,571 | None | 3,414,165 | 345.8 |
| 8 MiB definition of one million var() references | 8,386,537 | DecodeCost, budget spent | 10,000,000 | 90.3 |
| 199,000 groups using var() in a style attribute | 5,174,096 | None | 2,786,043 | 111.4 |

The rows after the first three are the calibration's three 8 MiB documents, each generator kind grown to the 8 MiB cap (the probe's search for N_desc's size stopped at 2.6 MB), the red team's shapes (T_varrule and T_dupid grown to the cap, T5 and T6 rebuilt from its harness, and its T2a grown to 6.4 MB), and three of this run's own (one prelude of 300,000 classes naming url() targets, one var() definition of a million references, and 199,000 groups using var() in a style attribute); the slowest check is 467 ms (the red team's url() class rules over elements sharing an id, 6.4 MB), and 525 ms in an earlier run of the same table. The red team's 8 MiB of marker rules is 8,466,645 bytes, past the reader's 8 MiB cap, and is refused as malformed before any count.

## 7. What the source reading changes for the guard design

1. **A stylesheet has two costs, and the issue's product is the cheaper one.** Selector branches x all elements (0.093 us) bounds the query; matched declarations (1.03 us for paint) bounds the application, and dominates whenever rules match many elements. Count selector branches, not rules, and all elements, not styled elements (the query walks every descendant). Count a custom-property rule or a geometry-property rule as two or three passes.
2. **Uses under any stylesheet** cost use renders x (all elements + about 15 x rules) x 0.25 us, quadratic in a document of uses. Neither issue product sees it, a one-rule stylesheet triggers it, and `Measure` already has the use renders. This is the cost most likely to meet a real document (the plotting-library guess).
3. **var() expansion is exponential and nearly free in bytes** (a 746-byte document takes 0.8 s; k = 4 at 15 levels would take minutes and gigabytes). It needs its own count, or a refusal of any custom property that references another custom property more than once.
4. **Markers are wider than the attribute**: inheritance, the `marker` shorthand, stylesheet rules, rect/circle/ellipse, and use multiplicity all place markers at full cost. The guard today counts an inherited marker once (D_rects_inherit M=100 N=1000 passes and takes 2.6 s, while the same rects with attributes are refused by `RenderedElements`).
5. **A per-placement constant matters** for many small markers: weigh each placement as about 20 renders more than its subtree, and each marked element as one more subtree.
6. **One limit or several.** All four products convert to microseconds with the rates above, so the guard could sum them into one estimated decode time and refuse past one number (the owner's limit in seconds), or keep a limit per product. The rates differ by a factor of 30 between products, so a single unweighted count cannot serve.

## 8. The guard's own verdicts on every regenerated document

`guard-estimates.csv` in this folder holds what CB-29's guard says (its verdict, its estimate and each of the four terms, and the steps it took) for each document this record decoded (174) and each document follow-up 2 decoded (139), regenerated byte for byte by the generators or read from the core outputs and the repository's pie. It was generated with the guard of 8edf1636, after the red team's fixes. A document is refused at a limit when the guard refuses it for anything (a cap, or its estimate past the limit). No check spent its step budget.

**Step 0 documents** (174):

| Limit | Refused, decodes under 5 s (under 1 s) | Admitted, decodes past 5 s (past 20 s) | Refused in all | Margin to the largest real document |
|---|---|---|---|---|
| 1 s | 34 (0) | 0 (0) | 76 | 13x |
| 2 s | 16 (0) | 0 (0) | 58 | 26x |
| 3 s | 10 (0) | 1 (0) | 51 | 39x |
| 5 s | 2 (0) | 2 (0) | 42 | 65x |
| 10 s | 0 (0) | 18 (0) | 24 | 131x |

**Step 0 and follow-up 2 documents** (313):

| Limit | Refused, decodes under 5 s (under 1 s) | Admitted, decodes past 5 s (past 20 s) | Refused in all | Margin to the largest real document |
|---|---|---|---|---|
| 1 s | 55 (4) | 0 (0) | 112 | 13x |
| 2 s | 25 (0) | 0 (0) | 82 | 26x |
| 3 s | 15 (0) | 1 (0) | 71 | 39x |
| 5 s | 5 (0) | 2 (0) | 60 | 65x |
| 10 s | 0 (0) | 20 (0) | 37 | 131x |

The margin is the limit over the largest estimate of a real document, 76.4 ms (mermaid sequence n=1093). Every step 0 estimate equals `est2Ms` within 0.1% but two: A_desc R=2000 E=5000 and A_desc R=5000 E=10000 are 0.82 of it, since a branch of one descendant step now counts only the descendants of its left compound's matches (the red team's m2). That admits the first at 2 s (it decodes in 1.8 s) and the second at 10 s (9.7 s). The four refused under 1 s at a 1 s limit are follow-up 2's N_nth Q=2 R=100 N=10000 (195 ms, estimated at 1.12 s) and N_desc K=2, 3 and 4 R=100 F=1 N=10000 (216 to 264 ms, 1.30 s each), where the selector counts err wide; the two admitted past 5 s at a 5 s limit are A_compound R=5000 E=10000 (5.3 s, estimated at 4.7 s) and D_marker M=100 V=10 L=1000 (5.2 s, 2.7 s).

## Not verified

- No source code: the cost model rests on IL I listed with my own reflection tool, not on the projects' sources or a decompiler. The uses x all-elements term and the D_paths superlinearity are measured, not explained by the reading.
- Only this desktop. The CI runner (the "slower machine" of the reading's limit rule) was not measured; the thresholds are this machine's.
- The var() case k = 4 at 15 levels, and any `OutOfMemoryException` path, were not run (memory risk on a desktop that hosts runner VMs). The cut-off at 15 levels is measured for k = 2 and k = 3 only.
- No real-world export other than the repository's pie and core's mermaid outputs: the plotting-library, Illustrator, Inkscape, mermaid.js and draw.io shapes are guesses. G_scatter is generated from memory of a format, not compared with a real file.
- Not measured: rules inside `@media`, arcs and smooth curves in `d` against Svg.Skia's vertex list (the probe counts one vertex per arc), nested markers (a marked element inside a marker), `<use>` of a `<symbol>` with its own stylesheet scope, elements with several classes, and the distinction between "styled elements" and "all elements" (my generators have almost no unstyled elements, so the two columns fit identically).
- Some fits rest on two or three points per kind (A_unclassed, A_unmatched, A_compound, A_desc, A_list, C_typedecl, D_use, D_use_poly).
- The mermaid "largest" sizes come from 1.6x growth and four bisections near a 5 s budget whose renders vary; the true boundary may sit a few percent higher.
- The test host's .NET patch (10.0.15) is inferred from the installed runtimes, not logged from the process.

## Files

This folder holds `calibration.md`, `decision-tables.md`, `est.py`, `est.csv`, `followup2.md`, `f2-est-depth.csv` and `guard-estimates.csv` (section 8). The other files below stayed in the chunk's working notes.

- `calibration.md` (this file), `tables.md` (every sweep row), `fits.md` (every fit).
- CSVs: `sweep-A.csv`, `sweep-C.csv`, `sweep-D.csv`, `sweep-X.csv`, `sweep-big.csv`, `real.csv`, `real-mermaid.csv`, `mermaid-renders.csv`, `guardcost.csv`, `explore.csv` to `explore4.csv`, `smoke.csv`. Logs beside each (`*.log`), and `ci-state.log` (the pool state before and after every run).
- Plans: `plan-*.txt`. Runner: `run-probe.ps1`. Analysis: `analyze.py`, `thresholds.py`, `tables.py`.
- The probe: `Cb29Step0Probe.cs` (deleted from the worktree). The IL lister: `ildump\`; listings: `il\`.
- Generated SVGs: `svg\` (the #1581 reproductions and the scatter shape), `real\` (the repository's pie and the guard test shapes), `mermaid\` (core's outputs, typical and largest per dialect); `mermaid-vault\` holds the throwaway vaults the renders used.
