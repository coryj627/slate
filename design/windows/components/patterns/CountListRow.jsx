import { __slateGlyph, __useRing } from '../internal/slateUi.jsx';

/* One row of a counted list (tags, properties, backlinks): chevron, icon, label and a right-aligned count
   (Slate addition, sized like the Fluent TreeItem: 32 px medium, 24 px small). Click selects; the chevron
   expands. Enter/Space selects, Right/Left expand and collapse. */
export function CountListRow({ label, count, icon, depth = 0, expanded, onToggle, nested = false, selected = false, onClick, size = 'medium', countLabel, role = 'treeitem', tabIndex = 0, disabled = false, className, style }) {
  const [h, setH] = React.useState(false);
  const [ring, rp] = __useRing();
  const small = size === 'small' || size === 'sm';
  const canExpand = expanded !== undefined;
  const act = () => { if (!disabled && onClick) onClick(); };
  const key = (e) => {
    if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); act(); }
    else if (e.key === 'ArrowRight' && canExpand && !expanded) { e.preventDefault(); onToggle && onToggle(true); }
    else if (e.key === 'ArrowLeft' && canExpand && expanded) { e.preventDefault(); onToggle && onToggle(false); }
  };
  const dim = disabled ? 'var(--text-disabled)' : null;
  const name = count != null ? `${label}, ${count}${countLabel ? ' ' + countLabel : ''}` : label;
  return <div role={role} aria-level={role === 'treeitem' ? depth + 1 : undefined} aria-expanded={canExpand ? !!expanded : undefined} aria-selected={role === 'treeitem' || role === 'option' ? !!selected : undefined}
    aria-disabled={disabled || undefined} aria-label={name} tabIndex={tabIndex} {...rp} onClick={act} onKeyDown={key} onMouseEnter={() => setH(true)} onMouseLeave={() => setH(false)} className={className}
    style={{ position: 'relative', height: small ? 24 : 32, display: 'flex', alignItems: 'center', gap: 6, padding: `0 8px 0 ${4 + depth * 20}px`, borderRadius: 4, boxSizing: 'border-box', cursor: disabled ? 'default' : 'pointer',
      background: selected ? 'var(--surface-selected)' : h && !disabled ? 'var(--surface-hover)' : 'transparent', outline: ring ? '2px solid var(--focus-ring)' : 'none', outlineOffset: -2, ...style }}>
    {selected && <span aria-hidden="true" style={{ position: 'absolute', left: 0, top: small ? 5 : 8, bottom: small ? 5 : 8, width: 3, borderRadius: 2, background: 'var(--accent-indicator)' }} />}
    {canExpand
      ? <span aria-hidden="true" onClick={(e) => { e.stopPropagation(); if (!disabled && onToggle) onToggle(!expanded); }} style={{ width: 20, height: 20, flex: 'none', display: 'inline-flex', alignItems: 'center', justifyContent: 'center', borderRadius: 4, color: dim || 'var(--text-secondary)' }}>{__slateGlyph(expanded ? 'E70D' : 'E76C', 12)}</span>
      : nested ? <span aria-hidden="true" style={{ width: 20, flex: 'none' }} /> : null}
    {icon && __slateGlyph(icon, 16, { color: dim || 'var(--text-secondary)' })}
    <span style={{ flex: 1, minWidth: 0, font: `${selected ? 600 : 400} 14px/20px var(--font-ui)`, color: dim || 'var(--text-primary)', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>{label}</span>
    {count != null && <span aria-hidden="true" style={{ flex: 'none', font: '12px/16px var(--font-ui)', color: dim || 'var(--text-tertiary)', fontVariantNumeric: 'tabular-nums' }}>{count}</span>}
  </div>;
}
