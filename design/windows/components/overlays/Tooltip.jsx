import { __slateNorm } from '../internal/slateProps.jsx';
// figma node: 9014:2661 Tooltip (3 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "style2=" + __venc(p.style2);

export function Tooltip(_p = {}) {
  _p = __slateNorm(_p, {"style2":["default","brand","inverted"]});
  const props = { ..._p, style2: _p.style2 ?? "default", label: _p.label ?? "Tooltip will wrap text to next line when max width is reached at 240px." };
  const __body0 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: 240,
      maxHeight: null,
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-1-rest)",
      borderTop: "1px solid rgba(255,255,255,0)",
      borderRight: "1px solid rgba(255,255,255,0)",
      borderBottom: "1px solid rgba(255,255,255,0)",
      borderLeft: "1px solid rgba(255,255,255,0)",
      boxShadow: "var(--shadow-8)",
      display: "flex",
      flexDirection: "column",
      padding: "6px 12px 6px 12px",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        maxWidth: 216,
        maxHeight: null,
        display: "flex",
        flexDirection: "row",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 12,
          lineHeight: "16px",
          color: "var(--neutral-foreground-1-rest)",
          flexGrow: 1,
        }}>{props.label}</span>
      </div>
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: 240,
      maxHeight: null,
      borderRadius: 4,
      backgroundColor: "var(--brand-background-1-rest)",
      borderTop: "1px solid var(--brand-stroke-1-rest)",
      borderRight: "1px solid var(--brand-stroke-1-rest)",
      borderBottom: "1px solid var(--brand-stroke-1-rest)",
      borderLeft: "1px solid var(--brand-stroke-1-rest)",
      boxShadow: "var(--shadow-8)",
      display: "flex",
      flexDirection: "column",
      padding: "6px 12px 6px 12px",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        maxWidth: 216,
        maxHeight: null,
        display: "flex",
        flexDirection: "row",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 12,
          lineHeight: "16px",
          color: "var(--neutral-foreground-on-brand-rest)",
          flexGrow: 1,
        }}>{props.label}</span>
      </div>
    </div>
  );
  const __body2 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      maxWidth: 240,
      maxHeight: null,
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-inverted-rest)",
      borderTop: "1px solid rgba(255,255,255,0)",
      borderRight: "1px solid rgba(255,255,255,0)",
      borderBottom: "1px solid rgba(255,255,255,0)",
      borderLeft: "1px solid rgba(255,255,255,0)",
      boxShadow: "var(--shadow-8)",
      display: "flex",
      flexDirection: "column",
      padding: "6px 12px 6px 12px",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        maxWidth: 216,
        maxHeight: null,
        display: "flex",
        flexDirection: "row",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 12,
          lineHeight: "16px",
          color: "var(--neutral-foreground-inverted-1-rest)",
          flexGrow: 1,
        }}>{props.label}</span>
      </div>
    </div>
  );
  const __impls = {
    // figma: Style=Default
    "style2=default": __body0,
    // figma: Style=Brand
    "style2=brand": __body1,
    // figma: Style=Inverted
    "style2=inverted": __body2,
  };
  return (__impls[__vkey(props)] ?? __body0)();
}
export default Tooltip;
