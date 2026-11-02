import { __slateNorm } from '../internal/slateProps.jsx';
import { Info } from '../internal/Info.jsx';

// figma node: 9121:5944 Message bar (2 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "layout=" + __venc(p.layout);

export function MessageBar(_p = {}) {
  _p = __slateNorm(_p, {"layout":["single line (default)","multi line"]});
  const props = { ..._p, textString: _p.textString ?? "Descriptive title   Message providing information to the user with actionable insights. Link", actions: _p.actions ?? true, layout: _p.layout ?? "single line (default)", dismiss: _p.dismiss ?? true, additionalAction: _p.additionalAction ?? true };
  const __body0 = () => (
    <div className={props.className} style={{
      width: 800,
      height: 36,
      borderRadius: 4,
      backgroundColor: "var(--status-background-color)",
      boxShadow: "inset 0 0 0 1px var(--status-stroke-color)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
      padding: "6px 12px 6px 12px",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
            position: "relative",
            width: 20,
            height: 20,
            flexShrink: 0,
            color: "var(--neutral-foreground-3-rest)",
          }}>{props.icon1 ?? <Info size={"20"} theme={"filled"} />}</div>
      </div>
      <span style={{
        position: "relative",
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 12,
        whiteSpace: "nowrap",
        overflow: "hidden",
        textOverflow: "ellipsis",
        lineHeight: "16px",
        color: "var(--neutral-foreground-1-rest)",
        flexGrow: 1,
      }}>{props.textString}</span>
      {props.actions && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
        justifyContent: "flex-end",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          borderRadius: 4,
          backgroundColor: "var(--neutral-background-1-rest)",
          borderTop: "1px solid var(--neutral-stroke-1-rest)",
          borderRight: "1px solid var(--neutral-stroke-1-rest)",
          borderBottom: "1px solid var(--neutral-stroke-1-rest)",
          borderLeft: "1px solid var(--neutral-stroke-1-rest)",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          padding: "5px 11px 5px 11px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "max(0px, calc(var(--spacing-horizontal-m-2) * 1px - 1px))",
          paddingTop: "max(0px, calc(var(--spacing-vertical-xxs) * 1px - 1px))",
          paddingRight: "max(0px, calc(var(--spacing-horizontal-m-2) * 1px - 1px))",
          paddingBottom: "max(0px, calc(var(--spacing-vertical-xxs) * 1px - 1px))",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            height: 20,
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            flexShrink: 0,
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              padding: "1px 0px 1px 0px",
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              paddingTop: "calc(var(--spacing-vertical-xxs) * 0.5px)", paddingBottom: "calc(var(--spacing-vertical-xxs) * 0.5px)",
              flexShrink: 0,
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
              }}>{props.actionLabel ?? "Action"}</span>
            </div>
          </div>
        </div>
        {props.additionalAction && (
        <div style={{
          position: "relative",
          borderRadius: 4,
          backgroundColor: "var(--neutral-background-1-rest)",
          borderTop: "1px solid var(--neutral-stroke-1-rest)",
          borderRight: "1px solid var(--neutral-stroke-1-rest)",
          borderBottom: "1px solid var(--neutral-stroke-1-rest)",
          borderLeft: "1px solid var(--neutral-stroke-1-rest)",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          padding: "5px 11px 5px 11px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "max(0px, calc(var(--spacing-horizontal-m-2) * 1px - 1px))",
          paddingTop: "max(0px, calc(var(--spacing-vertical-xxs) * 1px - 1px))",
          paddingRight: "max(0px, calc(var(--spacing-horizontal-m-2) * 1px - 1px))",
          paddingBottom: "max(0px, calc(var(--spacing-vertical-xxs) * 1px - 1px))",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            height: 20,
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            flexShrink: 0,
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              padding: "1px 0px 1px 0px",
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              paddingTop: "calc(var(--spacing-vertical-xxs) * 0.5px)", paddingBottom: "calc(var(--spacing-vertical-xxs) * 0.5px)",
              flexShrink: 0,
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
              }}>{props.secondaryActionLabel ?? "Action"}</span>
            </div>
          </div>
        </div>
        )}
      </div>
      )}
      {props.dismiss && (
      <div style={{
        position: "relative",
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
            <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
              position: "absolute",
              left: 4,
              top: 4,
              width: 12,
              height: 12,
              color: "var(--neutral-foreground-2-rest)",
            }}>
              <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 6 5.293 L 11.146 0.146 C 11.32 -0.027 11.589 -0.046 11.784 0.089 L 11.854 0.146 C 12.027 0.32 12.046 0.589 11.911 0.784 L 11.854 0.854 L 6.707 6 L 11.854 11.146 C 12.027 11.32 12.046 11.589 11.911 11.784 L 11.854 11.854 C 11.68 12.027 11.411 12.046 11.216 11.911 L 11.146 11.854 L 6 6.707 L 0.854 11.854 C 0.68 12.027 0.411 12.046 0.216 11.911 L 0.146 11.854 C -0.027 11.68 -0.046 11.411 0.089 11.216 L 0.146 11.146 L 5.293 6 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
        </div>
      </div>
      )}
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: 800,
      borderRadius: 4,
      backgroundColor: "var(--status-background-color)",
      boxShadow: "inset 0 0 0 1px var(--status-stroke-color)",
      display: "flex",
      flexDirection: "column",
      gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
      padding: "6px 12px 6px 12px",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "2px 0px 2px 0px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
          flexShrink: 0,
        }}>
          <div style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--neutral-foreground-3-rest)",
            }}>{props.icon1 ?? <Info size={"20"} theme={"filled"} />}</div>
        </div>
        <div style={{
          position: "relative",
          height: 20,
          display: "flex",
          flexDirection: "column",
          gap: 8,
          padding: "4px 0px 4px 0px",
          justifyContent: "center",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingTop: "calc(var(--spacing-vertical-xs-2) * 1px)",
          flexGrow: 1,
        }}>
          <span style={{
            position: "relative",
            width: 520,
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            lineHeight: "16px",
            color: "var(--neutral-foreground-1-rest)",
            flexShrink: 0,
          }}>{props.textString}</span>
        </div>
        {props.dismiss && (
        <div style={{
          position: "relative",
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
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          flexShrink: 0,
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
          }}>
            <div style={{
              position: "relative",
              width: 20,
              height: 20,
              overflow: "hidden",
              flexShrink: 0,
            }}>
              <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
                position: "absolute",
                left: 4,
                top: 4,
                width: 12,
                height: 12,
                color: "var(--neutral-foreground-2-rest)",
              }}>
                <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 6 5.293 L 11.146 0.146 C 11.32 -0.027 11.589 -0.046 11.784 0.089 L 11.854 0.146 C 12.027 0.32 12.046 0.589 11.911 0.784 L 11.854 0.854 L 6.707 6 L 11.854 11.146 C 12.027 11.32 12.046 11.589 11.911 11.784 L 11.854 11.854 C 11.68 12.027 11.411 12.046 11.216 11.911 L 11.146 11.854 L 6 6.707 L 0.854 11.854 C 0.68 12.027 0.411 12.046 0.216 11.911 L 0.146 11.854 C -0.027 11.68 -0.046 11.411 0.089 11.216 L 0.146 11.146 L 5.293 6 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
              </svg>
            </div>
          </div>
        </div>
        )}
      </div>
      {props.actions && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
        justifyContent: "flex-end",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          borderRadius: 4,
          backgroundColor: "var(--neutral-background-1-rest)",
          borderTop: "1px solid var(--neutral-stroke-1-rest)",
          borderRight: "1px solid var(--neutral-stroke-1-rest)",
          borderBottom: "1px solid var(--neutral-stroke-1-rest)",
          borderLeft: "1px solid var(--neutral-stroke-1-rest)",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          padding: "5px 11px 5px 11px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "max(0px, calc(var(--spacing-horizontal-m-2) * 1px - 1px))",
          paddingTop: "max(0px, calc(var(--spacing-vertical-xxs) * 1px - 1px))",
          paddingRight: "max(0px, calc(var(--spacing-horizontal-m-2) * 1px - 1px))",
          paddingBottom: "max(0px, calc(var(--spacing-vertical-xxs) * 1px - 1px))",
          flexShrink: 0,
        }}>
          <div style={{
            position: "relative",
            height: 20,
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            flexShrink: 0,
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              padding: "1px 0px 1px 0px",
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              paddingTop: "calc(var(--spacing-vertical-xxs) * 0.5px)", paddingBottom: "calc(var(--spacing-vertical-xxs) * 0.5px)",
              flexShrink: 0,
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
              }}>{props.actionLabel ?? "Action"}</span>
            </div>
          </div>
        </div>
        {props.additionalAction && (
        <div style={{
          position: "relative",
          borderRadius: 4,
          backgroundColor: "var(--neutral-background-1-rest)",
          borderTop: "1px solid var(--neutral-stroke-1-rest)",
          borderRight: "1px solid var(--neutral-stroke-1-rest)",
          borderBottom: "1px solid var(--neutral-stroke-1-rest)",
          borderLeft: "1px solid var(--neutral-stroke-1-rest)",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          padding: "5px 11px 5px 11px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "max(0px, calc(var(--spacing-horizontal-m-2) * 1px - 1px))",
          paddingTop: "max(0px, calc(var(--spacing-vertical-xxs) * 1px - 1px))",
          paddingRight: "max(0px, calc(var(--spacing-horizontal-m-2) * 1px - 1px))",
          paddingBottom: "max(0px, calc(var(--spacing-vertical-xxs) * 1px - 1px))",
          flexShrink: 0,
        }}>
          <div style={{
            position: "relative",
            height: 20,
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            flexShrink: 0,
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              padding: "1px 0px 1px 0px",
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              paddingTop: "calc(var(--spacing-vertical-xxs) * 0.5px)", paddingBottom: "calc(var(--spacing-vertical-xxs) * 0.5px)",
              flexShrink: 0,
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
              }}>{props.secondaryActionLabel ?? "Action"}</span>
            </div>
          </div>
        </div>
        )}
      </div>
      )}
    </div>
  );
  const __impls = {
    // figma: Layout=Single line (Default)
    "layout=single line (default)": __body0,
    // figma: Layout=Multi line
    "layout=multi line": __body1,
  };
  return (__impls[__vkey(props)] ?? __body0)();
}
export default MessageBar;
