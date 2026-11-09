import { __slateNorm } from '../internal/slateProps.jsx';
// figma node: 9291:4111 NavSubItem (6 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "state=" + __venc(p.state) + '|' + "selected=" + __venc(p.selected) + '|' + "secondaryActions=" + __venc(p.secondaryActions);

export function NavSubItem(_p = {}) {
  _p = __slateNorm(_p, {"state":["hover","pressed","rest"]});
  const props = { ..._p, subItemContent: _p.subItemContent ?? "Nav sub item content", focus: _p.focus ?? false, state: _p.state ?? "hover", selected: _p.selected ?? false, secondaryActions: _p.secondaryActions ?? true };
  const __body0 = () => (
    <div className={props.className} style={{
      width: 260,
      height: 32,
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--horizontal-xxs) * 1px)",
      padding: "0px 4px 0px 4px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--horizontal-xs) * 1px)",
      paddingTop: "calc(var(--vertical-none) * 1px)",
      paddingRight: "calc(var(--horizontal-xs) * 1px)",
      paddingBottom: "calc(var(--vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        transform: "matrix(0,1,-1,0,8,4)",
        transformOrigin: "0 0",
        width: 24,
        height: 4,
        opacity: 0,
        borderRadius: 2,
        background: "linear-gradient(102.529deg, rgb(0,120,212) 20.40%, rgb(3,100,184) 96.29%)",
      }} />
      <div style={{
        position: "relative",
        backgroundColor: "var(--neutral-background-4-rest)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--horizontal-l) * 1px)",
        padding: "0px 6px 0px 46px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingTop: "calc(var(--vertical-none) * 1px)",
        paddingRight: "calc(var(--horizontal-snudge) * 1px)",
        paddingBottom: "calc(var(--vertical-none) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--horizontal-snudge) * 1px)",
          padding: "4px 0px 6px 0px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--horizontal-none) * 1px)",
          paddingTop: "calc(var(--vertical-xs) * 1px)",
          paddingRight: "calc(var(--horizontal-none) * 1px)",
          paddingBottom: "calc(var(--vertical-snudge) * 1px)",
          flexGrow: 1,
          alignSelf: "stretch",
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 14,
            whiteSpace: "nowrap",
            lineHeight: "20px",
            color: "var(--neutral-foreground-2-rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.subItemContent}</span>
        </div>
      </div>
      {props.focus && (
      <div style={{
        position: "absolute",
        left: 12,
        top: 2,
        width: 246,
        height: 28,
        overflow: "hidden",
        borderRadius: 4,
        boxShadow: "0 0 0 2px var(--neutral-stroke-focus-2-rest-2)",
      }}>
        <svg width={246} height={28} viewBox="0 0 246 28" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 246,
          height: 28,
          overflow: "hidden",
          borderRadius: 4,
          color: "var(--neutral-stroke-focus-1-rest-2)",
        }}>
          <path d={"M 4 0 L 4 1 L 242 1 L 242 0 L 242 -1 L 4 -1 L 4 0 Z M 246 4 L 245 4 L 245 24 L 246 24 L 247 24 L 247 4 L 246 4 Z M 242 28 L 242 27 L 4 27 L 4 28 L 4 29 L 242 29 L 242 28 Z M 0 24 L 1 24 L 1 4 L 0 4 L -1 4 L -1 24 L 0 24 Z M 4 28 L 4 27 C 2.343 27 1 25.657 1 24 L 0 24 L -1 24 C -1 26.761 1.239 29 4 29 L 4 28 Z M 246 24 L 245 24 C 245 25.657 243.657 27 242 27 L 242 28 L 242 29 C 244.761 29 247 26.761 247 24 L 246 24 Z M 242 0 L 242 1 C 243.657 1 245 2.343 245 4 L 246 4 L 247 4 C 247 1.239 244.761 -1 242 -1 L 242 0 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: 260,
      height: 32,
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--horizontal-xxs) * 1px)",
      padding: "0px 4px 0px 4px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--horizontal-xs) * 1px)",
      paddingTop: "calc(var(--vertical-none) * 1px)",
      paddingRight: "calc(var(--horizontal-xs) * 1px)",
      paddingBottom: "calc(var(--vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        transform: "matrix(0,1,-1,0,8,4)",
        transformOrigin: "0 0",
        width: 24,
        height: 4,
        opacity: 0,
        borderRadius: 2,
        background: "linear-gradient(102.529deg, rgb(0,120,212) 20.40%, rgb(3,100,184) 96.29%)",
      }} />
      <div style={{
        position: "relative",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-4-hover)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--horizontal-l) * 1px)",
        padding: "0px 6px 0px 46px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingTop: "calc(var(--vertical-none) * 1px)",
        paddingRight: "calc(var(--horizontal-snudge) * 1px)",
        paddingBottom: "calc(var(--vertical-none) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--horizontal-s) * 1px)",
          padding: "4px 0px 6px 0px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--horizontal-none) * 1px)",
          paddingTop: "calc(var(--vertical-xs) * 1px)",
          paddingRight: "calc(var(--horizontal-none) * 1px)",
          paddingBottom: "calc(var(--vertical-snudge) * 1px)",
          flexGrow: 1,
          alignSelf: "stretch",
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 14,
            whiteSpace: "nowrap",
            lineHeight: "20px",
            color: "var(--neutral-foreground-2-hover)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.subItemContent}</span>
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "4px 0px 4px 0px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          gap: "calc(var(--horizontal-none) * 1px)",
          paddingTop: "calc(var(--vertical-xs) * 1px)",
          paddingBottom: "calc(var(--vertical-xs) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            borderRadius: 4,
            display: "flex",
            flexDirection: "row",
            padding: "2px 2px 2px 2px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
              justifyContent: "center",
              alignItems: "center",
              flexWrap: "nowrap",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>
              <div style={{
                position: "relative",
                width: 20,
                overflow: "hidden",
                flexShrink: 0,
                alignSelf: "stretch",
              }}>
                <svg width={14.968} height={14.968} viewBox="0 0 14.968 14.968" fill="none" style={{
                  position: "absolute",
                  left: 3,
                  top: 2.032,
                  width: 14.968,
                  height: 14.968,
                  color: "var(--neutral-foreground-1-rest)",
                }}>
                  <path d={"M 7.122 1.106 C 7.733 -0.115 9.36 -0.379 10.325 0.586 L 14.382 4.643 C 15.347 5.608 15.083 7.236 13.862 7.846 L 10.404 9.575 C 10.075 9.74 9.818 10.019 9.682 10.36 L 8.246 13.95 C 7.979 14.616 7.118 14.793 6.61 14.285 L 4 11.675 L 0.707 14.968 L 0 14.968 L 0 14.261 L 3.293 10.968 L 0.683 8.358 C 0.175 7.85 0.352 6.989 1.018 6.722 L 4.608 5.286 C 4.949 5.15 5.229 4.893 5.393 4.564 L 7.122 1.106 Z M 9.618 1.293 C 9.135 0.811 8.322 0.943 8.016 1.553 L 6.287 5.012 C 6.013 5.559 5.548 5.987 4.98 6.215 L 1.39 7.651 L 7.317 13.578 L 8.753 9.988 C 8.981 9.42 9.409 8.955 9.957 8.681 L 13.415 6.952 C 14.025 6.646 14.158 5.833 13.675 5.35 L 9.618 1.293 Z"} fill="currentColor" fillRule="nonzero" />
                </svg>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
  const __body2 = () => (
    <div className={props.className} style={{
      width: 260,
      height: 32,
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--horizontal-xxs) * 1px)",
      padding: "0px 4px 0px 4px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--horizontal-xs) * 1px)",
      paddingTop: "calc(var(--vertical-none) * 1px)",
      paddingRight: "calc(var(--horizontal-xs) * 1px)",
      paddingBottom: "calc(var(--vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        transform: "matrix(0,1,-1,0,8,4)",
        transformOrigin: "0 0",
        width: 24,
        height: 4,
        opacity: 0,
        borderRadius: 2,
        background: "linear-gradient(102.529deg, rgb(0,120,212) 20.40%, rgb(3,100,184) 96.29%)",
      }} />
      <div style={{
        position: "relative",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-4-pressed)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--horizontal-l) * 1px)",
        padding: "0px 6px 0px 46px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingTop: "calc(var(--vertical-none) * 1px)",
        paddingRight: "calc(var(--horizontal-snudge) * 1px)",
        paddingBottom: "calc(var(--vertical-none) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--horizontal-s) * 1px)",
          padding: "4px 0px 6px 0px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--horizontal-none) * 1px)",
          paddingTop: "calc(var(--vertical-xs) * 1px)",
          paddingRight: "calc(var(--horizontal-none) * 1px)",
          paddingBottom: "calc(var(--vertical-snudge) * 1px)",
          flexGrow: 1,
          alignSelf: "stretch",
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 14,
            whiteSpace: "nowrap",
            lineHeight: "20px",
            color: "var(--neutral-foreground-2-pressed)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.subItemContent}</span>
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "4px 0px 4px 0px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          gap: "calc(var(--horizontal-none) * 1px)",
          paddingTop: "calc(var(--vertical-xs) * 1px)",
          paddingBottom: "calc(var(--vertical-xs) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            borderRadius: 4,
            display: "flex",
            flexDirection: "row",
            padding: "2px 2px 2px 2px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
              justifyContent: "center",
              alignItems: "center",
              flexWrap: "nowrap",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>
              <div style={{
                position: "relative",
                width: 20,
                overflow: "hidden",
                flexShrink: 0,
                alignSelf: "stretch",
              }}>
                <svg width={14.968} height={14.968} viewBox="0 0 14.968 14.968" fill="none" style={{
                  position: "absolute",
                  left: 3,
                  top: 2.032,
                  width: 14.968,
                  height: 14.968,
                  color: "var(--neutral-foreground-1-rest)",
                }}>
                  <path d={"M 7.122 1.106 C 7.733 -0.115 9.36 -0.379 10.325 0.586 L 14.382 4.643 C 15.347 5.608 15.083 7.236 13.862 7.846 L 10.404 9.575 C 10.075 9.74 9.818 10.019 9.682 10.36 L 8.246 13.95 C 7.979 14.616 7.118 14.793 6.61 14.285 L 4 11.675 L 0.707 14.968 L 0 14.968 L 0 14.261 L 3.293 10.968 L 0.683 8.358 C 0.175 7.85 0.352 6.989 1.018 6.722 L 4.608 5.286 C 4.949 5.15 5.229 4.893 5.393 4.564 L 7.122 1.106 Z M 9.618 1.293 C 9.135 0.811 8.322 0.943 8.016 1.553 L 6.287 5.012 C 6.013 5.559 5.548 5.987 4.98 6.215 L 1.39 7.651 L 7.317 13.578 L 8.753 9.988 C 8.981 9.42 9.409 8.955 9.957 8.681 L 13.415 6.952 C 14.025 6.646 14.158 5.833 13.675 5.35 L 9.618 1.293 Z"} fill="currentColor" fillRule="nonzero" />
                </svg>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
  const __body3 = () => (
    <div className={props.className} style={{
      width: 260,
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--horizontal-xxs) * 1px)",
      padding: "0px 4px 0px 4px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--horizontal-xs) * 1px)",
      paddingTop: "calc(var(--vertical-none) * 1px)",
      paddingRight: "calc(var(--horizontal-xs) * 1px)",
      paddingBottom: "calc(var(--vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        transform: "matrix(0,1,-1,0,8,6)",
        transformOrigin: "0 0",
        width: 20,
        height: 4,
        borderRadius: 2,
        backgroundColor: "var(--brand-foreground-compound-rest)",
      }} />
      <div style={{
        position: "relative",
        backgroundColor: "var(--neutral-background-4-rest)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--horizontal-l) * 1px)",
        padding: "0px 6px 0px 46px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingTop: "calc(var(--vertical-none) * 1px)",
        paddingRight: "calc(var(--horizontal-snudge) * 1px)",
        paddingBottom: "calc(var(--vertical-none) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--horizontal-s) * 1px)",
          padding: "4px 0px 6px 0px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--horizontal-none) * 1px)",
          paddingTop: "calc(var(--vertical-xs) * 1px)",
          paddingRight: "calc(var(--horizontal-none) * 1px)",
          paddingBottom: "calc(var(--vertical-snudge) * 1px)",
          flexGrow: 1,
          alignSelf: "stretch",
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 600,
            fontSize: 14,
            whiteSpace: "nowrap",
            lineHeight: "20px",
            color: "var(--neutral-foreground-1-rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.subItemContent}</span>
        </div>
        {props.focus && (
        <div style={{
          position: "absolute",
          left: 2,
          top: 2,
          width: 212,
          height: 28,
          overflow: "hidden",
          borderRadius: 4,
          boxShadow: "0 0 0 2px var(--neutral-stroke-focus-2-rest-2)",
        }}>
          <svg width={212} height={28} viewBox="0 0 212 28" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 212,
            height: 28,
            overflow: "hidden",
            borderRadius: 4,
            color: "var(--neutral-stroke-focus-1-rest-2)",
          }}>
            <path d={"M 4 0 L 4 1 L 208 1 L 208 0 L 208 -1 L 4 -1 L 4 0 Z M 212 4 L 211 4 L 211 24 L 212 24 L 213 24 L 213 4 L 212 4 Z M 208 28 L 208 27 L 4 27 L 4 28 L 4 29 L 208 29 L 208 28 Z M 0 24 L 1 24 L 1 4 L 0 4 L -1 4 L -1 24 L 0 24 Z M 4 28 L 4 27 C 2.343 27 1 25.657 1 24 L 0 24 L -1 24 C -1 26.761 1.239 29 4 29 L 4 28 Z M 212 24 L 211 24 C 211 25.657 209.657 27 208 27 L 208 28 L 208 29 C 210.761 29 213 26.761 213 24 L 212 24 Z M 208 0 L 208 1 C 209.657 1 211 2.343 211 4 L 212 4 L 213 4 C 213 1.239 210.761 -1 208 -1 L 208 0 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
        )}
      </div>
    </div>
  );
  const __body4 = () => (
    <div className={props.className} style={{
      width: 260,
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--horizontal-xxs) * 1px)",
      padding: "0px 4px 0px 4px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--horizontal-xs) * 1px)",
      paddingTop: "calc(var(--vertical-none) * 1px)",
      paddingRight: "calc(var(--horizontal-xs) * 1px)",
      paddingBottom: "calc(var(--vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        transform: "matrix(0,1,-1,0,8,6)",
        transformOrigin: "0 0",
        width: 20,
        height: 4,
        borderRadius: 2,
        backgroundColor: "var(--brand-foreground-compound-rest)",
      }} />
      <div style={{
        position: "relative",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-4-hover)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--horizontal-l) * 1px)",
        padding: "0px 6px 0px 46px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingTop: "calc(var(--vertical-none) * 1px)",
        paddingRight: "calc(var(--horizontal-snudge) * 1px)",
        paddingBottom: "calc(var(--vertical-none) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--horizontal-s) * 1px)",
          padding: "4px 0px 6px 0px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--horizontal-none) * 1px)",
          paddingTop: "calc(var(--vertical-xs) * 1px)",
          paddingRight: "calc(var(--horizontal-none) * 1px)",
          paddingBottom: "calc(var(--vertical-snudge) * 1px)",
          flexGrow: 1,
          alignSelf: "stretch",
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 600,
            fontSize: 14,
            whiteSpace: "nowrap",
            lineHeight: "20px",
            color: "var(--neutral-foreground-1-hover)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.subItemContent}</span>
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "4px 0px 4px 0px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          gap: "calc(var(--horizontal-none) * 1px)",
          paddingTop: "calc(var(--vertical-xs) * 1px)",
          paddingBottom: "calc(var(--vertical-xs) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            borderRadius: 4,
            display: "flex",
            flexDirection: "row",
            padding: "2px 2px 2px 2px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
              justifyContent: "center",
              alignItems: "center",
              flexWrap: "nowrap",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>
              <div style={{
                position: "relative",
                width: 20,
                overflow: "hidden",
                flexShrink: 0,
                alignSelf: "stretch",
              }}>
                <svg width={14.968} height={14.968} viewBox="0 0 14.968 14.968" fill="none" style={{
                  position: "absolute",
                  left: 3,
                  top: 2.032,
                  width: 14.968,
                  height: 14.968,
                  color: "var(--neutral-foreground-1-rest)",
                }}>
                  <path d={"M 7.122 1.106 C 7.733 -0.115 9.36 -0.379 10.325 0.586 L 14.382 4.643 C 15.347 5.608 15.083 7.236 13.862 7.846 L 10.404 9.575 C 10.075 9.74 9.818 10.019 9.682 10.36 L 8.246 13.95 C 7.979 14.616 7.118 14.793 6.61 14.285 L 4 11.675 L 0.707 14.968 L 0 14.968 L 0 14.261 L 3.293 10.968 L 0.683 8.358 C 0.175 7.85 0.352 6.989 1.018 6.722 L 4.608 5.286 C 4.949 5.15 5.229 4.893 5.393 4.564 L 7.122 1.106 Z M 9.618 1.293 C 9.135 0.811 8.322 0.943 8.016 1.553 L 6.287 5.012 C 6.013 5.559 5.548 5.987 4.98 6.215 L 1.39 7.651 L 7.317 13.578 L 8.753 9.988 C 8.981 9.42 9.409 8.955 9.957 8.681 L 13.415 6.952 C 14.025 6.646 14.158 5.833 13.675 5.35 L 9.618 1.293 Z"} fill="currentColor" fillRule="nonzero" />
                </svg>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
  const __body5 = () => (
    <div className={props.className} style={{
      width: 260,
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--horizontal-xxs) * 1px)",
      padding: "0px 4px 0px 4px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--horizontal-xs) * 1px)",
      paddingTop: "calc(var(--vertical-none) * 1px)",
      paddingRight: "calc(var(--horizontal-xs) * 1px)",
      paddingBottom: "calc(var(--vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        transform: "matrix(0,1,-1,0,8,6)",
        transformOrigin: "0 0",
        width: 20,
        height: 4,
        borderRadius: 2,
        backgroundColor: "var(--brand-foreground-compound-rest)",
      }} />
      <div style={{
        position: "relative",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-4-pressed)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--horizontal-l) * 1px)",
        padding: "0px 6px 0px 46px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingTop: "calc(var(--vertical-none) * 1px)",
        paddingRight: "calc(var(--horizontal-snudge) * 1px)",
        paddingBottom: "calc(var(--vertical-none) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--horizontal-s) * 1px)",
          padding: "4px 0px 6px 0px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--horizontal-none) * 1px)",
          paddingTop: "calc(var(--vertical-xs) * 1px)",
          paddingRight: "calc(var(--horizontal-none) * 1px)",
          paddingBottom: "calc(var(--vertical-snudge) * 1px)",
          flexGrow: 1,
          alignSelf: "stretch",
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 600,
            fontSize: 14,
            whiteSpace: "nowrap",
            lineHeight: "20px",
            color: "var(--neutral-foreground-1-pressed)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.subItemContent}</span>
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "4px 0px 4px 0px",
          justifyContent: "flex-end",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          gap: "calc(var(--horizontal-none) * 1px)",
          paddingTop: "calc(var(--vertical-xs) * 1px)",
          paddingBottom: "calc(var(--vertical-xs) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            borderRadius: 4,
            display: "flex",
            flexDirection: "row",
            padding: "2px 2px 2px 2px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
              justifyContent: "center",
              alignItems: "center",
              flexWrap: "nowrap",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>
              <div style={{
                position: "relative",
                width: 20,
                overflow: "hidden",
                flexShrink: 0,
                alignSelf: "stretch",
              }}>
                <svg width={14.968} height={14.968} viewBox="0 0 14.968 14.968" fill="none" style={{
                  position: "absolute",
                  left: 3,
                  top: 2.032,
                  width: 14.968,
                  height: 14.968,
                  color: "var(--neutral-foreground-1-rest)",
                }}>
                  <path d={"M 7.122 1.106 C 7.733 -0.115 9.36 -0.379 10.325 0.586 L 14.382 4.643 C 15.347 5.608 15.083 7.236 13.862 7.846 L 10.404 9.575 C 10.075 9.74 9.818 10.019 9.682 10.36 L 8.246 13.95 C 7.979 14.616 7.118 14.793 6.61 14.285 L 4 11.675 L 0.707 14.968 L 0 14.968 L 0 14.261 L 3.293 10.968 L 0.683 8.358 C 0.175 7.85 0.352 6.989 1.018 6.722 L 4.608 5.286 C 4.949 5.15 5.229 4.893 5.393 4.564 L 7.122 1.106 Z M 9.618 1.293 C 9.135 0.811 8.322 0.943 8.016 1.553 L 6.287 5.012 C 6.013 5.559 5.548 5.987 4.98 6.215 L 1.39 7.651 L 7.317 13.578 L 8.753 9.988 C 8.981 9.42 9.409 8.955 9.957 8.681 L 13.415 6.952 C 14.025 6.646 14.158 5.833 13.675 5.35 L 9.618 1.293 Z"} fill="currentColor" fillRule="nonzero" />
                </svg>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
  const __impls = {
    // figma: State=Rest, Selected=False, Secondary actions=False
    "state=rest|selected=false|secondaryActions=false": __body0,
    // figma: State=Hover, Selected=False, Secondary actions=True
    "state=hover|selected=false|secondaryActions=true": __body1,
    // figma: State=Pressed, Selected=False, Secondary actions=True
    "state=pressed|selected=false|secondaryActions=true": __body2,
    // figma: State=Rest, Selected=True, Secondary actions=False
    "state=rest|selected=true|secondaryActions=false": __body3,
    // figma: State=Hover, Selected=True, Secondary actions=True
    "state=hover|selected=true|secondaryActions=true": __body4,
    // figma: State=Pressed, Selected=True, Secondary actions=True
    "state=pressed|selected=true|secondaryActions=true": __body5,
  };
  return (__impls[__vkey(props)] ?? __body1)();
}
export default NavSubItem;
