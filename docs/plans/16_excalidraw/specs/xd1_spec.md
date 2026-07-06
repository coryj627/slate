# XD1 executable spec — Embeds: `![[drawing]]` renders inline

Issues: XD1-1 ([#681](https://github.com/coryj627/slate/issues/681)) · XD1-2 ([#682](https://github.com/coryj627/slate/issues/682)). Milestone: [GH 34](https://github.com/coryj627/slate/milestone/34). One PR per issue. Gate: XD0-5 merged.
Program: [00_program.md](../00_program.md) (decisions 2, 6–9; §XD-A). Format facts: [../01_research_brief.md](../01_research_brief.md).

Baseline facts (verified 2026-07-05, this worktree):

- `EmbedResolution` (embeds.rs:26): `FullNote | Section | Block | Image | Unresolved`; `MAX_EMBED_DEPTH = 3` (:22); `IMAGE_EXTENSIONS` (:108), `looks_like_image` (:113), `infer_mime` (:125).
- Resolution pipeline: `Session::resolve_embed` (session.rs:1183) → `resolve_embed_at_depth` (:1192); image targets short-circuit through `read_attachment` (:782, doc comment at :1176); note targets go through `link_resolver`, then anchor narrowing; nested embeds pre-resolved to depth 3.
- uniffi mirror: `EmbedResolution` (slate-uniffi/src/lib.rs:1970), `resolve_embed` (:719). Adding an enum variant regenerates into a new Swift case — every `switch` over it must be updated in the same PR (Swift exhaustiveness makes misses a compile error).
- Swift render: `EmbedView.swift` — `switch resolution` (:51), image arm (:58) wraps content in `EmbedDisclosure` (:289; label, jump-to-source, `initiallyExpanded: depth == 0`); decode-failure view precedent in `imageDecodeFailureView`. **`EmbedDisclosure` today has a plain-`Text` label and no header accessory; its only action lives in the expanded content** — XD1-2 extends it (rule 4), it does not merely reuse it.
- Mermaid precedent for SVG-in-Swift: `MermaidView.swift` — SwiftDraw → `NSImage` → `Image(nsImage:)`, `structured_description` as `.accessibilityLabel`, source-text fallback on render failure. SwiftDraw is already a package dependency (apps/slate-mac/Package.swift:42–49).
- Link indexing of wrapper files needs **zero work**: `.excalidraw.md` is markdown to the scanner; the plugin duplicates every link as plain markdown in its sections *so that* indexers see them (brief §2). XD1-1 adds a test proving it, not code.
- Wikilink alias: `ParsedLink` carries the alias (links.rs); `resolve_embed(host, target, alt)` already receives it as `alt`.

---

## XD1-1 · Core: `EmbedResolution::Excalidraw` + routing (#681) — PR 1

### API (pinned)

```rust
// embeds.rs — new variant (order: after Image, before Unresolved)
Excalidraw {
    target_path: String,      // vault-relative resolved path (.excalidraw or .excalidraw.md)
    svg: Vec<u8>,             // XD0-4 output, images pre-resolved session-side
    description: String,      // XD0-3 scene_summary (§XD-A: never empty)
    warnings: Vec<String>,    // non-empty ⇒ EmbedView shows the degraded badge
    alt: Option<String>,
},
```

### Normative routing rules (`resolve_embed_at_depth`)

1. **Raw form:** target (anchor-stripped; an anchor on a raw target ⇒ `AnchorIgnored` warning, same as rule 2 — never silent) ending `.excalidraw` (ASCII case-insensitive, `looks_like_image` pattern) ⇒ resolve via the same path rules as images; read with `read_attachment` caps; `parse → derive → render_svg`; return `Excalidraw`. Not found ⇒ `Unresolved(TargetNotFound)`.
2. **Wrapper form:** when note resolution has produced a markdown file's contents (the existing `FullNote`/`Section`/`Block` path), sniff frontmatter first: `excalidraw-plugin` key present ⇒ `parse_wrapper → derive → render_svg` ⇒ `Excalidraw`, **regardless of any `#heading`/`^block` anchor** (the wrapper's headings/block-ids are plugin plumbing, not user content — anchor is ignored, warning appended). This check must run before section/block extraction so `![[Drawing#Text Elements]]` never leaks wrapper internals.
3. Excalidraw resolutions are **leaves**: no nested-embed recursion into them; they count toward depth exactly as `Image` does. Depth-exceeded inside a note chain still yields `Unresolved(DepthLimitReached)` before any drawing work.
4. Degraded loads still return `Excalidraw` with empty-scene SVG + honest description + warnings (decision 11) — the session converts `WrapperError::DecompressFailed` into the degraded scene per xd0-2 rule 3; `Unresolved` is only for *resolution* failures (missing file, depth).
5. Wrapper image refs resolve session-side per XD0-5's `open_excalidraw` rule (link_resolver + read_attachment caps; failures ⇒ placeholders) — same code path, no duplication (extract a shared session helper if needed).

uniffi: add the variant to the mirror enum (:1970) + `From` impl; regenerate bindings; update **every** Swift switch over `EmbedResolution` in the same PR (compiler enumerates them).

### Tests (PR 1)

- Routing: raw hit, raw miss, wrapper hit (all fence forms via the XD0-2 corpus), wrapper-with-anchor (anchor ignored + warned), depth interaction, degraded file ⇒ rule 4, alias ⇒ `alt`.
- Backlinks baseline proof: outgoing_links of a wrapper fixture include its Text Elements + Embedded Files wikilinks (session/tests/links_embeds.rs).
- Read-only: resolving embeds mutates no vault bytes (extends `census_excalidraw_read_only` fixture set).

- [ ] Variant + rules 1–5; shared image-resolution helper
- [ ] uniffi + regenerated bindings; all Swift switches updated
- [ ] Tests above; fmt/clippy

## XD1-2 · Swift: EmbedView renders drawings (#682) — PR 2

### Normative rules

1. New arm in the `EmbedView` switch (:51): `case .excalidraw(targetPath, svg, description, warnings, alt)` → `EmbedDisclosure(label:, jumpToSourceAction:, jumpToTarget: targetPath, initiallyExpanded: depth == 0)` — label = alt ?? display title derived like images (`imageEmbedTitle` precedent).
2. Content: SwiftDraw renders `svg` → `Image(nsImage:)`, `.resizable().scaledToFit().frame(maxWidth: .infinity)`. SwiftDraw failure ⇒ failure view (Mermaid precedent): warning icon + "Drawing could not be rendered" + the `description` as text (information survives render failure — §XD-A).
3. AX: the rendered drawing is **one** AX element; `.accessibilityLabel(label)`, `.accessibilityValue(description)` — a VO user reading a note hears the full structural summary inline. Per-element AX belongs to the XD2-4 tab surface, not embeds; the disclosure's activation affordance is the path to more.
4. **`EmbedDisclosure` extension (this PR owns it):** add an optional header-accessory slot (badge view + action button) to `EmbedDisclosure` — existing variants pass nothing and render byte-identically; the header stays a plain label with no `.isHeader` trait (preserve the audit-#194 decision). `warnings` non-empty ⇒ compact degraded badge in that slot (existing warning-badge tokens; APCA Lc ≥ 75 both appearances), badge tooltip lists warnings.
5. **No "Open Drawing" action in this PR.** XD1-2 ships without it; **XD2-1 adds it** to the header-accessory slot once `EditorItem.excalidraw` exists — the wave gate (Wave 3 after Wave 2) guarantees the order, so nothing dangles. Jump-to-source (already in `EmbedDisclosure`) is the only navigation until then.
6. Dynamic Type: header/badge text scales; the drawing itself scales with the embed width (it is an image, not text — no reflow claim).

### Tests (PR 2)

Unit: arm renders for a fixture SVG; failure path shows description text; AX label/value populated; badge appears iff warnings. a11y-check 100/100 on tip; APCA measured both appearances for badge/header (record in PR).

- [ ] Rules 1–6
- [ ] Unit + a11y gates; APCA measurements in PR description
