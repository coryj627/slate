# W7-1 editor accessibility architecture research

Research date: 2026-09-17 (America/New_York). Primary-source review prompted by
the owner asking how the Mac implementation works and whether an existing
structure offers a better Windows design. This is research, not an AT
acceptance run or approval to merge #747. Source links using `main`/`master`
describe the versions inspected on this date.

## Main finding: separate link elements from the Link text attribute

Microsoft defines `UIA_LinkAttributeId` as a `VT_UNKNOWN` containing the client
text range **targeted by an internal document link**, with null as the default.
It is not a general boolean link marker, and returning the source span itself
does not describe the destination of an external link, tag, or citation.
[Text attribute identifiers](https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-textattribute-ids).

Microsoft's ordinary hyperlink model is an element in the text container's
accessibility tree. The document remains a continuous text stream;
`GetChildren` exposes the hyperlink, `RangeFromChild` locates its text, and a
range inside the hyperlink identifies it through `GetEnclosingElement`.
The Hyperlink control type requires Invoke support. Compatible ranges backed
by the same text store must remain comparable.
[Embedded-object guidance and hyperlink examples](https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-textpattern-and-embedded-objects-overview),
[Hyperlink control type](https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-supporthyperlinkcontroltype).

This is a semantic contract problem in addition to the separately measured
[WPF Link interoperability problem](w7_1_link_interop_probe.md). A working
native adapter alone would not fix the meaning of the returned attribute.

## What Slate already does

The Mac source editor uses a plain `NSTextView` with its native VoiceOver text
behavior. It sets the text-area role, disables rich text and automatic link
detection, and applies canonical highlights as temporary color/underline
attributes. It does not implement the new editor semantic accessibility
mapping; its header explicitly distinguishes editor accessibility from
reading-view heading traits. See
[NoteEditorView.swift](../../../../apps/slate-mac/Sources/SlateMac/NoteEditorView.swift)
(lines 15–25, 203–210, 276–278, 936–1019, and the subclass at 1381).

The existing Mac reading pipeline is the closer local pattern: canonical
`ReadingInlineSegment` runs become attributed text, including native link
URLs and speech attributes; headings receive heading traits and levels.
See [ReadingInlineMapper.swift](../../../../apps/slate-mac/Sources/SlateMac/Reading/ReadingInlineMapper.swift)
(45–86) and [ReadingView.swift](../../../../apps/slate-mac/Sources/SlateMac/Reading/ReadingView.swift)
(561–562). AppKit also exposes an attributed-string-for-range accessibility
API; extending the source editor through that API would require separate
implementation and VoiceOver verification.
[Apple API](https://developer.apple.com/documentation/appkit/nsaccessibilityprotocol/accessibilityattributedstring(for:)).

Windows already uses real WPF `Hyperlink` elements with `NavigateUri` in
[ReadingDocumentBuilder.cs](../../../../apps/slate-windows/src/SlateWindows/Reading/ReadingDocumentBuilder.cs)
(1680–1720). [CodeCopyHyperlink.cs](../../../../apps/slate-windows/src/SlateWindows/Reading/CodeCopyHyperlink.cs)
documents field evidence that NVDA takes the spoken control role from the
child element. These are useful local precedents, although the AvalonEdit
source editor has no corresponding WPF document `Hyperlink` objects.

## Online implementations worth following

| Implementation | Verified source behavior | Relevance and limit |
|---|---|---|
| WPF | `HyperlinkAutomationPeer` exposes Hyperlink control type, a text-derived name, and Invoke calling the owner's click action. [Source](https://github.com/dotnet/wpf/blob/main/src/Microsoft.DotNet.Wpf/src/PresentationFramework/System/Windows/Automation/Peers/HyperlinkAutomationPeer.cs) | Closest host-framework precedent. A virtual editor span needs its own peer; this class requires a real `Hyperlink` owner. |
| Chromium | Link and bibliographic-reference roles map to Hyperlink elements. Text ranges return child element providers; the Text provider resolves child ranges. No `UIA_LinkAttributeId` case was found in the inspected range or node attribute implementations. [Node mapping](https://github.com/chromium/chromium/blob/main/ui/accessibility/platform/ax_platform_node_win.cc), [range children](https://github.com/chromium/chromium/blob/main/ui/accessibility/platform/ax_platform_node_textrangeprovider_win.cc), [RangeFromChild](https://github.com/chromium/chromium/blob/main/ui/accessibility/platform/ax_platform_node_textprovider_win.cc) | Strong example of the semantic tree plus continuous text model. Its range comparisons query its own range implementation, so it does not demonstrate arbitrary mixed-provider interoperability. |
| AccessKit | Shared tree schema feeds Windows UIA and macOS NSAccessibility adapters. The Windows range implementation returns no embedded children, has no Link attribute branch, and leaves FindAttribute unimplemented. It assumes its own range implementation for operand casts. [Architecture](https://github.com/AccessKit/accesskit), [Windows text source](https://github.com/AccessKit/accesskit/blob/main/adapters/windows/src/text.rs), [Mac adapter](https://github.com/AccessKit/accesskit/blob/main/adapters/macos/src/node.rs) | Useful cross-platform architecture reference. The inspected implementation is not a complete replacement for W7's rich semantics and cannot simply be mixed with WPF ranges. |
| Windows Terminal | A native `ITextRangeProvider` implementation shares its text-buffer model. Its base range provider returns no children and has no Link attribute implementation. [Source](https://github.com/microsoft/terminal/blob/main/src/types/UiaTextRangeBase.cpp), [interface](https://github.com/microsoft/terminal/blob/main/src/types/UiaTextRangeBase.hpp) | Useful for range lifetime, movement, and geometry design; not evidence for Markdown hyperlink semantics. |

## NVDA supports a child-element route, but speech still needs measurement

NVDA's generic `UIATextInfo.getTextWithFields` walks enclosing elements and
children by default. It calls `GetChildren` and `RangeFromChild`, clips ranges
using endpoint comparisons/movement, and emits control fields containing the
element's role. Hyperlinks are explicitly treated as elements whose name is
their content. Separately, its Link attribute handler treats a non-null
supported value as link formatting. The latter explains why the current
approach can produce a link marker without validating the attribute's
destination semantics.
[NVDA source](https://github.com/nvaccess/nvda/blob/master/source/NVDAObjects/UIA/__init__.py)
(276–280, 548–617, 689–718, 830–956, 999–1003).

This is evidence for a prototype using standard Hyperlink children, including
the generic UIA path rather than only a browser-specific implementation. It
does not prove Slate focus-mode speech, say-all, braille, or JAWS behavior.
Human checklist outcomes remain Pending.

## Recommended next investigation

Prototype virtual Hyperlink child peers backed by canonical Rust spans while
retaining AvalonEdit's text store, native range movement, and the existing
StyleId/StyleName decorator. Route Invoke through the existing activation
behavior. Return ordinary ranges through `RangeFromChild`; reserve Link
attribute handling for actual internal destination semantics, if needed.
This is a proposed fit, not an already verified solution.

The prototype must establish one owner per child, stable identities through
edits, correct enclosing/child behavior without cycles or duplicated text,
and both directions of Compare, CompareEndpoints, and MoveEndpointByRange.
Also verify selection, IME, offscreen access, navigation activation,
performance, NVDA, and licensed JAWS. Replacing all range exports with a
native provider is a larger fallback: WPF owns its wrapping and dispatch
boundary, so homogeneous native ranges are not a drop-in adapter change.

The current [W7 spec](../specs/w7_spec.md) §2.3 item 7 forbids editor children,
and §2.4 requires the source span as Link. A validated alternative must
explicitly revise those clauses, [contract 37](../../37_editor_peer_contracts.md),
and their tests before implementation is declared complete. The merge hold
remains in effect; research does not grant acceptance of the current deviation.
