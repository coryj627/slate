# Parity research notes (from user captures, 2026-11-01)

Competitor = the market-leading notes app (captures v1.14.4). Describe features generically; do not copy its UI.

## Competitor: where features are exposed
- Left action strip: quick switcher, graph view, new canvas, today's daily note, insert template, command palette, new base.
- Left pane views (switcher): Files, Search, Bookmarks.
- Files toolbar: new note, new folder, change sort order, auto-reveal current file, collapse/expand all. Tree shows type badges for non-note files (BASE, CANVAS), indent guides.
- Bottom of left pane: vault switcher (current vault ✓, Manage vaults…), help, settings.
- Tab strip: new tab, tab list menu (stack tabs, bookmark N tabs, close all, open tabs ✓), toggle right pane.
- Note header: back/forward, breadcrumb folder/note, view-mode toggle (tooltip: current view; click to read; Ctrl+click opens to the right), ⋯ more.
- ⋯ more menu: backlinks in document, reading view, source mode | split right, split down, open in new window | rename, move to, bookmark, merge file with, add property, export to PDF | find, replace | copy path › | version history, open linked view › | open in default app, show in system explorer, reveal in navigation | delete.
- Right pane views (icon strip): backlinks, outgoing links, tags, all properties, outline. Tooltip = view + note name.
  - Backlinks: linked mentions (count), unlinked mentions; tools: collapse results, more context, sort, search.
  - Tags: counts, nested tags tree; tools: sort, nested toggle, collapse/expand, search.
  - All properties: list with type icons + usage counts; tools: sort, search.
  - Outline: heading tree; tools: search, collapse/expand.
- Status bar: backlinks count, properties count, view-mode icon, words, characters, sync status (popover: state + Settings).
- Window states captured: standard, full screen, narrow.
- Empty tab: New note (Ctrl+N), New base, New canvas, Go to file (Ctrl+O).
- Note: inline title; properties block (title, tag pills removable, + Add property); syntax revealed at cursor; spellcheck squiggles; code blocks with language label, highlighting, indent guides, fold arrows.
- Base: view picker + result count; Sort, Group, Filter, Properties, Search, New; typed column icons; group headers; summary rows (average).
- Canvas: settings, zoom in/reset/fit/out, undo/redo, help; bottom tray add card / add note / add media; labelled groups; coloured cards; edges with arrows; missing-file card actions New note / Swap file / Remove; web embeds.
- First run: create vault, open folder as vault, open synced vault (sign in), language, help, quick start.
- Settings window: search; General, Appearance, Interface, Editor, Files and links, Hotkeys, Keychain, Core plugins, Community plugins; per-feature pages (Backlinks, Canvas, Command palette, Daily notes, File recovery, Note composer, Page preview, Quick switcher, Sync, Templates). General: version, check updates, changelog, auto-update, language, help, account, license, advanced.

## Slate today (captures 2026-09-20 + repo coryj627/slate apps/slate-windows)
- Menus: File, Workspace, Base, Editor, Canvas, Graph, Files.
- Editor menu: Activate at Cursor (Ctrl+Enter), Preview Embed (Ctrl+E), Toggle Reading Mode (Ctrl+Shift+E), Add Property…, Bulk Rename Properties… (Ctrl+Shift+R), Citation Summary (Ctrl+Shift+J), Jump to Bibliography (Ctrl+J), Reload Bibliography, Check Spelling While Typing, Open Reading Links in New Tab ✓, Math ›, Code Preamble Verbosity ›, Zoom In/Out/Actual Size.
- Vault header: name + path. Files pane: refresh, filter, sort (Name A to Z), Group dates, Tags, Dual pane, tree with checkboxes + word counts, selection count, Batch actions / File actions / Shortcuts expanders, top-level count.
- Right pane (17 text tabs): Outline, Backlinks, Outgoing links, Connections, Graph inspector, Embeds, Math, Code, Diagrams, Tasks, Tasks Review, History, Citations, Bibliography, Queries, Base dock, Sync.
- Graph inspector: filter by name; attachments / unresolved / orphans only; groups (add); arrows, text fade, node size, link thickness; center, repel, link force, link distance.
- Status bar: scan status + progress only.
- Help docs: docs/help/bases.md, canvas.md, graph.md, sidebar.md.
- Menu capture: no MenuItem style in repo (searched Themes + src) → WPF built-in template; dim rows = disabled. Issues: shortcut text doesn't dim with its row; submenu rows (Math, Code Preamble Verbosity) start left of the others; settings toggles and zoom disabled when no note is active.
