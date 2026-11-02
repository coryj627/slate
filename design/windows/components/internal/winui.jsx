/* Shared helpers for the controls rebuilt from the Windows UI kit (WinUI). Internal: not on the public namespace. */

export function __wGlyph(code, size = 16, style) {
  if (code == null || code === false || code === '') return null;
  if (typeof code !== 'string') return code;
  return <span aria-hidden="true" style={{ fontFamily: 'var(--font-icons)', fontSize: size, lineHeight: size + 'px', width: size, height: size, display: 'inline-flex', alignItems: 'center', justifyContent: 'center', fontStyle: 'normal', fontWeight: 400, flex: 'none', ...style }}>{String.fromCharCode(parseInt(code, 16))}</span>;
}

/* Hover, pressed and keyboard-focus state. mouse handlers go on the hit target, focus handlers on the focusable element. */
export function __useInteract(disabled) {
  const [h, setH] = React.useState(false), [p, setP] = React.useState(false), [f, setF] = React.useState(false);
  const mouse = disabled ? {} : { onMouseEnter: () => setH(true), onMouseLeave: () => { setH(false); setP(false); }, onMouseDown: () => setP(true), onMouseUp: () => setP(false) };
  const focus = { onFocus: (e) => { let kb = true; try { kb = e.currentTarget.matches(':focus-visible'); } catch (_) {} setF(kb); }, onBlur: () => setF(false) };
  return [{ hover: !disabled && h, pressed: !disabled && p, focus: f }, mouse, focus];
}

/* Visually hidden native input, so check boxes and radios keep real semantics and keyboard behaviour. */
export const __wHidden = { position: 'absolute', opacity: 0, width: 1, height: 1, margin: 0, padding: 0, pointerEvents: 'none' };

/* Resolve a forced design state ("hover", "pressed", "disabled", "focus") against live interaction. */
export function __wState(forced, live) {
  return {
    hover: forced === 'hover' || (!forced && live.hover),
    pressed: forced === 'pressed' || (!forced && live.pressed),
    focus: forced === 'focus' || live.focus,
  };
}
