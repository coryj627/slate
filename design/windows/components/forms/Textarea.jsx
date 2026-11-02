import { __slateNorm } from '../internal/slateProps.jsx';
import { ResizeHandler } from '../internal/ResizeHandler.jsx';

// figma node: 9064:11896 Textarea (63 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "style2=" + __venc(p.style2) + '|' + "size=" + __venc(p.size) + '|' + "state=" + __venc(p.state);

export function Textarea(_p = {}) {
  _p = __slateNorm(_p, {"style2":["outline","filled darker","filled lighter"],"size":["sm","medium (default)","lg"],"state":["rest","hover","pressed","focus","error","disabled","read only"]});
  const props = { ..._p, style2: _p.style2 ?? "outline", size: _p.size ?? "sm", state: _p.state ?? "rest", showResizeHandler: _p.showResizeHandler ?? false };
  const __body0 = () => (
    <div className={props.className} style={{
      width: 280,
      height: 52,
      overflow: "hidden",
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-1-rest)",
        borderTop: "1px solid var(--neutral-stroke-1-rest)",
        borderRight: "1px solid var(--neutral-stroke-1-rest)",
        borderBottom: "1px solid var(--neutral-stroke-1-rest)",
        borderLeft: "1px solid var(--neutral-stroke-1-rest)",
        display: "flex",
        flexDirection: "row",
        padding: "0px 10px 0px 10px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          display: "flex",
          flexDirection: "row",
          padding: "6px 2px 6px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
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
            color: "var(--neutral-foreground-4-rest)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>{props.placeholder ?? "Placeholder text"}</span>
        </div>
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 51,
        width: 280,
        height: 1,
        borderRadius: 4,
        backgroundColor: "var(--neutral-stroke-accessible-rest)",
      }} />
      {props.showResizeHandler && (
      <div style={{
          position: "absolute",
          left: 268,
          top: 40,
          width: 12,
          height: 12,
        }}>{props.icon1 ?? <ResizeHandler />}</div>
      )}
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: 280,
      height: 52,
      overflow: "hidden",
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-3-rest)",
        display: "flex",
        flexDirection: "row",
        padding: "0px 10px 0px 10px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          display: "flex",
          flexDirection: "row",
          padding: "6px 2px 6px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
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
            color: "var(--neutral-foreground-4-rest)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>{props.placeholder ?? "Placeholder text"}</span>
        </div>
      </div>
      {props.showResizeHandler && (
      <div style={{
          position: "absolute",
          left: 268,
          top: 40,
          width: 12,
          height: 12,
        }}>{props.icon1 ?? <ResizeHandler />}</div>
      )}
    </div>
  );
  const __body2 = () => (
    <div className={props.className} style={{
      width: 280,
      height: 52,
      overflow: "hidden",
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-1-rest)",
        display: "flex",
        flexDirection: "row",
        padding: "0px 10px 0px 10px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          display: "flex",
          flexDirection: "row",
          padding: "6px 2px 6px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
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
            color: "var(--neutral-foreground-4-rest)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>{props.placeholder ?? "Placeholder text"}</span>
        </div>
      </div>
      {props.showResizeHandler && (
      <div style={{
          position: "absolute",
          left: 268,
          top: 40,
          width: 12,
          height: 12,
        }}>{props.icon1 ?? <ResizeHandler />}</div>
      )}
    </div>
  );
  const __body3 = () => (
    <div className={props.className} style={{
      width: 280,
      height: 52,
      overflow: "hidden",
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-1-rest)",
        borderTop: "1px solid var(--neutral-stroke-1-hover)",
        borderRight: "1px solid var(--neutral-stroke-1-hover)",
        borderBottom: "1px solid var(--neutral-stroke-1-hover)",
        borderLeft: "1px solid var(--neutral-stroke-1-hover)",
        display: "flex",
        flexDirection: "row",
        padding: "0px 10px 0px 10px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          display: "flex",
          flexDirection: "row",
          padding: "6px 2px 6px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
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
            color: "var(--neutral-foreground-4-rest)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>{props.placeholder ?? "Placeholder text"}</span>
        </div>
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 51,
        width: 280,
        height: 1,
        borderRadius: 4,
        backgroundColor: "var(--neutral-stroke-accessible-hover)",
      }} />
      {props.showResizeHandler && (
      <div style={{
          position: "absolute",
          left: 268,
          top: 40,
          width: 12,
          height: 12,
        }}>{props.icon1 ?? <ResizeHandler />}</div>
      )}
    </div>
  );
  const __body4 = () => (
    <div className={props.className} style={{
      width: 280,
      height: 52,
      overflow: "hidden",
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
      position: "relative",
      color: "var(--brand-stroke-compound-pressed)",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-1-rest)",
        borderTop: "1px solid var(--neutral-stroke-1-pressed)",
        borderRight: "1px solid var(--neutral-stroke-1-pressed)",
        borderBottom: "1px solid var(--neutral-stroke-1-pressed)",
        borderLeft: "1px solid var(--neutral-stroke-1-pressed)",
        display: "flex",
        flexDirection: "row",
        padding: "0px 10px 0px 10px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          display: "flex",
          flexDirection: "row",
          padding: "6px 2px 6px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
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
            color: "var(--neutral-foreground-4-rest)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>{props.placeholder ?? "Placeholder text"}</span>
        </div>
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 50,
        width: 280,
        height: 2,
        borderRadius: 4,
        backgroundColor: "var(--neutral-stroke-accessible-pressed)",
      }} />
      <svg width={280} height={2} viewBox="0 0 280 2" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 50,
        width: 280,
        height: 2,
        borderRadius: 4,
      }}>
        <path d={"M 0 0 L 280 0 L 279.596 2 L 0.369 2 L 0 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.showResizeHandler && (
      <div style={{
          position: "absolute",
          left: 268,
          top: 40,
          width: 12,
          height: 12,
        }}>{props.icon1 ?? <ResizeHandler />}</div>
      )}
    </div>
  );
  const __body5 = () => (
    <div className={props.className} style={{
      width: 280,
      height: 52,
      overflow: "hidden",
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
      position: "relative",
      color: "var(--brand-stroke-compound-pressed)",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-3-rest)",
        display: "flex",
        flexDirection: "row",
        padding: "0px 10px 0px 10px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          display: "flex",
          flexDirection: "row",
          padding: "6px 2px 6px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
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
            color: "var(--neutral-foreground-4-rest)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>{props.placeholder ?? "Placeholder text"}</span>
        </div>
      </div>
      <svg width={280} height={2} viewBox="0 0 280 2" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 50,
        width: 280,
        height: 2,
        borderRadius: 4,
      }}>
        <path d={"M 0 0 L 280 0 L 279.596 2 L 0.369 2 L 0 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.showResizeHandler && (
      <div style={{
          position: "absolute",
          left: 268,
          top: 40,
          width: 12,
          height: 12,
        }}>{props.icon1 ?? <ResizeHandler />}</div>
      )}
    </div>
  );
  const __body6 = () => (
    <div className={props.className} style={{
      width: 280,
      height: 52,
      overflow: "hidden",
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
      position: "relative",
      color: "var(--brand-stroke-compound-pressed)",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-1-rest)",
        display: "flex",
        flexDirection: "row",
        padding: "0px 10px 0px 10px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          display: "flex",
          flexDirection: "row",
          padding: "6px 2px 6px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
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
            color: "var(--neutral-foreground-4-rest)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>{props.placeholder ?? "Placeholder text"}</span>
        </div>
      </div>
      <svg width={280} height={2} viewBox="0 0 280 2" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 50,
        width: 280,
        height: 2,
        borderRadius: 4,
      }}>
        <path d={"M 0 0 L 280 0 L 279.596 2 L 0.369 2 L 0 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.showResizeHandler && (
      <div style={{
          position: "absolute",
          left: 268,
          top: 40,
          width: 12,
          height: 12,
        }}>{props.icon1 ?? <ResizeHandler />}</div>
      )}
    </div>
  );
  const __body7 = () => (
    <div className={props.className} style={{
      width: 280,
      height: 52,
      overflow: "hidden",
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-1-rest)",
        borderTop: "1px solid var(--neutral-stroke-1-rest)",
        borderRight: "1px solid var(--neutral-stroke-1-rest)",
        borderBottom: "1px solid var(--neutral-stroke-1-rest)",
        borderLeft: "1px solid var(--neutral-stroke-1-rest)",
        display: "flex",
        flexDirection: "row",
        padding: "0px 10px 0px 10px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          display: "flex",
          flexDirection: "row",
          padding: "6px 2px 6px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
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
            color: "var(--neutral-foreground-4-rest)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>{props.placeholder ?? "Placeholder text"}</span>
        </div>
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 50,
        width: 280,
        height: 2,
        borderRadius: "0px 0px 4px 4px",
        backgroundColor: "var(--brand-stroke-compound-rest)",
      }} />
      {props.showResizeHandler && (
      <div style={{
          position: "absolute",
          left: 268,
          top: 40,
          width: 12,
          height: 12,
        }}>{props.icon1 ?? <ResizeHandler />}</div>
      )}
    </div>
  );
  const __body8 = () => (
    <div className={props.className} style={{
      width: 280,
      height: 52,
      overflow: "hidden",
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-3-rest)",
        display: "flex",
        flexDirection: "row",
        padding: "0px 10px 0px 10px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          display: "flex",
          flexDirection: "row",
          padding: "6px 2px 6px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
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
            color: "var(--neutral-foreground-4-rest)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>{props.placeholder ?? "Placeholder text"}</span>
        </div>
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 50,
        width: 280,
        height: 2,
        borderRadius: "0px 0px 4px 4px",
        backgroundColor: "var(--brand-stroke-compound-rest)",
      }} />
      {props.showResizeHandler && (
      <div style={{
          position: "absolute",
          left: 268,
          top: 40,
          width: 12,
          height: 12,
        }}>{props.icon1 ?? <ResizeHandler />}</div>
      )}
    </div>
  );
  const __body9 = () => (
    <div className={props.className} style={{
      width: 280,
      height: 52,
      overflow: "hidden",
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-1-rest)",
        display: "flex",
        flexDirection: "row",
        padding: "0px 10px 0px 10px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          display: "flex",
          flexDirection: "row",
          padding: "6px 2px 6px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
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
            color: "var(--neutral-foreground-4-rest)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>{props.placeholder ?? "Placeholder text"}</span>
        </div>
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 50,
        width: 280,
        height: 2,
        borderRadius: "0px 0px 4px 4px",
        backgroundColor: "var(--brand-stroke-compound-rest)",
      }} />
      {props.showResizeHandler && (
      <div style={{
          position: "absolute",
          left: 268,
          top: 40,
          width: 12,
          height: 12,
        }}>{props.icon1 ?? <ResizeHandler />}</div>
      )}
    </div>
  );
  const __body10 = () => (
    <div className={props.className} style={{
      width: 280,
      height: 52,
      overflow: "hidden",
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-1-rest)",
        borderTop: "1px solid var(--status-danger-stroke-2-rest)",
        borderRight: "1px solid var(--status-danger-stroke-2-rest)",
        borderBottom: "1px solid var(--status-danger-stroke-2-rest)",
        borderLeft: "1px solid var(--status-danger-stroke-2-rest)",
        display: "flex",
        flexDirection: "row",
        padding: "0px 10px 0px 10px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          display: "flex",
          flexDirection: "row",
          padding: "6px 2px 6px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
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
            color: "var(--neutral-foreground-4-rest)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>{props.placeholder ?? "Placeholder text"}</span>
        </div>
      </div>
      {props.showResizeHandler && (
      <div style={{
          position: "absolute",
          left: 268,
          top: 40,
          width: 12,
          height: 12,
        }}>{props.icon1 ?? <ResizeHandler />}</div>
      )}
    </div>
  );
  const __body11 = () => (
    <div className={props.className} style={{
      width: 280,
      height: 52,
      overflow: "hidden",
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-3-rest)",
        borderTop: "1px solid var(--status-danger-stroke-2-rest)",
        borderRight: "1px solid var(--status-danger-stroke-2-rest)",
        borderBottom: "1px solid var(--status-danger-stroke-2-rest)",
        borderLeft: "1px solid var(--status-danger-stroke-2-rest)",
        display: "flex",
        flexDirection: "row",
        padding: "0px 10px 0px 10px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          display: "flex",
          flexDirection: "row",
          padding: "6px 2px 6px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
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
            color: "var(--neutral-foreground-4-rest)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>{props.placeholder ?? "Placeholder text"}</span>
        </div>
      </div>
      {props.showResizeHandler && (
      <div style={{
          position: "absolute",
          left: 268,
          top: 40,
          width: 12,
          height: 12,
        }}>{props.icon1 ?? <ResizeHandler />}</div>
      )}
    </div>
  );
  const __body12 = () => (
    <div className={props.className} style={{
      width: 280,
      height: 52,
      overflow: "hidden",
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "rgba(255,255,255,0)",
        borderTop: "1px solid var(--neutral-stroke-disabled-rest)",
        borderRight: "1px solid var(--neutral-stroke-disabled-rest)",
        borderBottom: "1px solid var(--neutral-stroke-disabled-rest)",
        borderLeft: "1px solid var(--neutral-stroke-disabled-rest)",
        display: "flex",
        flexDirection: "row",
        padding: "0px 10px 0px 10px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          display: "flex",
          flexDirection: "row",
          padding: "6px 2px 6px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
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
            color: "var(--neutral-foreground-disabled-rest)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>{props.placeholder ?? "Placeholder text"}</span>
        </div>
      </div>
    </div>
  );
  const __body13 = () => (
    <div className={props.className} style={{
      width: 280,
      height: 52,
      overflow: "hidden",
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "rgba(255,255,255,0)",
        borderTop: "1px solid var(--neutral-stroke-disabled-rest)",
        borderRight: "1px solid var(--neutral-stroke-disabled-rest)",
        borderBottom: "1px solid var(--neutral-stroke-disabled-rest)",
        borderLeft: "1px solid var(--neutral-stroke-disabled-rest)",
        display: "flex",
        flexDirection: "row",
        padding: "0px 10px 0px 10px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          display: "flex",
          flexDirection: "row",
          padding: "6px 2px 6px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
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
            color: "var(--neutral-foreground-1-rest)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>{props.placeholder ?? "Placeholder text"}</span>
        </div>
      </div>
      {props.showResizeHandler && (
      <div style={{
          position: "absolute",
          left: 268,
          top: 40,
          width: 12,
          height: 12,
        }}>{props.icon1 ?? <ResizeHandler />}</div>
      )}
    </div>
  );
  const __body14 = () => (
    <div className={props.className} style={{
      width: 280,
      height: 52,
      overflow: "hidden",
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-1-rest)",
        borderTop: "1px solid var(--neutral-stroke-1-rest)",
        borderRight: "1px solid var(--neutral-stroke-1-rest)",
        borderBottom: "1px solid var(--neutral-stroke-1-rest)",
        borderLeft: "1px solid var(--neutral-stroke-1-rest)",
        display: "flex",
        flexDirection: "row",
        padding: "0px 6px 0px 6px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          display: "flex",
          flexDirection: "row",
          padding: "6px 2px 6px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexGrow: 1,
          alignSelf: "stretch",
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            whiteSpace: "nowrap",
            lineHeight: "16px",
            color: "var(--neutral-foreground-4-rest)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>{props.placeholder ?? "Placeholder text"}</span>
        </div>
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 51,
        width: 280,
        height: 1,
        borderRadius: 4,
        backgroundColor: "var(--neutral-stroke-accessible-rest)",
      }} />
      {props.showResizeHandler && (
      <div style={{
          position: "absolute",
          left: 268,
          top: 40,
          width: 12,
          height: 12,
        }}>{props.icon1 ?? <ResizeHandler />}</div>
      )}
    </div>
  );
  const __body15 = () => (
    <div className={props.className} style={{
      width: 280,
      height: 52,
      overflow: "hidden",
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-3-rest)",
        display: "flex",
        flexDirection: "row",
        padding: "0px 6px 0px 6px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          display: "flex",
          flexDirection: "row",
          padding: "6px 2px 6px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexGrow: 1,
          alignSelf: "stretch",
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            whiteSpace: "nowrap",
            lineHeight: "16px",
            color: "var(--neutral-foreground-4-rest)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>{props.placeholder ?? "Placeholder text"}</span>
        </div>
      </div>
      {props.showResizeHandler && (
      <div style={{
          position: "absolute",
          left: 268,
          top: 40,
          width: 12,
          height: 12,
        }}>{props.icon1 ?? <ResizeHandler />}</div>
      )}
    </div>
  );
  const __body16 = () => (
    <div className={props.className} style={{
      width: 280,
      height: 52,
      overflow: "hidden",
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-1-rest)",
        display: "flex",
        flexDirection: "row",
        padding: "0px 6px 0px 6px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          display: "flex",
          flexDirection: "row",
          padding: "6px 2px 6px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexGrow: 1,
          alignSelf: "stretch",
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            whiteSpace: "nowrap",
            lineHeight: "16px",
            color: "var(--neutral-foreground-4-rest)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>{props.placeholder ?? "Placeholder text"}</span>
        </div>
      </div>
      {props.showResizeHandler && (
      <div style={{
          position: "absolute",
          left: 268,
          top: 40,
          width: 12,
          height: 12,
        }}>{props.icon1 ?? <ResizeHandler />}</div>
      )}
    </div>
  );
  const __body17 = () => (
    <div className={props.className} style={{
      width: 280,
      height: 52,
      overflow: "hidden",
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-1-rest)",
        borderTop: "1px solid var(--neutral-stroke-1-hover)",
        borderRight: "1px solid var(--neutral-stroke-1-hover)",
        borderBottom: "1px solid var(--neutral-stroke-1-hover)",
        borderLeft: "1px solid var(--neutral-stroke-1-hover)",
        display: "flex",
        flexDirection: "row",
        padding: "0px 6px 0px 6px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          display: "flex",
          flexDirection: "row",
          padding: "6px 2px 6px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexGrow: 1,
          alignSelf: "stretch",
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            whiteSpace: "nowrap",
            lineHeight: "16px",
            color: "var(--neutral-foreground-4-rest)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>{props.placeholder ?? "Placeholder text"}</span>
        </div>
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 51,
        width: 280,
        height: 1,
        borderRadius: 4,
        backgroundColor: "var(--neutral-stroke-accessible-hover)",
      }} />
      {props.showResizeHandler && (
      <div style={{
          position: "absolute",
          left: 268,
          top: 40,
          width: 12,
          height: 12,
        }}>{props.icon1 ?? <ResizeHandler />}</div>
      )}
    </div>
  );
  const __body18 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
      position: "relative",
      color: "var(--brand-stroke-compound-pressed)",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        height: 52,
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-1-rest)",
        borderTop: "1px solid var(--neutral-stroke-1-pressed)",
        borderRight: "1px solid var(--neutral-stroke-1-pressed)",
        borderBottom: "1px solid var(--neutral-stroke-1-pressed)",
        borderLeft: "1px solid var(--neutral-stroke-1-pressed)",
        display: "flex",
        flexDirection: "row",
        padding: "0px 6px 0px 6px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          display: "flex",
          flexDirection: "row",
          padding: "6px 2px 6px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexGrow: 1,
          alignSelf: "stretch",
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            whiteSpace: "nowrap",
            lineHeight: "16px",
            color: "var(--neutral-foreground-4-rest)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>{props.placeholder ?? "Placeholder text"}</span>
        </div>
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 50,
        width: 280,
        height: 2,
        borderRadius: 4,
        backgroundColor: "var(--neutral-stroke-accessible-pressed)",
      }} />
      <svg width={280} height={2} viewBox="0 0 280 2" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 50,
        width: 280,
        height: 2,
        borderRadius: 4,
      }}>
        <path d={"M 0 0 L 280 0 L 279.596 2 L 0.369 2 L 0 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.showResizeHandler && (
      <div style={{
          position: "absolute",
          left: 268,
          top: 40,
          width: 12,
          height: 12,
        }}>{props.icon1 ?? <ResizeHandler />}</div>
      )}
    </div>
  );
  const __body19 = () => (
    <div className={props.className} style={{
      width: 280,
      height: 52,
      overflow: "hidden",
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
      position: "relative",
      color: "var(--brand-stroke-compound-pressed)",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-3-rest)",
        display: "flex",
        flexDirection: "row",
        padding: "0px 6px 0px 6px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          display: "flex",
          flexDirection: "row",
          padding: "6px 2px 6px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexGrow: 1,
          alignSelf: "stretch",
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            whiteSpace: "nowrap",
            lineHeight: "16px",
            color: "var(--neutral-foreground-4-rest)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>{props.placeholder ?? "Placeholder text"}</span>
        </div>
      </div>
      <svg width={280} height={2} viewBox="0 0 280 2" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 50,
        width: 280,
        height: 2,
        borderRadius: 4,
      }}>
        <path d={"M 0 0 L 280 0 L 279.596 2 L 0.369 2 L 0 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.showResizeHandler && (
      <div style={{
          position: "absolute",
          left: 268,
          top: 40,
          width: 12,
          height: 12,
        }}>{props.icon1 ?? <ResizeHandler />}</div>
      )}
    </div>
  );
  const __body20 = () => (
    <div className={props.className} style={{
      width: 280,
      overflow: "hidden",
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
      position: "relative",
      color: "var(--brand-stroke-compound-pressed)",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        height: 52,
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-1-rest)",
        display: "flex",
        flexDirection: "row",
        padding: "0px 6px 0px 6px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          display: "flex",
          flexDirection: "row",
          padding: "6px 2px 6px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexGrow: 1,
          alignSelf: "stretch",
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            whiteSpace: "nowrap",
            lineHeight: "16px",
            color: "var(--neutral-foreground-4-rest)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>{props.placeholder ?? "Placeholder text"}</span>
        </div>
      </div>
      <svg width={280} height={2} viewBox="0 0 280 2" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 50,
        width: 280,
        height: 2,
        borderRadius: 4,
      }}>
        <path d={"M 0 0 L 280 0 L 279.596 2 L 0.369 2 L 0 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.showResizeHandler && (
      <div style={{
          position: "absolute",
          left: 268,
          top: 40,
          width: 12,
          height: 12,
        }}>{props.icon1 ?? <ResizeHandler />}</div>
      )}
    </div>
  );
  const __body21 = () => (
    <div className={props.className} style={{
      width: 280,
      height: 52,
      overflow: "hidden",
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-1-rest)",
        borderTop: "1px solid var(--neutral-stroke-1-rest)",
        borderRight: "1px solid var(--neutral-stroke-1-rest)",
        borderBottom: "1px solid var(--neutral-stroke-1-rest)",
        borderLeft: "1px solid var(--neutral-stroke-1-rest)",
        display: "flex",
        flexDirection: "row",
        padding: "0px 6px 0px 6px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          display: "flex",
          flexDirection: "row",
          padding: "6px 2px 6px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexGrow: 1,
          alignSelf: "stretch",
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            whiteSpace: "nowrap",
            lineHeight: "16px",
            color: "var(--neutral-foreground-4-rest)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>{props.placeholder ?? "Placeholder text"}</span>
        </div>
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 50,
        width: 280,
        height: 2,
        borderRadius: "0px 0px 4px 4px",
        backgroundColor: "var(--brand-stroke-compound-rest)",
      }} />
      {props.showResizeHandler && (
      <div style={{
          position: "absolute",
          left: 268,
          top: 40,
          width: 12,
          height: 12,
        }}>{props.icon1 ?? <ResizeHandler />}</div>
      )}
    </div>
  );
  const __body22 = () => (
    <div className={props.className} style={{
      width: 280,
      height: 52,
      overflow: "hidden",
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-3-rest)",
        display: "flex",
        flexDirection: "row",
        padding: "0px 6px 0px 6px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          display: "flex",
          flexDirection: "row",
          padding: "6px 2px 6px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexGrow: 1,
          alignSelf: "stretch",
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            whiteSpace: "nowrap",
            lineHeight: "16px",
            color: "var(--neutral-foreground-4-rest)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>{props.placeholder ?? "Placeholder text"}</span>
        </div>
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 50,
        width: 280,
        height: 2,
        borderRadius: "0px 0px 4px 4px",
        backgroundColor: "var(--brand-stroke-compound-rest)",
      }} />
      {props.showResizeHandler && (
      <div style={{
          position: "absolute",
          left: 268,
          top: 40,
          width: 12,
          height: 12,
        }}>{props.icon1 ?? <ResizeHandler />}</div>
      )}
    </div>
  );
  const __body23 = () => (
    <div className={props.className} style={{
      width: 280,
      height: 52,
      overflow: "hidden",
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-1-rest)",
        display: "flex",
        flexDirection: "row",
        padding: "0px 6px 0px 6px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          display: "flex",
          flexDirection: "row",
          padding: "6px 2px 6px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexGrow: 1,
          alignSelf: "stretch",
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            whiteSpace: "nowrap",
            lineHeight: "16px",
            color: "var(--neutral-foreground-4-rest)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>{props.placeholder ?? "Placeholder text"}</span>
        </div>
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 50,
        width: 280,
        height: 2,
        borderRadius: "0px 0px 4px 4px",
        backgroundColor: "var(--brand-stroke-compound-rest)",
      }} />
      {props.showResizeHandler && (
      <div style={{
          position: "absolute",
          left: 268,
          top: 40,
          width: 12,
          height: 12,
        }}>{props.icon1 ?? <ResizeHandler />}</div>
      )}
    </div>
  );
  const __body24 = () => (
    <div className={props.className} style={{
      width: 280,
      height: 52,
      overflow: "hidden",
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-1-rest)",
        borderTop: "1px solid var(--status-danger-stroke-2-rest)",
        borderRight: "1px solid var(--status-danger-stroke-2-rest)",
        borderBottom: "1px solid var(--status-danger-stroke-2-rest)",
        borderLeft: "1px solid var(--status-danger-stroke-2-rest)",
        display: "flex",
        flexDirection: "row",
        padding: "0px 6px 0px 6px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          display: "flex",
          flexDirection: "row",
          padding: "6px 2px 6px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexGrow: 1,
          alignSelf: "stretch",
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            whiteSpace: "nowrap",
            lineHeight: "16px",
            color: "var(--neutral-foreground-4-rest)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>{props.placeholder ?? "Placeholder text"}</span>
        </div>
      </div>
      {props.showResizeHandler && (
      <div style={{
          position: "absolute",
          left: 268,
          top: 40,
          width: 12,
          height: 12,
        }}>{props.icon1 ?? <ResizeHandler />}</div>
      )}
    </div>
  );
  const __body25 = () => (
    <div className={props.className} style={{
      width: 280,
      height: 52,
      overflow: "hidden",
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-3-rest)",
        borderTop: "1px solid var(--status-danger-stroke-2-rest)",
        borderRight: "1px solid var(--status-danger-stroke-2-rest)",
        borderBottom: "1px solid var(--status-danger-stroke-2-rest)",
        borderLeft: "1px solid var(--status-danger-stroke-2-rest)",
        display: "flex",
        flexDirection: "row",
        padding: "0px 6px 0px 6px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          display: "flex",
          flexDirection: "row",
          padding: "6px 2px 6px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexGrow: 1,
          alignSelf: "stretch",
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            whiteSpace: "nowrap",
            lineHeight: "16px",
            color: "var(--neutral-foreground-4-rest)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>{props.placeholder ?? "Placeholder text"}</span>
        </div>
      </div>
      {props.showResizeHandler && (
      <div style={{
          position: "absolute",
          left: 268,
          top: 40,
          width: 12,
          height: 12,
        }}>{props.icon1 ?? <ResizeHandler />}</div>
      )}
    </div>
  );
  const __body26 = () => (
    <div className={props.className} style={{
      width: 280,
      height: 52,
      overflow: "hidden",
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "rgba(255,255,255,0)",
        borderTop: "1px solid var(--neutral-stroke-disabled-rest)",
        borderRight: "1px solid var(--neutral-stroke-disabled-rest)",
        borderBottom: "1px solid var(--neutral-stroke-disabled-rest)",
        borderLeft: "1px solid var(--neutral-stroke-disabled-rest)",
        display: "flex",
        flexDirection: "row",
        padding: "0px 6px 0px 6px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          display: "flex",
          flexDirection: "row",
          padding: "6px 2px 6px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexGrow: 1,
          alignSelf: "stretch",
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            whiteSpace: "nowrap",
            lineHeight: "16px",
            color: "var(--neutral-foreground-disabled-rest)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>{props.placeholder ?? "Placeholder text"}</span>
        </div>
      </div>
    </div>
  );
  const __body27 = () => (
    <div className={props.className} style={{
      width: 280,
      height: 52,
      overflow: "hidden",
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "rgba(255,255,255,0)",
        borderTop: "1px solid var(--neutral-stroke-disabled-rest)",
        borderRight: "1px solid var(--neutral-stroke-disabled-rest)",
        borderBottom: "1px solid var(--neutral-stroke-disabled-rest)",
        borderLeft: "1px solid var(--neutral-stroke-disabled-rest)",
        display: "flex",
        flexDirection: "row",
        padding: "0px 6px 0px 6px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-snudge-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          display: "flex",
          flexDirection: "row",
          padding: "6px 2px 6px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexGrow: 1,
          alignSelf: "stretch",
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            whiteSpace: "nowrap",
            lineHeight: "16px",
            color: "var(--neutral-foreground-1-rest)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>{props.placeholder ?? "Placeholder text"}</span>
        </div>
      </div>
      {props.showResizeHandler && (
      <div style={{
          position: "absolute",
          left: 268,
          top: 40,
          width: 12,
          height: 12,
        }}>{props.icon1 ?? <ResizeHandler />}</div>
      )}
    </div>
  );
  const __body28 = () => (
    <div className={props.className} style={{
      width: 280,
      height: 52,
      overflow: "hidden",
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-1-rest)",
        borderTop: "1px solid var(--neutral-stroke-1-rest)",
        borderRight: "1px solid var(--neutral-stroke-1-rest)",
        borderBottom: "1px solid var(--neutral-stroke-1-rest)",
        borderLeft: "1px solid var(--neutral-stroke-1-rest)",
        display: "flex",
        flexDirection: "row",
        padding: "0px 12px 0px 12px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          display: "flex",
          flexDirection: "row",
          padding: "6px 2px 6px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexGrow: 1,
          alignSelf: "stretch",
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 16,
            whiteSpace: "nowrap",
            lineHeight: "22px",
            color: "var(--neutral-foreground-4-rest)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>{props.placeholder ?? "Placeholder text"}</span>
        </div>
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 51,
        width: 280,
        height: 1,
        borderRadius: 4,
        backgroundColor: "var(--neutral-stroke-accessible-rest)",
      }} />
      {props.showResizeHandler && (
      <div style={{
          position: "absolute",
          left: 268,
          top: 40,
          width: 12,
          height: 12,
        }}>{props.icon1 ?? <ResizeHandler />}</div>
      )}
    </div>
  );
  const __body29 = () => (
    <div className={props.className} style={{
      width: 280,
      height: 52,
      overflow: "hidden",
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-3-rest)",
        display: "flex",
        flexDirection: "row",
        padding: "0px 12px 0px 12px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          display: "flex",
          flexDirection: "row",
          padding: "6px 2px 6px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexGrow: 1,
          alignSelf: "stretch",
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 16,
            whiteSpace: "nowrap",
            lineHeight: "22px",
            color: "var(--neutral-foreground-4-rest)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>{props.placeholder ?? "Placeholder text"}</span>
        </div>
      </div>
      {props.showResizeHandler && (
      <div style={{
          position: "absolute",
          left: 268,
          top: 40,
          width: 12,
          height: 12,
        }}>{props.icon1 ?? <ResizeHandler />}</div>
      )}
    </div>
  );
  const __body30 = () => (
    <div className={props.className} style={{
      width: 280,
      height: 52,
      overflow: "hidden",
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-1-rest)",
        display: "flex",
        flexDirection: "row",
        padding: "0px 12px 0px 12px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          display: "flex",
          flexDirection: "row",
          padding: "6px 2px 6px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexGrow: 1,
          alignSelf: "stretch",
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 16,
            whiteSpace: "nowrap",
            lineHeight: "22px",
            color: "var(--neutral-foreground-4-rest)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>{props.placeholder ?? "Placeholder text"}</span>
        </div>
      </div>
      {props.showResizeHandler && (
      <div style={{
          position: "absolute",
          left: 268,
          top: 40,
          width: 12,
          height: 12,
        }}>{props.icon1 ?? <ResizeHandler />}</div>
      )}
    </div>
  );
  const __body31 = () => (
    <div className={props.className} style={{
      width: 280,
      height: 52,
      overflow: "hidden",
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-1-rest)",
        borderTop: "1px solid var(--neutral-stroke-1-hover)",
        borderRight: "1px solid var(--neutral-stroke-1-hover)",
        borderBottom: "1px solid var(--neutral-stroke-1-hover)",
        borderLeft: "1px solid var(--neutral-stroke-1-hover)",
        display: "flex",
        flexDirection: "row",
        padding: "0px 12px 0px 12px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          display: "flex",
          flexDirection: "row",
          padding: "6px 2px 6px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexGrow: 1,
          alignSelf: "stretch",
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 16,
            whiteSpace: "nowrap",
            lineHeight: "22px",
            color: "var(--neutral-foreground-4-rest)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>{props.placeholder ?? "Placeholder text"}</span>
        </div>
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 51,
        width: 280,
        height: 1,
        borderRadius: 4,
        backgroundColor: "var(--neutral-stroke-accessible-hover)",
      }} />
      {props.showResizeHandler && (
      <div style={{
          position: "absolute",
          left: 268,
          top: 40,
          width: 12,
          height: 12,
        }}>{props.icon1 ?? <ResizeHandler />}</div>
      )}
    </div>
  );
  const __body32 = () => (
    <div className={props.className} style={{
      width: 280,
      height: 52,
      overflow: "hidden",
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
      position: "relative",
      color: "var(--brand-stroke-compound-pressed)",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-1-rest)",
        borderTop: "1px solid var(--neutral-stroke-1-pressed)",
        borderRight: "1px solid var(--neutral-stroke-1-pressed)",
        borderBottom: "1px solid var(--neutral-stroke-1-pressed)",
        borderLeft: "1px solid var(--neutral-stroke-1-pressed)",
        display: "flex",
        flexDirection: "row",
        padding: "0px 12px 0px 12px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          display: "flex",
          flexDirection: "row",
          padding: "6px 2px 6px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexGrow: 1,
          alignSelf: "stretch",
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 16,
            whiteSpace: "nowrap",
            lineHeight: "22px",
            color: "var(--neutral-foreground-4-rest)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>{props.placeholder ?? "Placeholder text"}</span>
        </div>
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 50,
        width: 280,
        height: 2,
        borderRadius: 4,
        backgroundColor: "var(--neutral-stroke-accessible-pressed)",
      }} />
      <svg width={280} height={2} viewBox="0 0 280 2" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 50,
        width: 280,
        height: 2,
        borderRadius: 4,
      }}>
        <path d={"M 0 0 L 280 0 L 279.596 2 L 0.369 2 L 0 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.showResizeHandler && (
      <div style={{
          position: "absolute",
          left: 268,
          top: 40,
          width: 12,
          height: 12,
        }}>{props.icon1 ?? <ResizeHandler />}</div>
      )}
    </div>
  );
  const __body33 = () => (
    <div className={props.className} style={{
      width: 280,
      height: 52,
      overflow: "hidden",
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
      position: "relative",
      color: "var(--brand-stroke-compound-pressed)",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-3-rest)",
        display: "flex",
        flexDirection: "row",
        padding: "0px 12px 0px 12px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          display: "flex",
          flexDirection: "row",
          padding: "6px 2px 6px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexGrow: 1,
          alignSelf: "stretch",
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 16,
            whiteSpace: "nowrap",
            lineHeight: "22px",
            color: "var(--neutral-foreground-4-rest)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>{props.placeholder ?? "Placeholder text"}</span>
        </div>
      </div>
      <svg width={280} height={2} viewBox="0 0 280 2" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 50,
        width: 280,
        height: 2,
        borderRadius: 4,
      }}>
        <path d={"M 0 0 L 280 0 L 279.596 2 L 0.369 2 L 0 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.showResizeHandler && (
      <div style={{
          position: "absolute",
          left: 268,
          top: 40,
          width: 12,
          height: 12,
        }}>{props.icon1 ?? <ResizeHandler />}</div>
      )}
    </div>
  );
  const __body34 = () => (
    <div className={props.className} style={{
      width: 280,
      height: 52,
      overflow: "hidden",
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
      position: "relative",
      color: "var(--brand-stroke-compound-pressed)",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-1-rest)",
        display: "flex",
        flexDirection: "row",
        padding: "0px 12px 0px 12px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          display: "flex",
          flexDirection: "row",
          padding: "6px 2px 6px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexGrow: 1,
          alignSelf: "stretch",
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 16,
            whiteSpace: "nowrap",
            lineHeight: "22px",
            color: "var(--neutral-foreground-4-rest)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>{props.placeholder ?? "Placeholder text"}</span>
        </div>
      </div>
      <svg width={280} height={2} viewBox="0 0 280 2" fill="none" style={{
        position: "absolute",
        left: 0,
        top: 50,
        width: 280,
        height: 2,
        borderRadius: 4,
      }}>
        <path d={"M 0 0 L 280 0 L 279.596 2 L 0.369 2 L 0 0 Z"} fill="currentColor" fillRule="nonzero" />
      </svg>
      {props.showResizeHandler && (
      <div style={{
          position: "absolute",
          left: 268,
          top: 40,
          width: 12,
          height: 12,
        }}>{props.icon1 ?? <ResizeHandler />}</div>
      )}
    </div>
  );
  const __body35 = () => (
    <div className={props.className} style={{
      width: 280,
      height: 52,
      overflow: "hidden",
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-1-rest)",
        borderTop: "1px solid var(--neutral-stroke-1-rest)",
        borderRight: "1px solid var(--neutral-stroke-1-rest)",
        borderBottom: "1px solid var(--neutral-stroke-1-rest)",
        borderLeft: "1px solid var(--neutral-stroke-1-rest)",
        display: "flex",
        flexDirection: "row",
        padding: "0px 12px 0px 12px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          display: "flex",
          flexDirection: "row",
          padding: "6px 2px 6px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexGrow: 1,
          alignSelf: "stretch",
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 16,
            whiteSpace: "nowrap",
            lineHeight: "22px",
            color: "var(--neutral-foreground-4-rest)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>{props.placeholder ?? "Placeholder text"}</span>
        </div>
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 50,
        width: 280,
        height: 2,
        borderRadius: "0px 0px 4px 4px",
        backgroundColor: "var(--brand-stroke-compound-rest)",
      }} />
      {props.showResizeHandler && (
      <div style={{
          position: "absolute",
          left: 268,
          top: 40,
          width: 12,
          height: 12,
        }}>{props.icon1 ?? <ResizeHandler />}</div>
      )}
    </div>
  );
  const __body36 = () => (
    <div className={props.className} style={{
      width: 280,
      height: 52,
      overflow: "hidden",
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-3-rest)",
        display: "flex",
        flexDirection: "row",
        padding: "0px 12px 0px 12px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          display: "flex",
          flexDirection: "row",
          padding: "6px 2px 6px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexGrow: 1,
          alignSelf: "stretch",
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 16,
            whiteSpace: "nowrap",
            lineHeight: "22px",
            color: "var(--neutral-foreground-4-rest)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>{props.placeholder ?? "Placeholder text"}</span>
        </div>
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 50,
        width: 280,
        height: 2,
        borderRadius: "0px 0px 4px 4px",
        backgroundColor: "var(--brand-stroke-compound-rest)",
      }} />
      {props.showResizeHandler && (
      <div style={{
          position: "absolute",
          left: 268,
          top: 40,
          width: 12,
          height: 12,
        }}>{props.icon1 ?? <ResizeHandler />}</div>
      )}
    </div>
  );
  const __body37 = () => (
    <div className={props.className} style={{
      width: 280,
      height: 52,
      overflow: "hidden",
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-1-rest)",
        display: "flex",
        flexDirection: "row",
        padding: "0px 12px 0px 12px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          display: "flex",
          flexDirection: "row",
          padding: "6px 2px 6px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexGrow: 1,
          alignSelf: "stretch",
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 16,
            whiteSpace: "nowrap",
            lineHeight: "22px",
            color: "var(--neutral-foreground-4-rest)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>{props.placeholder ?? "Placeholder text"}</span>
        </div>
      </div>
      <div style={{
        position: "absolute",
        left: 0,
        top: 50,
        width: 280,
        height: 2,
        borderRadius: "0px 0px 4px 4px",
        backgroundColor: "var(--brand-stroke-compound-rest)",
      }} />
      {props.showResizeHandler && (
      <div style={{
          position: "absolute",
          left: 268,
          top: 40,
          width: 12,
          height: 12,
        }}>{props.icon1 ?? <ResizeHandler />}</div>
      )}
    </div>
  );
  const __body38 = () => (
    <div className={props.className} style={{
      width: 280,
      height: 52,
      overflow: "hidden",
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-1-rest)",
        borderTop: "1px solid var(--status-danger-stroke-2-rest)",
        borderRight: "1px solid var(--status-danger-stroke-2-rest)",
        borderBottom: "1px solid var(--status-danger-stroke-2-rest)",
        borderLeft: "1px solid var(--status-danger-stroke-2-rest)",
        display: "flex",
        flexDirection: "row",
        padding: "0px 12px 0px 12px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          display: "flex",
          flexDirection: "row",
          padding: "6px 2px 6px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexGrow: 1,
          alignSelf: "stretch",
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 16,
            whiteSpace: "nowrap",
            lineHeight: "22px",
            color: "var(--neutral-foreground-4-rest)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>{props.placeholder ?? "Placeholder text"}</span>
        </div>
      </div>
      {props.showResizeHandler && (
      <div style={{
          position: "absolute",
          left: 268,
          top: 40,
          width: 12,
          height: 12,
        }}>{props.icon1 ?? <ResizeHandler />}</div>
      )}
    </div>
  );
  const __body39 = () => (
    <div className={props.className} style={{
      width: 280,
      height: 52,
      overflow: "hidden",
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-3-rest)",
        borderTop: "1px solid var(--status-danger-stroke-2-rest)",
        borderRight: "1px solid var(--status-danger-stroke-2-rest)",
        borderBottom: "1px solid var(--status-danger-stroke-2-rest)",
        borderLeft: "1px solid var(--status-danger-stroke-2-rest)",
        display: "flex",
        flexDirection: "row",
        padding: "0px 12px 0px 12px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          display: "flex",
          flexDirection: "row",
          padding: "6px 2px 6px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexGrow: 1,
          alignSelf: "stretch",
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 16,
            whiteSpace: "nowrap",
            lineHeight: "22px",
            color: "var(--neutral-foreground-4-rest)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>{props.placeholder ?? "Placeholder text"}</span>
        </div>
      </div>
      {props.showResizeHandler && (
      <div style={{
          position: "absolute",
          left: 268,
          top: 40,
          width: 12,
          height: 12,
        }}>{props.icon1 ?? <ResizeHandler />}</div>
      )}
    </div>
  );
  const __body40 = () => (
    <div className={props.className} style={{
      width: 280,
      height: 52,
      overflow: "hidden",
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "rgba(255,255,255,0)",
        borderTop: "1px solid var(--neutral-stroke-disabled-rest)",
        borderRight: "1px solid var(--neutral-stroke-disabled-rest)",
        borderBottom: "1px solid var(--neutral-stroke-disabled-rest)",
        borderLeft: "1px solid var(--neutral-stroke-disabled-rest)",
        display: "flex",
        flexDirection: "row",
        padding: "0px 12px 0px 12px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          display: "flex",
          flexDirection: "row",
          padding: "6px 2px 6px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexGrow: 1,
          alignSelf: "stretch",
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 16,
            whiteSpace: "nowrap",
            lineHeight: "22px",
            color: "var(--neutral-foreground-disabled-rest)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>{props.placeholder ?? "Placeholder text"}</span>
        </div>
      </div>
    </div>
  );
  const __body41 = () => (
    <div className={props.className} style={{
      width: 280,
      height: 52,
      overflow: "hidden",
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      paddingLeft: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-none-2) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        overflow: "hidden",
        borderRadius: 4,
        backgroundColor: "rgba(255,255,255,0)",
        borderTop: "1px solid var(--neutral-stroke-disabled-rest)",
        borderRight: "1px solid var(--neutral-stroke-disabled-rest)",
        borderBottom: "1px solid var(--neutral-stroke-disabled-rest)",
        borderLeft: "1px solid var(--neutral-stroke-disabled-rest)",
        display: "flex",
        flexDirection: "row",
        padding: "0px 12px 0px 12px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
        paddingTop: "calc(var(--spacing-vertical-none-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-none-2) * 1px)",
        flexGrow: 1,
        alignSelf: "stretch",
      }}>
        <div style={{
          position: "relative",
          overflow: "hidden",
          display: "flex",
          flexDirection: "row",
          padding: "6px 2px 6px 2px",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingLeft: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingTop: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          paddingRight: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          paddingBottom: "calc(var(--spacing-vertical-snudge-2) * 1px)",
          flexGrow: 1,
          alignSelf: "stretch",
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 16,
            whiteSpace: "nowrap",
            lineHeight: "22px",
            color: "var(--neutral-foreground-1-rest)",
            flexGrow: 1,
            alignSelf: "stretch",
          }}>{props.placeholder ?? "Placeholder text"}</span>
        </div>
      </div>
      {props.showResizeHandler && (
      <div style={{
          position: "absolute",
          left: 268,
          top: 40,
          width: 12,
          height: 12,
        }}>{props.icon1 ?? <ResizeHandler />}</div>
      )}
    </div>
  );
  const __impls = {
    // figma: Style=Outline, Size=Medium (Default), State=Rest
    "style2=outline|size=medium (default)|state=rest": __body0,
    // figma: Style=Filled darker, Size=Medium (Default), State=Rest
    "style2=filled darker|size=medium (default)|state=rest": __body1,
    // figma: Style=Filled lighter, Size=Medium (Default), State=Rest
    "style2=filled lighter|size=medium (default)|state=rest": __body2,
    // figma: Style=Outline, Size=Medium (Default), State=Hover
    "style2=outline|size=medium (default)|state=hover": __body3,
    // figma: Style=Filled darker, Size=Medium (Default), State=Hover
    "style2=filled darker|size=medium (default)|state=hover": __body1,
    // figma: Style=Filled lighter, Size=Medium (Default), State=Hover
    "style2=filled lighter|size=medium (default)|state=hover": __body2,
    // figma: Style=Outline, Size=Medium (Default), State=Pressed
    "style2=outline|size=medium (default)|state=pressed": __body4,
    // figma: Style=Filled darker, Size=Medium (Default), State=Pressed
    "style2=filled darker|size=medium (default)|state=pressed": __body5,
    // figma: Style=Filled lighter, Size=Medium (Default), State=Pressed
    "style2=filled lighter|size=medium (default)|state=pressed": __body6,
    // figma: Style=Outline, Size=Medium (Default), State=Focus
    "style2=outline|size=medium (default)|state=focus": __body7,
    // figma: Style=Filled darker, Size=Medium (Default), State=Focus
    "style2=filled darker|size=medium (default)|state=focus": __body8,
    // figma: Style=Filled lighter, Size=Medium (Default), State=Focus
    "style2=filled lighter|size=medium (default)|state=focus": __body9,
    // figma: Style=Outline, Size=Medium (Default), State=Error
    "style2=outline|size=medium (default)|state=error": __body10,
    // figma: Style=Filled darker, Size=Medium (Default), State=Error
    "style2=filled darker|size=medium (default)|state=error": __body11,
    // figma: Style=Filled lighter, Size=Medium (Default), State=Error
    "style2=filled lighter|size=medium (default)|state=error": __body10,
    // figma: Style=Outline, Size=Medium (Default), State=Disabled
    "style2=outline|size=medium (default)|state=disabled": __body12,
    // figma: Style=Filled darker, Size=Medium (Default), State=Disabled
    "style2=filled darker|size=medium (default)|state=disabled": __body12,
    // figma: Style=Filled lighter, Size=Medium (Default), State=Disabled
    "style2=filled lighter|size=medium (default)|state=disabled": __body12,
    // figma: Style=Outline, Size=Medium (Default), State=Read only
    "style2=outline|size=medium (default)|state=read only": __body13,
    // figma: Style=Filled darker, Size=Medium (Default), State=Read only
    "style2=filled darker|size=medium (default)|state=read only": __body13,
    // figma: Style=Filled lighter, Size=Medium (Default), State=Read only
    "style2=filled lighter|size=medium (default)|state=read only": __body13,
    // figma: Style=Outline, Size=Small, State=Rest
    "style2=outline|size=sm|state=rest": __body14,
    // figma: Style=Filled darker, Size=Small, State=Rest
    "style2=filled darker|size=sm|state=rest": __body15,
    // figma: Style=Filled lighter, Size=Small, State=Rest
    "style2=filled lighter|size=sm|state=rest": __body16,
    // figma: Style=Outline, Size=Small, State=Hover
    "style2=outline|size=sm|state=hover": __body17,
    // figma: Style=Filled darker, Size=Small, State=Hover
    "style2=filled darker|size=sm|state=hover": __body15,
    // figma: Style=Filled lighter, Size=Small, State=Hover
    "style2=filled lighter|size=sm|state=hover": __body16,
    // figma: Style=Outline, Size=Small, State=Pressed
    "style2=outline|size=sm|state=pressed": __body18,
    // figma: Style=Filled darker, Size=Small, State=Pressed
    "style2=filled darker|size=sm|state=pressed": __body19,
    // figma: Style=Filled lighter, Size=Small, State=Pressed
    "style2=filled lighter|size=sm|state=pressed": __body20,
    // figma: Style=Outline, Size=Small, State=Focus
    "style2=outline|size=sm|state=focus": __body21,
    // figma: Style=Filled darker, Size=Small, State=Focus
    "style2=filled darker|size=sm|state=focus": __body22,
    // figma: Style=Filled lighter, Size=Small, State=Focus
    "style2=filled lighter|size=sm|state=focus": __body23,
    // figma: Style=Outline, Size=Small, State=Error
    "style2=outline|size=sm|state=error": __body24,
    // figma: Style=Filled darker, Size=Small, State=Error
    "style2=filled darker|size=sm|state=error": __body25,
    // figma: Style=Filled lighter, Size=Small, State=Error
    "style2=filled lighter|size=sm|state=error": __body24,
    // figma: Style=Outline, Size=Small, State=Disabled
    "style2=outline|size=sm|state=disabled": __body26,
    // figma: Style=Filled darker, Size=Small, State=Disabled
    "style2=filled darker|size=sm|state=disabled": __body26,
    // figma: Style=Filled lighter, Size=Small, State=Disabled
    "style2=filled lighter|size=sm|state=disabled": __body26,
    // figma: Style=Outline, Size=Small, State=Read only
    "style2=outline|size=sm|state=read only": __body27,
    // figma: Style=Filled darker, Size=Small, State=Read only
    "style2=filled darker|size=sm|state=read only": __body27,
    // figma: Style=Filled lighter, Size=Small, State=Read only
    "style2=filled lighter|size=sm|state=read only": __body27,
    // figma: Style=Outline, Size=Large, State=Rest
    "style2=outline|size=lg|state=rest": __body28,
    // figma: Style=Filled darker, Size=Large, State=Rest
    "style2=filled darker|size=lg|state=rest": __body29,
    // figma: Style=Filled lighter, Size=Large, State=Rest
    "style2=filled lighter|size=lg|state=rest": __body30,
    // figma: Style=Outline, Size=Large, State=Hover
    "style2=outline|size=lg|state=hover": __body31,
    // figma: Style=Filled darker, Size=Large, State=Hover
    "style2=filled darker|size=lg|state=hover": __body29,
    // figma: Style=Filled lighter, Size=Large, State=Hover
    "style2=filled lighter|size=lg|state=hover": __body30,
    // figma: Style=Outline, Size=Large, State=Pressed
    "style2=outline|size=lg|state=pressed": __body32,
    // figma: Style=Filled darker, Size=Large, State=Pressed
    "style2=filled darker|size=lg|state=pressed": __body33,
    // figma: Style=Filled lighter, Size=Large, State=Pressed
    "style2=filled lighter|size=lg|state=pressed": __body34,
    // figma: Style=Outline, Size=Large, State=Focus
    "style2=outline|size=lg|state=focus": __body35,
    // figma: Style=Filled darker, Size=Large, State=Focus
    "style2=filled darker|size=lg|state=focus": __body36,
    // figma: Style=Filled lighter, Size=Large, State=Focus
    "style2=filled lighter|size=lg|state=focus": __body37,
    // figma: Style=Outline, Size=Large, State=Error
    "style2=outline|size=lg|state=error": __body38,
    // figma: Style=Filled darker, Size=Large, State=Error
    "style2=filled darker|size=lg|state=error": __body39,
    // figma: Style=Filled lighter, Size=Large, State=Error
    "style2=filled lighter|size=lg|state=error": __body38,
    // figma: Style=Outline, Size=Large, State=Disabled
    "style2=outline|size=lg|state=disabled": __body40,
    // figma: Style=Filled darker, Size=Large, State=Disabled
    "style2=filled darker|size=lg|state=disabled": __body40,
    // figma: Style=Filled lighter, Size=Large, State=Disabled
    "style2=filled lighter|size=lg|state=disabled": __body40,
    // figma: Style=Outline, Size=Large, State=Read only
    "style2=outline|size=lg|state=read only": __body41,
    // figma: Style=Filled darker, Size=Large, State=Read only
    "style2=filled darker|size=lg|state=read only": __body41,
    // figma: Style=Filled lighter, Size=Large, State=Read only
    "style2=filled lighter|size=lg|state=read only": __body41,
  };
  return (__impls[__vkey(props)] ?? __body14)();
}
export default Textarea;
