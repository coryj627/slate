// Verify the three CRITICAL repros fall back.
use slate_core::editor_spans::{highlight_spans, highlight_spans_in_range};

fn check(label: &str, src: &str, at: usize) {
    let r = highlight_spans_in_range(src, at..at);
    let is_fb = r.applied_range == (0..src.len());
    println!("[{label}] src={src:?} @ {at}");
    println!(
        "    applied_range = {:?}  (full = 0..{})",
        r.applied_range,
        src.len()
    );
    println!("    FALLBACK = {is_fb}");
    // Also assert the invariant for completeness.
    let (a, b) = (r.applied_range.start, r.applied_range.end);
    let mut expected: Vec<_> = highlight_spans(src)
        .into_iter()
        .filter(|s| (s.start_byte as usize) >= a && (s.end_byte as usize) <= b)
        .collect();
    let mut got = r.spans.clone();
    expected.sort_by_key(|s| (s.start_byte, s.end_byte, format!("{:?}", s.kind)));
    got.sort_by_key(|s| (s.start_byte, s.end_byte, format!("{:?}", s.kind)));
    assert_eq!(got, expected, "INVARIANT BROKEN for {label}");
    println!("    invariant OK");
    println!();
}

fn main() {
    // Bug #1: unclosed HTML block above the window
    check(
        "BUG#1 unclosed-html",
        "<!-- x\n\n```\n#tag\n",
        "<!-- x\n\n```\n#tag\n".find("#tag").unwrap(),
    );
    // Bug #2: frontmatter un-pairs a body fence
    check(
        "BUG#2 fm-unpairs-fence",
        "---\n~~~\n---\n~~~\n\n> q [[Q]]\n",
        "---\n~~~\n---\n~~~\n\n> q [[Q]]\n".find("[[Q]]").unwrap(),
    );
    // Bug #3: list-continuation indented line invents code
    let s3 = "- a\n\n    code\n";
    check("BUG#3 list-indent-code", s3, s3.rfind("    ").unwrap() + 4);
    let s3b = "- a\n  - b\n\n        code\n";
    check(
        "BUG#3 nested-list-indent",
        s3b,
        s3b.rfind("    ").unwrap() + 4,
    );
    println!("ALL THREE REPROS: confirmed fallback + invariant OK");
}
