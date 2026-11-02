/* Slate Windows UI kit — shared data + small primitives (icons, kbd). */
const SLATE_NS = window.SlateDesignSystem_326dc9 || {};

/* Icon order (readme → Iconography): Segoe Fluent Icons glyphs first — the icon font the WPF
   Fluent theme ships — painted with currentColor; the DS <Icon> SVGs only where no glyph is mapped. */
const GLYPH = {
  search:'E721', settings:'E713', pin:'E718', star:'E734', info:'E946', more:'E712', nav:'E700', code:'E943', glasses:'E736',
  filter:'E71C', clock:'E823', dismiss:'E711', dismiss12:'E711', open:'E8A7', task:'E9D5', link:'E71B', calendar:'E787',
  person:'E77B', lightbulb:'EA80', warning:'E7BA', sync:'E895', checkmark:'E73E', image:'EB9F', chevR:'E76C', chevD:'E70D',
  flag:'E7C1', arrowLeft:'E72B', play:'E768', circleHalf:'E793', doc:'E8A5', add:'E710', edit:'E70F', list:'E8FD',
  folder:'E8B7', folderOpen:'E838', tag:'E8EC', split:'E89F', panel:'E8A0', copy:'E8C8', history:'E81C',
  dockLeft:'E90C', dockRight:'E90D', sun:'E706', moon:'E708', min:'E921', max:'E922', restore:'E923', close:'E8BB',
};
function SIcon({ k, size = 16, style }) {
  const g = GLYPH[k];
  if (!g) return <span style={{ width: size, height: size, flex: 'none', ...style }} />;
  return <span aria-hidden="true" style={{ width: size, height: size, flex: 'none', display: 'inline-flex', alignItems: 'center', justifyContent: 'center', fontFamily: 'var(--font-icons)', fontSize: size, lineHeight: 1, fontStyle: 'normal', ...style }}>{String.fromCharCode(parseInt(g, 16))}</span>;
}
function Kbd({ children }) {
  return <span style={{ font: '12px/16px var(--font-ui)', color: 'var(--text-tertiary)', whiteSpace: 'nowrap' }}>{children}</span>;
}
function IconBtn({ k, label, onClick, active }) {
  const [h, setH] = React.useState(false);
  const Btn = SLATE_NS.Button; const g = <SIcon k={k} size={16} />;
  return <button title={label} aria-label={label} aria-pressed={active ? true : undefined} onClick={onClick} onMouseEnter={() => setH(true)} onMouseLeave={() => setH(false)}
    style={{ padding: 0, border: 'none', background: 'transparent', cursor: 'pointer', borderRadius: 4, display: 'flex', color: active ? 'var(--accent-text)' : 'var(--text-secondary)' }}>
    {Btn ? <Btn appearance="subtle" layout="icon only" size="medium" state={active ? 'selected' : h ? 'hover' : 'rest'} xOutline20={g} xFilled20={g} icon1={g} /> : g}
  </button>;
}

const SLATE_VAULT = [
  { name: 'blog', kind: 'folder', children: [{ name: 'Hello world', date: 'May 24' }, { name: 'Why Slate', date: 'May 25' }] },
  { name: 'daily', kind: 'folder', children: [{ name: '2026-05-28', date: 'May 28', tasks: '1/3' }, { name: '2026-05-27', date: 'May 27', tasks: '3/3' }, { name: '2026-05-26', date: 'May 26' }] },
  { name: 'learning', kind: 'folder', open: true, children: [
    { name: 'AI for accessibility — short reflection', date: 'May 20' }, { name: 'Calculus notes — derivatives', date: 'May 18' },
    { name: 'Linear algebra glossary', date: 'May 22' }, { name: 'Linear algebra lecture 2', date: 'May 23' },
    { name: 'Linear algebra lecture 3', date: '2 days ago', pinned: true }, { name: 'Linear algebra lecture 4', date: 'May 28', tasks: '0/2' },
    { name: 'MathML raw', date: 'May 12' }] },
  { name: 'people', kind: 'folder', children: [{ name: 'Cory Joseph', date: 'May 2' }] },
  { name: 'projects', kind: 'folder', children: [{ name: 'Slate', date: 'Jun 1', tasks: '4/9' }] },
  { name: 'recipes', kind: 'folder', children: [{ name: 'Apple pie', date: 'Apr 30' }, { name: 'Whipped cream', date: 'Apr 30' }] },
  { name: 'reference', kind: 'folder', children: [{ name: 'Code cookbook', date: 'May 3' }, { name: 'Markdown features', date: 'May 3' }, { name: 'Math sampler', date: 'May 4' }] },
];
const SLATE_TAGS = [{ t: 'lecture', n: 3 }, { t: 'math', n: 6 }, { t: 'projects', n: 2, kids: [{ t: 'reading', n: 1 }] }, { t: 'recipe', n: 2 }];
const SLATE_COMMANDS = [
  ['Switch Theme…', '', 'Workspace'],
  ['Toggle Reading Mode', 'Ctrl+Shift+E', 'Editor'], ['Quick Open…', 'Ctrl+O', 'File'], ['Search Vault…', 'Ctrl+Shift+F', 'Workspace'],
  ['New Note from Template…', 'Ctrl+Shift+N', 'File'], ['Find…', 'Ctrl+F', 'Editor'], ['Split Right', 'Ctrl+\\', 'Workspace'],
  ['Toggle Right Pane', 'Ctrl+Alt+I', 'Workspace'], ['Toggle Files Sidebar', '', 'Workspace'], ['Show Tasks Review', 'Ctrl+R', 'Workspace'], ['Citation Summary', 'Ctrl+Shift+J', 'Editor'],
  ['Bulk Rename Properties…', 'Ctrl+Shift+R', 'Editor'], ['Add Property…', '', 'Editor'], ['Open Vault…', 'Ctrl+Shift+O', 'File'],
  ['Show History Panel', '', 'Workspace'], ['Refresh Sync Diagnostics', '', 'Workspace'], ['Focus Filter', 'Ctrl+Alt+F', 'Files Sidebar'],
];
/* Right-pane views, in Slate's order (17). Icons are Segoe Fluent Icons code points. */
const SLATE_PANELS = [['outline', 'Outline', 'E8FD'], ['backlinks', 'Backlinks', 'E71B'], ['outgoing', 'Outgoing links', 'E8A7'], ['connections', 'Connections', 'F003'], ['graph', 'Graph inspector', 'E9E9'],
  ['embeds', 'Embeds', 'E8A1'], ['math', 'Math', 'E8EF'], ['code', 'Code', 'E943'], ['diagrams', 'Diagrams', 'E9D2'], ['tasks', 'Tasks', 'E9D5'], ['review', 'Tasks Review', 'E73A', 'Ctrl+R'],
  ['history', 'History', 'E81C'], ['citations', 'Citations', 'E82D'], ['bibliography', 'Bibliography', 'E8F1'], ['queries', 'Queries', 'E71C'], ['base', 'Base dock', 'E8A9'], ['sync', 'Sync', 'E895'],
].map(([id, label, icon, shortcut]) => ({ id, label, icon, shortcut }));
/* Files sidebar sort modes (SidebarSortMode). */
const SLATE_SORT = [{ value: 'name-asc', label: 'Name A to Z' }, { value: 'name-desc', label: 'Name Z to A' }, { value: 'mod-new', label: 'Modified (newest first)' }, { value: 'mod-old', label: 'Modified (oldest first)' }, { value: 'cre-new', label: 'Created (newest first)' }, { value: 'cre-old', label: 'Created (oldest first)' }];
/* Settings › Appearance › Theme, and the palette's Switch Theme… list. */
const SLATE_THEMES = [{ value: 'light', label: 'Light' }, { value: 'dark', label: 'Dark' }, { value: 'system', label: 'Use Windows setting' }];
Object.assign(window, { SLATE_NS, SIcon, Kbd, IconBtn, SLATE_VAULT, SLATE_TAGS, SLATE_COMMANDS, SLATE_PANELS, SLATE_SORT, SLATE_THEMES });
