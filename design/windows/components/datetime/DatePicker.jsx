import { __wGlyph } from '../internal/winui.jsx';

const __iso = (d) => d.getFullYear() + '-' + String(d.getMonth() + 1).padStart(2, '0') + '-' + String(d.getDate()).padStart(2, '0');
const __parse = (v) => { if (!v) return null; if (v instanceof Date) return v; const m = /^(\d{4})-(\d{2})-(\d{2})/.exec(v); return m ? new Date(+m[1], +m[2] - 1, +m[3]) : null; };

/* Calendar date picker from the Windows UI kit (Date and time): a 32 px button-like field ("Pick a date", 12 px calendar
   glyph 12 px from the right) that opens a 300 px calendar flyout. Day view: 40 × 40 day cells with 2 px gaps, 7 columns,
   6 rows. Today is an accent-filled circle; the selected day has an accent ring. Arrow keys move by day and week,
   Page Up/Down by month, Enter picks, Esc closes. value is "YYYY-MM-DD". */
export function DatePicker(props = {}) {
  const { className, style, value, onChange, placeholder = 'Pick a date', label, disabled, firstDayOfWeek = 0, locale, width = 120 } = props;
  const today = __parse(props.today) || new Date();
  const sel = __parse(value);
  const [open, setOpen] = React.useState(!!props.open);
  React.useEffect(() => setOpen(!!props.open), [props.open]);
  const [focusDay, setFocusDay] = React.useState(sel || today);
  const [h, setH] = React.useState(false);
  const field = React.useRef(null), grid = React.useRef(null), wrap = React.useRef(null);
  const month = new Date(focusDay.getFullYear(), focusDay.getMonth(), 1);
  const start = new Date(month); start.setDate(1 - ((month.getDay() - firstDayOfWeek + 7) % 7));
  const days = Array.from({ length: 42 }, (_, i) => { const d = new Date(start); d.setDate(start.getDate() + i); return d; });
  const same = (a, b) => a && b && a.getFullYear() === b.getFullYear() && a.getMonth() === b.getMonth() && a.getDate() === b.getDate();
  const weekdays = Array.from({ length: 7 }, (_, i) => { const d = new Date(2026, 10, 1 + ((firstDayOfWeek + i) % 7)); return d.toLocaleDateString(locale, { weekday: 'short' }).slice(0, 2); });
  const title = month.toLocaleDateString(locale, { month: 'long', year: 'numeric' });
  const shift = (n, unit) => { const d = new Date(focusDay); if (unit === 'm') d.setMonth(d.getMonth() + n); else d.setDate(d.getDate() + n); setFocusDay(d); };
  const pick = (d) => { onChange && onChange(__iso(d)); setOpen(false); field.current && field.current.focus(); };
  React.useEffect(() => { if (!open || props.open) return; const t = setTimeout(() => { const b = grid.current && grid.current.querySelector('[data-focus="true"]'); b && b.focus(); }, 0); return () => clearTimeout(t); }, [open, focusDay]);
  React.useEffect(() => { if (!open || props.open) return; const f = (e) => { if (wrap.current && !wrap.current.contains(e.target)) setOpen(false); }; document.addEventListener('mousedown', f, true); return () => document.removeEventListener('mousedown', f, true); }, [open]);
  const key = (e) => {
    const k = e.key; let done = true;
    if (k === 'ArrowLeft') shift(-1); else if (k === 'ArrowRight') shift(1); else if (k === 'ArrowUp') shift(-7); else if (k === 'ArrowDown') shift(7);
    else if (k === 'PageUp') shift(-1, 'm'); else if (k === 'PageDown') shift(1, 'm');
    else if (k === 'Home') shift(-((focusDay.getDay() - firstDayOfWeek + 7) % 7)); else if (k === 'End') shift(6 - ((focusDay.getDay() - firstDayOfWeek + 7) % 7));
    else if (k === 'Enter' || k === ' ') pick(focusDay); else if (k === 'Escape') { setOpen(false); field.current && field.current.focus(); } else done = false;
    if (done) { e.preventDefault(); e.stopPropagation(); }
  };
  const nav = (glyph, n, name) => <button type="button" aria-label={name} onClick={() => shift(n, 'm')} style={{ width: 32, height: 32, flex: 'none', padding: 0, border: 'none', borderRadius: 4, background: 'transparent', color: 'var(--text-secondary)', display: 'flex', alignItems: 'center', justifyContent: 'center', cursor: 'default' }}>{__wGlyph(glyph, 12)}</button>;
  const shown = sel ? sel.toLocaleDateString(locale) : placeholder;
  return <div ref={wrap} className={className} style={{ position: 'relative', display: 'inline-flex', flexDirection: 'column', gap: 8, ...style }}>
    {label && <span style={{ font: 'var(--type-body)', color: disabled ? 'var(--text-disabled)' : 'var(--text-primary)' }}>{label}</span>}
    <button ref={field} type="button" disabled={disabled} aria-haspopup="dialog" aria-expanded={open} aria-label={(label ? label + ', ' : '') + shown} onClick={() => setOpen((o) => !o)} onMouseEnter={() => setH(true)} onMouseLeave={() => setH(false)}
      style={{ position: 'relative', width, minHeight: 32, boxSizing: 'border-box', padding: '0 12px', border: '1px solid ' + (disabled ? 'var(--border-disabled)' : 'var(--neutral-stroke-1-rest)'), borderRadius: 4, background: disabled ? 'var(--surface-disabled)' : h ? 'var(--neutral-background-1-hover)' : 'var(--neutral-background-1-rest)', display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 8, font: 'var(--type-body)', color: disabled ? 'var(--text-disabled)' : 'var(--text-primary)', textAlign: 'left', cursor: 'default' }}>
      <span style={{ whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>{shown}</span>
      {__wGlyph('E787', 12, { color: disabled ? 'var(--text-disabled)' : 'var(--text-secondary)' })}
    </button>
    {open && <div role="dialog" aria-label={title} onKeyDown={key} style={{ position: props.open ? 'relative' : 'absolute', top: props.open ? 0 : 'calc(100% + 4px)', left: 0, zIndex: 60, width: 300, boxSizing: 'border-box', borderRadius: 8, border: '1px solid var(--border-subtle)', background: 'var(--neutral-background-1-rest)', boxShadow: 'var(--shadow-16)', overflow: 'hidden' }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: 4, height: 40, padding: '0 4px 0 12px', borderBottom: '1px solid var(--border-subtle)' }}>
        <span aria-live="polite" style={{ flex: 1, font: 'var(--type-body-strong)', color: 'var(--text-primary)' }}>{title}</span>
        {nav('E70E', -1, 'Previous month')}{nav('E70D', 1, 'Next month')}
      </div>
      <div style={{ padding: 3 }}>
        <div aria-hidden="true" style={{ display: 'grid', gridTemplateColumns: 'repeat(7, 40px)', gap: 2, height: 40, alignItems: 'center' }}>
          {weekdays.map((w) => <span key={w} style={{ textAlign: 'center', font: '600 12px/16px var(--font-ui-small)', color: 'var(--text-primary)' }}>{w}</span>)}
        </div>
        <div ref={grid} role="grid" aria-label={title} style={{ display: 'grid', gridTemplateColumns: 'repeat(7, 40px)', gridAutoRows: '40px', gap: 2 }}>
          {days.map((d) => {
            const inMonth = d.getMonth() === month.getMonth(), isToday = same(d, today), isSel = same(d, sel), isFocus = same(d, focusDay);
            return <button key={d.toISOString()} type="button" role="gridcell" aria-selected={isSel} aria-current={isToday ? 'date' : undefined} aria-label={d.toLocaleDateString(locale, { weekday: 'long', year: 'numeric', month: 'long', day: 'numeric' })} tabIndex={isFocus ? 0 : -1} data-focus={isFocus}
              onClick={() => pick(d)} onFocus={() => !isFocus && setFocusDay(d)}
              style={{ width: 40, height: 40, padding: 0, border: 'none', borderRadius: '50%', boxSizing: 'border-box', cursor: 'default', font: isToday ? 'var(--type-body-strong)' : 'var(--type-body)',
                background: isToday ? 'var(--accent)' : 'transparent', color: isToday ? 'var(--text-on-accent)' : isSel ? 'var(--accent-text)' : inMonth ? 'var(--text-primary)' : 'var(--text-secondary)',
                boxShadow: isSel && !isToday ? 'inset 0 0 0 1px var(--accent)' : isSel && isToday ? 'inset 0 0 0 2px var(--accent), inset 0 0 0 3px var(--text-on-accent)' : 'none' }}
              onMouseEnter={(e) => { if (!isToday) e.currentTarget.style.background = 'var(--surface-hover)'; }} onMouseLeave={(e) => { if (!isToday) e.currentTarget.style.background = 'transparent'; }}>{d.getDate()}</button>;
          })}
        </div>
      </div>
    </div>}
  </div>;
}
export default DatePicker;
