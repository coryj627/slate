# U0 — Baseline & design foundation

**Goal.** Lay the technical and visual foundation the rest of the program stands on: raise the OS floor, build the single source of truth for icons and design tokens (light + dark), and extend the test harness so U1–U5 inherit the presentation-ready gates automatically. No user-visible feature ships here — but nothing else can hit the bar without it.

**Depends on:** nothing. **Unblocks:** all of U1–U5. **Parallel:** none (do this first).

**Milestone-level risk:** low technically, but the token + icon layer is load-bearing for the whole program's visual coherence and dark/light gate — get the API shape right so call sites never reach for a literal.

## Issues

### U0-1 · Chore: raise deployment target to macOS 15; remove 13/14 workarounds `swift-ui` `test`
- Bump `Package.swift` platform `.macOS(.v13)` → `.macOS(.v15)`; update CI matrix and any runner images.
- Remove the accreted back-compat: one-arg `.onChange` → two-arg; adopt modern `NavigationSplitView` / focus APIs where they delete workaround code (audit the comments that say "sticking with the one-arg form … macOS 13 minimum").
- **Acceptance:** app builds and full suite is green on the macOS 15 SDK with no availability-related warnings; no behavior change.

### U0-2 · Mac UI: `SlateSymbol` semantic icon layer `swift-ui` `a11y` `design`
- A single semantic enum/registry mapping app roles (`.search`, `.save`, `.newTab`, `.splitRight`, `.readingMode`, `.editingMode`, `.folder`, `.folderOpen`, leaf icons, …) → SF Symbols v7 names, with `if #available(macOS 26, *)` fallbacks to a v6 equivalent for macOS 15–25.
- Enforces consistent size/weight/rendering-mode per surface; every symbol constructor requires (or defaults) an accessibility label so an icon can't ship unlabeled.
- Replace existing raw `Image(systemName:)`/`Label(_, systemImage:)` call sites incrementally (start with the toolbar in `MainSplitView`).
- **Tests:** every semantic role resolves on both the v7 and fallback path; snapshot of the toolbar renders identically pre/post; a11y label present for each. a11y-check 100/100.
- **Acceptance:** no call site references a raw SF Symbol string; toggling the SDK availability path yields a valid symbol in every case.

### U0-3 · Mac UI: design-token system (spacing / type / color) with light + dark + APCA `swift-ui` `a11y` `design`
- Centralize spacing scale, type ramp, and **semantic color roles** (surface, surface-secondary, text-primary, text-secondary, accent, selection, separator, destructive, …) as dynamic colors correct in light and dark. No literals at call sites.
- Authored so **Milestone R** can later re-skin the roles (tokens are the theming seam) without touching U call sites.
- Ship an APCA verification helper (extend `APCAContrast` test util) that measures token pairings in both appearances.
- **Tests:** APCA Lc ≥ 75 for every text-on-surface and control pairing in **both** light and dark; snapshot of a token catalog view in both appearances.
- **Acceptance:** a documented token catalog; the tightest contrast pairs pass Lc ≥ 75 measured in both modes.

### U0-4 · Test: presentation-ready test harness for U1–U5 `test` `a11y` ✅ shipped
Shipped as `PresentationReady` (`apps/slate-mac/Tests/SlateMacTests/PresentationReady.swift`) — the single entry point U1–U5 surface tests call to hold themselves to the program DoD. Assertions:
- `assertContrastFloor(_:)` — every `(text, surface)` pairing clears APCA `|Lc| > 75` in **both** Aqua and DarkAqua (DoD §D). Backed by the `APCAContrast.lc(text:background:for:)` per-appearance extension.
- `assertResolvesDistinctlyPerAppearance(_:)` — each dynamic color resolves to a different value light vs dark (no appearance leak).
- `assertRendersInBothAppearances(_:)` — the view renders to a finite, non-empty size in both appearances (headless via `ImageRenderer`; catches per-appearance crashes / failed renders).

**Honest coverage boundary.** What is **not** unit-testable is caught only *partially* by two other gates — neither fully verifies runtime behaviour, so the residual is a known **manual** step, not "covered by automation":
- **`a11y-check`** (CI, over all of `Sources/SlateMac`, so new component families are scanned automatically — the "wire into the a11y baseline" requirement): **static anti-patterns only** — missing accessibility label, `lineLimit(1)`, fixed-point font size, animation lacking a Reduce-Motion guard. It does **not** verify that text actually reflows at XXL or that an animation is actually suppressed under Reduce Motion.
- **VoiceOver feature-test runbook** (`docs/runbooks/voiceover-feature-test.md` §3b, added here): **manual** behavioural checks for Dynamic Type reflow and Reduce-Motion, plus VoiceOver label/trait/reading-order — the parts with no XCTest-introspectable surface (no public API to read a rendered SwiftUI AX tree; the `\.dynamicTypeSize` override isn't honored by headless `ImageRenderer`/`NSHostingView`, measured: identical size at `.large` and `.accessibility5`; animation timing isn't observable).

**Automated behavioural reflow/animation testing remains an open gap** (manual runbook only). The harness deliberately does not fake those assertions. Exercised by `PresentationReadyTests` (render smoke + a contrast negative control proving the check has teeth) and by `DesignTokensTests` (token correctness through the same harness).
