import { __slateGlyph } from '../internal/slateUi.jsx';

/* One setting: optional icon, label, description and the control on the right (or below with `stacked`).
   Slate addition following the Windows 11 Settings card: surface card, subtle border, 4 px radius. */
export function SettingRow({ label, description, icon, children, htmlFor, disabled = false, stacked = false, className, style }) {
  const id = React.useId();
  const fg = disabled ? 'var(--text-disabled)' : 'var(--text-primary)';
  const Label = htmlFor ? 'label' : 'div';
  return <div role="group" aria-labelledby={`${id}-l`} aria-describedby={description ? `${id}-d` : undefined} className={className}
    style={{ display: 'flex', flexDirection: stacked ? 'column' : 'row', alignItems: stacked ? 'stretch' : 'center', gap: stacked ? 12 : 16, minHeight: 68, padding: '12px 16px', boxSizing: 'border-box', borderRadius: 4, background: 'var(--surface-card)', border: '1px solid var(--border-subtle)', ...style }}>
    <div style={{ flex: 1, minWidth: 0, display: 'flex', alignItems: 'center', gap: 16 }}>
      {icon && __slateGlyph(icon, 20, { color: fg })}
      <div style={{ minWidth: 0, display: 'flex', flexDirection: 'column', gap: 2 }}>
        <Label id={`${id}-l`} htmlFor={htmlFor} style={{ font: '14px/20px var(--font-ui)', color: fg }}>{label}</Label>
        {description && <span id={`${id}-d`} style={{ font: '12px/16px var(--font-ui)', color: disabled ? 'var(--text-disabled)' : 'var(--text-secondary)', textWrap: 'pretty' }}>{description}</span>}
      </div>
    </div>
    {children && <div style={{ flex: 'none', display: 'flex', alignItems: 'center', gap: 8, justifyContent: stacked ? 'flex-start' : 'flex-end', paddingLeft: stacked && icon ? 36 : 0 }}>{children}</div>}
  </div>;
}
