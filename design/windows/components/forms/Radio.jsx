import { __useInteract, __wHidden, __wState } from '../internal/winui.jsx';

/* Rebuilt from the Windows UI kit "Radio Button / Radio Button": a 20 px circle (1 px stroke) in a 32 px row, 8 px gap.
   Checked: accent fill with an 8 px dot (10 px on hover, 6 px pressed). Colours are Slate tokens. */
export function Radio(props = {}) {
  const { className, style, layout = 'icon+label after', labelText, children, onChange, name, id, value, icon1 } = props;
  const given = props.checked === true || props.checked === 'on';
  const [on, setOn] = React.useState(given);
  React.useEffect(() => setOn(given), [given]);
  const disabled = !!props.disabled || props.state === 'disabled';
  const [live, mouse, focusOn] = __useInteract(disabled);
  const s = __wState(props.state, live);
  const fill = disabled ? (on ? 'var(--border-disabled)' : 'transparent')
    : on ? (s.pressed ? 'var(--accent-pressed)' : s.hover ? 'var(--accent-hover)' : 'var(--accent)')
    : s.pressed ? 'var(--control-alt-fill-pressed)' : s.hover ? 'var(--control-alt-fill-hover)' : 'var(--control-alt-fill)';
  const stroke = disabled ? 'var(--border-disabled)' : on ? fill : 'var(--border-strong)';
  const dot = s.pressed ? 6 : s.hover ? 10 : 8;
  const below = layout === 'icon+label below';
  const text = children ?? labelText ?? 'Label';
  const pick = () => { if (disabled || on) return; setOn(true); onChange && onChange(true); };
  return <label className={className} {...mouse} style={{ position: 'relative', display: 'inline-flex', flexDirection: below ? 'column' : 'row', alignItems: 'center', gap: below ? 4 : 8, minHeight: 32, boxSizing: 'border-box', borderRadius: 4, cursor: disabled ? 'default' : 'pointer', outline: s.focus ? '2px solid var(--focus-ring)' : 'none', outlineOffset: 1, font: 'var(--type-body)', color: disabled ? 'var(--text-disabled)' : 'var(--text-primary)', ...style }}>
    <input type="radio" id={id} name={name} value={value} checked={on} disabled={disabled} aria-label={layout === 'icon only' && typeof text === 'string' ? text : undefined} onChange={pick} {...focusOn} style={__wHidden} />
    {icon1 ?? <span aria-hidden="true" style={{ width: 20, height: 20, flex: 'none', boxSizing: 'border-box', borderRadius: '50%', border: '1px solid ' + stroke, background: fill, display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
      {on && <span style={{ width: dot, height: dot, borderRadius: '50%', background: disabled ? 'var(--surface-card)' : 'var(--text-on-accent)' }} />}
    </span>}
    {layout !== 'icon only' && <span>{text}</span>}
  </label>;
}
export default Radio;
