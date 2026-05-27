// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! Command palette infrastructure (Milestone Q).
//!
//! A composable registry of named, invocable actions. The Mac
//! command palette (issue #313) reads from this; the menu bridge
//! (issue #314) wires every existing menu item into it; future
//! Tier-1 plugins (V1.x, `docs/plans/05_locked_architecture_decisions.md`
//! §10, lines 1587 & 1623) extend it.
//!
//! ## Design
//!
//! - [`CommandRegistry`] is `Send + Sync`; cheap to wrap in an `Arc`
//!   and share across threads.
//! - Commands carry pure metadata in [`Command`]; the action handle
//!   ([`CommandAction`]) lives behind a trait object inside the
//!   registry so the metadata can round-trip through the FFI
//!   independently.
//! - [`CommandRegistry::list`] returns commands sorted by section
//!   then id so the palette renders deterministically and tests
//!   don't get flake from `HashMap` iteration order.
//! - [`CommandRegistry::invoke_by_id`] clones the action `Arc` out
//!   of the read lock before calling `invoke`. This matters: an
//!   action that registers, lists, or invokes another command must
//!   not deadlock against its own read guard. Tested.

use std::collections::HashMap;
use std::sync::{Arc, RwLock};
use thiserror::Error;

/// Top-level grouping for commands. Mirrors the menu sections in
/// the Mac app (File / Edit / View / Vault) plus a few that span
/// editor and tasks surfaces. Adding a section is a deliberate
/// edit — the palette renders these in declaration order.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash)]
#[repr(u8)]
pub enum CommandSection {
    File = 0,
    Navigation = 1,
    View = 2,
    Vault = 3,
    Editor = 4,
    Tasks = 5,
    Settings = 6,
    Plugins = 7,
}

/// Metadata for a registered command. The action itself lives in
/// the registry; `Command` is the bit the palette renders.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct Command {
    /// Stable identifier, e.g. `"slate.file.openVault"`. Keybindings
    /// and recents persist by this id, so changing it is a breaking
    /// change for users.
    pub id: String,
    /// Human-visible label shown in the palette result list.
    ///
    /// **L10n:** Plain en-US `String` in V1; localization indirection
    /// lands in V2 per issue #264. Callers shouldn't wrap this with
    /// `LocalizedStringKey` ad-hoc — that's the V2 transition's job
    /// once the rest of the app's user-facing strings move under
    /// one l10n scheme.
    pub label: String,
    /// Optional VoiceOver hint. Falls back to `label` when `None`.
    pub accessibility_hint: Option<String>,
    /// Optional hotkey hint string for display next to the label
    /// (e.g. `"⌘N"`). The actual chord registration lives on the
    /// foreign side.
    pub hotkey_hint: Option<String>,
    /// Grouping for the palette's section view.
    pub section: CommandSection,
}

/// Invocable action attached to a registered command. The registry
/// holds these behind `Arc` so [`CommandRegistry::invoke_by_id`]
/// can clone the handle out of the read lock before running.
///
/// `Send + Sync` so the registry stays cheap to share across
/// threads. Implementations should be careful about reentrant
/// invokes — see [`CommandRegistry`] for the locking story.
pub trait CommandAction: Send + Sync {
    fn invoke(&self) -> Result<(), CommandError>;
}

/// Errors produced by the command registry.
#[derive(Debug, Error, PartialEq, Eq)]
pub enum CommandError {
    /// `invoke_by_id` was called with an id no command is
    /// registered under.
    #[error("unknown command id: {0}")]
    UnknownId(String),
    /// The action's `invoke` returned an error. The wrapped string
    /// is the action-specific failure message — opaque to the
    /// registry.
    #[error("command action failed: {0}")]
    ActionFailed(String),
}

/// Composable registry of commands. Cheap to wrap in `Arc` and
/// share. See module docs for the locking guarantees.
pub struct CommandRegistry {
    commands: RwLock<HashMap<String, Entry>>,
}

struct Entry {
    metadata: Command,
    action: Arc<dyn CommandAction>,
}

impl CommandRegistry {
    pub fn new() -> Self {
        Self {
            commands: RwLock::new(HashMap::new()),
        }
    }

    /// Register a command. Returns `true` if the call replaced an
    /// existing entry with the same id, `false` if this is the
    /// first time the id was seen.
    ///
    /// Replace-semantics are deliberate — they let plugin hot-
    /// reload (V1.x) update a registered command without
    /// re-creating the registry. Callers that need to enforce
    /// uniqueness (the core menu bridge in #314 for `slate.*` ids,
    /// or a future plugin loader policing namespaces) MUST check
    /// the return value and log / reject when it's `true`. Silent
    /// override of a core command id by a plugin would be a
    /// privilege-escalation footgun — that policy lives at the
    /// registration site, not in the registry itself.
    #[must_use = "register replaces existing entries silently; check the return value if uniqueness matters"]
    pub fn register(&self, command: Command, action: Arc<dyn CommandAction>) -> bool {
        let id = command.id.clone();
        self.commands
            .write()
            .expect("CommandRegistry RwLock poisoned")
            .insert(
                id,
                Entry {
                    metadata: command,
                    action,
                },
            )
            .is_some()
    }

    /// Return every registered command's metadata, sorted by
    /// `(section, id)` for deterministic palette rendering.
    ///
    /// **Caller pattern:** call once on palette open and filter
    /// the returned `Vec` on the foreign side, not on every
    /// keystroke. The sort is `O(n log n)` and the result clones
    /// every `Command`; that's invisible at the expected ~100
    /// commands but pathological if invoked per-key. The palette
    /// UI (#316) caches the snapshot for the lifetime of the
    /// palette's open state.
    pub fn list(&self) -> Vec<Command> {
        let guard = self
            .commands
            .read()
            .expect("CommandRegistry RwLock poisoned");
        let mut out: Vec<Command> = guard.values().map(|e| e.metadata.clone()).collect();
        drop(guard);
        out.sort_by(|a, b| (a.section as u8, &a.id).cmp(&(b.section as u8, &b.id)));
        out
    }

    /// Return the metadata for a single command, or `None` if no
    /// command is registered under `id`.
    pub fn find_by_id(&self, id: &str) -> Option<Command> {
        self.commands
            .read()
            .expect("CommandRegistry RwLock poisoned")
            .get(id)
            .map(|e| e.metadata.clone())
    }

    /// Invoke the action for `id`. Clones the action `Arc` out of
    /// the read guard before running so the action is free to
    /// re-enter the registry (register, list, invoke a different
    /// command) without deadlocking.
    pub fn invoke_by_id(&self, id: &str) -> Result<(), CommandError> {
        let action = {
            let guard = self
                .commands
                .read()
                .expect("CommandRegistry RwLock poisoned");
            guard
                .get(id)
                .ok_or_else(|| CommandError::UnknownId(id.to_string()))?
                .action
                .clone()
        };
        action.invoke()
    }
}

impl Default for CommandRegistry {
    fn default() -> Self {
        Self::new()
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::sync::atomic::{AtomicU32, Ordering};
    use std::thread;

    /// In-process action that bumps a counter on every invoke and
    /// optionally fails. Lives only in tests — production actions
    /// will come via the foreign callback interface in slate-uniffi.
    struct CountingAction {
        count: AtomicU32,
        fail: bool,
    }

    impl CountingAction {
        fn new() -> Self {
            Self {
                count: AtomicU32::new(0),
                fail: false,
            }
        }

        fn failing() -> Self {
            Self {
                count: AtomicU32::new(0),
                fail: true,
            }
        }

        fn invoked(&self) -> u32 {
            self.count.load(Ordering::SeqCst)
        }
    }

    impl CommandAction for CountingAction {
        fn invoke(&self) -> Result<(), CommandError> {
            self.count.fetch_add(1, Ordering::SeqCst);
            if self.fail {
                Err(CommandError::ActionFailed("test failure".into()))
            } else {
                Ok(())
            }
        }
    }

    fn fixture(id: &str, section: CommandSection) -> Command {
        Command {
            id: id.into(),
            label: id.into(),
            accessibility_hint: None,
            hotkey_hint: None,
            section,
        }
    }

    #[test]
    fn register_and_list_round_trip() {
        let reg = CommandRegistry::new();
        let replaced = reg.register(
            fixture("slate.test.alpha", CommandSection::File),
            Arc::new(CountingAction::new()),
        );
        assert!(!replaced, "first registration should not be a replace");
        let listed = reg.list();
        assert_eq!(listed.len(), 1);
        assert_eq!(listed[0].id, "slate.test.alpha");
        assert_eq!(listed[0].section, CommandSection::File);
    }

    #[test]
    fn list_is_stable_sorted_by_section_then_id() {
        let reg = CommandRegistry::new();
        let _ = reg.register(fixture("z", CommandSection::File), Arc::new(CountingAction::new()));
        let _ = reg.register(fixture("a", CommandSection::File), Arc::new(CountingAction::new()));
        let _ = reg.register(fixture("m", CommandSection::View), Arc::new(CountingAction::new()));
        let _ = reg.register(fixture("b", CommandSection::Plugins), Arc::new(CountingAction::new()));
        let listed = reg.list();
        let ids: Vec<&str> = listed.iter().map(|c| c.id.as_str()).collect();
        // File section (a, z) → View (m) → Plugins (b)
        assert_eq!(ids, vec!["a", "z", "m", "b"]);
    }

    #[test]
    fn find_by_id_hits_and_misses() {
        let reg = CommandRegistry::new();
        let _ = reg.register(
            fixture("alpha", CommandSection::File),
            Arc::new(CountingAction::new()),
        );
        assert_eq!(reg.find_by_id("alpha").unwrap().id, "alpha");
        assert!(reg.find_by_id("beta").is_none());
    }

    #[test]
    fn invoke_by_id_dispatches_to_action() {
        let reg = CommandRegistry::new();
        let action = Arc::new(CountingAction::new());
        let _ = reg.register(fixture("alpha", CommandSection::File), action.clone());
        reg.invoke_by_id("alpha").unwrap();
        reg.invoke_by_id("alpha").unwrap();
        assert_eq!(action.invoked(), 2);
    }

    #[test]
    fn invoke_by_id_returns_unknown_for_missing() {
        let reg = CommandRegistry::new();
        match reg.invoke_by_id("missing") {
            Err(CommandError::UnknownId(id)) => assert_eq!(id, "missing"),
            other => panic!("expected UnknownId, got {other:?}"),
        }
    }

    #[test]
    fn invoke_by_id_propagates_action_error() {
        let reg = CommandRegistry::new();
        let _ = reg.register(
            fixture("alpha", CommandSection::File),
            Arc::new(CountingAction::failing()),
        );
        match reg.invoke_by_id("alpha") {
            Err(CommandError::ActionFailed(msg)) => assert_eq!(msg, "test failure"),
            other => panic!("expected ActionFailed, got {other:?}"),
        }
    }

    #[test]
    fn re_registering_returns_true_and_replaces_prior_entry() {
        let reg = CommandRegistry::new();
        let first = Arc::new(CountingAction::new());
        let second = Arc::new(CountingAction::new());
        let initial = reg.register(fixture("alpha", CommandSection::File), first.clone());
        let replace = reg.register(fixture("alpha", CommandSection::View), second.clone());
        assert!(!initial, "first registration is not a replacement");
        assert!(replace, "second registration must signal replacement");
        reg.invoke_by_id("alpha").unwrap();
        assert_eq!(first.invoked(), 0);
        assert_eq!(second.invoked(), 1);
        assert_eq!(reg.find_by_id("alpha").unwrap().section, CommandSection::View);
    }

    /// Regression test for the invoke-while-holding-read-guard
    /// deadlock: an action that calls back into the registry must
    /// not block on its own read guard. The action below registers
    /// a new command during invoke; if `invoke_by_id` held the
    /// read lock across `action.invoke()`, the `register` call
    /// inside would block forever on the write-lock acquisition.
    #[test]
    fn action_can_reenter_registry_without_deadlock() {
        struct Reentering {
            reg: Arc<CommandRegistry>,
            inner: Arc<CountingAction>,
        }

        impl CommandAction for Reentering {
            fn invoke(&self) -> Result<(), CommandError> {
                // Register a nested command while we're "inside"
                // an invoke. Also do a read (find_by_id) — both
                // would deadlock if the parent invoke held a guard.
                let _ = self.reg.register(
                    Command {
                        id: "nested".into(),
                        label: "nested".into(),
                        accessibility_hint: None,
                        hotkey_hint: None,
                        section: CommandSection::Vault,
                    },
                    self.inner.clone(),
                );
                let _ = self.reg.find_by_id("nested");
                Ok(())
            }
        }

        let reg = Arc::new(CommandRegistry::new());
        let inner = Arc::new(CountingAction::new());
        let _ = reg.register(
            fixture("alpha", CommandSection::File),
            Arc::new(Reentering {
                reg: reg.clone(),
                inner: inner.clone(),
            }),
        );
        reg.invoke_by_id("alpha").unwrap();
        assert!(reg.find_by_id("nested").is_some());
    }

    /// Three-level reentrance with a write at the deepest frame:
    /// `a.invoke → invoke(b) → invoke(c) → register(d) → invoke(c)`.
    /// Catches the worst case the module-level locking story
    /// promises is safe — a write lock acquired while two read
    /// guards from the same logical call chain would still be open
    /// if `invoke_by_id` ever held them across the action dispatch.
    #[test]
    fn nested_invoke_with_register_does_not_deadlock() {
        struct CallsB {
            reg: Arc<CommandRegistry>,
        }
        impl CommandAction for CallsB {
            fn invoke(&self) -> Result<(), CommandError> {
                self.reg.invoke_by_id("b")
            }
        }

        struct CallsCAndRegistersD {
            reg: Arc<CommandRegistry>,
            inner: Arc<CountingAction>,
        }
        impl CommandAction for CallsCAndRegistersD {
            fn invoke(&self) -> Result<(), CommandError> {
                // Write while nested two invokes deep.
                let _ = self.reg.register(
                    Command {
                        id: "d".into(),
                        label: "d".into(),
                        accessibility_hint: None,
                        hotkey_hint: None,
                        section: CommandSection::Plugins,
                    },
                    self.inner.clone(),
                );
                self.reg.invoke_by_id("c")
            }
        }

        let reg = Arc::new(CommandRegistry::new());
        let inner = Arc::new(CountingAction::new());
        let _ = reg.register(
            fixture("a", CommandSection::File),
            Arc::new(CallsB { reg: reg.clone() }),
        );
        let _ = reg.register(
            fixture("b", CommandSection::File),
            Arc::new(CallsCAndRegistersD {
                reg: reg.clone(),
                inner: inner.clone(),
            }),
        );
        let _ = reg.register(fixture("c", CommandSection::File), inner.clone());

        reg.invoke_by_id("a").unwrap();

        assert_eq!(inner.invoked(), 1, "c executed once via the nested chain");
        assert!(
            reg.find_by_id("d").is_some(),
            "d registered from inside the nested invoke chain"
        );
    }

    /// Concurrent reentrance: one thread loops `invoke_by_id` on a
    /// reentrant action that calls `register` while another thread
    /// loops `register` + `list`. Any read-then-write deadlock
    /// would hang this test (and is harvested by the test runner
    /// timeout).
    #[test]
    fn concurrent_reentrant_invoke_and_register_does_not_deadlock() {
        use std::sync::Mutex;

        struct Reentering {
            reg: Arc<CommandRegistry>,
            next: Mutex<u32>,
        }
        impl CommandAction for Reentering {
            fn invoke(&self) -> Result<(), CommandError> {
                let id = {
                    let mut n = self.next.lock().expect("next mutex poisoned");
                    let id = format!("reentrant.{n}");
                    *n += 1;
                    id
                };
                let _ = self.reg.register(
                    Command {
                        id,
                        label: "x".into(),
                        accessibility_hint: None,
                        hotkey_hint: None,
                        section: CommandSection::Plugins,
                    },
                    Arc::new(CountingAction::new()),
                );
                Ok(())
            }
        }

        let reg = Arc::new(CommandRegistry::new());
        let action = Arc::new(Reentering {
            reg: reg.clone(),
            next: Mutex::new(0),
        });
        let _ = reg.register(fixture("root", CommandSection::File), action);

        let mut handles = vec![];
        let invoker_reg = reg.clone();
        handles.push(thread::spawn(move || {
            for _ in 0..50 {
                invoker_reg.invoke_by_id("root").unwrap();
            }
        }));
        let writer_reg = reg.clone();
        handles.push(thread::spawn(move || {
            for i in 0..50 {
                let _ = writer_reg.register(
                    Command {
                        id: format!("writer.{i}"),
                        label: "y".into(),
                        accessibility_hint: None,
                        hotkey_hint: None,
                        section: CommandSection::Plugins,
                    },
                    Arc::new(CountingAction::new()),
                );
                let _ = writer_reg.list();
            }
        }));
        for h in handles {
            h.join().unwrap();
        }
        // 50 reentrant + 50 writer + 1 root = 101.
        assert_eq!(reg.list().len(), 101);
    }

    #[test]
    fn concurrent_register_and_invoke_is_thread_safe() {
        let reg = Arc::new(CommandRegistry::new());
        let mut handles = vec![];
        for i in 0..16 {
            let r = reg.clone();
            handles.push(thread::spawn(move || {
                let _ = r.register(
                    fixture(&format!("c{i}"), CommandSection::File),
                    Arc::new(CountingAction::new()),
                );
                // Even if we lose the race against another thread's
                // register, list/invoke must not panic or deadlock.
                let _ = r.invoke_by_id(&format!("c{i}"));
                let _ = r.list();
            }));
        }
        for h in handles {
            h.join().unwrap();
        }
        assert_eq!(reg.list().len(), 16);
    }

    #[test]
    fn default_is_empty_registry() {
        let reg: CommandRegistry = Default::default();
        assert!(reg.list().is_empty());
    }
}
