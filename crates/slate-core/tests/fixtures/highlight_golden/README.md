# Highlight golden: malformed code

`crates/slate-core/tests/highlight_golden.rs` pins how `highlight_code` classifies **malformed** code (#1576).

- **`malformed.txt`** is the corpus. Each snippet starts with a `=== <language> #<n>` header line. Highlighting appends a newline to each snippet, as a fenced block's body always ends with one.
  - The corpus has at least one snippet for every language in `code::highlighted_language_tags()`; `the_malformed_corpus_covers_every_highlighted_language` checks that.
  - `every_alias_highlights_like_its_language` checks that each alias (`rs`, `ts`, `c++`, …) reads exactly like the language's own tag.
- **`expected.txt`** is the golden. For each snippet it holds the header, the source as highlighted (newlines shown as `⏎`), one `token <kind> <start>..<end> <text>` line per token, and one `semantic <kind> <start>..<end> <name>` line per semantic span.

Broken code gets its classes from each grammar's error recovery, so a tree-sitter or grammar bump can change them without any well-formed test noticing.

- `c #5`–`#7` are inputs whose class a reader hears changed between tree-sitter 0.26.13 and 0.27.1 (#1540). They came from fuzzing the two versions, and the golden fails on 0.26.13.
- `javascript #3` is an input where 0.27 renamed the node (`shorthand_property_identifier_pattern` → `shorthand_property_identifier`). Both names map to `Identifier`, so a reader hears no change, and the golden shows none.

**A diff here is a behaviour change for malformed code.** When a bump changes it on purpose, regenerate the golden:

```
SLATE_HIGHLIGHT_GOLDEN_UPDATE=1 cargo test -p slate-core --test highlight_golden
```

Then review the diff, and say in the PR which snippets changed and what a reader now hears for them. The failure names each snippet by its header. To add a snippet, append a block with a new header, regenerate, and review the new block.
