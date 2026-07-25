# W3-1 container spike — findings

Spike for [#728](https://github.com/coryj627/slate/issues/728), answering the two mechanism choices `w3_inline_runs_spec.md` §10.6 left open. Code: `apps/slate-windows/tools/ContainerSpike` (both containers over one fixture note) and `apps/slate-windows/tools/ContainerSpikeProbe` (UIA client probe). Both are **throwaway** and are deleted when W3-1 lands.

## Headline

**Neither container satisfies §W3-1 as written, and they fail on complementary axes.** This is not the either/or §10.6 anticipated: the shape that meets the requirements is one container *plus custom `AutomationPeer`s*, and that is scope neither §10.6 nor `w3_spec.md` costed.

| requirement | `FlowDocumentScrollViewer` | `ItemsControl` of blocks |
|---|---|---|
| Text pattern over the note (§W3-1 item 4) | **yes** — `Document`, 363 chars | **no** — no Text provider at all |
| reading order correct | **yes** — 12/14 landmarks, none out of order | n/a — 0/14 |
| `HeadingLevel` reaches the peer (§10.6 item 2) | **no** | **yes** — Level1 + Level2 |
| `Hyperlink` control types (§10.3) | **no** — 0 peers | **yes** — 5 peers |
| `HelpText` carries `AxText` (§10.3) | n/a | **yes** — `Unresolved link`, citation speech |
| native `List`/`ListItem` (owner call) | **no** — 0 / 0 | **no** — 1 `List` (the control's own chrome), 0 `ListItem` |
| non-text children in the text range | **no** — code interior and embed card absent | n/a |
| interactive children invokable | yes — 6 | yes — 4 |

## What this settles

**§10.6 item 2 is answered.** `AutomationProperties.HeadingLevel` does **not** survive onto the peer of a `FlowDocument` `Paragraph`; it does survive on a `TextBlock`. Heading-level exposure is therefore a cost of choosing `FlowDocument`, not a free property of it.

**The owner's native-list call costs custom peers either way.** Neither container produced a single `ListItem` control type. WPF's `System.Windows.Documents.List`/`ListItem` are *layout* elements: they render markers and indentation, and in the flow variant the list arrives as document text (`•\tfirst bullet`). So "JAWS/NVDA list navigation works natively" (`w3_spec.md:23`) requires W3-1 to author `AutomationPeer`s regardless of which container wins. That is new, and it should be reflected in the issue before the PR is scoped.

**`ItemsControl` cannot satisfy §W3-1 item 4.** It exposes no Text provider, so there is no `DocumentRange`, no say-all continuity, and nothing for the existing §W-C text-range assertions (`ShellAccessibilityTests.cs:1106-1162` already drives `GetText`/`MoveEndpointByRange`/`CompareEndpoints`) to attach to. A Text pattern cannot be retrofitted onto a stack of `TextBlock`s without writing a text provider from scratch.

## Client-side confirmation

The UIA **client** probe was run from an interactive desktop and **agrees with the provider-side reading on every discriminating measurement**: `flow` = `Document` + Text pattern + 363 chars + 12/14 landmarks + **0 hyperlinks** + no `HeadingLevel`; `items` = no Text pattern + 5 hyperlinks (2 carrying `AxText` as `HelpText`) + `HeadingLevel` Level1/Level2. So "FlowDocument exposes no `Hyperlink` control types" is a real property, not an artefact of walking the provider tree.

**Two results in the first client run were the spike's own bugs, not container properties**, and are recorded here because they nearly became findings:

- *"hyperlinks focusable: 0"* in both variants. Every `Hyperlink` had `Command` set to a `RoutedCommand` with **no `CommandBinding`**, so `CanExecute` was false and WPF disabled it. Replaced with a click handler. Re-measured afterwards: **the 0-vs-5 hyperlink split is unchanged**, so the headline finding survives its own correction.
- *2 axe-windows errors in both variants* (`NameNotNull`, `SiblingUniqueAndFocusable`). Caused by the fixture naming both task checkboxes `"Task"`, not by either container — identical in both columns, so they discriminate nothing. Now named from the item text.

## What is NOT settled

**Whether NVDA can reach the flow variant's links.** Zero `Hyperlink` control types does not settle it: in a UIA document NVDA builds its browse-mode buffer from the **text pattern** and can find embedded objects through text ranges rather than through control-tree children. If NVDA's `K` reaches those links, the missing control types are not disqualifying; if it is silent, they are. **This is now measurable rather than a matter of opinion** — see below.

**What JAWS does.** No automation path was found for JAWS; that half stays manual.

## Automating the NVDA half

[`NvdaTestingDriver`](https://github.com/kastwey/nvda-testing-driver) drives a bundled portable NVDA through a modified NvdaRemote plugin and returns spoken text, so §10.6's "recorded AT evidence" can be a committed test instead of a manual script — for this spike and for every W3 surface after it. `ContainerSpikeProbe --nvda` runs say-all, `H`, `H1`/`H2`, `K`×4, `L`, `I`×3, table and button nav against both variants and tabulates what NVDA said against what it must say.

**Currency check, per the `w3_spec.md:14` dependency doctrine:**

| | finding |
|---|---|
| version | `0.2.0-beta` — never left beta |
| upstream | last commit **2022-12-08**, ~3.5 years stale |
| .NET 10 | restores and loads cleanly (`NvdaTestingDriver, Version=0.2.0.0`) |
| security | pulls `Newtonsoft.Json` **12.0.1**, GHSA-5crp-9r3c-p9vr (**high**) — overridden to 13.0.3 in the spike csproj |
| NVDA driven | the bundled 2022-era portable copy, **not** the NVDA 2026.1.1 installed on this machine |
| environment | needs an interactive desktop; other screen readers must be off |

So: good enough to answer the container question, **not** obviously good enough to adopt into the shipped test suite. Adopting it for the wave would need a decision on the stale bundled NVDA and the CVE, and that is a separate call from this spike's.

## Method, and its limits

Two independent readings were taken to guard against measuring an artefact: one with the element laid out detached (`Measure`/`Arrange`), one hosted in an off-screen `Window` so a `PresentationSource` exists. **They agree exactly** — 16 peers for flow and 38 for items in both — so the provider-side numbers are not an artefact of the missing window. They are still provider-side, which is the limit noted above.

The two containers consume the **same** core block model (`ReadingBlocksSource` + `ReadingInlineSegmentsSource`) through the **same** inline builder, so the container is the only variable. Reading-order landmark checks assert both presence and authored order.

## Provisional recommendation

`FlowDocumentScrollViewer`, plus custom peers for heading level and list semantics — **conditional on the NVDA link result above**.

The reasoning is an asymmetry in what can be added later. The Text pattern is the one property that cannot be retrofitted: giving an `ItemsControl` a document text range means writing an `ITextProvider` over a stack of `TextBlock`s from scratch, and every §W-C text-range assertion, say-all continuity and browse-mode behaviour depends on it. Heading level, link roles and list roles are all things an `AutomationPeer` can supply.

The condition matters, though. If NVDA's `K` cannot reach links in the flow variant, then link exposure *also* needs custom peers — on top of headings and lists — and a container that requires re-authoring three of its four semantic layers is worth re-opening the choice over. Run `--nvda` before treating this as decided.

## Reproducing

```
# provider-side, runs anywhere including CI and a session-0 shell
dotnet build apps/slate-windows/tools/ContainerSpike/ContainerSpike.csproj
apps/slate-windows/tools/ContainerSpike/bin/Debug/net10.0-windows/ContainerSpike.exe \
  --probe peers --out spike-evidence

# client-side + axe: REQUIRES an interactive desktop (session 1)
dotnet run --project apps/slate-windows/tools/ContainerSpikeProbe -- --out spike-evidence

# what NVDA actually says: interactive desktop, and turn off any running
# screen reader first (the driver starts its own portable NVDA)
dotnet run --project apps/slate-windows/tools/ContainerSpikeProbe -- --nvda --out spike-evidence

# eyes/ears on one variant
apps/slate-windows/tools/ContainerSpike/bin/Debug/net10.0-windows/ContainerSpike.exe \
  --container flow      # or: items
```

The client probe prints the manual JAWS/NVDA script (say-all, `H`, `K`, `L`/`I`, Tab, browse-vs-focus mode) that closes the remaining evidence gap. Record the outcome in `w_c_matrix.md` with AT versions.
