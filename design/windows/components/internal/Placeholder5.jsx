import { __slateNorm } from './slateProps.jsx';

// figma node: 9158:623 Placeholder (2 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "slot=" + __venc(p.slot);

export function Placeholder5(_p = {}) {
  _p = __slateNorm(_p, {"slot":["1","2"]});
  const props = { ..._p, slot: _p.slot ?? "1", slotBefore: _p.slotBefore ?? true };
  const __body0 = () => (
    <div className={props.className} style={{
      width: 250,
      height: 44,
      backgroundColor: "var(--brand-background-2-rest)",
      position: "relative",
      ...props.style,
    }}>
      <span style={{
        position: "absolute",
        left: 42,
        top: 15,
        width: 164,
        height: 14,
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 10,
        textAlign: "center",
        whiteSpace: "nowrap",
        lineHeight: "14px",
        color: "var(--brand-foreground-1-rest)",
      }}>{props.text1 ?? "SWAP WITH CONTENT COMPONENT"}</span>
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: 16,
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      {props.slotBefore && (
      <div style={{
          position: "relative",
          width: 56,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.image ?? <Placeholder5 text1={" "} slot={"1"} />}</div>
      )}
      <div style={{
          position: "relative",
          width: 240,
          flexShrink: 0,
          alignSelf: "stretch",
          height: "auto",
        }}>{props.instance ?? <Placeholder5 slot={"1"} />}</div>
    </div>
  );
  const __impls = {
    // figma: Slot=1
    "slot=1": __body0,
    // figma: Slot=2
    "slot=2": __body1,
  };
  return (__impls[__vkey(props)] ?? __body0)();
}
export default Placeholder5;
