import { __slateGlyph, __SlateIconButton, __SlateMenu, __SlateTip, __useRing, __useTip, __slateTipText } from '../internal/slateUi.jsx';

/* Header bar above a note: back/forward, folder breadcrumb, Source/Reading switch and the ⋯ menu (Slate addition,
   built from Fluent Button, Tooltip and MenuItem). The last path segment is the note and gets aria-current. */
export function NoteHeader({ path = [], onNavigate, canGoBack = false, canGoForward = false, onBack, onForward, mode = 'reading', onModeChange, compact, moreItems, moreLabel = 'More options', shortcuts = {}, className, style }) {
  const [menu, setMenu] = React.useState(null);
  const box = React.useRef(null);
  const [narrow, setNarrow] = React.useState(false);
  React.useEffect(() => {
    if (compact !== undefined || !box.current || typeof ResizeObserver === 'undefined') return;
    const ro = new ResizeObserver(([e]) => setNarrow(e.contentRect.width < 520));
    ro.observe(box.current);
    return () => ro.disconnect();
  }, [compact]);
  const small = compact ?? narrow;
  const moreBtn = React.useRef(null), crumbBtn = React.useRef(null);
  const [crumbs, setCrumbs] = React.useState(null);
  const folders = path.slice(0, -1), note = path[path.length - 1];
  return <div ref={box} className={className} style={{ height: 40, display: 'flex', alignItems: 'center', gap: 2, padding: '0 8px', boxSizing: 'border-box', background: 'var(--surface-card)', ...style }}>
    <__SlateIconButton icon="E72B" label="Back" shortcut={shortcuts.back} disabled={!canGoBack} onClick={onBack} />
    <__SlateIconButton icon="E72A" label="Forward" shortcut={shortcuts.forward} disabled={!canGoForward} onClick={onForward} />
    <nav aria-label="Note location" style={{ flex: 1, minWidth: 0, marginLeft: 4, display: 'flex' }}>
      <ol style={{ listStyle: 'none', margin: 0, padding: 0, display: 'flex', alignItems: 'center', gap: 2, minWidth: 0 }}>
        {small && folders.length > 0
          ? <li style={{ position: 'relative', display: 'flex', alignItems: 'center', gap: 2, flex: 'none' }}>
              <__Crumb btnRef={crumbBtn} label="…" title={folders.join(' › ')} ariaLabel="Folders" haspopup expanded={!!crumbs} onClick={(e) => setCrumbs(crumbs ? null : { kb: e.detail === 0 })} />{__slateGlyph('E76C', 12, { color: 'var(--text-tertiary)' })}
              {crumbs && <__SlateMenu label="Folders" keyboard={crumbs.kb} width={220} items={folders.map((f, i) => ({ label: f, icon: 'E8B7', onSelect: () => onNavigate && onNavigate(i) }))} onClose={(back) => { setCrumbs(null); if (back && crumbBtn.current) crumbBtn.current.focus(); }} />}
            </li>
          : folders.map((f, i) => <li key={i} style={{ display: 'flex', alignItems: 'center', gap: 2, flex: '0 3 auto', minWidth: 40 }}>
              <__Crumb label={f} onClick={() => onNavigate && onNavigate(i)} />{__slateGlyph('E76C', 12, { color: 'var(--text-tertiary)' })}
            </li>)}
        {note != null && <li style={{ flex: '0 1 auto', minWidth: 48, display: 'flex' }}>
          <span aria-current="page" title={note} style={{ padding: '0 6px', font: '600 14px/20px var(--font-ui)', color: 'var(--text-primary)', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>{note}</span>
        </li>}
      </ol>
    </nav>
    {onModeChange !== undefined || mode ? <__ModeSwitch mode={mode} onModeChange={onModeChange} compact={small} shortcut={shortcuts.mode} /> : null}
    {moreItems && <span style={{ position: 'relative', display: 'inline-flex', marginLeft: 2 }}>
      <__SlateIconButton buttonRef={moreBtn} icon="E712" label={moreLabel} shortcut={shortcuts.more} haspopup="menu" expanded={!!menu} onClick={(e) => setMenu(menu ? null : { kb: e.detail === 0 })} />
      {menu && <__SlateMenu label={moreLabel} keyboard={menu.kb} align="end" width={280} items={moreItems} onClose={(back) => { setMenu(null); if (back && moreBtn.current) moreBtn.current.focus(); }} />}
    </span>}
  </div>;
}

function __Crumb({ label, onClick, title, ariaLabel, haspopup, expanded, btnRef }) {
  const [h, setH] = React.useState(false), [p, setP] = React.useState(false);
  const [ring, rp] = __useRing();
  return <button ref={btnRef} type="button" onClick={onClick} aria-label={ariaLabel} aria-haspopup={haspopup ? 'menu' : undefined} aria-expanded={haspopup ? expanded : undefined} {...rp} onMouseEnter={() => setH(true)} onMouseLeave={() => { setH(false); setP(false); }} onMouseDown={() => setP(true)} onMouseUp={() => setP(false)}
    title={title ?? label} style={{ height: 28, minWidth: 0, maxWidth: '100%', padding: '0 6px', border: 'none', borderRadius: 4, background: p ? 'var(--surface-pressed)' : h ? 'var(--surface-hover)' : 'transparent', font: '14px/20px var(--font-ui)', color: 'var(--text-secondary)', cursor: 'pointer', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis', outline: ring ? '2px solid var(--focus-ring)' : 'none', outlineOffset: -2 }}>{label}</button>;
}

/* Two-option radio group. Arrow keys switch; only the checked option is a tab stop. */
function __ModeSwitch({ mode, onModeChange, compact, shortcut }) {
  const opts = [['source', 'E943', 'Source'], ['reading', 'E736', 'Reading']];
  const refs = React.useRef([]);
  const key = (e) => {
    if (!['ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown'].includes(e.key)) return;
    e.preventDefault();
    const next = mode === 'source' ? 1 : 0;
    onModeChange && onModeChange(opts[next][0]);
    refs.current[next] && refs.current[next].focus();
  };
  return <div role="radiogroup" aria-label="View" onKeyDown={key} style={{ display: 'flex', padding: 2, gap: 2, borderRadius: 6, background: 'var(--surface-raised)', flex: 'none' }}>
    {opts.map(([m, ic, l], i) => <__Seg key={m} btnRef={(el) => (refs.current[i] = el)} on={mode === m} icon={ic} label={l} compact={compact} shortcut={shortcut} onClick={() => onModeChange && onModeChange(m)} />)}
  </div>;
}

function __Seg({ on, icon, label, compact, shortcut, onClick, btnRef }) {
  const [h, setH] = React.useState(false);
  const [ring, rp] = __useRing();
  const tp = __useTip();
  const el = React.useRef(null);
  return <>
    <button ref={(n) => { el.current = n; btnRef(n); }} type="button" role="radio" aria-checked={on} aria-label={compact ? label : undefined} aria-keyshortcuts={shortcut || undefined} tabIndex={on ? 0 : -1} onClick={onClick}
      onFocus={(e) => { rp.onFocus(e); if (compact) tp.show(true); }} onBlur={() => { rp.onBlur(); tp.hide(); }}
      onMouseEnter={() => { setH(true); tp.show(false); }} onMouseLeave={() => { setH(false); tp.hide(); }}
      style={{ height: 28, display: 'flex', alignItems: 'center', gap: 6, padding: compact ? '0 6px' : '0 10px', border: 'none', borderRadius: 4, cursor: 'pointer', font: '14px/20px var(--font-ui)',
        background: on ? 'var(--surface-card)' : h ? 'var(--surface-hover)' : 'transparent', boxShadow: on ? 'var(--shadow-2)' : 'none', color: on ? 'var(--text-primary)' : 'var(--text-secondary)', outline: ring ? '2px solid var(--focus-ring)' : 'none', outlineOffset: -2 }}>
      {__slateGlyph(icon, 16)}{!compact && label}
    </button>
    {tp.open && el.current && <__SlateTip anchor={el.current} text={__slateTipText(on ? `${label} view` : `Switch to ${label.toLowerCase()} view`, shortcut)} />}
  </>;
}
