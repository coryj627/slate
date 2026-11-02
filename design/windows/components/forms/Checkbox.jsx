import { __wGlyph, __useInteract, __wHidden, __wState } from '../internal/winui.jsx';

/* Rebuilt from the Windows UI kit "Checkbox / Checkbox": a 20 px box (18 px fill inside a 1 px stroke, radius 3 inside 4)
   in a 32 px row with an 8 px gap before the label. Glyphs are the WPF Fluent theme's: E73E checked, E9AE indeterminate.
   Colours are Slate tokens. */
export function Checkbox(props = {}) {
  const { className, style, label = true, labelText, children, onChange, name, id, value } = props;
  const circular = (props.appearance ?? props.style2) === 'circular';
  const given = props.checked !== undefined ? (props.checked === 'mixed' ? 'indeterminate' : props.checked ? 'checked' : 'unchecked') : props.status ?? 'unchecked';
  const [val, setVal] = React.useState(given);
  React.useEffect(() => setVal(given), [given]);
  const disabled = !!props.disabled || props.state === 'disabled';
  const [live, mouse, focusOn] = __useInteract(disabled);
  const s = __wState(props.state, live);
  const lit = val !== 'unchecked';
  const fill = disabled ? (lit ? 'var(--border-disabled)' : 'transparent')
    : lit ? (s.pressed ? 'var(--accent-pressed)' : s.hover ? 'var(--accent-hover)' : 'var(--accent)')
    : s.pressed ? 'var(--control-alt-fill-pressed)' : s.hover ? 'var(--control-alt-fill-hover)' : 'var(--control-alt-fill)';
  const stroke = disabled ? 'var(--border-disabled)' : lit ? fill : 'var(--border-strong)';
  const ref = React.useRef(null);
  React.useEffect(() => { if (ref.current) ref.current.indeterminate = val === 'indeterminate'; }, [val]);
  const toggle = () => { if (disabled) return; const next = val === 'checked' ? 'unchecked' : 'checked'; setVal(next); onChange && onChange(next === 'checked'); };
  const text = children ?? labelText ?? 'Label';
  return <label className={className} {...mouse} style={{ position: 'relative', display: 'inline-flex', alignItems: 'center', gap: 8, minHeight: 32, boxSizing: 'border-box', borderRadius: 4, cursor: disabled ? 'default' : 'pointer', outline: s.focus ? '2px solid var(--focus-ring)' : 'none', outlineOffset: 1, font: 'var(--type-body)', color: disabled ? 'var(--text-disabled)' : 'var(--text-primary)', ...style }}>
    <input ref={ref} type="checkbox" id={id} name={name} value={value} checked={val === 'checked'} disabled={disabled} aria-label={label === false && typeof text === 'string' ? text : undefined} onChange={toggle} {...focusOn} style={__wHidden} />
    <span aria-hidden="true" style={{ width: 20, height: 20, flex: 'none', boxSizing: 'border-box', borderRadius: circular ? '50%' : 4, border: '1px solid ' + stroke, background: fill, display: 'flex', alignItems: 'center', justifyContent: 'center', color: disabled ? 'var(--surface-card)' : 'var(--text-on-accent)' }}>
      {val === 'checked' && __wGlyph('E73E', 12)}{val === 'indeterminate' && __wGlyph('E9AE', 12)}
    </span>
    {label !== false && <span>{text}</span>}
  </label>;
}
export default Checkbox;
