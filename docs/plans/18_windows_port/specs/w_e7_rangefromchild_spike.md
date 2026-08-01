# W-E7 gate spike — `RangeFromChild` over custom peers

**Verdict (2026-08-01): the gate is OPEN.** `ITextProvider::RangeFromChild`
resolves every custom reading element cross-process on the live app —
the planned NVDA browse-mode add-on (00_program §W-E7) is technically
viable. Pinned by
`ReadingTextPattern_RangeFromChildResolvesEveryCustomPeer` in
`ShellAccessibilityTests`, which runs in the CI FlaUI gate on every PR.

## The question and why it gated W-E7

The W-E7 add-on grants genuine NVDA browse mode over the reading
document (`treeInterceptorClass` on the document object — the
Kindle/Chromium pattern; browse mode cannot be requested through UIA,
G21). Browse mode positions its virtual cursor by mapping OBJECTS to
TEXT RANGES, and NVDA leans on `rangeFromChild` in four load-bearing
places (verified in nvaccess/nvda@5ba9521):

1. `UIATextInfo.__init__` — object position → range; failure is
   `LookupError`, **no fallback** (`NVDAObjects/UIA/__init__.py:508`).
2. `UIABrowseModeDocument.__contains__` — ends with
   `makeTextInfo(obj)`; failure returns **False**, so the element is
   treated as *outside the document* and focus never routes through
   browse mode (`UIAHandler/browseMode.py:751`). This is the
   make-or-break: one unresolvable element silently exiles itself.
3. The renderer's child loop — every child from `GetChildren` is
   mapped back via `rangeFromChild`; failure silently drops the child
   (`NVDAObjects/UIA/__init__.py:896`).
4. Quick-nav iteration — failure skips the item or **aborts the whole
   iteration** (`UIAHandler/browseMode.py:294-455`).

UIA browse mode builds no off-screen buffer: `UIABrowseModeDocumentTextInfo`
is a live proxy over `UIATextInfo`, so these calls run on every read.
Whether WPF answers them for our custom peers was undocumented — "the
highest-risk unknown" (w3_1_container_spike.md).

## The mechanism (dotnet/wpf `TextAdaptor.RangeFromChild`)

WPF resolves in three tiers (`TextAdaptor.cs:561-639`):

1. **TextElement peers** — `ElementStart`/`ElementEnd`. Covers
   Hyperlinks: the embed Jump link, `CodeCopyHyperlink` (whose peer
   stays a `TextElementAutomationPeer` — the field-pass-3 Button
   control-type override deliberately preserved this).
2. **UIElements whose LOGICAL PARENT is an `InlineUIContainer` /
   `BlockUIContainer`** — the container's `ContentStart`/`ContentEnd`.
   Exactly ONE `LogicalTreeHelper.GetParent` hop; custom
   `FrameworkElementAutomationPeer` subclasses qualify by type
   (FEAP derives from `UIElementAutomationPeer`). This is the tier
   carrying `ReadingMathElement`, `ReadingDiagramElement`, and the
   task `CheckBox`.
3. **A linear adjacency scan** over the text container — matches only
   elements directly adjacent at element/embedded positions.

Anything else throws `InvalidOperationException` ("Element is not
within the document range"), which a COM client sees as HRESULT
`0x80131509` (`COR_E_INVALIDOPERATION`) rather than the `E_INVALIDARG`
the Win32 spec prescribes — a WPF spec deviation that is harmless to
NVDA (its `COMError` catch is HRESULT-agnostic) but worth knowing when
reading NVDA debug logs.

## THE INVARIANT this imposes (load-bearing, forever)

**Every interactive UIElement placed in the reading document must be
the DIRECT child of its `InlineUIContainer`/`BlockUIContainer`.** A
wrapper panel between container and control fails all three tiers:
the element still appears in the UIA tree (WPF's `GetChildrenCore`
`iterate()` fallback surfaces nested descendants), but its
`RangeFromChild` throws — the precise combination that makes NVDA
exile the element from browse mode (§call-site 2) while still showing
it to object navigation. All current elements satisfy the invariant
(`ReadingDocumentBuilder`: `new InlineUIContainer(element)` for math /
diagram / checkbox, `new BlockUIContainer(visual)` for embed images);
the pinned sweep turns any future violation into a CI failure with
the offending element named.

Peer-suppressed elements (embed images return a null peer — mac
`accessibilityHidden` parity) never appear as text-pattern children
and are exempt by construction.

## Empirical result (2026-08-01, live app, UIA3 cross-process)

`ReadingTextPattern_RangeFromChildResolvesEveryCustomPeer` launches
the real `SlateWindows.exe` over a vault exercising every hosting
shape, enters reading mode, and probes:

| Probe | Hosting shape | Result |
|---|---|---|
| math element | custom FEAP in `InlineUIContainer` (tier 2) | resolves |
| diagram element | custom FEAP in `InlineUIContainer` (tier 2) | resolves |
| task checkbox | native control in `InlineUIContainer` (tier 2) | resolves |
| Copy code | `CodeCopyHyperlink` TextElement peer (tier 1) | resolves |
| embed Jump link | Hyperlink TextElement peer (tier 1) | resolves |
| **sweep**: every `DocumentRange.GetChildren()` child | all of the above | resolves |

Each probe also asserts range sanity: endpoints ordered, contained
within the document range, `GetEnclosingElement` non-null. Range TEXT
is deliberately unasserted — WPF blanks embedded objects in the text
stream (G23), and the add-on needs the POSITION, not the text.

## Contingency (recorded, not needed today)

If a future element ever genuinely requires nesting, the app controls
the provider: `HeadingStyleTextProvider` (the decorator already
answering StyleId/StyleName) could implement an ancestor-walking
`RangeFromChild` — walk `LogicalTreeHelper.GetParent` from the child
to the first UIContainer, then answer with that container's range —
making tier 2 transitive. Not implemented: the direct-child invariant
is simpler, satisfied everywhere, and CI-enforced.

## What W-E7 still needs (future decision, unchanged scope)

The add-on itself: an NVDA appModule granting
`treeInterceptorClass = UIABrowseModeDocument` (or subclass) on the
`ReadingSurface` document object via the W3-1 identity contract
(`SlateWindows.exe` / `Slate.MainWindow` / `ReadingSurface`), plus
Role.MATH + `mathMl` mapping to light up NVDA's native MathCAT
pipeline (G23's convention layer). Add-on Store first, upstreaming à
la Kindle as the endgame. Enhancement-only: every §W-C gate keeps
passing with stock ATs.
