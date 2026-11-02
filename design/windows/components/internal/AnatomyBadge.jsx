import { __slateNorm } from './slateProps.jsx';
// figma node: 9000:6033 Anatomy / Badge (3 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "style2=" + __venc(p.style2);

export function AnatomyBadge(_p = {}) {
  _p = __slateNorm(_p, {"style2":["standard","subtle","link"]});
  const props = { ..._p, style2: _p.style2 ?? "standard" };
  const __body0 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      overflow: "hidden",
      borderRadius: 100,
      backgroundColor: "var(--colors-neutral-grey-14)",
      display: "flex",
      flexDirection: "row",
      padding: "2px 16px 6px 16px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      position: "relative",
      ...props.style,
    }}>
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 600,
        fontSize: 16,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: 1.5,
        letterSpacing: "-0.010em",
        color: "var(--colors-neutral-grey-white)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.text1 ?? "Badge"}</span>
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      overflow: "hidden",
      borderRadius: 100,
      backgroundColor: "var(--colors-neutral-grey-88-2)",
      display: "flex",
      flexDirection: "row",
      padding: "2px 16px 6px 16px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      position: "relative",
      ...props.style,
    }}>
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 600,
        fontSize: 16,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: 1.5,
        letterSpacing: "-0.010em",
        color: "var(--neutral-foreground-2-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.text1 ?? "Badge"}</span>
    </div>
  );
  const __body2 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      overflow: "hidden",
      borderRadius: 100,
      backgroundColor: "var(--brand-background-2-rest-2)",
      display: "flex",
      flexDirection: "row",
      padding: "2px 12px 4px 12px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      position: "relative",
      ...props.style,
    }}>
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 14,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "24px",
        letterSpacing: "-0.025em",
        color: "var(--brand-foreground-link-rest-4)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.text1 ?? "Link Badge"}</span>
    </div>
  );
  const __impls = {
    // figma: Style=Standard
    "style2=standard": __body0,
    // figma: Style=Subtle
    "style2=subtle": __body1,
    // figma: Style=Link
    "style2=link": __body2,
  };
  return (__impls[__vkey(props)] ?? __body0)();
}
export default AnatomyBadge;
