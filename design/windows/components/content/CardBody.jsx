import { ArrowSwap2 } from '../internal/ArrowSwap2.jsx';

// figma node: 9230:4897 .Card body (2 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "customBody=" + __venc(p.customBody);

export function CardBody(_p = {}) {
  const props = { ..._p, bodyString: _p.bodyString ?? "Copilot is an AI tool designed to improve productivity by integrating with Microsoft applications, offering content generation and task automation features.", customBody: _p.customBody ?? false };
  const __body0 = () => (
    <div className={props.className} style={{
      width: 280,
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-m-2) * 1px)",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 12,
        lineHeight: "16px",
        color: "var(--neutral-foreground-2-rest)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>{props.bodyString}</span>
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: 280,
      height: 36,
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-m-2) * 1px)",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        backgroundColor: "var(--brand-background-2-rest)",
        display: "flex",
        flexDirection: "column",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          height: 44,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <ArrowSwap2
            style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--brand-foreground-2-rest)",
            }}
            size={"16"}
            theme={"regular"}
          />
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "column",
            padding: "0px 0px 2px 0px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingBottom: "calc(var(--spacing-vertical-xxs) * 1px)",
            flexShrink: 0,
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 400,
              fontSize: 10,
              textAlign: "center",
              lineHeight: "14px",
              color: "var(--brand-foreground-2-rest)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>SWAP AREA WITH YOUR COMPONENT</span>
          </div>
        </div>
      </div>
    </div>
  );
  const __impls = {
    // figma: Custom body=False
    "customBody=false": __body0,
    // figma: Custom body=True
    "customBody=true": __body1,
  };
  return (__impls[__vkey(props)] ?? __body0)();
}
export default CardBody;
