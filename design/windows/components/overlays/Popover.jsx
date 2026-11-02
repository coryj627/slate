import { __slateNorm } from '../internal/slateProps.jsx';
import { ContentPlaceholder2 } from '../internal/ContentPlaceholder2.jsx';

// figma node: 9087:453 Popover (3 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "style2=" + __venc(p.style2);

export function Popover(_p = {}) {
  _p = __slateNorm(_p, {"style2":["default","inverted","brand"]});
  const props = { ..._p, style2: _p.style2 ?? "default" };
  const __body0 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-1-rest)",
      borderTop: "1px solid rgba(255,255,255,0)",
      borderRight: "1px solid rgba(255,255,255,0)",
      borderBottom: "1px solid rgba(255,255,255,0)",
      borderLeft: "1px solid rgba(255,255,255,0)",
      boxShadow: "var(--shadow-16)",
      display: "flex",
      flexDirection: "column",
      padding: "16px 16px 16px 16px",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--popover-padding-horizontal) * 1px)",
      paddingTop: "calc(var(--popover-padding-vertical) * 1px)",
      paddingRight: "calc(var(--popover-padding-horizontal) * 1px)",
      paddingBottom: "calc(var(--popover-padding-vertical) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          height: 44,
          flexShrink: 0,
          alignSelf: "stretch",
          width: "auto",
        }}>{props.contentSwap ?? <ContentPlaceholder2 />}</div>
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--brand-background-1-rest)",
      borderTop: "1px solid var(--brand-stroke-1-rest)",
      borderRight: "1px solid var(--brand-stroke-1-rest)",
      borderBottom: "1px solid var(--brand-stroke-1-rest)",
      borderLeft: "1px solid var(--brand-stroke-1-rest)",
      boxShadow: "var(--shadow-16)",
      display: "flex",
      flexDirection: "column",
      padding: "16px 16px 16px 16px",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--popover-padding-horizontal) * 1px)",
      paddingTop: "calc(var(--popover-padding-vertical) * 1px)",
      paddingRight: "calc(var(--popover-padding-horizontal) * 1px)",
      paddingBottom: "calc(var(--popover-padding-vertical) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          height: 44,
          flexShrink: 0,
          alignSelf: "stretch",
          width: "auto",
        }}>{props.contentSwap ?? <ContentPlaceholder2 />}</div>
    </div>
  );
  const __body2 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-inverted-rest)",
      borderTop: "1px solid rgba(255,255,255,0)",
      borderRight: "1px solid rgba(255,255,255,0)",
      borderBottom: "1px solid rgba(255,255,255,0)",
      borderLeft: "1px solid rgba(255,255,255,0)",
      boxShadow: "var(--shadow-16)",
      display: "flex",
      flexDirection: "column",
      padding: "16px 16px 16px 16px",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--popover-padding-horizontal) * 1px)",
      paddingTop: "calc(var(--popover-padding-vertical) * 1px)",
      paddingRight: "calc(var(--popover-padding-horizontal) * 1px)",
      paddingBottom: "calc(var(--popover-padding-vertical) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
          position: "relative",
          height: 44,
          flexShrink: 0,
          alignSelf: "stretch",
          width: "auto",
        }}>{props.contentSwap ?? <ContentPlaceholder2 />}</div>
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
export default Popover;
