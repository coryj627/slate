# W7-1 Link interoperability measurement

Date: 2026-09-17 (America/New_York). Automated engineering probe; this is
**not** an NVDA, JAWS or Narrator acceptance run. Those results remain Pending.

Environment: .NET SDK 10.0.401; .NET 10.0.12; Windows 11 build 26200.9457;
UIAutomationCore.dll file version 7.2.26100.1, product version 10.0.26100.1.
Both explicit CUIAutomation and CUIAutomation8 clients were checked.

## Isolated control and observed failure

A separate process hosted a native WPF RichTextBox containing
`Standalone native RichTextBox probe.` Its peer decorated the native Text
provider so Link returned a retained WPF wrapper around DocumentRange.
GetSelection returned that exact same wrapper. Native-only cases omitted
the tracing decorator, using WPF's TextRangeAdaptor directly. No Slate,
AvalonEdit, Markdown, session or FFI code was involved.

Ordinary DocumentRange.GetText(100) and GetSelection()[0].GetText(100)
succeeded. The Link object accepted QueryInterface for IUIAutomationTextRange,
but GetText(100) returned E_POINTER (0x80004003) before the provider callback.
GetText(-1) and endpoint comparison also crashed the isolated server in
earlier diagnostic runs. Priming through GetSelection did not repair Link.
Calling SDK vtable slot 12 directly reproduced the failure, excluding
FlaUI's method wrapper and generated interop invocation as the cause.

The WPF wrapper's controlling IUnknown and ITextRangeProvider pointers had
different vtables; the Link VARIANT held the former. ComDefaultInterface
did not change this. WPF's own wrapper also passes attribute values through
without wrapping them ([source](https://source.dot.net/PresentationCore/MS/Internal/Automation/TextRangeProviderWrapper.cs.html)).

## Forwarding adapter result

The adapter exposes IUnknown and ITextRangeProvider at the same native
pointer, aggregates the system free-threaded marshaler, and forwards all
eighteen text methods to the existing dispatcher-wrapped WPF range. The
version without the marshaler hung. With it, traces showed a worker-thread
call reaching the native text provider on its STA dispatcher.

| Client operation | Result on both clients |
|---|---|
| Link.GetText(100), fresh Link.GetText(100) | S_OK, exact text |
| Link.Clone().GetText(100) | S_OK, exact text |
| Link.Compare(document) | S_OK, true |
| Link.CompareEndpoints(Start, document, Start) | S_OK, zero |
| document.Compare(Link), document.CompareEndpoints(..., Link, ...) | E_INVALIDARG (0x80070057) |
| Clone compared with document in either direction | S_OK, true |

The reverse failure is a remaining limitation: ordinary WPF range operand
unwrapping rejects the export adapter. Clone the exported range before
passing it as that operand. COM aggregation did not fix the failure and
is not used. Acceptance of the limitation is **pending the owner decision**
in [contract A-6](../../37_editor_peer_contracts.md).

The simple adapter's creator reference is released immediately after its
RCW is created. Temporary VARIANT references balance. Releasing client
references and the probe's cached server export freed the native allocation
at reference count zero. The production provider does not cache an export;
the unit witness also checks that releasing its owned RCW frees the export.

## Committed regression witnesses

`EditorSemanticTextRangeTests.LinkAttributeRangesUseWpfMarshalingAndAcceptOrdinaryRangeOperands`
checks foreign-thread reads, actual native ABI calls, search, cloning,
selection and movement. `LinkExportUsesOneComIdentityAndReleasesItsNativeOwnership`
checks interface identity and ownership. These run through the real editor
peer and canonical session.

`ShellAccessibilityTests.EditorTextPattern_SemanticAttributesUnitsAndEvents_AreClean`
calls the Link object's GetText(-1), StyleName, Clone and comparison methods
across processes. It also checks ordinary Text-pattern units, both-direction
attribute search, geometry, the peer-tree walk, batched events and axe.
It does not catch and ignore Link read failures.

```powershell
dotnet test apps/slate-windows/tests/SlateWindows.Tests/SlateWindows.Tests.csproj --configuration Release --filter FullyQualifiedName~EditorSemanticTextRangeTests
$env:SLATE_REQUIRE_UI_AUTOMATION = '1'
dotnet test apps/slate-windows/tests/SlateWindows.AccessibilityTests/SlateWindows.AccessibilityTests.csproj --configuration Release --filter FullyQualifiedName~EditorTextPattern_SemanticAttributesUnitsAndEvents_AreClean
```

The cross-process journey passed locally with the adapter and strict Link
method assertions. CI and stock-reader human acceptance are separate gates.
