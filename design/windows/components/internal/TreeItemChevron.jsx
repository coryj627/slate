import { Chevron2 } from './Chevron2.jsx';

// figma node: 9064:2395 .Tree item chevron (2 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "expanded=" + __venc(p.expanded);

export function TreeItemChevron(_p = {}) {
  const props = { ..._p, expanded: _p.expanded ?? true };
  const __body0 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      padding: "10px 6px 10px 6px",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-mnudge-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-mnudge-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 12,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
          color: "var(--neutral-foreground-3-rest)",
        }}>{props.icon1 ?? <Chevron2 direction={"right"} size={"12"} theme={"regular"} />}</div>
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      padding: "10px 6px 10px 6px",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-mnudge-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-mnudge-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          width: 12,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
          color: "var(--neutral-foreground-3-rest)",
        }}>{props.icon1 ?? <Chevron2 direction={"down"} size={"12"} theme={"regular"} />}</div>
    </div>
  );
  const __impls = {
    // figma: Expanded=False
    "expanded=false": __body0,
    // figma: Expanded=True
    "expanded=true": __body1,
  };
  return (__impls[__vkey(props)] ?? __body1)();
}
export default TreeItemChevron;
