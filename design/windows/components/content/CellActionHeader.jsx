import { __slateNorm } from '../internal/slateProps.jsx';
// figma node: 9249:10273 .Cell action header (2 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "size=" + __venc(p.size);

export function CellActionHeader(_p = {}) {
  _p = __slateNorm(_p, {"size":["small/smaller","medium/large"]});
  const props = { ..._p, size: _p.size ?? "small/smaller", focus: _p.focus ?? false };
  const __body0 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 33,
      overflow: "hidden",
      backgroundColor: "rgba(255,255,255,0)",
      borderTop: "1px solid var(--neutral-stroke-2-rest)",
      borderRight: "1px solid var(--neutral-stroke-2-rest)",
      borderBottom: "1px solid var(--neutral-stroke-2-rest)",
      borderLeft: "1px solid var(--neutral-stroke-2-rest)",
      display: "flex",
      flexDirection: "column",
      gap: 10,
      padding: "4px 0px 5px 0px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 24,
          height: 24,
          borderRadius: 4,
          backgroundColor: "rgba(255,255,255,0)",
          display: "flex",
          flexDirection: "row",
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xxs) * 1px)",
          flexShrink: 0,
        }} />
        <div style={{
          position: "relative",
          width: 24,
          height: 24,
          borderRadius: 4,
          backgroundColor: "rgba(255,255,255,0)",
          display: "flex",
          flexDirection: "row",
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xxs) * 1px)",
          flexShrink: 0,
        }} />
        <div style={{
          position: "relative",
          width: 24,
          height: 24,
          borderRadius: 4,
          backgroundColor: "rgba(255,255,255,0)",
          display: "flex",
          flexDirection: "row",
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-xxs) * 1px)",
          flexShrink: 0,
        }} />
      </div>
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 33,
      overflow: "hidden",
      backgroundColor: "rgba(255,255,255,0)",
      borderTop: "1px solid var(--neutral-stroke-2-rest)",
      borderRight: "1px solid var(--neutral-stroke-2-rest)",
      borderBottom: "1px solid var(--neutral-stroke-2-rest)",
      borderLeft: "1px solid var(--neutral-stroke-2-rest)",
      display: "flex",
      flexDirection: "column",
      gap: 10,
      padding: "4px 0px 5px 0px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          borderRadius: 4,
          backgroundColor: "rgba(255,255,255,0)",
          display: "flex",
          flexDirection: "row",
          padding: "6px 6px 6px 6px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexShrink: 0,
        }}>
          <div style={{
            position: "relative",
            width: 20,
            height: 20,
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            flexShrink: 0,
          }} />
        </div>
        <div style={{
          position: "relative",
          borderRadius: 4,
          backgroundColor: "rgba(255,255,255,0)",
          display: "flex",
          flexDirection: "row",
          padding: "6px 6px 6px 6px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexShrink: 0,
        }}>
          <div style={{
            position: "relative",
            width: 20,
            height: 20,
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            flexShrink: 0,
          }} />
        </div>
        <div style={{
          position: "relative",
          borderRadius: 4,
          backgroundColor: "rgba(255,255,255,0)",
          display: "flex",
          flexDirection: "row",
          padding: "6px 6px 6px 6px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexShrink: 0,
        }}>
          <div style={{
            position: "relative",
            width: 20,
            height: 20,
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            flexShrink: 0,
          }} />
        </div>
      </div>
    </div>
  );
  const __impls = {
    // figma: Size=Small/Smaller
    "size=small/smaller": __body0,
    // figma: Size=Medium/large
    "size=medium/large": __body1,
  };
  return (__impls[__vkey(props)] ?? __body0)();
}
export default CellActionHeader;
