// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! #1917: no statement is compiled again per binding, on any path driven
//! here.
//!
//! rusqlite's statement cache clears a statement's bindings when the
//! statement goes back in, and SQLite 3.54 expires any statement whose plan
//! looked at a bound value, so the statement is compiled again on its next
//! use. On `main` that was 38 of core's SQL literals and most of its
//! `format!` templates: any statement with an equality on a bare parameter
//! and a join, an ORDER BY, a GROUP BY or a DISTINCT, and any with a bound
//! LIMIT. No short rule covers the class, so this fact reads nothing from
//! source. It drives the callers and asks SQLite, through
//! [`counting_statements`].
//!
//! Each flow holds two things. The class: nothing the flow ran twelve times
//! or more was re-prepared more than twice. And, against a pass that proves
//! nothing: each statement the flow is listed for did run twelve times or
//! more, on at most two statement objects. A statement that a site prepares
//! per call, or that the cache dropped, shows an object for every run.

use super::common::*;
use super::*;

/// How often a flow runs each statement at least, each time with another
/// note, page or query.
const TIMES: usize = 12;
const NOTES: usize = 40;

/// One of the 38 literals `main` re-prepared, by where its SQL starts,
/// what it holds and where it ends (whitespace flattened; an empty part
/// matches anything). Three parts, because several of the statements differ
/// only in their last clause.
struct Member {
    site: &'static str,
    starts: &'static str,
    holds: &'static str,
    ends: &'static str,
}

const fn member(
    site: &'static str,
    starts: &'static str,
    holds: &'static str,
    ends: &'static str,
) -> Member {
    Member {
        site,
        starts,
        holds,
        ends,
    }
}

struct Flow {
    name: &'static str,
    reaches: &'static [Member],
    run: fn(&VaultSession, &std::path::Path),
}

fn note(index: usize) -> String {
    format!("n{:02}.md", index % NOTES)
}

fn run_view(session: &VaultSession, filter: Option<&str>) {
    let filters = filter.map_or_else(String::new, |filter| {
        assert!(!filter.contains('\''), "single quotes break YAML quoting");
        format!("    filters: '{filter}'\n")
    });
    let yaml =
        format!("views:\n  - type: table\n    name: T\n{filters}    order:\n      - file.name\n");
    let (base, warnings) = crate::bases::parse_base(&yaml);
    assert!(warnings.is_empty(), "{warnings:?}");
    let query_json = serde_json::to_string(&crate::bases::view_query(&base, 0)).unwrap();
    let handle = session.open_query(&query_json, None).unwrap();
    let result = session
        .base_execute(handle, 0, None, None, &CancelToken::new())
        .unwrap();
    session.close_base(handle);
    assert_eq!(result.view_error, None, "{filter:?}");
}

fn run_dql(session: &VaultSession, source: &str) {
    let handle = session.open_dql(source, None).unwrap();
    let result = session
        .base_execute(handle, 0, None, None, &CancelToken::new())
        .unwrap();
    session.close_base(handle);
    assert_eq!(result.view_error, None, "{source}");
    assert!(result.rows.len() >= NOTES, "{source}");
}

const FLOWS: &[Flow] = &[
    Flow {
        name: "a plain bases view",
        reaches: &[
            member(
                "bases/engine.rs load_properties",
                "SELECT key, value_kind, value_text FROM properties WHERE file_id = ?1",
                "",
                "ORDER BY ordinal",
            ),
            member(
                "bases/engine.rs load_tags",
                "SELECT tag_norm FROM file_tags WHERE file_id = ?1",
                "",
                "",
            ),
            member(
                "bases/engine.rs load_outgoing_links",
                "SELECT target_raw, target_path, target_anchor, is_embed, display_text FROM links",
                "WHERE source_file_id = ?1",
                "",
            ),
            member(
                "bases/engine.rs load_backlinks",
                "SELECT src.path FROM links l JOIN files src",
                "WHERE l.target_path = ?1",
                "",
            ),
        ],
        run: |session, _| run_view(session, None),
    },
    Flow {
        name: "a bases view for each temporal operator, outside the pushed-down position",
        reaches: &[
            member(
                "bases/engine.rs oplog_has_change_since",
                "SELECT 1 FROM oplog_events e",
                "",
                "AND e.event_class = 1 AND e.ts_ms >= ?2 LIMIT 1",
            ),
            member(
                "bases/engine.rs oplog_has_property_change",
                "SELECT 1 FROM oplog_events e",
                "e.property_key = ?2",
                "",
            ),
            member(
                "bases/engine.rs oplog_deleted_content_matches",
                "SELECT 1 FROM oplog_events e",
                "e.deleted_text LIKE",
                "",
            ),
            member(
                "bases/engine.rs oplog_deleted_content_matches_regex",
                "SELECT e.deleted_text FROM oplog_events e",
                "",
                "",
            ),
        ],
        run: |session, _| {
            for operator in [
                r#"oplog.has_change_since("7d")"#,
                r#"oplog.has_property_change("status", "7d")"#,
                r#"oplog.deleted_content_matches("nothing like it", "7d")"#,
                r#"oplog.deleted_content_matches_regex("nothing.like.it", "7d")"#,
                r#"oplog.created_since("7d")"#,
                r#"oplog.untouched_for("7d")"#,
            ] {
                run_view(
                    session,
                    Some(&format!(r#"{operator} || file.name == "no such note""#)),
                );
            }
        },
    },
    Flow {
        name: "DQL that reads tags, and DQL that reads an inline field",
        reaches: &[
            member(
                "tags_db.rs load_dql_tags_for_path",
                "SELECT t.tag_raw FROM dql_file_tags t JOIN files f",
                "",
                "",
            ),
            member(
                "dql_inline_fields_db.rs load_dql_inline_fields_for_path",
                "SELECT key, value_json FROM dql_inline_fields WHERE file_id = ?1",
                "",
                "",
            ),
        ],
        run: |session, _| {
            run_dql(session, "TABLE file.tags\n");
            run_dql(session, "TABLE extra\n");
        },
    },
    Flow {
        name: "a note's link panels, its bundle, its backlinks, and the unresolved links by pages of two",
        reaches: &[
            member(
                "links_db.rs outgoing_links_for",
                "SELECT links.target_path, links.target_raw, links.target_anchor",
                "",
                "WHERE files.path = ?1 ORDER BY links.ordinal ASC",
            ),
            member(
                "links_db.rs outgoing_links_bounded (embeds)",
                "SELECT links.target_path, links.target_raw, links.target_anchor",
                "",
                "AND links.is_embed != 0 ORDER BY links.ordinal ASC LIMIT ?2",
            ),
            member(
                "links_db.rs outgoing_links_bounded",
                "SELECT links.target_path, links.target_raw, links.target_anchor",
                "",
                "WHERE files.path = ?1 ORDER BY links.ordinal ASC LIMIT ?2",
            ),
            member(
                "links_db.rs outgoing_links_count (embeds)",
                "SELECT COUNT(*) FROM links JOIN files",
                "",
                "WHERE files.path = ?1 AND links.is_embed != 0",
            ),
            member(
                "links_db.rs outgoing_links_count",
                "SELECT COUNT(*) FROM links JOIN files",
                "",
                "WHERE files.path = ?1",
            ),
            member(
                "links_db.rs backlinks_for",
                "SELECT files.path, links.snippet",
                "WHERE links.target_path = ?1",
                "",
            ),
            member(
                "links_db.rs unresolved_links",
                "SELECT files.path, links.target_raw, links.ordinal, links.snippet",
                "WHERE links.target_path IS NULL",
                "",
            ),
        ],
        run: |session, _| {
            for index in 0..TIMES {
                let path = note(index);
                session
                    .note_link_panels(&path, Paging::first(50), 50, 50)
                    .unwrap();
                session.note_load_bundle(&path, Paging::first(50)).unwrap();
                session.backlinks(&path, Paging::first(50)).unwrap();
            }
            let mut cursor = None;
            for _ in 0..TIMES {
                let page = session
                    .list_unresolved_links(Paging { cursor, limit: 2 })
                    .unwrap();
                assert_eq!(page.items.len(), 2);
                cursor = page.next_cursor;
            }
        },
    },
    Flow {
        name: "a note's metadata, outline and tasks",
        reaches: &[
            member(
                "properties_db.rs properties_for_file",
                "SELECT key, value_kind, value_text, key_identity FROM properties",
                "",
                "",
            ),
            member(
                "session.rs get_file_metadata_impl",
                "SELECT ordinal, level, text, anchor_id, byte_offset FROM headings",
                "",
                "ORDER BY ordinal ASC",
            ),
            member(
                "session.rs note_outline",
                "SELECT ordinal, level, text, anchor_id, byte_offset FROM headings",
                "",
                "ORDER BY ordinal ASC LIMIT ?2",
            ),
            member(
                "tasks_db.rs tasks_for_file",
                "SELECT ordinal, text, status_char, completed",
                "",
                "FROM tasks WHERE file_id = ?1 ORDER BY ordinal ASC",
            ),
            member(
                "tasks_db.rs note_tasks_bounded",
                "SELECT ordinal, text, status_char, completed",
                "",
                "FROM tasks WHERE file_id = ?1 AND completed = ?2 ORDER BY ordinal ASC LIMIT ?3",
            ),
        ],
        run: |session, _| {
            for index in 0..TIMES {
                let path = note(index);
                assert!(session.get_file_metadata(&path).unwrap().is_some());
                session.note_outline(&path, 50).unwrap();
                session.note_tasks(&path, 50).unwrap();
                assert_eq!(session.tasks_for_file(&path).unwrap().len(), 1);
            }
        },
    },
    Flow {
        name: "an embed of a heading",
        reaches: &[member(
            "session.rs resolve_indexed_section_embed",
            "SELECT level, text, anchor_id, byte_offset FROM headings",
            "",
            "",
        )],
        run: |session, _| {
            for index in 0..TIMES {
                session
                    .resolve_embed(
                        &note(index),
                        &format!("n{:02}#Part A", (index + 1) % NOTES),
                        None,
                        &CancelToken::new(),
                    )
                    .unwrap();
            }
        },
    },
    Flow {
        name: "the tag tree, twelve times",
        reaches: &[member(
            "session.rs tag_tree_page_locked",
            "SELECT COUNT(DISTINCT file_id) FROM file_tags WHERE tag_norm = ?1",
            "",
            "",
        )],
        run: |session, _| {
            for _ in 0..TIMES {
                let page = session
                    .tag_tree_page(Paging::first(50), &CancelToken::new())
                    .unwrap();
                assert!(!page.entries.is_empty());
            }
        },
    },
    Flow {
        name: "a board of file cards: opened, its outline, its table",
        reaches: &[
            member(
                "session.rs DbTitleSource::title_for",
                "SELECT p.value_text FROM properties p",
                "p.key = 'title'",
                "",
            ),
            member(
                "session.rs canvas_outline",
                "SELECT node_id, depth, kind, title",
                "",
                "",
            ),
            member(
                "session.rs canvas_table_rows",
                "SELECT node_id, kind, title, group_path, target",
                "",
                "",
            ),
        ],
        run: |session, _| {
            let info = session.open_canvas("board.canvas").unwrap();
            for _ in 0..TIMES {
                assert_eq!(session.canvas_outline(info.handle).unwrap().len(), NOTES);
                assert_eq!(session.canvas_table_rows(info.handle).unwrap().len(), NOTES);
            }
            session.close_canvas(info.handle);
        },
    },
    Flow {
        name: "full-text search in a folder, and with a tag scope with and without a query",
        reaches: &[
            member(
                "search_db.rs full_text_search (folder scope)",
                "SELECT files.path, snippet(files_fts",
                "AND files.path LIKE ?4",
                "",
            ),
            member(
                "search_db.rs tag_scoped_search",
                "SELECT files.path, snippet(files_fts",
                "SELECT 1 FROM file_tags t",
                "",
            ),
            member(
                "search_db.rs tag_scoped_list_all",
                "SELECT DISTINCT files.path FROM files JOIN file_tags t",
                "",
                "",
            ),
        ],
        run: |session, _| {
            let cancel = CancelToken::new();
            for index in 0..TIMES {
                let word = format!("word{index}");
                session
                    .full_text_search(&word, &crate::SearchScope::Folder("deep".into()), &cancel)
                    .unwrap();
                let tag = crate::SearchScope::Tag(format!("t{}", index % 7));
                session.full_text_search(&word, &tag, &cancel).unwrap();
                session.full_text_search("", &tag, &cancel).unwrap();
            }
        },
    },
    Flow {
        name: "a rescan with the graph live, after twelve dangling links gain their targets",
        reaches: &[
            member(
                "links_db.rs graph_linkset_for",
                "SELECT links.target_path, links.target_raw, links.is_embed, links.is_external",
                "",
                "",
            ),
            member(
                "links_db.rs graph_inbound_rows",
                "SELECT files.path, links.target_raw, links.is_embed FROM links",
                "",
                "",
            ),
        ],
        run: |session, root| {
            session.with_graph(|_| ()).unwrap();
            for index in 0..TIMES {
                std::fs::write(
                    root.join(format!("missing{index:02}.md")),
                    format!("# Found {index}\n"),
                )
                .unwrap();
            }
            let rescan = session
                .rescan_with_progress(&CancelToken::new(), None)
                .unwrap();
            assert_eq!(rescan.files_indexed, TIMES as u64);
            assert!(session.graph_is_built());
        },
    },
    Flow {
        // The `format!` templates no reading of SQL literals could judge:
        // none of the 38, all of them held by the first assertion.
        name: "the lists: files, a folder's children, a filter, tasks, a property, a summary",
        reaches: &[],
        run: |session, _| {
            let cancel = CancelToken::new();
            let mut files = None;
            let mut children = None;
            let mut tasks = None;
            for index in 0..TIMES {
                let page = session
                    .list_files(
                        FileFilter::All,
                        Paging {
                            cursor: files,
                            limit: 2,
                        },
                        &cancel,
                    )
                    .unwrap();
                files = page.next_cursor;
                let page = session
                    .list_dir_children_page(
                        "",
                        Paging {
                            cursor: children,
                            limit: 2,
                        },
                        &cancel,
                    )
                    .unwrap();
                children = page.next_cursor;
                let page = session
                    .tasks_in_vault(
                        crate::TaskFilter::default(),
                        Paging {
                            cursor: tasks,
                            limit: 2,
                        },
                    )
                    .unwrap();
                tasks = page.next_cursor;
                session
                    .filter_files(&format!("word{index}"), None, None, &[], Paging::first(2))
                    .unwrap();
                session
                    .files_with_property("status", "open", Paging::first(2 + index as u32))
                    .unwrap();
                session
                    .files_with_property_key("status", Paging::first(2 + index as u32))
                    .unwrap();
                assert!(session.get_file_summary(&note(index)).unwrap().is_some());
            }
        },
    },
];

#[test]
fn no_statement_is_re_prepared_per_binding_on_any_driven_path() {
    let (tmp, session) = make_vault(|p| {
        let mut cards = Vec::new();
        for index in 0..NOTES {
            let body = format!(
                "---\ntitle: Title {index}\nstatus: open\ntags: [alpha, t{}]\n---\n# Note {index}\n\n\
                 ## Part A\n\nBody word{index} #inline links [[n{:02}]] and embeds ![[n{:02}]] \
                 and [[missing{index:02}]].\n\n- [ ] task {index}\n\n## Part B\n\nextra:: {index}\n",
                index % 7,
                (index + 1) % NOTES,
                (index + 2) % NOTES,
            );
            p.write_file(&note(index), body.as_bytes()).unwrap();
            cards.push(format!(
                r#"{{"id":"c{index}","type":"file","file":"{}","x":{},"y":0,"width":250,"height":60}}"#,
                note(index),
                index * 300
            ));
        }
        p.write_file("deep/in.md", b"word3 in a folder\n").unwrap();
        p.write_file(
            "board.canvas",
            format!(r#"{{"nodes":[{}],"edges":[]}}"#, cards.join(",")).as_bytes(),
        )
        .unwrap();
    });
    session.scan_initial(&CancelToken::new()).unwrap();
    // Some history for the temporal operators to read.
    for index in 0..3 {
        let path = note(index);
        let text = session.read_text(&path).unwrap();
        let saved = session
            .save_text(&path, &format!("{text}one more line\n"), None)
            .unwrap();
        session
            .save_text(
                &path,
                &text.replace("status: open", "status: done"),
                Some(&saved.new_content_hash),
            )
            .unwrap();
    }

    let mut wrong = Vec::new();
    for flow in FLOWS {
        let ((), counts) = counting_statements(&session, || (flow.run)(&session, tmp.path()));

        // The class: nothing this flow ran per binding was compiled again
        // per binding.
        for (sql, count) in &counts.0 {
            if count.runs >= TIMES as u64 && count.reprepares > 2 {
                wrong.push(format!(
                    "{}: re-prepared {} times in {} runs: {sql}",
                    flow.name, count.reprepares, count.runs
                ));
            }
        }

        // Against the vacuous pass: each listed statement ran, and ran from
        // the statement cache.
        for member in flow.reaches {
            let matching: Vec<(&String, &StatementCount)> = counts
                .0
                .iter()
                .filter(|(sql, _)| {
                    sql.starts_with(member.starts)
                        && sql.contains(member.holds)
                        && sql.ends_with(member.ends)
                })
                .collect();
            let [(sql, count)] = matching.as_slice() else {
                wrong.push(format!(
                    "{}: {} names {} of the statements the flow ran: {matching:#?}",
                    flow.name,
                    member.site,
                    matching.len()
                ));
                continue;
            };
            if count.runs < TIMES as u64 {
                wrong.push(format!(
                    "{}: {} ran {} times, not {TIMES}: {sql}",
                    flow.name, member.site, count.runs
                ));
            }
            if count.objects > 2 {
                wrong.push(format!(
                    "{}: {} was built {} times for {} runs, so it did not run from the statement \
                     cache: {sql}",
                    flow.name, member.site, count.objects, count.runs
                ));
            }
        }
    }
    assert!(
        wrong.is_empty(),
        "{} readings of a statement compiled per binding (a statement is listed once for each \
         flow that ran it):\n  {}",
        wrong.len(),
        wrong.join("\n  ")
    );
}
