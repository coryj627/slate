import SwiftUI

/// Split view shown once a vault is open.
///
/// Three-column layout: file list (#11) | note content (#45) | outline (#46).
/// `NavigationSplitView` with the `(sidebar, content, detail)` initializer
/// gives us system-styled column dividers, keyboard navigation between
/// columns (Cmd+1/2/3 via the System menu), and per-column collapse/
/// resize behaviour for free on macOS.
struct MainSplitView: View {
    @EnvironmentObject private var appState: AppState

    /// Focus-return target for the two `.alert` modifiers below.
    /// Each alert button action assigns this so AppKit /
    /// VoiceOver routes keyboard + AX focus back to the editor
    /// after the alert dismisses — WCAG 2.4.3 (Focus Order) +
    /// 2.1.2 (No Keyboard Trap). The actual NSTextView focus
    /// transition is handled by AppKit's responder chain; the
    /// SwiftUI `@AccessibilityFocusState` here exists so the
    /// alert-dismiss path has an explicit "this is the
    /// destination" signal the linter and AX engine can verify.
    @AccessibilityFocusState private var alertFocusReturn: AlertFocusTarget?

    /// Single-case enum for the focus-state binding. SwiftUI's
    /// AccessibilityFocusState needs a typed value to bind
    /// `.accessibilityFocused(_:equals:)` against.
    enum AlertFocusTarget: Hashable {
        case editor
    }

    var body: some View {
        // Each column gets its own .accessibilityLabel so VoiceOver
        // announces meaningful names when the user navigates between
        // panes (Cmd+Opt+arrow on macOS) instead of falling back to the
        // mangled NSHostingView type names that the Accessibility
        // Inspector flagged with "Element has no description".
        NavigationSplitView {
            FileListSidebar()
                .accessibilityLabel("Files sidebar")
        } content: {
            NoteContentView()
                .accessibilityLabel("Note content pane")
                .accessibilityFocused($alertFocusReturn, equals: .editor)
        } detail: {
            OutlineSidebar()
                .accessibilityLabel("Outline sidebar")
        }
        .navigationTitle(vaultTitle)
        .toolbar {
            // Save status indicator: a Modified/Saved label that
            // VoiceOver reads when the toolbar focus lands on it.
            // Hidden when no note is loaded so the toolbar doesn't
            // claim "Saved" against an empty editor.
            if appState.loadedFilePath != nil {
                ToolbarItem(placement: .automatic) {
                    Text(appState.hasUnsavedChanges ? "Modified" : "Saved")
                        .font(.callout)
                        .foregroundStyle(.secondary)
                        .accessibilityLabel(
                            appState.hasUnsavedChanges
                                ? "Modified. Unsaved changes in the editor."
                                : "Saved. Editor matches the on-disk file."
                        )
                }
            }
            ToolbarItem(placement: .automatic) {
                Button {
                    appState.saveCurrentNote()
                } label: {
                    Label("Save", systemImage: "square.and.arrow.down")
                }
                .keyboardShortcut("s", modifiers: .command)
                .disabled(
                    appState.loadedFilePath == nil
                        || appState.isSaving
                        || !appState.hasUnsavedChanges
                )
                .accessibilityHint(
                    "Save the current note to disk. Cmd+S."
                )
            }
            ToolbarItem(placement: .automatic) {
                Button {
                    appState.toggleSearchOverlay()
                } label: {
                    Label("Search", systemImage: "magnifyingglass")
                }
                .keyboardShortcut("f", modifiers: .command)
                .accessibilityHint(
                    "Opens the search overlay. Cmd+F to toggle, Esc to close."
                )
            }
            ToolbarItem(placement: .automatic) {
                Button {
                    appState.openTemplatePicker()
                } label: {
                    Label("New from Template", systemImage: "doc.badge.plus")
                }
                .keyboardShortcut("n", modifiers: [.command, .shift])
                .accessibilityHint(
                    "Opens the template picker. Cmd+Shift+N. Esc closes."
                )
            }
            ToolbarItem(placement: .automatic) {
                Button {
                    appState.openTasksReview()
                } label: {
                    Label("Tasks Review", systemImage: "checklist")
                }
                .keyboardShortcut("t", modifiers: [.command, .shift])
                .disabled(appState.currentSession == nil)
                .accessibilityHint(
                    "Opens the vault-wide tasks review. Cmd+Shift+T. Esc closes."
                )
            }
            ToolbarItem(placement: .automatic) {
                Button("Close Vault") {
                    // Route through attemptCloseVault so the
                    // "Save changes?" prompt fires when the editor
                    // is dirty. The announcement moves to the
                    // applyPendingNavigation tail when navigation
                    // actually succeeds — closeVault() is still
                    // called there.
                    if appState.hasUnsavedChanges {
                        appState.attemptCloseVault()
                    } else {
                        appState.closeVault()
                        postAccessibilityAnnouncement(
                            "Vault closed. Returned to the welcome screen."
                        )
                    }
                }
                .accessibilityHint(
                    "Returns to the welcome screen. Prompts to save if the editor has unsaved changes."
                )
            }
        }
        .overlay(alignment: .top) {
            if appState.isSearchOpen {
                SearchOverlay()
                    // Transition stays subtle; honors Reduce Motion
                    // through SwiftUI's automatic crossfade fallback
                    // when the user has it enabled.
                    .transition(.move(edge: .top).combined(with: .opacity))
            }
        }
        // WriteConflict resolution alert (#64). Three buttons:
        //
        //  - Keep mine — default-styled (visually primary) so the
        //    sighted user can hit Return when they're confident
        //    their version is the one to ship. VoiceOver users
        //    arrow to it explicitly.
        //  - Reload from disk — `.destructive` because it drops
        //    the in-editor buffer.
        //  - Cancel — `.cancel` so Escape (and accidental keyboard
        //    dismissal) leaves the unsaved buffer intact and the
        //    user can think about it. Per issue #64: "accidental
        //    Return doesn't lose data" — Cancel as the cancel-role
        //    means the platform routes keyboard dismissal here.
        .alert(
            "File changed externally",
            isPresented: writeConflictPresented,
            presenting: appState.currentSaveConflict
        ) { conflict in
            Button("Keep mine") {
                appState.resolveSaveConflictKeepMine()
                alertFocusReturn = .editor
            }
            .accessibilityHint(
                "Save your version, overwriting the external change."
            )
            Button("Reload from disk", role: .destructive) {
                appState.resolveSaveConflictReloadFromDisk()
                alertFocusReturn = .editor
            }
            .accessibilityHint(
                "Discard your unsaved edits and reload the on-disk version."
            )
            Button("Cancel", role: .cancel) {
                appState.resolveSaveConflictCancel()
                alertFocusReturn = .editor
            }
            .accessibilityHint(
                "Close this dialog. Your unsaved changes stay in the editor."
            )
        } message: { conflict in
            Text(
                "\(filename(of: conflict.path)) was modified outside the editor since you opened it. Choose how to resolve the conflict."
            )
        }
        // Save-changes prompt for close-vault / file-switch while
        // dirty (#63 + #64). Save is the default action; Cancel is
        // the cancel-role so platform keyboard dismissal leaves
        // the dirty buffer intact rather than dropping it.
        .alert(
            "Save changes?",
            isPresented: pendingNavigationPresented,
            presenting: appState.pendingNavigation
        ) { _ in
            Button("Save") {
                appState.resolvePendingNavigationSave()
                alertFocusReturn = .editor
            }
            .accessibilityHint(
                "Save the current note, then continue with the requested navigation."
            )
            Button("Discard", role: .destructive) {
                appState.resolvePendingNavigationDiscard()
                alertFocusReturn = .editor
            }
            .accessibilityHint(
                "Throw away unsaved changes and continue."
            )
            Button("Cancel", role: .cancel) {
                appState.resolvePendingNavigationCancel()
                alertFocusReturn = .editor
            }
            .accessibilityHint(
                "Stay on the current note. Unsaved changes remain in the editor."
            )
        } message: { _ in
            let name = filename(
                of: appState.loadedFilePath ?? ""
            )
            Text(
                "Save changes to \(name)?"
            )
        }
        .sheet(isPresented: $appState.isTemplatePickerOpen) {
            TemplatePicker()
                .environmentObject(appState)
        }
        .sheet(isPresented: templateFlowSheetPresented) {
            TemplatePromptSheet()
                .environmentObject(appState)
        }
        .sheet(isPresented: $appState.isTasksReviewOpen) {
            TasksReviewView()
                .environmentObject(appState)
        }
        .onAppear {
            postAccessibilityAnnouncement(
                "Vault \(vaultTitle) opened. Scanning files for the sidebar."
            )
        }
    }

    /// Binding driving the `TemplatePromptSheet` sheet. True
    /// whenever `pendingTemplateFlow` is in a non-idle state
    /// (either prompts or name step). The setter resets the flow
    /// when SwiftUI fires a dismissal from outside (e.g. a swipe-
    /// down on iPad — defensive against future platform parity).
    private var templateFlowSheetPresented: Binding<Bool> {
        Binding(
            get: { appState.pendingTemplateFlow != .idle },
            set: { presented in
                if !presented {
                    appState.cancelTemplateFlow()
                }
            }
        )
    }

    private var writeConflictPresented: Binding<Bool> {
        Binding(
            get: { appState.currentSaveConflict != nil },
            set: { presented in
                if !presented {
                    appState.currentSaveConflict = nil
                }
            }
        )
    }

    private var pendingNavigationPresented: Binding<Bool> {
        Binding(
            get: { appState.pendingNavigation != nil },
            set: { presented in
                if !presented {
                    appState.pendingNavigation = nil
                }
            }
        )
    }

    private func filename(of path: String) -> String {
        (path as NSString).lastPathComponent
    }

    private var vaultTitle: String {
        appState.currentVaultURL?.lastPathComponent ?? "Vault"
    }
}
