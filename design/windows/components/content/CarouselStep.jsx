import { __slateNorm } from '../internal/slateProps.jsx';
// figma node: 9380:3589 .Carousel step (6 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "type=" + __venc(p.type) + '|' + "state=" + __venc(p.state);

export function CarouselStep(_p = {}) {
  _p = __slateNorm(_p, {"type":["inactive","active"],"state":["rest","hover","pressed"]});
  const props = { ..._p, type: _p.type ?? "inactive", state: _p.state ?? "rest", showFocus: _p.showFocus ?? false };
  const __body0 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "8px 4px 8px 4px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--horizontal-xs) * 1px)",
      paddingTop: "calc(var(--vertical-s) * 1px)",
      paddingRight: "calc(var(--horizontal-xs) * 1px)",
      paddingBottom: "calc(var(--vertical-s) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--horizontal-xs) * 1px)",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 16,
          borderRadius: 9999,
          backgroundColor: "var(--rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }} />
      </div>
      {props.showFocus && (
      <div style={{
        position: "absolute",
        left: 3,
        top: 5,
        width: 18,
        height: 14,
        overflow: "hidden",
        borderRadius: 6,
        boxShadow: "0 0 0 2px var(--neutral-stroke-focus-2-rest)",
      }}>
        <svg width={18} height={14} viewBox="0 0 18 14" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 18,
          height: 14,
          overflow: "hidden",
          borderRadius: 4,
          color: "var(--neutral-stroke-focus-1-rest)",
        }}>
          <path d={"M 4 0 L 4 1 L 14 1 L 14 0 L 14 -1 L 4 -1 L 4 0 Z M 18 4 L 17 4 L 17 10 L 18 10 L 19 10 L 19 4 L 18 4 Z M 14 14 L 14 13 L 4 13 L 4 14 L 4 15 L 14 15 L 14 14 Z M 0 10 L 1 10 L 1 4 L 0 4 L -1 4 L -1 10 L 0 10 Z M 4 14 L 4 13 C 2.343 13 1 11.657 1 10 L 0 10 L -1 10 C -1 12.761 1.239 15 4 15 L 4 14 Z M 18 10 L 17 10 C 17 11.657 15.657 13 14 13 L 14 14 L 14 15 C 16.761 15 19 12.761 19 10 L 18 10 Z M 14 0 L 14 1 C 15.657 1 17 2.343 17 4 L 18 4 L 19 4 C 19 1.239 16.761 -1 14 -1 L 14 0 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "8px 4px 8px 4px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--horizontal-xs) * 1px)",
      paddingTop: "calc(var(--vertical-s) * 1px)",
      paddingRight: "calc(var(--horizontal-xs) * 1px)",
      paddingBottom: "calc(var(--vertical-s) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--horizontal-xs) * 1px)",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 16,
          borderRadius: 9999,
          backgroundColor: "var(--hover)",
          flexShrink: 0,
          alignSelf: "stretch",
        }} />
      </div>
    </div>
  );
  const __body2 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "8px 4px 8px 4px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--horizontal-xs) * 1px)",
      paddingTop: "calc(var(--vertical-s) * 1px)",
      paddingRight: "calc(var(--horizontal-xs) * 1px)",
      paddingBottom: "calc(var(--vertical-s) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--horizontal-xs) * 1px)",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 16,
          borderRadius: 9999,
          backgroundColor: "var(--pressed)",
          flexShrink: 0,
          alignSelf: "stretch",
        }} />
      </div>
    </div>
  );
  const __body3 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "8px 8px 8px 8px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--horizontal-s) * 1px)",
      paddingTop: "calc(var(--vertical-s) * 1px)",
      paddingRight: "calc(var(--horizontal-s) * 1px)",
      paddingBottom: "calc(var(--vertical-s) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 8,
        opacity: 0.3,
        borderRadius: 9999,
        backgroundColor: "var(--rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }} />
      {props.showFocus && (
      <div style={{
        position: "absolute",
        left: 3,
        top: 5,
        width: 18,
        height: 14,
        overflow: "hidden",
        borderRadius: 9999,
        boxShadow: "0 0 0 2px var(--neutral-stroke-focus-2-rest)",
      }}>
        <svg width={18} height={14} viewBox="0 0 18 14" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 18,
          height: 14,
          overflow: "hidden",
          borderRadius: 4,
          color: "var(--neutral-stroke-focus-1-rest)",
        }}>
          <path d={"M 4 0 L 4 1 L 14 1 L 14 0 L 14 -1 L 4 -1 L 4 0 Z M 18 4 L 17 4 L 17 10 L 18 10 L 19 10 L 19 4 L 18 4 Z M 14 14 L 14 13 L 4 13 L 4 14 L 4 15 L 14 15 L 14 14 Z M 0 10 L 1 10 L 1 4 L 0 4 L -1 4 L -1 10 L 0 10 Z M 4 14 L 4 13 C 2.343 13 1 11.657 1 10 L 0 10 L -1 10 C -1 12.761 1.239 15 4 15 L 4 14 Z M 18 10 L 17 10 C 17 11.657 15.657 13 14 13 L 14 14 L 14 15 C 16.761 15 19 12.761 19 10 L 18 10 Z M 14 0 L 14 1 C 15.657 1 17 2.343 17 4 L 18 4 L 19 4 C 19 1.239 16.761 -1 14 -1 L 14 0 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
    </div>
  );
  const __body4 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "8px 8px 8px 8px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--horizontal-s) * 1px)",
      paddingTop: "calc(var(--vertical-s) * 1px)",
      paddingRight: "calc(var(--horizontal-s) * 1px)",
      paddingBottom: "calc(var(--vertical-s) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 8,
        borderRadius: 9999,
        backgroundColor: "var(--hover)",
        flexShrink: 0,
        alignSelf: "stretch",
      }} />
    </div>
  );
  const __body5 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "8px 8px 8px 8px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--horizontal-s) * 1px)",
      paddingTop: "calc(var(--vertical-s) * 1px)",
      paddingRight: "calc(var(--horizontal-s) * 1px)",
      paddingBottom: "calc(var(--vertical-s) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 8,
        borderRadius: 9999,
        backgroundColor: "var(--pressed)",
        flexShrink: 0,
        alignSelf: "stretch",
      }} />
    </div>
  );
  const __impls = {
    // figma: Type=Active, State=Rest
    "type=active|state=rest": __body0,
    // figma: Type=Active, State=Hover
    "type=active|state=hover": __body1,
    // figma: Type=Active, State=Pressed
    "type=active|state=pressed": __body2,
    // figma: Type=Inactive, State=Rest
    "type=inactive|state=rest": __body3,
    // figma: Type=Inactive, State=Hover
    "type=inactive|state=hover": __body4,
    // figma: Type=Inactive, State=Pressed
    "type=inactive|state=pressed": __body5,
  };
  return (__impls[__vkey(props)] ?? __body3)();
}
export default CarouselStep;
