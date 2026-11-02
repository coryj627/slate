import { __slateNorm } from '../internal/slateProps.jsx';
import { Calendar3 } from '../internal/Calendar3.jsx';

// figma node: 9116:17888 Horizontal TabList (12 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "layout=" + __venc(p.layout) + '|' + "size=" + __venc(p.size) + '|' + "circular=" + __venc(p.circular);

export function HorizontalTabList(_p = {}) {
  _p = __slateNorm(_p, {"layout":["icon before","text only","icon only"],"size":["medium (default)","sm"]});
  const props = { ..._p, layout: _p.layout ?? "icon before", size: _p.size ?? "medium (default)", circular: _p.circular ?? true };
  const __body0 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
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
        gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        padding: "12px 10px 12px 10px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingTop: "calc(var(--spacing-horizontal-m-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingBottom: "calc(var(--spacing-horizontal-m-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
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
          <Calendar3
            style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--brand-foreground-compound-rest)",
            }}
            direction={"ltr"}
            size={"20"}
            theme={"filled"}
          />
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
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
            color: "var(--neutral-foreground-1-rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>First tab</span>
        </div>
        <div style={{
          position: "absolute",
          left: 0,
          top: 41,
          width: 102,
          height: 3,
        }}>
          <div style={{
            position: "absolute",
            left: 12,
            top: 0,
            width: 78,
            height: 3,
            borderRadius: 9999,
            backgroundColor: "var(--brand-stroke-compound-rest)",
          }} />
        </div>
      </div>
      <div style={{
        position: "relative",
        borderRadius: 4,
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        padding: "12px 10px 12px 10px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingTop: "calc(var(--spacing-horizontal-m-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingBottom: "calc(var(--spacing-horizontal-m-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
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
          <Calendar3
            style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-rest)",
            }}
            direction={"ltr"}
            size={"20"}
            theme={"regular"}
          />
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          flexShrink: 0,
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
          }}>Second tab</span>
        </div>
        <div style={{
          position: "absolute",
          left: 0,
          top: 41,
          width: 120,
          height: 3,
        }} />
      </div>
      <div style={{
        position: "relative",
        borderRadius: 4,
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        padding: "12px 10px 12px 10px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingTop: "calc(var(--spacing-horizontal-m-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingBottom: "calc(var(--spacing-horizontal-m-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
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
          <Calendar3
            style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-rest)",
            }}
            direction={"ltr"}
            size={"20"}
            theme={"regular"}
          />
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          flexShrink: 0,
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
          }}>Third tab</span>
        </div>
        <div style={{
          position: "absolute",
          left: 0,
          top: 41,
          width: 106,
          height: 3,
        }} />
      </div>
      <div style={{
        position: "relative",
        borderRadius: 4,
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        padding: "12px 10px 12px 10px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingTop: "calc(var(--spacing-horizontal-m-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingBottom: "calc(var(--spacing-horizontal-m-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
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
          <Calendar3
            style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-rest)",
            }}
            direction={"ltr"}
            size={"20"}
            theme={"regular"}
          />
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          flexShrink: 0,
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
          }}>Fourth tab</span>
        </div>
        <div style={{
          position: "absolute",
          left: 0,
          top: 41,
          width: 115,
          height: 3,
        }} />
      </div>
      <div style={{
        position: "relative",
        borderRadius: 4,
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        padding: "12px 10px 12px 10px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingTop: "calc(var(--spacing-horizontal-m-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingBottom: "calc(var(--spacing-horizontal-m-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
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
          <Calendar3
            style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-rest)",
            }}
            direction={"ltr"}
            size={"20"}
            theme={"regular"}
          />
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          flexShrink: 0,
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
          }}>Fifth tab</span>
        </div>
        <div style={{
          position: "absolute",
          left: 0,
          top: 41,
          width: 102,
          height: 3,
        }} />
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
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
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
        gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        padding: "12px 10px 12px 10px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingTop: "calc(var(--spacing-horizontal-m-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingBottom: "calc(var(--spacing-horizontal-m-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
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
            color: "var(--neutral-foreground-1-rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>First tab</span>
        </div>
        <div style={{
          position: "absolute",
          left: 0,
          top: 41,
          width: 76,
          height: 3,
        }}>
          <div style={{
            position: "absolute",
            left: 12,
            top: 0,
            width: 52,
            height: 3,
            borderRadius: 9999,
            backgroundColor: "var(--brand-stroke-compound-rest)",
          }} />
        </div>
      </div>
      <div style={{
        position: "relative",
        borderRadius: 4,
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        padding: "12px 10px 12px 10px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingTop: "calc(var(--spacing-horizontal-m-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingBottom: "calc(var(--spacing-horizontal-m-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          flexShrink: 0,
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
          }}>Second tab</span>
        </div>
        <div style={{
          position: "absolute",
          left: 0,
          top: 41,
          width: 94,
          height: 3,
        }} />
      </div>
      <div style={{
        position: "relative",
        borderRadius: 4,
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        padding: "12px 10px 12px 10px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingTop: "calc(var(--spacing-horizontal-m-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingBottom: "calc(var(--spacing-horizontal-m-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          flexShrink: 0,
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
          }}>Third tab</span>
        </div>
        <div style={{
          position: "absolute",
          left: 0,
          top: 41,
          width: 80,
          height: 3,
        }} />
      </div>
      <div style={{
        position: "relative",
        borderRadius: 4,
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        padding: "12px 10px 12px 10px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingTop: "calc(var(--spacing-horizontal-m-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingBottom: "calc(var(--spacing-horizontal-m-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          flexShrink: 0,
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
          }}>Fourth tab</span>
        </div>
        <div style={{
          position: "absolute",
          left: 0,
          top: 41,
          width: 89,
          height: 3,
        }} />
      </div>
      <div style={{
        position: "relative",
        borderRadius: 4,
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        padding: "12px 10px 12px 10px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingTop: "calc(var(--spacing-horizontal-m-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingBottom: "calc(var(--spacing-horizontal-m-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          flexShrink: 0,
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
          }}>Fifth tab</span>
        </div>
        <div style={{
          position: "absolute",
          left: 0,
          top: 41,
          width: 76,
          height: 3,
        }} />
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
  );
  const __body2 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
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
        gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        padding: "12px 10px 12px 10px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingTop: "calc(var(--spacing-horizontal-m-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingBottom: "calc(var(--spacing-horizontal-m-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
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
          <Calendar3
            style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--brand-foreground-compound-rest)",
            }}
            direction={"ltr"}
            size={"20"}
            theme={"filled"}
          />
        </div>
        <div style={{
          position: "absolute",
          left: 0,
          top: 41,
          width: 40,
          height: 3,
        }}>
          <div style={{
            position: "absolute",
            left: 12,
            top: 0,
            width: 16,
            height: 3,
            borderRadius: 9999,
            backgroundColor: "var(--brand-stroke-compound-rest)",
          }} />
        </div>
      </div>
      <div style={{
        position: "relative",
        borderRadius: 4,
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        padding: "12px 10px 12px 10px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingTop: "calc(var(--spacing-horizontal-m-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingBottom: "calc(var(--spacing-horizontal-m-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
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
          <Calendar3
            style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-rest)",
            }}
            direction={"ltr"}
            size={"20"}
            theme={"regular"}
          />
        </div>
        <div style={{
          position: "absolute",
          left: 0,
          top: 41,
          width: 40,
          height: 3,
        }} />
      </div>
      <div style={{
        position: "relative",
        borderRadius: 4,
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        padding: "12px 10px 12px 10px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingTop: "calc(var(--spacing-horizontal-m-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingBottom: "calc(var(--spacing-horizontal-m-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
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
          <Calendar3
            style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-rest)",
            }}
            direction={"ltr"}
            size={"20"}
            theme={"regular"}
          />
        </div>
        <div style={{
          position: "absolute",
          left: 0,
          top: 41,
          width: 40,
          height: 3,
        }} />
      </div>
      <div style={{
        position: "relative",
        borderRadius: 4,
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        padding: "12px 10px 12px 10px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingTop: "calc(var(--spacing-horizontal-m-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingBottom: "calc(var(--spacing-horizontal-m-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
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
          <Calendar3
            style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-rest)",
            }}
            direction={"ltr"}
            size={"20"}
            theme={"regular"}
          />
        </div>
        <div style={{
          position: "absolute",
          left: 0,
          top: 41,
          width: 40,
          height: 3,
        }} />
      </div>
      <div style={{
        position: "relative",
        borderRadius: 4,
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        padding: "12px 10px 12px 10px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingTop: "calc(var(--spacing-horizontal-m-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingBottom: "calc(var(--spacing-horizontal-m-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
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
          <Calendar3
            style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-rest)",
            }}
            direction={"ltr"}
            size={"20"}
            theme={"regular"}
          />
        </div>
        <div style={{
          position: "absolute",
          left: 0,
          top: 41,
          width: 40,
          height: 3,
        }} />
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
  );
  const __body3 = () => (
    <div className={props.className} style={{
      width: 617,
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        borderRadius: 9999,
        backgroundColor: "var(--brand-background-2-rest)",
        borderTop: "1px solid var(--brand-stroke-compound-rest)",
        borderRight: "1px solid var(--brand-stroke-compound-rest)",
        borderBottom: "1px solid var(--brand-stroke-compound-rest)",
        borderLeft: "1px solid var(--brand-stroke-compound-rest)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        padding: "6px 10px 6px 10px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
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
          <Calendar3
            style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--brand-foreground-compound-rest)",
            }}
            direction={"ltr"}
            size={"20"}
            theme={"filled"}
          />
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
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
            color: "var(--brand-foreground-2-rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>First tab</span>
        </div>
      </div>
      <div style={{
        position: "relative",
        borderRadius: 9999,
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        padding: "6px 10px 6px 10px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
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
          <Calendar3
            style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-rest)",
            }}
            direction={"ltr"}
            size={"20"}
            theme={"regular"}
          />
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          flexShrink: 0,
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
          }}>Second tab</span>
        </div>
      </div>
      <div style={{
        position: "relative",
        borderRadius: 9999,
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        padding: "6px 10px 6px 10px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
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
          <Calendar3
            style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-rest)",
            }}
            direction={"ltr"}
            size={"20"}
            theme={"regular"}
          />
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          flexShrink: 0,
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
          }}>Third tab</span>
        </div>
      </div>
      <div style={{
        position: "relative",
        borderRadius: 9999,
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        padding: "6px 10px 6px 10px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
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
          <Calendar3
            style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-rest)",
            }}
            direction={"ltr"}
            size={"20"}
            theme={"regular"}
          />
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          flexShrink: 0,
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
          }}>Fourth tab</span>
        </div>
      </div>
      <div style={{
        position: "relative",
        borderRadius: 9999,
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        padding: "6px 10px 6px 10px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
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
          <Calendar3
            style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-rest)",
            }}
            direction={"ltr"}
            size={"20"}
            theme={"regular"}
          />
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          flexShrink: 0,
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
          }}>Fifth tab</span>
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
  );
  const __body4 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        borderRadius: 9999,
        backgroundColor: "var(--brand-background-2-rest)",
        borderTop: "1px solid var(--brand-stroke-compound-rest)",
        borderRight: "1px solid var(--brand-stroke-compound-rest)",
        borderBottom: "1px solid var(--brand-stroke-compound-rest)",
        borderLeft: "1px solid var(--brand-stroke-compound-rest)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        padding: "6px 10px 6px 10px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
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
            color: "var(--brand-foreground-2-rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>First tab</span>
        </div>
      </div>
      <div style={{
        position: "relative",
        borderRadius: 9999,
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        padding: "6px 10px 6px 10px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          flexShrink: 0,
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
          }}>Second tab</span>
        </div>
      </div>
      <div style={{
        position: "relative",
        borderRadius: 9999,
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        padding: "6px 10px 6px 10px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          flexShrink: 0,
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
          }}>Third tab</span>
        </div>
      </div>
      <div style={{
        position: "relative",
        borderRadius: 9999,
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        padding: "6px 10px 6px 10px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          flexShrink: 0,
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
          }}>Fourth tab</span>
        </div>
      </div>
      <div style={{
        position: "relative",
        borderRadius: 9999,
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        padding: "6px 10px 6px 10px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          flexShrink: 0,
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
          }}>Fifth tab</span>
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
  );
  const __body5 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        borderRadius: 9999,
        backgroundColor: "var(--brand-background-2-rest)",
        borderTop: "1px solid var(--brand-stroke-compound-rest)",
        borderRight: "1px solid var(--brand-stroke-compound-rest)",
        borderBottom: "1px solid var(--brand-stroke-compound-rest)",
        borderLeft: "1px solid var(--brand-stroke-compound-rest)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        padding: "6px 10px 6px 10px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
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
          <Calendar3
            style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--brand-foreground-compound-rest)",
            }}
            direction={"ltr"}
            size={"20"}
            theme={"filled"}
          />
        </div>
      </div>
      <div style={{
        position: "relative",
        borderRadius: 9999,
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        padding: "6px 10px 6px 10px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
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
          <Calendar3
            style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-rest)",
            }}
            direction={"ltr"}
            size={"20"}
            theme={"regular"}
          />
        </div>
      </div>
      <div style={{
        position: "relative",
        borderRadius: 9999,
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        padding: "6px 10px 6px 10px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
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
          <Calendar3
            style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-rest)",
            }}
            direction={"ltr"}
            size={"20"}
            theme={"regular"}
          />
        </div>
      </div>
      <div style={{
        position: "relative",
        borderRadius: 9999,
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        padding: "6px 10px 6px 10px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
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
          <Calendar3
            style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-rest)",
            }}
            direction={"ltr"}
            size={"20"}
            theme={"regular"}
          />
        </div>
      </div>
      <div style={{
        position: "relative",
        borderRadius: 9999,
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        padding: "6px 10px 6px 10px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
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
          <Calendar3
            style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-rest)",
            }}
            direction={"ltr"}
            size={"20"}
            theme={"regular"}
          />
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
  );
  const __body6 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
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
        gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        padding: "6px 6px 6px 6px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
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
          <Calendar3
            style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--brand-foreground-compound-rest)",
            }}
            direction={"ltr"}
            size={"20"}
            theme={"filled"}
          />
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
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
            color: "var(--neutral-foreground-1-rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>First tab</span>
        </div>
        <div style={{
          position: "absolute",
          left: 0,
          top: 30,
          width: 90,
          height: 2,
        }}>
          <div style={{
            position: "absolute",
            left: 8,
            top: 0,
            width: 74,
            height: 2,
            borderRadius: 9999,
            backgroundColor: "var(--brand-stroke-compound-rest)",
          }} />
        </div>
      </div>
      <div style={{
        position: "relative",
        borderRadius: 4,
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        padding: "6px 6px 6px 6px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
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
          <Calendar3
            style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-rest)",
            }}
            direction={"ltr"}
            size={"20"}
            theme={"regular"}
          />
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          flexShrink: 0,
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
          }}>Second tab</span>
        </div>
        <div style={{
          position: "absolute",
          left: 0,
          top: 30,
          width: 108,
          height: 2,
        }} />
      </div>
      <div style={{
        position: "relative",
        borderRadius: 4,
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        padding: "6px 6px 6px 6px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
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
          <Calendar3
            style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-rest)",
            }}
            direction={"ltr"}
            size={"20"}
            theme={"regular"}
          />
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          flexShrink: 0,
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
          }}>Third tab</span>
        </div>
        <div style={{
          position: "absolute",
          left: 0,
          top: 30,
          width: 94,
          height: 2,
        }} />
      </div>
      <div style={{
        position: "relative",
        borderRadius: 4,
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        padding: "6px 6px 6px 6px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
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
          <Calendar3
            style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-rest)",
            }}
            direction={"ltr"}
            size={"20"}
            theme={"regular"}
          />
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          flexShrink: 0,
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
          }}>Fourth tab</span>
        </div>
        <div style={{
          position: "absolute",
          left: 0,
          top: 30,
          width: 103,
          height: 2,
        }} />
      </div>
      <div style={{
        position: "relative",
        borderRadius: 4,
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        padding: "6px 6px 6px 6px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
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
          <Calendar3
            style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-rest)",
            }}
            direction={"ltr"}
            size={"20"}
            theme={"regular"}
          />
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          flexShrink: 0,
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
          }}>Fifth tab</span>
        </div>
        <div style={{
          position: "absolute",
          left: 0,
          top: 30,
          width: 90,
          height: 2,
        }} />
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
  );
  const __body7 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
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
        gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        padding: "6px 6px 6px 6px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
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
            color: "var(--neutral-foreground-1-rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>First tab</span>
        </div>
        <div style={{
          position: "absolute",
          left: 0,
          top: 30,
          width: 68,
          height: 2,
        }}>
          <div style={{
            position: "absolute",
            left: 8,
            top: 0,
            width: 52,
            height: 2,
            borderRadius: 9999,
            backgroundColor: "var(--brand-stroke-compound-rest)",
          }} />
        </div>
      </div>
      <div style={{
        position: "relative",
        borderRadius: 4,
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        padding: "6px 6px 6px 6px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          flexShrink: 0,
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
          }}>Second tab</span>
        </div>
        <div style={{
          position: "absolute",
          left: 0,
          top: 30,
          width: 86,
          height: 2,
        }} />
      </div>
      <div style={{
        position: "relative",
        borderRadius: 4,
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        padding: "6px 6px 6px 6px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          flexShrink: 0,
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
          }}>Third tab</span>
        </div>
        <div style={{
          position: "absolute",
          left: 0,
          top: 30,
          width: 72,
          height: 2,
        }} />
      </div>
      <div style={{
        position: "relative",
        borderRadius: 4,
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        padding: "6px 6px 6px 6px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          flexShrink: 0,
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
          }}>Fourth tab</span>
        </div>
        <div style={{
          position: "absolute",
          left: 0,
          top: 30,
          width: 81,
          height: 2,
        }} />
      </div>
      <div style={{
        position: "relative",
        borderRadius: 4,
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        padding: "6px 6px 6px 6px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          flexShrink: 0,
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
          }}>Fifth tab</span>
        </div>
        <div style={{
          position: "absolute",
          left: 0,
          top: 30,
          width: 68,
          height: 2,
        }} />
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
  );
  const __body8 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
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
        gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        padding: "6px 6px 6px 6px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
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
          <Calendar3
            style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--brand-foreground-compound-rest)",
            }}
            direction={"ltr"}
            size={"20"}
            theme={"filled"}
          />
        </div>
        <div style={{
          position: "absolute",
          left: 0,
          top: 30,
          width: 32,
          height: 2,
        }}>
          <div style={{
            position: "absolute",
            left: 8,
            top: 0,
            width: 16,
            height: 2,
            borderRadius: 9999,
            backgroundColor: "var(--brand-stroke-compound-rest)",
          }} />
        </div>
      </div>
      <div style={{
        position: "relative",
        borderRadius: 4,
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        padding: "6px 6px 6px 6px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
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
          <Calendar3
            style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-rest)",
            }}
            direction={"ltr"}
            size={"20"}
            theme={"regular"}
          />
        </div>
        <div style={{
          position: "absolute",
          left: 0,
          top: 30,
          width: 32,
          height: 2,
        }} />
      </div>
      <div style={{
        position: "relative",
        borderRadius: 4,
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        padding: "6px 6px 6px 6px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
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
          <Calendar3
            style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-rest)",
            }}
            direction={"ltr"}
            size={"20"}
            theme={"regular"}
          />
        </div>
        <div style={{
          position: "absolute",
          left: 0,
          top: 30,
          width: 32,
          height: 2,
        }} />
      </div>
      <div style={{
        position: "relative",
        borderRadius: 4,
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        padding: "6px 6px 6px 6px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
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
          <Calendar3
            style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-rest)",
            }}
            direction={"ltr"}
            size={"20"}
            theme={"regular"}
          />
        </div>
        <div style={{
          position: "absolute",
          left: 0,
          top: 30,
          width: 32,
          height: 2,
        }} />
      </div>
      <div style={{
        position: "relative",
        borderRadius: 4,
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
        padding: "6px 6px 6px 6px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
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
          <Calendar3
            style={{
              position: "relative",
              width: 20,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-rest)",
            }}
            direction={"ltr"}
            size={"20"}
            theme={"regular"}
          />
        </div>
        <div style={{
          position: "absolute",
          left: 0,
          top: 30,
          width: 32,
          height: 2,
        }} />
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
  );
  const __body9 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        height: 24,
        borderRadius: 9999,
        backgroundColor: "var(--brand-background-2-rest)",
        borderTop: "1px solid var(--brand-stroke-compound-rest)",
        borderRight: "1px solid var(--brand-stroke-compound-rest)",
        borderBottom: "1px solid var(--brand-stroke-compound-rest)",
        borderLeft: "1px solid var(--brand-stroke-compound-rest)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        padding: "4px 8px 4px 8px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingTop: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingBottom: "calc(var(--spacing-horizontal-xs-2) * 1px)",
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
          <Calendar3
            style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--brand-foreground-compound-rest)",
            }}
            direction={"ltr"}
            size={"16"}
            theme={"filled"}
          />
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 600,
            fontSize: 12,
            whiteSpace: "nowrap",
            lineHeight: "16px",
            color: "var(--brand-foreground-2-rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>First tab</span>
        </div>
      </div>
      <div style={{
        position: "relative",
        height: 24,
        borderRadius: 9999,
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        padding: "4px 8px 4px 8px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingTop: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingBottom: "calc(var(--spacing-horizontal-xs-2) * 1px)",
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
          <Calendar3
            style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-rest)",
            }}
            direction={"ltr"}
            size={"16"}
            theme={"regular"}
          />
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
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
          }}>Second tab</span>
        </div>
      </div>
      <div style={{
        position: "relative",
        height: 24,
        borderRadius: 9999,
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        padding: "4px 8px 4px 8px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingTop: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingBottom: "calc(var(--spacing-horizontal-xs-2) * 1px)",
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
          <Calendar3
            style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-rest)",
            }}
            direction={"ltr"}
            size={"16"}
            theme={"regular"}
          />
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
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
          }}>Third tab</span>
        </div>
      </div>
      <div style={{
        position: "relative",
        height: 24,
        borderRadius: 9999,
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        padding: "4px 8px 4px 8px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingTop: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingBottom: "calc(var(--spacing-horizontal-xs-2) * 1px)",
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
          <Calendar3
            style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-rest)",
            }}
            direction={"ltr"}
            size={"16"}
            theme={"regular"}
          />
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
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
          }}>Fourth tab</span>
        </div>
      </div>
      <div style={{
        position: "relative",
        height: 24,
        borderRadius: 9999,
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        padding: "4px 8px 4px 8px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingTop: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingBottom: "calc(var(--spacing-horizontal-xs-2) * 1px)",
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
          <Calendar3
            style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-rest)",
            }}
            direction={"ltr"}
            size={"16"}
            theme={"regular"}
          />
        </div>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
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
          }}>Fifth tab</span>
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
  );
  const __body10 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        height: 24,
        borderRadius: 9999,
        backgroundColor: "var(--brand-background-2-rest)",
        borderTop: "1px solid var(--brand-stroke-compound-rest)",
        borderRight: "1px solid var(--brand-stroke-compound-rest)",
        borderBottom: "1px solid var(--brand-stroke-compound-rest)",
        borderLeft: "1px solid var(--brand-stroke-compound-rest)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        padding: "4px 8px 4px 8px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingTop: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingBottom: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 600,
            fontSize: 12,
            whiteSpace: "nowrap",
            lineHeight: "16px",
            color: "var(--brand-foreground-2-rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>First tab</span>
        </div>
      </div>
      <div style={{
        position: "relative",
        height: 24,
        borderRadius: 9999,
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        padding: "4px 8px 4px 8px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingTop: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingBottom: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
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
          }}>Second tab</span>
        </div>
      </div>
      <div style={{
        position: "relative",
        height: 24,
        borderRadius: 9999,
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        padding: "4px 8px 4px 8px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingTop: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingBottom: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
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
          }}>Third tab</span>
        </div>
      </div>
      <div style={{
        position: "relative",
        height: 24,
        borderRadius: 9999,
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        padding: "4px 8px 4px 8px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingTop: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingBottom: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
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
          }}>Fourth tab</span>
        </div>
      </div>
      <div style={{
        position: "relative",
        height: 24,
        borderRadius: 9999,
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        padding: "4px 8px 4px 8px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingTop: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingBottom: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          padding: "0px 2px 0px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          flexShrink: 0,
          alignSelf: "stretch",
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
          }}>Fifth tab</span>
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
  );
  const __body11 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        height: 24,
        borderRadius: 9999,
        backgroundColor: "var(--brand-background-2-rest)",
        borderTop: "1px solid var(--brand-stroke-compound-rest)",
        borderRight: "1px solid var(--brand-stroke-compound-rest)",
        borderBottom: "1px solid var(--brand-stroke-compound-rest)",
        borderLeft: "1px solid var(--brand-stroke-compound-rest)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        padding: "4px 8px 4px 8px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingTop: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingBottom: "calc(var(--spacing-horizontal-xs-2) * 1px)",
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
          <Calendar3
            style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--brand-foreground-compound-rest)",
            }}
            direction={"ltr"}
            size={"16"}
            theme={"filled"}
          />
        </div>
      </div>
      <div style={{
        position: "relative",
        height: 24,
        borderRadius: 9999,
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        padding: "4px 8px 4px 8px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingTop: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingBottom: "calc(var(--spacing-horizontal-xs-2) * 1px)",
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
          <Calendar3
            style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-rest)",
            }}
            direction={"ltr"}
            size={"16"}
            theme={"regular"}
          />
        </div>
      </div>
      <div style={{
        position: "relative",
        height: 24,
        borderRadius: 9999,
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        padding: "4px 8px 4px 8px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingTop: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingBottom: "calc(var(--spacing-horizontal-xs-2) * 1px)",
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
          <Calendar3
            style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-rest)",
            }}
            direction={"ltr"}
            size={"16"}
            theme={"regular"}
          />
        </div>
      </div>
      <div style={{
        position: "relative",
        height: 24,
        borderRadius: 9999,
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        padding: "4px 8px 4px 8px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingTop: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingBottom: "calc(var(--spacing-horizontal-xs-2) * 1px)",
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
          <Calendar3
            style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-rest)",
            }}
            direction={"ltr"}
            size={"16"}
            theme={"regular"}
          />
        </div>
      </div>
      <div style={{
        position: "relative",
        height: 24,
        borderRadius: 9999,
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        padding: "4px 8px 4px 8px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingTop: "calc(var(--spacing-horizontal-xs-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-s-2) * 1px)",
        paddingBottom: "calc(var(--spacing-horizontal-xs-2) * 1px)",
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
          <Calendar3
            style={{
              position: "relative",
              width: 16,
              flexShrink: 0,
              alignSelf: "stretch",
              height: "auto",
              color: "var(--neutral-foreground-2-rest)",
            }}
            direction={"ltr"}
            size={"16"}
            theme={"regular"}
          />
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
  );
  const __impls = {
    // figma: Layout=Icon before, Size=Medium (default), Circular=False
    "layout=icon before|size=medium (default)|circular=false": __body0,
    // figma: Layout=Text only, Size=Medium (default), Circular=False
    "layout=text only|size=medium (default)|circular=false": __body1,
    // figma: Layout=Icon only, Size=Medium (default), Circular=False
    "layout=icon only|size=medium (default)|circular=false": __body2,
    // figma: Layout=Icon before, Size=Medium (default), Circular=True
    "layout=icon before|size=medium (default)|circular=true": __body3,
    // figma: Layout=Text only, Size=Medium (default), Circular=True
    "layout=text only|size=medium (default)|circular=true": __body4,
    // figma: Layout=Icon only, Size=Medium (default), Circular=True
    "layout=icon only|size=medium (default)|circular=true": __body5,
    // figma: Layout=Icon before, Size=Small, Circular=False
    "layout=icon before|size=sm|circular=false": __body6,
    // figma: Layout=Text only, Size=Small, Circular=False
    "layout=text only|size=sm|circular=false": __body7,
    // figma: Layout=Icon only, Size=Small, Circular=False
    "layout=icon only|size=sm|circular=false": __body8,
    // figma: Layout=Icon before, Size=Small, Circular=True
    "layout=icon before|size=sm|circular=true": __body9,
    // figma: Layout=Text only, Size=Small, Circular=True
    "layout=text only|size=sm|circular=true": __body10,
    // figma: Layout=Icon only, Size=Small, Circular=True
    "layout=icon only|size=sm|circular=true": __body11,
  };
  return (__impls[__vkey(props)] ?? __body3)();
}
export default HorizontalTabList;
