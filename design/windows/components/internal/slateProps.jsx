// Slate prop normaliser. Lets every Fluent component accept short, consistent values
// alongside the Figma-derived ones: appearance= for style2=, size="small|medium|large"
// for "sm"/"medium (default)"/"lg", and booleans for "on"/"off" props. Old values keep working.
const __n = (v) => { const s = String(v).toLowerCase().replace(/\s*\([^)]*\)/g, '').replace(/[-_]/g, ' ').replace(/\s+/g, ' ').trim();
  return ({ sm: 'small', md: 'medium', lg: 'large', xl: 'extra large', xs: 'extra small', extralarge: 'extra large', extrasmall: 'extra small', 'x small': 'extra small', 'x large': 'extra large' })[s] || s; };
// Disabled variants: the Figma export lost some instance overrides, so a few disabled bodies
// (labelled Switch, Radio) drew enabled colours. These scoped overrides re-point enabled roles
// to the disabled ones for the whole subtree; bodies that already use disabled tokens are unaffected.
const __SLATE_DISABLED = {
  '--neutral-foreground-1-rest': 'var(--neutral-foreground-disabled-rest)', '--neutral-foreground-2-rest': 'var(--neutral-foreground-disabled-rest)',
  '--neutral-foreground-3-rest': 'var(--neutral-foreground-disabled-rest)', '--neutral-foreground-on-brand-rest': 'var(--neutral-foreground-disabled-rest)',
  '--brand-foreground-1-rest': 'var(--neutral-foreground-disabled-rest)', '--brand-foreground-compound-rest': 'var(--neutral-foreground-disabled-rest)',
  '--colors-neutral-white': 'var(--neutral-foreground-disabled-rest)',
  '--brand-stroke-1-rest': 'var(--neutral-stroke-disabled-rest)', '--brand-stroke-compound-rest': 'var(--neutral-stroke-disabled-rest)',
  '--neutral-stroke-1-rest': 'var(--neutral-stroke-disabled-rest)', '--neutral-stroke-accessible-rest': 'var(--neutral-stroke-disabled-rest)',
  '--slate-switch-track-border': 'var(--neutral-stroke-disabled-rest)',
  '--brand-background-1-rest': 'var(--neutral-background-disabled-rest)', '--brand-background-compound-rest': 'var(--neutral-background-disabled-rest)',
};
export function __slateNorm(p, enums) {
  if (!p || !enums) return p;
  let o = p;
  if (enums.style2 && p.appearance != null && p.style2 == null) o = { ...o, style2: p.appearance };
  for (const k in enums) {
    const v = o[k]; const allowed = enums[k];
    if (v == null || allowed.includes(v)) continue;
    let hit;
    if (typeof v === 'boolean' && allowed.includes('on') && allowed.includes('off')) hit = v ? 'on' : 'off';
    else if (typeof v === 'string') { const t = __n(v); hit = allowed.find((a) => __n(a) === t); }
    if (hit !== undefined) { if (o === p) o = { ...p }; o[k] = hit; }
  }
  if (o.state === 'disabled' || o.states === 'disabled' || o.disabled === 'on') o = { ...o, style: { ...__SLATE_DISABLED, ...(o.style || {}) } };
  return o;
}
