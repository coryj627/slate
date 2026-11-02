import { __slateNorm } from '../internal/slateProps.jsx';
// figma node: 9398:17774 Persona (16 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "layout=" + __venc(p.layout) + '|' + "style2=" + __venc(p.style2) + '|' + "size=" + __venc(p.size) + '|' + "alignment=" + __venc(p.alignment);

export function Persona(_p = {}) {
  _p = __slateNorm(_p, {"layout":["text after (default)","text before (avatar only)","text below (avatar only)"],"style2":["avatar (default)","presence badge"],"size":["small (badge only)","medium (default)","lg"],"alignment":["center (default)","top"]});
  const props = { ..._p, primaryString: _p.primaryString ?? "Primary string", subtext: _p.subtext ?? true, tertiaryString: _p.tertiaryString ?? "Tertiary string", secondaryString: _p.secondaryString ?? "Secondary string", quaternaryText: _p.quaternaryText ?? false, tertiaryText: _p.tertiaryText ?? false, quaternaryString: _p.quaternaryString ?? "Quaternary string", layout: _p.layout ?? "text after (default)", style2: _p.style2 ?? "avatar (default)", size: _p.size ?? (_p.style2 === "presence badge" ? "small (badge only)" : "medium (default)"), alignment: _p.alignment ?? "center (default)" };
  const __body0 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--avatar-spacing-2) * 1px)",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        borderRadius: 9999,
        borderTop: "1px solid rgba(255,255,255,0)",
        borderRight: "1px solid rgba(255,255,255,0)",
        borderBottom: "1px solid rgba(255,255,255,0)",
        borderLeft: "1px solid rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div className="fig-asset-1ca2b212726726ee" style={{
          position: "relative",
          width: 32,
          height: 32,
          borderRadius: 9999,
          borderTop: "1px solid rgba(255,255,255,0)",
          borderRight: "1px solid rgba(255,255,255,0)",
          borderBottom: "1px solid rgba(255,255,255,0)",
          borderLeft: "1px solid rgba(255,255,255,0)",
          flexShrink: 0,
        }} />
        <div style={{
          position: "absolute",
          left: 22,
          top: 22,
          borderRadius: 9999,
          backgroundColor: "var(--neutral-background-1-rest)",
          boxShadow: "0 0 0 2px var(--neutral-background-1-rest)",
          display: "flex",
          flexDirection: "row",
          alignItems: "flex-start",
          flexWrap: "nowrap",
        }}>
          <div style={{
            position: "relative",
            width: 10,
            height: 10,
            flexShrink: 0,
          }}>
            <svg width={10} height={10} viewBox="0 0 10 10" fill="none" style={{
              position: "absolute",
              left: 0,
              top: 0,
              width: 10,
              height: 10,
              color: "var(--status-available-foreground-3-rest)",
            }}>
              <path d={"M 5 10 C 7.761 10 10 7.761 10 5 C 10 2.239 7.761 0 5 0 C 2.239 0 0 2.239 0 5 C 0 7.761 2.239 10 5 10 Z M 7.104 4.104 L 4.854 6.354 C 4.658 6.549 4.342 6.549 4.146 6.354 L 3.146 5.354 C 2.951 5.158 2.951 4.842 3.146 4.646 C 3.342 4.451 3.658 4.451 3.854 4.646 L 4.5 5.293 L 6.396 3.396 C 6.592 3.201 6.908 3.201 7.104 3.396 C 7.299 3.592 7.299 3.908 7.104 4.104 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
        </div>
      </div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        gap: -2,
        padding: "2px 0px 2px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xxs) * 1px)",
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
          color: "var(--neutral-foreground-1-rest)",
          flexShrink: 0,
        }}>{props.primaryString}</span>
        {props.subtext && (
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            lineHeight: "16px",
            color: "var(--neutral-foreground-2-rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.secondaryString}</span>
          {props.tertiaryText && (
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            lineHeight: "16px",
            color: "var(--neutral-foreground-2-rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.tertiaryString}</span>
          )}
          {props.quaternaryText && (
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            lineHeight: "16px",
            color: "var(--neutral-foreground-2-rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.quaternaryString}</span>
          )}
        </div>
        )}
      </div>
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--avatar-spacing-2) * 1px)",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        borderRadius: 9999,
        borderTop: "1px solid rgba(255,255,255,0)",
        borderRight: "1px solid rgba(255,255,255,0)",
        borderBottom: "1px solid rgba(255,255,255,0)",
        borderLeft: "1px solid rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div className="fig-asset-1ca2b212726726ee" style={{
          position: "relative",
          width: 32,
          height: 32,
          borderRadius: 9999,
          borderTop: "1px solid rgba(255,255,255,0)",
          borderRight: "1px solid rgba(255,255,255,0)",
          borderBottom: "1px solid rgba(255,255,255,0)",
          borderLeft: "1px solid rgba(255,255,255,0)",
          flexShrink: 0,
        }} />
        <div style={{
          position: "absolute",
          left: 22,
          top: 22,
          borderRadius: 9999,
          backgroundColor: "var(--neutral-background-1-rest)",
          boxShadow: "0 0 0 2px var(--neutral-background-1-rest)",
          display: "flex",
          flexDirection: "row",
          alignItems: "flex-start",
          flexWrap: "nowrap",
        }}>
          <div style={{
            position: "relative",
            width: 10,
            height: 10,
            flexShrink: 0,
          }}>
            <svg width={10} height={10} viewBox="0 0 10 10" fill="none" style={{
              position: "absolute",
              left: 0,
              top: 0,
              width: 10,
              height: 10,
              color: "var(--status-available-foreground-3-rest)",
            }}>
              <path d={"M 5 10 C 7.761 10 10 7.761 10 5 C 10 2.239 7.761 0 5 0 C 2.239 0 0 2.239 0 5 C 0 7.761 2.239 10 5 10 Z M 7.104 4.104 L 4.854 6.354 C 4.658 6.549 4.342 6.549 4.146 6.354 L 3.146 5.354 C 2.951 5.158 2.951 4.842 3.146 4.646 C 3.342 4.451 3.658 4.451 3.854 4.646 L 4.5 5.293 L 6.396 3.396 C 6.592 3.201 6.908 3.201 7.104 3.396 C 7.299 3.592 7.299 3.908 7.104 4.104 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
        </div>
      </div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        gap: -2,
        padding: "2px 0px 2px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xxs) * 1px)",
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
          color: "var(--neutral-foreground-1-rest)",
          flexShrink: 0,
        }}>{props.primaryString}</span>
        {props.subtext && (
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            lineHeight: "16px",
            color: "var(--neutral-foreground-2-rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.secondaryString}</span>
          {props.tertiaryText && (
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            lineHeight: "16px",
            color: "var(--neutral-foreground-2-rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.tertiaryString}</span>
          )}
          {props.quaternaryText && (
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            lineHeight: "16px",
            color: "var(--neutral-foreground-2-rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.quaternaryString}</span>
          )}
        </div>
        )}
      </div>
    </div>
  );
  const __body2 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--avatar-spacing-2) * 1px)",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        borderRadius: 9999,
        borderTop: "2px solid rgba(255,255,255,0)",
        borderRight: "2px solid rgba(255,255,255,0)",
        borderBottom: "2px solid rgba(255,255,255,0)",
        borderLeft: "2px solid rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div className="fig-asset-1ca2b212726726ee" style={{
          position: "relative",
          width: 40,
          height: 40,
          borderRadius: 9999,
          borderTop: "2px solid rgba(255,255,255,0)",
          borderRight: "2px solid rgba(255,255,255,0)",
          borderBottom: "2px solid rgba(255,255,255,0)",
          borderLeft: "2px solid rgba(255,255,255,0)",
          flexShrink: 0,
        }} />
        <div style={{
          position: "absolute",
          left: 28,
          top: 28,
          borderRadius: 9999,
          backgroundColor: "var(--neutral-background-1-rest)",
          boxShadow: "0 0 0 2px var(--neutral-background-1-rest)",
          display: "flex",
          flexDirection: "row",
          alignItems: "flex-start",
          flexWrap: "nowrap",
        }}>
          <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
          }}>
            <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
              position: "absolute",
              left: 0,
              top: 0,
              width: 12,
              height: 12,
              color: "var(--status-available-foreground-3-rest)",
            }}>
              <path d={"M 6 12 C 9.314 12 12 9.314 12 6 C 12 2.686 9.314 0 6 0 C 2.686 0 0 2.686 0 6 C 0 9.314 2.686 12 6 12 Z M 8.53 5.28 L 5.78 8.03 C 5.487 8.323 5.013 8.323 4.72 8.03 L 3.72 7.03 C 3.427 6.737 3.427 6.263 3.72 5.97 C 4.013 5.677 4.487 5.677 4.78 5.97 L 5.25 6.439 L 7.47 4.22 C 7.763 3.927 8.237 3.927 8.53 4.22 C 8.823 4.513 8.823 4.987 8.53 5.28 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
        </div>
      </div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        gap: -2,
        padding: "2px 0px 2px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xxs) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 16,
          whiteSpace: "nowrap",
          lineHeight: "22px",
          color: "var(--neutral-foreground-1-rest)",
          flexShrink: 0,
        }}>{props.primaryString}</span>
        {props.subtext && (
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 14,
            lineHeight: "20px",
            color: "var(--neutral-foreground-2-rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.secondaryString}</span>
          {props.tertiaryText && (
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 14,
            lineHeight: "20px",
            color: "var(--neutral-foreground-2-rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.tertiaryString}</span>
          )}
          {props.quaternaryText && (
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 14,
            lineHeight: "20px",
            color: "var(--neutral-foreground-2-rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.quaternaryString}</span>
          )}
        </div>
        )}
      </div>
    </div>
  );
  const __body3 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--avatar-spacing-2) * 1px)",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        borderRadius: 9999,
        borderTop: "2px solid rgba(255,255,255,0)",
        borderRight: "2px solid rgba(255,255,255,0)",
        borderBottom: "2px solid rgba(255,255,255,0)",
        borderLeft: "2px solid rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div className="fig-asset-1ca2b212726726ee" style={{
          position: "relative",
          width: 40,
          height: 40,
          borderRadius: 9999,
          borderTop: "2px solid rgba(255,255,255,0)",
          borderRight: "2px solid rgba(255,255,255,0)",
          borderBottom: "2px solid rgba(255,255,255,0)",
          borderLeft: "2px solid rgba(255,255,255,0)",
          flexShrink: 0,
        }} />
        <div style={{
          position: "absolute",
          left: 28,
          top: 28,
          borderRadius: 9999,
          backgroundColor: "var(--neutral-background-1-rest)",
          boxShadow: "0 0 0 2px var(--neutral-background-1-rest)",
          display: "flex",
          flexDirection: "row",
          alignItems: "flex-start",
          flexWrap: "nowrap",
        }}>
          <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
          }}>
            <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
              position: "absolute",
              left: 0,
              top: 0,
              width: 12,
              height: 12,
              color: "var(--status-available-foreground-3-rest)",
            }}>
              <path d={"M 6 12 C 9.314 12 12 9.314 12 6 C 12 2.686 9.314 0 6 0 C 2.686 0 0 2.686 0 6 C 0 9.314 2.686 12 6 12 Z M 8.53 5.28 L 5.78 8.03 C 5.487 8.323 5.013 8.323 4.72 8.03 L 3.72 7.03 C 3.427 6.737 3.427 6.263 3.72 5.97 C 4.013 5.677 4.487 5.677 4.78 5.97 L 5.25 6.439 L 7.47 4.22 C 7.763 3.927 8.237 3.927 8.53 4.22 C 8.823 4.513 8.823 4.987 8.53 5.28 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
        </div>
      </div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        gap: -2,
        padding: "2px 0px 2px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xxs) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 16,
          whiteSpace: "nowrap",
          lineHeight: "22px",
          color: "var(--neutral-foreground-1-rest)",
          flexShrink: 0,
        }}>{props.primaryString}</span>
        {props.subtext && (
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 14,
            lineHeight: "20px",
            color: "var(--neutral-foreground-2-rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.secondaryString}</span>
          {props.tertiaryText && (
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 14,
            lineHeight: "20px",
            color: "var(--neutral-foreground-2-rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.tertiaryString}</span>
          )}
          {props.quaternaryText && (
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 14,
            lineHeight: "20px",
            color: "var(--neutral-foreground-2-rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.quaternaryString}</span>
          )}
        </div>
        )}
      </div>
    </div>
  );
  const __body4 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--avatar-spacing-2) * 1px)",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        gap: -2,
        padding: "2px 0px 2px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xxs) * 1px)",
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
          color: "var(--neutral-foreground-1-rest)",
          flexShrink: 0,
        }}>{props.primaryString}</span>
        {props.subtext && (
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            lineHeight: "16px",
            color: "var(--neutral-foreground-2-rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.secondaryString}</span>
          {props.tertiaryText && (
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            lineHeight: "16px",
            color: "var(--neutral-foreground-2-rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.tertiaryString}</span>
          )}
          {props.quaternaryText && (
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            lineHeight: "16px",
            color: "var(--neutral-foreground-2-rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.quaternaryString}</span>
          )}
        </div>
        )}
      </div>
      <div style={{
        position: "relative",
        borderRadius: 9999,
        borderTop: "1px solid rgba(255,255,255,0)",
        borderRight: "1px solid rgba(255,255,255,0)",
        borderBottom: "1px solid rgba(255,255,255,0)",
        borderLeft: "1px solid rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div className="fig-asset-1ca2b212726726ee" style={{
          position: "relative",
          width: 32,
          height: 32,
          borderRadius: 9999,
          borderTop: "1px solid rgba(255,255,255,0)",
          borderRight: "1px solid rgba(255,255,255,0)",
          borderBottom: "1px solid rgba(255,255,255,0)",
          borderLeft: "1px solid rgba(255,255,255,0)",
          flexShrink: 0,
        }} />
        <div style={{
          position: "absolute",
          left: 22,
          top: 22,
          borderRadius: 9999,
          backgroundColor: "var(--neutral-background-1-rest)",
          boxShadow: "0 0 0 2px var(--neutral-background-1-rest)",
          display: "flex",
          flexDirection: "row",
          alignItems: "flex-start",
          flexWrap: "nowrap",
        }}>
          <div style={{
            position: "relative",
            width: 10,
            height: 10,
            flexShrink: 0,
          }}>
            <svg width={10} height={10} viewBox="0 0 10 10" fill="none" style={{
              position: "absolute",
              left: 0,
              top: 0,
              width: 10,
              height: 10,
              color: "var(--status-available-foreground-3-rest)",
            }}>
              <path d={"M 5 10 C 7.761 10 10 7.761 10 5 C 10 2.239 7.761 0 5 0 C 2.239 0 0 2.239 0 5 C 0 7.761 2.239 10 5 10 Z M 7.104 4.104 L 4.854 6.354 C 4.658 6.549 4.342 6.549 4.146 6.354 L 3.146 5.354 C 2.951 5.158 2.951 4.842 3.146 4.646 C 3.342 4.451 3.658 4.451 3.854 4.646 L 4.5 5.293 L 6.396 3.396 C 6.592 3.201 6.908 3.201 7.104 3.396 C 7.299 3.592 7.299 3.908 7.104 4.104 Z"} fill="currentColor" fillRule="nonzero" />
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
      gap: "calc(var(--avatar-spacing-2) * 1px)",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        gap: -2,
        padding: "2px 0px 2px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xxs) * 1px)",
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
          color: "var(--neutral-foreground-1-rest)",
          flexShrink: 0,
        }}>{props.primaryString}</span>
        {props.subtext && (
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            lineHeight: "16px",
            color: "var(--neutral-foreground-2-rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.secondaryString}</span>
          {props.tertiaryText && (
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            lineHeight: "16px",
            color: "var(--neutral-foreground-2-rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.tertiaryString}</span>
          )}
          {props.quaternaryText && (
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            lineHeight: "16px",
            color: "var(--neutral-foreground-2-rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.quaternaryString}</span>
          )}
        </div>
        )}
      </div>
      <div style={{
        position: "relative",
        borderRadius: 9999,
        borderTop: "1px solid rgba(255,255,255,0)",
        borderRight: "1px solid rgba(255,255,255,0)",
        borderBottom: "1px solid rgba(255,255,255,0)",
        borderLeft: "1px solid rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div className="fig-asset-1ca2b212726726ee" style={{
          position: "relative",
          width: 32,
          height: 32,
          borderRadius: 9999,
          borderTop: "1px solid rgba(255,255,255,0)",
          borderRight: "1px solid rgba(255,255,255,0)",
          borderBottom: "1px solid rgba(255,255,255,0)",
          borderLeft: "1px solid rgba(255,255,255,0)",
          flexShrink: 0,
        }} />
        <div style={{
          position: "absolute",
          left: 22,
          top: 22,
          borderRadius: 9999,
          backgroundColor: "var(--neutral-background-1-rest)",
          boxShadow: "0 0 0 2px var(--neutral-background-1-rest)",
          display: "flex",
          flexDirection: "row",
          alignItems: "flex-start",
          flexWrap: "nowrap",
        }}>
          <div style={{
            position: "relative",
            width: 10,
            height: 10,
            flexShrink: 0,
          }}>
            <svg width={10} height={10} viewBox="0 0 10 10" fill="none" style={{
              position: "absolute",
              left: 0,
              top: 0,
              width: 10,
              height: 10,
              color: "var(--status-available-foreground-3-rest)",
            }}>
              <path d={"M 5 10 C 7.761 10 10 7.761 10 5 C 10 2.239 7.761 0 5 0 C 2.239 0 0 2.239 0 5 C 0 7.761 2.239 10 5 10 Z M 7.104 4.104 L 4.854 6.354 C 4.658 6.549 4.342 6.549 4.146 6.354 L 3.146 5.354 C 2.951 5.158 2.951 4.842 3.146 4.646 C 3.342 4.451 3.658 4.451 3.854 4.646 L 4.5 5.293 L 6.396 3.396 C 6.592 3.201 6.908 3.201 7.104 3.396 C 7.299 3.592 7.299 3.908 7.104 4.104 Z"} fill="currentColor" fillRule="nonzero" />
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
      gap: "calc(var(--avatar-spacing-2) * 1px)",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        gap: -2,
        padding: "2px 0px 2px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xxs) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 16,
          whiteSpace: "nowrap",
          lineHeight: "22px",
          color: "var(--neutral-foreground-1-rest)",
          flexShrink: 0,
        }}>{props.primaryString}</span>
        {props.subtext && (
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 14,
            lineHeight: "20px",
            color: "var(--neutral-foreground-2-rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.secondaryString}</span>
          {props.tertiaryText && (
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 14,
            lineHeight: "20px",
            color: "var(--neutral-foreground-2-rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.tertiaryString}</span>
          )}
          {props.quaternaryText && (
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 14,
            lineHeight: "20px",
            color: "var(--neutral-foreground-2-rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.quaternaryString}</span>
          )}
        </div>
        )}
      </div>
      <div style={{
        position: "relative",
        borderRadius: 9999,
        borderTop: "2px solid rgba(255,255,255,0)",
        borderRight: "2px solid rgba(255,255,255,0)",
        borderBottom: "2px solid rgba(255,255,255,0)",
        borderLeft: "2px solid rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div className="fig-asset-1ca2b212726726ee" style={{
          position: "relative",
          width: 40,
          height: 40,
          borderRadius: 9999,
          borderTop: "2px solid rgba(255,255,255,0)",
          borderRight: "2px solid rgba(255,255,255,0)",
          borderBottom: "2px solid rgba(255,255,255,0)",
          borderLeft: "2px solid rgba(255,255,255,0)",
          flexShrink: 0,
        }} />
        <div style={{
          position: "absolute",
          left: 28,
          top: 28,
          borderRadius: 9999,
          backgroundColor: "var(--neutral-background-1-rest)",
          boxShadow: "0 0 0 2px var(--neutral-background-1-rest)",
          display: "flex",
          flexDirection: "row",
          alignItems: "flex-start",
          flexWrap: "nowrap",
        }}>
          <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
          }}>
            <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
              position: "absolute",
              left: 0,
              top: 0,
              width: 12,
              height: 12,
              color: "var(--status-available-foreground-3-rest)",
            }}>
              <path d={"M 6 12 C 9.314 12 12 9.314 12 6 C 12 2.686 9.314 0 6 0 C 2.686 0 0 2.686 0 6 C 0 9.314 2.686 12 6 12 Z M 8.53 5.28 L 5.78 8.03 C 5.487 8.323 5.013 8.323 4.72 8.03 L 3.72 7.03 C 3.427 6.737 3.427 6.263 3.72 5.97 C 4.013 5.677 4.487 5.677 4.78 5.97 L 5.25 6.439 L 7.47 4.22 C 7.763 3.927 8.237 3.927 8.53 4.22 C 8.823 4.513 8.823 4.987 8.53 5.28 Z"} fill="currentColor" fillRule="nonzero" />
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
      gap: "calc(var(--avatar-spacing-2) * 1px)",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        gap: -2,
        padding: "2px 0px 2px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xxs) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 16,
          whiteSpace: "nowrap",
          lineHeight: "22px",
          color: "var(--neutral-foreground-1-rest)",
          flexShrink: 0,
        }}>{props.primaryString}</span>
        {props.subtext && (
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 14,
            lineHeight: "20px",
            color: "var(--neutral-foreground-2-rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.secondaryString}</span>
          {props.tertiaryText && (
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 14,
            lineHeight: "20px",
            color: "var(--neutral-foreground-2-rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.tertiaryString}</span>
          )}
          {props.quaternaryText && (
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 14,
            lineHeight: "20px",
            color: "var(--neutral-foreground-2-rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.quaternaryString}</span>
          )}
        </div>
        )}
      </div>
      <div style={{
        position: "relative",
        borderRadius: 9999,
        borderTop: "2px solid rgba(255,255,255,0)",
        borderRight: "2px solid rgba(255,255,255,0)",
        borderBottom: "2px solid rgba(255,255,255,0)",
        borderLeft: "2px solid rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div className="fig-asset-1ca2b212726726ee" style={{
          position: "relative",
          width: 40,
          height: 40,
          borderRadius: 9999,
          borderTop: "2px solid rgba(255,255,255,0)",
          borderRight: "2px solid rgba(255,255,255,0)",
          borderBottom: "2px solid rgba(255,255,255,0)",
          borderLeft: "2px solid rgba(255,255,255,0)",
          flexShrink: 0,
        }} />
        <div style={{
          position: "absolute",
          left: 28,
          top: 28,
          borderRadius: 9999,
          backgroundColor: "var(--neutral-background-1-rest)",
          boxShadow: "0 0 0 2px var(--neutral-background-1-rest)",
          display: "flex",
          flexDirection: "row",
          alignItems: "flex-start",
          flexWrap: "nowrap",
        }}>
          <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
          }}>
            <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
              position: "absolute",
              left: 0,
              top: 0,
              width: 12,
              height: 12,
              color: "var(--status-available-foreground-3-rest)",
            }}>
              <path d={"M 6 12 C 9.314 12 12 9.314 12 6 C 12 2.686 9.314 0 6 0 C 2.686 0 0 2.686 0 6 C 0 9.314 2.686 12 6 12 Z M 8.53 5.28 L 5.78 8.03 C 5.487 8.323 5.013 8.323 4.72 8.03 L 3.72 7.03 C 3.427 6.737 3.427 6.263 3.72 5.97 C 4.013 5.677 4.487 5.677 4.78 5.97 L 5.25 6.439 L 7.47 4.22 C 7.763 3.927 8.237 3.927 8.53 4.22 C 8.823 4.513 8.823 4.987 8.53 5.28 Z"} fill="currentColor" fillRule="nonzero" />
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
      flexDirection: "column",
      gap: "calc(var(--avatar-spacing-2) * 1px)",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        borderRadius: 9999,
        borderTop: "1px solid rgba(255,255,255,0)",
        borderRight: "1px solid rgba(255,255,255,0)",
        borderBottom: "1px solid rgba(255,255,255,0)",
        borderLeft: "1px solid rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div className="fig-asset-1ca2b212726726ee" style={{
          position: "relative",
          width: 32,
          height: 32,
          borderRadius: 9999,
          borderTop: "1px solid rgba(255,255,255,0)",
          borderRight: "1px solid rgba(255,255,255,0)",
          borderBottom: "1px solid rgba(255,255,255,0)",
          borderLeft: "1px solid rgba(255,255,255,0)",
          flexShrink: 0,
        }} />
        <div style={{
          position: "absolute",
          left: 22,
          top: 22,
          borderRadius: 9999,
          backgroundColor: "var(--neutral-background-1-rest)",
          boxShadow: "0 0 0 2px var(--neutral-background-1-rest)",
          display: "flex",
          flexDirection: "row",
          alignItems: "flex-start",
          flexWrap: "nowrap",
        }}>
          <div style={{
            position: "relative",
            width: 10,
            height: 10,
            flexShrink: 0,
          }}>
            <svg width={10} height={10} viewBox="0 0 10 10" fill="none" style={{
              position: "absolute",
              left: 0,
              top: 0,
              width: 10,
              height: 10,
              color: "var(--status-available-foreground-3-rest)",
            }}>
              <path d={"M 5 10 C 7.761 10 10 7.761 10 5 C 10 2.239 7.761 0 5 0 C 2.239 0 0 2.239 0 5 C 0 7.761 2.239 10 5 10 Z M 7.104 4.104 L 4.854 6.354 C 4.658 6.549 4.342 6.549 4.146 6.354 L 3.146 5.354 C 2.951 5.158 2.951 4.842 3.146 4.646 C 3.342 4.451 3.658 4.451 3.854 4.646 L 4.5 5.293 L 6.396 3.396 C 6.592 3.201 6.908 3.201 7.104 3.396 C 7.299 3.592 7.299 3.908 7.104 4.104 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
        </div>
      </div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        gap: -2,
        padding: "2px 0px 2px 0px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xxs) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 400,
          fontSize: 14,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "20px",
          color: "var(--neutral-foreground-1-rest)",
          flexShrink: 0,
        }}>{props.primaryString}</span>
        {props.subtext && (
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          alignItems: "center",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            textAlign: "center",
            lineHeight: "16px",
            color: "var(--neutral-foreground-2-rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.secondaryString}</span>
          {props.tertiaryText && (
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            textAlign: "center",
            lineHeight: "16px",
            color: "var(--neutral-foreground-2-rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.tertiaryString}</span>
          )}
          {props.quaternaryText && (
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            textAlign: "center",
            lineHeight: "16px",
            color: "var(--neutral-foreground-2-rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.quaternaryString}</span>
          )}
        </div>
        )}
      </div>
    </div>
  );
  const __body9 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "column",
      gap: "calc(var(--avatar-spacing-2) * 1px)",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        borderRadius: 9999,
        borderTop: "2px solid rgba(255,255,255,0)",
        borderRight: "2px solid rgba(255,255,255,0)",
        borderBottom: "2px solid rgba(255,255,255,0)",
        borderLeft: "2px solid rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div className="fig-asset-1ca2b212726726ee" style={{
          position: "relative",
          width: 40,
          height: 40,
          borderRadius: 9999,
          borderTop: "2px solid rgba(255,255,255,0)",
          borderRight: "2px solid rgba(255,255,255,0)",
          borderBottom: "2px solid rgba(255,255,255,0)",
          borderLeft: "2px solid rgba(255,255,255,0)",
          flexShrink: 0,
        }} />
        <div style={{
          position: "absolute",
          left: 28,
          top: 28,
          borderRadius: 9999,
          backgroundColor: "var(--neutral-background-1-rest)",
          boxShadow: "0 0 0 2px var(--neutral-background-1-rest)",
          display: "flex",
          flexDirection: "row",
          alignItems: "flex-start",
          flexWrap: "nowrap",
        }}>
          <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
          }}>
            <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
              position: "absolute",
              left: 0,
              top: 0,
              width: 12,
              height: 12,
              color: "var(--status-available-foreground-3-rest)",
            }}>
              <path d={"M 6 12 C 9.314 12 12 9.314 12 6 C 12 2.686 9.314 0 6 0 C 2.686 0 0 2.686 0 6 C 0 9.314 2.686 12 6 12 Z M 8.53 5.28 L 5.78 8.03 C 5.487 8.323 5.013 8.323 4.72 8.03 L 3.72 7.03 C 3.427 6.737 3.427 6.263 3.72 5.97 C 4.013 5.677 4.487 5.677 4.78 5.97 L 5.25 6.439 L 7.47 4.22 C 7.763 3.927 8.237 3.927 8.53 4.22 C 8.823 4.513 8.823 4.987 8.53 5.28 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
        </div>
      </div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        gap: -2,
        padding: "2px 0px 2px 0px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xxs) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 16,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "22px",
          color: "var(--neutral-foreground-1-rest)",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>{props.primaryString}</span>
        {props.subtext && (
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          alignItems: "center",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 14,
            textAlign: "center",
            lineHeight: "20px",
            color: "var(--neutral-foreground-2-rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.secondaryString}</span>
          {props.tertiaryText && (
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 14,
            textAlign: "center",
            lineHeight: "20px",
            color: "var(--neutral-foreground-2-rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.tertiaryString}</span>
          )}
          {props.quaternaryText && (
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 14,
            textAlign: "center",
            lineHeight: "20px",
            color: "var(--neutral-foreground-2-rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.quaternaryString}</span>
          )}
        </div>
        )}
      </div>
    </div>
  );
  const __body10 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--presence-badge-spacing-2) * 1px)",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest)",
        boxShadow: "0 0 0 2px var(--neutral-background-1-rest)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          width: 12,
          height: 12,
          flexShrink: 0,
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 12,
            height: 12,
            color: "var(--status-available-foreground-3-rest)",
          }}>
            <path d={"M 6 12 C 9.314 12 12 9.314 12 6 C 12 2.686 9.314 0 6 0 C 2.686 0 0 2.686 0 6 C 0 9.314 2.686 12 6 12 Z M 8.53 5.28 L 5.78 8.03 C 5.487 8.323 5.013 8.323 4.72 8.03 L 3.72 7.03 C 3.427 6.737 3.427 6.263 3.72 5.97 C 4.013 5.677 4.487 5.677 4.78 5.97 L 5.25 6.439 L 7.47 4.22 C 7.763 3.927 8.237 3.927 8.53 4.22 C 8.823 4.513 8.823 4.987 8.53 5.28 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        gap: -2,
        padding: "2px 0px 2px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xxs) * 1px)",
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
          color: "var(--neutral-foreground-1-rest)",
          flexShrink: 0,
        }}>{props.primaryString}</span>
        {props.subtext && (
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            lineHeight: "16px",
            color: "var(--neutral-foreground-2-rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.secondaryString}</span>
          {props.tertiaryText && (
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            lineHeight: "16px",
            color: "var(--neutral-foreground-2-rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.tertiaryString}</span>
          )}
          {props.quaternaryText && (
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            lineHeight: "16px",
            color: "var(--neutral-foreground-2-rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.quaternaryString}</span>
          )}
        </div>
        )}
      </div>
    </div>
  );
  const __body11 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--presence-badge-spacing-2) * 1px)",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: 10,
        padding: "5px 0px 5px 0px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingTop: "calc(var(--presence-top-alignment-padding-2) * 1px)",
        paddingBottom: "calc(var(--presence-top-alignment-padding-2) * 1px)",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          borderRadius: 9999,
          backgroundColor: "var(--neutral-background-1-rest)",
          boxShadow: "0 0 0 2px var(--neutral-background-1-rest)",
          display: "flex",
          flexDirection: "row",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
        }}>
          <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
          }}>
            <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
              position: "absolute",
              left: 0,
              top: 0,
              width: 12,
              height: 12,
              color: "var(--status-available-foreground-3-rest)",
            }}>
              <path d={"M 6 12 C 9.314 12 12 9.314 12 6 C 12 2.686 9.314 0 6 0 C 2.686 0 0 2.686 0 6 C 0 9.314 2.686 12 6 12 Z M 8.53 5.28 L 5.78 8.03 C 5.487 8.323 5.013 8.323 4.72 8.03 L 3.72 7.03 C 3.427 6.737 3.427 6.263 3.72 5.97 C 4.013 5.677 4.487 5.677 4.78 5.97 L 5.25 6.439 L 7.47 4.22 C 7.763 3.927 8.237 3.927 8.53 4.22 C 8.823 4.513 8.823 4.987 8.53 5.28 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
        </div>
      </div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        gap: -2,
        padding: "2px 0px 2px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xxs) * 1px)",
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
          color: "var(--neutral-foreground-1-rest)",
          flexShrink: 0,
        }}>{props.primaryString}</span>
        {props.subtext && (
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            lineHeight: "16px",
            color: "var(--neutral-foreground-2-rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.secondaryString}</span>
          {props.tertiaryText && (
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            lineHeight: "16px",
            color: "var(--neutral-foreground-2-rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.tertiaryString}</span>
          )}
          {props.quaternaryText && (
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            lineHeight: "16px",
            color: "var(--neutral-foreground-2-rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.quaternaryString}</span>
          )}
        </div>
        )}
      </div>
    </div>
  );
  const __body12 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--presence-badge-spacing-2) * 1px)",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest)",
        boxShadow: "0 0 0 2px var(--neutral-background-1-rest)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <svg width={20} height={20} viewBox="0 0 20 20" fill="none" style={{
          position: "relative",
          width: 20,
          height: 20,
          flexShrink: 0,
          color: "var(--status-available-foreground-3-rest)",
        }}>
          <path d={"M 10 20 C 15.523 20 20 15.523 20 10 C 20 4.477 15.523 0 10 0 C 4.477 0 0 4.477 0 10 C 0 15.523 4.477 20 10 20 Z M 14.207 8.207 L 9.707 12.707 C 9.317 13.098 8.683 13.098 8.293 12.707 L 6.293 10.707 C 5.902 10.317 5.902 9.683 6.293 9.293 C 6.683 8.902 7.317 8.902 7.707 9.293 L 9 10.586 L 12.793 6.793 C 13.183 6.402 13.817 6.402 14.207 6.793 C 14.598 7.183 14.598 7.817 14.207 8.207 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        gap: -2,
        padding: "2px 0px 2px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xxs) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 16,
          whiteSpace: "nowrap",
          lineHeight: "22px",
          color: "var(--neutral-foreground-1-rest)",
          flexShrink: 0,
        }}>{props.primaryString}</span>
        {props.subtext && (
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 14,
            lineHeight: "20px",
            color: "var(--neutral-foreground-2-rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.secondaryString}</span>
          {props.tertiaryText && (
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 14,
            lineHeight: "20px",
            color: "var(--neutral-foreground-2-rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.tertiaryString}</span>
          )}
          {props.quaternaryText && (
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 14,
            lineHeight: "20px",
            color: "var(--neutral-foreground-2-rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.quaternaryString}</span>
          )}
        </div>
        )}
      </div>
    </div>
  );
  const __body13 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--presence-badge-spacing-2) * 1px)",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: 10,
        padding: "2px 0px 2px 0px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingTop: "calc(var(--presence-top-alignment-padding-2) * 1px)",
        paddingBottom: "calc(var(--presence-top-alignment-padding-2) * 1px)",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          borderRadius: 9999,
          backgroundColor: "var(--neutral-background-1-rest)",
          boxShadow: "0 0 0 2px var(--neutral-background-1-rest)",
          display: "flex",
          flexDirection: "row",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
        }}>
          <svg width={20} height={20} viewBox="0 0 20 20" fill="none" style={{
            position: "relative",
            width: 20,
            height: 20,
            flexShrink: 0,
            color: "var(--status-available-foreground-3-rest)",
          }}>
            <path d={"M 10 20 C 15.523 20 20 15.523 20 10 C 20 4.477 15.523 0 10 0 C 4.477 0 0 4.477 0 10 C 0 15.523 4.477 20 10 20 Z M 14.207 8.207 L 9.707 12.707 C 9.317 13.098 8.683 13.098 8.293 12.707 L 6.293 10.707 C 5.902 10.317 5.902 9.683 6.293 9.293 C 6.683 8.902 7.317 8.902 7.707 9.293 L 9 10.586 L 12.793 6.793 C 13.183 6.402 13.817 6.402 14.207 6.793 C 14.598 7.183 14.598 7.817 14.207 8.207 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        gap: -2,
        padding: "2px 0px 2px 0px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingTop: "calc(var(--spacing-vertical-xxs) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-xxs) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 16,
          whiteSpace: "nowrap",
          lineHeight: "22px",
          color: "var(--neutral-foreground-1-rest)",
          flexShrink: 0,
        }}>{props.primaryString}</span>
        {props.subtext && (
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "column",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 14,
            lineHeight: "20px",
            color: "var(--neutral-foreground-2-rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.secondaryString}</span>
          {props.tertiaryText && (
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 14,
            lineHeight: "20px",
            color: "var(--neutral-foreground-2-rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.tertiaryString}</span>
          )}
          {props.quaternaryText && (
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 14,
            lineHeight: "20px",
            color: "var(--neutral-foreground-2-rest)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.quaternaryString}</span>
          )}
        </div>
        )}
      </div>
    </div>
  );
  const __body14 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--presence-badge-spacing-2) * 1px)",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        borderRadius: 9999,
        backgroundColor: "var(--neutral-background-1-rest)",
        boxShadow: "0 0 0 1px var(--neutral-background-1-rest)",
        display: "flex",
        flexDirection: "row",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <svg width={6} height={6} viewBox="0 0 6 6" fill="none" style={{
          position: "relative",
          width: 6,
          height: 6,
          flexShrink: 0,
          color: "var(--status-available-foreground-3-rest)",
        }}>
          <path d={"M 3 6 C 4.657 6 6 4.657 6 3 C 6 1.343 4.657 0 3 0 C 1.343 0 0 1.343 0 3 C 0 4.657 1.343 6 3 6 Z M 4.262 2.462 L 2.912 3.812 C 2.795 3.929 2.605 3.929 2.488 3.812 L 1.888 3.212 C 1.771 3.095 1.771 2.905 1.888 2.788 C 2.005 2.671 2.195 2.671 2.312 2.788 L 2.7 3.176 L 3.838 2.038 C 3.955 1.921 4.145 1.921 4.262 2.038 C 4.379 2.155 4.379 2.345 4.262 2.462 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      <span style={{
        position: "relative",
        width: 74,
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 12,
        lineHeight: "16px",
        color: "var(--neutral-foreground-1-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.primaryString}</span>
    </div>
  );
  const __body15 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--presence-badge-spacing-2) * 1px)",
      alignItems: "center",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: 10,
        alignItems: "center",
        flexWrap: "nowrap",
        paddingTop: "calc(var(--presence-top-alignment-padding-2) * 1px)",
        paddingBottom: "calc(var(--presence-top-alignment-padding-2) * 1px)",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          borderRadius: 9999,
          backgroundColor: "var(--neutral-background-1-rest)",
          boxShadow: "0 0 0 1px var(--neutral-background-1-rest)",
          display: "flex",
          flexDirection: "row",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
        }}>
          <svg width={6} height={6} viewBox="0 0 6 6" fill="none" style={{
            position: "relative",
            width: 6,
            height: 6,
            flexShrink: 0,
            color: "var(--status-available-foreground-3-rest)",
          }}>
            <path d={"M 3 6 C 4.657 6 6 4.657 6 3 C 6 1.343 4.657 0 3 0 C 1.343 0 0 1.343 0 3 C 0 4.657 1.343 6 3 6 Z M 4.262 2.462 L 2.912 3.812 C 2.795 3.929 2.605 3.929 2.488 3.812 L 1.888 3.212 C 1.771 3.095 1.771 2.905 1.888 2.788 C 2.005 2.671 2.195 2.671 2.312 2.788 L 2.7 3.176 L 3.838 2.038 C 3.955 1.921 4.145 1.921 4.262 2.038 C 4.379 2.155 4.379 2.345 4.262 2.462 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      <span style={{
        position: "relative",
        width: 74,
        fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
        fontWeight: 400,
        fontSize: 12,
        lineHeight: "16px",
        color: "var(--neutral-foreground-1-rest)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>{props.primaryString}</span>
    </div>
  );
  const __impls = {
    // figma: Layout=Text after (Default), Style=Avatar (Default), Size=Medium (Default), Alignment=Center (Default)
    "layout=text after (default)|style2=avatar (default)|size=medium (default)|alignment=center (default)": __body0,
    // figma: Layout=Text after (Default), Style=Avatar (Default), Size=Medium (Default), Alignment=Top
    "layout=text after (default)|style2=avatar (default)|size=medium (default)|alignment=top": __body1,
    // figma: Layout=Text after (Default), Style=Avatar (Default), Size=Large, Alignment=Center (Default)
    "layout=text after (default)|style2=avatar (default)|size=lg|alignment=center (default)": __body2,
    // figma: Layout=Text after (Default), Style=Avatar (Default), Size=Large, Alignment=Top
    "layout=text after (default)|style2=avatar (default)|size=lg|alignment=top": __body3,
    // figma: Layout=Text before (Avatar only), Style=Avatar (Default), Size=Medium (Default), Alignment=Center (Default)
    "layout=text before (avatar only)|style2=avatar (default)|size=medium (default)|alignment=center (default)": __body4,
    // figma: Layout=Text before (Avatar only), Style=Avatar (Default), Size=Medium (Default), Alignment=Top
    "layout=text before (avatar only)|style2=avatar (default)|size=medium (default)|alignment=top": __body5,
    // figma: Layout=Text before (Avatar only), Style=Avatar (Default), Size=Large, Alignment=Center (Default)
    "layout=text before (avatar only)|style2=avatar (default)|size=lg|alignment=center (default)": __body6,
    // figma: Layout=Text before (Avatar only), Style=Avatar (Default), Size=Large, Alignment=Top
    "layout=text before (avatar only)|style2=avatar (default)|size=lg|alignment=top": __body7,
    // figma: Layout=Text below (Avatar only), Style=Avatar (Default), Size=Medium (Default), Alignment=Center (Default)
    "layout=text below (avatar only)|style2=avatar (default)|size=medium (default)|alignment=center (default)": __body8,
    // figma: Layout=Text below (Avatar only), Style=Avatar (Default), Size=Large, Alignment=Center (Default)
    "layout=text below (avatar only)|style2=avatar (default)|size=lg|alignment=center (default)": __body9,
    // figma: Layout=Text after (Default), Style=Presence badge, Size=Medium (Default), Alignment=Center (Default)
    "layout=text after (default)|style2=presence badge|size=medium (default)|alignment=center (default)": __body10,
    // figma: Layout=Text after (Default), Style=Presence badge, Size=Medium (Default), Alignment=Top
    "layout=text after (default)|style2=presence badge|size=medium (default)|alignment=top": __body11,
    // figma: Layout=Text after (Default), Style=Presence badge, Size=Large, Alignment=Center (Default)
    "layout=text after (default)|style2=presence badge|size=lg|alignment=center (default)": __body12,
    // figma: Layout=Text after (Default), Style=Presence badge, Size=Large, Alignment=Top
    "layout=text after (default)|style2=presence badge|size=lg|alignment=top": __body13,
    // figma: Layout=Text after (Default), Style=Presence badge, Size=Small (Badge only), Alignment=Center (Default)
    "layout=text after (default)|style2=presence badge|size=small (badge only)|alignment=center (default)": __body14,
    // figma: Layout=Text after (Default), Style=Presence badge, Size=Small (Badge only), Alignment=Top
    "layout=text after (default)|style2=presence badge|size=small (badge only)|alignment=top": __body15,
  };
  return (__impls[__vkey(props)] ?? __body0)();
}
export default Persona;
