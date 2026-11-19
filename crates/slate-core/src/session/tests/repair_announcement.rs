// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! #1683 (plan 47 CB-12): a repair announces only what it changed.
//!
//! After a move lands, the sweep repairs both of its markers, and the old
//! path then names nothing: no file, and no `files` row, because the move's
//! own commit moved the row. The repair's not-found arm used to act as if
//! it had removed a row whether or not one stood: it announced `Deleted`,
//! applied a graph removal and moved the session's generation. A Windows
//! host marked a tab still at the old path missing and said so.
//!
//! Two sessions on one vault stand for two Slate windows: each holds its
//! own connection to the shared `.slate/cache.sqlite`, as two processes do.
//! Every path here carries `1683`, so no fault-seam trigger another fact
//! sets (a path substring, process-wide) can match one of them.

use super::common::*;
use super::*;
use crate::graph::NodeKey;

/// Records every file-change notification, in the order it arrives.
#[derive(Default)]
struct Recorder {
    changes: std::sync::Mutex<Vec<FileChangeEvent>>,
}

impl VaultEventListener for Recorder {
    fn on_error(&self, _code: EventErrorCode, _path: String, _message: String) {}
    fn on_file_change(&self, event: FileChangeEvent) {
        self.changes.lock().unwrap().push(event);
    }
}

impl Recorder {
    fn on(session: &VaultSession) -> Arc<Recorder> {
        let recorder = Arc::new(Recorder::default());
        session.register_event_listener(recorder.clone());
        recorder
    }

    /// The notifications so far as `Kind path`, or `Kind path <- previous`
    /// for one that carries a previous path, taken out of the record.
    fn take(&self) -> Vec<String> {
        self.changes
            .lock()
            .unwrap()
            .drain(..)
            .map(|event| match event.previous_path {
                Some(previous) => format!("{:?} {} <- {previous}", event.kind, event.path),
                None => format!("{:?} {}", event.kind, event.path),
            })
            .collect()
    }

    /// [`Self::take`], sorted.
    fn take_sorted(&self) -> Vec<String> {
        let mut heard = self.take();
        heard.sort();
        heard
    }
}

fn strings(items: &[&str]) -> Vec<String> {
    items.iter().map(|item| item.to_string()).collect()
}

fn nothing() -> Vec<String> {
    Vec::new()
}

/// A scanned vault holding `first1683.md` and `other1683.md`, and a
/// recorder on the session.
fn one_note_vault() -> (tempfile::TempDir, VaultSession, Arc<Recorder>) {
    let (tmp, a) = make_vault(|provider| {
        provider.write_file("first1683.md", b"- [ ] one\n").unwrap();
        provider
            .write_file("other1683.md", b"- [ ] other\n")
            .unwrap();
    });
    a.scan_initial(&CancelToken::new()).unwrap();
    let recorder = Recorder::on(&a);
    (tmp, a, recorder)
}

/// Another session on the same vault, as another Slate window holds one.
fn another_session(tmp: &tempfile::TempDir) -> VaultSession {
    VaultSession::from_filesystem(tmp.path().to_path_buf()).unwrap()
}

/// Another session renames `first1683.md` to `second1683.md` and closes.
fn another_session_renames_first(tmp: &tempfile::TempDir) {
    let b = another_session(tmp);
    b.rename_file("first1683.md", "second1683.md").unwrap();
}

/// The vault-wide task list as `path: text`, in the order the query gives.
fn vault_tasks(session: &VaultSession) -> Vec<String> {
    session
        .tasks_in_vault(crate::TaskFilter::default(), Paging::first(50))
        .unwrap()
        .items
        .into_iter()
        .map(|task| format!("{}: {}", task.path, task.task.text))
        .collect()
}

/// An aged marker under `path`, as a move leaves one when it lands, or a
/// held one, as a move plants it before the kernel call.
fn plant_marker(session: &VaultSession, path: &str, held: bool) {
    let created_ms = if held {
        VaultSession::HELD_MOVE_MARKER_MS
    } else {
        1
    };
    let conn = session.conn.lock().unwrap();
    conn.execute(
        "INSERT INTO text_write_intents(path, created_ms, token, registered_epoch)
         VALUES (?1, ?2, ?3, COALESCE((SELECT index_epoch FROM files WHERE path = ?1), 0))",
        rusqlite::params![path, created_ms, fresh_intent_token()],
    )
    .unwrap();
}

/// How many markers stand under `path`.
fn markers_under(session: &VaultSession, path: &str) -> i64 {
    let conn = session.conn.lock().unwrap();
    conn.query_row(
        "SELECT COUNT(*) FROM text_write_intents WHERE path = ?1",
        rusqlite::params![path],
        |row| row.get(0),
    )
    .unwrap()
}

/// Whether a `files` row stands under `path`.
fn row_stands(session: &VaultSession, path: &str) -> bool {
    let conn = session.conn.lock().unwrap();
    conn.query_row(
        "SELECT EXISTS(SELECT 1 FROM files WHERE path = ?1)",
        rusqlite::params![path],
        |row| row.get(0),
    )
    .unwrap()
}

/// The paths of the live graph index's file nodes. Asserts the index is
/// live first: reading it through `with_graph` would build one.
fn live_graph_paths(session: &VaultSession, context: &str) -> Vec<String> {
    assert!(
        session.graph_is_built(),
        "the graph index is live {context}"
    );
    let mut paths: Vec<String> = session
        .with_graph(|graph| graph.canonical_nodes())
        .unwrap()
        .into_iter()
        .filter_map(|(key, _, _)| match key {
            NodeKey::Path(path) => Some(path),
            _ => None,
        })
        .collect();
    paths.sort();
    paths
}

/// The live graph index structurally equals a fresh build from the index.
fn assert_graph_matches_rebuild(session: &VaultSession, context: &str) {
    assert!(
        session.graph_is_built(),
        "the graph index is live {context}"
    );
    let fresh = session.graph_rebuild_reference().unwrap();
    let live = session
        .with_graph(|graph| (graph.deep_equals(&fresh), graph.canonical_nodes()))
        .unwrap();
    assert!(
        live.0,
        "the graph index equals a rebuild {context}\nlive nodes: {:?}\nfresh nodes: {:?}",
        live.1,
        fresh.canonical_nodes()
    );
}

/// Fact 1: another session's rename is heard as nothing for the old path,
/// whichever task query sweeps it. A vault-wide query hears the
/// destination read again, and only that.
#[test]
fn another_sessions_rename_is_not_announced_as_a_deletion() {
    let mut heard = Vec::new();
    for query in ["tasks_for_file", "note_tasks", "tasks_in_vault"] {
        let (tmp, a, recorder) = one_note_vault();
        another_session_renames_first(&tmp);
        assert_eq!(
            recorder.take(),
            nothing(),
            "premise: A heard nothing of B's rename"
        );
        match query {
            "tasks_for_file" => {
                a.tasks_for_file("first1683.md").unwrap();
            }
            "note_tasks" => {
                a.note_tasks("first1683.md", 50).unwrap();
            }
            _ => {
                vault_tasks(&a);
            }
        }
        heard.push((query, recorder.take_sorted()));
    }

    assert_eq!(
        heard,
        vec![
            ("tasks_for_file", nothing()),
            ("note_tasks", nothing()),
            ("tasks_in_vault", strings(&["Modified second1683.md"])),
        ]
    );
}

/// Fact 2: no second session is needed. A session that renamed a note
/// itself hears nothing for the old path when it next sweeps it.
#[test]
fn a_sessions_own_rename_is_not_followed_by_a_deletion_of_the_old_path() {
    let (_tmp, a, recorder) = one_note_vault();
    a.rename_file("first1683.md", "second1683.md").unwrap();
    assert_eq!(
        recorder.take(),
        strings(&["Renamed second1683.md <- first1683.md"]),
        "premise: the rename's own event"
    );

    a.tasks_for_file("first1683.md").unwrap();
    assert_eq!(recorder.take(), nothing());

    vault_tasks(&a);
    assert_eq!(recorder.take(), strings(&["Modified second1683.md"]));
}

/// Fact 3: a folder's rename and a move into another folder are renames
/// too. The vault-wide query hears each destination read again, once, and
/// nothing for the three old paths.
#[test]
fn another_sessions_folder_rename_and_move_announce_no_deletion() {
    let (tmp, a) = make_vault(|provider| {
        provider
            .write_file("Dir1683/x1683.md", b"- [ ] x\n")
            .unwrap();
        provider
            .write_file("Dir1683/y1683.md", b"- [ ] y\n")
            .unwrap();
        provider
            .write_file("third1683.md", b"- [ ] third\n")
            .unwrap();
        provider.create_dir("Sub1683").unwrap();
    });
    a.scan_initial(&CancelToken::new()).unwrap();
    let recorder = Recorder::on(&a);
    {
        let b = another_session(&tmp);
        b.rename_folder("Dir1683", "Ren1683").unwrap();
        b.move_file("third1683.md", "Sub1683").unwrap();
    }

    vault_tasks(&a);

    assert_eq!(
        recorder.take_sorted(),
        strings(&[
            "Modified Ren1683/x1683.md",
            "Modified Ren1683/y1683.md",
            "Modified Sub1683/third1683.md",
        ])
    );
}

/// Fact 4: the two host repair routes, on a path with no row and no file,
/// report success as before and send nothing: for a path that never
/// existed, and for the old path of another session's rename.
#[test]
fn a_hosts_repair_of_a_path_with_no_row_and_no_file_announces_nothing() {
    let (tmp, a, recorder) = one_note_vault();
    let routes = |path: &str| {
        let reindexed = a.reindex_path(path);
        assert!(
            matches!(reindexed, Ok(())),
            "reindex_path({path}): {reindexed:?}"
        );
        let repaired = a.repair_or_contain_path(path);
        assert!(
            matches!(repaired, Ok(TaskIndexRepairOutcome::Repaired)),
            "repair_or_contain_path({path}): {repaired:?}"
        );
    };

    routes("never1683.md");
    let never = recorder.take();

    another_session_renames_first(&tmp);
    routes("first1683.md");
    let renamed = recorder.take();

    assert_eq!((never, renamed), (nothing(), nothing()));
}

/// Fact 5: a repair that found nothing still clears the marker it was run
/// for, so the next query runs no repair for the path and hears nothing.
#[test]
fn a_repair_that_found_nothing_still_clears_its_marker() {
    let (tmp, a, recorder) = one_note_vault();
    another_session_renames_first(&tmp);
    assert_eq!(
        markers_under(&a, "first1683.md"),
        1,
        "premise: the rename left an aged marker under the old path"
    );

    a.tasks_for_file("first1683.md").unwrap();
    recorder.take();
    assert_eq!(markers_under(&a, "first1683.md"), 0);

    a.tasks_for_file("first1683.md").unwrap();
    assert_eq!(recorder.take(), nothing());
    assert_eq!(markers_under(&a, "first1683.md"), 0);
}

/// Fact 6: a repair that found nothing changed nothing a host's caches
/// key on, so the session's generation does not move.
#[test]
fn a_repair_that_found_nothing_leaves_the_generation_alone() {
    let (tmp, a, _recorder) = one_note_vault();
    another_session_renames_first(&tmp);
    let before = a.interaction_generation();

    a.tasks_for_file("first1683.md").unwrap();

    assert_eq!(a.interaction_generation(), before);
}

/// Fact 7: one session with its graph built renames a note and then asks
/// for the old path's tasks. The graph index stays live: the repair stages
/// no removal of a node the rename already moved. Then the vault-wide
/// query re-reads the destination, and the index still equals a rebuild.
///
/// The graph's generation is read only across the old path's query: the
/// destination's re-read follows the stat poison, stages a metadata touch
/// and moves it by one.
#[test]
fn a_rename_then_a_task_query_keeps_the_graph_index() {
    let (_tmp, a) = make_vault(|provider| {
        provider.write_file("first1683.md", b"- [ ] one\n").unwrap();
        provider
            .write_file("linker1683.md", b"see [[first1683]]\n")
            .unwrap();
    });
    a.scan_initial(&CancelToken::new()).unwrap();
    a.with_graph(|graph| graph.node_count()).unwrap();
    a.rename_file("first1683.md", "second1683.md").unwrap();
    assert!(
        a.graph_is_built(),
        "premise: the rename kept the graph live"
    );
    let after_rename = a.graph_generation();

    a.tasks_for_file("first1683.md").unwrap();

    assert!(
        a.graph_is_built(),
        "the graph index is live after the old path's query"
    );
    assert_eq!(a.graph_generation(), after_rename);

    vault_tasks(&a);

    assert_eq!(
        live_graph_paths(&a, "after the vault-wide query"),
        strings(&["linker1683.md", "second1683.md"])
    );
    assert_graph_matches_rebuild(&a, "after the vault-wide query");
}

/// Fact 8: a host learns of another session's rename from its next
/// change-feed window, which names it as a rename, whether or not a sweep
/// repaired the old path first.
#[test]
fn another_sessions_rename_reaches_the_next_feed_window_whatever_was_swept() {
    let (tmp, a, _recorder) = one_note_vault();
    a.register_change_feed().unwrap();
    another_session_renames_first(&tmp);

    a.tasks_for_file("first1683.md").unwrap();

    let window = a.change_feed_window(None, &CancelToken::new()).unwrap();
    let mut records = Vec::new();
    let mut cursor = None;
    loop {
        let page = a
            .change_feed_page(
                &window.token,
                cursor.take(),
                crate::MAX_CHANGE_FEED_PAGE_LIMIT,
                &CancelToken::new(),
            )
            .unwrap();
        records.extend(page.records);
        match page.next_cursor {
            Some(next) => cursor = Some(next),
            None => break,
        }
    }
    let renames: Vec<(String, Option<String>)> = records
        .into_iter()
        .filter(|record| record.effect == crate::ChangeEffect::Renamed)
        .map(|record| (record.path, record.renamed_from))
        .collect();
    assert_eq!(
        renames,
        vec![(
            "second1683.md".to_string(),
            Some("first1683.md".to_string())
        )]
    );
}

/// Fact 9: a rename that stopped between the volume and the index (#1981's
/// shape) leaves the row under the old path and a held marker under each
/// name. The sweep removes that row and still announces it, and reads the
/// destination, whose first row it announces `Created` (#1704).
#[test]
fn a_stopped_renames_recovery_still_announces_the_row_it_removed() {
    let (tmp, a) = make_vault(|provider| {
        provider.write_file("A1683.md", b"- [ ] one\n").unwrap();
    });
    a.scan_initial(&CancelToken::new()).unwrap();
    plant_marker(&a, "A1683.md", true);
    plant_marker(&a, "B1683.md", true);
    std::fs::rename(tmp.path().join("A1683.md"), tmp.path().join("B1683.md")).unwrap();
    let recorder = Recorder::on(&a);

    assert_eq!(vault_tasks(&a), strings(&["B1683.md: one"]));

    assert_eq!(
        recorder.take_sorted(),
        strings(&["Created B1683.md", "Deleted A1683.md"])
    );
    assert!(!row_stands(&a, "A1683.md"));
}

/// Fact 10: where a row stands and its file is gone, the repair removes the
/// row, removes its graph node, moves the generation and announces
/// `Deleted`, as before (round 24).
#[test]
fn a_repair_that_removes_a_standing_row_still_removes_its_node_and_moves_the_generation() {
    let (tmp, a) = make_vault(|provider| {
        provider.write_file("gone1683.md", b"- [ ] gone\n").unwrap();
        provider
            .write_file("linker1683.md", b"see [[gone1683]]\n")
            .unwrap();
    });
    a.scan_initial(&CancelToken::new()).unwrap();
    a.with_graph(|graph| graph.node_count()).unwrap();
    std::fs::remove_file(tmp.path().join("gone1683.md")).unwrap();
    plant_marker(&a, "gone1683.md", false);
    let recorder = Recorder::on(&a);
    let before = a.interaction_generation();

    vault_tasks(&a);

    assert_eq!(recorder.take(), strings(&["Deleted gone1683.md"]));
    assert!(!row_stands(&a, "gone1683.md"));
    assert_eq!(
        live_graph_paths(&a, "after the repair removed the row"),
        strings(&["linker1683.md"])
    );
    assert_graph_matches_rebuild(&a, "after the repair removed the row");
    assert_eq!(a.interaction_generation(), before + 1);
}

/// A provider whose `ghost1683.md` stats as a small file and reads as not
/// found: a file deleted between the repair's stat and its read.
struct GoneBeforeTheReadProvider {
    inner: FsVaultProvider,
}

const GHOST: &str = "ghost1683.md";

fn gone_before_the_read() -> VaultError {
    VaultError::Io(std::io::Error::new(
        std::io::ErrorKind::NotFound,
        "test provider: the file went between the stat and the read",
    ))
}

impl VaultProvider for GoneBeforeTheReadProvider {
    fn list_dir(&self, relative: &str) -> Result<Vec<crate::DirEntry>, VaultError> {
        self.inner.list_dir(relative)
    }
    fn read_file(&self, relative: &str) -> Result<Vec<u8>, VaultError> {
        if relative == GHOST {
            return Err(gone_before_the_read());
        }
        self.inner.read_file(relative)
    }
    fn read_file_with_cap(&self, relative: &str, max_bytes: u64) -> Result<Vec<u8>, VaultError> {
        if relative == GHOST {
            return Err(gone_before_the_read());
        }
        self.inner.read_file_with_cap(relative, max_bytes)
    }
    fn read_in_vault_with_cap(
        &self,
        relative: &str,
        max_bytes: u64,
    ) -> Result<Vec<u8>, VaultError> {
        if relative == GHOST {
            return Err(gone_before_the_read());
        }
        self.inner.read_in_vault_with_cap(relative, max_bytes)
    }
    fn write_file(&self, relative: &str, contents: &[u8]) -> Result<(), VaultError> {
        self.inner.write_file(relative, contents)
    }
    fn delete(&self, relative: &str) -> Result<(), VaultError> {
        self.inner.delete(relative)
    }
    fn rename(&self, from: &str, to: &str) -> Result<(), VaultError> {
        self.inner.rename(from, to)
    }
    fn create_dir(&self, relative: &str) -> Result<(), VaultError> {
        self.inner.create_dir(relative)
    }
    fn stat(&self, relative: &str) -> Result<crate::FileStat, VaultError> {
        if relative == GHOST {
            return Ok(crate::FileStat {
                size_bytes: 10,
                mtime_ms: 1,
                ctime_ms: 0,
                birthtime_ms: 0,
                kind: EntryKind::File,
            });
        }
        self.inner.stat(relative)
    }
    fn mutation_path_kind(&self, path: &str) -> Result<Option<EntryKind>, VaultError> {
        self.inner.mutation_path_kind(path)
    }
    fn mutation_path_exists(&self, path: &str) -> Result<bool, VaultError> {
        self.inner.mutation_path_exists(path)
    }
    fn canonical_path(&self, path: &str) -> Result<Option<String>, VaultError> {
        self.inner.canonical_path(path)
    }
    fn watch(
        &self,
        sink: Arc<dyn crate::FileEventSink>,
    ) -> Result<Option<crate::WatchHandle>, VaultError> {
        self.inner.watch(sink)
    }
}

/// Fact 11: the repair's read stages a new path's graph addition before it
/// reads the file. A file gone between the stat and the read leaves that
/// addition staged and no row inserted, so the graph is left as it was:
/// nothing staged for the path is applied, so the graph's generation does
/// not move, and nothing is announced.
///
/// The generation is read first: an applied addition followed by an applied
/// removal of the same node leaves a graph that equals a rebuild, and only
/// the generation shows that a replay ran.
#[test]
fn a_file_gone_between_the_stat_and_the_read_leaves_the_graph_as_it_was() {
    let tmp = tempfile::tempdir().unwrap();
    std::fs::write(tmp.path().join("real1683.md"), b"- [ ] real\n").unwrap();
    let provider = Arc::new(GoneBeforeTheReadProvider {
        inner: FsVaultProvider::new(tmp.path().to_path_buf()),
    });
    let a = VaultSession::open(
        provider as Arc<dyn VaultProvider>,
        SessionConfig::new(tmp.path().join(".slate")),
    )
    .unwrap();
    a.scan_initial(&CancelToken::new()).unwrap();
    a.with_graph(|graph| graph.node_count()).unwrap();
    assert!(!row_stands(&a, GHOST), "premise: no row stands at the path");
    plant_marker(&a, GHOST, false);
    let graph_generation_before = a.graph_generation();
    let recorder = Recorder::on(&a);

    vault_tasks(&a);

    assert_eq!(
        a.graph_generation(),
        graph_generation_before,
        "the graph's generation moved: a replay was applied for the path"
    );
    assert_eq!(
        live_graph_paths(&a, "after the repair"),
        strings(&["real1683.md"])
    );
    assert_graph_matches_rebuild(&a, "after the repair");
    assert!(!row_stands(&a, GHOST));
    assert_eq!(markers_under(&a, GHOST), 0);
    assert_eq!(recorder.take(), nothing());
}

/// A temporary folder that keeps two names differing only by case apart, or
/// `None` where the volume cannot: a Linux volume as it is, and on Windows a
/// folder switched to case-sensitive names, which needs the Windows Subsystem
/// for Linux feature and no elevation (the shape of `canonical_identity.rs`'s
/// `two_files_by_case_vault`).
fn case_keeping_folder() -> Option<tempfile::TempDir> {
    let tmp = tempfile::tempdir().unwrap();
    #[cfg(windows)]
    {
        let switched = std::process::Command::new("fsutil")
            .args(["file", "setCaseSensitiveInfo"])
            .arg(tmp.path())
            .arg("enable")
            .output()
            .is_ok_and(|output| output.status.success());
        if !switched {
            return None;
        }
    }
    std::fs::write(tmp.path().join("Probe1683.tmp"), b"p").unwrap();
    let aliases = tmp.path().join("probe1683.tmp").exists();
    std::fs::remove_file(tmp.path().join("Probe1683.tmp")).unwrap();
    if aliases {
        return None;
    }
    Some(tmp)
}

/// Fact 12: on a volume that keeps case, a case-only rename is a plain
/// one. The sweep after it reports the note read again under its new name
/// and nothing for the old name, which names no file and no row.
///
/// Where the volume cannot keep case the fact has nothing to run on. It
/// says which happened, so a run's output shows whether it ran.
// The line printed is the fact's own record and names no vault path; #507's
// no-stdout rule is for slate-core itself.
#[allow(clippy::disallowed_macros)]
#[test]
fn a_case_only_rename_on_a_folder_that_keeps_case_reports_only_the_stored_spelling() {
    let Some(tmp) = case_keeping_folder() else {
        println!(
            "a_case_only_rename_on_a_folder_that_keeps_case_reports_only_the_stored_spelling: SKIPPED, this volume cannot keep case"
        );
        return;
    };
    println!(
        "a_case_only_rename_on_a_folder_that_keeps_case_reports_only_the_stored_spelling: RAN on a folder that keeps case"
    );
    std::fs::write(tmp.path().join("A1683.md"), b"- [ ] one\n").unwrap();
    let a = VaultSession::from_filesystem(tmp.path().to_path_buf()).unwrap();
    a.scan_initial(&CancelToken::new()).unwrap();
    a.rename_file("A1683.md", "a1683.md").unwrap();
    assert!(
        !tmp.path().join("A1683.md").exists(),
        "premise: the old spelling names nothing"
    );
    let recorder = Recorder::on(&a);

    assert_eq!(vault_tasks(&a), strings(&["a1683.md: one"]));

    assert_eq!(recorder.take(), strings(&["Modified a1683.md"]));
}

/// Fact 13 (plan 47 CB-31, #1704): a repair that writes a path's first row
/// announces `Created`, as a save of a new path does, through both host
/// repair routes; a repair of a path the index already holds still
/// announces `Modified`. A Windows host reseats a missing tab on `Created`
/// and not on `Modified`.
#[test]
fn a_repair_that_first_indexes_a_file_announces_it_created() {
    let (_tmp, a, recorder) = one_note_vault();
    a.provider
        .write_file("arrived1683.md", b"- [ ] arrived\n")
        .unwrap();
    let repaired = a.repair_or_contain_path("arrived1683.md");
    assert!(
        matches!(repaired, Ok(TaskIndexRepairOutcome::Repaired)),
        "{repaired:?}"
    );
    let first_repair = recorder.take();
    a.repair_or_contain_path("arrived1683.md").unwrap();
    let second_repair = recorder.take();

    let (_tmp, b, recorder) = one_note_vault();
    b.provider
        .write_file("arrived1683.md", b"- [ ] arrived\n")
        .unwrap();
    b.reindex_path("arrived1683.md").unwrap();
    let first_reindex = recorder.take();
    b.reindex_path("arrived1683.md").unwrap();
    let second_reindex = recorder.take();

    assert_eq!(
        (first_repair, second_repair, first_reindex, second_reindex),
        (
            strings(&["Created arrived1683.md"]),
            strings(&["Modified arrived1683.md"]),
            strings(&["Created arrived1683.md"]),
            strings(&["Modified arrived1683.md"]),
        )
    );
}
