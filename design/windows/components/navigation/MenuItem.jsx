import { __wGlyph } from '../internal/winui.jsx';

/* Rebuilt from the Windows UI kit menu flyout item: 40 px pitch, a 34 px highlight inset 5 px from the menu edges
   (radius 3), label 16 px from the edge. Slate's menu rules: one leading column (16 px check column, or a 20 px icon
   slot where the check replaces the icon); shortcut and chevron share the end column and dim with the row. */
export function MenuItem(props = {}) {
  const { className, style, checkmark, endContent, endContentString = '', chevron, focus, icon, secondaryText, primaryString = 'Menu item', secondaryString = '' } = props;
  const st = props.state ?? 'rest';
  const disabled = st === 'disabled' || st === 'disabled (checked only)';
  const checked = !!props.selected || st === 'rest (checked only)' || st === 'disabled (checked only)';
  const bg = disabled ? 'transparent' : st === 'pressed' ? 'var(--surface-pressed)' : st === 'hover' ? 'var(--surface-hover)' : 'transparent';
  const fg = disabled ? 'var(--text-disabled)' : 'var(--text-primary)';
  const sub = disabled ? 'var(--text-disabled)' : 'var(--text-secondary)';
  const lead = checked ? props.pxFilled20 ?? props.icon2 ?? props.pxRegular20 ?? props.icon1 : props.pxRegular20 ?? props.icon1;
  const sw = props.switch;
  return <div className={className} style={{ boxSizing: 'border-box', minHeight: 40, padding: '3px 5px', display: 'flex', width: '100%', ...style }}>
    <div style={{ flex: 1, minWidth: 0, minHeight: 34, boxSizing: 'border-box', padding: '7px 11px', borderRadius: 3, background: bg, display: 'flex', alignItems: 'center', gap: 12, color: fg, outline: focus && !disabled ? '2px solid var(--focus-ring)' : 'none', outlineOffset: -2 }}>
      {checkmark && <span style={{ width: 16, height: 16, flex: 'none', display: 'flex' }}>{checked ? __wGlyph('E73E', 16) : null}</span>}
      {icon && <span style={{ width: 20, height: 20, flex: 'none', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>{lead}</span>}
      <span style={{ flex: 1, minWidth: 0, display: 'flex', flexDirection: 'column' }}>
        <span style={{ font: 'var(--type-body)', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>{primaryString}</span>
        {secondaryText && secondaryString && <span style={{ font: 'var(--type-caption)', color: sub }}>{secondaryString}</span>}
      </span>
      {endContent && endContentString && <span style={{ font: 'var(--type-caption)', color: sub, marginLeft: 12, whiteSpace: 'nowrap' }}>{endContentString}</span>}
      {sw && <span aria-hidden="true" style={{ position: 'relative', width: 40, height: 20, flex: 'none', boxSizing: 'border-box', borderRadius: 10, border: '1px solid ' + (checked && !disabled ? 'var(--accent)' : disabled ? 'var(--border-disabled)' : 'var(--border-strong)'), background: checked && !disabled ? 'var(--accent)' : 'transparent' }}>
        <span style={{ position: 'absolute', top: 3, left: checked ? 23 : 3, width: 12, height: 12, borderRadius: '50%', background: checked && !disabled ? 'var(--text-on-accent)' : disabled ? 'var(--border-disabled)' : 'var(--text-secondary)' }} />
      </span>}
      {chevron && __wGlyph('E76C', 12, { color: sub })}
    </div>
  </div>;
}
export default MenuItem;
