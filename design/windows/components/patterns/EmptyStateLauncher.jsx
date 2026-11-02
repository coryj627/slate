import { MenuItem } from '../navigation/MenuItem.jsx';
import { __slateGlyph, __useRing } from '../internal/slateUi.jsx';

/* Empty tab or pane: optional logo, a title, a line of help and the primary actions with their shortcuts
   (Slate addition; each action is a Fluent MenuItem row inside a native button). */
export function EmptyStateLauncher({ title = 'No note open', description, actions = [], logo, className, style }) {
  return <div className={className} style={{ flex: 1, minHeight: 0, display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', gap: 16, padding: 24, boxSizing: 'border-box', background: 'var(--surface-card)', ...style }}>
    {logo && <img src={logo} alt="" style={{ width: 48, height: 48, borderRadius: 8, display: 'block' }} />}
    {(title || description) && <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 4, textAlign: 'center' }}>
      {title && <h2 style={{ margin: 0, font: '600 20px/28px var(--font-ui-display)', color: 'var(--text-primary)' }}>{title}</h2>}
      {description && <p style={{ margin: 0, maxWidth: 360, font: '14px/20px var(--font-ui)', color: 'var(--text-secondary)', textWrap: 'pretty' }}>{description}</p>}
    </div>}
    {actions.length > 0 && <ul style={{ listStyle: 'none', margin: 0, padding: 0, width: 360, maxWidth: '100%', display: 'flex', flexDirection: 'column', gap: 0 }}>
      {actions.map((a, i) => <li key={i}><__LaunchRow {...a} /></li>)}
    </ul>}
  </div>;
}

function __LaunchRow({ label, shortcut, icon, onClick, disabled }) {
  const [h, setH] = React.useState(false), [p, setP] = React.useState(false);
  const [ring, rp] = __useRing();
  const g = icon ? __slateGlyph(icon, 16, { width: 20, height: 20 }) : undefined;
  const st = disabled ? 'disabled' : p ? 'pressed' : h ? 'hover' : 'rest';
  return <button type="button" onClick={onClick} disabled={disabled} aria-keyshortcuts={shortcut || undefined} {...rp}
    onMouseEnter={() => setH(true)} onMouseLeave={() => { setH(false); setP(false); }} onMouseDown={() => setP(true)} onMouseUp={() => setP(false)}
    style={{ display: 'block', width: '100%', padding: 0, border: 'none', borderRadius: 4, background: 'transparent', textAlign: 'left', cursor: disabled ? 'default' : 'pointer', outline: 'none' }}>
    <MenuItem style={{ width: '100%', ...(st === 'rest' ? { backgroundColor: 'transparent' } : {}) }} icon={!!icon} pxRegular20={g} pxFilled20={g} checkmark={false} chevron={false} endContent={!!shortcut} endContentString={shortcut || ''} secondaryText={false} primaryString={label} state={st} focus={ring && st === 'rest'} />
  </button>;
}
