# Obsidian Bases execution golden

`companion-vault/` contains the three synthetic notes that were present when
Obsidian wrote the raw captures. They are copied into a temporary filesystem
vault by `tests/bases_obsidian_e2e.rs`; the test then copies the raw `.base`
files alongside them, scans through `VaultSession`, and executes every view.

`expected.json` is the executable golden. It pins:

- `obsidian-basic.base` / `Table`: active notes in descending `priority` order,
  `Notes/Gamma.md` then `Notes/Alpha.md`, with all four displayed cell values.
- `obsidian-formulas.base` / `Table`: all five indexed files in path order.
  The three notes have `formula.weighted_total` values `16`, `3`, and `39`;
  the two raw `.base` files have empty formula cells because they do not define
  `score` or `priority`. This is intentional: `file.*` covers every indexed
  file type, not only Markdown notes.
- `obsidian-formulas.base` / `View`: the two active note paths. Obsidian did
  not write an `order` list for this view, so the capture displays no explicit
  columns in Slate and the golden intentionally pins only its rows.

The integration test runs this matrix in a warm session and after a cold
session reopen. Before and after both runs, it compares the copied captures and
the repository sources to pinned raw bytes. The SHA-256 values in
`expected.json` match `PROVENANCE.md` and are independently checked with the
command documented there.
