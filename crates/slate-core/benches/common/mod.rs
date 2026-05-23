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

    if seed % 5 == 0 {
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
            1 if !code_block_written && seed % 10 == 0 => {
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
