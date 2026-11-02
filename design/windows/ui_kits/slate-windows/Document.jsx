/* Workspace: tab strip (the Source/Reading switch lives in NoteHeader) + document surface (editor = Cascadia source, reader = Open Sans render). */
const SLATE_NOTE = {
  title: 'Linear algebra lecture 3',
  props: [['course', 'Linear algebra'], ['lecture', '3'], ['tags', ['math', 'lecture']], ['created', '2026-05-27'], ['source', '[@strang2016, ch. 6]']],
  source: [
    ['meta', '---'], ['meta', 'course: Linear algebra'], ['meta', 'lecture: 3'], ['meta', 'tags: [math, lecture]'], ['meta', 'created: 2026-05-27'], ['meta', 'source: "[@strang2016, ch. 6]"'], ['meta', '---'],
    ['h1', '# Linear algebra lecture 3'], ['p', ''],
    ['p', 'Lecture notes for the third session of the linear algebra course. The focus today is on eigenvalues and eigenvectors.'], ['p', ''],
    ['h2', '## Where we are in the course'], ['p', ''],
    ['mix', ['The full notes are at ', ['link', '[[Linear algebra lecture 2]]'], ' for context.']], ['p', ''],
    ['mix', [['link', '![[Linear algebra lecture 2]]']]], ['p', ''],
    ['h2', '## Eigenvalues and eigenvectors'], ['p', ''],
    ['mix', ['For a square matrix ', ['code', '$A$'], ', a nonzero vector ', ['code', '$v$'], ' is an **eigenvector** of ', ['code', '$A$'], ' if:']], ['p', ''],
    ['code', '$$A v = \\lambda v$$'], ['p', ''],
    ['mix', ['See ', ['link', '[[Linear algebra glossary#eigenvalue]]'], ' and ', ['tag', '#review'], '.']], ['p', ''],
    ['code', '```python'], ['code', 'import numpy as np'], ['code', 'np.linalg.eig([[4, -2], [1, 1]])'], ['code', '```'], ['p', ''],
    ['mix', ['- [ ] Read ', ['link', '[[Linear algebra supplementary]]']]],
  ],
};
/* Note tabs: the DS TabView (Windows UI kit tab view). */
function NoteTabs({ tabs, active, onPick, onClose }) {
  const T = SLATE_NS.TabView;
  return T ? <T style={{ background: 'var(--surface-window)', flex: 'none' }} label="Open notes" tabs={tabs.map((t) => ({ id: t, label: t, icon: 'E8A5' }))} value={active || undefined} onChange={onPick} onClose={onClose} /> : null;
}
const srcColor = { meta: 'var(--editor-metadata)', h1: 'var(--editor-heading)', h2: 'var(--editor-heading)', code: 'var(--editor-code)', p: 'var(--text-primary)', mix: 'var(--text-primary)' };
function EditorView({ note }) {
  return <div role="textbox" aria-multiline="true" aria-label="Note source" style={{ flex: 1, overflow: 'auto', background: 'var(--surface-editor)', font: 'var(--editor-size)/var(--editor-lh) var(--font-editor)', padding: '16px 0' }}>
    {note.source.map(([k, v], i) => <div key={i} style={{ display: 'flex', background: i === 13 ? 'var(--surface-hover)' : 'transparent' }}>
      <span style={{ width: 52, flex: 'none', textAlign: 'right', paddingRight: 16, color: 'var(--text-tertiary)', fontSize: 'var(--editor-gutter-size)', userSelect: 'none' }}>{i + 1}</span>
      <span style={{ whiteSpace: 'pre-wrap', color: srcColor[k], fontWeight: k[0] === 'h' ? 700 : 400, paddingRight: 24 }}>
        {k === 'mix' ? v.map((s, j) => typeof s === 'string' ? <span key={j}>{s}</span> : <span key={j} style={{ color: s[0] === 'code' ? 'var(--editor-code)' : 'var(--editor-wikilink)' }}>{s[1]}</span>) : v || ' '}
        {i === 13 && <span style={{ borderLeft: '2px solid var(--text-primary)', marginLeft: 1 }} />}
      </span></div>)}
  </div>;
}
function Wikilink({ children, broken, onOpen }) {
  return <a href="#" onClick={e => { e.preventDefault(); !broken && onOpen && onOpen(children); }} title={broken ? 'Note does not exist yet' : undefined}
    style={{ color: broken ? 'var(--status-error)' : 'var(--accent-text)', textDecoration: broken ? 'underline dashed' : 'underline', textUnderlineOffset: 3 }}>{children}</a>;
}
function PropertiesBlock({ props }) {
  const [open, setOpen] = React.useState(true);
  return <section aria-label="Properties" style={{ border: '1px solid var(--border-subtle)', borderRadius: 8, marginBottom: 24, background: 'var(--surface-card)' }}>
    <button aria-expanded={open} onClick={() => setOpen(!open)} style={{ width: '100%', display: 'flex', alignItems: 'center', gap: 8, height: 36, padding: '0 12px', border: 'none', background: 'transparent', font: '600 14px/20px var(--font-ui)', color: 'var(--text-primary)', cursor: 'pointer' }}>
      <SIcon k={open ? 'chevD' : 'chevR'} size={12} />Properties<span style={{ fontWeight: 400, color: 'var(--text-tertiary)', fontSize: 12 }}>{props.length}</span></button>
    {open && <div style={{ padding: '0 12px 10px', display: 'grid', gridTemplateColumns: '120px 1fr', rowGap: 4, font: '14px/20px var(--font-ui)' }}>
      {props.map(([k, v]) => <React.Fragment key={k}><span style={{ color: 'var(--text-secondary)', padding: '4px 0' }}>{k}</span>
        <span style={{ padding: '4px 0', display: 'flex', gap: 6, flexWrap: 'wrap' }}>{Array.isArray(v) ? v.map(t => <span key={t} style={{ padding: '0 8px', borderRadius: 4, background: 'var(--surface-raised)', font: '12px/20px var(--font-ui)' }}>#{t}</span>) : v}</span></React.Fragment>)}
    </div>}
  </section>;
}
function ReaderView({ note, onOpen }) {
  const P = { font: 'var(--reader-body-size)/var(--reader-body-lh) var(--font-reader)', margin: '0 0 16px', textWrap: 'pretty' };
  return <div style={{ flex: 1, overflow: 'auto', background: 'var(--surface-card)' }}>
    <article style={{ maxWidth: 'var(--reader-measure)', margin: '0 auto', padding: '32px 40px 64px', fontFamily: 'var(--font-reader)', color: 'var(--text-primary)' }}>
      <PropertiesBlock props={note.props} />
      <h1 style={{ font: '700 var(--reader-h1-size)/var(--reader-h1-lh) var(--font-reader)', margin: '0 0 16px' }}>{note.title}</h1>
      <p style={P}>Lecture notes for the third session of the linear algebra course. The focus today is on eigenvalues and eigenvectors — the central machinery for everything that follows.</p>
      <h2 style={{ font: '600 var(--reader-h2-size)/var(--reader-h2-lh) var(--font-reader)', margin: '28px 0 12px' }}>Where we are in the course</h2>
      <p style={P}>The full notes are at <Wikilink onOpen={onOpen}>Linear algebra lecture 2</Wikilink> for context.</p>
      <aside aria-label="Embedded note: Linear algebra lecture 2" style={{ border: '1px solid var(--border-subtle)', borderRadius: 8, margin: '0 0 20px', overflow: 'hidden' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 8, padding: '8px 12px', background: 'var(--surface-raised)', font: '600 13px/20px var(--font-ui)' }}><SIcon k="doc" />Linear algebra lecture 2<span style={{ flex: 1 }} /><IconBtn k="open" label="Open embedded note" onClick={() => onOpen('Linear algebra lecture 2')} /></div>
        <div style={{ padding: '12px 16px', font: '15px/24px var(--font-reader)', color: 'var(--text-secondary)' }}>Matrix multiplication, the determinant, and the conditions under which a matrix is invertible…</div>
      </aside>
      <h2 style={{ font: '600 var(--reader-h2-size)/var(--reader-h2-lh) var(--font-reader)', margin: '28px 0 12px' }}>Eigenvalues and eigenvectors</h2>
      <p style={P}>For a square matrix <i>A</i>, a nonzero vector <i>v</i> is an <strong>eigenvector</strong> of <i>A</i> if there exists a scalar <i>λ</i> such that:</p>
      <div role="math" aria-label="A v equals lambda v" style={{ textAlign: 'center', font: 'italic 20px/32px "Cambria Math", Cambria, serif', margin: '0 0 16px' }}>A v = λ v</div>
      <p style={P}>See <Wikilink onOpen={onOpen}>Linear algebra glossary#eigenvalue</Wikilink> and <span style={{ color: 'var(--accent-text)' }}>#review</span>.</p>
      <figure style={{ margin: '0 0 20px', border: '1px solid var(--border-subtle)', borderRadius: 8, overflow: 'hidden' }}>
        <figcaption style={{ display: 'flex', alignItems: 'center', gap: 8, padding: '4px 4px 4px 12px', background: 'var(--surface-raised)', font: '12px/16px var(--font-ui)', color: 'var(--text-secondary)' }}><SIcon k="code" size={16} />python<span style={{ flex: 1 }} /><IconBtn k="copy" label="Copy code" /></figcaption>
        <pre style={{ margin: 0, padding: '12px 16px', font: '14px/22px var(--font-editor)', background: 'var(--surface-card)' }}><span style={{ color: 'var(--code-keyword)' }}>import</span> numpy <span style={{ color: 'var(--code-keyword)' }}>as</span> np{'\n'}np.linalg.<span style={{ color: 'var(--code-function)' }}>eig</span>([[<span style={{ color: 'var(--code-number)' }}>4</span>, <span style={{ color: 'var(--code-number)' }}>-2</span>], [<span style={{ color: 'var(--code-number)' }}>1</span>, <span style={{ color: 'var(--code-number)' }}>1</span>]])</pre>
      </figure>
      <label style={{ display: 'flex', gap: 10, alignItems: 'center', ...P }}><input type="checkbox" style={{ width: 18, height: 18, accentColor: 'var(--accent)' }} />Read <Wikilink broken>Linear algebra supplementary</Wikilink></label>
    </article>
  </div>;
}
Object.assign(window, { SLATE_NOTE, NoteTabs, EditorView, ReaderView });
