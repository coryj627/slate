import { __slateNorm } from '../internal/slateProps.jsx';
import { Chevron } from '../internal/Chevron.jsx';

// figma node: 9077:5931 Breadcrumb (3 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "size=" + __venc(p.size);

export function Breadcrumb(_p = {}) {
  _p = __slateNorm(_p, {"size":["sm","md","lg"]});
  const props = { ..._p, size: _p.size ?? "sm" };
  const __body0 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      gap: "calc(var(--horizontal-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        gap: "calc(var(--horizontal-none) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 44,
          borderRadius: 4,
          backgroundColor: "rgba(255,255,255,0)",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--horizontal-xs) * 1px)",
          padding: "8px 16px 8px 16px",
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
            display: "flex",
            flexDirection: "row",
            gap: 10,
            padding: "2px 2px 2px 2px",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <svg width={20} viewBox="0 0 20 20" fill="none" style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              color: "var(--neutral-foreground-2-rest)",
            }}>
              <path d={"M 20 10 C 20 15.523 15.523 20 10 20 C 4.477 20 0 15.523 0 10 C 0 4.477 4.477 0 10 0 C 15.523 0 20 4.477 20 10 Z M 18.5 10 C 18.5 14.694 14.694 18.5 10 18.5 C 5.306 18.5 1.5 14.694 1.5 10 C 1.5 5.306 5.306 1.5 10 1.5 C 14.694 1.5 18.5 5.306 18.5 10 Z"} fill="currentColor" fillRule="evenodd" />
            </svg>
          </div>
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
                fontWeight: 400,
                fontSize: 16,
                whiteSpace: "nowrap",
                lineHeight: "22px",
                color: "var(--neutral-foreground-2-rest)",
                flexShrink: 0,
                alignSelf: "stretch",
              }}>Text</span>
            </div>
          </div>
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          alignItems: "center",
          flexWrap: "nowrap",
          flexShrink: 0,
        }}>
          <Chevron
            style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--neutral-foreground-2-rest)",
            }}
            direction={"right"}
            size={"20"}
            theme={"regular"}
          />
        </div>
      </div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        gap: "calc(var(--horizontal-none) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 44,
          borderRadius: 4,
          backgroundColor: "rgba(255,255,255,0)",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--horizontal-xs) * 1px)",
          padding: "8px 16px 8px 16px",
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
            display: "flex",
            flexDirection: "row",
            gap: 10,
            padding: "2px 2px 2px 2px",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <svg width={20} viewBox="0 0 20 20" fill="none" style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              color: "var(--neutral-foreground-2-rest)",
            }}>
              <path d={"M 20 10 C 20 15.523 15.523 20 10 20 C 4.477 20 0 15.523 0 10 C 0 4.477 4.477 0 10 0 C 15.523 0 20 4.477 20 10 Z M 18.5 10 C 18.5 14.694 14.694 18.5 10 18.5 C 5.306 18.5 1.5 14.694 1.5 10 C 1.5 5.306 5.306 1.5 10 1.5 C 14.694 1.5 18.5 5.306 18.5 10 Z"} fill="currentColor" fillRule="evenodd" />
            </svg>
          </div>
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
                fontWeight: 400,
                fontSize: 16,
                whiteSpace: "nowrap",
                lineHeight: "22px",
                color: "var(--neutral-foreground-2-rest)",
                flexShrink: 0,
                alignSelf: "stretch",
              }}>Text</span>
            </div>
          </div>
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          alignItems: "center",
          flexWrap: "nowrap",
          flexShrink: 0,
        }}>
          <Chevron
            style={{
              position: "relative",
              width: 20,
              height: 20,
              flexShrink: 0,
              color: "var(--neutral-foreground-2-rest)",
            }}
            direction={"right"}
            size={"20"}
            theme={"regular"}
          />
        </div>
      </div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        gap: "calc(var(--horizontal-none) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 46,
          borderRadius: 4,
          backgroundColor: "rgba(255,255,255,0)",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--horizontal-xs) * 1px)",
          padding: "8px 16px 8px 16px",
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
            display: "flex",
            flexDirection: "row",
            gap: 10,
            padding: "2px 2px 2px 2px",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <svg width={20} viewBox="0 0 20 20" fill="none" style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              color: "var(--neutral-foreground-2-rest)",
            }}>
              <path d={"M 20 10 C 20 15.523 15.523 20 10 20 C 4.477 20 0 15.523 0 10 C 0 4.477 4.477 0 10 0 C 15.523 0 20 4.477 20 10 Z M 18.5 10 C 18.5 14.694 14.694 18.5 10 18.5 C 5.306 18.5 1.5 14.694 1.5 10 C 1.5 5.306 5.306 1.5 10 1.5 C 14.694 1.5 18.5 5.306 18.5 10 Z"} fill="currentColor" fillRule="evenodd" />
            </svg>
          </div>
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
                fontSize: 16,
                whiteSpace: "nowrap",
                lineHeight: "22px",
                color: "var(--neutral-foreground-2-rest)",
                flexShrink: 0,
                alignSelf: "stretch",
              }}>Text</span>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      gap: "calc(var(--horizontal-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        gap: "calc(var(--horizontal-none) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 37,
          borderRadius: 4,
          backgroundColor: "rgba(255,255,255,0)",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--horizontal-snudge) * 1px)",
          padding: "6px 12px 6px 12px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--horizontal-snudge) * 1px)",
          paddingTop: "calc(var(--vertical-snudge) * 1px)",
          paddingRight: "calc(var(--horizontal-snudge) * 1px)",
          paddingBottom: "calc(var(--vertical-snudge) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            gap: 10,
            padding: "2px 2px 2px 2px",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <svg width={16} viewBox="0 0 16 16" fill="none" style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              color: "var(--neutral-foreground-2-rest)",
            }}>
              <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
            </svg>
          </div>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
            padding: "1px 0px 1px 0px",
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
              paddingBottom: "calc(var(--spacing-vertical-xxs) * 1px)",
              flexShrink: 0,
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
              }}>Text</span>
            </div>
          </div>
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          alignItems: "center",
          flexWrap: "nowrap",
          flexShrink: 0,
        }}>
          <Chevron
            style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--neutral-foreground-2-rest)",
            }}
            direction={"right"}
            size={"16"}
            theme={"regular"}
          />
        </div>
      </div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        gap: "calc(var(--horizontal-none) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 37,
          borderRadius: 4,
          backgroundColor: "rgba(255,255,255,0)",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--horizontal-snudge) * 1px)",
          padding: "6px 12px 6px 12px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--horizontal-snudge) * 1px)",
          paddingTop: "calc(var(--vertical-snudge) * 1px)",
          paddingRight: "calc(var(--horizontal-snudge) * 1px)",
          paddingBottom: "calc(var(--vertical-snudge) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            gap: 10,
            padding: "2px 2px 2px 2px",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <svg width={16} viewBox="0 0 16 16" fill="none" style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              color: "var(--neutral-foreground-2-rest)",
            }}>
              <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
            </svg>
          </div>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
            padding: "1px 0px 1px 0px",
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
              paddingBottom: "calc(var(--spacing-vertical-xxs) * 1px)",
              flexShrink: 0,
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
              }}>Text</span>
            </div>
          </div>
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          alignItems: "center",
          flexWrap: "nowrap",
          flexShrink: 0,
        }}>
          <Chevron
            style={{
              position: "relative",
              width: 16,
              height: 16,
              flexShrink: 0,
              color: "var(--neutral-foreground-2-rest)",
            }}
            direction={"right"}
            size={"16"}
            theme={"regular"}
          />
        </div>
      </div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        gap: "calc(var(--horizontal-none) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 38,
          borderRadius: 4,
          backgroundColor: "rgba(255,255,255,0)",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--horizontal-snudge) * 1px)",
          padding: "6px 12px 6px 12px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--horizontal-snudge) * 1px)",
          paddingTop: "calc(var(--vertical-snudge) * 1px)",
          paddingRight: "calc(var(--horizontal-snudge) * 1px)",
          paddingBottom: "calc(var(--vertical-snudge) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            gap: 10,
            padding: "2px 2px 2px 2px",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <svg width={16} viewBox="0 0 16 16" fill="none" style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              color: "var(--neutral-foreground-2-rest)",
            }}>
              <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
            </svg>
          </div>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            gap: "calc(var(--spacing-horizontal-xs-2) * 1px)",
            padding: "1px 0px 1px 0px",
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
              paddingBottom: "calc(var(--spacing-vertical-xxs) * 1px)",
              flexShrink: 0,
            }}>
              <span style={{
                position: "relative",
                fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
                fontWeight: 600,
                fontSize: 14,
                whiteSpace: "nowrap",
                lineHeight: "20px",
                color: "var(--neutral-foreground-2-rest)",
                flexShrink: 0,
              }}>Text</span>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
  const __body2 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      gap: "calc(var(--horizontal-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        gap: "calc(var(--horizontal-none) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 33,
          borderRadius: 4,
          backgroundColor: "rgba(255,255,255,0)",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--horizontal-none) * 1px)",
          padding: "2px 8px 2px 8px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--horizontal-snudge) * 1px)",
          paddingTop: "calc(var(--vertical-xxs) * 1px)",
          paddingRight: "calc(var(--horizontal-snudge) * 1px)",
          paddingBottom: "calc(var(--vertical-xxs) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            gap: 10,
            padding: "2px 2px 2px 2px",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <svg width={16} viewBox="0 0 16 16" fill="none" style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              color: "var(--neutral-foreground-2-rest)",
            }}>
              <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
            </svg>
          </div>
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
                fontWeight: 400,
                fontSize: 12,
                whiteSpace: "nowrap",
                lineHeight: "16px",
                color: "var(--neutral-foreground-2-rest)",
                flexShrink: 0,
                alignSelf: "stretch",
              }}>Text</span>
            </div>
          </div>
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          alignItems: "center",
          flexWrap: "nowrap",
          flexShrink: 0,
        }}>
          <Chevron
            style={{
              position: "relative",
              width: 12,
              height: 12,
              flexShrink: 0,
              color: "var(--neutral-foreground-2-rest)",
            }}
            direction={"right"}
            size={"12"}
            theme={"regular"}
          />
        </div>
      </div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        gap: "calc(var(--horizontal-none) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 33,
          borderRadius: 4,
          backgroundColor: "rgba(255,255,255,0)",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--horizontal-none) * 1px)",
          padding: "2px 8px 2px 8px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--horizontal-snudge) * 1px)",
          paddingTop: "calc(var(--vertical-xxs) * 1px)",
          paddingRight: "calc(var(--horizontal-snudge) * 1px)",
          paddingBottom: "calc(var(--vertical-xxs) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            gap: 10,
            padding: "2px 2px 2px 2px",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <svg width={16} viewBox="0 0 16 16" fill="none" style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              color: "var(--neutral-foreground-2-rest)",
            }}>
              <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
            </svg>
          </div>
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
                fontWeight: 400,
                fontSize: 12,
                whiteSpace: "nowrap",
                lineHeight: "16px",
                color: "var(--neutral-foreground-2-rest)",
                flexShrink: 0,
                alignSelf: "stretch",
              }}>Text</span>
            </div>
          </div>
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          alignItems: "center",
          flexWrap: "nowrap",
          flexShrink: 0,
        }}>
          <Chevron
            style={{
              position: "relative",
              width: 12,
              height: 12,
              flexShrink: 0,
              color: "var(--neutral-foreground-2-rest)",
            }}
            direction={"right"}
            size={"12"}
            theme={"regular"}
          />
        </div>
      </div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        gap: "calc(var(--horizontal-none) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          width: 35,
          borderRadius: 4,
          backgroundColor: "rgba(255,255,255,0)",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--horizontal-none) * 1px)",
          padding: "2px 8px 2px 8px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--horizontal-snudge) * 1px)",
          paddingTop: "calc(var(--vertical-xxs) * 1px)",
          paddingRight: "calc(var(--horizontal-snudge) * 1px)",
          paddingBottom: "calc(var(--vertical-xxs) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            gap: 10,
            padding: "2px 2px 2px 2px",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <svg width={16} viewBox="0 0 16 16" fill="none" style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              color: "var(--neutral-foreground-2-rest)",
            }}>
              <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
            </svg>
          </div>
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
                fontSize: 12,
                whiteSpace: "nowrap",
                lineHeight: "16px",
                color: "var(--neutral-foreground-2-rest)",
                flexShrink: 0,
                alignSelf: "stretch",
              }}>Text</span>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
  const __impls = {
    // figma: Size=Large
    "size=lg": __body0,
    // figma: Size=Medium
    "size=md": __body1,
    // figma: Size=Small
    "size=sm": __body2,
  };
  return (__impls[__vkey(props)] ?? __body2)();
}
export default Breadcrumb;
