import { __slateNorm } from '../internal/slateProps.jsx';
import { Placeholder5 } from '../internal/Placeholder5.jsx';

// figma node: 9202:9382 Dialog (4 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "size=" + __venc(p.size) + '|' + "layout=" + __venc(p.layout);

export function Dialog(_p = {}) {
  _p = __slateNorm(_p, {"size":["600px","320px"],"layout":["text","placeholder"]});
  const props = { ..._p, footer: _p.footer ?? true, headerText: _p.headerText ?? "Main question or action", secondaryButton: _p.secondaryButton ?? true, tertiaryButton: _p.tertiaryButton ?? false, checkbox: _p.checkbox ?? false, dismiss: _p.dismiss ?? true, size: _p.size ?? "600px", layout: _p.layout ?? "text", body: _p.body ?? true, bodyText: _p.bodyText ?? "Here is more about the consequences of the main action, if details are needed." };
  const __body0 = () => (
    <div className={props.className} style={{
      width: 448,
      minWidth: 320,
      maxWidth: 520,
      overflow: "hidden",
      borderRadius: 8,
      backgroundColor: "var(--neutral-background-1-rest)",
      boxShadow: "var(--shadow-64)",
      display: "flex",
      flexDirection: "column",
      gap: "calc(var(--spacing-vertical-s-2) * 1px)",
      padding: "24px 24px 24px 24px",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-xxl) * 1px)",
      paddingTop: "calc(var(--spacing-horizontal-xxl) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xxl) * 1px)",
      paddingBottom: "calc(var(--spacing-horizontal-xxl) * 1px)",
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
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-horizontal-none-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 20,
          lineHeight: "28px",
          color: "var(--neutral-foreground-1-rest)",
          flexGrow: 1,
        }}>{props.headerText}</span>
        {props.dismiss && (
        <div style={{
          position: "relative",
          width: 28,
          height: 28,
          display: "flex",
          flexDirection: "row",
          padding: "0px 4px 0px 4px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingLeft: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
          flexShrink: 0,
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
                  color: "var(--neutral-foreground-1-rest)",
                }}>
                  <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 6 5.293 L 11.146 0.146 C 11.32 -0.027 11.589 -0.046 11.784 0.089 L 11.854 0.146 C 12.027 0.32 12.046 0.589 11.911 0.784 L 11.854 0.854 L 6.707 6 L 11.854 11.146 C 12.027 11.32 12.046 11.589 11.911 11.784 L 11.854 11.854 C 11.68 12.027 11.411 12.046 11.216 11.911 L 11.146 11.854 L 6 6.707 L 0.854 11.854 C 0.68 12.027 0.411 12.046 0.216 11.911 L 0.146 11.854 C -0.027 11.68 -0.046 11.411 0.089 11.216 L 0.146 11.146 L 5.293 6 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
                </svg>
              </div>
            </div>
          </div>
        </div>
        )}
      </div>
      {props.body && (
      <div style={{
          position: "relative",
          height: 120,
          flexShrink: 0,
          alignSelf: "stretch",
          width: "auto",
        }}>{props.contentSwap ?? <Placeholder5 slot={"1"} />}</div>
      )}
      {props.footer && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "4px 0px 4px 0px",
        justifyContent: "space-between",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-horizontal-none-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 116,
          height: 32,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
          flexShrink: 0,
        }}>
          {props.tertiaryButton && (
          <div style={{
            position: "relative",
            minWidth: 96,
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
            paddingTop: "max(0px, calc(var(--spacing-vertical-snudge-2) * 1px - 1px))",
            paddingRight: "max(0px, calc(var(--spacing-horizontal-m-2) * 1px - 1px))",
            paddingBottom: "max(0px, calc(var(--spacing-vertical-snudge-2) * 1px - 1px))",
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
                paddingTop: "calc(var(--typography-body-1-strong-offset) * 0.5px)", paddingBottom: "calc(var(--typography-body-1-strong-offset) * 0.5px)",
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
                }}>{props.tertiaryLabel ?? "Tertiary action"}</span>
              </div>
            </div>
          </div>
          )}
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
          flexShrink: 0,
        }}>
          <div style={{
            position: "relative",
            minWidth: 96,
            borderRadius: 4,
            backgroundColor: "var(--brand-background-1-rest)",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
            padding: "6px 12px 6px 12px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
            paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
            paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
            flexShrink: 0,
          }}>
            <div style={{
              position: "relative",
              height: 20,
              display: "flex",
              flexDirection: "row",
              gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
              padding: "0px 0px 0px 0px",
              justifyContent: "center",
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              flexShrink: 0,
            }}>
              <div style={{
                position: "relative",
                display: "flex",
                flexDirection: "row",
                alignItems: "flex-start",
                flexWrap: "nowrap",
                flexShrink: 0,
                alignSelf: "stretch",
              }}>
                <span style={{
                  position: "relative",
                  fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
                  fontWeight: 600,
                  fontSize: 14,
                  whiteSpace: "nowrap",
                  lineHeight: "20px",
                  color: "var(--neutral-foreground-on-brand-rest)",
                  flexShrink: 0,
                  alignSelf: "stretch",
                }}>{props.primaryLabel ?? "Take action"}</span>
              </div>
            </div>
          </div>
          {props.secondaryButton && (
          <div style={{
            position: "relative",
            minWidth: 96,
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
            paddingTop: "max(0px, calc(var(--spacing-vertical-snudge-2) * 1px - 1px))",
            paddingRight: "max(0px, calc(var(--spacing-horizontal-m-2) * 1px - 1px))",
            paddingBottom: "max(0px, calc(var(--spacing-vertical-snudge-2) * 1px - 1px))",
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
                }}>{props.secondaryLabel ?? "Different action"}</span>
              </div>
            </div>
          </div>
          )}
        </div>
      </div>
      )}
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: 448,
      minWidth: 320,
      maxWidth: 520,
      overflow: "hidden",
      borderRadius: 8,
      backgroundColor: "var(--neutral-background-1-rest)",
      boxShadow: "var(--shadow-64)",
      display: "flex",
      flexDirection: "column",
      gap: "calc(var(--spacing-vertical-s-2) * 1px)",
      padding: "24px 24px 24px 24px",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-xxl) * 1px)",
      paddingTop: "calc(var(--spacing-horizontal-xxl) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xxl) * 1px)",
      paddingBottom: "calc(var(--spacing-horizontal-xxl) * 1px)",
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
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-horizontal-none-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 20,
          lineHeight: "28px",
          color: "var(--neutral-foreground-1-rest)",
          flexGrow: 1,
        }}>{props.headerText}</span>
        {props.dismiss && (
        <div style={{
          position: "relative",
          width: 28,
          height: 28,
          display: "flex",
          flexDirection: "row",
          padding: "0px 4px 0px 4px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingLeft: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
          flexShrink: 0,
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
                  color: "var(--neutral-foreground-1-rest)",
                }}>
                  <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 6 5.293 L 11.146 0.146 C 11.32 -0.027 11.589 -0.046 11.784 0.089 L 11.854 0.146 C 12.027 0.32 12.046 0.589 11.911 0.784 L 11.854 0.854 L 6.707 6 L 11.854 11.146 C 12.027 11.32 12.046 11.589 11.911 11.784 L 11.854 11.854 C 11.68 12.027 11.411 12.046 11.216 11.911 L 11.146 11.854 L 6 6.707 L 0.854 11.854 C 0.68 12.027 0.411 12.046 0.216 11.911 L 0.146 11.854 C -0.027 11.68 -0.046 11.411 0.089 11.216 L 0.146 11.146 L 5.293 6 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
                </svg>
              </div>
            </div>
          </div>
        </div>
        )}
      </div>
      {props.body && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        gap: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 14,
          lineHeight: "20px",
          color: "var(--neutral-foreground-1-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.bodyText}</span>
        {props.checkbox && (
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            width: 24,
            display: "flex",
            flexDirection: "row",
            padding: "8px 8px 8px 8px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
            paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
            paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
            flexShrink: 0,
          }}>
            <div style={{
              position: "relative",
              width: 16,
              height: 16,
              borderRadius: 2,
              borderTop: "1px solid var(--neutral-stroke-accessible-rest)",
              borderRight: "1px solid var(--neutral-stroke-accessible-rest)",
              borderBottom: "1px solid var(--neutral-stroke-accessible-rest)",
              borderLeft: "1px solid var(--neutral-stroke-accessible-rest)",
              flexShrink: 0,
            }} />
          </div>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            padding: "6px 8px 6px 0px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
            paddingRight: "calc(var(--spacing-vertical-s-2) * 1px)",
            paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
            flexShrink: 0,
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: "calc(var(--spacing-horizontal-xs) * 1px)",
              alignItems: "flex-end",
              flexWrap: "nowrap",
              flexShrink: 0,
            }}>
              <span style={{
                position: "relative",
                fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
                fontWeight: 400,
                fontSize: 14,
                whiteSpace: "nowrap",
                lineHeight: "20px",
                color: "var(--neutral-foreground-1-rest)",
                flexShrink: 0,
              }}>Don’t show this message again</span>
            </div>
          </div>
        </div>
        )}
      </div>
      )}
      {props.footer && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        padding: "4px 0px 4px 0px",
        justifyContent: "space-between",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-horizontal-none-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 116,
          height: 32,
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
          flexShrink: 0,
        }}>
          {props.tertiaryButton && (
          <div style={{
            position: "relative",
            minWidth: 96,
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
            paddingTop: "max(0px, calc(var(--spacing-vertical-snudge-2) * 1px - 1px))",
            paddingRight: "max(0px, calc(var(--spacing-horizontal-m-2) * 1px - 1px))",
            paddingBottom: "max(0px, calc(var(--spacing-vertical-snudge-2) * 1px - 1px))",
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
                paddingTop: "calc(var(--typography-body-1-strong-offset) * 0.5px)", paddingBottom: "calc(var(--typography-body-1-strong-offset) * 0.5px)",
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
                }}>{props.tertiaryLabel ?? "Tertiary action"}</span>
              </div>
            </div>
          </div>
          )}
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
          flexShrink: 0,
        }}>
          <div style={{
            position: "relative",
            minWidth: 96,
            borderRadius: 4,
            backgroundColor: "var(--brand-background-1-rest)",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
            padding: "6px 12px 6px 12px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
            paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
            paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
            flexShrink: 0,
          }}>
            <div style={{
              position: "relative",
              height: 20,
              display: "flex",
              flexDirection: "row",
              gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
              padding: "0px 0px 0px 0px",
              justifyContent: "center",
              alignItems: "center",
              flexWrap: "nowrap",
              boxSizing: "border-box",
              flexShrink: 0,
            }}>
              <div style={{
                position: "relative",
                display: "flex",
                flexDirection: "row",
                alignItems: "flex-start",
                flexWrap: "nowrap",
                flexShrink: 0,
                alignSelf: "stretch",
              }}>
                <span style={{
                  position: "relative",
                  fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
                  fontWeight: 600,
                  fontSize: 14,
                  whiteSpace: "nowrap",
                  lineHeight: "20px",
                  color: "var(--neutral-foreground-on-brand-rest)",
                  flexShrink: 0,
                  alignSelf: "stretch",
                }}>{props.primaryLabel ?? "Take action"}</span>
              </div>
            </div>
          </div>
          {props.secondaryButton && (
          <div style={{
            position: "relative",
            minWidth: 96,
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
            paddingTop: "max(0px, calc(var(--spacing-vertical-snudge-2) * 1px - 1px))",
            paddingRight: "max(0px, calc(var(--spacing-horizontal-m-2) * 1px - 1px))",
            paddingBottom: "max(0px, calc(var(--spacing-vertical-snudge-2) * 1px - 1px))",
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
                }}>{props.secondaryLabel ?? "Different action"}</span>
              </div>
            </div>
          </div>
          )}
        </div>
      </div>
      )}
    </div>
  );
  const __body2 = () => (
    <div className={props.className} style={{
      width: 320,
      minWidth: 320,
      maxWidth: 479,
      maxHeight: null,
      overflow: "hidden",
      borderRadius: 8,
      backgroundColor: "var(--neutral-background-1-rest)",
      boxShadow: "var(--shadow-64)",
      display: "flex",
      flexDirection: "column",
      gap: "calc(var(--spacing-vertical-s-2) * 1px)",
      padding: "24px 24px 24px 24px",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-xxl) * 1px)",
      paddingTop: "calc(var(--spacing-horizontal-xxl) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xxl) * 1px)",
      paddingBottom: "calc(var(--spacing-horizontal-xxl) * 1px)",
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
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-horizontal-none-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 20,
          lineHeight: "28px",
          color: "var(--neutral-foreground-1-rest)",
          flexGrow: 1,
        }}>{props.headerText}</span>
        {props.dismiss && (
        <div style={{
          position: "relative",
          width: 28,
          height: 28,
          display: "flex",
          flexDirection: "row",
          padding: "0px 4px 0px 4px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingLeft: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
          flexShrink: 0,
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
                  color: "var(--neutral-foreground-1-rest)",
                }}>
                  <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 6 5.293 L 11.146 0.146 C 11.32 -0.027 11.589 -0.046 11.784 0.089 L 11.854 0.146 C 12.027 0.32 12.046 0.589 11.911 0.784 L 11.854 0.854 L 6.707 6 L 11.854 11.146 C 12.027 11.32 12.046 11.589 11.911 11.784 L 11.854 11.854 C 11.68 12.027 11.411 12.046 11.216 11.911 L 11.146 11.854 L 6 6.707 L 0.854 11.854 C 0.68 12.027 0.411 12.046 0.216 11.911 L 0.146 11.854 C -0.027 11.68 -0.046 11.411 0.089 11.216 L 0.146 11.146 L 5.293 6 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
                </svg>
              </div>
            </div>
          </div>
        </div>
        )}
      </div>
      {props.body && (
      <div style={{
        position: "relative",
        height: 120,
        backgroundColor: "var(--brand-background-2-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "absolute",
          left: 53,
          top: 53,
          width: 164,
          height: 14,
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 10,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "14px",
          color: "var(--brand-foreground-1-rest)",
        }}>SWAP WITH CONTENT COMPONENT</span>
      </div>
      )}
      {props.footer && (
      <div style={{
        position: "relative",
        height: 76,
        display: "flex",
        flexDirection: "column",
        gap: "calc(var(--spacing-vertical-s-2) * 1px)",
        padding: "4px 0px 4px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-horizontal-none-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.tertiaryButton && (
        <div style={{
          position: "relative",
          minWidth: 96,
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
          paddingTop: "max(0px, calc(var(--spacing-vertical-snudge-2) * 1px - 1px))",
          paddingRight: "max(0px, calc(var(--spacing-horizontal-m-2) * 1px - 1px))",
          paddingBottom: "max(0px, calc(var(--spacing-vertical-snudge-2) * 1px - 1px))",
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
              }}>{props.tertiaryLabel ?? "Tertiary action"}</span>
            </div>
          </div>
        </div>
        )}
        <div style={{
          position: "relative",
          minWidth: 96,
          borderRadius: 4,
          backgroundColor: "var(--brand-background-1-rest)",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          padding: "6px 12px 6px 12px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            height: 20,
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
            padding: "0px 0px 0px 0px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            flexShrink: 0,
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              alignItems: "flex-start",
              flexWrap: "nowrap",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>
              <span style={{
                position: "relative",
                fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
                fontWeight: 600,
                fontSize: 14,
                whiteSpace: "nowrap",
                lineHeight: "20px",
                color: "var(--neutral-foreground-on-brand-rest)",
                flexShrink: 0,
                alignSelf: "stretch",
              }}>{props.primaryLabel ?? "Take action"}</span>
            </div>
          </div>
        </div>
        {props.secondaryButton && (
        <div style={{
          position: "relative",
          minWidth: 96,
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
          paddingTop: "max(0px, calc(var(--spacing-vertical-snudge-2) * 1px - 1px))",
          paddingRight: "max(0px, calc(var(--spacing-horizontal-m-2) * 1px - 1px))",
          paddingBottom: "max(0px, calc(var(--spacing-vertical-snudge-2) * 1px - 1px))",
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
              }}>{props.secondaryLabel ?? "Different action"}</span>
            </div>
          </div>
        </div>
        )}
      </div>
      )}
    </div>
  );
  const __body3 = () => (
    <div className={props.className} style={{
      width: 320,
      minWidth: 320,
      maxWidth: 479,
      maxHeight: null,
      overflow: "hidden",
      borderRadius: 8,
      backgroundColor: "var(--neutral-background-1-rest)",
      boxShadow: "var(--shadow-64)",
      display: "flex",
      flexDirection: "column",
      gap: "calc(var(--spacing-vertical-s-2) * 1px)",
      padding: "24px 24px 24px 24px",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-xxl) * 1px)",
      paddingTop: "calc(var(--spacing-horizontal-xxl) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xxl) * 1px)",
      paddingBottom: "calc(var(--spacing-horizontal-xxl) * 1px)",
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
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-horizontal-none-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 20,
          lineHeight: "28px",
          color: "var(--neutral-foreground-1-rest)",
          flexGrow: 1,
        }}>{props.headerText}</span>
        {props.dismiss && (
        <div style={{
          position: "relative",
          width: 28,
          height: 28,
          display: "flex",
          flexDirection: "row",
          padding: "0px 4px 0px 4px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          gap: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingLeft: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
          flexShrink: 0,
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
                  color: "var(--neutral-foreground-1-rest)",
                }}>
                  <path d={"M 0.089 0.216 L 0.146 0.146 C 0.32 -0.027 0.589 -0.046 0.784 0.089 L 0.854 0.146 L 6 5.293 L 11.146 0.146 C 11.32 -0.027 11.589 -0.046 11.784 0.089 L 11.854 0.146 C 12.027 0.32 12.046 0.589 11.911 0.784 L 11.854 0.854 L 6.707 6 L 11.854 11.146 C 12.027 11.32 12.046 11.589 11.911 11.784 L 11.854 11.854 C 11.68 12.027 11.411 12.046 11.216 11.911 L 11.146 11.854 L 6 6.707 L 0.854 11.854 C 0.68 12.027 0.411 12.046 0.216 11.911 L 0.146 11.854 C -0.027 11.68 -0.046 11.411 0.089 11.216 L 0.146 11.146 L 5.293 6 L 0.146 0.854 C -0.027 0.68 -0.046 0.411 0.089 0.216 L 0.146 0.146 L 0.089 0.216 Z"} fill="currentColor" fillRule="nonzero" />
                </svg>
              </div>
            </div>
          </div>
        </div>
        )}
      </div>
      {props.body && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        gap: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 14,
          lineHeight: "20px",
          color: "var(--neutral-foreground-1-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.bodyText}</span>
        {props.checkbox && (
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            width: 24,
            display: "flex",
            flexDirection: "row",
            padding: "8px 8px 8px 8px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
            paddingTop: "calc(var(--spacing-vertical-s-2) * 1px)",
            paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
            paddingBottom: "calc(var(--spacing-vertical-s-2) * 1px)",
            flexShrink: 0,
          }}>
            <div style={{
              position: "relative",
              width: 16,
              height: 16,
              borderRadius: 2,
              borderTop: "1px solid var(--neutral-stroke-accessible-rest)",
              borderRight: "1px solid var(--neutral-stroke-accessible-rest)",
              borderBottom: "1px solid var(--neutral-stroke-accessible-rest)",
              borderLeft: "1px solid var(--neutral-stroke-accessible-rest)",
              flexShrink: 0,
            }} />
          </div>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            padding: "6px 8px 6px 0px",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
            paddingRight: "calc(var(--spacing-vertical-s-2) * 1px)",
            paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
            flexShrink: 0,
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: "calc(var(--spacing-horizontal-xs) * 1px)",
              alignItems: "flex-end",
              flexWrap: "nowrap",
              flexShrink: 0,
            }}>
              <span style={{
                position: "relative",
                fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
                fontWeight: 400,
                fontSize: 14,
                whiteSpace: "nowrap",
                lineHeight: "20px",
                color: "var(--neutral-foreground-3-rest)",
                flexShrink: 0,
              }}>Don’t show this message again</span>
            </div>
          </div>
        </div>
        )}
      </div>
      )}
      {props.footer && (
      <div style={{
        position: "relative",
        height: 76,
        display: "flex",
        flexDirection: "column",
        gap: "calc(var(--spacing-vertical-s-2) * 1px)",
        padding: "4px 0px 4px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingTop: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
        paddingBottom: "calc(var(--spacing-horizontal-none-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.tertiaryButton && (
        <div style={{
          position: "relative",
          minWidth: 96,
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
          paddingTop: "max(0px, calc(var(--spacing-vertical-snudge-2) * 1px - 1px))",
          paddingRight: "max(0px, calc(var(--spacing-horizontal-m-2) * 1px - 1px))",
          paddingBottom: "max(0px, calc(var(--spacing-vertical-snudge-2) * 1px - 1px))",
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
              }}>{props.tertiaryLabel ?? "Tertiary action"}</span>
            </div>
          </div>
        </div>
        )}
        <div style={{
          position: "relative",
          minWidth: 96,
          borderRadius: 4,
          backgroundColor: "var(--brand-background-1-rest)",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
          padding: "6px 12px 6px 12px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            height: 20,
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
            padding: "0px 0px 0px 0px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            flexShrink: 0,
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              alignItems: "flex-start",
              flexWrap: "nowrap",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>
              <span style={{
                position: "relative",
                fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
                fontWeight: 600,
                fontSize: 14,
                whiteSpace: "nowrap",
                lineHeight: "20px",
                color: "var(--neutral-foreground-on-brand-rest)",
                flexShrink: 0,
                alignSelf: "stretch",
              }}>{props.primaryLabel ?? "Take action"}</span>
            </div>
          </div>
        </div>
        {props.secondaryButton && (
        <div style={{
          position: "relative",
          minWidth: 96,
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
          paddingTop: "max(0px, calc(var(--spacing-vertical-snudge-2) * 1px - 1px))",
          paddingRight: "max(0px, calc(var(--spacing-horizontal-m-2) * 1px - 1px))",
          paddingBottom: "max(0px, calc(var(--spacing-vertical-snudge-2) * 1px - 1px))",
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
              }}>{props.secondaryLabel ?? "Different action"}</span>
            </div>
          </div>
        </div>
        )}
      </div>
      )}
    </div>
  );
  const __impls = {
    // figma: Size=600px, Layout=Placeholder
    "size=600px|layout=placeholder": __body0,
    // figma: Size=600px, Layout=Text
    "size=600px|layout=text": __body1,
    // figma: Size=320px, Layout=Placeholder
    "size=320px|layout=placeholder": __body2,
    // figma: Size=320px, Layout=Text
    "size=320px|layout=text": __body3,
  };
  return (__impls[__vkey(props)] ?? __body1)();
}
export default Dialog;
