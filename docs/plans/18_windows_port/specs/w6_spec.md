# W6 executable spec — Structural surfaces: canvas & graph

Issues: W6-1 ([#745](https://github.com/coryj627/slate/issues/745)) · W6-2* ([#746](https://github.com/coryj627/slate/issues/746)). Milestone: [GH 22](https://github.com/coryj627/slate/milestone/22). One PR per issue — **these are the two largest issues in the program**; each may be delivered as a stacked PR series against a single issue if the wave demands it (the issue stays the unit of acceptance). *(\* W6-2 iff Milestone P shipped — an entry criterion, so effectively unconditional; the marker exists only for matrix mechanics.)*
Program: [00_program.md](../00_program.md) (decision 14; DoD §W-A/§W-C/§W-D). **Depends on (wave-6 gate): W4-1** (canvas table/grid substrate) **and W5-1** (navigator command surface + finalized chord table). Behavioral reference: the T program (`../../09_canvas/00_program.md`, interaction contract `t0_interaction_contract.md`, AT checklist) and the P program (`../../11_graph/00_program.md`).

## W6-1 · Canvas (Milestone T parity) — PR series 1

1. Consumes the canonical layer T built: parser/model/derivation (reading order, containment, adjacency, summaries), `canvas_apply` FFI, placement engine, scene/outline/table projections, op-log undo. **None of that is re-derived** (§W-G); if any W6 need turns out to be Swift-derived on mac, it moves to core first (decision 14).
2. The T interaction contract is the behavioral spec: mode stack (move/resize/connect/…), Esc-commits ladder, navigator command layer, mark-then-act multi-select, announcer grammar + verbosity + "Where am I?" — re-hosted on WPF with the canonical announcement events (§W-D rows for the whole announcer corpus).
3. Projections: outline (tree peer), table (W4-1 substrate), visual renderer with per-card UIA elements + windowing — the renderer's AT model mirrors the mac per-card AX element design, with ItemContainer/Selection patterns.
4. Authoring parity: full T verb set (create/delete/color/group/connect/edit/duplicate/convert/locate…), card editor, pickers, and nearest-preset naming. Slate-owned dark/light canvas fills use the shared W1-1 token set and meet the APCA acceptance inherited from T. Under Windows Contrast themes, semantic roles collapse onto compatible dynamic `SystemColors` pairs and preserve meaning with text/icons/borders rather than color alone; user-customized system colors are not APCA-gated. This issue records the canvas-specific checks, while W8-2 locks the shared dark/light pairs and Contrast-theme behavior behind CI/UI automation.
5. Dynamic Type equivalent: renderer labels respect Windows text scaling.
6. §W-A rows: scene/outline/table/apply round-trips byte-identical; §K-scale budgets re-verified through the binding (canvas benches' fixture sizes).
7. JAWS/NVDA canvas checklist (T's `at_smoke_checklist.md` re-expressed for UIA) executed and recorded (W7-4 owns the format).

## W6-2 · Graph view (Milestone P parity) — PR series 2

1. Consumes P's canonical model and its **accessible textual representation** (the entry-criterion artifact) + metrics substrate; accessible-first order per P's locked decisions (one model, two projections).
2. The P interaction model is the behavioral spec once shipped; same §W-A/§W-C/§W-D discipline; determinism guarantees (P rejected nondeterministic layout) hold identically through the binding.

- [ ] (each) matrix rows green; canonical-consumption audit (§W-G) recorded; AT checklists executed
