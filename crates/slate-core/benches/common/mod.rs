//! Synthetic vault fixture for benchmarks.
//!
//! `generate_vault(n)` lays out `n` Markdown files across ~50 sub-
//! directories under a `TempDir`. File content is deterministic per
//! seed (so re-running benchmarks compares apples-to-apples) and
//! shaped roughly like real notes:
//!
//! - Mixed size distribution: ~60 % files in the 500 B – 2 KB range,
//!   ~30 % in 5 – 20 KB, ~10 % in 50 – 200 KB.
//! - Every file starts with a `# Title` H1; longer files mix `##`
//!   subheadings, paragraphs, and the occasional code block.
//! - ~20 % of files carry a YAML frontmatter block — enough exposure
//!   to flush out parser handling without dominating the distribution.
//!
//! No external RNG. Each file's content is derived from its index
//! via simple modular math, which keeps the fixture cheap to ship
//! (no new deps) and trivially reproducible.

use std::fs;
use std::path::PathBuf;

use tempfile::TempDir;

/// Word pool the generator draws from when filling paragraphs. ~60
/// short common words mixed with longer ones so paragraph length is
/// realistic without dragging in a lorem-ipsum crate.
const WORDS: &[&str] = &[
    "the",
    "quick",
    "brown",
    "fox",
    "jumps",
    "over",
    "lazy",
    "dog",
    "lorem",
    "ipsum",
    "dolor",
    "sit",
    "amet",
    "consectetur",
    "adipiscing",
    "elit",
    "sed",
    "do",
    "eiusmod",
    "tempor",
    "incididunt",
    "ut",
    "labore",
    "et",
    "dolore",
    "magna",
    "aliqua",
    "enim",
    "ad",
    "minim",
    "veniam",
    "quis",
    "nostrud",
    "exercitation",
    "ullamco",
    "laboris",
    "nisi",
    "aliquip",
    "ex",
    "ea",
    "commodo",
    "consequat",
    "duis",
    "aute",
    "irure",
    "in",
    "reprehenderit",
    "voluptate",
    "velit",
    "esse",
    "cillum",
    "fugiat",
    "nulla",
    "pariatur",
    "excepteur",
    "sint",
    "occaecat",
    "cupidatat",
    "non",
    "proident",
];

/// Build a synthetic vault of `file_count` Markdown files. Returns
/// the owning `TempDir` so the caller controls its lifetime.
pub fn generate_vault(file_count: usize) -> TempDir {
    let tmp = tempfile::tempdir().expect("create tempdir for synthetic vault");
    // Spread files across subdirectories so the scanner exercises
    // tree traversal, not just one big flat directory.
    let subdir_count = (file_count / 100).clamp(1, 50);

    let mut last_dir: Option<PathBuf> = None;
    for i in 0..file_count {
        let subdir = format!("notes/{:03}", i % subdir_count);
        let dir = tmp.path().join(&subdir);
        // Avoid an mkdir call per file when the previous file used
        // the same subdir.
        if last_dir.as_deref() != Some(dir.as_path()) {
            fs::create_dir_all(&dir).expect("create subdir");
            last_dir = Some(dir.clone());
        }

        let content = synthetic_markdown(i);
        let path = dir.join(format!("note-{:08}.md", i));
        fs::write(&path, content).expect("write synthetic note");
    }

    tmp
}

/// Generate one note's contents, sized according to a 60/30/10 mix
/// of small / medium / large files.
fn synthetic_markdown(seed: usize) -> String {
    let target_size = target_size_for(seed);
    let mut out = String::with_capacity(target_size + 256);

    if seed.is_multiple_of(5) {
        out.push_str("---\n");
        out.push_str(&format!("title: Note {seed}\n"));
        out.push_str(&format!("tags: [bench, file-{}]\n", seed % 7));
        out.push_str("---\n\n");
    }

    out.push_str(&format!("# Note {seed}\n\n"));

    let mut sub_idx: u32 = 1;
    let mut code_block_written = false;
    while out.len() < target_size {
        let kind = (out.len().wrapping_add(seed)) % 8;
        match kind {
            0 => {
                out.push_str(&format!("## Section {sub_idx}\n\n"));
                sub_idx += 1;
            }
            1 if !code_block_written && seed.is_multiple_of(10) => {
                out.push_str("```rust\nfn hello() {\n    println!(\"world\");\n}\n```\n\n");
                code_block_written = true;
            }
            _ => {
                let para_len = 200 + (out.len() % 600);
                let mut para = String::with_capacity(para_len);
                let mut wi = (out.len().wrapping_add(seed)) % WORDS.len();
                while para.len() < para_len {
                    para.push_str(WORDS[wi]);
                    para.push(' ');
                    wi = (wi + 7) % WORDS.len();
                }
                out.push_str(para.trim_end());
                out.push_str("\n\n");
            }
        }
    }

    out
}

fn target_size_for(seed: usize) -> usize {
    match seed % 10 {
        0..=5 => 500 + (seed % 1_500),    // ~60% small
        6..=8 => 5_000 + (seed % 15_000), // ~30% medium
        _ => 50_000 + (seed % 150_000),   // ~10% large
    }
}

// =====================================================================
// Tasks fixture (Milestone G #115, refreshed #146)
// =====================================================================

/// Build a vault of `file_count` Markdown files with a
/// realistic-vault task distribution. Returns the owning `TempDir`.
///
/// **Distribution rationale.** Real-world Obsidian vaults from
/// casual users skew heavily toward notes that contain **no
/// tasks at all** (daily-journal entries, reference material,
/// captured-text snippets). A smaller chunk carries one or two
/// task lines embedded inline in body text (action items
/// captured while reading). A few notes are dedicated to-do
/// lists with a heavier task block.
///
/// The original `generate_tasks_vault` shape (every file gets a
/// uniform `## Tasks` block with 10 task lines) was useful for
/// stress-testing the parser hot path but **over-counted the
/// per-file parse cost** vs. the typical workload. After the
/// `extract_tasks` fast path landed (#144), most files in a real
/// vault skip the pulldown-cmark walk entirely — a benefit the
/// uniform fixture couldn't measure.
///
/// Current shape:
/// - **~70% zero-task files.** Body content (frontmatter,
///   headings, paragraphs, occasional code fence) with no
///   task lines. Exercises the M3 fast-path return.
/// - **~25% light files.** 1–3 tasks scattered through body
///   paragraphs (not bunched in a `## Tasks` section).
///   Exercises the parser's mid-document line-walk path.
/// - **~5% heavy files.** 10–15 tasks in a dedicated `## Tasks`
///   block. Exercises the bulk-insert path of
///   `replace_tasks_for_file`.
///
/// File category is chosen deterministically via `seed % 100`,
/// so re-running the bench compares apples-to-apples across
/// commits.
pub fn generate_tasks_vault(file_count: usize) -> TempDir {
    let tmp = tempfile::tempdir().expect("create tempdir for tasks vault");
    let subdir_count = (file_count / 100).clamp(1, 50);
    let mut last_dir: Option<PathBuf> = None;
    for i in 0..file_count {
        let subdir = format!("notes/{:03}", i % subdir_count);
        let dir = tmp.path().join(&subdir);
        if last_dir.as_deref() != Some(dir.as_path()) {
            fs::create_dir_all(&dir).expect("create subdir");
            last_dir = Some(dir.clone());
        }
        let path = dir.join(format!("note-{i:08}.md"));
        fs::write(&path, synthetic_note_realistic(i)).expect("write tasks note");
    }
    tmp
}

/// Realistic-distribution note body. `seed % 100` picks the
/// category (zero / light / heavy); the seed also drives the
/// per-file content shape so the result is byte-stable across
/// runs.
fn synthetic_note_realistic(seed: usize) -> String {
    match seed % 100 {
        // 0..70 — zero-task. Reuse the regular `synthetic_markdown`
        // shape so the cold-scan path sees real frontmatter +
        // headings + paragraphs + code fences (≈ Apple-Notes-import
        // territory).
        0..=69 => synthetic_markdown(seed),
        // 70..95 — light. 1–3 tasks scattered through body
        // paragraphs.
        70..=94 => synthetic_light_tasks_note(seed),
        // 95..99 — heavy. 10–15 tasks in a dedicated `## Tasks`
        // block.
        _ => synthetic_heavy_tasks_note(seed),
    }
}

/// Body with 1–3 task lines sprinkled between paragraphs.
fn synthetic_light_tasks_note(seed: usize) -> String {
    // Deterministic task count based on the seed so the
    // distribution stays apples-to-apples across re-runs.
    let task_count = 1 + (seed % 3); // 1, 2, or 3 tasks
    let mut out = String::with_capacity(800);
    out.push_str(&format!("# Note {seed}\n\n"));
    out.push_str(
        "Some captured reading notes follow. The task list below \
         is intentionally mixed with prose so the parser walks the \
         full body, not a dedicated section.\n\n",
    );
    out.push_str(&format!(
        "## Section\n\nA paragraph about topic {}.\n\n",
        seed % 7
    ));
    for j in 0..task_count {
        out.push_str(&task_line(seed, j));
        out.push('\n');
        out.push_str(&format!(
            "Follow-up paragraph after task {j}. More prose to keep \
             the parser scanning past the task line.\n\n"
        ));
    }
    out.push_str("Final paragraph closing the note.\n");
    out
}

/// Body with a dedicated `## Tasks` block carrying 10–15 task
/// lines.
fn synthetic_heavy_tasks_note(seed: usize) -> String {
    let task_count = 10 + (seed % 6); // 10..=15
    let mut out = String::with_capacity(task_count * 64 + 256);
    out.push_str(&format!(
        "# Note {seed}\n\nProject overview paragraph.\n\n## Tasks\n\n"
    ));
    for j in 0..task_count {
        out.push_str(&task_line(seed, j));
        out.push('\n');
    }
    out.push_str("\n## Notes\n\nWrap-up paragraph after the task block.\n");
    out
}

/// One task line with deterministic metadata shape. Mix of
/// open / done / in-progress, ~50% with due date, ~25% with
/// priority, ~10% with recurrence — same statistical mix the
/// previous fixture used, just applied selectively.
fn task_line(seed: usize, j: usize) -> String {
    const STATUSES: &[char] = &[' ', ' ', ' ', ' ', ' ', 'x', 'x', 'x', 'x', '/'];
    let status = STATUSES[(seed + j) % STATUSES.len()];
    let mut line = format!("- [{status}] task {seed}-{j}");
    if (seed + j).is_multiple_of(2) {
        let day = ((seed + j) % 30) + 1;
        line.push_str(&format!(" 📅 2026-06-{day:02}"));
    }
    match (seed + j) % 8 {
        0 => line.push_str(" ⏫"),
        3 => line.push_str(" 🔼"),
        _ => {}
    }
    if (seed + j).is_multiple_of(10) {
        line.push_str(" 🔁 every week");
    }
    line
}
