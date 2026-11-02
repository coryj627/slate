import { Button } from '../actions/Button.jsx';
import { MenuItem } from '../navigation/MenuItem.jsx';
import { Tooltip } from '../overlays/Tooltip.jsx';

/* Shared internals for components/patterns (Slate additions). Names start with "__", so they stay off the
   public namespace. Icons are Segoe Fluent Icons code points ("E721") or any React node (readme → Iconography). */

export function __slateGlyph(icon, size = 16, style) {
  if (icon == null || icon === false || icon === '') return null;
  if (typeof icon !== 'string') return icon;
  return <span aria-hidden="true" style={{ width: size, height: size, flex: 'none', display: 'inline-flex', alignItems: 'center', justifyContent: 'center', fontFamily: 'var(--font-icons)', fontSize: size, lineHeight: 1, fontStyle: 'normal', fontWeight: 400, ...style }}>{String.fromCharCode(parseInt(icon, 16))}</span>;
}

export const __slateTipText = (label, shortcut) => (shortcut ? `${label} (${shortcut})` : label);

const __isKeyboardFocus = (el) => { try { return el.matches(':focus-visible'); } catch (e) { return true; } };

/* Keyboard-only focus ring: [ring, handlers]. Spread the handlers on the focusable element. */
export function __useRing() {
  const [ring, setRing] = React.useState(false);
  return [ring, { onFocus: (e) => setRing(__isKeyboardFocus(e.currentTarget)), onBlur: () => setRing(false) }];
}

/* Tooltip timing: 500 ms after hover, at once on keyboard focus. */
export function __useTip() {
  const [open, setOpen] = React.useState(false);
  const t = React.useRef(0);
  React.useEffect(() => () => clearTimeout(t.current), []);
  return { open, show: (now) => { clearTimeout(t.current); if (now) setOpen(true); else t.current = setTimeout(() => setOpen(true), 500); }, hide: () => { clearTimeout(t.current); setOpen(false); } };
}

/* The DS Tooltip in a fixed layer beside its anchor, kept inside the window. */
export function __SlateTip({ anchor, text, side = 'below' }) {
  const ref = React.useRef(null);
  const [pos, setPos] = React.useState(null);
  React.useLayoutEffect(() => {
    if (!anchor || !ref.current) return;
    const a = anchor.getBoundingClientRect(), t = ref.current.getBoundingClientRect(), g = 4;
    let left, top;
    if (side === 'left' || side === 'right') { top = a.top + a.height / 2 - t.height / 2; left = side === 'left' ? a.left - t.width - g : a.right + g; }
    else { left = a.left + a.width / 2 - t.width / 2; top = side === 'above' ? a.top - t.height - g : a.bottom + g; }
    setPos({ left: Math.max(g, Math.min(left, window.innerWidth - t.width - g)), top: Math.max(g, Math.min(top, window.innerHeight - t.height - g)) });
  }, [anchor, text, side]);
  return <div ref={ref} role="tooltip" style={{ position: 'fixed', left: pos ? pos.left : 0, top: pos ? pos.top : 0, zIndex: 1000, pointerEvents: 'none', visibility: pos ? 'visible' : 'hidden', whiteSpace: 'nowrap' }}><Tooltip label={text} /></div>;
}

/* Icon-only DS Button inside a native <button>: hover, pressed, selected and keyboard-focus states, plus a tooltip
   that shows the label and chord. `active` gives the selected look without aria-selected. */
export function __SlateIconButton({ icon, label, shortcut, onClick, onKeyDown, pressed, selected, active, expanded, haspopup, disabled, size = 'medium', tooltipSide = 'below', buttonRef, tabIndex, role, controls, tip = true }) {
  const [h, setH] = React.useState(false), [p, setP] = React.useState(false), [f, setF] = React.useState(false);
  const el = React.useRef(null);
  const tp = __useTip();
  const setRef = (n) => { el.current = n; if (typeof buttonRef === 'function') buttonRef(n); else if (buttonRef) buttonRef.current = n; };
  const g = __slateGlyph(icon, 16);
  const on = active || pressed || selected || expanded;
  const state = disabled ? 'disabled' : p ? 'pressed' : on ? 'selected' : h ? 'hover' : 'rest';
  return <>
    <button ref={setRef} type="button" role={role} aria-label={label} aria-keyshortcuts={shortcut || undefined} aria-pressed={pressed === undefined ? undefined : !!pressed}
      aria-selected={selected === undefined ? undefined : !!selected} aria-expanded={expanded === undefined ? undefined : !!expanded} aria-haspopup={haspopup} aria-controls={controls} disabled={disabled} tabIndex={tabIndex}
      onClick={(e) => { tp.hide(); onClick && onClick(e); }} onKeyDown={(e) => { if (e.key === 'Escape') tp.hide(); onKeyDown && onKeyDown(e); }}
      onMouseEnter={() => { setH(true); if (tip) tp.show(false); }} onMouseLeave={() => { setH(false); setP(false); tp.hide(); }} onMouseDown={() => { setP(true); tp.hide(); }} onMouseUp={() => setP(false)}
      onFocus={(e) => { const kb = __isKeyboardFocus(e.currentTarget); setF(kb); if (kb && tip) tp.show(true); }} onBlur={() => { setF(false); tp.hide(); }}
      style={{ padding: 0, margin: 0, border: 'none', background: 'transparent', borderRadius: 4, display: 'flex', flex: 'none', cursor: disabled ? 'default' : 'pointer', outline: 'none' }}>
      <Button appearance="subtle" layout="icon only" size={size} state={state} focus={f} xOutline20={g} xFilled20={g} xOutline24={g} xFilled24={g} icon1={g} />
    </button>
    {tip && tp.open && !expanded && el.current && <__SlateTip anchor={el.current} text={__slateTipText(label, shortcut)} side={tooltipSide} />}
  </>;
}

/* Search field with the Fluent input anatomy: 32 px, subtle border, strong bottom stroke, accent stroke on focus. */
export function __SlateSearchField({ value, onChange, placeholder = 'Search', inputRef, onKeyDown, trailing, style }) {
  const [fo, setFo] = React.useState(false);
  return <label style={{ flex: 1, minWidth: 0, height: 32, display: 'flex', alignItems: 'center', gap: 6, padding: '0 2px 0 8px', borderRadius: 4, background: 'var(--surface-card)', border: '1px solid var(--border-subtle)', borderBottom: fo ? '2px solid var(--accent-indicator)' : '1px solid var(--border-strong)', boxSizing: 'border-box', ...style }}>
    {__slateGlyph('E721', 16, { color: 'var(--text-secondary)' })}
    <input ref={inputRef} value={value ?? ''} placeholder={placeholder} aria-label={placeholder} onChange={(e) => onChange && onChange(e.target.value)} onKeyDown={onKeyDown} onFocus={() => setFo(true)} onBlur={() => setFo(false)}
      style={{ flex: 1, minWidth: 0, border: 'none', outline: 'none', background: 'transparent', padding: 0, font: '14px/20px var(--font-ui)', color: 'var(--text-primary)' }} />
    {trailing}
  </label>;
}

/* Popup menu of DS MenuItems. One leading column: if any item has an icon, every row keeps the 20 px icon slot and a
   checked item shows the check in place of its icon (WPF MenuItem behaviour); otherwise checkable menus keep the 16 px
   check column on every row. Arrow keys skip separators and disabled rows. onClose(returnFocus). */
export function __SlateMenu({ items = [], label, onClose, align = 'start', side = 'below', width = 240, keyboard = false }) {
  const id = React.useId();
  const ok = (it) => it && !it.separator && !it.disabled;
  const live = items.map((it, i) => (ok(it) ? i : -1)).filter((i) => i >= 0);
  const [active, setActive] = React.useState(keyboard ? live[0] ?? -1 : -1);
  const [kbd, setKbd] = React.useState(keyboard);
  const ref = React.useRef(null);
  React.useEffect(() => {
    if (ref.current) ref.current.focus();
    const down = (e) => { const box = ref.current && ref.current.parentNode; if (box && !box.contains(e.target)) onClose && onClose(false); };
    document.addEventListener('mousedown', down, true);
    return () => document.removeEventListener('mousedown', down, true);
  }, []);
  const hasIcons = items.some((it) => it && it.icon);
  const hasChecks = items.some((it) => it && it.checked !== undefined);
  const move = (d) => { if (!live.length) return; let p = live.indexOf(active); p = p < 0 ? (d > 0 ? 0 : live.length - 1) : (p + d + live.length) % live.length; setActive(live[p]); setKbd(true); };
  const pick = (it) => { if (!ok(it)) return; onClose && onClose(true); it.onSelect && it.onSelect(); };
  const key = (e) => {
    let handled = true;
    if (e.key === 'ArrowDown') move(1);
    else if (e.key === 'ArrowUp') move(-1);
    else if (e.key === 'Home') { setActive(live[0] ?? -1); setKbd(true); }
    else if (e.key === 'End') { setActive(live[live.length - 1] ?? -1); setKbd(true); }
    else if (e.key === 'Enter' || e.key === ' ') pick(items[active]);
    else if (e.key === 'Escape') onClose && onClose(true);
    else if (e.key === 'Tab') { onClose && onClose(false); handled = false; }
    else handled = false;
    if (handled) { e.preventDefault(); e.stopPropagation(); }
  };
  const place = side === 'left' ? { right: 'calc(100% + 4px)', top: 0 } : side === 'right' ? { left: 'calc(100% + 4px)', top: 0 }
    : { [side === 'above' ? 'bottom' : 'top']: 'calc(100% + 4px)', [align === 'end' ? 'right' : 'left']: 0 };
  const slot = (it) => (it.checked ? __slateGlyph('E73E', 16, { width: 20, height: 20 }) : it.icon ? __slateGlyph(it.icon, 16, { width: 20, height: 20 }) : <span style={{ display: 'block', width: 20, height: 20 }} />);
  return <div ref={ref} role="menu" aria-label={label} tabIndex={-1} aria-activedescendant={active >= 0 ? `${id}-${active}` : undefined} onKeyDown={key}
    style={{ position: 'absolute', ...place, zIndex: 60, width, padding: '2px 0', borderRadius: 8, background: 'var(--neutral-background-1-rest)', border: '1px solid var(--border-subtle)', boxShadow: 'var(--shadow-16)', boxSizing: 'border-box', outline: 'none' }}>
    {items.map((it, i) => {
      if (!it) return null;
      if (it.separator) return <div key={i} role="separator" style={{ height: 1, background: 'var(--border-subtle)', margin: '2px 0' }} />;
      const lead = hasIcons ? slot(it) : undefined;
      const st = it.disabled ? 'disabled' : i === active && !kbd ? 'hover' : 'rest';
      return <div key={i} id={`${id}-${i}`} role={it.checked !== undefined ? 'menuitemcheckbox' : 'menuitem'} aria-checked={it.checked !== undefined ? !!it.checked : undefined} aria-disabled={it.disabled || undefined} aria-keyshortcuts={it.shortcut || undefined}
        onMouseEnter={() => { if (ok(it)) { setActive(i); setKbd(false); } }} onMouseDown={(e) => e.preventDefault()} onClick={() => pick(it)} style={{ cursor: it.disabled ? 'default' : 'pointer' }}>
        <MenuItem style={{ width: '100%' }} icon={hasIcons} pxRegular20={lead} pxFilled20={lead} checkmark={!hasIcons && hasChecks} selected={!!it.checked} chevron={!!it.submenu} endContent={!!it.shortcut} endContentString={it.shortcut || ''} secondaryText={false} primaryString={it.label} state={st} focus={kbd && i === active && !it.disabled} />
      </div>;
    })}
  </div>;
}
