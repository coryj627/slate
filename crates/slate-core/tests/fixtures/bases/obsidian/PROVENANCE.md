# Genuine Obsidian Bases fixture provenance

These `.base` files were written by the installed Obsidian application. They
are raw fixture bytes, not reconstructed or normalized examples.

## Capture environment

- Obsidian: 1.12.7 (`CFBundleVersion` 0.14.8)
- macOS: 26.5.1, Apple silicon
- Capture window: 2026-07-10T03:33:40Z through 2026-07-10T03:52:51Z
- Temporary vault: `/private/tmp/slate-obsidian-bases-capture-20260709`
- Finalized and copied: 2026-07-10T03:54:21Z

The temporary vault contained three synthetic Markdown notes with `status`,
`priority`, `category`, and `score` frontmatter. No user vault content was used.

## Capture steps

### `obsidian-basic.base`

1. Opened the temporary folder with Obsidian's **Open folder as vault** flow.
2. Ran **Bases: Create new base** from Obsidian's command palette.
3. Renamed the file to `obsidian-basic` with Obsidian's **Rename file** command.
4. Added the view filter `status is active` through the Bases Filter controls.
5. Displayed `file.name`, `category`, `priority`, and `status` through the
   Properties control.
6. Added a descending `priority` sort through the Sort control.
7. Closed the temporary vault window after Obsidian flushed the file.

Source path:
`/private/tmp/slate-obsidian-bases-capture-20260709/obsidian-basic.base`

SHA-256:
`0ae6455a9b4c5a6e39e48aa3291bd80669ee8735254f3e0885b26178d3149fd5`

### `obsidian-formulas.base`

1. Ran **Bases: Create new base** in the same temporary vault.
2. Renamed the clean capture to `obsidian-formulas-clean` inside Obsidian.
3. Added formula `weighted_total` with expression `score * priority` using
   Obsidian's Add formula editor.
4. Added a second Table view through the view picker.
5. Added `status is active` to the second view through the Bases Filter controls.
6. Closed the temporary vault window after Obsidian flushed the file.

Source path:
`/private/tmp/slate-obsidian-bases-capture-20260709/obsidian-formulas-clean.base`

The committed filename drops `-clean`; the byte content is unchanged.

SHA-256:
`8127ab360d98b05fb85eea33b76e93c5ad9f8b25c6efd9255a603ec6f81ccbf8`

## Copy verification

Run from the repository root:

```bash
shasum -a 256 crates/slate-core/tests/fixtures/bases/obsidian/*.base
```

The output must match the hashes above before the corpus is accepted.
