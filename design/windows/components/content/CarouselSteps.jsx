import { __slateNorm } from '../internal/slateProps.jsx';
// figma node: 9380:3548 .Carousel steps (5 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "steps=" + __venc(p.steps);

export function CarouselSteps(_p = {}) {
  _p = __slateNorm(_p, {"steps":["1 (default)","2","3","4","5"]});
  const props = { ..._p, steps: _p.steps ?? "1 (default)", showFocus: _p.showFocus ?? false };
  const __body0 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 8,
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
        padding: "8px 4px 8px 4px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xs) * 1px)",
        paddingTop: "calc(var(--vertical-s) * 1px)",
        paddingRight: "calc(var(--horizontal-xs) * 1px)",
        paddingBottom: "calc(var(--vertical-s) * 1px)",
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
            backgroundColor: "var(--rest)",
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
        padding: "8px 8px 8px 8px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-s) * 1px)",
        paddingTop: "calc(var(--vertical-s) * 1px)",
        paddingRight: "calc(var(--horizontal-s) * 1px)",
        paddingBottom: "calc(var(--vertical-s) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
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
      </div>
      <div style={{
        position: "relative",
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
        flexShrink: 0,
        alignSelf: "stretch",
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
      </div>
      <div style={{
        position: "relative",
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
        flexShrink: 0,
        alignSelf: "stretch",
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
      </div>
      <div style={{
        position: "relative",
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
        flexShrink: 0,
        alignSelf: "stretch",
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
      </div>
      {props.showFocus && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 120,
        height: 24,
        overflow: "hidden",
        borderRadius: 4,
        boxShadow: "0 0 0 2px var(--neutral-stroke-focus-2-rest)",
      }}>
        <svg width={120} height={24} viewBox="0 0 120 24" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 120,
          height: 24,
          overflow: "hidden",
          borderRadius: 4,
          color: "var(--neutral-stroke-focus-1-rest)",
        }}>
          <path d={"M 4 0 L 4 1 L 116 1 L 116 0 L 116 -1 L 4 -1 L 4 0 Z M 120 4 L 119 4 L 119 20 L 120 20 L 121 20 L 121 4 L 120 4 Z M 116 24 L 116 23 L 4 23 L 4 24 L 4 25 L 116 25 L 116 24 Z M 0 20 L 1 20 L 1 4 L 0 4 L -1 4 L -1 20 L 0 20 Z M 4 24 L 4 23 C 2.343 23 1 21.657 1 20 L 0 20 L -1 20 C -1 22.761 1.239 25 4 25 L 4 24 Z M 120 20 L 119 20 C 119 21.657 117.657 23 116 23 L 116 24 L 116 25 C 118.761 25 121 22.761 121 20 L 120 20 Z M 116 0 L 116 1 C 117.657 1 119 2.343 119 4 L 120 4 L 121 4 C 121 1.239 118.761 -1 116 -1 L 116 0 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
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
        padding: "8px 8px 8px 8px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-s) * 1px)",
        paddingTop: "calc(var(--vertical-s) * 1px)",
        paddingRight: "calc(var(--horizontal-s) * 1px)",
        paddingBottom: "calc(var(--vertical-s) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
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
      </div>
      <div style={{
        position: "relative",
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
            backgroundColor: "var(--rest)",
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
        padding: "8px 8px 8px 8px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-s) * 1px)",
        paddingTop: "calc(var(--vertical-s) * 1px)",
        paddingRight: "calc(var(--horizontal-s) * 1px)",
        paddingBottom: "calc(var(--vertical-s) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
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
      </div>
      <div style={{
        position: "relative",
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
        flexShrink: 0,
        alignSelf: "stretch",
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
      </div>
      <div style={{
        position: "relative",
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
        flexShrink: 0,
        alignSelf: "stretch",
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
      </div>
      {props.showFocus && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 120,
        height: 24,
        overflow: "hidden",
        borderRadius: 4,
        boxShadow: "0 0 0 2px var(--neutral-stroke-focus-2-rest)",
      }}>
        <svg width={120} height={24} viewBox="0 0 120 24" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 120,
          height: 24,
          overflow: "hidden",
          borderRadius: 4,
          color: "var(--neutral-stroke-focus-1-rest)",
        }}>
          <path d={"M 4 0 L 4 1 L 116 1 L 116 0 L 116 -1 L 4 -1 L 4 0 Z M 120 4 L 119 4 L 119 20 L 120 20 L 121 20 L 121 4 L 120 4 Z M 116 24 L 116 23 L 4 23 L 4 24 L 4 25 L 116 25 L 116 24 Z M 0 20 L 1 20 L 1 4 L 0 4 L -1 4 L -1 20 L 0 20 Z M 4 24 L 4 23 C 2.343 23 1 21.657 1 20 L 0 20 L -1 20 C -1 22.761 1.239 25 4 25 L 4 24 Z M 120 20 L 119 20 C 119 21.657 117.657 23 116 23 L 116 24 L 116 25 C 118.761 25 121 22.761 121 20 L 120 20 Z M 116 0 L 116 1 C 117.657 1 119 2.343 119 4 L 120 4 L 121 4 C 121 1.239 118.761 -1 116 -1 L 116 0 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
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
        padding: "8px 8px 8px 8px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-s) * 1px)",
        paddingTop: "calc(var(--vertical-s) * 1px)",
        paddingRight: "calc(var(--horizontal-s) * 1px)",
        paddingBottom: "calc(var(--vertical-s) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
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
      </div>
      <div style={{
        position: "relative",
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
        flexShrink: 0,
        alignSelf: "stretch",
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
      </div>
      <div style={{
        position: "relative",
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
            backgroundColor: "var(--rest)",
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
        padding: "8px 8px 8px 8px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-s) * 1px)",
        paddingTop: "calc(var(--vertical-s) * 1px)",
        paddingRight: "calc(var(--horizontal-s) * 1px)",
        paddingBottom: "calc(var(--vertical-s) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
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
      </div>
      <div style={{
        position: "relative",
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
        flexShrink: 0,
        alignSelf: "stretch",
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
      </div>
      {props.showFocus && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 120,
        height: 24,
        overflow: "hidden",
        borderRadius: 4,
        boxShadow: "0 0 0 2px var(--neutral-stroke-focus-2-rest)",
      }}>
        <svg width={120} height={24} viewBox="0 0 120 24" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 120,
          height: 24,
          overflow: "hidden",
          borderRadius: 4,
          color: "var(--neutral-stroke-focus-1-rest)",
        }}>
          <path d={"M 4 0 L 4 1 L 116 1 L 116 0 L 116 -1 L 4 -1 L 4 0 Z M 120 4 L 119 4 L 119 20 L 120 20 L 121 20 L 121 4 L 120 4 Z M 116 24 L 116 23 L 4 23 L 4 24 L 4 25 L 116 25 L 116 24 Z M 0 20 L 1 20 L 1 4 L 0 4 L -1 4 L -1 20 L 0 20 Z M 4 24 L 4 23 C 2.343 23 1 21.657 1 20 L 0 20 L -1 20 C -1 22.761 1.239 25 4 25 L 4 24 Z M 120 20 L 119 20 C 119 21.657 117.657 23 116 23 L 116 24 L 116 25 C 118.761 25 121 22.761 121 20 L 120 20 Z M 116 0 L 116 1 C 117.657 1 119 2.343 119 4 L 120 4 L 121 4 C 121 1.239 118.761 -1 116 -1 L 116 0 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
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
        padding: "8px 8px 8px 8px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-s) * 1px)",
        paddingTop: "calc(var(--vertical-s) * 1px)",
        paddingRight: "calc(var(--horizontal-s) * 1px)",
        paddingBottom: "calc(var(--vertical-s) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
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
      </div>
      <div style={{
        position: "relative",
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
        flexShrink: 0,
        alignSelf: "stretch",
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
      </div>
      <div style={{
        position: "relative",
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
        flexShrink: 0,
        alignSelf: "stretch",
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
      </div>
      <div style={{
        position: "relative",
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
            backgroundColor: "var(--rest)",
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
        padding: "8px 8px 8px 8px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-s) * 1px)",
        paddingTop: "calc(var(--vertical-s) * 1px)",
        paddingRight: "calc(var(--horizontal-s) * 1px)",
        paddingBottom: "calc(var(--vertical-s) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
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
      </div>
      {props.showFocus && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 120,
        height: 24,
        overflow: "hidden",
        borderRadius: 4,
        boxShadow: "0 0 0 2px var(--neutral-stroke-focus-2-rest)",
      }}>
        <svg width={120} height={24} viewBox="0 0 120 24" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 120,
          height: 24,
          overflow: "hidden",
          borderRadius: 4,
          color: "var(--neutral-stroke-focus-1-rest)",
        }}>
          <path d={"M 4 0 L 4 1 L 116 1 L 116 0 L 116 -1 L 4 -1 L 4 0 Z M 120 4 L 119 4 L 119 20 L 120 20 L 121 20 L 121 4 L 120 4 Z M 116 24 L 116 23 L 4 23 L 4 24 L 4 25 L 116 25 L 116 24 Z M 0 20 L 1 20 L 1 4 L 0 4 L -1 4 L -1 20 L 0 20 Z M 4 24 L 4 23 C 2.343 23 1 21.657 1 20 L 0 20 L -1 20 C -1 22.761 1.239 25 4 25 L 4 24 Z M 120 20 L 119 20 C 119 21.657 117.657 23 116 23 L 116 24 L 116 25 C 118.761 25 121 22.761 121 20 L 120 20 Z M 116 0 L 116 1 C 117.657 1 119 2.343 119 4 L 120 4 L 121 4 C 121 1.239 118.761 -1 116 -1 L 116 0 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
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
        padding: "8px 8px 8px 8px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-s) * 1px)",
        paddingTop: "calc(var(--vertical-s) * 1px)",
        paddingRight: "calc(var(--horizontal-s) * 1px)",
        paddingBottom: "calc(var(--vertical-s) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
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
      </div>
      <div style={{
        position: "relative",
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
        flexShrink: 0,
        alignSelf: "stretch",
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
      </div>
      <div style={{
        position: "relative",
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
        flexShrink: 0,
        alignSelf: "stretch",
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
      </div>
      <div style={{
        position: "relative",
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
        flexShrink: 0,
        alignSelf: "stretch",
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
      </div>
      <div style={{
        position: "relative",
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
            backgroundColor: "var(--rest)",
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
        width: 120,
        height: 24,
        overflow: "hidden",
        borderRadius: 4,
        boxShadow: "0 0 0 2px var(--neutral-stroke-focus-2-rest)",
      }}>
        <svg width={120} height={24} viewBox="0 0 120 24" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 120,
          height: 24,
          overflow: "hidden",
          borderRadius: 4,
          color: "var(--neutral-stroke-focus-1-rest)",
        }}>
          <path d={"M 4 0 L 4 1 L 116 1 L 116 0 L 116 -1 L 4 -1 L 4 0 Z M 120 4 L 119 4 L 119 20 L 120 20 L 121 20 L 121 4 L 120 4 Z M 116 24 L 116 23 L 4 23 L 4 24 L 4 25 L 116 25 L 116 24 Z M 0 20 L 1 20 L 1 4 L 0 4 L -1 4 L -1 20 L 0 20 Z M 4 24 L 4 23 C 2.343 23 1 21.657 1 20 L 0 20 L -1 20 C -1 22.761 1.239 25 4 25 L 4 24 Z M 120 20 L 119 20 C 119 21.657 117.657 23 116 23 L 116 24 L 116 25 C 118.761 25 121 22.761 121 20 L 120 20 Z M 116 0 L 116 1 C 117.657 1 119 2.343 119 4 L 120 4 L 121 4 C 121 1.239 118.761 -1 116 -1 L 116 0 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
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
export default CarouselSteps;
