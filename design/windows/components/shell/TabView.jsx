import { __wGlyph } from '../internal/winui.jsx';

/* Tab view from the Windows UI kit (Navigation › Tab view, "in content area"): 32 px tabs, 8 px top radius, 16 px icon
   8 px from the edge, label at 32 px, a 40 × 32 close button. The open tab joins the content surface with flared bottom
   corners; other tabs are text-secondary with a 1 px divider. Left/Right/Home/End move between tabs. */
function __Tab({ t, on, prevOn, last, closable, onPick, onClose, tabRef, tabIndex, onKeyDown, closeLabel }) {
  const [h, setH] = React.useState(false), [ch, setCh] = React.useState(false), [kb, setKb] = React.useState(false);
  const fill = on ? 'var(--surface-card)' : h ? 'var(--surface-hover)' : 'transparent';
  const flare = (side) => <span aria-hidden="true" style={{ position: 'absolute', bottom: 0, [side]: -4, width: 4, height: 4, background: `radial-gradient(circle at ${side === 'left' ? '0 0' : '100% 0'}, transparent 3.5px, var(--surface-card) 4px)` }} />;
  return <div role="tab" ref={tabRef} aria-selected={on} tabIndex={tabIndex} onClick={onPick} onKeyDown={onKeyDown}
    onMouseEnter={() => setH(true)} onMouseLeave={() => setH(false)} onFocus={(e) => { let k = true; try { k = e.currentTarget.matches(':focus-visible'); } catch (_) {} setKb(k); }} onBlur={() => setKb(false)}
    style={{ position: 'relative', flex: '0 1 240px', minWidth: 100, maxWidth: 240, height: 32, boxSizing: 'border-box', display: 'flex', alignItems: 'center', gap: 8, paddingLeft: 8, borderRadius: '8px 8px 0 0', background: fill, border: on ? '1px solid var(--border-subtle)' : '1px solid transparent', borderBottom: 'none', marginBottom: on ? -1 : 0, cursor: 'default', outline: kb ? '2px solid var(--focus-ring)' : 'none', outlineOffset: -2, zIndex: on ? 1 : 0 }}>
    {on && flare('left')}{on && flare('right')}
    {!on && !prevOn && !last && !h && <span aria-hidden="true" style={{ position: 'absolute', right: -1, top: 8, width: 1, height: 16, background: 'var(--border-subtle)' }} />}
    {t.icon != null && <span style={{ width: 16, height: 16, flex: 'none', display: 'flex', color: on ? 'var(--text-primary)' : 'var(--text-secondary)' }}>{__wGlyph(t.icon, 16)}</span>}
    <span style={{ flex: 1, minWidth: 0, font: 'var(--type-body)', color: on ? 'var(--text-primary)' : 'var(--text-secondary)', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>{t.label}</span>
    {closable && <button type="button" tabIndex={-1} aria-label={closeLabel + ' ' + t.label} onClick={(e) => { e.stopPropagation(); onClose && onClose(t.id); }} onMouseEnter={() => setCh(true)} onMouseLeave={() => setCh(false)}
      style={{ position: 'relative', width: 40, height: 32, flex: 'none', padding: 0, border: 'none', background: 'transparent', color: 'var(--text-secondary)', display: 'flex', alignItems: 'center', justifyContent: 'center', cursor: 'default' }}>
      <span aria-hidden="true" style={{ position: 'absolute', left: 5, top: 1, width: 30, height: 30, borderRadius: 3, background: ch ? 'var(--surface-hover)' : 'transparent' }} />
      <span style={{ position: 'relative', display: 'flex' }}>{__wGlyph('E711', 12)}</span>
    </button>}
  </div>;
}

export function TabView(props = {}) {
  const { className, style, tabs = [{ id: 'note', label: 'Linear algebra lecture 3', icon: 'E8A5' }, { id: 'why', label: 'Why Slate', icon: 'E8A5' }], value, onChange, onClose, onAdd, closable = true, label = 'Open notes', addLabel = 'New tab', closeLabel = 'Close', trailing } = props;
  const [own, setOwn] = React.useState(value ?? (tabs[0] && tabs[0].id));
  React.useEffect(() => { if (value !== undefined) setOwn(value); }, [value]);
  const cur = value !== undefined ? value : own;
  const refs = React.useRef([]);
  const pick = (id) => { setOwn(id); onChange && onChange(id); };
  const idx = Math.max(0, tabs.findIndex((t) => t.id === cur));
  const key = (i) => (e) => {
    const n = tabs.length; let j = null;
    if (e.key === 'ArrowRight') j = (i + 1) % n; else if (e.key === 'ArrowLeft') j = (i - 1 + n) % n; else if (e.key === 'Home') j = 0; else if (e.key === 'End') j = n - 1;
    else if ((e.key === 'Delete' || (e.key === 'F4' && e.ctrlKey)) && closable) { e.preventDefault(); onClose && onClose(tabs[i].id); return; }
    if (j != null) { e.preventDefault(); pick(tabs[j].id); refs.current[j] && refs.current[j].focus(); }
  };
  const [ah, setAh] = React.useState(false);
  return <div className={className} style={{ display: 'flex', alignItems: 'flex-end', minHeight: 'var(--tabstrip-min-height)', padding: '0 8px', boxSizing: 'border-box', borderBottom: '1px solid var(--border-subtle)', ...style }}>
    <div role="tablist" aria-label={label} style={{ display: 'flex', alignItems: 'flex-end', minWidth: 0, flex: '0 1 auto' }}>
      {tabs.map((t, i) => <__Tab key={t.id} t={t} on={i === idx} prevOn={i + 1 === idx} last={i === tabs.length - 1} closable={closable} closeLabel={closeLabel} onPick={() => pick(t.id)} onClose={onClose} tabIndex={i === idx ? 0 : -1} tabRef={(el) => (refs.current[i] = el)} onKeyDown={key(i)} />)}
    </div>
    {onAdd && <button type="button" aria-label={addLabel} title={addLabel} onClick={onAdd} onMouseEnter={() => setAh(true)} onMouseLeave={() => setAh(false)}
      style={{ position: 'relative', width: 40, height: 32, flex: 'none', padding: 0, border: 'none', background: 'transparent', color: 'var(--text-primary)', display: 'flex', alignItems: 'center', justifyContent: 'center', cursor: 'default' }}>
      <span aria-hidden="true" style={{ position: 'absolute', left: 5, top: 1, width: 30, height: 30, borderRadius: 3, background: ah ? 'var(--surface-hover)' : 'transparent' }} />
      <span style={{ position: 'relative', display: 'flex' }}>{__wGlyph('E710', 12)}</span>
    </button>}
    <div style={{ flex: 1 }} />
    {trailing}
  </div>;
}
export default TabView;
