import { __wGlyph } from '../internal/winui.jsx';

/* Rebuilt from the Windows UI kit navigation view item: 40 px pitch with a 34 px highlight inset 5 px (radius 3), a 16 px
   icon 16 px from the edge and the label 12 px after it. Selected rows use Slate's pale gold fill and a 3 × 16 px gold pill
   on the leading edge. */
export function NavNodeMedium(props = {}) {
  const { className, style, content = 'Navigation item', selected, open, focus, subItem, group, secondaryActions } = props;
  const st = props.state ?? 'rest';
  const icon = selected ? props.iconBeforeFilled ?? props.iconBeforeRegular ?? props.icon1 : props.iconBeforeRegular ?? props.icon1;
  const bg = st === 'pressed' ? 'var(--surface-pressed)' : selected ? 'var(--surface-selected)' : st === 'hover' ? 'var(--surface-hover)' : 'transparent';
  const expandable = group || open !== undefined || subItem != null;
  return <div className={className} style={{ display: 'flex', flexDirection: 'column', ...style }}>
    <div style={{ minHeight: 40, padding: '3px 5px', boxSizing: 'border-box', display: 'flex' }}>
      <div style={{ position: 'relative', flex: 1, minWidth: 0, minHeight: 34, boxSizing: 'border-box', padding: '7px 11px', borderRadius: 3, background: bg, display: 'flex', alignItems: 'center', gap: 12, color: 'var(--text-primary)', outline: focus ? '2px solid var(--focus-ring)' : 'none', outlineOffset: -2 }}>
        {selected && <span style={{ position: 'absolute', left: -1, top: '50%', marginTop: -8, width: 3, height: 16, borderRadius: 999, background: 'var(--accent-indicator)' }} />}
        {icon != null && <span style={{ width: 16, height: 16, flex: 'none', display: 'flex', alignItems: 'center', justifyContent: 'center', color: selected ? 'var(--accent-text)' : 'inherit' }}>{icon}</span>}
        <span style={{ flex: 1, minWidth: 0, font: 'var(--type-body)', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>{content}</span>
        {secondaryActions && __wGlyph('E712', 16, { color: 'var(--text-secondary)' })}
        {expandable && __wGlyph(open ? 'E70E' : 'E70D', 12, { color: 'var(--text-secondary)' })}
      </div>
    </div>
    {open && subItem}
  </div>;
}
export default NavNodeMedium;
