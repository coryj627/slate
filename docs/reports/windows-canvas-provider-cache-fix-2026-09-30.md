# Canvas WPF peer cache repair evidence — 2026-10-01

Baseline: d13f4b320ebee41a62a3a8e732fe91acc1fd24f1, Windows 10.0.26300, .NET 10.0.12. Final repair is an uncommitted delta over this baseline; manifests record the exact sources and binaries.

The real WPF regression connects the hosted root with WM_GETOBJECT, uses the renderer-attached automation peer, proves a non-null initial near-card selection provider, then raises Down through the renderer's PreviewKeyDown route. Document selection, installed selection, viewport and Evidence materialization all reach their required state before the provider assertion. No test manually resets the peer cache.

Behavioral RED against preserved original production:

- Initial focused provider runs fail at the exact post-Down null-provider assertion in 0.5383367 and 0.5342361 seconds.
- Tightened combined run preserves the connected-seed premise by deriving/measuring the initial near state before showing the window. Provider RED is 0.1360460 seconds. Own-visibility-under-hidden-ancestor RED is 0.4202000 seconds at Assert.Empty; the installed state is held unchanged through those visibility toggles. The strengthened existing hidden-board fact passes.

Repair: synchronously refresh the existing board peer's child cache after a winning installed state and on its own VisibilityProperty changes. The peer registry, topology derivation, virtualization, selection, navigation and focus semantics are unchanged; FromElement creates no idle peer. Two new regressions retain hidden children, identity round trips and VirtualizedItem/SelectionItem cells. The existing hidden-board fact now uses the actual peer and removes its manual-reset helper while preserving empty-hidden/nonempty-shown assertions.

Focused GREEN: 3/3 passed, zero skipped; exact TRX wall 1.5758268 seconds, result durations 0.1559756 / 0.1905308 / 0.4291719 seconds. Scoped whitespace formatting and common Release build pass; build has zero warnings/errors. Independent read-only review found no actionable blocker.

Original physical CanvasSurfaces_VisualBoardPeersAndZoom_AreClean and broader suites are delegated back to root. The deterministic provider defect is proven repaired; the original physical journey must pass before attributing its entire failure to this seam. No NVDA/global-shortcut changes were made.

Primary framework evidence: [WPF 10.0.12 AutomationPeer](https://raw.githubusercontent.com/dotnet/wpf/v10.0.12/src/Microsoft.DotNet.Wpf/src/PresentationCore/System/Windows/Automation/Peers/AutomationPeer.cs), especially ProviderFromPeer, ValidateConnected, GetChildren/EnsureChildren and ResetChildrenCache. [Microsoft's provider API](https://learn.microsoft.com/en-us/dotnet/api/system.windows.automation.peers.automationpeer.providerfrompeer?view=windowsdesktop-10.0).

## Original physical journey

The unchanged `CanvasSurfaces_VisualBoardPeersAndZoom_AreClean` physical fact passes on the repaired binaries (1/1, approximately 7 seconds). Its Down/Up, off-board reveal, Left/Right and dimmed-card traversal assertions all remain. It failed before repair in both the complete desktop run and unchanged isolation. This connects the deterministic provider defect to the original physical selection/reveal boundary. Broader Canvas and lifetime validation is still being completed; no human screen-reader acceptance is claimed.

## Broader final verification

All 840 applicable Canvas and command-contract tests passed after the repair, with only the existing named Canvas file-symlink privilege fact excluded locally. The expanded lifetime/callback/cancellation/buffer census passed 21/21 again. All 13 related physical Canvas and palette journeys passed on final binaries, zero skipped, approximately 3m10s. Final source is committed and pushed at 8c8a4c55; the preceding manifest captures its exact repaired sources/binaries. Complete remote CI is running on that revision. No assertion, hardware bound, native inventory, peer identity or virtualization rule was relaxed.

