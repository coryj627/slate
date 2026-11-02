/* Settings window (a separate WPF Window in the app): SettingsLayout + SettingRow over Slate's real preferences
   (AppPreferencesStore, EditorPreferencesViewModel, SidebarSettingsStore). Values are local to the kit. */
const SLATE_SETTINGS_CATS = [{ id: 'appearance', label: 'Appearance', icon: 'E790' }, { id: 'editor', label: 'Editor', icon: 'E70F' }, { id: 'math', label: 'Math', icon: 'E8EF' }, { id: 'files', label: 'Files sidebar', icon: 'E8B7' }, { id: 'history', label: 'History', icon: 'E81C' }];
const SLATE_SETTINGS = [
  ['appearance', 'Theme', '', ['sel', 'Use Windows setting', SLATE_THEMES.map((t) => t.label)]],
  ['appearance', 'Accent color', 'Automatic uses Slate gold unless you\u2019ve chosen an accent color in Windows.', ['sel', 'Automatic', ['Automatic', 'Slate gold', 'Windows accent']]],
  ['editor', 'Font size', 'Text size in the editor.', ['sel', '14', ['12', '13', '14', '15', '16', '18', '20']]],
  ['editor', 'Check spelling while typing', 'Underlines misspelled words in the editor.', ['sw', true]],
  ['editor', 'Open reading links in a new tab', 'When off, links in reading mode open in the current tab.', ['sw', true]],
  ['editor', 'Code preamble verbosity', '', ['sel', 'Preamble only', ['Preamble only', 'Preamble + first line', 'Preamble + all tokens']]],
  ['math', 'Speech style', '', ['sel', 'ClearSpeak', ['ClearSpeak', 'SimpleSpeak']]],
  ['math', 'Verbosity', '', ['sel', 'Medium', ['Terse', 'Medium', 'Verbose']]],
  ['math', 'Braille code', '', ['sel', 'Nemeth', ['Nemeth', 'UEB']]],
  ['files', 'Sort order', '', ['sel', 'Name A to Z', SLATE_SORT.map((s) => s.label)]],
  ['files', 'Group by date', '', ['sw', false]],
  ['files', 'Show tags', '', ['sw', true]],
  ['history', 'Show changes since open', '', ['sw', false]],
];
function SettingSelect({ value, options, label, onChange }) {
  return <span style={{ position: 'relative', display: 'inline-flex' }}>
    <select aria-label={label} value={value} onChange={(e) => onChange(e.target.value)} style={{ appearance: 'none', height: 32, minWidth: 180, padding: '0 30px 0 10px', borderRadius: 4, border: '1px solid var(--border-subtle)', borderBottom: '1px solid var(--border-strong)', background: 'var(--surface-card)', color: 'var(--text-primary)', font: '14px/20px var(--font-ui)', cursor: 'pointer' }}>
      {options.map((o) => <option key={o}>{o}</option>)}
    </select>
    <SIcon k="chevD" size={12} style={{ position: 'absolute', right: 10, top: 10, pointerEvents: 'none', color: 'var(--text-secondary)' }} />
  </span>;
}
function SettingSwitch({ on, label, onChange }) {
  const [h, setH] = React.useState(false); const S = SLATE_NS.Switch;
  return <button role="switch" aria-checked={on} aria-label={label} onClick={() => onChange(!on)} onMouseEnter={() => setH(true)} onMouseLeave={() => setH(false)}
    style={{ padding: 0, border: 'none', background: 'transparent', display: 'flex', cursor: 'pointer', borderRadius: 10 }}>{S ? <S checked={on} layout="switch" state={h ? 'hover' : 'rest'} /> : (on ? 'On' : 'Off')}</button>;
}
function SettingsWindow({ onClose, theme, onTheme }) {
  const { SettingsLayout, SettingRow } = SLATE_NS;
  const [cat, setCat] = React.useState('appearance');
  const [q, setQ] = React.useState('');
  const [vals, setVals] = React.useState(() => Object.fromEntries(SLATE_SETTINGS.map(([, l, , c]) => [l, c[1]])));
  React.useEffect(() => { const k = (e) => { if (e.key === 'Escape') onClose(); }; window.addEventListener('keydown', k); return () => window.removeEventListener('keydown', k); }, []);
  const rows = SLATE_SETTINGS.filter(([c, l]) => (q ? l.toLowerCase().includes(q.toLowerCase()) : c === cat));
  const set = (l) => (v) => setVals((o) => ({ ...o, [l]: v }));
  const themeLabel = (SLATE_THEMES.find((t) => t.value === theme) || SLATE_THEMES[2]).label;
  const valueOf = (l) => (l === 'Theme' ? themeLabel : vals[l]);
  const onChangeOf = (l) => (l === 'Theme' ? (lab) => onTheme(SLATE_THEMES.find((t) => t.label === lab).value) : set(l));
  return <div onMouseDown={(e) => e.target === e.currentTarget && onClose()} style={{ position: 'absolute', inset: 0, zIndex: 70, background: 'var(--scrim)', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
    <div role="dialog" aria-modal="true" aria-label="Settings" style={{ width: 900, maxWidth: '92%', height: 600, maxHeight: '88%', display: 'flex', flexDirection: 'column', borderRadius: 8, overflow: 'hidden', border: '1px solid var(--border-subtle)', boxShadow: 'var(--shadow-64)', background: 'var(--surface-window)' }}>
      <div style={{ height: 32, flex: 'none', display: 'flex', alignItems: 'center', paddingLeft: 12 }}>
        <span style={{ flex: 1, font: '12px/16px var(--font-ui)', color: 'var(--text-primary)' }}>Settings</span>
        <CaptionBtn k="close" label="Close settings" danger onClick={onClose} />
      </div>
      <div style={{ flex: 1, minHeight: 0 }}>
        {SettingsLayout && <SettingsLayout categories={SLATE_SETTINGS_CATS} value={cat} onChange={(v) => { setCat(v); setQ(''); }} query={q} onQueryChange={setQ}
          title={q ? 'Results for \u201c' + q + '\u201d' : undefined} description={!q && cat === 'math' ? 'How Slate reads math aloud and shows it in braille.' : undefined}>
          {rows.length === 0 && <p style={{ margin: 0, font: '14px/20px var(--font-ui)', color: 'var(--text-secondary)' }}>No settings match.</p>}
          {rows.map(([, l, d, [kind, , opts]]) => <SettingRow key={l} label={l} description={d || undefined}>
            {kind === 'sel' ? <SettingSelect value={valueOf(l)} options={opts} label={l} onChange={onChangeOf(l)} /> : <SettingSwitch on={vals[l]} label={l} onChange={set(l)} />}
          </SettingRow>)}
        </SettingsLayout>}
      </div>
    </div>
  </div>;
}
Object.assign(window, { SettingsWindow });
