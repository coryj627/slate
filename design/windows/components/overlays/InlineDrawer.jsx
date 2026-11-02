import { __slateNorm } from '../internal/slateProps.jsx';
import { DEPRECATEDDivider } from '../internal/DEPRECATEDDivider.jsx';
import { Placeholder3 } from '../internal/Placeholder3.jsx';

// figma node: 9170:8046 .Inline drawer (9 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "size=" + __venc(p.size) + '|' + "verticalDivider=" + __venc(p.verticalDivider);

export function InlineDrawer(_p = {}) {
  _p = __slateNorm(_p, {"size":["small (default)","md","lg"],"verticalDivider":["none","left","right"]});
  const props = { ..._p, footer: _p.footer ?? true, size: _p.size ?? "small (default)", verticalDivider: _p.verticalDivider ?? "none" };
  const __body0 = () => (
    <div className={props.className} style={{
      width: 940,
      height: 952,
      minWidth: 592,
      maxWidth: 940,
      maxHeight: null,
      backgroundColor: "var(--neutral-background-1-rest)",
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        backgroundColor: "var(--neutral-background-1-rest)",
        display: "flex",
        flexDirection: "column",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
          padding: "24px 16px 12px 24px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxl) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xxl) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-l-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-m-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
            alignItems: "center",
            flexWrap: "nowrap",
            flexGrow: 1,
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
              alignItems: "center",
              flexWrap: "nowrap",
              flexGrow: 1,
            }}>
              <span style={{
                position: "relative",
                fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
                fontWeight: 600,
                fontSize: 20,
                lineHeight: "26px",
                color: "var(--neutral-foreground-1-rest)",
                flexGrow: 1,
              }}>Title</span>
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
          </div>
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          alignItems: "center",
          flexWrap: "nowrap",
          paddingLeft: "calc(var(--horizontal-padding) * 1px)",
          paddingRight: "calc(var(--horizontal-padding) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            height: 1,
            backgroundColor: "var(--stroke-color)",
            flexGrow: 1,
          }} />
          <div style={{
            position: "relative",
            height: 1,
            backgroundColor: "var(--stroke-color)",
            flexGrow: 1,
          }} />
        </div>
      </div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        padding: "0px 24px 0px 24px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xxl) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xxl) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            flexGrow: 1,
            alignSelf: "stretch",
            width: "auto",
            height: "auto",
          }}>{props.contentSwap ?? <Placeholder3 slot={"1"} />}</div>
      </div>
      {props.footer && (
      <div style={{
        position: "relative",
        borderRadius: 24,
        backgroundColor: "var(--neutral-background-1-rest)",
        display: "flex",
        flexDirection: "column",
        gap: "calc(var(--spacing-vertical-mnudge-2) * 1px)",
        padding: "16px 24px 24px 24px",
        justifyContent: "flex-end",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xxl) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-l-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xxl) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xxl) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
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
            width: 74,
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
                }}>Primary</span>
              </div>
            </div>
          </div>
          <div style={{
            position: "relative",
            width: 91,
            borderRadius: 4,
            backgroundColor: "var(--neutral-background-1-rest)",
            borderTop: "1px solid var(--neutral-stroke-1-rest)",
            borderRight: "1px solid var(--neutral-stroke-1-rest)",
            borderBottom: "1px solid var(--neutral-stroke-1-rest)",
            borderLeft: "1px solid var(--neutral-stroke-1-rest)",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
            padding: "5px 12px 5px 12px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
            paddingTop: "max(0px, calc(var(--spacing-vertical-snudge-2) * 1px - 1px))",
            paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
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
                }}>Secondary</span>
              </div>
            </div>
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
                <svg width={12.500} height={2.500} viewBox="0 0 12.500 2.500" fill="none" style={{
                  position: "absolute",
                  left: 3.75,
                  top: 8.75,
                  width: 12.5,
                  height: 2.5,
                  color: "var(--neutral-foreground-2-rest)",
                }}>
                  <path d={"M 2.5 1.25 C 2.5 1.94 1.94 2.5 1.25 2.5 C 0.56 2.5 0 1.94 0 1.25 C 0 0.56 0.56 0 1.25 0 C 1.94 0 2.5 0.56 2.5 1.25 Z M 7.5 1.25 C 7.5 1.94 6.94 2.5 6.25 2.5 C 5.56 2.5 5 1.94 5 1.25 C 5 0.56 5.56 0 6.25 0 C 6.94 0 7.5 0.56 7.5 1.25 Z M 11.25 2.5 C 11.94 2.5 12.5 1.94 12.5 1.25 C 12.5 0.56 11.94 0 11.25 0 C 10.56 0 10 0.56 10 1.25 C 10 1.94 10.56 2.5 11.25 2.5 Z"} fill="currentColor" fillRule="nonzero" />
                </svg>
              </div>
            </div>
          </div>
        </div>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 940,
          display: "flex",
          flexDirection: "row",
          alignItems: "center",
          flexWrap: "nowrap",
          paddingLeft: "calc(var(--horizontal-padding) * 1px)",
          paddingRight: "calc(var(--horizontal-padding) * 1px)",
        }}>
          <div style={{
            position: "relative",
            height: 1,
            backgroundColor: "var(--stroke-color)",
            flexGrow: 1,
          }} />
          <div style={{
            position: "relative",
            height: 1,
            backgroundColor: "var(--stroke-color)",
            flexGrow: 1,
          }} />
        </div>
      </div>
      )}
      <DEPRECATEDDivider
        style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 952,
          transform: "matrix(0,1,-1,0,941,0)",
          transformOrigin: "0 0",
          height: 1,
        }}
        layout={"default"}
        style2={"default"}
        inset={"block (default)"}
        contentAlign={"center (default)"}
        orientation={"horizontal"}
      />
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: 592,
      height: 952,
      minWidth: 320,
      maxWidth: 592,
      maxHeight: null,
      backgroundColor: "var(--neutral-background-1-rest)",
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        backgroundColor: "var(--neutral-background-1-rest)",
        display: "flex",
        flexDirection: "column",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
          padding: "24px 16px 12px 24px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxl) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xxl) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-l-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-m-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
            alignItems: "center",
            flexWrap: "nowrap",
            flexGrow: 1,
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
              alignItems: "center",
              flexWrap: "nowrap",
              flexGrow: 1,
            }}>
              <span style={{
                position: "relative",
                fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
                fontWeight: 600,
                fontSize: 20,
                lineHeight: "26px",
                color: "var(--neutral-foreground-1-rest)",
                flexGrow: 1,
              }}>Title</span>
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
          </div>
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          alignItems: "center",
          flexWrap: "nowrap",
          paddingLeft: "calc(var(--horizontal-padding) * 1px)",
          paddingRight: "calc(var(--horizontal-padding) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            height: 1,
            backgroundColor: "var(--stroke-color)",
            flexGrow: 1,
          }} />
          <div style={{
            position: "relative",
            height: 1,
            backgroundColor: "var(--stroke-color)",
            flexGrow: 1,
          }} />
        </div>
      </div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        padding: "0px 24px 0px 24px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xxl) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xxl) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            flexGrow: 1,
            alignSelf: "stretch",
            width: "auto",
            height: "auto",
          }}>{props.contentSwap ?? <Placeholder3 slot={"1"} />}</div>
      </div>
      {props.footer && (
      <div style={{
        position: "relative",
        borderRadius: 24,
        backgroundColor: "var(--neutral-background-1-rest)",
        display: "flex",
        flexDirection: "column",
        gap: "calc(var(--spacing-vertical-mnudge-2) * 1px)",
        padding: "16px 24px 24px 24px",
        justifyContent: "flex-end",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xxl) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-l-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xxl) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xxl) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
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
            width: 74,
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
                }}>Primary</span>
              </div>
            </div>
          </div>
          <div style={{
            position: "relative",
            width: 91,
            borderRadius: 4,
            backgroundColor: "var(--neutral-background-1-rest)",
            borderTop: "1px solid var(--neutral-stroke-1-rest)",
            borderRight: "1px solid var(--neutral-stroke-1-rest)",
            borderBottom: "1px solid var(--neutral-stroke-1-rest)",
            borderLeft: "1px solid var(--neutral-stroke-1-rest)",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
            padding: "5px 12px 5px 12px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
            paddingTop: "max(0px, calc(var(--spacing-vertical-snudge-2) * 1px - 1px))",
            paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
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
                }}>Secondary</span>
              </div>
            </div>
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
                <svg width={12.500} height={2.500} viewBox="0 0 12.500 2.500" fill="none" style={{
                  position: "absolute",
                  left: 3.75,
                  top: 8.75,
                  width: 12.5,
                  height: 2.5,
                  color: "var(--neutral-foreground-2-rest)",
                }}>
                  <path d={"M 2.5 1.25 C 2.5 1.94 1.94 2.5 1.25 2.5 C 0.56 2.5 0 1.94 0 1.25 C 0 0.56 0.56 0 1.25 0 C 1.94 0 2.5 0.56 2.5 1.25 Z M 7.5 1.25 C 7.5 1.94 6.94 2.5 6.25 2.5 C 5.56 2.5 5 1.94 5 1.25 C 5 0.56 5.56 0 6.25 0 C 6.94 0 7.5 0.56 7.5 1.25 Z M 11.25 2.5 C 11.94 2.5 12.5 1.94 12.5 1.25 C 12.5 0.56 11.94 0 11.25 0 C 10.56 0 10 0.56 10 1.25 C 10 1.94 10.56 2.5 11.25 2.5 Z"} fill="currentColor" fillRule="nonzero" />
                </svg>
              </div>
            </div>
          </div>
        </div>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 592,
          display: "flex",
          flexDirection: "row",
          alignItems: "center",
          flexWrap: "nowrap",
          paddingLeft: "calc(var(--horizontal-padding) * 1px)",
          paddingRight: "calc(var(--horizontal-padding) * 1px)",
        }}>
          <div style={{
            position: "relative",
            height: 1,
            backgroundColor: "var(--stroke-color)",
            flexGrow: 1,
          }} />
          <div style={{
            position: "relative",
            height: 1,
            backgroundColor: "var(--stroke-color)",
            flexGrow: 1,
          }} />
        </div>
      </div>
      )}
      <DEPRECATEDDivider
        style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 952,
          transform: "matrix(0,1,-1,0,593,0)",
          transformOrigin: "0 0",
          height: 1,
        }}
        layout={"default"}
        style2={"default"}
        inset={"block (default)"}
        contentAlign={"center (default)"}
        orientation={"horizontal"}
      />
    </div>
  );
  const __body2 = () => (
    <div className={props.className} style={{
      width: 320,
      height: 952,
      minWidth: 82,
      maxWidth: 320,
      maxHeight: null,
      backgroundColor: "var(--neutral-background-1-rest)",
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        backgroundColor: "var(--neutral-background-1-rest)",
        display: "flex",
        flexDirection: "column",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
          padding: "24px 16px 12px 24px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxl) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xxl) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-l-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-m-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
            alignItems: "center",
            flexWrap: "nowrap",
            flexGrow: 1,
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
              alignItems: "center",
              flexWrap: "nowrap",
              flexGrow: 1,
            }}>
              <span style={{
                position: "relative",
                fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
                fontWeight: 600,
                fontSize: 20,
                lineHeight: "26px",
                color: "var(--neutral-foreground-1-rest)",
                flexGrow: 1,
              }}>Title</span>
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
          </div>
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          alignItems: "center",
          flexWrap: "nowrap",
          paddingLeft: "calc(var(--horizontal-padding) * 1px)",
          paddingRight: "calc(var(--horizontal-padding) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            height: 1,
            backgroundColor: "var(--stroke-color)",
            flexGrow: 1,
          }} />
          <div style={{
            position: "relative",
            height: 1,
            backgroundColor: "var(--stroke-color)",
            flexGrow: 1,
          }} />
        </div>
      </div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        padding: "0px 24px 0px 24px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xxl) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xxl) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            flexGrow: 1,
            alignSelf: "stretch",
            width: "auto",
            height: "auto",
          }}>{props.contentSwap ?? <Placeholder3 slot={"1"} />}</div>
      </div>
      {props.footer && (
      <div style={{
        position: "relative",
        borderRadius: 24,
        backgroundColor: "var(--neutral-background-1-rest)",
        display: "flex",
        flexDirection: "column",
        gap: "calc(var(--spacing-vertical-mnudge-2) * 1px)",
        padding: "16px 24px 24px 24px",
        justifyContent: "flex-end",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xxl) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-l-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xxl) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xxl) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
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
            width: 74,
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
                }}>Primary</span>
              </div>
            </div>
          </div>
          <div style={{
            position: "relative",
            width: 91,
            borderRadius: 4,
            backgroundColor: "var(--neutral-background-1-rest)",
            borderTop: "1px solid var(--neutral-stroke-1-rest)",
            borderRight: "1px solid var(--neutral-stroke-1-rest)",
            borderBottom: "1px solid var(--neutral-stroke-1-rest)",
            borderLeft: "1px solid var(--neutral-stroke-1-rest)",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
            padding: "5px 12px 5px 12px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
            paddingTop: "max(0px, calc(var(--spacing-vertical-snudge-2) * 1px - 1px))",
            paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
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
                }}>Secondary</span>
              </div>
            </div>
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
                <svg width={12.500} height={2.500} viewBox="0 0 12.500 2.500" fill="none" style={{
                  position: "absolute",
                  left: 3.75,
                  top: 8.75,
                  width: 12.5,
                  height: 2.5,
                  color: "var(--neutral-foreground-2-rest)",
                }}>
                  <path d={"M 2.5 1.25 C 2.5 1.94 1.94 2.5 1.25 2.5 C 0.56 2.5 0 1.94 0 1.25 C 0 0.56 0.56 0 1.25 0 C 1.94 0 2.5 0.56 2.5 1.25 Z M 7.5 1.25 C 7.5 1.94 6.94 2.5 6.25 2.5 C 5.56 2.5 5 1.94 5 1.25 C 5 0.56 5.56 0 6.25 0 C 6.94 0 7.5 0.56 7.5 1.25 Z M 11.25 2.5 C 11.94 2.5 12.5 1.94 12.5 1.25 C 12.5 0.56 11.94 0 11.25 0 C 10.56 0 10 0.56 10 1.25 C 10 1.94 10.56 2.5 11.25 2.5 Z"} fill="currentColor" fillRule="nonzero" />
                </svg>
              </div>
            </div>
          </div>
        </div>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 320,
          display: "flex",
          flexDirection: "row",
          alignItems: "center",
          flexWrap: "nowrap",
          paddingLeft: "calc(var(--horizontal-padding) * 1px)",
          paddingRight: "calc(var(--horizontal-padding) * 1px)",
        }}>
          <div style={{
            position: "relative",
            height: 1,
            backgroundColor: "var(--stroke-color)",
            flexGrow: 1,
          }} />
          <div style={{
            position: "relative",
            height: 1,
            backgroundColor: "var(--stroke-color)",
            flexGrow: 1,
          }} />
        </div>
      </div>
      )}
      <DEPRECATEDDivider
        style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 952,
          transform: "matrix(0,1,-1,0,321,0)",
          transformOrigin: "0 0",
          height: 1,
        }}
        layout={"default"}
        style2={"default"}
        inset={"block (default)"}
        contentAlign={"center (default)"}
        orientation={"horizontal"}
      />
    </div>
  );
  const __body3 = () => (
    <div className={props.className} style={{
      width: 940,
      height: 952,
      minWidth: 592,
      maxWidth: 940,
      maxHeight: null,
      backgroundColor: "var(--neutral-background-1-rest)",
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        backgroundColor: "var(--neutral-background-1-rest)",
        display: "flex",
        flexDirection: "column",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
          padding: "24px 16px 12px 24px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxl) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xxl) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-l-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-m-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
            alignItems: "center",
            flexWrap: "nowrap",
            flexGrow: 1,
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
              alignItems: "center",
              flexWrap: "nowrap",
              flexGrow: 1,
            }}>
              <span style={{
                position: "relative",
                fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
                fontWeight: 600,
                fontSize: 20,
                lineHeight: "26px",
                color: "var(--neutral-foreground-1-rest)",
                flexGrow: 1,
              }}>Title</span>
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
          </div>
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          alignItems: "center",
          flexWrap: "nowrap",
          paddingLeft: "calc(var(--horizontal-padding) * 1px)",
          paddingRight: "calc(var(--horizontal-padding) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            height: 1,
            backgroundColor: "var(--stroke-color)",
            flexGrow: 1,
          }} />
          <div style={{
            position: "relative",
            height: 1,
            backgroundColor: "var(--stroke-color)",
            flexGrow: 1,
          }} />
        </div>
      </div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        padding: "0px 24px 0px 24px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xxl) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xxl) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            flexGrow: 1,
            alignSelf: "stretch",
            width: "auto",
            height: "auto",
          }}>{props.contentSwap ?? <Placeholder3 slot={"1"} />}</div>
      </div>
      {props.footer && (
      <div style={{
        position: "relative",
        borderRadius: 24,
        backgroundColor: "var(--neutral-background-1-rest)",
        display: "flex",
        flexDirection: "column",
        gap: "calc(var(--spacing-vertical-mnudge-2) * 1px)",
        padding: "16px 24px 24px 24px",
        justifyContent: "flex-end",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xxl) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-l-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xxl) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xxl) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
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
            width: 74,
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
                }}>Primary</span>
              </div>
            </div>
          </div>
          <div style={{
            position: "relative",
            width: 91,
            borderRadius: 4,
            backgroundColor: "var(--neutral-background-1-rest)",
            borderTop: "1px solid var(--neutral-stroke-1-rest)",
            borderRight: "1px solid var(--neutral-stroke-1-rest)",
            borderBottom: "1px solid var(--neutral-stroke-1-rest)",
            borderLeft: "1px solid var(--neutral-stroke-1-rest)",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
            padding: "5px 12px 5px 12px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
            paddingTop: "max(0px, calc(var(--spacing-vertical-snudge-2) * 1px - 1px))",
            paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
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
                }}>Secondary</span>
              </div>
            </div>
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
                <svg width={12.500} height={2.500} viewBox="0 0 12.500 2.500" fill="none" style={{
                  position: "absolute",
                  left: 3.75,
                  top: 8.75,
                  width: 12.5,
                  height: 2.5,
                  color: "var(--neutral-foreground-2-rest)",
                }}>
                  <path d={"M 2.5 1.25 C 2.5 1.94 1.94 2.5 1.25 2.5 C 0.56 2.5 0 1.94 0 1.25 C 0 0.56 0.56 0 1.25 0 C 1.94 0 2.5 0.56 2.5 1.25 Z M 7.5 1.25 C 7.5 1.94 6.94 2.5 6.25 2.5 C 5.56 2.5 5 1.94 5 1.25 C 5 0.56 5.56 0 6.25 0 C 6.94 0 7.5 0.56 7.5 1.25 Z M 11.25 2.5 C 11.94 2.5 12.5 1.94 12.5 1.25 C 12.5 0.56 11.94 0 11.25 0 C 10.56 0 10 0.56 10 1.25 C 10 1.94 10.56 2.5 11.25 2.5 Z"} fill="currentColor" fillRule="nonzero" />
                </svg>
              </div>
            </div>
          </div>
        </div>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 940,
          display: "flex",
          flexDirection: "row",
          alignItems: "center",
          flexWrap: "nowrap",
          paddingLeft: "calc(var(--horizontal-padding) * 1px)",
          paddingRight: "calc(var(--horizontal-padding) * 1px)",
        }}>
          <div style={{
            position: "relative",
            height: 1,
            backgroundColor: "var(--stroke-color)",
            flexGrow: 1,
          }} />
          <div style={{
            position: "relative",
            height: 1,
            backgroundColor: "var(--stroke-color)",
            flexGrow: 1,
          }} />
        </div>
      </div>
      )}
      <DEPRECATEDDivider
        style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 952,
          transform: "matrix(0,1,-1,0,0,0)",
          transformOrigin: "0 0",
          height: 1,
        }}
        layout={"default"}
        style2={"default"}
        inset={"block (default)"}
        contentAlign={"center (default)"}
        orientation={"horizontal"}
      />
    </div>
  );
  const __body4 = () => (
    <div className={props.className} style={{
      width: 592,
      height: 952,
      minWidth: 320,
      maxWidth: 592,
      maxHeight: null,
      backgroundColor: "var(--neutral-background-1-rest)",
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        backgroundColor: "var(--neutral-background-1-rest)",
        display: "flex",
        flexDirection: "column",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
          padding: "24px 16px 12px 24px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxl) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xxl) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-l-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-m-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
            alignItems: "center",
            flexWrap: "nowrap",
            flexGrow: 1,
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
              alignItems: "center",
              flexWrap: "nowrap",
              flexGrow: 1,
            }}>
              <span style={{
                position: "relative",
                fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
                fontWeight: 600,
                fontSize: 20,
                lineHeight: "26px",
                color: "var(--neutral-foreground-1-rest)",
                flexGrow: 1,
              }}>Title</span>
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
          </div>
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          alignItems: "center",
          flexWrap: "nowrap",
          paddingLeft: "calc(var(--horizontal-padding) * 1px)",
          paddingRight: "calc(var(--horizontal-padding) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            height: 1,
            backgroundColor: "var(--stroke-color)",
            flexGrow: 1,
          }} />
          <div style={{
            position: "relative",
            height: 1,
            backgroundColor: "var(--stroke-color)",
            flexGrow: 1,
          }} />
        </div>
      </div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        padding: "0px 24px 0px 24px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xxl) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xxl) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            flexGrow: 1,
            alignSelf: "stretch",
            width: "auto",
            height: "auto",
          }}>{props.contentSwap ?? <Placeholder3 slot={"1"} />}</div>
      </div>
      {props.footer && (
      <div style={{
        position: "relative",
        borderRadius: 24,
        backgroundColor: "var(--neutral-background-1-rest)",
        display: "flex",
        flexDirection: "column",
        gap: "calc(var(--spacing-vertical-mnudge-2) * 1px)",
        padding: "16px 24px 24px 24px",
        justifyContent: "flex-end",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xxl) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-l-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xxl) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xxl) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
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
            width: 74,
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
                }}>Primary</span>
              </div>
            </div>
          </div>
          <div style={{
            position: "relative",
            width: 91,
            borderRadius: 4,
            backgroundColor: "var(--neutral-background-1-rest)",
            borderTop: "1px solid var(--neutral-stroke-1-rest)",
            borderRight: "1px solid var(--neutral-stroke-1-rest)",
            borderBottom: "1px solid var(--neutral-stroke-1-rest)",
            borderLeft: "1px solid var(--neutral-stroke-1-rest)",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
            padding: "5px 12px 5px 12px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
            paddingTop: "max(0px, calc(var(--spacing-vertical-snudge-2) * 1px - 1px))",
            paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
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
                }}>Secondary</span>
              </div>
            </div>
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
                <svg width={12.500} height={2.500} viewBox="0 0 12.500 2.500" fill="none" style={{
                  position: "absolute",
                  left: 3.75,
                  top: 8.75,
                  width: 12.5,
                  height: 2.5,
                  color: "var(--neutral-foreground-2-rest)",
                }}>
                  <path d={"M 2.5 1.25 C 2.5 1.94 1.94 2.5 1.25 2.5 C 0.56 2.5 0 1.94 0 1.25 C 0 0.56 0.56 0 1.25 0 C 1.94 0 2.5 0.56 2.5 1.25 Z M 7.5 1.25 C 7.5 1.94 6.94 2.5 6.25 2.5 C 5.56 2.5 5 1.94 5 1.25 C 5 0.56 5.56 0 6.25 0 C 6.94 0 7.5 0.56 7.5 1.25 Z M 11.25 2.5 C 11.94 2.5 12.5 1.94 12.5 1.25 C 12.5 0.56 11.94 0 11.25 0 C 10.56 0 10 0.56 10 1.25 C 10 1.94 10.56 2.5 11.25 2.5 Z"} fill="currentColor" fillRule="nonzero" />
                </svg>
              </div>
            </div>
          </div>
        </div>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 592,
          display: "flex",
          flexDirection: "row",
          alignItems: "center",
          flexWrap: "nowrap",
          paddingLeft: "calc(var(--horizontal-padding) * 1px)",
          paddingRight: "calc(var(--horizontal-padding) * 1px)",
        }}>
          <div style={{
            position: "relative",
            height: 1,
            backgroundColor: "var(--stroke-color)",
            flexGrow: 1,
          }} />
          <div style={{
            position: "relative",
            height: 1,
            backgroundColor: "var(--stroke-color)",
            flexGrow: 1,
          }} />
        </div>
      </div>
      )}
      <DEPRECATEDDivider
        style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 952,
          transform: "matrix(0,1,-1,0,0,0)",
          transformOrigin: "0 0",
          height: 1,
        }}
        layout={"default"}
        style2={"default"}
        inset={"block (default)"}
        contentAlign={"center (default)"}
        orientation={"horizontal"}
      />
    </div>
  );
  const __body5 = () => (
    <div className={props.className} style={{
      width: 320,
      height: 952,
      minWidth: 82,
      maxWidth: 320,
      maxHeight: null,
      backgroundColor: "var(--neutral-background-1-rest)",
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        backgroundColor: "var(--neutral-background-1-rest)",
        display: "flex",
        flexDirection: "column",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
          padding: "24px 16px 12px 24px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxl) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xxl) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-l-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-m-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
            alignItems: "center",
            flexWrap: "nowrap",
            flexGrow: 1,
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
              alignItems: "center",
              flexWrap: "nowrap",
              flexGrow: 1,
            }}>
              <span style={{
                position: "relative",
                fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
                fontWeight: 600,
                fontSize: 20,
                lineHeight: "26px",
                color: "var(--neutral-foreground-1-rest)",
                flexGrow: 1,
              }}>Title</span>
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
          </div>
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          alignItems: "center",
          flexWrap: "nowrap",
          paddingLeft: "calc(var(--horizontal-padding) * 1px)",
          paddingRight: "calc(var(--horizontal-padding) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            height: 1,
            backgroundColor: "var(--stroke-color)",
            flexGrow: 1,
          }} />
          <div style={{
            position: "relative",
            height: 1,
            backgroundColor: "var(--stroke-color)",
            flexGrow: 1,
          }} />
        </div>
      </div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        padding: "0px 24px 0px 24px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xxl) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xxl) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            flexGrow: 1,
            alignSelf: "stretch",
            width: "auto",
            height: "auto",
          }}>{props.contentSwap ?? <Placeholder3 slot={"1"} />}</div>
      </div>
      {props.footer && (
      <div style={{
        position: "relative",
        borderRadius: 24,
        backgroundColor: "var(--neutral-background-1-rest)",
        display: "flex",
        flexDirection: "column",
        gap: "calc(var(--spacing-vertical-mnudge-2) * 1px)",
        padding: "16px 24px 24px 24px",
        justifyContent: "flex-end",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xxl) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-l-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xxl) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xxl) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
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
            width: 74,
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
                }}>Primary</span>
              </div>
            </div>
          </div>
          <div style={{
            position: "relative",
            width: 91,
            borderRadius: 4,
            backgroundColor: "var(--neutral-background-1-rest)",
            borderTop: "1px solid var(--neutral-stroke-1-rest)",
            borderRight: "1px solid var(--neutral-stroke-1-rest)",
            borderBottom: "1px solid var(--neutral-stroke-1-rest)",
            borderLeft: "1px solid var(--neutral-stroke-1-rest)",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
            padding: "5px 12px 5px 12px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
            paddingTop: "max(0px, calc(var(--spacing-vertical-snudge-2) * 1px - 1px))",
            paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
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
                }}>Secondary</span>
              </div>
            </div>
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
                <svg width={12.500} height={2.500} viewBox="0 0 12.500 2.500" fill="none" style={{
                  position: "absolute",
                  left: 3.75,
                  top: 8.75,
                  width: 12.5,
                  height: 2.5,
                  color: "var(--neutral-foreground-2-rest)",
                }}>
                  <path d={"M 2.5 1.25 C 2.5 1.94 1.94 2.5 1.25 2.5 C 0.56 2.5 0 1.94 0 1.25 C 0 0.56 0.56 0 1.25 0 C 1.94 0 2.5 0.56 2.5 1.25 Z M 7.5 1.25 C 7.5 1.94 6.94 2.5 6.25 2.5 C 5.56 2.5 5 1.94 5 1.25 C 5 0.56 5.56 0 6.25 0 C 6.94 0 7.5 0.56 7.5 1.25 Z M 11.25 2.5 C 11.94 2.5 12.5 1.94 12.5 1.25 C 12.5 0.56 11.94 0 11.25 0 C 10.56 0 10 0.56 10 1.25 C 10 1.94 10.56 2.5 11.25 2.5 Z"} fill="currentColor" fillRule="nonzero" />
                </svg>
              </div>
            </div>
          </div>
        </div>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 320,
          display: "flex",
          flexDirection: "row",
          alignItems: "center",
          flexWrap: "nowrap",
          paddingLeft: "calc(var(--horizontal-padding) * 1px)",
          paddingRight: "calc(var(--horizontal-padding) * 1px)",
        }}>
          <div style={{
            position: "relative",
            height: 1,
            backgroundColor: "var(--stroke-color)",
            flexGrow: 1,
          }} />
          <div style={{
            position: "relative",
            height: 1,
            backgroundColor: "var(--stroke-color)",
            flexGrow: 1,
          }} />
        </div>
      </div>
      )}
      <DEPRECATEDDivider
        style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 952,
          transform: "matrix(0,1,-1,0,0,0)",
          transformOrigin: "0 0",
          height: 1,
        }}
        layout={"default"}
        style2={"default"}
        inset={"block (default)"}
        contentAlign={"center (default)"}
        orientation={"horizontal"}
      />
    </div>
  );
  const __body6 = () => (
    <div className={props.className} style={{
      width: 940,
      height: 952,
      minWidth: 592,
      maxWidth: 940,
      maxHeight: null,
      backgroundColor: "var(--neutral-background-1-rest)",
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        backgroundColor: "var(--neutral-background-1-rest)",
        display: "flex",
        flexDirection: "column",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
          padding: "24px 16px 12px 24px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxl) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xxl) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-l-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-m-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
            alignItems: "center",
            flexWrap: "nowrap",
            flexGrow: 1,
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
              alignItems: "center",
              flexWrap: "nowrap",
              flexGrow: 1,
            }}>
              <span style={{
                position: "relative",
                fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
                fontWeight: 600,
                fontSize: 20,
                lineHeight: "26px",
                color: "var(--neutral-foreground-1-rest)",
                flexGrow: 1,
              }}>Title</span>
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
          </div>
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          alignItems: "center",
          flexWrap: "nowrap",
          paddingLeft: "calc(var(--horizontal-padding) * 1px)",
          paddingRight: "calc(var(--horizontal-padding) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            height: 1,
            backgroundColor: "var(--stroke-color)",
            flexGrow: 1,
          }} />
          <div style={{
            position: "relative",
            height: 1,
            backgroundColor: "var(--stroke-color)",
            flexGrow: 1,
          }} />
        </div>
      </div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        padding: "0px 24px 0px 24px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xxl) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xxl) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            flexGrow: 1,
            alignSelf: "stretch",
            width: "auto",
            height: "auto",
          }}>{props.contentSwap ?? <Placeholder3 slot={"1"} />}</div>
      </div>
      {props.footer && (
      <div style={{
        position: "relative",
        borderRadius: 24,
        backgroundColor: "var(--neutral-background-1-rest)",
        display: "flex",
        flexDirection: "column",
        gap: "calc(var(--spacing-vertical-mnudge-2) * 1px)",
        padding: "16px 24px 24px 24px",
        justifyContent: "flex-end",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xxl) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-l-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xxl) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xxl) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
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
            width: 74,
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
                }}>Primary</span>
              </div>
            </div>
          </div>
          <div style={{
            position: "relative",
            width: 91,
            borderRadius: 4,
            backgroundColor: "var(--neutral-background-1-rest)",
            borderTop: "1px solid var(--neutral-stroke-1-rest)",
            borderRight: "1px solid var(--neutral-stroke-1-rest)",
            borderBottom: "1px solid var(--neutral-stroke-1-rest)",
            borderLeft: "1px solid var(--neutral-stroke-1-rest)",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
            padding: "5px 12px 5px 12px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
            paddingTop: "max(0px, calc(var(--spacing-vertical-snudge-2) * 1px - 1px))",
            paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
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
                }}>Secondary</span>
              </div>
            </div>
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
                <svg width={12.500} height={2.500} viewBox="0 0 12.500 2.500" fill="none" style={{
                  position: "absolute",
                  left: 3.75,
                  top: 8.75,
                  width: 12.5,
                  height: 2.5,
                  color: "var(--neutral-foreground-2-rest)",
                }}>
                  <path d={"M 2.5 1.25 C 2.5 1.94 1.94 2.5 1.25 2.5 C 0.56 2.5 0 1.94 0 1.25 C 0 0.56 0.56 0 1.25 0 C 1.94 0 2.5 0.56 2.5 1.25 Z M 7.5 1.25 C 7.5 1.94 6.94 2.5 6.25 2.5 C 5.56 2.5 5 1.94 5 1.25 C 5 0.56 5.56 0 6.25 0 C 6.94 0 7.5 0.56 7.5 1.25 Z M 11.25 2.5 C 11.94 2.5 12.5 1.94 12.5 1.25 C 12.5 0.56 11.94 0 11.25 0 C 10.56 0 10 0.56 10 1.25 C 10 1.94 10.56 2.5 11.25 2.5 Z"} fill="currentColor" fillRule="nonzero" />
                </svg>
              </div>
            </div>
          </div>
        </div>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 940,
          display: "flex",
          flexDirection: "row",
          alignItems: "center",
          flexWrap: "nowrap",
          paddingLeft: "calc(var(--horizontal-padding) * 1px)",
          paddingRight: "calc(var(--horizontal-padding) * 1px)",
        }}>
          <div style={{
            position: "relative",
            height: 1,
            backgroundColor: "var(--stroke-color)",
            flexGrow: 1,
          }} />
          <div style={{
            position: "relative",
            height: 1,
            backgroundColor: "var(--stroke-color)",
            flexGrow: 1,
          }} />
        </div>
      </div>
      )}
    </div>
  );
  const __body7 = () => (
    <div className={props.className} style={{
      width: 592,
      height: 952,
      minWidth: 320,
      maxWidth: 592,
      maxHeight: null,
      backgroundColor: "var(--neutral-background-1-rest)",
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        backgroundColor: "var(--neutral-background-1-rest)",
        display: "flex",
        flexDirection: "column",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
          padding: "24px 16px 12px 24px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxl) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xxl) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-l-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-m-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
            alignItems: "center",
            flexWrap: "nowrap",
            flexGrow: 1,
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
              alignItems: "center",
              flexWrap: "nowrap",
              flexGrow: 1,
            }}>
              <span style={{
                position: "relative",
                fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
                fontWeight: 600,
                fontSize: 20,
                lineHeight: "26px",
                color: "var(--neutral-foreground-1-rest)",
                flexGrow: 1,
              }}>Title</span>
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
          </div>
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          alignItems: "center",
          flexWrap: "nowrap",
          paddingLeft: "calc(var(--horizontal-padding) * 1px)",
          paddingRight: "calc(var(--horizontal-padding) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            height: 1,
            backgroundColor: "var(--stroke-color)",
            flexGrow: 1,
          }} />
          <div style={{
            position: "relative",
            height: 1,
            backgroundColor: "var(--stroke-color)",
            flexGrow: 1,
          }} />
        </div>
      </div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        padding: "0px 24px 0px 24px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xxl) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xxl) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            flexGrow: 1,
            alignSelf: "stretch",
            width: "auto",
            height: "auto",
          }}>{props.contentSwap ?? <Placeholder3 slot={"1"} />}</div>
      </div>
      {props.footer && (
      <div style={{
        position: "relative",
        borderRadius: 24,
        backgroundColor: "var(--neutral-background-1-rest)",
        display: "flex",
        flexDirection: "column",
        gap: "calc(var(--spacing-vertical-mnudge-2) * 1px)",
        padding: "16px 24px 24px 24px",
        justifyContent: "flex-end",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xxl) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-l-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xxl) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xxl) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
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
            width: 74,
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
                }}>Primary</span>
              </div>
            </div>
          </div>
          <div style={{
            position: "relative",
            width: 91,
            borderRadius: 4,
            backgroundColor: "var(--neutral-background-1-rest)",
            borderTop: "1px solid var(--neutral-stroke-1-rest)",
            borderRight: "1px solid var(--neutral-stroke-1-rest)",
            borderBottom: "1px solid var(--neutral-stroke-1-rest)",
            borderLeft: "1px solid var(--neutral-stroke-1-rest)",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
            padding: "5px 12px 5px 12px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
            paddingTop: "max(0px, calc(var(--spacing-vertical-snudge-2) * 1px - 1px))",
            paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
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
                }}>Secondary</span>
              </div>
            </div>
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
                <svg width={12.500} height={2.500} viewBox="0 0 12.500 2.500" fill="none" style={{
                  position: "absolute",
                  left: 3.75,
                  top: 8.75,
                  width: 12.5,
                  height: 2.5,
                  color: "var(--neutral-foreground-2-rest)",
                }}>
                  <path d={"M 2.5 1.25 C 2.5 1.94 1.94 2.5 1.25 2.5 C 0.56 2.5 0 1.94 0 1.25 C 0 0.56 0.56 0 1.25 0 C 1.94 0 2.5 0.56 2.5 1.25 Z M 7.5 1.25 C 7.5 1.94 6.94 2.5 6.25 2.5 C 5.56 2.5 5 1.94 5 1.25 C 5 0.56 5.56 0 6.25 0 C 6.94 0 7.5 0.56 7.5 1.25 Z M 11.25 2.5 C 11.94 2.5 12.5 1.94 12.5 1.25 C 12.5 0.56 11.94 0 11.25 0 C 10.56 0 10 0.56 10 1.25 C 10 1.94 10.56 2.5 11.25 2.5 Z"} fill="currentColor" fillRule="nonzero" />
                </svg>
              </div>
            </div>
          </div>
        </div>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 592,
          display: "flex",
          flexDirection: "row",
          alignItems: "center",
          flexWrap: "nowrap",
          paddingLeft: "calc(var(--horizontal-padding) * 1px)",
          paddingRight: "calc(var(--horizontal-padding) * 1px)",
        }}>
          <div style={{
            position: "relative",
            height: 1,
            backgroundColor: "var(--stroke-color)",
            flexGrow: 1,
          }} />
          <div style={{
            position: "relative",
            height: 1,
            backgroundColor: "var(--stroke-color)",
            flexGrow: 1,
          }} />
        </div>
      </div>
      )}
    </div>
  );
  const __body8 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 952,
      minWidth: 82,
      maxWidth: 320,
      maxHeight: null,
      backgroundColor: "var(--neutral-background-1-rest)",
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        backgroundColor: "var(--neutral-background-1-rest)",
        display: "flex",
        flexDirection: "column",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
          padding: "24px 16px 12px 24px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxl) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-xxl) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-l-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-m-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
            alignItems: "center",
            flexWrap: "nowrap",
            flexGrow: 1,
          }}>
            <div style={{
              position: "relative",
              display: "flex",
              flexDirection: "row",
              gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
              alignItems: "center",
              flexWrap: "nowrap",
              flexGrow: 1,
            }}>
              <span style={{
                position: "relative",
                fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
                fontWeight: 600,
                fontSize: 20,
                lineHeight: "26px",
                color: "var(--neutral-foreground-1-rest)",
                flexGrow: 1,
              }}>Title</span>
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
          </div>
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          alignItems: "center",
          flexWrap: "nowrap",
          paddingLeft: "calc(var(--horizontal-padding) * 1px)",
          paddingRight: "calc(var(--horizontal-padding) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            height: 1,
            backgroundColor: "var(--stroke-color)",
            flexGrow: 1,
          }} />
          <div style={{
            position: "relative",
            height: 1,
            backgroundColor: "var(--stroke-color)",
            flexGrow: 1,
          }} />
        </div>
      </div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        padding: "0px 24px 0px 24px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xxl) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xxl) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            flexGrow: 1,
            alignSelf: "stretch",
            width: "auto",
            height: "auto",
          }}>{props.contentSwap ?? <Placeholder3 slot={"1"} />}</div>
      </div>
      {props.footer && (
      <div style={{
        position: "relative",
        borderRadius: 24,
        backgroundColor: "var(--neutral-background-1-rest)",
        display: "flex",
        flexDirection: "column",
        gap: "calc(var(--spacing-vertical-mnudge-2) * 1px)",
        padding: "16px 24px 24px 24px",
        justifyContent: "flex-end",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-xxl) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-l-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-xxl) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xxl) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
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
            width: 74,
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
                }}>Primary</span>
              </div>
            </div>
          </div>
          <div style={{
            position: "relative",
            width: 91,
            borderRadius: 4,
            backgroundColor: "var(--neutral-background-1-rest)",
            borderTop: "1px solid var(--neutral-stroke-1-rest)",
            borderRight: "1px solid var(--neutral-stroke-1-rest)",
            borderBottom: "1px solid var(--neutral-stroke-1-rest)",
            borderLeft: "1px solid var(--neutral-stroke-1-rest)",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
            padding: "5px 12px 5px 12px",
            justifyContent: "center",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
            paddingTop: "max(0px, calc(var(--spacing-vertical-snudge-2) * 1px - 1px))",
            paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
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
                }}>Secondary</span>
              </div>
            </div>
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
                <svg width={12.500} height={2.500} viewBox="0 0 12.500 2.500" fill="none" style={{
                  position: "absolute",
                  left: 3.75,
                  top: 8.75,
                  width: 12.5,
                  height: 2.5,
                  color: "var(--neutral-foreground-2-rest)",
                }}>
                  <path d={"M 2.5 1.25 C 2.5 1.94 1.94 2.5 1.25 2.5 C 0.56 2.5 0 1.94 0 1.25 C 0 0.56 0.56 0 1.25 0 C 1.94 0 2.5 0.56 2.5 1.25 Z M 7.5 1.25 C 7.5 1.94 6.94 2.5 6.25 2.5 C 5.56 2.5 5 1.94 5 1.25 C 5 0.56 5.56 0 6.25 0 C 6.94 0 7.5 0.56 7.5 1.25 Z M 11.25 2.5 C 11.94 2.5 12.5 1.94 12.5 1.25 C 12.5 0.56 11.94 0 11.25 0 C 10.56 0 10 0.56 10 1.25 C 10 1.94 10.56 2.5 11.25 2.5 Z"} fill="currentColor" fillRule="nonzero" />
                </svg>
              </div>
            </div>
          </div>
        </div>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 320,
          display: "flex",
          flexDirection: "row",
          alignItems: "center",
          flexWrap: "nowrap",
          paddingLeft: "calc(var(--horizontal-padding) * 1px)",
          paddingRight: "calc(var(--horizontal-padding) * 1px)",
        }}>
          <div style={{
            position: "relative",
            height: 1,
            backgroundColor: "var(--stroke-color)",
            flexGrow: 1,
          }} />
          <div style={{
            position: "relative",
            height: 1,
            backgroundColor: "var(--stroke-color)",
            flexGrow: 1,
          }} />
        </div>
      </div>
      )}
    </div>
  );
  const __impls = {
    // figma: Size=Large, Vertical Divider=Right
    "size=lg|verticalDivider=right": __body0,
    // figma: Size=Medium, Vertical Divider=Right
    "size=md|verticalDivider=right": __body1,
    // figma: Size=Small (Default), Vertical Divider=Right
    "size=small (default)|verticalDivider=right": __body2,
    // figma: Size=Large, Vertical Divider=Left
    "size=lg|verticalDivider=left": __body3,
    // figma: Size=Medium, Vertical Divider=Left
    "size=md|verticalDivider=left": __body4,
    // figma: Size=Small (Default), Vertical Divider=Left
    "size=small (default)|verticalDivider=left": __body5,
    // figma: Size=Large, Vertical Divider=None
    "size=lg|verticalDivider=none": __body6,
    // figma: Size=Medium, Vertical Divider=None
    "size=md|verticalDivider=none": __body7,
    // figma: Size=Small (Default), Vertical Divider=None
    "size=small (default)|verticalDivider=none": __body8,
  };
  return (__impls[__vkey(props)] ?? __body8)();
}
export default InlineDrawer;
