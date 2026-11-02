/* Right pane: PaneSwitcher over Slate's 17 views (the rest in More panels), a PanelToolbar, then the open view.
   Backlinks, Outgoing links, Outline and Citations are recreated; the other views show a note saying so. */
const SLATE_BACKLINKS = [
  ['Linear algebra lecture 4', 28, '…won\u2019t make sense without [[Linear algebra lecture 3]] in place.'],
  ['Linear algebra glossary', 22, 'Eigenvalues are introduced in [[Linear algebra lecture 3]].'],
  ['2026-05-27', 27, 'Wrote up [[Linear algebra lecture 3]] after class.'],
];
const SLATE_OUTLINE = [['Linear algebra lecture 3', 0], ['Where we are in the course', 1], ['Eigenvalues and eigenvectors', 1], ['Why this matters', 1], ['Reading for next time', 1]];
function RightPane({ onOpen }) {
  const [view, setView] = React.useState('backlinks');
  const [q, setQ] = React.useState('');
  const [sort, setSort] = React.useState('name');
  const [openRows, setOpenRows] = React.useState({ 'Linear algebra lecture 4': true });
  const [outlineOpen, setOutlineOpen] = React.useState(true);
  const { PaneSwitcher, PanelToolbar, CountListRow } = SLATE_NS;
  const panel = SLATE_PANELS.find((p) => p.id === view);
  const hit = (s) => !q || s.toLowerCase().includes(q.toLowerCase());
  const links = SLATE_BACKLINKS.filter(([t, , s]) => hit(t) || hit(s)).sort((a, b) => (sort === 'name' ? a[0].localeCompare(b[0]) : b[1] - a[1]));
  const allOpen = SLATE_BACKLINKS.every(([t]) => openRows[t]);
  const snippet = { margin: '0 8px 6px 34px', padding: '6px 10px', borderRadius: 6, background: 'var(--surface-card)', border: '1px solid var(--border-subtle)', font: '13px/20px var(--font-reader)', color: 'var(--text-secondary)' };
  const empty = (t) => <p style={{ margin: '4px 12px', font: '14px/20px var(--font-ui)', color: 'var(--text-secondary)' }}>{t}</p>;
  const tools = {
    backlinks: { count: links.length, sortOptions: [{ value: 'name', label: 'Note name' }, { value: 'modified', label: 'Modified (newest first)' }], sortValue: sort, onSortChange: setSort, expanded: allOpen,
      onExpandedChange: (v) => setOpenRows(Object.fromEntries(SLATE_BACKLINKS.map(([t]) => [t, v]))) },
    outgoing: { count: 4 },
    outline: { expanded: outlineOpen, onExpandedChange: setOutlineOpen },
    citations: { count: 1 },
  }[view] || { search: false };
  return <aside aria-label="Right pane" style={{ width: 'var(--rightpane-width)', flex: 'none', display: 'flex', flexDirection: 'column', background: 'var(--surface-window)', borderLeft: '1px solid var(--border-subtle)', minHeight: 0 }}>
    <div style={{ padding: '4px 8px', borderBottom: '1px solid var(--border-subtle)', flex: 'none' }}>
      {PaneSwitcher && <PaneSwitcher items={SLATE_PANELS} value={view} onChange={(v) => { setView(v); setQ(''); }} label="Right pane views" />}
    </div>
    <div role="tabpanel" aria-label={panel.label} style={{ flex: 1, minHeight: 0, display: 'flex', flexDirection: 'column' }}>
      {PanelToolbar && <PanelToolbar key={view} title={panel.label} query={q} onQueryChange={setQ} {...tools} />}
      <div style={{ flex: 1, overflow: 'auto', padding: '0 4px 8px' }}>
        {view === 'backlinks' && <div role="tree" aria-label="Backlinks">
          {links.length === 0 && empty('No backlinks match \u201c' + q + '\u201d.')}
          {links.map(([t, , s]) => <React.Fragment key={t}>
            <CountListRow icon="E8A5" label={t} count={1} countLabel="mention" expanded={!!openRows[t]} onToggle={(v) => setOpenRows((o) => ({ ...o, [t]: v }))} onClick={() => onOpen(t)} />
            {openRows[t] && <div style={snippet}>{s}</div>}
          </React.Fragment>)}
        </div>}
        {view === 'outgoing' && <div role="listbox" aria-label="Outgoing links">
          {['Linear algebra glossary', 'Linear algebra lecture 2', 'Linear algebra lecture 4'].filter(hit).map((t) => <CountListRow key={t} role="option" icon="E8A5" label={t} onClick={() => onOpen(t)} />)}
          {hit('Linear algebra supplementary') && <div style={{ display: 'flex', alignItems: 'center', gap: 6, height: 32, padding: '0 8px 0 4px', font: '14px/20px var(--font-ui)', color: 'var(--status-error)' }}>
            <SIcon k="warning" size={16} />Linear algebra supplementary<span style={{ color: 'var(--text-tertiary)', fontSize: 12 }}>· unresolved</span></div>}
        </div>}
        {view === 'outline' && SLATE_OUTLINE.filter(([t, d]) => (outlineOpen || d === 0) && hit(t)).map(([t, d], i) => {
          const on = t === 'Eigenvalues and eigenvectors';
          return <div key={t} style={{ position: 'relative', display: 'flex', alignItems: 'center', gap: 8, height: 32, padding: `0 8px 0 ${12 + d * 16}px`, borderRadius: 4, cursor: 'pointer', font: '14px/20px var(--font-ui)', background: on ? 'var(--surface-selected)' : 'transparent' }}>
            {on && <span style={{ position: 'absolute', left: 0, top: 8, bottom: 8, width: 3, borderRadius: 2, background: 'var(--accent-indicator)' }} />}
            <span style={{ color: 'var(--text-tertiary)', font: '12px/20px var(--font-editor)', width: 22 }}>H{d + 1}</span>{t}</div>;
        })}
        {view === 'citations' && <>
          <div style={{ margin: '0 4px', padding: '10px 12px', borderRadius: 6, background: 'var(--surface-card)', border: '1px solid var(--border-subtle)', font: '13px/20px var(--font-reader)' }}>
            <div style={{ font: '600 13px/20px var(--font-ui)' }}>strang2016</div>Strang, G. (2016). <i>Introduction to Linear Algebra</i> (5th ed.), ch. 6. Wellesley-Cambridge Press.</div>
          <div style={{ padding: '8px 8px', color: 'var(--text-tertiary)', font: '12px/16px var(--font-ui)' }}>Ctrl+Shift+J for the citation summary · Ctrl+J jumps to the bibliography</div>
        </>}
        {!['backlinks', 'outgoing', 'outline', 'citations'].includes(view) && empty(panel.label + ' isn\u2019t recreated in this kit.')}
      </div>
    </div>
  </aside>;
}
function CommandPalette({ open, onClose, onRun, theme, onTheme }) {
  const [q, setQ] = React.useState(''); const [sel, setSel] = React.useState(0); const [mode, setMode] = React.useState('commands');
  const ref = React.useRef(null);
  React.useEffect(() => { if (open) { setQ(''); setSel(0); setMode('commands'); setTimeout(() => ref.current && ref.current.focus(), 0); } }, [open]);
  if (!open) return null;
  const themes = mode === 'theme';
  const list = themes ? SLATE_THEMES.map(t => [t.label, '', '', t.value]) : SLATE_COMMANDS;
  const items = list.filter(c => c[0].toLowerCase().includes(q.toLowerCase()));
  const pick = c => { if (themes) return onTheme(c[3]); if (c[0] === 'Switch Theme…') { setMode('theme'); setQ(''); setSel(Math.max(0, SLATE_THEMES.findIndex(t => t.value === theme))); ref.current && ref.current.focus(); return; } onRun(c[0]); };
  const key = e => { if (e.key === 'Escape') onClose(); if (e.key === 'ArrowDown') { e.preventDefault(); setSel(s => Math.min(s + 1, items.length - 1)); } if (e.key === 'ArrowUp') { e.preventDefault(); setSel(s => Math.max(s - 1, 0)); } if (e.key === 'Enter' && items[sel]) pick(items[sel]); };
  return <div onClick={onClose} style={{ position: 'absolute', inset: 0, background: 'var(--scrim)', display: 'flex', justifyContent: 'center', alignItems: 'flex-start', paddingTop: 72, zIndex: 50 }}>
    <div role="dialog" aria-label="Command palette" onClick={e => e.stopPropagation()} style={{ width: 600, maxWidth: '90%', background: 'var(--surface-card)', borderRadius: 8, boxShadow: 'var(--shadow-28)', border: '1px solid var(--border-subtle)', overflow: 'hidden' }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: 10, padding: '0 14px', height: 48, borderBottom: '1px solid var(--border-subtle)' }}>
        <SIcon k="search" size={20} style={{ color: 'var(--text-secondary)' }} />
        <input ref={ref} value={q} onChange={e => { setQ(e.target.value); setSel(0); }} onKeyDown={key} placeholder={themes ? 'Select a theme' : 'Type a command'} aria-label={themes ? 'Theme' : 'Command'}
          style={{ flex: 1, border: 'none', outline: 'none', background: 'transparent', font: '16px/22px var(--font-ui)', color: 'var(--text-primary)' }} />
        <Kbd>Esc to close</Kbd>
      </div>
      <div role="listbox" style={{ maxHeight: 360, overflow: 'auto', padding: 4 }}>
        {items.length === 0 && <div style={{ padding: 16, font: '14px var(--font-ui)', color: 'var(--text-secondary)' }}>No commands match “{q}”.</div>}
        {items.map((c, i) => <div key={c[0]} role="option" aria-selected={i === sel} aria-label={themes && c[3] === theme ? c[0] + ', current theme' : undefined} onMouseEnter={() => setSel(i)} onClick={() => pick(c)}
          style={{ position: 'relative', display: 'flex', alignItems: 'center', gap: 12, height: 36, padding: '0 12px', borderRadius: 4, cursor: 'pointer', background: i === sel ? 'var(--surface-selected)' : 'transparent', font: '14px/20px var(--font-ui)' }}>
          {i === sel && <span style={{ position: 'absolute', left: 0, top: 9, bottom: 9, width: 3, borderRadius: 2, background: 'var(--accent-indicator)' }} />}
          {themes && <span aria-hidden="true" style={{ width: 16, flex: 'none', fontFamily: 'var(--font-icons)', fontSize: 16, lineHeight: '16px' }}>{c[3] === theme ? String.fromCharCode(0xE73E) : ''}</span>}
          <span style={{ flex: 1 }}>{c[0]}</span><span style={{ font: '12px var(--font-ui)', color: 'var(--text-tertiary)' }}>{c[2]}</span><span style={{ width: 96, textAlign: 'right' }}><Kbd>{c[1]}</Kbd></span></div>)}
      </div>
    </div>
  </div>;
}
Object.assign(window, { RightPane, CommandPalette });
