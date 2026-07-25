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

## What is NOT settled, and must be before the choice is final

1. **Whether a UIA *client* sees the flow variant's hyperlinks.** The probe walks the **provider** tree via `AutomationPeer.GetChildren()`. WPF may surface `TextElement` peers (including `Hyperlink`) only through the text pattern's range children rather than the control tree, in which case the flow variant's "0 hyperlink peers" is an artefact of the walk and its real deficit is much smaller. **This is the single most decision-relevant unknown**, because §10.3's whole activation contract rides on `ControlType.Hyperlink` + `Invoke` + `HelpText`.
2. **What JAWS and NVDA actually do.** §10.6 makes recorded AT evidence the pass criterion. UIA exposure is necessary, not sufficient — a reader may reach BlockUIContainer children through the control tree even though they are outside the text range.
3. **axe-windows cleanliness**, which needs a live process.

None of these can run from a session-0 shell: UIA cannot cross sessions, so the client probe must be run from an interactive desktop.

## Method, and its limits

Two independent readings were taken to guard against measuring an artefact: one with the element laid out detached (`Measure`/`Arrange`), one hosted in an off-screen `Window` so a `PresentationSource` exists. **They agree exactly** — 16 peers for flow and 38 for items in both — so the provider-side numbers are not an artefact of the missing window. They are still provider-side, which is the limit noted above.

The two containers consume the **same** core block model (`ReadingBlocksSource` + `ReadingInlineSegmentsSource`) through the **same** inline builder, so the container is the only variable. Reading-order landmark checks assert both presence and authored order.

## Provisional recommendation

`FlowDocumentScrollViewer`, plus custom peers for heading level and list semantics — **conditional on finding 1**. The reasoning: the Text pattern is the one property that cannot be added afterwards, while heading level, link roles and list roles are all things a peer can supply. If finding 1 resolves badly (a client genuinely sees no hyperlinks in a FlowDocument), the balance shifts sharply, because link exposure would then also need custom peers on top of everything else — and that combination is worth re-opening the choice over.

## Reproducing

```
# provider-side, runs anywhere including CI and a session-0 shell
dotnet build apps/slate-windows/tools/ContainerSpike/ContainerSpike.csproj
apps/slate-windows/tools/ContainerSpike/bin/Debug/net10.0-windows/ContainerSpike.exe \
  --probe peers --out spike-evidence

# client-side + axe: REQUIRES an interactive desktop (session 1)
dotnet run --project apps/slate-windows/tools/ContainerSpikeProbe -- --out spike-evidence

# eyes/ears on one variant
apps/slate-windows/tools/ContainerSpike/bin/Debug/net10.0-windows/ContainerSpike.exe \
  --container flow      # or: items
```

The client probe prints the manual JAWS/NVDA script (say-all, `H`, `K`, `L`/`I`, Tab, browse-vs-focus mode) that closes the remaining evidence gap. Record the outcome in `w_c_matrix.md` with AT versions.
