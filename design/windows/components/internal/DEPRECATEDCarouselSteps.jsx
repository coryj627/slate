import { __slateNorm } from './slateProps.jsx';
// figma node: 9322:484 .DEPRECATED_Carousel steps (5 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "steps=" + __venc(p.steps);

export function DEPRECATEDCarouselSteps(_p = {}) {
  _p = __slateNorm(_p, {"steps":["1 (default)","2","3","4","5"]});
  const props = { ..._p, steps: _p.steps ?? "1 (default)", showFocus: _p.showFocus ?? false };
  const __body0 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
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
        flexShrink: 0,
        alignSelf: "stretch",
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
      </div>
      <div style={{
        position: "relative",
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
        flexShrink: 0,
        alignSelf: "stretch",
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
      </div>
      <div style={{
        position: "relative",
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
        flexShrink: 0,
        alignSelf: "stretch",
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
      </div>
      <div style={{
        position: "relative",
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
        flexShrink: 0,
        alignSelf: "stretch",
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
      </div>
      <div style={{
        position: "relative",
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
        flexShrink: 0,
        alignSelf: "stretch",
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
      </div>
      {props.showFocus && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 88,
        height: 20,
        overflow: "hidden",
        borderRadius: 4,
        boxShadow: "0 0 0 2px var(--neutral-stroke-focus-2-rest-4)",
      }}>
        <svg width={88} height={20} viewBox="0 0 88 20" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 88,
          height: 20,
          overflow: "hidden",
          borderRadius: 4,
          color: "var(--neutral-stroke-focus-1-rest-4)",
        }}>
          <path d={"M 4 0 L 4 1 L 84 1 L 84 0 L 84 -1 L 4 -1 L 4 0 Z M 88 4 L 87 4 L 87 16 L 88 16 L 89 16 L 89 4 L 88 4 Z M 84 20 L 84 19 L 4 19 L 4 20 L 4 21 L 84 21 L 84 20 Z M 0 16 L 1 16 L 1 4 L 0 4 L -1 4 L -1 16 L 0 16 Z M 4 20 L 4 19 C 2.343 19 1 17.657 1 16 L 0 16 L -1 16 C -1 18.761 1.239 21 4 21 L 4 20 Z M 88 16 L 87 16 C 87 17.657 85.657 19 84 19 L 84 20 L 84 21 C 86.761 21 89 18.761 89 16 L 88 16 Z M 84 0 L 84 1 C 85.657 1 87 2.343 87 4 L 88 4 L 89 4 C 89 1.239 86.761 -1 84 -1 L 84 0 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
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
        flexShrink: 0,
        alignSelf: "stretch",
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
      </div>
      <div style={{
        position: "relative",
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
        flexShrink: 0,
        alignSelf: "stretch",
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
      </div>
      <div style={{
        position: "relative",
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
        flexShrink: 0,
        alignSelf: "stretch",
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
      </div>
      <div style={{
        position: "relative",
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
        flexShrink: 0,
        alignSelf: "stretch",
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
      </div>
      <div style={{
        position: "relative",
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
        flexShrink: 0,
        alignSelf: "stretch",
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
      </div>
      {props.showFocus && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 88,
        height: 20,
        overflow: "hidden",
        borderRadius: 4,
        boxShadow: "0 0 0 2px var(--neutral-stroke-focus-2-rest-4)",
      }}>
        <svg width={88} height={20} viewBox="0 0 88 20" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 88,
          height: 20,
          overflow: "hidden",
          borderRadius: 4,
          color: "var(--neutral-stroke-focus-1-rest-4)",
        }}>
          <path d={"M 4 0 L 4 1 L 84 1 L 84 0 L 84 -1 L 4 -1 L 4 0 Z M 88 4 L 87 4 L 87 16 L 88 16 L 89 16 L 89 4 L 88 4 Z M 84 20 L 84 19 L 4 19 L 4 20 L 4 21 L 84 21 L 84 20 Z M 0 16 L 1 16 L 1 4 L 0 4 L -1 4 L -1 16 L 0 16 Z M 4 20 L 4 19 C 2.343 19 1 17.657 1 16 L 0 16 L -1 16 C -1 18.761 1.239 21 4 21 L 4 20 Z M 88 16 L 87 16 C 87 17.657 85.657 19 84 19 L 84 20 L 84 21 C 86.761 21 89 18.761 89 16 L 88 16 Z M 84 0 L 84 1 C 85.657 1 87 2.343 87 4 L 88 4 L 89 4 C 89 1.239 86.761 -1 84 -1 L 84 0 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
    </div>
  );
  const __body2 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
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
        flexShrink: 0,
        alignSelf: "stretch",
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
      </div>
      <div style={{
        position: "relative",
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
        flexShrink: 0,
        alignSelf: "stretch",
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
      </div>
      <div style={{
        position: "relative",
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
        flexShrink: 0,
        alignSelf: "stretch",
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
      </div>
      <div style={{
        position: "relative",
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
        flexShrink: 0,
        alignSelf: "stretch",
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
      </div>
      <div style={{
        position: "relative",
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
        flexShrink: 0,
        alignSelf: "stretch",
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
      </div>
      {props.showFocus && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 88,
        height: 20,
        overflow: "hidden",
        borderRadius: 4,
        boxShadow: "0 0 0 2px var(--neutral-stroke-focus-2-rest-4)",
      }}>
        <svg width={88} height={20} viewBox="0 0 88 20" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 88,
          height: 20,
          overflow: "hidden",
          borderRadius: 4,
          color: "var(--neutral-stroke-focus-1-rest-4)",
        }}>
          <path d={"M 4 0 L 4 1 L 84 1 L 84 0 L 84 -1 L 4 -1 L 4 0 Z M 88 4 L 87 4 L 87 16 L 88 16 L 89 16 L 89 4 L 88 4 Z M 84 20 L 84 19 L 4 19 L 4 20 L 4 21 L 84 21 L 84 20 Z M 0 16 L 1 16 L 1 4 L 0 4 L -1 4 L -1 16 L 0 16 Z M 4 20 L 4 19 C 2.343 19 1 17.657 1 16 L 0 16 L -1 16 C -1 18.761 1.239 21 4 21 L 4 20 Z M 88 16 L 87 16 C 87 17.657 85.657 19 84 19 L 84 20 L 84 21 C 86.761 21 89 18.761 89 16 L 88 16 Z M 84 0 L 84 1 C 85.657 1 87 2.343 87 4 L 88 4 L 89 4 C 89 1.239 86.761 -1 84 -1 L 84 0 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
    </div>
  );
  const __body3 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
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
        flexShrink: 0,
        alignSelf: "stretch",
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
      </div>
      <div style={{
        position: "relative",
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
        flexShrink: 0,
        alignSelf: "stretch",
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
      </div>
      <div style={{
        position: "relative",
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
        flexShrink: 0,
        alignSelf: "stretch",
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
      </div>
      <div style={{
        position: "relative",
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
        flexShrink: 0,
        alignSelf: "stretch",
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
      </div>
      <div style={{
        position: "relative",
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
        flexShrink: 0,
        alignSelf: "stretch",
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
      </div>
      {props.showFocus && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 88,
        height: 20,
        overflow: "hidden",
        borderRadius: 4,
        boxShadow: "0 0 0 2px var(--neutral-stroke-focus-2-rest-4)",
      }}>
        <svg width={88} height={20} viewBox="0 0 88 20" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 88,
          height: 20,
          overflow: "hidden",
          borderRadius: 4,
          color: "var(--neutral-stroke-focus-1-rest-4)",
        }}>
          <path d={"M 4 0 L 4 1 L 84 1 L 84 0 L 84 -1 L 4 -1 L 4 0 Z M 88 4 L 87 4 L 87 16 L 88 16 L 89 16 L 89 4 L 88 4 Z M 84 20 L 84 19 L 4 19 L 4 20 L 4 21 L 84 21 L 84 20 Z M 0 16 L 1 16 L 1 4 L 0 4 L -1 4 L -1 16 L 0 16 Z M 4 20 L 4 19 C 2.343 19 1 17.657 1 16 L 0 16 L -1 16 C -1 18.761 1.239 21 4 21 L 4 20 Z M 88 16 L 87 16 C 87 17.657 85.657 19 84 19 L 84 20 L 84 21 C 86.761 21 89 18.761 89 16 L 88 16 Z M 84 0 L 84 1 C 85.657 1 87 2.343 87 4 L 88 4 L 89 4 C 89 1.239 86.761 -1 84 -1 L 84 0 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
    </div>
  );
  const __body4 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
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
        flexShrink: 0,
        alignSelf: "stretch",
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
      </div>
      <div style={{
        position: "relative",
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
        flexShrink: 0,
        alignSelf: "stretch",
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
      </div>
      <div style={{
        position: "relative",
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
        flexShrink: 0,
        alignSelf: "stretch",
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
      </div>
      <div style={{
        position: "relative",
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
        flexShrink: 0,
        alignSelf: "stretch",
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
      </div>
      <div style={{
        position: "relative",
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
        flexShrink: 0,
        alignSelf: "stretch",
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
      </div>
      {props.showFocus && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 88,
        height: 20,
        overflow: "hidden",
        borderRadius: 4,
        boxShadow: "0 0 0 2px var(--neutral-stroke-focus-2-rest-4)",
      }}>
        <svg width={88} height={20} viewBox="0 0 88 20" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 88,
          height: 20,
          overflow: "hidden",
          borderRadius: 4,
          color: "var(--neutral-stroke-focus-1-rest-4)",
        }}>
          <path d={"M 4 0 L 4 1 L 84 1 L 84 0 L 84 -1 L 4 -1 L 4 0 Z M 88 4 L 87 4 L 87 16 L 88 16 L 89 16 L 89 4 L 88 4 Z M 84 20 L 84 19 L 4 19 L 4 20 L 4 21 L 84 21 L 84 20 Z M 0 16 L 1 16 L 1 4 L 0 4 L -1 4 L -1 16 L 0 16 Z M 4 20 L 4 19 C 2.343 19 1 17.657 1 16 L 0 16 L -1 16 C -1 18.761 1.239 21 4 21 L 4 20 Z M 88 16 L 87 16 C 87 17.657 85.657 19 84 19 L 84 20 L 84 21 C 86.761 21 89 18.761 89 16 L 88 16 Z M 84 0 L 84 1 C 85.657 1 87 2.343 87 4 L 88 4 L 89 4 C 89 1.239 86.761 -1 84 -1 L 84 0 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
    </div>
  );
  const __impls = {
    // figma: Steps=1 (default)
    "steps=1 (default)": __body0,
    // figma: Steps=2
    "steps=2": __body1,
    // figma: Steps=3
    "steps=3": __body2,
    // figma: Steps=4
    "steps=4": __body3,
    // figma: Steps=5
    "steps=5": __body4,
  };
  return (__impls[__vkey(props)] ?? __body0)();
}
export default DEPRECATEDCarouselSteps;
