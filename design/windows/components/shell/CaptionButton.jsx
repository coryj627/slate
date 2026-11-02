import { __wGlyph, __useInteract } from '../internal/winui.jsx';

const __CAP = { minimize: ['E921', 'Minimize'], maximize: ['E922', 'Maximize'], restore: ['E923', 'Restore'], close: ['E8BB', 'Close'] };

/* Title bar caption button from the Windows UI kit (Shell › Title bar): 10 px Segoe Fluent Icons glyph, subtle hover and
   pressed fills, Close turns #C42B1C with a white glyph. 46 px wide (Slate keeps the Windows width) × the title bar height. */
export function CaptionButton(props = {}) {
  const { className, style, kind = 'close', inactive, label, onClick, height = 48, buttonRef } = props;
  const [live, mouse, focusOn] = __useInteract(false);
  const st = props.state ?? (live.pressed ? 'pressed' : live.hover ? 'hover' : 'rest');
  const close = kind === 'close';
  const hot = st === 'hover' || st === 'pressed';
  const bg = !hot ? 'transparent' : close ? (st === 'pressed' ? 'rgba(196,43,28,0.9)' : '#C42B1C') : st === 'pressed' ? 'var(--surface-pressed)' : 'var(--surface-hover)';
  const fg = close && hot ? (st === 'pressed' ? 'rgba(255,255,255,0.7)' : '#FFFFFF') : inactive ? 'var(--text-disabled)' : st === 'pressed' ? 'var(--text-secondary)' : 'var(--text-primary)';
  const [code, name] = __CAP[kind] || __CAP.close;
  return <button ref={buttonRef} type="button" className={className} aria-label={label ?? name} title={label ?? name} onClick={onClick} {...mouse} {...focusOn}
    style={{ width: 46, height, flex: 'none', padding: 0, border: 'none', background: bg, color: fg, display: 'inline-flex', alignItems: 'center', justifyContent: 'center', cursor: 'default', outline: live.focus ? '2px solid var(--focus-ring)' : 'none', outlineOffset: -2, ...style }}>
    {__wGlyph(code, 10)}
  </button>;
}
export default CaptionButton;
