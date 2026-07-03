// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

import AppKit
import SwiftUI

/// The bottom-left utility rail (Milestone U4-3, #472): a fixed-height row of
/// icon buttons pinned at the sidebar's bottom edge, below the file tree. Three
/// controls, each a labeled glyph in a comfortable hit target:
///
///  - **Settings** — sends `showSettingsWindow:`, the exact selector the
///    `slate.settings.open` command action sends, so the ⌘, menu chord and this
///    button are two entry points onto one implementation.
///  - **Help** — hands the repository README URL to `AppState.openHelp()`, which
///    routes through the injected `externalOpener` (gap G13); also registered as
///    `slate.help.open`.
///  - **Vault switcher** — a `Menu` of recent vaults (the current one
///    checkmarked + disabled), plus "Open Other Vault…" and "Close Vault". A
///    recent switch closes the current vault through the dirty gate first, so
///    cancelling the "Save changes?" prompt cancels the switch.
///
/// Sizing follows the U4-1 rail idiom: a Dynamic-Type-scaling glyph
/// (`.font(.title2)` + `.imageScale(.large)`, ~28pt at the default size — never a
/// frozen `.system(size:)`, which the a11y gate rejects) in a ≥ 36×32 target.
/// The whole bar is one AX container labeled "Vault utilities"; every button is
/// labeled and carries a `.help` tooltip; the `Menu` is keyboard-operable.
struct SidebarUtilityBar: View {
    @EnvironmentObject private var appState: AppState

    /// Row height (spec §U4-3). The glyph targets are 32pt tall inside it, so
    /// the 36pt row gives a 2pt breathing margin top and bottom.
    private static let barHeight: CGFloat = 36

    var body: some View {
        VStack(spacing: 0) {
            // Hairline above the bar, separating it from the tree / panels
            // above (Tokens separator, same as the tree↔panel divider).
            Divider()
            HStack(spacing: Tokens.Spacing.sm) {
                settingsButton
                helpButton
                vaultSwitcherMenu
                Spacer(minLength: 0)
            }
            .padding(.horizontal, Tokens.Spacing.sm)
            .frame(height: Self.barHeight)
        }
        .frame(maxWidth: .infinity)
        .background(Tokens.ColorRole.surface)
        // One AX container for the whole bar so VoiceOver announces a named
        // region ("Vault utilities") wrapping the three controls, each of
        // which keeps its own label/trait.
        .accessibilityElement(children: .contain)
        .accessibilityLabel("Vault utilities")
    }

    // MARK: - Settings

    private var settingsButton: some View {
        Button {
            // Send the SAME selector the `slate.settings.open` command action
            // sends (SlateCommands.swift): SwiftUI's `Settings { }` scene
            // installs a `showSettingsWindow:` responder + the ⌘, chord. This
            // button and that menu chord are two entry points onto one
            // implementation. `NSApplication.shared` (not the `NSApp` global)
            // forces the singleton's lazy creation so the send resolves in both
            // production and the test runner — the exact rationale documented
            // at the command registration.
            NSApplication.shared.sendAction(
                Selector(("showSettingsWindow:")),
                to: nil,
                from: nil
            )
        } label: {
            utilityGlyph(.settings)
        }
        .buttonStyle(.plain)
        .accessibilityLabel("Settings")
        .accessibilityHint("Opens the Settings window.")
        .help("Settings (⌘,)")
    }

    // MARK: - Help

    private var helpButton: some View {
        Button {
            appState.openHelp()
        } label: {
            utilityGlyph(.help)
        }
        .buttonStyle(.plain)
        .accessibilityLabel("Help")
        .accessibilityHint("Opens the project README in your default browser.")
        .help("Help")
    }

    // MARK: - Vault switcher

    private var vaultSwitcherMenu: some View {
        Menu {
            // Recent vaults: the current one is checkmarked and disabled (it's
            // already open); the rest switch to that vault through the dirty
            // gate. A leading `checkmark` glyph is the shape cue (never color
            // alone); the label text carries the visible name for Voice Control.
            ForEach(appState.recentVaults) { entry in
                let isCurrent = entry.path == appState.currentVaultURL?.path
                Button {
                    appState.switchToRecent(entry)
                } label: {
                    if isCurrent {
                        // Leading checkmark (through the SlateSymbol layer — no
                        // raw glyph): the shape cue for the open vault. The
                        // Label's text (the vault name) is the accessible name;
                        // the glyph rides along decoratively.
                        SlateSymbol.checkmark.label(entry.displayName)
                    } else {
                        Text(entry.displayName)
                    }
                }
                .disabled(isCurrent)
            }
            if !appState.recentVaults.isEmpty {
                Divider()
            }
            Button("Open Other Vault…") {
                appState.pickAndOpenVault()
            }
            Button("Close Vault") {
                // The dirty-gate + announcement flow, unchanged — shared with
                // the File-menu / palette `slate.vault.close` registration.
                appState.closeVaultFromUserAction()
            }
        } label: {
            utilityGlyph(.vaultSwitch)
        }
        // Borderless button menu (the TabBarView all-tabs precedent) so the
        // trigger reads as a plain icon control, not a bordered popup button.
        .menuStyle(.borderlessButton)
        // No disclosure chevron — the bar is a tight icon row; the glyph +
        // tooltip name the control.
        .menuIndicator(.hidden)
        .frame(width: 40, height: 32)
        .accessibilityLabel("Switch vault")
        .accessibilityHint("Opens a menu of recent vaults and vault actions.")
        .help("Switch vault")
    }

    // MARK: - Shared glyph

    /// A utility-bar glyph: the U4-1 rail sizing idiom (semantic style +
    /// `.imageScale(.large)` ≈ 28pt, Dynamic-Type-scaling) in a ≥ 36×32 target,
    /// tinted `textSecondary` (rest) on the bar's `surface`. `image(label:)`
    /// keeps the glyph labeled at the SlateSymbol layer; the call sites above
    /// also set an explicit `.accessibilityLabel` matching the visible tooltip
    /// (Label-in-Name), and for the `Menu` the container's own label wins.
    private func utilityGlyph(_ symbol: SlateSymbol) -> some View {
        symbol.image(label: symbol.title)
            .font(.title2)
            .imageScale(.large)
            .frame(width: 40, height: 32)
            .foregroundStyle(Tokens.ColorRole.textSecondary)
            .contentShape(Rectangle())
    }
}
