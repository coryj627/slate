import { NavNodeMedium } from '../navigation/NavNodeMedium.jsx';
import { __slateGlyph, __SlateSearchField, __useRing } from '../internal/slateUi.jsx';

/* Settings window body: category list on the left (Fluent vertical tabs, optional search above), the open category's
   title, description and SettingRows on the right (Slate addition). Up/Down move between categories. */
export function SettingsLayout({ categories = [], value, onChange, title, description, query, onQueryChange, searchPlaceholder = 'Find a setting', navLabel = 'Settings categories', children, className, style }) {
  const cur = categories.find((c) => c.id === value) || categories[0];
  const tabs = React.useRef([]);
  const idx = Math.max(0, categories.indexOf(cur));
  const go = (i) => { const n = categories.length; if (!n) return; const j = (i + n) % n; if (tabs.current[j]) tabs.current[j].focus(); onChange && onChange(categories[j].id); };
  const key = (i) => (e) => {
    if (e.key === 'ArrowDown') go(i + 1); else if (e.key === 'ArrowUp') go(i - 1); else if (e.key === 'Home') go(0); else if (e.key === 'End') go(categories.length - 1); else return;
    e.preventDefault();
  };
  const heading = title || (cur && cur.label);
  return <div className={className} style={{ display: 'grid', gridTemplateColumns: '240px minmax(0,1fr)', height: '100%', minHeight: 0, background: 'var(--surface-window)', color: 'var(--text-primary)', ...style }}>
    <div style={{ display: 'flex', flexDirection: 'column', gap: 8, padding: '12px 8px', minHeight: 0, overflow: 'auto' }}>
      {onQueryChange && <__SlateSearchField value={query} onChange={onQueryChange} placeholder={searchPlaceholder} style={{ flex: 'none' }} />}
      <div role="tablist" aria-orientation="vertical" aria-label={navLabel} style={{ display: 'flex', flexDirection: 'column', margin: '0 -5px' }}>
        {categories.map((c, i) => <__Category key={c.id} c={c} on={c === cur} tabIndex={i === idx ? 0 : -1} btnRef={(el) => (tabs.current[i] = el)} onClick={() => onChange && onChange(c.id)} onKeyDown={key(i)} />)}
      </div>
    </div>
    <section role="tabpanel" aria-label={heading} style={{ minHeight: 0, overflow: 'auto', padding: '16px 32px 32px 24px' }}>
      <h2 style={{ margin: 0, font: '600 20px/28px var(--font-ui-display)', color: 'var(--text-primary)' }}>{heading}</h2>
      {description && <p style={{ margin: '4px 0 0', maxWidth: 560, font: '14px/20px var(--font-ui)', color: 'var(--text-secondary)', textWrap: 'pretty' }}>{description}</p>}
      <div style={{ display: 'flex', flexDirection: 'column', gap: 4, marginTop: 16 }}>{children}</div>
    </section>
  </div>;
}

function __Category({ c, on, tabIndex, btnRef, onClick, onKeyDown }) {
  const [h, setH] = React.useState(false), [p, setP] = React.useState(false);
  const [ring, rp] = __useRing();
  const g = __slateGlyph(c.icon, 16);
  return <button ref={btnRef} type="button" role="tab" aria-selected={on} tabIndex={tabIndex} onClick={onClick} onKeyDown={onKeyDown} {...rp}
    onMouseEnter={() => setH(true)} onMouseLeave={() => { setH(false); setP(false); }} onMouseDown={() => setP(true)} onMouseUp={() => setP(false)}
    style={{ display: 'block', width: '100%', padding: 0, border: 'none', borderRadius: 4, background: 'transparent', textAlign: 'left', cursor: 'pointer', outline: 'none' }}>
    <NavNodeMedium style={{ width: '100%' }} content={c.label} selected={on} state={p ? 'pressed' : h ? 'hover' : 'rest'} focus={ring} iconBeforeRegular={g} iconBeforeFilled={g} />
  </button>;
}
