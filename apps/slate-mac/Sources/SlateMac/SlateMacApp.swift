// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import SwiftUI

extension CancelImportCommandContract {
    /// Typed File-menu counterpart of the registry's `hotkeyHint`.
    /// Keeping the literal in this menu-hosting file also preserves the
    /// existing source-wide menu reachability gate for Command-period.
    static let keyboardShortcut = KeyboardShortcut(
        ".", modifiers: [.command])
}

/// Entry point for the Slate Mac app.
///
/// The single window hosts a `RootView` that picks between the welcome
/// screen and the open-vault split view based on `AppState`. App-level
/// commands replace the default File menu. File ▸ Open is ⌘O = "Quick
/// Open…" (the documents users open all day are notes; #863) — on the
/// welcome screen it falls through to the vault picker, so the chord
/// works globally; "Open Vault…" itself lives on ⇧⌘O.
/// Rounds 31–32: quit fence. Queued sidebar-organization writes and
/// in-flight structural operations are the user's committed intent —
/// normal termination waits for both to settle (bounded at five
/// seconds) instead of killing them mid-flight. Durable cross-launch
/// recovery of writes that already FAILED is tracked in #944.
final class SlateAppDelegate: NSObject, NSApplicationDelegate {
    func applicationShouldTerminate(
        _ sender: NSApplication
    ) -> NSApplication.TerminateReply {
        MainActor.assumeIsolated {
            guard AppState.hasPendingSidebarWorkAtTermination else {
                return .terminateNow
            }
            Task { @MainActor in
                await AppState.settleSidebarWriterChainsForTermination()
                sender.reply(toApplicationShouldTerminate: true)
            }
            return .terminateLater
        }
    }
}

/// FL-07 (#661): View-menu items for the sidebar navigation family.
/// Collapse/Expand act on the tree's loaded state; Back/Forward own the
/// ⌘⌃[ / ⌘⌃] chords (menu-declared so they are reachable regardless of
/// sidebar focus, matching the registry's hints).
struct SidebarNavigationMenuItems: View {
  @EnvironmentObject private var appState: AppState

  private func evaluation(_ id: String) -> SidebarActionEvaluation? {
    appState.sidebarActionProjection(surface: .menuBar).first { $0.id == id }
  }

  private func item(_ id: String) -> some View {
    Group {
      if let evaluation = evaluation(id) {
        Button(evaluation.label) {
          guard let intent = evaluation.intent else { return }
          do {
            _ = try appState.dispatchSidebarAction(intent)
          } catch {
            appState.postMutationAnnouncement(error.sidebarActionAnnouncement)
          }
        }
        .disabled(evaluation.disabledReason != nil)
        .accessibilityHint(evaluation.definition.accessibilityHint)
      }
    }
  }

  var body: some View {
    item(SlateCommandID.sidebarCollapseAll)
    item(SlateCommandID.sidebarExpandLoaded)
    item(SlateCommandID.sidebarHistoryBack)
      .keyboardShortcut("[", modifiers: [.command, .control])
    item(SlateCommandID.sidebarHistoryForward)
      .keyboardShortcut("]", modifiers: [.command, .control])
    // FL-09 (#663): ⌥⌘F — ⌘F stays with content search.
    item(SlateCommandID.sidebarFocusFilter)
      .keyboardShortcut("f", modifiers: [.command, .option])
  }
}

@main
struct SlateMacApp: App {
    @NSApplicationDelegateAdaptor(SlateAppDelegate.self)
    private var appDelegate
    @StateObject private var appState = AppState()

    /// File owns familiar macOS grouping without changing the catalog's stable
    /// Open-first order used by the command palette and registry.
    enum SidebarFileMenuActionGroup: CaseIterable {
        case creation
        case open
        case management
        case organization
        case inspection
        case destructive

        var actionIDs: [String] {
            switch self {
            case .creation:
                return [
                    SlateCommandID.newNote, SlateCommandID.newFolder,
                    SlateCommandID.newFromTemplate,
                    SlateCommandID.importFilesAndFolders,
                ]
            case .open:
                return [SlateCommandID.sidebarOpen]
            case .management:
                return [
                    SlateCommandID.renameEntry, SlateCommandID.moveTo,
                    SlateCommandID.duplicateEntry,
                ]
            case .organization:
                return [
                    SlateCommandID.sidebarPinNote,
                    SlateCommandID.sidebarUnpinNote,
                    SlateCommandID.sidebarUnpinAll,
                    SlateCommandID.sidebarAddShortcut,
                    SlateCommandID.sidebarRemoveShortcut,
                    SlateCommandID.sidebarClearRecents,
                ]
            case .inspection:
                return [
                    SlateCommandID.revealInFinder, SlateCommandID.copyPath,
                    SlateCommandID.sidebarCopyWikilink,
                ]
            case .destructive:
                return [SlateCommandID.deleteEntry]
            }
        }
    }

    static func sidebarFileMenuEvaluations(
        for group: SidebarFileMenuActionGroup,
        from evaluations: [SidebarActionEvaluation]
    ) -> [SidebarActionEvaluation] {
        group.actionIDs.compactMap { id in
            evaluations.first(where: { $0.id == id })
        }
    }

    init() {
        // Install the slate-core diagnostics sink once at startup (#507).
        // slate-core routes its non-fatal warnings through the Rust `log`
        // facade, which is a no-op until a host installs a sink; this is the
        // host's opt-in. `verbose` is DEBUG-only: release builds see
        // warn-level records (which carry no vault paths / note names),
        // while a debug build additionally gets the debug lines that do
        // carry paths, for local troubleshooting. Idempotent on the Rust
        // side, so a second `init()` (e.g. under SwiftUI previews) is safe.
        #if DEBUG
        initHostLogging(verbose: true)
        #else
        initHostLogging(verbose: false)
        #endif

        // Opt out of native window tabbing (windows.md "system windows
        // behave as people expect"): Slate ships its own workspace tab
        // strip, and the system's Show Tab Bar / Merge All Windows
        // items would nest that custom tab UI inside a native tab —
        // two tab metaphors in one window frame. Revisit if a real
        // multi-window story lands (Milestone W parity work).
        NSWindow.allowsAutomaticWindowTabbing = false
    }

    var body: some Scene {
        WindowGroup("Slate") {
            RootView()
                .environmentObject(appState)
                .frame(minWidth: 640, minHeight: 480)
                // #872: reopen the last vault on cold launch (HIG
                // launching.md — "Restore previous state on restart …
                // avoid making people retrace steps"; Obsidian / VS Code
                // reopen the last workspace). Runs once — idempotent via
                // `hasAttemptedLaunchRestore` — and a no-op if a vault is
                // already open. Holding ⌥ at launch, or turning off
                // Settings ▸ General ▸ "Reopen last vault at launch",
                // lands on Welcome instead; a moved/missing last vault
                // falls through the existing not-found flow. Deliberately
                // NOT in AppState.init: constructing an AppState (incl.
                // under XCTest) must never open a vault as a side effect.
                .task { appState.restoreMostRecentVaultOnLaunch() }
        }
        .commands {
            // #880: makes the customizable `toolbar(id: "main")` reachable.
            // On macOS this is what surfaces View ▸ Customize Toolbar… and the
            // Control-click "Customize Toolbar…" editor (and the Show/Hide
            // Toolbar item); without it the per-item `ToolbarItem(id:)`
            // customization has no user-facing entry point.
            ToolbarCommands()

            CommandGroup(replacing: .newItem) {
                let noteSaveDisabledReason = appState.activeNoteSaveDisabledReason
                let sidebarEvaluations = appState.sidebarActionProjection(
                    surface: .menuBar)
                let cancelImport = CancelImportCommandContract.projection(
                    for: appState)

                // File starts with New-family commands, in the familiar macOS
                // order, while every item still owns its live catalog state.
                sidebarFileMenuActions(.creation, evaluations: sidebarEvaluations)

                Button(CancelImportCommandContract.label) {
                    try? CancelImportCommandContract.perform(on: appState)
                }
                .keyboardShortcut(
                    CancelImportCommandContract.keyboardShortcut)
                .disabled(!cancelImport.isEnabled)
                .accessibilityHint(cancelImport.hint)
                .help(cancelImport.hint)

                Divider()

                // ⇧⌘O (#863; was ⌘O): opening a vault is app-level and
                // rare — bare ⌘O now belongs to Quick Open below, the
                // Obsidian-parity quick-switcher chord. The welcome
                // screen's buttons and File ▸ Open Recent keep vault
                // opening one gesture away.
                Button("Open Vault…") {
                    appState.pickAndOpenVault()
                }
                .keyboardShortcut("o", modifiers: [.command, .shift])

                // Standard File ▸ Open Recent submenu (HIG: apps that
                // open documents/folders provide one, with Clear Menu
                // last). Reuses the same store + `openRecent` route as
                // the welcome screen and the utility-bar switcher.
                // Disabled (not hidden) when empty, like the system's.
                Menu("Open Recent") {
                    ForEach(appState.recentVaults) { entry in
                        Button(entry.displayName) {
                            appState.openRecent(entry)
                        }
                        .help(entry.path)
                    }
                    Divider()
                    Button("Clear Menu") {
                        appState.clearRecentVaults()
                    }
                }
                .disabled(appState.recentVaults.isEmpty)

                sidebarFileMenuActions(.open, evaluations: sidebarEvaluations)

                // Quick switcher (#495). ⌘O fuzzy-opens a note by name. With
                // no vault it falls through to the vault picker, so the chord
                // remains useful on the welcome screen.
                Button("Quick Open…") {
                    appState.openQuickSwitcher()
                }
                .keyboardShortcut("o", modifiers: [.command])

                Divider()

                // File ▸ Save (HIG: the File menu carries Save). ⌘S
                // previously lived ONLY on the toolbar button — the
                // same #422 dead-zone as ⌘F: dead with sidebar focus,
                // exactly when a keyboard user finishes tree work and
                // reflexively saves. Enablement mirrors the toolbar
                // button (a no-op Save stays visibly disabled).
                Button("Save") {
                    appState.saveCurrentNote()
                }
                .keyboardShortcut("s", modifiers: .command)
                .disabled(
                    appState.loadedFilePath == nil
                        || appState.isSaving
                        || !appState.activeNoteHasUnsavedChanges
                        || noteSaveDisabledReason != nil
                )
                .accessibilityHint(
                    noteSaveDisabledReason ?? "Save the current note to disk.")
                .help(
                    noteSaveDisabledReason ?? "Save the current note to disk")

                Divider()

                sidebarFileMenuActions(.management, evaluations: sidebarEvaluations)

                Divider()

                // FL-06 (#659): the pin verbs' menu-bar home. Sort lives in
                // the View menu ("Sort Sidebar By"), Finder-style.
                sidebarFileMenuActions(.organization, evaluations: sidebarEvaluations)

                Divider()

                sidebarFileMenuActions(.inspection, evaluations: sidebarEvaluations)

                Divider()

                sidebarFileMenuActions(.destructive, evaluations: sidebarEvaluations)

                Divider()

                // Workspace tab lifecycle (U1-2, #454). Menu items beat the
                // window's implicit ⌘W (performClose:) in AppKit's key-
                // equivalent order, which is exactly the override we want
                // inside a vault; Close Window remains reachable at ⌘⇧W.
                // Disabled (not hidden) without a vault so the shortcuts
                // aren't silent no-ops on the welcome screen.
                // ⌘T (#863): returned to the tab family as Duplicate Tab —
                // Slate's "new tab" verb, since a tab always hosts an item
                // (u1_spec §U1-2: there is no empty-tab page; if one ever
                // lands, it inherits ⌘T).
                Button("Duplicate Tab") {
                    appState.newTab()
                }
                .keyboardShortcut("t", modifiers: [.command])
                .disabled(!appState.isVaultOpen || appState.workspace.activeTab == nil)

                Button("Close Tab") {
                    appState.requestCloseTab()
                }
                .keyboardShortcut("w", modifiers: [.command])
                .disabled(!appState.isVaultOpen || appState.workspace.activeTab == nil)

                // ⇧⌘T (#863): Reopen Closed Tab, the macOS/Obsidian
                // convention next to Close Tab. Pops the per-vault-session
                // closed-tab stack (WorkspaceState.closedTabs) through the
                // standard open funnel — dedup and pane placement honored.
                // Disabled when the stack is empty; `canReopenClosedTab`
                // is AppState's published mirror of that emptiness
                // (predates the #868 objectWillChange bridge, which now
                // also forwards workspace publishes to this menu; the
                // mirror stays as the tested, named Bool surface).
                Button("Reopen Closed Tab") {
                    appState.reopenClosedTab()
                }
                .keyboardShortcut("t", modifiers: [.command, .shift])
                .disabled(!appState.isVaultOpen || !appState.canReopenClosedTab)

                Button("Close Window") {
                    NSApplication.shared.keyWindow?.performClose(nil)
                }
                .keyboardShortcut("w", modifiers: [.command, .shift])

                // Close Vault's menu-bar home (HIG: a Close-family
                // command for the primary open document belongs in
                // File). The utility-bar switcher and the palette
                // command remain; all three route through
                // `closeVaultFromUserAction` (U4-3 #472 funnel). No
                // chord — closing a vault is deliberate, not muscle-
                // memory (same rationale as the palette registration).
                Button("Close Vault") {
                    appState.closeVaultFromUserAction()
                }
                .disabled(!appState.isVaultOpen)
            }

            // File ▸ Print… (#869, printing.md:24 "macOS: File menu > Print").
            // Placed in the `.printItem` group so it REPLACES SwiftUI's default
            // macOS Print / Page Setup — otherwise File would carry TWO Print
            // items and TWO ⌘P claimants (Codex round 1). Prints the current
            // note's rendered reading content via NSPrintOperation (Save-as-PDF
            // for free). Enablement gates on the PUBLISHED `loadedFilePath`
            // (stably disabled with no note open); the action re-guards +
            // announces a nudge. ⇧⌘P stays the Command Palette (Page Setup
            // lives inside the print panel, so it isn't displaced).
            CommandGroup(replacing: .printItem) {
                Button("Print…") {
                    appState.printCurrentNote()
                }
                .keyboardShortcut("p", modifiers: [.command])
                .disabled(appState.loadedFilePath == nil)
            }

            // Tab navigation lives under View alongside the palette/search
            // items — one menu for "where am I looking" commands.
            // #372: ⌘Z / ⇧⌘Z route by focus — the canvas undo stack
            // when a canvas surface owns the tab, the standard responder
            // chain (NSTextView's NSUndoManager) everywhere else. One
            // owner for the chord; the buttons forward faithfully so
            // note-editor undo behaves exactly as before.
            // #867: the titles carry the action name — NSUndoManager's
            // own localized composition on the responder path ("Undo
            // Typing"), the canvas stack's recorded verb when a canvas
            // surface owns the chord ("Undo Create card"). Rule
            // (undo-and-redo.md): "In menu items, use descriptive
            // labels"; the-menu-bar.md: "Undo | Append action name".
            // Focus ROUTING is #372, byte-for-byte unchanged — only
            // title and enablement are new. Re-render pulse:
            // appState.undoMenuTick (the #867 notification pipeline)
            // plus every other appState publish; the titles are
            // computed live at render.
            CommandGroup(replacing: .undoRedo) {
                // #871: ⌘Z / ⇧⌘Z now route across THREE domains, precedence
                // canvas → structural → responder-chain. The order MUST match
                // the title/enablement getters (undoMenuItemTitle etc.) so the
                // render-time title and the press-time action always resolve
                // the SAME domain; `undoTargetsStructural` is defined FALSE
                // whenever `undoTargetsCanvas` is true, making the first two
                // mutually exclusive. Both gates read PUBLISHED state only
                // (never a live firstResponder probe — the #867/#871 desync).
                Button(appState.undoMenuItemTitle) {
                    if appState.undoTargetsCanvas {
                        appState.canvasUndo()
                    } else if appState.undoTargetsStructural {
                        appState.structuralUndo()
                    } else {
                        NSApp.sendAction(Selector(("undo:")), to: nil, from: nil)
                    }
                }
                .keyboardShortcut("z", modifiers: [.command])
                .disabled(!appState.undoMenuItemEnabled)

                Button(appState.redoMenuItemTitle) {
                    if appState.undoTargetsCanvas {
                        appState.canvasRedo()
                    } else if appState.undoTargetsStructural {
                        appState.structuralRedo()
                    } else {
                        NSApp.sendAction(Selector(("redo:")), to: nil, from: nil)
                    }
                }
                .keyboardShortcut("z", modifiers: [.command, .shift])
                .disabled(!appState.redoMenuItemEnabled)
            }

            CommandGroup(after: .toolbar) {
                // U3-2 (#466): the single ⌘⇧E registration — the per-group
                // strip buttons carry the visual affordance without a
                // shortcut (duplicate shortcuts across split panes are
                // undefined in SwiftUI). Menu-bar placement also keeps the
                // chord alive with sidebar focus (the #422 lesson).
                // #868: changeable label (menus.md — "a single item
                // whose label changes with state"; the-menu-bar.md:
                // show/hide titles must reflect current state). The
                // title names the DIRECTION the action will take, read
                // from the ACTIVE tab's mode; re-renders via the #868
                // workspace→appState objectWillChange bridge. No tab
                // reads as .editing → "Enter Reading Mode" (the action
                // no-ops there; enablement scope unchanged from #466).
                // The PALETTE keeps the static "Toggle Reading Mode"
                // noun — searchable and state-free; the menu↔palette
                // registry invariant is CHORD parity, not label parity.
                Button(
                    appState.activeTabIsReading
                        ? "Exit Reading Mode" : "Enter Reading Mode"
                ) {
                    appState.toggleViewMode()
                }
                .keyboardShortcut("e", modifiers: [.command, .shift])
                .disabled(!appState.isVaultOpen)

                // U3-4 (#468): single ⌘⇧D owner, same rationale as ⌘⇧E.
                // #868 changeable label: Show ⇄ Hide from the published
                // mirror of the widget's view-local source-mode @State
                // (appState.propertiesSourceShowing; NotePropertiesHeader
                // remains the single mutator). No note → mirror false →
                // "Show Properties Source". Palette noun stays static,
                // as above.
                Button(
                    appState.propertiesSourceShowing
                        ? "Hide Properties Source" : "Show Properties Source"
                ) {
                    appState.togglePropertiesSourceCommand()
                }
                .keyboardShortcut("d", modifiers: [.command, .shift])
                .disabled(!appState.isVaultOpen)

                Button("Show Next Tab") {
                    appState.selectNextTab()
                }
                .keyboardShortcut("]", modifiers: [.command, .shift])
                .disabled(!appState.isVaultOpen)

                Button("Show Previous Tab") {
                    appState.selectPreviousTab()
                }
                .keyboardShortcut("[", modifiers: [.command, .shift])
                .disabled(!appState.isVaultOpen)

                Button("Move Tab Left") {
                    appState.moveActiveTabLeft()
                }
                .keyboardShortcut(.leftArrow, modifiers: [.control, .command])
                .disabled(!appState.isVaultOpen)

                Button("Move Tab Right") {
                    appState.moveActiveTabRight()
                }
                .keyboardShortcut(.rightArrow, modifiers: [.control, .command])
                .disabled(!appState.isVaultOpen)

                // M-3 (#534): panel-scoped command's menu home (the
                // registry invariant is menu↔palette unification; the
                // workspace-tabs precedent above is the normative home
                // for View-section commands). No hotkey — refresh is a
                // rare, deliberate action.
                // Title-style capitalization (menus.md) — these two were
                // sentence-case among Title-Case siblings.
                Button("Refresh Sync Diagnostics") {
                    appState.refreshSyncDiagnostics()
                }
                .disabled(!appState.isVaultOpen)

                // O-5 (#543): the history panel's menu home — same
                // View-section rule as the sync-diagnostics item above.
                Button("Show History Panel") {
                    appState.showHistoryPanel()
                }
                .disabled(!appState.isVaultOpen)

                // Right-pane hide/reveal (#882, split-views.md:44 — a hideable
                // pane needs a menu command + keyboard shortcut to reveal).
                // #868 changeable label (the-menu-bar.md: show/hide titles must
                // reflect current state); the palette keeps the static "Toggle
                // Right Pane" noun. ⌥⌘I is the single chord owner (menu-bar-
                // homed — survives sidebar focus, the #422 lesson). Re-renders
                // via the isRightPaneVisible publish.
                Button(
                    appState.isRightPaneVisible ? "Hide Right Pane" : "Show Right Pane"
                ) {
                    appState.toggleRightPane()
                }
                .keyboardShortcut("i", modifiers: [.command, .option])
                .disabled(!appState.isVaultOpen)

                Divider()

                // FL-06 (#658): the sort/group command set's menu-bar home,
                // Finder's View ▸ Sort By convention. Radio state reflects
                // the published selection's container; every item dispatches
                // the same catalog command the palette owns.
                SidebarSortMenu()

                // FL-07 (#661): sidebar navigation's menu-bar home. The
                // history chords' single menu owners live here (#422
                // dead-zone rule); ⌃1–9 stay focus-scoped view chords by
                // spec and have no menu items.
                SidebarNavigationMenuItems()

                Divider()

                // ⌘R / ⇧⌘J / ⌘J migrated from toolbar-button
                // registrations (the #422 dead-zone — see the File ▸
                // Save note). The toolbar buttons remain click/AX
                // affordances; each chord's single owner is its menu
                // item here. Labels match the palette registrations.
                // Enablement mirrors the corresponding toolbar button.
                // Verb-first menu labels (menus.md "verb or verb phrase
                // for action items") — the palette keeps the noun forms
                // ("Tasks Review") as its search-friendly names; the
                // registry invariant is chord parity, not label parity.
                // ⌘R (#863; was ⇧⌘T, freed for Reopen Closed Tab): bare
                // ⌘R was unbound, carries no system-wide macOS claim in
                // a non-browser app, and R = Review. Its R-family
                // neighbors are all differently scoped (⌥⌘R Rename,
                // ⇧⌘R Bulk Rename Properties, ⌃⌘R Canvas Resize).
                // #879: this now REVEALS the `Leaf.tasksReview` right-pane
                // leaf (un-hiding the pane) rather than presenting a sheet
                // — same "Show <panel>" verb as Show History Panel above.
                Button("Show Tasks Review") {
                    appState.openTasksReview()
                }
                .keyboardShortcut("r", modifiers: [.command])
                .disabled(appState.currentSession == nil)

                Button("Show Citation Summary") {
                    appState.isCitationSummaryOpen = true
                }
                .keyboardShortcut("j", modifiers: [.command, .shift])
                .disabled(appState.selectedFilePath == nil)

                Button("Jump to Bibliography") {
                    appState.jumpToBibliographyFromExpandedCitation()
                }
                .keyboardShortcut("j", modifiers: .command)
                .disabled(appState.expandedCitation == nil)

                Divider()

                // Milestone T (#518): the ⌃⌘I Where-am-I chord lives in
                // the menu bar (single owner, survives focus changes —
                // the #422 lesson). FOCUS-ROUTED (like the zoom chords):
                // dispatches to the active surface's readback — canvas,
                // graph diagram, or bases — so it isn't a canvas-only no-op
                // on other surfaces (the graph diagram's ⌃⌘I was previously
                // swallowed here). Palette mirrors exist per surface.
                Button("Where Am I?") {
                    appState.routedWhereAmI()
                }
                .keyboardShortcut("i", modifiers: [.control, .command])
                .disabled(!appState.isVaultOpen)

                // #368: ⌥⌘N New Card — canvas-scoped (⌘N stays New
                // Note; the allocation table keeps ⌘N free for notes).
                Button("Canvas: New Card") {
                    appState.canvasNewCard()
                }
                .keyboardShortcut("n", modifiers: [.command, .option])
                .disabled(
                    appState.activeCanvasDocument == nil
                        || appState.activeCanvasMutationDisabledReason != nil)
                .accessibilityHint(
                    appState.activeCanvasMutationDisabledReason
                        ?? "Create a text card next to the selection.")
                .help(
                    appState.activeCanvasMutationDisabledReason
                        ?? "Create a text card next to the selection.")

                Button("Canvas: Move Mode") {
                    appState.canvasEnterMoveMode()
                }
                .keyboardShortcut("g", modifiers: [.control, .command])
                .disabled(
                    appState.activeCanvasDocument == nil
                        || appState.activeCanvasMutationDisabledReason != nil)
                .accessibilityHint(
                    appState.activeCanvasMutationDisabledReason
                        ?? "Move the selected Canvas item with the arrow keys.")
                .help(
                    appState.activeCanvasMutationDisabledReason
                        ?? "Move the selected Canvas item with the arrow keys.")

                Button("Canvas: Resize Mode") {
                    appState.canvasCommitOrEnterResize()
                }
                .keyboardShortcut("r", modifiers: [.control, .command])
                .disabled(
                    appState.activeCanvasDocument == nil
                        || appState.activeCanvasMutationDisabledReason != nil)
                .accessibilityHint(
                    appState.activeCanvasMutationDisabledReason
                        ?? "Resize the selected Canvas item with the arrow keys.")
                .help(
                    appState.activeCanvasMutationDisabledReason
                        ?? "Resize the selected Canvas item with the arrow keys.")

                Button("Canvas: Connect To…") {
                    appState.canvasOpenConnectPicker()
                }
                .keyboardShortcut("c", modifiers: [.control, .command])
                .disabled(
                    appState.activeCanvasDocument == nil
                        || appState.activeCanvasMutationDisabledReason != nil)
                .accessibilityHint(
                    appState.activeCanvasMutationDisabledReason
                        ?? "Choose a Canvas card to connect.")
                .help(
                    appState.activeCanvasMutationDisabledReason
                        ?? "Choose a Canvas card to connect.")

                Button("Canvas: Toggle Mark") {
                    appState.canvasToggleMark()
                }
                .keyboardShortcut("m", modifiers: [.control, .command])
                .disabled(appState.activeCanvasDocument == nil)

                Button("Canvas: Create Connected Card") {
                    appState.canvasCreateConnectedCard()
                }
                .keyboardShortcut("n", modifiers: [.control, .option, .command])
                .disabled(
                    appState.activeCanvasDocument == nil
                        || appState.activeCanvasMutationDisabledReason != nil)
                .accessibilityHint(
                    appState.activeCanvasMutationDisabledReason
                        ?? "Create a new card connected to the selection.")
                .help(
                    appState.activeCanvasMutationDisabledReason
                        ?? "Create a new card connected to the selection.")

                // ⌘= / ⌘- / ⌘0 (#848; formerly the canvas-scoped "Canvas:
                // Zoom …" items, #520): the app's conventional zoom
                // chords, focus-routed EXACTLY like Undo/Redo above —
                // a canvas tab owns them (`canvasZoomIn()` etc., the
                // #520 viewport behavior unchanged); every other surface
                // gets editor text zoom (`editorZoomIn()` etc., a
                // persisted scale over the body-style base size).
                // One menu owner per chord; the registry keeps the
                // canvas-scoped commands as the palette equivalents
                // (canvas program rule R1) with these chords as their
                // hotkeyHints. Enabled whenever a vault is open, so ⌘=
                // is never a silent no-op while note-editing (#848's
                // founding complaint). Still one modifier apart from
                // the ⌥⌘=/⌥⌘- pane-grow chords.
                //
                // ZOOM BOUNDARY (documented decision, #848): editor
                // text zoom scales the monospaced EDITING surfaces —
                // the NSTextView note editor (active and parked panes),
                // the code-blocks panel, the properties-source YAML
                // editor, and the canvas card editor. Reading mode
                // deliberately does NOT zoom: its prose is Dynamic-Type-
                // backed and already tracks the system Text Size
                // (WCAG 1.4.4), so zooming it too would double-scale.
                // Obsidian's zoom is window-wide; Slate trades that
                // parity for not fighting the system setting.
                // Routing order (#848 + P2-3): canvas tab → canvas
                // viewport; graph tab in Diagram mode → graph viewport;
                // else editor text zoom. One menu owner per chord.
                Button("Zoom In") { appState.routedZoomIn() }
                    .keyboardShortcut("=", modifiers: [.command])
                    .disabled(!appState.isVaultOpen)

                Button("Zoom Out") { appState.routedZoomOut() }
                    .keyboardShortcut("-", modifiers: [.command])
                    .disabled(!appState.isVaultOpen)

                Button("Actual Size") { appState.routedActualSize() }
                    .keyboardShortcut("0", modifiers: [.command])
                    .disabled(!appState.isVaultOpen)

                // ⌥⌘0 "Fit Graph" — a new, graph-only chord (spec §P2-3,
                // verified unclaimed). Enabled only when the diagram is the
                // active graph surface.
                Button("Fit Graph") { appState.graphDiagramFit() }
                    .keyboardShortcut("0", modifiers: [.command, .option])
                    .disabled(!appState.graphDiagramZoomActive)

                Divider()

                // ⌘1…⌘9 select tab N (9 = last, macOS convention).
                ForEach(1..<10, id: \.self) { ordinal in
                    Button("Tab \(ordinal == 9 ? "9 (Last)" : String(ordinal))") {
                        appState.selectTab(ordinal: ordinal)
                    }
                    .keyboardShortcut(
                        KeyEquivalent(Character("\(ordinal)")), modifiers: [.command]
                    )
                    .disabled(!appState.isVaultOpen)
                }

                Divider()

                // Split panes (U1-3, #455).
                Button("Split Right") {
                    appState.splitActivePane(axis: .horizontal)
                }
                .keyboardShortcut("\\", modifiers: [.command])
                .disabled(!appState.isVaultOpen)

                Button("Split Down") {
                    appState.splitActivePane(axis: .vertical)
                }
                .keyboardShortcut("\\", modifiers: [.command, .option])
                .disabled(!appState.isVaultOpen)

                Button("Focus Pane Left") {
                    appState.focusPane(.left)
                }
                .keyboardShortcut(.leftArrow, modifiers: [.command, .option])
                .disabled(!appState.isVaultOpen)

                Button("Focus Pane Right") {
                    appState.focusPane(.right)
                }
                .keyboardShortcut(.rightArrow, modifiers: [.command, .option])
                .disabled(!appState.isVaultOpen)

                Button("Focus Pane Above") {
                    appState.focusPane(.up)
                }
                .keyboardShortcut(.upArrow, modifiers: [.command, .option])
                .disabled(!appState.isVaultOpen)

                Button("Focus Pane Below") {
                    appState.focusPane(.down)
                }
                .keyboardShortcut(.downArrow, modifiers: [.command, .option])
                .disabled(!appState.isVaultOpen)

                Button("Grow Pane") {
                    appState.growFocusedPane()
                }
                .keyboardShortcut("=", modifiers: [.command, .option])
                .disabled(!appState.isVaultOpen)

                Button("Shrink Pane") {
                    appState.shrinkFocusedPane()
                }
                .keyboardShortcut("-", modifiers: [.command, .option])
                .disabled(!appState.isVaultOpen)
            }
            // Command palette — Milestone Q #313. The menu item
            // provides both the ⌘⇧P chord and the discoverable
            // "Show Command Palette…" entry. Closing is handled
            // exclusively by Esc inside the sheet (matches Xcode
            // / Sublime / TextMate convention; the alternative —
            // a hidden in-sheet ⌘⇧P button — would rely on SwiftUI
            // shortcut routing through the sheet's responder chain
            // and can't be unit-tested without XCUITest infra).
            //
            // Stays ENABLED on the welcome screen so ⌘⇧P isn't a
            // silent no-op there. The vault-scoped guard lives in
            // `requestCommandPalette()`: it only flips
            // `isCommandPaletteOpen` when a vault is open (no sheet
            // is mounted on the welcome screen, so flipping the bool
            // would re-trigger the palette on the next vault open —
            // the #313/#328 hazard); otherwise it announces "open a
            // vault first" so keyboard/VoiceOver users get feedback.
            CommandGroup(after: .sidebar) {
                Button("Show Command Palette…") {
                    appState.requestCommandPalette()
                }
                .keyboardShortcut("p", modifiers: [.command, .shift])
            }

            // Edit-menu additions. HIG places Find under Edit (Edit ▸
            // Find ▸ Find… ⌘F); the #422 requirement is only that the
            // chord be MENU-BAR-homed (reachable regardless of focus),
            // which any menu satisfies — so the HIG-correct Edit
            // placement costs nothing.
            CommandGroup(after: .textEditing) {
                // #874 (Cory-confirmed 2026-07-12): ⌘F is now
                // find-in-note, reversing the #422 vault-first ⌘F.
                // `requestFindInFocusedSurface()` routes to the focused
                // surface — the note editor's find bar
                // (`NoteEditorView` now sets `usesFindBar = true`), or a
                // focused canvas/base's local filter field (unchanged).
                // searching.md:29 ("support Find-in-window/page for
                // locating content in open documents"). Menu-bar-homed
                // so the chord is reachable regardless of focus (the #422
                // rule). Never inert: editing mode reveals the editor
                // find bar; reading mode / no note falls through the
                // vault-guarded `requestSearchOverlay()` (opens vault
                // search, or announces "open a vault first").
                Button("Find…") {
                    appState.requestFindInFocusedSurface()
                }
                .keyboardShortcut("f", modifiers: [.command])

                // #874: vault-wide search moved from ⌘F to ⇧⌘F (was
                // unclaimed) — the Obsidian / VS Code "search all files"
                // chord. The #422 vault-first identity is preserved, one
                // shifted keystroke away. Vault-scoped guard +
                // announcement live in `requestSearchOverlay()` (the
                // welcome screen has no overlay host).
                Button("Search Vault…") {
                    appState.requestSearchOverlay()
                }
                .keyboardShortcut("f", modifiers: [.command, .shift])

                Divider()

                // ⇧⌘R migrated from NotePropertiesHeader's hidden
                // opacity-0 button (the #422 dead-zone pattern, plus
                // the chord silently vanished whenever the properties
                // header wasn't mounted). A vault-wide bulk edit is an
                // Edit-menu verb; label matches the palette
                // registration.
                Button("Bulk Rename Properties…") {
                    appState.isBulkRenameSheetOpen = true
                }
                .keyboardShortcut("r", modifiers: [.command, .shift])
                .disabled(appState.currentSession == nil)

                Divider()

                // #855: opt-in live spell checking. A menu-hosted
                // Toggle renders as a checkmark item; the pref is
                // `@Published` on appState, so the checkmark re-renders
                // live. No chord — Edit menu + palette
                // (`slate.editor.toggleSpellCheck`) only. Default OFF:
                // Markdown source red-squiggles fences/wikilinks/keys,
                // so prose writers opt in. Applied live through
                // `NoteEditorView.updateNSView`; the YAML
                // properties-source editor (`PlainTextEditor`) stays
                // always-off — structured text is never prose.
                Toggle(
                    "Check Spelling While Typing",
                    isOn: Binding(
                        get: { appState.editorSpellCheckEnabled },
                        set: { _ in appState.toggleEditorSpellCheck() }
                    )
                )
            }

            // Help ▸ Slate Help (HIG: the Help menu's first item is
            // "<AppName> Help"). Routes through the same `openHelp`
            // the palette and utility bar use. `replacing: .help`
            // rather than `after:` — replacing swaps only the ITEM
            // group; the system menu-command search field is AppKit-
            // injected into the app's help menu at display time and
            // SURVIVES the replacement (measured via AX on the
            // running app: AXTextField "search text field" + results
            // table render above "Slate Help"). `after:` would keep
            // SwiftUI's dead default "<App> Help" stub next to ours.
            CommandGroup(replacing: .help) {
                Button("Slate Help") {
                    appState.openHelp()
                }
            }
        }

        // Settings scene (#224) — Cmd+, opens it from anywhere.
        // SwiftUI auto-installs the "Slate ▸ Settings…" menu item
        // and the keyboard shortcut when the App declares a
        // `Settings` scene.
        Settings {
            SettingsView()
                .environmentObject(appState)
        }
    }

    /// File-menu-owned key equivalents for the four established Sidebar
    /// chords. Every other catalog action deliberately returns nil so the menu
    /// is the single, deterministic shortcut owner.
    private static func sidebarMenuKeyboardShortcut(
        for id: String
    ) -> KeyboardShortcut? {
        switch id {
        case SlateCommandID.newNote:
            return KeyboardShortcut("n", modifiers: [.command])
        case SlateCommandID.newFromTemplate:
            return KeyboardShortcut("n", modifiers: [.command, .shift])
        case SlateCommandID.renameEntry:
            return KeyboardShortcut("r", modifiers: [.command, .option])
        case SlateCommandID.moveTo:
            return KeyboardShortcut("m", modifiers: [.command, .shift])
        default:
            return nil
        }
    }

    /// One renderer preserves the shared labels, disabled reasons, frozen
    /// dispatch, destructive role, and four established shortcuts in every
    /// File-owned catalog group.
    @ViewBuilder
    private func sidebarFileMenuActions(
        _ group: SidebarFileMenuActionGroup,
        evaluations: [SidebarActionEvaluation]
    ) -> some View {
        ForEach(
            Self.sidebarFileMenuEvaluations(for: group, from: evaluations),
            id: \.id
        ) { evaluation in
            Button(
                evaluation.definition.label,
                role: evaluation.definition.isDestructive ? .destructive : nil
            ) {
                guard let intent = evaluation.intent else { return }
                do {
                    _ = try appState.dispatchSidebarAction(intent)
                } catch {
                    appState.postMutationAnnouncement(
                        error.sidebarActionAnnouncement)
                }
            }
            .disabled(evaluation.disabledReason != nil)
            .accessibilityHint(
                evaluation.disabledReason
                    ?? evaluation.definition.accessibilityHint)
            .help(
                evaluation.disabledReason
                    ?? evaluation.definition.accessibilityHint)
            .keyboardShortcut(
                Self.sidebarMenuKeyboardShortcut(for: evaluation.id))
        }
    }
}

/// Top-level router: welcome screen until a vault is open, then the
/// split view. Lives next to the App entry point so the routing logic
/// is visible at a glance.
struct RootView: View {
    @EnvironmentObject private var appState: AppState

    var body: some View {
        Group {
            if appState.isVaultOpen {
                MainSplitView()
            } else {
                WelcomeView()
            }
        }
        .background {
            SidebarTemplateShortcutMonitor(
                attachedSheetOwnsTemplateAction: { sheet in
                    appState.isTemplatePickerOpen
                        || appState.pendingTemplateFlow != .idle
                        || appState.templateShortcutWindowOwnsActionLauncher(sheet)
                },
                hostWindowChanged: {
                    appState.setTemplateShortcutHostWindow($0)
                },
                rejectBlockedWindow: { context in
                    switch context {
                    case .attachedDialog:
                        return appState.rejectTemplateShortcutForActiveDialog()
                    case .otherWindow:
                        return appState.rejectTemplateShortcutForOtherWindow()
                    }
                },
                clearBlockedNotice: {
                    appState.clearTemplateShortcutDialogNotice()
                },
                invoke: {
                    appState.invokeSidebarKeyboardAction(SlateCommandID.newFromTemplate)
                })
            .frame(width: 0, height: 0)
            .accessibilityHidden(true)
        }
        .onReceive(
            NotificationCenter.default.publisher(
                for: NSApplication.didBecomeActiveNotification)
        ) { _ in
            appState.scheduleTemplateAvailabilityRefresh()
        }
        .onReceive(
            NotificationCenter.default.publisher(
                for: NSWindow.didBecomeKeyNotification)
        ) { _ in
            appState.templateShortcutWindowAdmissionDidChange()
        }
    }
}

/// The File menu keeps the visible ⇧⌘N key equivalent even while disabled.
/// A local key-down monitor runs before menu dispatch, consumes that one chord,
/// and routes it through the catalog's `.keyboard` projection. Consuming the
/// event prevents the enabled menu item from dispatching a second time.
private struct SidebarTemplateShortcutMonitor: NSViewRepresentable {
    let attachedSheetOwnsTemplateAction: (NSWindow?) -> Bool
    let hostWindowChanged: (NSWindow?) -> Void
    let rejectBlockedWindow: (SidebarTemplateShortcutRouting.BlockContext) -> Bool
    let clearBlockedNotice: () -> Void
    let invoke: () -> Bool

    func makeNSView(context: Context) -> MonitorView {
        let view = MonitorView()
        view.attachedSheetOwnsTemplateAction = attachedSheetOwnsTemplateAction
        view.hostWindowChanged = hostWindowChanged
        view.rejectBlockedWindow = rejectBlockedWindow
        view.clearBlockedNotice = clearBlockedNotice
        view.invoke = invoke
        return view
    }

    func updateNSView(_ view: MonitorView, context: Context) {
        view.attachedSheetOwnsTemplateAction = attachedSheetOwnsTemplateAction
        view.hostWindowChanged = hostWindowChanged
        view.rejectBlockedWindow = rejectBlockedWindow
        view.clearBlockedNotice = clearBlockedNotice
        view.invoke = invoke
    }

    static func dismantleNSView(_ view: MonitorView, coordinator: ()) {
        view.hostWindowChanged(nil)
        view.stopMonitoring()
    }

    final class MonitorView: NSView {
        var attachedSheetOwnsTemplateAction: (NSWindow?) -> Bool = { _ in false }
        var hostWindowChanged: (NSWindow?) -> Void = { _ in }
        var rejectBlockedWindow: (SidebarTemplateShortcutRouting.BlockContext) -> Bool = {
            _ in true
        }
        var clearBlockedNotice: () -> Void = {}
        var invoke: () -> Bool = { false }
        private var monitor: Any?

        override var intrinsicContentSize: NSSize { .zero }

        override func viewDidMoveToWindow() {
            super.viewDidMoveToWindow()
            hostWindowChanged(window)
            window == nil ? stopMonitoring() : startMonitoringIfNeeded()
        }

        func stopMonitoring() {
            guard let monitor else { return }
            NSEvent.removeMonitor(monitor)
            self.monitor = nil
        }

        private func startMonitoringIfNeeded() {
            guard monitor == nil else { return }
            monitor = NSEvent.addLocalMonitorForEvents(matching: .keyDown) {
                [weak self] event in
                guard let self else { return event }
                let attachedSheet = self.window?.attachedSheet
                if attachedSheet == nil {
                    self.clearBlockedNotice()
                }
                let eventWindow = event.window
                let hasMarkedText = (eventWindow?.firstResponder as? NSTextView)?
                    .hasMarkedText() == true
                let handled = SidebarTemplateShortcutRouting.route(
                    applicationIsActive: NSApp.isActive,
                    hostWindow: self.window,
                    hostAttachedSheet: attachedSheet,
                    modalWindow: NSApplication.shared.modalWindow,
                    attachedSheetOwnsTemplateAction:
                        self.attachedSheetOwnsTemplateAction(attachedSheet),
                    eventWindow: eventWindow,
                    keyWindow: NSApp.keyWindow,
                    hasMarkedText: hasMarkedText,
                    isRepeat: event.isARepeat,
                    charactersIgnoringModifiers: event.charactersIgnoringModifiers,
                    modifierFlags: event.modifierFlags,
                    rejectBlockedWindow: { context in
                        if let eventWindow {
                            self.presentDialogNotice(
                                context.reason,
                                in: eventWindow)
                        }
                        return self.rejectBlockedWindow(context)
                    },
                    invoke: self.invoke)
                return handled ? nil : event
            }
        }

        /// Put feedback in reserved window chrome. A content overlay can hide
        /// a focused field or dialog control; a titlebar accessory adds its
        /// own layout row and remains until the owning window is dismissed.
        private func presentDialogNotice(_ message: String, in window: NSWindow) {
            let identifier = NSUserInterfaceItemIdentifier(
                "slate.template-shortcut-dialog-notice")
            window.titlebarAccessoryViewControllers.removeAll {
                $0.view.identifier == identifier
            }

            let notice = DialogNoticeView()
            notice.identifier = identifier
            notice.frame = NSRect(
                x: 0, y: 0, width: max(window.frame.width, 320), height: 40)
            notice.autoresizingMask = [.width]
            notice.material = .headerView
            notice.blendingMode = .withinWindow
            notice.state = .active

            let label = NSTextField(wrappingLabelWithString: message)
            label.alignment = .center
            label.font = .systemFont(ofSize: NSFont.systemFontSize)
            label.maximumNumberOfLines = 2
            label.translatesAutoresizingMaskIntoConstraints = false
            notice.addSubview(label)

            NSLayoutConstraint.activate([
                label.leadingAnchor.constraint(equalTo: notice.leadingAnchor, constant: 12),
                label.trailingAnchor.constraint(equalTo: notice.trailingAnchor, constant: -12),
                label.centerYAnchor.constraint(equalTo: notice.centerYAnchor),
            ])
            label.setAccessibilityLabel(message)

            let controller = NSTitlebarAccessoryViewController()
            controller.layoutAttribute = .bottom
            controller.view = notice
            window.addTitlebarAccessoryViewController(controller)
        }

        /// The status must remain perceivable without intercepting controls in
        /// the dialog it explains.
        private final class DialogNoticeView: NSVisualEffectView {
            override func hitTest(_ point: NSPoint) -> NSView? { nil }
        }

        deinit {
            if let monitor {
                NSEvent.removeMonitor(monitor)
            }
        }
    }
}

/// Pure routing decision kept separate from monitor lifetime so wrong-window,
/// non-key-window, marked-text, repeat, and modifier behavior are testable.
@MainActor
enum SidebarTemplateShortcutRouting {
    enum BlockContext {
        case attachedDialog
        case otherWindow

        @MainActor var reason: String {
            switch self {
            case .attachedDialog:
                return AppState.templateDialogBusyReason
            case .otherWindow:
                return AppState.templateOtherWindowReason
            }
        }
    }

    static func route(
        applicationIsActive: Bool,
        hostWindow: NSWindow?,
        hostAttachedSheet: NSWindow?,
        modalWindow: NSWindow?,
        attachedSheetOwnsTemplateAction: Bool,
        eventWindow: NSWindow?,
        keyWindow: NSWindow?,
        hasMarkedText: Bool,
        isRepeat: Bool,
        charactersIgnoringModifiers: String?,
        modifierFlags: NSEvent.ModifierFlags,
        rejectBlockedWindow: (BlockContext) -> Bool,
        invoke: () -> Bool
    ) -> Bool {
        guard applicationIsActive,
            let eventWindow,
            keyWindow === eventWindow,
            !hasMarkedText
        else { return false }

        let allowedNoise: NSEvent.ModifierFlags = [.capsLock, .function, .numericPad]
        let modifiers = modifierFlags
            .intersection(.deviceIndependentFlagsMask)
            .subtracting(allowedNoise)
        guard modifiers == [.command, .shift],
            charactersIgnoringModifiers?.lowercased() == "n"
        else { return false }
        // Repeats are consumed but never invoked or allowed to fall through to
        // the File-menu key equivalent.
        if isRepeat { return true }
        let eventBelongsToHost = eventWindow === hostWindow
            || eventWindow === hostAttachedSheet
        if !eventBelongsToHost {
            return rejectBlockedWindow(
                eventWindow === modalWindow ? .attachedDialog : .otherWindow)
        }
        if eventWindow === hostAttachedSheet, !attachedSheetOwnsTemplateAction {
            return rejectBlockedWindow(.attachedDialog)
        }
        return invoke()
    }
}
