import { __slateNorm } from './slateProps.jsx';
// figma node: 9322:463 .DEPRECATED_Carousel step (6 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "type=" + __venc(p.type) + '|' + "state=" + __venc(p.state);

export function DEPRECATEDCarouselStep(_p = {}) {
  _p = __slateNorm(_p, {"type":["inactive","active"],"state":["rest","hover","pressed"]});
  const props = { ..._p, type: _p.type ?? "inactive", state: _p.state ?? "rest", showFocus: _p.showFocus ?? false };
  const __body0 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "6px 4px 6px 4px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--horizontal-xs) * 1px)",
      paddingTop: "calc(var(--vertical-snudge) * 1px)",
      paddingRight: "calc(var(--horizontal-xs) * 1px)",
      paddingBottom: "calc(var(--vertical-snudge) * 1px)",
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
          backgroundColor: "var(--brand-foreground-2-rest-4)",
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
        height: 10,
        overflow: "hidden",
        borderRadius: 6,
        boxShadow: "0 0 0 2px var(--neutral-stroke-focus-2-rest-4)",
      }}>
        <svg width={18} height={10} viewBox="0 0 18 10" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 18,
          height: 10,
          overflow: "hidden",
          borderRadius: 4,
          color: "var(--neutral-stroke-focus-1-rest-4)",
        }}>
          <path d={"M 4 0 L 4 1 L 14 1 L 14 0 L 14 -1 L 4 -1 L 4 0 Z M 18 4 L 17 4 L 17 6 L 18 6 L 19 6 L 19 4 L 18 4 Z M 14 10 L 14 9 L 4 9 L 4 10 L 4 11 L 14 11 L 14 10 Z M 0 6 L 1 6 L 1 4 L 0 4 L -1 4 L -1 6 L 0 6 Z M 4 10 L 4 9 C 2.343 9 1 7.657 1 6 L 0 6 L -1 6 C -1 8.761 1.239 11 4 11 L 4 10 Z M 18 6 L 17 6 C 17 7.657 15.657 9 14 9 L 14 10 L 14 11 C 16.761 11 19 8.761 19 6 L 18 6 Z M 14 0 L 14 1 C 15.657 1 17 2.343 17 4 L 18 4 L 19 4 C 19 1.239 16.761 -1 14 -1 L 14 0 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
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
      padding: "6px 4px 6px 4px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--horizontal-xs) * 1px)",
      paddingTop: "calc(var(--vertical-snudge) * 1px)",
      paddingRight: "calc(var(--horizontal-xs) * 1px)",
      paddingBottom: "calc(var(--vertical-snudge) * 1px)",
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
          backgroundColor: "var(--brand-foreground-2-hover-4)",
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
      padding: "6px 4px 6px 4px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--horizontal-xs) * 1px)",
      paddingTop: "calc(var(--vertical-snudge) * 1px)",
      paddingRight: "calc(var(--horizontal-xs) * 1px)",
      paddingBottom: "calc(var(--vertical-snudge) * 1px)",
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
          backgroundColor: "var(--brand-foreground-2-pressed-4)",
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
      padding: "6px 4px 6px 4px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--horizontal-xs) * 1px)",
      paddingTop: "calc(var(--vertical-snudge) * 1px)",
      paddingRight: "calc(var(--horizontal-xs) * 1px)",
      paddingBottom: "calc(var(--vertical-snudge) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 8,
        opacity: 0.3,
        borderRadius: 9999,
        backgroundColor: "var(--brand-foreground-2-rest-4)",
        flexShrink: 0,
        alignSelf: "stretch",
      }} />
      {props.showFocus && (
      <div style={{
        position: "absolute",
        left: 3,
        top: 5,
        width: 10,
        height: 10,
        overflow: "hidden",
        borderRadius: 9999,
        boxShadow: "0 0 0 2px var(--neutral-stroke-focus-2-rest-4)",
      }}>
        <svg width={10} height={10} viewBox="0 0 10 10" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 10,
          height: 10,
          overflow: "hidden",
          borderRadius: 4,
          color: "var(--neutral-stroke-focus-1-rest-4)",
        }}>
          <path d={"M 4 0 L 4 1 L 6 1 L 6 0 L 6 -1 L 4 -1 L 4 0 Z M 10 4 L 9 4 L 9 6 L 10 6 L 11 6 L 11 4 L 10 4 Z M 6 10 L 6 9 L 4 9 L 4 10 L 4 11 L 6 11 L 6 10 Z M 0 6 L 1 6 L 1 4 L 0 4 L -1 4 L -1 6 L 0 6 Z M 4 10 L 4 9 C 2.343 9 1 7.657 1 6 L 0 6 L -1 6 C -1 8.761 1.239 11 4 11 L 4 10 Z M 10 6 L 9 6 C 9 7.657 7.657 9 6 9 L 6 10 L 6 11 C 8.761 11 11 8.761 11 6 L 10 6 Z M 6 0 L 6 1 C 7.657 1 9 2.343 9 4 L 10 4 L 11 4 C 11 1.239 8.761 -1 6 -1 L 6 0 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
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
      padding: "6px 4px 6px 4px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--horizontal-xs) * 1px)",
      paddingTop: "calc(var(--vertical-snudge) * 1px)",
      paddingRight: "calc(var(--horizontal-xs) * 1px)",
      paddingBottom: "calc(var(--vertical-snudge) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 8,
        borderRadius: 9999,
        backgroundColor: "var(--brand-foreground-2-hover-4)",
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
      padding: "6px 4px 6px 4px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--horizontal-xs) * 1px)",
      paddingTop: "calc(var(--vertical-snudge) * 1px)",
      paddingRight: "calc(var(--horizontal-xs) * 1px)",
      paddingBottom: "calc(var(--vertical-snudge) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 8,
        borderRadius: 9999,
        backgroundColor: "var(--brand-foreground-2-pressed-4)",
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
export default DEPRECATEDCarouselStep;
