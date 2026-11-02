import { __SlateIconButton, __SlateMenu } from '../internal/slateUi.jsx';

/* Icon strip that switches a pane between its views (Slate addition, built from Fluent Button, Tooltip, MenuItem).
   Arrow keys move between views, Home/End jump to the ends. When there are more views than maxVisible, the last
   slot becomes "More panels", a menu of the rest; it takes the selected look while one of those views is open. */
export function PaneSwitcher({ items = [], value, onChange, maxVisible = 6, orientation = 'horizontal', label = 'Panels', moreLabel = 'More panels', className, style }) {
  const vertical = orientation === 'vertical';
  const over = items.length > maxVisible;
  const shown = over ? items.slice(0, Math.max(1, maxVisible - 1)) : items;
  const rest = over ? items.slice(shown.length) : [];
  const hidden = rest.find((it) => it.id === value);
  const [menu, setMenu] = React.useState(null);
  const tabs = React.useRef([]);
  const more = React.useRef(null);
  const cur = Math.max(0, shown.findIndex((it) => it.id === value));
  const go = (i) => { const n = shown.length, el = tabs.current[(i + n) % n]; if (el) el.focus(); };
  const key = (i) => (e) => {
    const next = vertical ? 'ArrowDown' : 'ArrowRight', prev = vertical ? 'ArrowUp' : 'ArrowLeft';
    if (e.key === next) go(i + 1); else if (e.key === prev) go(i - 1); else if (e.key === 'Home') go(0); else if (e.key === 'End') go(shown.length - 1); else return;
    e.preventDefault();
  };
  const bar = (on) => on && <span aria-hidden="true" style={vertical
    ? { position: 'absolute', left: 0, top: 8, bottom: 8, width: 3, borderRadius: 2, background: 'var(--accent-indicator)', pointerEvents: 'none' }
    : { position: 'absolute', left: 8, right: 8, bottom: 0, height: 3, borderRadius: 2, background: 'var(--accent-indicator)', pointerEvents: 'none' }} />;
  const tipSide = vertical ? 'left' : 'below';
  const dir = vertical ? 'column' : 'row';
  return <div className={className} style={{ display: 'flex', flexDirection: dir, alignItems: 'center', gap: 2, ...style }}>
    <div role="tablist" aria-label={label} aria-orientation={orientation} style={{ display: 'flex', flexDirection: dir, gap: 2 }}>
      {shown.map((it, i) => <span key={it.id} style={{ position: 'relative', display: 'inline-flex' }}>
        <__SlateIconButton buttonRef={(el) => (tabs.current[i] = el)} role="tab" selected={it.id === value} tabIndex={i === cur ? 0 : -1} icon={it.icon} label={it.label} shortcut={it.shortcut}
          tooltipSide={tipSide} onKeyDown={key(i)} onClick={() => onChange && onChange(it.id)} />
        {bar(it.id === value)}
      </span>)}
    </div>
    {rest.length > 0 && <span style={{ position: 'relative', display: 'inline-flex' }}>
      <__SlateIconButton buttonRef={more} icon="E712" label={hidden ? `${moreLabel} · ${hidden.label}` : moreLabel} haspopup="menu" expanded={!!menu} active={!!hidden} tooltipSide={tipSide}
        onClick={(e) => setMenu(menu ? null : { kb: e.detail === 0 })} />
      {bar(!!hidden)}
      {menu && <__SlateMenu label={moreLabel} keyboard={menu.kb} side={vertical ? 'left' : 'below'} align="end" width={232}
        items={rest.map((it) => ({ label: it.label, icon: it.icon, shortcut: it.shortcut, checked: it.id === value, onSelect: () => onChange && onChange(it.id) }))}
        onClose={(back) => { setMenu(null); if (back && more.current) more.current.focus(); }} />}
    </span>}
  </div>;
}
