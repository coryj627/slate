/* Title bar (title + app menu + quick commands + caption buttons in one row), status bar.
   Menus mirror MainWindow.xaml. In WPF this row is built with WindowChrome (see readme → Window chrome). */
const SLATE_MENUS = {
  File: ['Open Vault…\tCtrl+Shift+O', 'Quick Open…\tCtrl+O', '-', 'New Note from Template…\tCtrl+Shift+N', 'New Canvas', '-', 'Close Vault', '-', 'Files Sidebar\t›', '-', 'Exit'],
  Workspace: ['Duplicate Tab\tCtrl+T', 'Close Tab\tCtrl+W', 'Reopen Closed Tab\tCtrl+Shift+T', '-', 'Split Right\tCtrl+\\', 'Split Down\tCtrl+Alt+\\', '-', 'Toggle Files Sidebar', 'Toggle Right Pane\tCtrl+Alt+I', 'Search Vault…\tCtrl+Shift+F', 'Show Tasks Review\tCtrl+R', 'Show History Panel'],
  Base: ['Refresh', 'Where Am I?', '-', 'Next View', 'Previous View', 'View as Table', 'View as List'],
  Editor: ['Find…\tCtrl+F', 'Activate at Cursor', 'Preview Embed', 'Toggle Reading Mode\tCtrl+Shift+E', '-', 'Add Property…', 'Bulk Rename Properties…\tCtrl+Shift+R', 'Citation Summary\tCtrl+Shift+J', 'Jump to Bibliography\tCtrl+J', '-', '✓ Check Spelling While Typing', 'Math\t›', '-', 'Zoom In', 'Zoom Out', 'Actual Size'],
  Canvas: ['Where Am I?'], Graph: ['Open Graph'], Help: ['Keyboard Shortcuts', 'About Slate'],
};
const SLATE_SUBMENUS = {
  'Files Sidebar': ['Open', 'Open in New Tab', 'Open in Split', '-', 'Import Files and Folders…', 'Cancel Import', '-', 'Refresh', 'Collapse All', 'Expand Loaded', 'Focus Filter\tCtrl+Alt+F', 'Back\tCtrl+Alt+[', 'Forward\tCtrl+Alt+]', '-', 'Pin Note', 'Unpin Note', 'Unpin All in Folder', 'Add to Shortcuts', 'Remove Shortcut', 'Use Vault Default Sort', 'Clear Recents'],
  Math: ['✓ Speech Style: ClearSpeak', 'Speech Style: SimpleSpeak', '-', 'Verbosity: Terse', '✓ Verbosity: Medium', 'Verbosity: Verbose', '-', '✓ Braille: Nemeth', 'Braille: UEB'],
};
const MNEMONIC = { File: 'f', Workspace: 'w', Base: 'b', Editor: 'e', Canvas: 'n', Graph: 'g', Help: 'h' };
const TOP_MENUS = Object.keys(SLATE_MENUS);
const parseItem = (it) => {
  if (it === '-') return { sep: true };
  const [raw, c] = it.split('\t'); const label = raw.replace('✓ ', '');
  return { label, checked: raw.startsWith('✓ '), sub: c === '›' ? (SLATE_SUBMENUS[label] || []).map(parseItem) : null, chord: c === '›' ? '' : (c || '') };
};
const firstSel = (list) => list.findIndex((e) => !e.sep);

function MenuList({ label, entries, active, onHover, onPick, flyout, width }) {
  const MI = SLATE_NS.MenuItem; const checks = entries.some((e) => e.checked);
  return <div role="menu" aria-label={label} style={{ width, padding: 4, borderRadius: 8, background: 'var(--neutral-background-1-rest)', border: '1px solid var(--border-subtle)', boxShadow: 'var(--shadow-16)', boxSizing: 'border-box' }}>
    {entries.map((e, i) => e.sep ? <div key={i} role="separator" style={{ height: 1, background: 'var(--border-subtle)', margin: '4px 0' }} /> :
      <div key={i} role={checks ? 'menuitemcheckbox' : 'menuitem'} aria-checked={checks ? e.checked : undefined} aria-haspopup={e.sub ? 'menu' : undefined} aria-expanded={e.sub ? (i === active && !!flyout) : undefined}
        onMouseEnter={() => onHover(i)} onClick={(ev) => { ev.stopPropagation(); onPick(i); }} style={{ position: 'relative', cursor: 'pointer' }}>
        {MI ? <MI style={{ width: '100%' }} icon={false} primaryString={e.label} checkmark={checks} selected={e.checked} chevron={!!e.sub} endContent={!!e.chord} endContentString={e.chord} state={i === active ? 'hover' : 'rest'} /> : e.label}
        {i === active && flyout && <div style={{ position: 'absolute', left: 'calc(100% + 2px)', top: -5, zIndex: 1 }}>{flyout}</div>}
      </div>)}
  </div>;
}

/* Logo button → cascading app menu. Alt+F opens it (Alt+W/B/E/N/G/H open at that menu). */
function AppMenu({ onCommand }) {
  const [open, setOpen] = React.useState(false);
  const [path, setPath] = React.useState([0, -1, -1]);
  const [level, setLevel] = React.useState(0);
  const [h, setH] = React.useState(false);
  const btn = React.useRef(null);
  const l0 = TOP_MENUS.map((n) => ({ label: n, sub: SLATE_MENUS[n].map(parseItem), chord: 'Alt+' + MNEMONIC[n].toUpperCase() }));
  const l1 = l0[path[0]].sub;
  const l1item = path[1] >= 0 ? l1[path[1]] : null;
  const l2 = l1item && l1item.sub ? l1item.sub : null;
  const close = () => { setOpen(false); setLevel(0); btn.current && btn.current.focus(); };
  const openAt = (i) => { setOpen(true); setPath([i, -1, -1]); setLevel(0); };
  const pick = (e) => { close(); onCommand && onCommand(e.label); };
  const enter = (lv, p) => {
    if (lv === 0) { setPath([p[0], firstSel(l0[p[0]].sub), -1]); setLevel(1); }
    else if (lv === 1) { const it = l0[p[0]].sub[p[1]]; if (it && it.sub) { setPath([p[0], p[1], firstSel(it.sub)]); setLevel(2); } else if (it) pick(it); }
    else if (lv === 2 && l2 && l2[p[2]]) pick(l2[p[2]]);
  };
  React.useEffect(() => {
    const k = (ev) => { if (!ev.altKey || ev.ctrlKey || ev.shiftKey) return; const i = TOP_MENUS.findIndex((n) => MNEMONIC[n] === ev.key.toLowerCase()); if (i >= 0) { ev.preventDefault(); openAt(i); } };
    window.addEventListener('keydown', k); return () => window.removeEventListener('keydown', k);
  }, []);
  React.useEffect(() => {
    if (!open) return;
    const k = (ev) => {
      const lists = [l0, l1, l2]; const cur = lists[level] || []; const idx = path[level];
      const move = (d) => { const s = cur.map((e, i) => (e.sep ? -1 : i)).filter((i) => i >= 0); let p = s.indexOf(idx); p = p < 0 ? (d > 0 ? 0 : s.length - 1) : (p + d + s.length) % s.length; const np = [...path]; np[level] = s[p]; for (let j = level + 1; j < 3; j++) np[j] = -1; setPath(np); };
      const back = () => { const np = [...path]; np[level] = -1; setPath(np); setLevel(level - 1); };
      let handled = true;
      if (ev.key === 'ArrowDown') move(1);
      else if (ev.key === 'ArrowUp') move(-1);
      else if (ev.key === 'ArrowRight') { if (level === 0 || (level === 1 && l2)) enter(level, path); }
      else if (ev.key === 'ArrowLeft') { if (level > 0) back(); }
      else if (ev.key === 'Escape') { if (level > 0) back(); else close(); }
      else if (ev.key === 'Enter' || ev.key === ' ') enter(level, path);
      else if (ev.key === 'Tab') close();
      else if (level === 0 && !ev.ctrlKey && /^[a-z]$/i.test(ev.key)) { const i = TOP_MENUS.findIndex((n) => MNEMONIC[n] === ev.key.toLowerCase()); if (i >= 0) enter(0, [i, -1, -1]); else handled = false; }
      else handled = false;
      if (handled) { ev.preventDefault(); ev.stopPropagation(); }
    };
    window.addEventListener('keydown', k, true); return () => window.removeEventListener('keydown', k, true);
  }, [open, path, level]);
  const l2list = l2 && level >= 1 ? <MenuList label={l1item.label} entries={l2} active={path[2]} width={300}
    onHover={(k) => { setPath([path[0], path[1], k]); setLevel(2); }} onPick={(k) => pick(l2[k])} /> : null;
  const l1list = <MenuList label={TOP_MENUS[path[0]]} entries={l1} active={path[1]} width={320} flyout={l2list}
    onHover={(j) => { setPath([path[0], j, -1]); setLevel(1); }} onPick={(j) => enter(1, [path[0], j, -1])} />;
  return <div style={{ position: 'relative', flex: 'none' }}>
    <button ref={btn} title="Application menu (Alt+F)" aria-label="Application menu" aria-keyshortcuts="Alt+F" aria-haspopup="menu" aria-expanded={open}
      onClick={() => (open ? close() : openAt(0))} onMouseEnter={() => setH(true)} onMouseLeave={() => setH(false)}
      style={{ height: 32, display: 'flex', alignItems: 'center', gap: 4, padding: '0 6px', border: 'none', borderRadius: 4, cursor: 'pointer', color: 'var(--text-secondary)', background: open ? 'var(--surface-pressed)' : h ? 'var(--surface-hover)' : 'transparent' }}>
      <img src="../../assets/logo/slate-gold.png" alt="" style={{ width: 20, height: 20, borderRadius: 4, display: 'block' }} />
      <SIcon k="chevD" size={10} />
    </button>
    {open && <>
      <div onClick={close} style={{ position: 'fixed', inset: 0, zIndex: 40 }} />
      <div style={{ position: 'absolute', top: 'calc(100% + 4px)', left: 0, zIndex: 41 }}>
        <MenuList label="Application menu" entries={l0} active={path[0]} width={220} flyout={l1list}
          onHover={(i) => { setPath([i, -1, -1]); setLevel(0); }} onPick={(i) => enter(0, [i, -1, -1])} />
      </div>
    </>}
  </div>;
}

/* Caption buttons: the DS CaptionButton (46 × 48). */
function CaptionBtn({ k, label, onClick }) {
  const C = SLATE_NS.CaptionButton;
  const kind = { min: 'minimize', max: 'maximize', restore: 'restore', close: 'close' }[k];
  return C ? <C kind={kind} label={label} onClick={onClick} style={{ height: '100%' }} /> : null;
}

function TitleBar({ title, onSearch, onCommand }) {
  const [max, setMax] = React.useState(true);
  return <header style={{ minHeight: 'var(--titlebar-min-height)', display: 'flex', alignItems: 'center', paddingLeft: 8, background: 'var(--surface-window)', flex: 'none', position: 'relative', zIndex: 30, userSelect: 'none' }}>
    <AppMenu onCommand={onCommand} />
    <div data-drag-region="true" onDoubleClick={() => setMax((m) => !m)} style={{ flex: 1, minWidth: 0, alignSelf: 'stretch', display: 'flex', alignItems: 'center', padding: '0 12px 0 8px' }}>
      <span style={{ font: '12px/16px var(--font-ui)', color: 'var(--text-primary)', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>{title}</span>
    </div>
    <div style={{ display: 'flex', gap: 2, alignItems: 'center' }}>
      <IconBtn k="search" label="Command palette (Ctrl+Shift+P)" onClick={onSearch} />
      <IconBtn k="settings" label="Settings" onClick={() => onCommand && onCommand('Settings')} />
    </div>
    <div style={{ display: 'flex', alignSelf: 'stretch', marginLeft: 8 }}>
      <CaptionBtn k="min" label="Minimize" />
      <CaptionBtn k={max ? 'restore' : 'max'} label={max ? 'Restore' : 'Maximize'} onClick={() => setMax((m) => !m)} />
      <CaptionBtn k="close" label="Close" danger />
    </div>
  </header>;
}

/* Status-bar pane toggles: 24 × 24, flat, pressed while the pane is visible. */
function StatusBtn({ k, label, pressed, onClick }) {
  const [h, setH] = React.useState(false);
  return <button aria-label={label} title={label} aria-pressed={pressed} onClick={onClick} onMouseEnter={() => setH(true)} onMouseLeave={() => setH(false)}
    style={{ width: 24, height: 24, flex: 'none', border: 'none', borderRadius: 4, padding: 0, display: 'flex', alignItems: 'center', justifyContent: 'center', cursor: 'pointer',
      color: pressed ? 'var(--text-primary)' : 'var(--text-secondary)', background: h ? 'var(--surface-hover)' : 'transparent' }}>
    <SIcon k={k} size={16} />
  </button>;
}
function StatusBar({ mode, words, left, onLeft, right, onRight }) {
  return <div role="status" style={{ minHeight: 'var(--statusbar-min-height)', display: 'flex', alignItems: 'center', gap: 16, padding: '0 2px', background: 'var(--surface-window)', borderTop: '1px solid var(--border-subtle)', font: '12px/16px var(--font-ui)', color: 'var(--text-secondary)', flex: 'none' }}>
    <StatusBtn k="dockLeft" label={(left ? 'Hide' : 'Show') + ' Files sidebar'} pressed={left} onClick={onLeft} />
    <span style={{ display: 'flex', gap: 6, alignItems: 'center', marginLeft: -8 }}><SIcon k="sync" size={12} />Vault indexed · 47 notes</span>
    <span style={{ flex: 1 }} />
    <span>{mode === 'read' ? 'Reading' : 'Editing'}</span><span>{words} words</span><span>Ln 14, Col 22</span><span>Markdown</span>
    <StatusBtn k="dockRight" label={(right ? 'Hide' : 'Show') + ' right pane (Ctrl+Alt+I)'} pressed={right} onClick={onRight} />
  </div>;
}
Object.assign(window, { TitleBar, StatusBar, AppMenu, CaptionBtn });
