import { __slateGlyph, __SlateIconButton, __SlateMenu, __SlateSearchField } from '../internal/slateUi.jsx';

/* Tool row at the top of a panel: title and count, then search, sort and collapse/expand all (Slate addition, built
   from Fluent Button, MenuItem and the Fluent input anatomy). Search opens in place of the title; Esc clears it,
   a second Esc closes it. Extra actions go in children, before the built-in buttons. */
export function PanelToolbar({ title, count, children, search = true, query, onQueryChange, searchOpen, onSearchOpenChange, searchPlaceholder, sortOptions, sortValue, onSortChange, sortLabel = 'Sort', expanded, onExpandedChange, className, style }) {
  const [openLocal, setOpenLocal] = React.useState(false);
  const [qLocal, setQLocal] = React.useState('');
  const [menu, setMenu] = React.useState(null);
  const open = searchOpen ?? openLocal;
  const q = query ?? qLocal;
  const setOpen = (v) => { setOpenLocal(v); onSearchOpenChange && onSearchOpenChange(v); };
  const setQ = (v) => { setQLocal(v); onQueryChange && onQueryChange(v); };
  const input = React.useRef(null), sBtn = React.useRef(null), sortBtn = React.useRef(null), opened = React.useRef(false);
  React.useEffect(() => { if (open && opened.current && input.current) input.current.focus(); opened.current = true; }, [open]);
  const close = () => { setQ(''); setOpen(false); setTimeout(() => sBtn.current && sBtn.current.focus(), 0); };
  const ph = searchPlaceholder || (title ? `Search ${String(title).toLowerCase()}` : 'Search');
  const sortItems = (sortOptions || []).map((o) => (o.separator || o.value === undefined ? o : { label: o.label, checked: o.value === sortValue, onSelect: () => onSortChange && onSortChange(o.value) }));
  return <div className={className} style={{ height: 40, display: 'flex', alignItems: 'center', gap: 2, padding: '0 4px 0 12px', boxSizing: 'border-box', ...style }}>
    {open
      ? <__SlateSearchField inputRef={input} value={q} onChange={setQ} placeholder={ph} style={{ marginRight: 2 }}
          onKeyDown={(e) => { if (e.key === 'Escape') { e.preventDefault(); e.stopPropagation(); if (q) setQ(''); else close(); } }}
          trailing={<__SlateIconButton icon="E711" label="Close search" shortcut="Esc" size="small" onClick={close} />} />
      : <div style={{ flex: 1, minWidth: 0, display: 'flex', alignItems: 'baseline', gap: 6 }}>
          <span style={{ font: '600 14px/20px var(--font-ui)', color: 'var(--text-primary)', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>{title}</span>
          {count != null && <span style={{ font: '12px/16px var(--font-ui)', color: 'var(--text-tertiary)', fontVariantNumeric: 'tabular-nums', flex: 'none' }}>{count}</span>}
        </div>}
    {children}
    {search && !open && <__SlateIconButton buttonRef={sBtn} icon="E721" label={ph} onClick={() => setOpen(true)} />}
    {sortItems.length > 0 && <span style={{ position: 'relative', display: 'inline-flex' }}>
      <__SlateIconButton buttonRef={sortBtn} icon="E8CB" label={sortLabel} haspopup="menu" expanded={!!menu} onClick={(e) => setMenu(menu ? null : { kb: e.detail === 0 })} />
      {menu && <__SlateMenu label={sortLabel} keyboard={menu.kb} align="end" width={240} items={sortItems} onClose={(back) => { setMenu(null); if (back && sortBtn.current) sortBtn.current.focus(); }} />}
    </span>}
    {expanded !== undefined && <__SlateIconButton icon={expanded ? 'F166' : 'F164'} label={expanded ? 'Collapse all' : 'Expand all'} onClick={() => onExpandedChange && onExpandedChange(!expanded)} />}
  </div>;
}
