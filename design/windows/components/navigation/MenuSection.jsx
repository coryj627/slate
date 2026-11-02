import { __slateNorm } from '../internal/slateProps.jsx';
// figma node: 9121:6394 Menu section (3 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "divider=" + __venc(p.divider) + '|' + "state=" + __venc(p.state);

export function MenuSection(_p = {}) {
  _p = __slateNorm(_p, {"state":["rest","disabled"]});
  const props = { ..._p, divider: _p.divider ?? ((_p.state ?? "rest") === "disabled"), state: _p.state ?? "rest", primaryString: _p.primaryString ?? "Section header" };
  const __body0 = () => (
    <div className={props.className} style={{
      width: 240,
      minWidth: 120,
      minHeight: 32,
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-1-rest)",
      display: "flex",
      flexDirection: "row",
      padding: "8px 6px 8px 6px",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 700,
        fontSize: 12,
        lineHeight: "16px",
        color: "var(--neutral-foreground-2-rest)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>{props.primaryString}</span>
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: 240,
      minWidth: 120,
      minHeight: 32,
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-1-rest)",
      display: "flex",
      flexDirection: "row",
      padding: "8px 6px 8px 6px",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 700,
        fontSize: 12,
        lineHeight: "16px",
        color: "var(--neutral-foreground-disabled-rest)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>{props.primaryString}</span>
    </div>
  );
  const __body2 = () => (
    <div className={props.className} style={{
      width: 240,
      height: 5,
      minWidth: 120,
      maxWidth: null,
      maxHeight: 5,
      display: "flex",
      flexDirection: "row",
      padding: "8px 6px 8px 6px",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "absolute",
        left: -4,
        top: 2,
        width: 248,
        height: 1,
        backgroundColor: "var(--neutral-stroke-2-rest)",
      }} />
    </div>
  );
  const __impls = {
    // figma: Divider=False, State=Rest
    "divider=false|state=rest": __body0,
    // figma: Divider=False, State=Disabled
    "divider=false|state=disabled": __body1,
    // figma: Divider=True, State=Disabled
    "divider=true|state=disabled": __body2,
  };
  return (__impls[__vkey(props)] ?? __body0)();
}
export default MenuSection;
