import { __slateNorm } from '../internal/slateProps.jsx';
// figma node: 9380:3537 .Images Preview Step (2 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "state=" + __venc(p.state);

export function ImagesPreviewStep(_p = {}) {
  _p = __slateNorm(_p, {"state":["selected","rest"]});
  const props = { ..._p, state: _p.state ?? "selected" };
  const __body0 = () => (
    <div className={props.className} style={{
      width: 48,
      height: 48,
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 48,
        overflow: "hidden",
        borderRadius: 2,
        backgroundColor: "var(--brand-background-2-rest)",
        boxShadow: "inset 0 0 0 0.500px var(--brand-stroke-1-rest)",
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          height: 22,
          display: "flex",
          flexDirection: "column",
          gap: "calc(var(--spacing-vertical-xs-2) * 1px)",
          padding: "4px 0px 4px 0px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
          flexShrink: 0,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 700,
            fontSize: 10,
            textAlign: "center",
            whiteSpace: "nowrap",
            lineHeight: "14px",
            textBox: "trim-both cap alphabetic",
            letterSpacing: "0.030em",
            color: "var(--brand-foreground-2-rest)",
            flexShrink: 0,
          }}>48x48</span>
          <span style={{
            position: "relative",
            width: 32,
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 4,
            textAlign: "center",
            lineHeight: 1,
            color: "var(--brand-foreground-2-rest)",
            flexShrink: 0,
          }}>Use component swap to change</span>
        </div>
      </div>
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: 40,
      height: 40,
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 40,
        overflow: "hidden",
        borderRadius: 2,
        backgroundColor: "var(--brand-background-2-rest)",
        boxShadow: "inset 0 0 0 0.500px var(--brand-stroke-1-rest)",
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          height: 22,
          display: "flex",
          flexDirection: "column",
          gap: "calc(var(--spacing-vertical-xs-2) * 1px)",
          padding: "4px 0px 4px 0px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
          flexShrink: 0,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 700,
            fontSize: 10,
            textAlign: "center",
            whiteSpace: "nowrap",
            lineHeight: "14px",
            textBox: "trim-both cap alphabetic",
            letterSpacing: "0.030em",
            color: "var(--brand-foreground-2-rest)",
            flexShrink: 0,
          }}>40x40</span>
          <span style={{
            position: "relative",
            width: 32,
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 4,
            textAlign: "center",
            lineHeight: 1,
            color: "var(--brand-foreground-2-rest)",
            flexShrink: 0,
          }}>Use component swap to change</span>
        </div>
      </div>
    </div>
  );
  const __impls = {
    // figma: State=Selected
    "state=selected": __body0,
    // figma: State=Rest
    "state=rest": __body1,
  };
  return (__impls[__vkey(props)] ?? __body0)();
}
export default ImagesPreviewStep;
