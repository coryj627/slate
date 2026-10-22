// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later

//! #1559: the MathML, speech and braille slate-core produces for math,
//! pinned.
//!
//! Every math block in the demo vault and in the field-pass fixture note
//! `fixtures/math_golden/MathCAT field pass.md`, and each expression in
//! `fixtures/math_golden/extra-expressions.txt`, renders through the real
//! pipeline (`render_math`) under all twelve user-selectable combinations
//! of speech style, verbosity and braille code. The result must match
//! `fixtures/math_golden/expected.txt` exactly, so a dependency bump or a
//! pipeline change that alters what screen-reader and braille users get
//! fails here, and the diff is what review reads. The MathML is pinned
//! too: Windows exposes it to screen readers through UI Automation.
//!
//! Regenerate on purpose, then review the diff:
//! `SLATE_MATH_GOLDEN_UPDATE=1 cargo test -p slate-core --test math_golden`

use std::fmt::Write as _;
use std::path::{Path, PathBuf};

use slate_core::math::{
    BrailleCode, MathDisplayStyle, MathPrefs, MathSpeechStyle, MathVerbosity, RawMathBlock,
    extract_math_blocks, render_math,
};

const UPDATE: &str = "SLATE_MATH_GOLDEN_UPDATE";

const FIELD_PASS_NOTE: &str = "MathCAT field pass.md";

const SPEECH_STYLES: [(MathSpeechStyle, &str); 2] = [
    (MathSpeechStyle::ClearSpeak, "ClearSpeak"),
    (MathSpeechStyle::SimpleSpeak, "SimpleSpeak"),
];

const VERBOSITIES: [(MathVerbosity, &str); 3] = [
    (MathVerbosity::Terse, "Terse"),
    (MathVerbosity::Medium, "Medium"),
    (MathVerbosity::Verbose, "Verbose"),
];

const BRAILLE_CODES: [(BrailleCode, &str); 2] =
    [(BrailleCode::Nemeth, "Nemeth"), (BrailleCode::Ueb, "UEB")];

fn fixture_dir() -> PathBuf {
    Path::new(env!("CARGO_MANIFEST_DIR"))
        .join("tests")
        .join("fixtures")
        .join("math_golden")
}

/// MathCAT's panic hook silences panic messages (#1583), so a failure says
/// why on stderr before it panics. Only this helper prints, and what it
/// prints is the fixture corpus and its renderings, never user content.
#[expect(
    clippy::disallowed_macros,
    reason = "libtest must show why the golden failed despite MathCAT's silent panic hook"
)]
fn fail(message: String) -> ! {
    eprintln!("{message}");
    panic!("{message}");
}

/// One golden line per field: a display block's source, and the speech
/// that falls back to it, can span lines.
fn one_line(text: &str) -> String {
    text.replace('\r', "␍").replace('\n', "⏎")
}

/// The corpus in a stable order: the demo vault's notes by path, each
/// note's blocks in order, then the field-pass fixture note's blocks, then
/// the extra expressions in file order.
/// Hidden folders (`.obsidian`, a local `.slate` cache) are not notes.
fn corpus() -> Vec<(String, String, MathDisplayStyle)> {
    let vault = Path::new(env!("CARGO_MANIFEST_DIR"))
        .join("..")
        .join("..")
        .join("demo-vault");
    let mut notes = Vec::new();
    let mut folders = vec![vault.clone()];
    while let Some(folder) = folders.pop() {
        for entry in std::fs::read_dir(&folder).expect("read a demo-vault folder") {
            let entry = entry.expect("read a demo-vault entry");
            if entry.file_name().to_string_lossy().starts_with('.') {
                continue;
            }
            let path = entry.path();
            if path.is_dir() {
                folders.push(path);
            } else if path.extension().is_some_and(|extension| extension == "md") {
                notes.push(path);
            }
        }
    }
    let relative = |path: &Path| {
        path.strip_prefix(&vault)
            .expect("a note inside the demo vault")
            .components()
            .map(|component| component.as_os_str().to_string_lossy().into_owned())
            .collect::<Vec<_>>()
            .join("/")
    };
    notes.sort_by_cached_key(|note| relative(note));

    let mut entries = Vec::new();
    for note in &notes {
        let text = std::fs::read_to_string(note).expect("read a demo-vault note");
        for (index, block) in extract_math_blocks(&text).into_iter().enumerate() {
            entries.push((
                format!("demo-vault/{} #{}", relative(note), index + 1),
                block.source,
                block.display_style,
            ));
        }
    }
    // The fixture note of the VoiceOver and NVDA field passes (#1561,
    // #1562), so every phrase they record has a golden line to match.
    let field_pass = std::fs::read_to_string(fixture_dir().join(FIELD_PASS_NOTE))
        .expect("read the field-pass fixture note");
    for (index, block) in extract_math_blocks(&field_pass).into_iter().enumerate() {
        entries.push((
            format!("{FIELD_PASS_NOTE} #{}", index + 1),
            block.source,
            block.display_style,
        ));
    }
    let extra = std::fs::read_to_string(fixture_dir().join("extra-expressions.txt"))
        .expect("read extra-expressions.txt");
    for (index, line) in extra.lines().enumerate() {
        let line = line.trim();
        if line.is_empty() || line.starts_with('#') {
            continue;
        }
        entries.push((
            format!("extra-expressions.txt:{}", index + 1),
            line.to_string(),
            MathDisplayStyle::Inline,
        ));
    }
    entries
}

/// Renders every corpus entry under all twelve combinations. The MathML
/// depends on none of the settings, speech on the speech style and
/// verbosity only, and braille on the braille code only; every
/// combination is rendered, and checked to agree with that, so each entry
/// lists its MathML, six speeches and two braille codes.
fn render_corpus() -> String {
    let mut out = String::new();
    for (origin, source, display_style) in corpus() {
        let raw = RawMathBlock {
            source: source.clone(),
            display_style,
            line: 1,
            byte_offset: 0,
        };
        let mut mathml: Option<String> = None;
        let mut speeches: Vec<(String, String)> = Vec::new();
        let mut brailles: Vec<(&str, String)> = Vec::new();
        for (speech_style, style) in SPEECH_STYLES {
            for (verbosity, level) in VERBOSITIES {
                for (braille_code, code) in BRAILLE_CODES {
                    let block = render_math(
                        &raw,
                        MathPrefs {
                            speech_style,
                            verbosity,
                            braille_code,
                        },
                    );
                    match &mathml {
                        Some(seen) if *seen != block.mathml => fail(format!(
                            "{origin}: the MathML differs between settings; \
                             the golden format assumes it does not"
                        )),
                        Some(_) => {}
                        None => mathml = Some(block.mathml.clone()),
                    }
                    let label = format!("{style} {level}");
                    match speeches.iter().find(|(seen, _)| *seen == label) {
                        Some((_, speech)) if *speech != block.speech => fail(format!(
                            "{origin}: the {label} speech differs between braille codes; \
                             the golden format assumes it does not"
                        )),
                        Some(_) => {}
                        None => speeches.push((label, block.speech)),
                    }
                    let braille = String::from_utf8_lossy(&block.braille).into_owned();
                    match brailles.iter().find(|(seen, _)| *seen == code) {
                        Some((_, seen)) if *seen != braille => fail(format!(
                            "{origin}: the {code} braille differs between speech settings; \
                             the golden format assumes it does not"
                        )),
                        Some(_) => {}
                        None => brailles.push((code, braille)),
                    }
                }
            }
        }
        writeln!(out, "=== {origin}").expect("write to a String");
        writeln!(out, "source: {}", one_line(&source)).expect("write to a String");
        writeln!(out, "MathML: {}", one_line(&mathml.unwrap_or_default()))
            .expect("write to a String");
        for (label, speech) in &speeches {
            writeln!(out, "{label}: {}", one_line(speech)).expect("write to a String");
        }
        for (code, braille) in &brailles {
            writeln!(out, "{code}: {}", one_line(braille)).expect("write to a String");
        }
    }
    out
}

/// The entries of a golden text: each `=== origin` header with its lines.
fn entries(text: &str) -> Vec<(&str, Vec<&str>)> {
    let mut entries: Vec<(&str, Vec<&str>)> = Vec::new();
    for line in text.lines() {
        if let Some(origin) = line.strip_prefix("=== ") {
            entries.push((origin, Vec::new()));
        } else if let Some((_, lines)) = entries.last_mut() {
            lines.push(line);
        }
    }
    entries
}

/// A readable account of what changed, entry by entry.
fn describe_changes(expected: &str, actual: &str) -> (usize, String) {
    let expected = entries(expected);
    let actual = entries(actual);
    let mut changed = 0;
    let mut report = String::new();
    for (origin, lines) in &actual {
        match expected.iter().find(|(seen, _)| seen == origin) {
            None => {
                changed += 1;
                writeln!(report, "added {origin}").expect("write to a String");
                for line in lines {
                    writeln!(report, "  + {line}").expect("write to a String");
                }
            }
            Some((_, old)) if old != lines => {
                changed += 1;
                writeln!(report, "changed {origin}").expect("write to a String");
                for index in 0..old.len().max(lines.len()) {
                    match (old.get(index), lines.get(index)) {
                        (Some(before), Some(after)) if before == after => {}
                        (before, after) => {
                            if let Some(before) = before {
                                writeln!(report, "  - {before}").expect("write to a String");
                            }
                            if let Some(after) = after {
                                writeln!(report, "  + {after}").expect("write to a String");
                            }
                        }
                    }
                }
            }
            Some(_) => {}
        }
    }
    for (origin, _) in &expected {
        if !actual.iter().any(|(seen, _)| seen == origin) {
            changed += 1;
            writeln!(report, "removed {origin}").expect("write to a String");
        }
    }
    (changed, report)
}

#[test]
fn math_speech_and_braille_match_the_golden_corpus() {
    let actual = render_corpus();
    let path = fixture_dir().join("expected.txt");
    if std::env::var(UPDATE).as_deref() == Ok("1") {
        std::fs::write(&path, &actual).expect("write the math golden");
        return;
    }
    let expected = std::fs::read_to_string(&path).unwrap_or_default();
    if expected != actual {
        let (changed, report) = describe_changes(&expected, &actual);
        let noun = if changed == 1 { "entry" } else { "entries" };
        fail(format!(
            "{report}\nThe math MathML, speech or braille changed for {changed} corpus {noun} \
             (above). If the change is intended, regenerate the golden with\n  {UPDATE}=1 cargo \
             test -p slate-core --test math_golden\nand review its diff: it is what screen-reader \
             and braille users will now hear and read."
        ));
    }
}
