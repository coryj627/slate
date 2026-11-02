import { __wGlyph } from '../internal/winui.jsx';

/* Scroll bar from the Windows UI kit (Scrolling): a 14 px lane. At rest, a 2 px thumb 4 px from the outer edge. On hover
   (or expanded), a 12 px track with a 6 px thumb and 14 × 16 caret buttons (8 px glyphs). Drag the thumb, or click the
   track or a caret, to move. position and thumb are fractions (0–1). */
export function ScrollBar(props = {}) {
  const { className, style, orientation = 'vertical', length = 200, thumb = 0.66, position, onChange, expanded, label = 'Scroll' } = props;
  const v = orientation !== 'horizontal';
  const [own, setOwn] = React.useState(position ?? 0);
  React.useEffect(() => { if (position !== undefined) setOwn(position); }, [position]);
  const pos = Math.min(1, Math.max(0, position !== undefined ? position : own));
  const [h, setH] = React.useState(false);
  const ex = expanded ?? h;
  const caret = ex ? 16 : 0;
  const pad = ex ? 1 : 2;
  const track = length - pad * 2 - caret * 2;
  const tl = Math.max(16, track * Math.min(1, thumb));
  const at = (track - tl) * pos;
  const set = (p) => { const c = Math.min(1, Math.max(0, p)); setOwn(c); onChange && onChange(c); };
  const drag = React.useRef(null);
  const down = (e) => { e.preventDefault(); e.stopPropagation(); e.currentTarget.setPointerCapture(e.pointerId); drag.current = { start: v ? e.clientY : e.clientX, pos }; };
  const move = (e) => { if (!drag.current || track <= tl) return; const d = (v ? e.clientY : e.clientX) - drag.current.start; set(drag.current.pos + d / (track - tl)); };
  const up = () => { drag.current = null; };
  const step = (d) => set(pos + d * 0.1);
  const box = v ? { width: 14, height: length } : { width: length, height: 14 };
  const along = (n) => (v ? { top: n } : { left: n });
  const thick = ex ? 6 : 2;
  const cross = ex ? (14 - thick) / 2 : 8;
  const caretBtn = (d) => <span role="button" aria-label={d < 0 ? 'Scroll back' : 'Scroll forward'} onMouseDown={(e) => { e.preventDefault(); step(d); }}
    style={{ position: 'absolute', ...(v ? { left: 0, width: 14, height: 16, [d < 0 ? 'top' : 'bottom']: 0 } : { top: 0, height: 14, width: 16, [d < 0 ? 'left' : 'right']: 0 }), display: 'flex', alignItems: 'center', justifyContent: 'center', color: 'var(--text-secondary)' }}>
    {__wGlyph(v ? (d < 0 ? 'EDDB' : 'EDDC') : d < 0 ? 'EDD9' : 'EDDA', 8)}
  </span>;
  return <div className={className} role="scrollbar" aria-orientation={v ? 'vertical' : 'horizontal'} aria-label={label} aria-valuemin={0} aria-valuemax={100} aria-valuenow={Math.round(pos * 100)}
    onMouseEnter={() => setH(true)} onMouseLeave={() => setH(false)}
    onMouseDown={(e) => { if (e.target !== e.currentTarget) return; const r = e.currentTarget.getBoundingClientRect(); const p = (v ? e.clientY - r.top : e.clientX - r.left) - pad - caret; step(p < at ? -1 : 1); }}
    style={{ position: 'relative', ...box, flex: 'none', boxSizing: 'border-box', borderRadius: v ? '0 7px 7px 0' : '0 0 7px 7px', background: ex ? 'var(--neutral-background-1-rest)' : 'transparent', transition: 'background var(--duration-faster) var(--curve-easy-ease)', ...style }}>
    {ex && caretBtn(-1)}{ex && caretBtn(1)}
    <span onPointerDown={down} onPointerMove={move} onPointerUp={up}
      style={{ position: 'absolute', ...(v ? { left: cross, width: thick, height: tl } : { top: cross, height: thick, width: tl }), ...along(pad + caret + at), borderRadius: 999, background: 'var(--border-strong)', cursor: 'default' }} />
  </div>;
}
export default ScrollBar;
