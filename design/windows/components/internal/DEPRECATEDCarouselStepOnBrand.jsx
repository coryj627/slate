import { __slateNorm } from './slateProps.jsx';
// figma node: 9322:529 .DEPRECATED_Carousel step - on brand (6 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "type=" + __venc(p.type) + '|' + "state=" + __venc(p.state);

export function DEPRECATEDCarouselStepOnBrand(_p = {}) {
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
          height: 8,
          borderRadius: 9999,
          backgroundColor: "var(--neutral-foreground-on-brand-rest-3)",
          flexShrink: 0,
        }} />
      </div>
      {props.showFocus && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 24,
        height: 20,
        overflow: "hidden",
        borderRadius: 4,
        boxShadow: "0 0 0 2px var(--neutral-stroke-focus-2-rest-4)",
      }}>
        <svg width={24} height={20} viewBox="0 0 24 20" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 20,
          overflow: "hidden",
          borderRadius: 4,
          color: "var(--neutral-stroke-focus-1-rest-4)",
        }}>
          <path d={"M 4 0 L 4 1 L 20 1 L 20 0 L 20 -1 L 4 -1 L 4 0 Z M 24 4 L 23 4 L 23 16 L 24 16 L 25 16 L 25 4 L 24 4 Z M 20 20 L 20 19 L 4 19 L 4 20 L 4 21 L 20 21 L 20 20 Z M 0 16 L 1 16 L 1 4 L 0 4 L -1 4 L -1 16 L 0 16 Z M 4 20 L 4 19 C 2.343 19 1 17.657 1 16 L 0 16 L -1 16 C -1 18.761 1.239 21 4 21 L 4 20 Z M 24 16 L 23 16 C 23 17.657 21.657 19 20 19 L 20 20 L 20 21 C 22.761 21 25 18.761 25 16 L 24 16 Z M 20 0 L 20 1 C 21.657 1 23 2.343 23 4 L 24 4 L 25 4 C 25 1.239 22.761 -1 20 -1 L 20 0 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
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
          height: 8,
          borderRadius: 9999,
          backgroundColor: "var(--neutral-foreground-on-brand-rest-3)",
          flexShrink: 0,
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
        width: 8,
        height: 8,
        opacity: 0.3,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-foreground-on-brand-rest-3)",
        flexShrink: 0,
      }} />
      {props.showFocus && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 16,
        height: 20,
        overflow: "hidden",
        borderRadius: 4,
        boxShadow: "0 0 0 2px var(--neutral-stroke-focus-2-rest-4)",
      }}>
        <svg width={16} height={20} viewBox="0 0 16 20" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 16,
          height: 20,
          overflow: "hidden",
          borderRadius: 4,
          color: "var(--neutral-stroke-focus-1-rest-4)",
        }}>
          <path d={"M 4 0 L 4 1 L 12 1 L 12 0 L 12 -1 L 4 -1 L 4 0 Z M 16 4 L 15 4 L 15 16 L 16 16 L 17 16 L 17 4 L 16 4 Z M 12 20 L 12 19 L 4 19 L 4 20 L 4 21 L 12 21 L 12 20 Z M 0 16 L 1 16 L 1 4 L 0 4 L -1 4 L -1 16 L 0 16 Z M 4 20 L 4 19 C 2.343 19 1 17.657 1 16 L 0 16 L -1 16 C -1 18.761 1.239 21 4 21 L 4 20 Z M 16 16 L 15 16 C 15 17.657 13.657 19 12 19 L 12 20 L 12 21 C 14.761 21 17 18.761 17 16 L 16 16 Z M 12 0 L 12 1 C 13.657 1 15 2.343 15 4 L 16 4 L 17 4 C 17 1.239 14.761 -1 12 -1 L 12 0 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
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
        height: 8,
        borderRadius: 9999,
        backgroundColor: "var(--neutral-foreground-on-brand-rest-3)",
        flexShrink: 0,
      }} />
    </div>
  );
  const __impls = {
    // figma: Type=Active, State=Rest
    "type=active|state=rest": __body0,
    // figma: Type=Active, State=Hover
    "type=active|state=hover": __body1,
    // figma: Type=Active, State=Pressed
    "type=active|state=pressed": __body1,
    // figma: Type=Inactive, State=Rest
    "type=inactive|state=rest": __body2,
    // figma: Type=Inactive, State=Hover
    "type=inactive|state=hover": __body3,
    // figma: Type=Inactive, State=Pressed
    "type=inactive|state=pressed": __body3,
  };
  return (__impls[__vkey(props)] ?? __body2)();
}
export default DEPRECATEDCarouselStepOnBrand;
