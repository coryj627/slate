/* Files sidebar — PanelToolbar (sort and group, collapse/expand all), filter, Shortcuts, Recents, tree, Tags (CountListRow).
   Row anatomy per docs/help/sidebar.md. */
function SidebarSection({ title, children, defaultOpen = true, right }) {
  const [open, setOpen] = React.useState(defaultOpen);
  return <div style={{ marginBottom: 6 }}>
    <button onClick={() => setOpen(!open)} aria-expanded={open} style={{ width: '100%', height: 28, display: 'flex', alignItems: 'center', gap: 6, padding: '0 8px', border: 'none', background: 'transparent', color: 'var(--text-secondary)', font: '600 12px/16px var(--font-ui)', cursor: 'pointer', textAlign: 'left' }}>
      <SIcon k={open ? 'chevD' : 'chevR'} size={12} /><span style={{ flex: 1 }}>{title}</span>{right}</button>
    {open && children}
  </div>;
}
function FileRow({ item, depth, selected, onOpen }) {
  const [h, setH] = React.useState(false);
  return <div role="treeitem" aria-selected={selected} tabIndex={0} onClick={() => onOpen(item.name)} onKeyDown={e => e.key === 'Enter' && onOpen(item.name)}
    onMouseEnter={() => setH(true)} onMouseLeave={() => setH(false)} title={item.name + '.md'}
    style={{ position: 'relative', display: 'flex', gap: 8, padding: `5px 8px 5px ${12 + depth * 16}px`, margin: '0 4px', borderRadius: 4, cursor: 'pointer',
      background: selected ? 'var(--surface-selected)' : h ? 'var(--surface-hover)' : 'transparent' }}>
    {selected && <span style={{ position: 'absolute', left: 0, top: 8, bottom: 8, width: 3, borderRadius: 2, background: 'var(--accent-indicator)' }} />}
    <SIcon k="doc" size={16} style={{ marginTop: 2, color: 'var(--text-secondary)' }} />
    <div style={{ minWidth: 0, flex: 1 }}>
      <div style={{ font: `${selected ? 600 : 400} 14px/20px var(--font-ui)`, color: 'var(--text-primary)', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>{item.name}</div>
      <div style={{ display: 'flex', gap: 8, alignItems: 'center', font: '12px/16px var(--font-ui)', color: 'var(--text-tertiary)' }}>
        <span>{item.date}</span>
        {item.tasks && <span style={{ display: 'inline-flex', gap: 3, alignItems: 'center' }}><SIcon k="task" size={12} />{item.tasks}</span>}
      </div>
    </div>
    {item.pinned && <SIcon k="pin" size={12} style={{ marginTop: 4, color: 'var(--text-secondary)' }} />}
  </div>;
}
function FolderRow({ f, depth, current, onOpen, sig }) {
  const [open, setOpen] = React.useState(!!f.open);
  React.useEffect(() => { if (sig && sig.open !== null) setOpen(sig.open); }, [sig && sig.v]);
  const [h, setH] = React.useState(false);
  return <div role="group">
    <div role="treeitem" aria-expanded={open} tabIndex={0} onClick={() => setOpen(!open)} onMouseEnter={() => setH(true)} onMouseLeave={() => setH(false)}
      style={{ display: 'flex', alignItems: 'center', gap: 6, height: 28, padding: `0 8px 0 ${8 + depth * 16}px`, margin: '0 4px', borderRadius: 4, cursor: 'pointer', background: h ? 'var(--surface-hover)' : 'transparent', font: '14px/20px var(--font-ui)', color: 'var(--text-primary)' }}>
      <SIcon k={open ? 'chevD' : 'chevR'} size={12} style={{ color: 'var(--text-secondary)' }} />
      <SIcon k={open ? 'folderOpen' : 'folder'} size={16} style={{ color: 'var(--text-secondary)' }} />
      <span style={{ flex: 1 }}>{f.name}</span><span style={{ font: '12px var(--font-ui)', color: 'var(--text-tertiary)' }}>{f.children.length}</span>
    </div>
    {open && f.children.map(c => <FileRow key={c.name} item={c} depth={depth + 1} selected={current === c.name} onOpen={onOpen} />)}
  </div>;
}
function Sidebar({ current, onOpen }) {
  const [q, setQ] = React.useState('');
  const [sort, setSort] = React.useState('name-asc');
  const [group, setGroup] = React.useState(false);
  const [sig, setSig] = React.useState({ v: 0, open: null });
  const [tagOpen, setTagOpen] = React.useState({});
  const [tag, setTag] = React.useState(null);
  const { PanelToolbar, CountListRow } = SLATE_NS;
  const byName = (a, b) => (sort === 'name-desc' ? b.name.localeCompare(a.name) : a.name.localeCompare(b.name));
  const vault = sort.startsWith('name') ? [...SLATE_VAULT].sort(byName).map((f) => ({ ...f, children: [...f.children].sort(byName) })) : SLATE_VAULT;
  const all = SLATE_VAULT.flatMap(f => f.children.map(c => ({ ...c, folder: f.name })));
  const hits = q ? all.filter(c => c.name.toLowerCase().includes(q.toLowerCase())) : null;
  return <nav aria-label="Files" style={{ width: 'var(--sidebar-width)', flex: 'none', display: 'flex', flexDirection: 'column', background: 'var(--surface-window)', borderRight: '1px solid var(--border-subtle)', minHeight: 0 }}>
    {PanelToolbar && <PanelToolbar title="demo-vault" search={false} sortLabel="Sort and group" sortValue={sort} onSortChange={setSort}
      sortOptions={[...SLATE_SORT, { separator: true }, { label: 'Group by date', checked: group, onSelect: () => setGroup((g) => !g) }]}
      expanded={sig.open !== false} onExpandedChange={(v) => setSig((x) => ({ v: x.v + 1, open: v }))}>
      <IconBtn k="add" label="New note (Ctrl+N)" />
    </PanelToolbar>}
    <div style={{ padding: '0 12px 8px' }}>
      <label style={{ display: 'flex', alignItems: 'center', gap: 6, height: 32, padding: '0 8px', borderRadius: 4, background: 'var(--surface-card)', border: '1px solid var(--border-subtle)', borderBottom: '1px solid var(--border-strong)' }}>
        <SIcon k="search" size={16} style={{ color: 'var(--text-secondary)' }} />
        <input value={q} onChange={e => setQ(e.target.value)} onKeyDown={e => e.key === 'Escape' && setQ('')} placeholder="Filter  (#tag  @last7d  has:task)" aria-label="Filter files"
          style={{ flex: 1, minWidth: 0, border: 'none', outline: 'none', background: 'transparent', font: '14px/20px var(--font-ui)', color: 'var(--text-primary)' }} />
      </label>
    </div>
    <div role="tree" style={{ flex: 1, overflow: 'auto', paddingBottom: 8 }}>
      {hits ? <div><div style={{ padding: '4px 12px', font: '12px var(--font-ui)', color: 'var(--text-secondary)' }}>{hits.length ? hits.length + ' results.' : 'No results.'}</div>
        {hits.map(c => <FileRow key={c.folder + c.name} item={{ ...c, date: c.date + ' · in ' + c.folder }} depth={0} selected={current === c.name} onOpen={onOpen} />)}</div> : <>
        <SidebarSection title="Shortcuts">
          {[['Slate', 'star'], ['learning', 'folder'], ['#reading', 'tag']].map(([n, k]) => <div key={n} onClick={() => n === 'Slate' && onOpen('Slate')} style={{ display: 'flex', alignItems: 'center', gap: 8, height: 28, padding: '0 12px 0 28px', font: '14px/20px var(--font-ui)', cursor: 'pointer', color: 'var(--text-primary)' }}>
            <SIcon k={k} size={16} style={{ color: 'var(--text-secondary)' }} />{n}</div>)}
        </SidebarSection>
        <SidebarSection title="Recents" defaultOpen={false}>
          {['Why Slate', '2026-05-28', 'Linear algebra glossary'].map(n => <div key={n} onClick={() => onOpen(n)} style={{ display: 'flex', alignItems: 'center', gap: 8, height: 28, padding: '0 12px 0 28px', font: '14px/20px var(--font-ui)', cursor: 'pointer' }}><SIcon k="clock" size={16} style={{ color: 'var(--text-secondary)' }} />{n}</div>)}
        </SidebarSection>
        <SidebarSection title="Files">{vault.map(f => <FolderRow key={f.name} f={f} depth={0} current={current} onOpen={onOpen} sig={sig} />)}</SidebarSection>
        <SidebarSection title="Tags" defaultOpen={false} right={<span style={{ fontWeight: 400 }}>12 tags</span>}>
          <div role="tree" aria-label="Tags" style={{ padding: '0 4px 0 12px' }}>{SLATE_TAGS.map(t => <React.Fragment key={t.t}>
            <CountListRow icon="E8EC" label={t.t} count={t.n} countLabel={t.n === 1 ? 'note' : 'notes'} nested={!t.kids} expanded={t.kids ? !!tagOpen[t.t] : undefined} onToggle={(v) => setTagOpen((o) => ({ ...o, [t.t]: v }))} selected={tag === t.t} onClick={() => setTag(t.t)} />
            {t.kids && tagOpen[t.t] && t.kids.map(k => <CountListRow key={k.t} icon="E8EC" label={k.t} count={k.n} countLabel={k.n === 1 ? 'note' : 'notes'} depth={1} nested selected={tag === t.t + '/' + k.t} onClick={() => setTag(t.t + '/' + k.t)} />)}
          </React.Fragment>)}</div>
        </SidebarSection>
      </>}
    </div>
  </nav>;
}
Object.assign(window, { Sidebar });
