import { __slateNorm } from '../internal/slateProps.jsx';
import { Chevron } from '../internal/Chevron.jsx';
import { Placeholder } from '../internal/Placeholder.jsx';

// figma node: 9000:5035 Button (150 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "style2=" + __venc(p.style2) + '|' + "state=" + __venc(p.state) + '|' + "size=" + __venc(p.size) + '|' + "layout=" + __venc(p.layout);

function __FluentWebButton(_p = {}) {
  _p = __slateNorm(_p, {"style2":["primary","secondary (default)","outline","subtle","transparent"],"state":["rest","hover","pressed","selected","disabled"],"size":["lg","medium (default)","sm"],"layout":["icon and label (default)","icon only"]});
  const props = { ..._p, style2: _p.style2 ?? "primary", state: _p.state ?? "rest", size: _p.size ?? "lg", layout: _p.layout ?? "icon and label (default)", menuButton: _p.menuButton ?? false, label: _p.label ?? "Button", focus: _p.focus ?? false, icon: _p.icon ?? true };
  const __body0 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "8px 8px 8px 8px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-s) * 1px)",
      paddingTop: "calc(var(--vertical-s) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s) * 1px)",
      paddingBottom: "calc(var(--vertical-s) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xxs) * 1px)",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
            color: "var(--neutral-foreground-disabled-rest-4)",
          }}>{props.xOutline24 ?? <Placeholder size={"24"} theme={"regular"} />}</div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-disabled-rest-4)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-snudge) * 1px)",
      padding: "8px 16px 8px 16px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-l) * 1px)",
      paddingTop: "calc(var(--vertical-s) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-l) * 1px)",
      paddingBottom: "calc(var(--vertical-s) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.icon && (
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
          color: "var(--neutral-foreground-disabled-rest-4)",
        }}>{props.xOutline24 ?? <Placeholder size={"24"} theme={"regular"} />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
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
          padding: "0px 0px 0px 0px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingTop: "0px", paddingBottom: "0px",
          flexShrink: 0,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 600,
            fontSize: 16,
            whiteSpace: "nowrap",
            lineHeight: "22px",
            color: "var(--neutral-foreground-disabled-rest-4)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.label}</span>
        </div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-disabled-rest-4)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
    </div>
  );
  const __body2 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "8px 8px 8px 8px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-s) * 1px)",
      paddingTop: "calc(var(--vertical-s) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s) * 1px)",
      paddingBottom: "calc(var(--vertical-s) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xxs) * 1px)",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
            color: "var(--neutral-foreground-2-brand-selected-3)",
          }}>{props.xFilled24 ?? <Placeholder size={"24"} theme={"filled"} />}</div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-brand-selected-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"filled"} />}</div>
        )}
      </div>
    </div>
  );
  const __body3 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-snudge) * 1px)",
      padding: "8px 16px 8px 16px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-l) * 1px)",
      paddingTop: "calc(var(--vertical-s) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-l) * 1px)",
      paddingBottom: "calc(var(--vertical-s) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.icon && (
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
          color: "var(--neutral-foreground-2-brand-selected-3)",
        }}>{props.xFilled24 ?? <Placeholder size={"24"} theme={"filled"} />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
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
          padding: "0px 0px 0px 0px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingTop: "0px", paddingBottom: "0px",
          flexShrink: 0,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 600,
            fontSize: 16,
            whiteSpace: "nowrap",
            lineHeight: "22px",
            color: "var(--neutral-foreground-2-brand-selected-3)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.label}</span>
        </div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-brand-selected-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"filled"} />}</div>
        )}
      </div>
    </div>
  );
  const __body4 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "8px 8px 8px 8px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-s) * 1px)",
      paddingTop: "calc(var(--vertical-s) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s) * 1px)",
      paddingBottom: "calc(var(--vertical-s) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xxs) * 1px)",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
            color: "var(--neutral-foreground-2-brand-pressed-4)",
          }}>{props.xFilled24 ?? <Placeholder size={"24"} theme={"filled"} />}</div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-brand-pressed-4)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
    </div>
  );
  const __body5 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-snudge) * 1px)",
      padding: "8px 16px 8px 16px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-l) * 1px)",
      paddingTop: "calc(var(--vertical-s) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-l) * 1px)",
      paddingBottom: "calc(var(--vertical-s) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.icon && (
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
          color: "var(--neutral-foreground-2-brand-pressed-4)",
        }}>{props.xFilled24 ?? <Placeholder size={"24"} theme={"filled"} />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
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
          padding: "0px 0px 0px 0px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingTop: "0px", paddingBottom: "0px",
          flexShrink: 0,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 600,
            fontSize: 16,
            whiteSpace: "nowrap",
            lineHeight: "22px",
            color: "var(--neutral-foreground-2-brand-pressed-4)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.label}</span>
        </div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-brand-pressed-4)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
    </div>
  );
  const __body6 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "8px 8px 8px 8px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-s) * 1px)",
      paddingTop: "calc(var(--vertical-s) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s) * 1px)",
      paddingBottom: "calc(var(--vertical-s) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xxs) * 1px)",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
            color: "var(--neutral-foreground-2-brand-hover-4)",
          }}>{props.xFilled24 ?? <Placeholder size={"24"} theme={"filled"} />}</div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-brand-hover-4)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
    </div>
  );
  const __body7 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-snudge) * 1px)",
      padding: "8px 16px 8px 16px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-l) * 1px)",
      paddingTop: "calc(var(--vertical-s) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-l) * 1px)",
      paddingBottom: "calc(var(--vertical-s) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.icon && (
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
          color: "var(--neutral-foreground-2-brand-hover-4)",
        }}>{props.xFilled24 ?? <Placeholder size={"24"} theme={"filled"} />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
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
          padding: "0px 0px 0px 0px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingTop: "0px", paddingBottom: "0px",
          flexShrink: 0,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 600,
            fontSize: 16,
            whiteSpace: "nowrap",
            lineHeight: "22px",
            color: "var(--neutral-foreground-2-brand-hover-4)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.label}</span>
        </div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-brand-hover-4)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
    </div>
  );
  const __body8 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "8px 8px 8px 8px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-s) * 1px)",
      paddingTop: "calc(var(--vertical-s) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s) * 1px)",
      paddingBottom: "calc(var(--vertical-s) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xxs) * 1px)",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
            color: "var(--neutral-foreground-2-rest-3)",
          }}>{props.xOutline24 ?? <Placeholder size={"24"} theme={"regular"} />}</div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
      {props.focus && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 40,
        height: 40,
        overflow: "hidden",
        borderRadius: 4,
        borderTop: "2px solid var(--neutral-stroke-focus-2-rest-4)",
        borderRight: "2px solid var(--neutral-stroke-focus-2-rest-4)",
        borderBottom: "2px solid var(--neutral-stroke-focus-2-rest-4)",
        borderLeft: "2px solid var(--neutral-stroke-focus-2-rest-4)",
      }}>
        <svg width={40} height={40} viewBox="0 0 40 40" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 40,
          height: 40,
          overflow: "hidden",
          borderRadius: 4,
          borderTop: "1px solid var(--neutral-stroke-focus-1-rest-4)",
          borderRight: "1px solid var(--neutral-stroke-focus-1-rest-4)",
          borderBottom: "1px solid var(--neutral-stroke-focus-1-rest-4)",
          borderLeft: "1px solid var(--neutral-stroke-focus-1-rest-4)",
        }}>
          <path d={"M 4 0 L 4 1 L 36 1 L 36 0 L 36 -1 L 4 -1 L 4 0 Z M 40 4 L 39 4 L 39 36 L 40 36 L 41 36 L 41 4 L 40 4 Z M 36 40 L 36 39 L 4 39 L 4 40 L 4 41 L 36 41 L 36 40 Z M 0 36 L 1 36 L 1 4 L 0 4 L -1 4 L -1 36 L 0 36 Z M 4 40 L 4 39 C 2.343 39 1 37.657 1 36 L 0 36 L -1 36 C -1 38.761 1.239 41 4 41 L 4 40 Z M 40 36 L 39 36 C 39 37.657 37.657 39 36 39 L 36 40 L 36 41 C 38.761 41 41 38.761 41 36 L 40 36 Z M 36 0 L 36 1 C 37.657 1 39 2.343 39 4 L 40 4 L 41 4 C 41 1.239 38.761 -1 36 -1 L 36 0 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
    </div>
  );
  const __body9 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-snudge) * 1px)",
      padding: "8px 16px 8px 16px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-l) * 1px)",
      paddingTop: "calc(var(--vertical-s) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-l) * 1px)",
      paddingBottom: "calc(var(--vertical-s) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.icon && (
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
          color: "var(--neutral-foreground-2-rest-3)",
        }}>{props.xOutline24 ?? <Placeholder size={"24"} theme={"regular"} />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
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
          padding: "0px 0px 0px 0px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingTop: "0px", paddingBottom: "0px",
          flexShrink: 0,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 600,
            fontSize: 16,
            whiteSpace: "nowrap",
            lineHeight: "22px",
            color: "var(--neutral-foreground-2-rest-3)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.label}</span>
        </div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
      {props.focus && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 112,
        height: 40,
        overflow: "hidden",
        borderRadius: 4,
        borderTop: "2px solid var(--neutral-stroke-focus-2-rest-4)",
        borderRight: "2px solid var(--neutral-stroke-focus-2-rest-4)",
        borderBottom: "2px solid var(--neutral-stroke-focus-2-rest-4)",
        borderLeft: "2px solid var(--neutral-stroke-focus-2-rest-4)",
      }}>
        <svg width={112} height={40} viewBox="0 0 112 40" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 112,
          height: 40,
          overflow: "hidden",
          borderRadius: 4,
          borderTop: "1px solid var(--neutral-stroke-focus-1-rest-4)",
          borderRight: "1px solid var(--neutral-stroke-focus-1-rest-4)",
          borderBottom: "1px solid var(--neutral-stroke-focus-1-rest-4)",
          borderLeft: "1px solid var(--neutral-stroke-focus-1-rest-4)",
        }}>
          <path d={"M 4 0 L 4 1 L 108 1 L 108 0 L 108 -1 L 4 -1 L 4 0 Z M 112 4 L 111 4 L 111 36 L 112 36 L 113 36 L 113 4 L 112 4 Z M 108 40 L 108 39 L 4 39 L 4 40 L 4 41 L 108 41 L 108 40 Z M 0 36 L 1 36 L 1 4 L 0 4 L -1 4 L -1 36 L 0 36 Z M 4 40 L 4 39 C 2.343 39 1 37.657 1 36 L 0 36 L -1 36 C -1 38.761 1.239 41 4 41 L 4 40 Z M 112 36 L 111 36 C 111 37.657 109.657 39 108 39 L 108 40 L 108 41 C 110.761 41 113 38.761 113 36 L 112 36 Z M 108 0 L 108 1 C 109.657 1 111 2.343 111 4 L 112 4 L 113 4 C 113 1.239 110.761 -1 108 -1 L 108 0 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
    </div>
  );
  const __body10 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      display: "flex",
      flexDirection: "row",
      padding: "8px 8px 8px 8px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-s) * 1px)",
      paddingTop: "calc(var(--vertical-s) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s) * 1px)",
      paddingBottom: "calc(var(--vertical-s) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xxs) * 1px)",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
            color: "var(--neutral-foreground-disabled-rest-4)",
          }}>{props.xOutline24 ?? <Placeholder size={"24"} theme={"regular"} />}</div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-disabled-rest-4)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
    </div>
  );
  const __body11 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-snudge) * 1px)",
      padding: "8px 16px 8px 16px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-l) * 1px)",
      paddingTop: "calc(var(--vertical-s) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-l) * 1px)",
      paddingBottom: "calc(var(--vertical-s) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.icon && (
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
          color: "var(--neutral-foreground-disabled-rest-4)",
        }}>{props.xOutline24 ?? <Placeholder size={"24"} theme={"regular"} />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
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
          padding: "0px 0px 0px 0px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingTop: "0px", paddingBottom: "0px",
          flexShrink: 0,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 600,
            fontSize: 16,
            whiteSpace: "nowrap",
            lineHeight: "22px",
            color: "var(--neutral-foreground-disabled-rest-4)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.label}</span>
        </div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-disabled-rest-4)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
    </div>
  );
  const __body12 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-subtle-selected-3)",
      display: "flex",
      flexDirection: "row",
      padding: "8px 8px 8px 8px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-s) * 1px)",
      paddingTop: "calc(var(--vertical-s) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s) * 1px)",
      paddingBottom: "calc(var(--vertical-s) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xxs) * 1px)",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
            color: "var(--neutral-foreground-2-brand-selected-3)",
          }}>{props.xFilled24 ?? <Placeholder size={"24"} theme={"filled"} />}</div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-selected-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"filled"} />}</div>
        )}
      </div>
    </div>
  );
  const __body13 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-subtle-selected-3)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-snudge) * 1px)",
      padding: "8px 16px 8px 16px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-l) * 1px)",
      paddingTop: "calc(var(--vertical-s) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-l) * 1px)",
      paddingBottom: "calc(var(--vertical-s) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.icon && (
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
          color: "var(--neutral-foreground-2-brand-selected-3)",
        }}>{props.xFilled24 ?? <Placeholder size={"24"} theme={"filled"} />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
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
          padding: "0px 0px 0px 0px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingTop: "0px", paddingBottom: "0px",
          flexShrink: 0,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 600,
            fontSize: 16,
            whiteSpace: "nowrap",
            lineHeight: "22px",
            color: "var(--neutral-foreground-1-pressed-3)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.label}</span>
        </div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-selected-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"filled"} />}</div>
        )}
      </div>
    </div>
  );
  const __body14 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-subtle-pressed-3)",
      display: "flex",
      flexDirection: "row",
      padding: "8px 8px 8px 8px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-s) * 1px)",
      paddingTop: "calc(var(--vertical-s) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s) * 1px)",
      paddingBottom: "calc(var(--vertical-s) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xxs) * 1px)",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
            color: "var(--neutral-foreground-2-brand-pressed-4)",
          }}>{props.xFilled24 ?? <Placeholder size={"24"} theme={"filled"} />}</div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-pressed-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
    </div>
  );
  const __body15 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-subtle-pressed-3)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-snudge) * 1px)",
      padding: "8px 16px 8px 16px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-l) * 1px)",
      paddingTop: "calc(var(--vertical-s) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-l) * 1px)",
      paddingBottom: "calc(var(--vertical-s) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.icon && (
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
          color: "var(--neutral-foreground-2-brand-pressed-4)",
        }}>{props.xFilled24 ?? <Placeholder size={"24"} theme={"filled"} />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
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
          padding: "0px 0px 0px 0px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingTop: "0px", paddingBottom: "0px",
          flexShrink: 0,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 600,
            fontSize: 16,
            whiteSpace: "nowrap",
            lineHeight: "22px",
            color: "var(--neutral-foreground-1-pressed-3)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.label}</span>
        </div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-pressed-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
    </div>
  );
  const __body16 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-subtle-hover-3)",
      display: "flex",
      flexDirection: "row",
      padding: "8px 8px 8px 8px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-s) * 1px)",
      paddingTop: "calc(var(--vertical-s) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s) * 1px)",
      paddingBottom: "calc(var(--vertical-s) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xxs) * 1px)",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
            color: "var(--neutral-foreground-2-brand-hover-4)",
          }}>{props.xFilled24 ?? <Placeholder size={"24"} theme={"filled"} />}</div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-hover-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
    </div>
  );
  const __body17 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-subtle-hover-3)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-snudge) * 1px)",
      padding: "8px 16px 8px 16px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-l) * 1px)",
      paddingTop: "calc(var(--vertical-s) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-l) * 1px)",
      paddingBottom: "calc(var(--vertical-s) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.icon && (
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
          color: "var(--neutral-foreground-2-brand-hover-4)",
        }}>{props.xFilled24 ?? <Placeholder size={"24"} theme={"filled"} />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
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
          padding: "0px 0px 0px 0px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingTop: "0px", paddingBottom: "0px",
          flexShrink: 0,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 600,
            fontSize: 16,
            whiteSpace: "nowrap",
            lineHeight: "22px",
            color: "var(--neutral-foreground-1-hover-3)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.label}</span>
        </div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-hover-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
    </div>
  );
  const __body18 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      borderTop: "1px solid var(--neutral-stroke-disabled-rest-4)",
      borderRight: "1px solid var(--neutral-stroke-disabled-rest-4)",
      borderBottom: "1px solid var(--neutral-stroke-disabled-rest-4)",
      borderLeft: "1px solid var(--neutral-stroke-disabled-rest-4)",
      display: "flex",
      flexDirection: "row",
      padding: "7px 7px 7px 7px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "max(0px, calc(var(--spacing-horizontal-s) * 1px - 1px))",
      paddingTop: "max(0px, calc(var(--vertical-s) * 1px - 1px))",
      paddingRight: "max(0px, calc(var(--spacing-horizontal-s) * 1px - 1px))",
      paddingBottom: "max(0px, calc(var(--vertical-s) * 1px - 1px))",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xxs) * 1px)",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
            color: "var(--neutral-foreground-disabled-rest-4)",
          }}>{props.xOutline24 ?? <Placeholder size={"24"} theme={"regular"} />}</div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-disabled-rest-4)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
    </div>
  );
  const __body19 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      borderTop: "1px solid var(--neutral-stroke-disabled-rest-4)",
      borderRight: "1px solid var(--neutral-stroke-disabled-rest-4)",
      borderBottom: "1px solid var(--neutral-stroke-disabled-rest-4)",
      borderLeft: "1px solid var(--neutral-stroke-disabled-rest-4)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-snudge) * 1px)",
      padding: "7px 15px 7px 15px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "max(0px, calc(var(--spacing-horizontal-l) * 1px - 1px))",
      paddingTop: "max(0px, calc(var(--vertical-s) * 1px - 1px))",
      paddingRight: "max(0px, calc(var(--spacing-horizontal-l) * 1px - 1px))",
      paddingBottom: "max(0px, calc(var(--vertical-s) * 1px - 1px))",
      position: "relative",
      ...props.style,
    }}>
      {props.icon && (
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
          color: "var(--neutral-foreground-disabled-rest-4)",
        }}>{props.xOutline24 ?? <Placeholder size={"24"} theme={"regular"} />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
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
          padding: "0px 0px 0px 0px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingTop: "0px", paddingBottom: "0px",
          flexShrink: 0,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 600,
            fontSize: 16,
            whiteSpace: "nowrap",
            lineHeight: "22px",
            color: "var(--neutral-foreground-disabled-rest-4)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.label}</span>
        </div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-disabled-rest-4)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
    </div>
  );
  const __body20 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      borderTop: "2px solid var(--neutral-stroke-1-selected-3)",
      borderRight: "2px solid var(--neutral-stroke-1-selected-3)",
      borderBottom: "2px solid var(--neutral-stroke-1-selected-3)",
      borderLeft: "2px solid var(--neutral-stroke-1-selected-3)",
      display: "flex",
      flexDirection: "row",
      padding: "8px 8px 8px 8px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-s) * 1px)",
      paddingTop: "calc(var(--vertical-s) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s) * 1px)",
      paddingBottom: "calc(var(--vertical-s) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xxs) * 1px)",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
            color: "var(--neutral-foreground-1-selected-3)",
          }}>{props.xFilled24 ?? <Placeholder size={"24"} theme={"filled"} />}</div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-1-selected-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"filled"} />}</div>
        )}
      </div>
    </div>
  );
  const __body21 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      borderTop: "2px solid var(--neutral-stroke-1-selected-3)",
      borderRight: "2px solid var(--neutral-stroke-1-selected-3)",
      borderBottom: "2px solid var(--neutral-stroke-1-selected-3)",
      borderLeft: "2px solid var(--neutral-stroke-1-selected-3)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-snudge) * 1px)",
      padding: "8px 16px 8px 16px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-l) * 1px)",
      paddingTop: "calc(var(--vertical-s) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-l) * 1px)",
      paddingBottom: "calc(var(--vertical-s) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.icon && (
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
          color: "var(--neutral-foreground-1-selected-3)",
        }}>{props.xFilled24 ?? <Placeholder size={"24"} theme={"filled"} />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
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
          padding: "0px 0px 0px 0px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingTop: "0px", paddingBottom: "0px",
          flexShrink: 0,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 600,
            fontSize: 16,
            whiteSpace: "nowrap",
            lineHeight: "22px",
            color: "var(--neutral-foreground-1-pressed-3)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.label}</span>
        </div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-1-selected-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"filled"} />}</div>
        )}
      </div>
    </div>
  );
  const __body22 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      borderTop: "1px solid var(--neutral-stroke-1-pressed-3)",
      borderRight: "1px solid var(--neutral-stroke-1-pressed-3)",
      borderBottom: "1px solid var(--neutral-stroke-1-pressed-3)",
      borderLeft: "1px solid var(--neutral-stroke-1-pressed-3)",
      display: "flex",
      flexDirection: "row",
      padding: "7px 7px 7px 7px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "max(0px, calc(var(--spacing-horizontal-s) * 1px - 1px))",
      paddingTop: "max(0px, calc(var(--vertical-s) * 1px - 1px))",
      paddingRight: "max(0px, calc(var(--spacing-horizontal-s) * 1px - 1px))",
      paddingBottom: "max(0px, calc(var(--vertical-s) * 1px - 1px))",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xxs) * 1px)",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
            color: "var(--neutral-foreground-1-pressed-3)",
          }}>{props.xOutline24 ?? <Placeholder size={"24"} theme={"regular"} />}</div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-1-pressed-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
    </div>
  );
  const __body23 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      borderTop: "1px solid var(--neutral-stroke-1-pressed-3)",
      borderRight: "1px solid var(--neutral-stroke-1-pressed-3)",
      borderBottom: "1px solid var(--neutral-stroke-1-pressed-3)",
      borderLeft: "1px solid var(--neutral-stroke-1-pressed-3)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-snudge) * 1px)",
      padding: "7px 15px 7px 15px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "max(0px, calc(var(--spacing-horizontal-l) * 1px - 1px))",
      paddingTop: "max(0px, calc(var(--vertical-s) * 1px - 1px))",
      paddingRight: "max(0px, calc(var(--spacing-horizontal-l) * 1px - 1px))",
      paddingBottom: "max(0px, calc(var(--vertical-s) * 1px - 1px))",
      position: "relative",
      ...props.style,
    }}>
      {props.icon && (
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
          color: "var(--neutral-foreground-1-pressed-3)",
        }}>{props.xOutline24 ?? <Placeholder size={"24"} theme={"regular"} />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
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
          padding: "0px 0px 0px 0px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingTop: "0px", paddingBottom: "0px",
          flexShrink: 0,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 600,
            fontSize: 16,
            whiteSpace: "nowrap",
            lineHeight: "22px",
            color: "var(--neutral-foreground-1-pressed-3)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.label}</span>
        </div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-1-pressed-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
    </div>
  );
  const __body24 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      borderTop: "1px solid var(--neutral-stroke-1-hover-3)",
      borderRight: "1px solid var(--neutral-stroke-1-hover-3)",
      borderBottom: "1px solid var(--neutral-stroke-1-hover-3)",
      borderLeft: "1px solid var(--neutral-stroke-1-hover-3)",
      display: "flex",
      flexDirection: "row",
      padding: "7px 7px 7px 7px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "max(0px, calc(var(--spacing-horizontal-s) * 1px - 1px))",
      paddingTop: "max(0px, calc(var(--vertical-s) * 1px - 1px))",
      paddingRight: "max(0px, calc(var(--spacing-horizontal-s) * 1px - 1px))",
      paddingBottom: "max(0px, calc(var(--vertical-s) * 1px - 1px))",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xxs) * 1px)",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
            color: "var(--neutral-foreground-1-hover-3)",
          }}>{props.xOutline24 ?? <Placeholder size={"24"} theme={"regular"} />}</div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-1-hover-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
    </div>
  );
  const __body25 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      borderTop: "1px solid var(--neutral-stroke-1-hover-3)",
      borderRight: "1px solid var(--neutral-stroke-1-hover-3)",
      borderBottom: "1px solid var(--neutral-stroke-1-hover-3)",
      borderLeft: "1px solid var(--neutral-stroke-1-hover-3)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-snudge) * 1px)",
      padding: "7px 15px 7px 15px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "max(0px, calc(var(--spacing-horizontal-l) * 1px - 1px))",
      paddingTop: "max(0px, calc(var(--vertical-s) * 1px - 1px))",
      paddingRight: "max(0px, calc(var(--spacing-horizontal-l) * 1px - 1px))",
      paddingBottom: "max(0px, calc(var(--vertical-s) * 1px - 1px))",
      position: "relative",
      ...props.style,
    }}>
      {props.icon && (
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
          color: "var(--neutral-foreground-1-hover-3)",
        }}>{props.xOutline24 ?? <Placeholder size={"24"} theme={"regular"} />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
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
          padding: "0px 0px 0px 0px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingTop: "0px", paddingBottom: "0px",
          flexShrink: 0,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 600,
            fontSize: 16,
            whiteSpace: "nowrap",
            lineHeight: "22px",
            color: "var(--neutral-foreground-1-hover-3)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.label}</span>
        </div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-1-hover-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
    </div>
  );
  const __body26 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      borderTop: "1px solid var(--neutral-stroke-1-rest-3)",
      borderRight: "1px solid var(--neutral-stroke-1-rest-3)",
      borderBottom: "1px solid var(--neutral-stroke-1-rest-3)",
      borderLeft: "1px solid var(--neutral-stroke-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      padding: "7px 7px 7px 7px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "max(0px, calc(var(--spacing-horizontal-s) * 1px - 1px))",
      paddingTop: "max(0px, calc(var(--vertical-s) * 1px - 1px))",
      paddingRight: "max(0px, calc(var(--spacing-horizontal-s) * 1px - 1px))",
      paddingBottom: "max(0px, calc(var(--vertical-s) * 1px - 1px))",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xxs) * 1px)",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
            color: "var(--neutral-foreground-1-rest-3)",
          }}>{props.xOutline24 ?? <Placeholder size={"24"} theme={"regular"} />}</div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-1-rest-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
      {props.focus && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 40,
        height: 40,
        overflow: "hidden",
        borderRadius: 4,
        borderTop: "2px solid var(--neutral-stroke-focus-2-rest-4)",
        borderRight: "2px solid var(--neutral-stroke-focus-2-rest-4)",
        borderBottom: "2px solid var(--neutral-stroke-focus-2-rest-4)",
        borderLeft: "2px solid var(--neutral-stroke-focus-2-rest-4)",
      }}>
        <svg width={40} height={40} viewBox="0 0 40 40" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 40,
          height: 40,
          overflow: "hidden",
          borderRadius: 4,
          borderTop: "1px solid var(--neutral-stroke-focus-1-rest-4)",
          borderRight: "1px solid var(--neutral-stroke-focus-1-rest-4)",
          borderBottom: "1px solid var(--neutral-stroke-focus-1-rest-4)",
          borderLeft: "1px solid var(--neutral-stroke-focus-1-rest-4)",
        }}>
          <path d={"M 4 0 L 4 1 L 36 1 L 36 0 L 36 -1 L 4 -1 L 4 0 Z M 40 4 L 39 4 L 39 36 L 40 36 L 41 36 L 41 4 L 40 4 Z M 36 40 L 36 39 L 4 39 L 4 40 L 4 41 L 36 41 L 36 40 Z M 0 36 L 1 36 L 1 4 L 0 4 L -1 4 L -1 36 L 0 36 Z M 4 40 L 4 39 C 2.343 39 1 37.657 1 36 L 0 36 L -1 36 C -1 38.761 1.239 41 4 41 L 4 40 Z M 40 36 L 39 36 C 39 37.657 37.657 39 36 39 L 36 40 L 36 41 C 38.761 41 41 38.761 41 36 L 40 36 Z M 36 0 L 36 1 C 37.657 1 39 2.343 39 4 L 40 4 L 41 4 C 41 1.239 38.761 -1 36 -1 L 36 0 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
    </div>
  );
  const __body27 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      borderTop: "1px solid var(--neutral-stroke-1-rest-3)",
      borderRight: "1px solid var(--neutral-stroke-1-rest-3)",
      borderBottom: "1px solid var(--neutral-stroke-1-rest-3)",
      borderLeft: "1px solid var(--neutral-stroke-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-snudge) * 1px)",
      padding: "7px 15px 7px 15px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "max(0px, calc(var(--spacing-horizontal-l) * 1px - 1px))",
      paddingTop: "max(0px, calc(var(--vertical-s) * 1px - 1px))",
      paddingRight: "max(0px, calc(var(--spacing-horizontal-l) * 1px - 1px))",
      paddingBottom: "max(0px, calc(var(--vertical-s) * 1px - 1px))",
      position: "relative",
      ...props.style,
    }}>
      {props.icon && (
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
          color: "var(--neutral-foreground-1-rest-3)",
        }}>{props.xOutline24 ?? <Placeholder size={"24"} theme={"regular"} />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
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
          padding: "0px 0px 0px 0px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingTop: "0px", paddingBottom: "0px",
          flexShrink: 0,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 600,
            fontSize: 16,
            whiteSpace: "nowrap",
            lineHeight: "22px",
            color: "var(--neutral-foreground-1-rest-3)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.label}</span>
        </div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-1-rest-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
      {props.focus && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 112,
        height: 40,
        overflow: "hidden",
        borderRadius: 4,
        borderTop: "2px solid var(--neutral-stroke-focus-2-rest-4)",
        borderRight: "2px solid var(--neutral-stroke-focus-2-rest-4)",
        borderBottom: "2px solid var(--neutral-stroke-focus-2-rest-4)",
        borderLeft: "2px solid var(--neutral-stroke-focus-2-rest-4)",
      }}>
        <svg width={112} height={40} viewBox="0 0 112 40" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 112,
          height: 40,
          overflow: "hidden",
          borderRadius: 4,
          borderTop: "1px solid var(--neutral-stroke-focus-1-rest-4)",
          borderRight: "1px solid var(--neutral-stroke-focus-1-rest-4)",
          borderBottom: "1px solid var(--neutral-stroke-focus-1-rest-4)",
          borderLeft: "1px solid var(--neutral-stroke-focus-1-rest-4)",
        }}>
          <path d={"M 4 0 L 4 1 L 108 1 L 108 0 L 108 -1 L 4 -1 L 4 0 Z M 112 4 L 111 4 L 111 36 L 112 36 L 113 36 L 113 4 L 112 4 Z M 108 40 L 108 39 L 4 39 L 4 40 L 4 41 L 108 41 L 108 40 Z M 0 36 L 1 36 L 1 4 L 0 4 L -1 4 L -1 36 L 0 36 Z M 4 40 L 4 39 C 2.343 39 1 37.657 1 36 L 0 36 L -1 36 C -1 38.761 1.239 41 4 41 L 4 40 Z M 112 36 L 111 36 C 111 37.657 109.657 39 108 39 L 108 40 L 108 41 C 110.761 41 113 38.761 113 36 L 112 36 Z M 108 0 L 108 1 C 109.657 1 111 2.343 111 4 L 112 4 L 113 4 C 113 1.239 110.761 -1 108 -1 L 108 0 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
    </div>
  );
  const __body28 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-disabled-rest-4)",
      display: "flex",
      flexDirection: "row",
      padding: "8px 8px 8px 8px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-s) * 1px)",
      paddingTop: "calc(var(--vertical-s) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s) * 1px)",
      paddingBottom: "calc(var(--vertical-s) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xxs) * 1px)",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
            color: "var(--neutral-foreground-disabled-rest-4)",
          }}>{props.xOutline24 ?? <Placeholder size={"24"} theme={"regular"} />}</div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-disabled-rest-4)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
    </div>
  );
  const __body29 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-disabled-rest-4)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-snudge) * 1px)",
      padding: "8px 16px 8px 16px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-l) * 1px)",
      paddingTop: "calc(var(--vertical-s) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-l) * 1px)",
      paddingBottom: "calc(var(--vertical-s) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.icon && (
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
          color: "var(--neutral-foreground-disabled-rest-4)",
        }}>{props.xOutline24 ?? <Placeholder size={"24"} theme={"regular"} />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
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
          padding: "0px 0px 0px 0px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingTop: "0px", paddingBottom: "0px",
          flexShrink: 0,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 600,
            fontSize: 16,
            whiteSpace: "nowrap",
            lineHeight: "22px",
            color: "var(--neutral-foreground-disabled-rest-4)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.label}</span>
        </div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-disabled-rest-4)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
    </div>
  );
  const __body30 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--brand-background-1-selected-3)",
      display: "flex",
      flexDirection: "row",
      padding: "8px 8px 8px 8px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-s) * 1px)",
      paddingTop: "calc(var(--vertical-s) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s) * 1px)",
      paddingBottom: "calc(var(--vertical-s) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xxs) * 1px)",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
            color: "var(--neutral-foreground-on-brand-rest-3)",
          }}>{props.xFilled24 ?? <Placeholder size={"24"} theme={"filled"} />}</div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-on-brand-rest-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"filled"} />}</div>
        )}
      </div>
    </div>
  );
  const __body31 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--brand-background-1-selected-3)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-snudge) * 1px)",
      padding: "8px 16px 8px 16px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-l) * 1px)",
      paddingTop: "calc(var(--vertical-s) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-l) * 1px)",
      paddingBottom: "calc(var(--vertical-s) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.icon && (
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
          color: "var(--neutral-foreground-on-brand-rest-3)",
        }}>{props.xFilled24 ?? <Placeholder size={"24"} theme={"filled"} />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
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
          padding: "0px 0px 0px 0px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingTop: "0px", paddingBottom: "0px",
          flexShrink: 0,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 600,
            fontSize: 16,
            whiteSpace: "nowrap",
            lineHeight: "22px",
            color: "var(--neutral-foreground-on-brand-rest-3)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.label}</span>
        </div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-on-brand-rest-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"filled"} />}</div>
        )}
      </div>
    </div>
  );
  const __body32 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--brand-background-1-pressed-3)",
      display: "flex",
      flexDirection: "row",
      padding: "8px 8px 8px 8px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-s) * 1px)",
      paddingTop: "calc(var(--vertical-s) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s) * 1px)",
      paddingBottom: "calc(var(--vertical-s) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xxs) * 1px)",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
            color: "var(--neutral-foreground-on-brand-rest-3)",
          }}>{props.xOutline24 ?? <Placeholder size={"24"} theme={"regular"} />}</div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-on-brand-rest-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
    </div>
  );
  const __body33 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--brand-background-1-pressed-3)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-snudge) * 1px)",
      padding: "8px 16px 8px 16px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-l) * 1px)",
      paddingTop: "calc(var(--vertical-s) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-l) * 1px)",
      paddingBottom: "calc(var(--vertical-s) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.icon && (
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
          color: "var(--neutral-foreground-on-brand-rest-3)",
        }}>{props.xOutline24 ?? <Placeholder size={"24"} theme={"regular"} />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
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
          padding: "0px 0px 0px 0px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingTop: "0px", paddingBottom: "0px",
          flexShrink: 0,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 600,
            fontSize: 16,
            whiteSpace: "nowrap",
            lineHeight: "22px",
            color: "var(--neutral-foreground-on-brand-rest-3)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.label}</span>
        </div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-on-brand-rest-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
    </div>
  );
  const __body34 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--brand-background-1-hover-3)",
      display: "flex",
      flexDirection: "row",
      padding: "8px 8px 8px 8px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-s) * 1px)",
      paddingTop: "calc(var(--vertical-s) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s) * 1px)",
      paddingBottom: "calc(var(--vertical-s) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xxs) * 1px)",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
            color: "var(--neutral-foreground-on-brand-rest-3)",
          }}>{props.xOutline24 ?? <Placeholder size={"24"} theme={"regular"} />}</div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-on-brand-rest-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
    </div>
  );
  const __body35 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--brand-background-1-hover-3)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-snudge) * 1px)",
      padding: "8px 16px 8px 16px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-l) * 1px)",
      paddingTop: "calc(var(--vertical-s) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-l) * 1px)",
      paddingBottom: "calc(var(--vertical-s) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.icon && (
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
          color: "var(--neutral-foreground-on-brand-rest-3)",
        }}>{props.xOutline24 ?? <Placeholder size={"24"} theme={"regular"} />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
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
          padding: "0px 0px 0px 0px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingTop: "0px", paddingBottom: "0px",
          flexShrink: 0,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 600,
            fontSize: 16,
            whiteSpace: "nowrap",
            lineHeight: "22px",
            color: "var(--neutral-foreground-on-brand-rest-3)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.label}</span>
        </div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-on-brand-rest-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
    </div>
  );
  const __body36 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--brand-background-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      padding: "8px 8px 8px 8px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-s) * 1px)",
      paddingTop: "calc(var(--vertical-s) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s) * 1px)",
      paddingBottom: "calc(var(--vertical-s) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xxs) * 1px)",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
            color: "var(--neutral-foreground-on-brand-rest-3)",
          }}>{props.xOutline24 ?? <Placeholder size={"24"} theme={"regular"} />}</div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-on-brand-rest-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
      {props.focus && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 40,
        height: 40,
        overflow: "hidden",
        borderRadius: 4,
        borderTop: "2px solid var(--neutral-stroke-focus-2-rest-4)",
        borderRight: "2px solid var(--neutral-stroke-focus-2-rest-4)",
        borderBottom: "2px solid var(--neutral-stroke-focus-2-rest-4)",
        borderLeft: "2px solid var(--neutral-stroke-focus-2-rest-4)",
      }}>
        <svg width={40} height={40} viewBox="0 0 40 40" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 40,
          height: 40,
          overflow: "hidden",
          borderRadius: 4,
          borderTop: "1px solid var(--neutral-stroke-focus-1-rest-4)",
          borderRight: "1px solid var(--neutral-stroke-focus-1-rest-4)",
          borderBottom: "1px solid var(--neutral-stroke-focus-1-rest-4)",
          borderLeft: "1px solid var(--neutral-stroke-focus-1-rest-4)",
        }}>
          <path d={"M 4 0 L 4 1 L 36 1 L 36 0 L 36 -1 L 4 -1 L 4 0 Z M 40 4 L 39 4 L 39 36 L 40 36 L 41 36 L 41 4 L 40 4 Z M 36 40 L 36 39 L 4 39 L 4 40 L 4 41 L 36 41 L 36 40 Z M 0 36 L 1 36 L 1 4 L 0 4 L -1 4 L -1 36 L 0 36 Z M 4 40 L 4 39 C 2.343 39 1 37.657 1 36 L 0 36 L -1 36 C -1 38.761 1.239 41 4 41 L 4 40 Z M 40 36 L 39 36 C 39 37.657 37.657 39 36 39 L 36 40 L 36 41 C 38.761 41 41 38.761 41 36 L 40 36 Z M 36 0 L 36 1 C 37.657 1 39 2.343 39 4 L 40 4 L 41 4 C 41 1.239 38.761 -1 36 -1 L 36 0 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
    </div>
  );
  const __body37 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--brand-background-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-snudge) * 1px)",
      padding: "8px 16px 8px 16px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-l) * 1px)",
      paddingTop: "calc(var(--vertical-s) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-l) * 1px)",
      paddingBottom: "calc(var(--vertical-s) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.icon && (
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
          color: "var(--neutral-foreground-on-brand-rest-3)",
        }}>{props.xOutline24 ?? <Placeholder size={"24"} theme={"regular"} />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
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
          padding: "0px 0px 0px 0px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingTop: "0px", paddingBottom: "0px",
          flexShrink: 0,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 600,
            fontSize: 16,
            whiteSpace: "nowrap",
            lineHeight: "22px",
            color: "var(--neutral-foreground-on-brand-rest-3)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.label}</span>
        </div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-on-brand-rest-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
      {props.focus && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 112,
        height: 40,
        overflow: "hidden",
        borderRadius: 4,
        borderTop: "2px solid var(--neutral-stroke-focus-2-rest-4)",
        borderRight: "2px solid var(--neutral-stroke-focus-2-rest-4)",
        borderBottom: "2px solid var(--neutral-stroke-focus-2-rest-4)",
        borderLeft: "2px solid var(--neutral-stroke-focus-2-rest-4)",
      }}>
        <svg width={112} height={40} viewBox="0 0 112 40" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 112,
          height: 40,
          overflow: "hidden",
          borderRadius: 4,
          borderTop: "1px solid var(--neutral-stroke-focus-1-rest-4)",
          borderRight: "1px solid var(--neutral-stroke-focus-1-rest-4)",
          borderBottom: "1px solid var(--neutral-stroke-focus-1-rest-4)",
          borderLeft: "1px solid var(--neutral-stroke-focus-1-rest-4)",
        }}>
          <path d={"M 4 0 L 4 1 L 108 1 L 108 0 L 108 -1 L 4 -1 L 4 0 Z M 112 4 L 111 4 L 111 36 L 112 36 L 113 36 L 113 4 L 112 4 Z M 108 40 L 108 39 L 4 39 L 4 40 L 4 41 L 108 41 L 108 40 Z M 0 36 L 1 36 L 1 4 L 0 4 L -1 4 L -1 36 L 0 36 Z M 4 40 L 4 39 C 2.343 39 1 37.657 1 36 L 0 36 L -1 36 C -1 38.761 1.239 41 4 41 L 4 40 Z M 112 36 L 111 36 C 111 37.657 109.657 39 108 39 L 108 40 L 108 41 C 110.761 41 113 38.761 113 36 L 112 36 Z M 108 0 L 108 1 C 109.657 1 111 2.343 111 4 L 112 4 L 113 4 C 113 1.239 110.761 -1 108 -1 L 108 0 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
    </div>
  );
  const __body38 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-disabled-rest-4)",
      borderTop: "1px solid var(--neutral-stroke-disabled-rest-4)",
      borderRight: "1px solid var(--neutral-stroke-disabled-rest-4)",
      borderBottom: "1px solid var(--neutral-stroke-disabled-rest-4)",
      borderLeft: "1px solid var(--neutral-stroke-disabled-rest-4)",
      display: "flex",
      flexDirection: "row",
      padding: "7px 7px 7px 7px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "max(0px, calc(var(--spacing-horizontal-s) * 1px - 1px))",
      paddingTop: "max(0px, calc(var(--vertical-s) * 1px - 1px))",
      paddingRight: "max(0px, calc(var(--spacing-horizontal-s) * 1px - 1px))",
      paddingBottom: "max(0px, calc(var(--vertical-s) * 1px - 1px))",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xxs) * 1px)",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
            color: "var(--neutral-foreground-disabled-rest-4)",
          }}>{props.xOutline24 ?? <Placeholder size={"24"} theme={"regular"} />}</div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-disabled-rest-4)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
    </div>
  );
  const __body39 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-disabled-rest-4)",
      borderTop: "1px solid var(--neutral-stroke-disabled-rest-4)",
      borderRight: "1px solid var(--neutral-stroke-disabled-rest-4)",
      borderBottom: "1px solid var(--neutral-stroke-disabled-rest-4)",
      borderLeft: "1px solid var(--neutral-stroke-disabled-rest-4)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-snudge) * 1px)",
      padding: "7px 15px 7px 15px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "max(0px, calc(var(--spacing-horizontal-l) * 1px - 1px))",
      paddingTop: "max(0px, calc(var(--vertical-s) * 1px - 1px))",
      paddingRight: "max(0px, calc(var(--spacing-horizontal-l) * 1px - 1px))",
      paddingBottom: "max(0px, calc(var(--vertical-s) * 1px - 1px))",
      position: "relative",
      ...props.style,
    }}>
      {props.icon && (
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
          color: "var(--neutral-foreground-disabled-rest-4)",
        }}>{props.xOutline24 ?? <Placeholder size={"24"} theme={"regular"} />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
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
          padding: "0px 0px 0px 0px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingTop: "0px", paddingBottom: "0px",
          flexShrink: 0,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 600,
            fontSize: 16,
            whiteSpace: "nowrap",
            lineHeight: "22px",
            color: "var(--neutral-foreground-disabled-rest-4)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.label}</span>
        </div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-disabled-rest-4)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
    </div>
  );
  const __body40 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-1-selected-3)",
      borderTop: "1px solid var(--neutral-stroke-1-selected-3)",
      borderRight: "1px solid var(--neutral-stroke-1-selected-3)",
      borderBottom: "1px solid var(--neutral-stroke-1-selected-3)",
      borderLeft: "1px solid var(--neutral-stroke-1-selected-3)",
      display: "flex",
      flexDirection: "row",
      padding: "7px 7px 7px 7px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "max(0px, calc(var(--spacing-horizontal-s) * 1px - 1px))",
      paddingTop: "max(0px, calc(var(--vertical-s) * 1px - 1px))",
      paddingRight: "max(0px, calc(var(--spacing-horizontal-s) * 1px - 1px))",
      paddingBottom: "max(0px, calc(var(--vertical-s) * 1px - 1px))",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xxs) * 1px)",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
            color: "var(--neutral-foreground-1-selected-3)",
          }}>{props.xFilled24 ?? <Placeholder size={"24"} theme={"filled"} />}</div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-1-selected-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"filled"} />}</div>
        )}
      </div>
    </div>
  );
  const __body41 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-1-selected-3)",
      borderTop: "1px solid var(--neutral-stroke-1-selected-3)",
      borderRight: "1px solid var(--neutral-stroke-1-selected-3)",
      borderBottom: "1px solid var(--neutral-stroke-1-selected-3)",
      borderLeft: "1px solid var(--neutral-stroke-1-selected-3)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-snudge) * 1px)",
      padding: "7px 15px 7px 15px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "max(0px, calc(var(--spacing-horizontal-l) * 1px - 1px))",
      paddingTop: "max(0px, calc(var(--vertical-s) * 1px - 1px))",
      paddingRight: "max(0px, calc(var(--spacing-horizontal-l) * 1px - 1px))",
      paddingBottom: "max(0px, calc(var(--vertical-s) * 1px - 1px))",
      position: "relative",
      ...props.style,
    }}>
      {props.icon && (
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
          color: "var(--neutral-foreground-1-selected-3)",
        }}>{props.xFilled24 ?? <Placeholder size={"24"} theme={"filled"} />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
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
          padding: "0px 0px 0px 0px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingTop: "0px", paddingBottom: "0px",
          flexShrink: 0,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 600,
            fontSize: 16,
            whiteSpace: "nowrap",
            lineHeight: "22px",
            color: "var(--neutral-foreground-1-pressed-3)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.label}</span>
        </div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-1-selected-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"filled"} />}</div>
        )}
      </div>
    </div>
  );
  const __body42 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-1-pressed-3)",
      borderTop: "1px solid var(--neutral-stroke-1-pressed-3)",
      borderRight: "1px solid var(--neutral-stroke-1-pressed-3)",
      borderBottom: "1px solid var(--neutral-stroke-1-pressed-3)",
      borderLeft: "1px solid var(--neutral-stroke-1-pressed-3)",
      display: "flex",
      flexDirection: "row",
      padding: "7px 7px 7px 7px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "max(0px, calc(var(--spacing-horizontal-s) * 1px - 1px))",
      paddingTop: "max(0px, calc(var(--vertical-s) * 1px - 1px))",
      paddingRight: "max(0px, calc(var(--spacing-horizontal-s) * 1px - 1px))",
      paddingBottom: "max(0px, calc(var(--vertical-s) * 1px - 1px))",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xxs) * 1px)",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
            color: "var(--neutral-foreground-1-pressed-3)",
          }}>{props.xOutline24 ?? <Placeholder size={"24"} theme={"regular"} />}</div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-1-pressed-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
    </div>
  );
  const __body43 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-1-pressed-3)",
      borderTop: "1px solid var(--neutral-stroke-1-pressed-3)",
      borderRight: "1px solid var(--neutral-stroke-1-pressed-3)",
      borderBottom: "1px solid var(--neutral-stroke-1-pressed-3)",
      borderLeft: "1px solid var(--neutral-stroke-1-pressed-3)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-snudge) * 1px)",
      padding: "7px 15px 7px 15px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "max(0px, calc(var(--spacing-horizontal-l) * 1px - 1px))",
      paddingTop: "max(0px, calc(var(--vertical-s) * 1px - 1px))",
      paddingRight: "max(0px, calc(var(--spacing-horizontal-l) * 1px - 1px))",
      paddingBottom: "max(0px, calc(var(--vertical-s) * 1px - 1px))",
      position: "relative",
      ...props.style,
    }}>
      {props.icon && (
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
          color: "var(--neutral-foreground-1-pressed-3)",
        }}>{props.xOutline24 ?? <Placeholder size={"24"} theme={"regular"} />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
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
          padding: "0px 0px 0px 0px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingTop: "0px", paddingBottom: "0px",
          flexShrink: 0,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 600,
            fontSize: 16,
            whiteSpace: "nowrap",
            lineHeight: "22px",
            color: "var(--neutral-foreground-1-pressed-3)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.label}</span>
        </div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-1-pressed-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
    </div>
  );
  const __body44 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-1-hover-3)",
      borderTop: "1px solid var(--neutral-stroke-1-hover-3)",
      borderRight: "1px solid var(--neutral-stroke-1-hover-3)",
      borderBottom: "1px solid var(--neutral-stroke-1-hover-3)",
      borderLeft: "1px solid var(--neutral-stroke-1-hover-3)",
      display: "flex",
      flexDirection: "row",
      padding: "7px 7px 7px 7px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "max(0px, calc(var(--spacing-horizontal-s) * 1px - 1px))",
      paddingTop: "max(0px, calc(var(--vertical-s) * 1px - 1px))",
      paddingRight: "max(0px, calc(var(--spacing-horizontal-s) * 1px - 1px))",
      paddingBottom: "max(0px, calc(var(--vertical-s) * 1px - 1px))",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xxs) * 1px)",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
            color: "var(--neutral-foreground-1-hover-3)",
          }}>{props.xOutline24 ?? <Placeholder size={"24"} theme={"regular"} />}</div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-1-hover-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
    </div>
  );
  const __body45 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-1-hover-3)",
      borderTop: "1px solid var(--neutral-stroke-1-hover-3)",
      borderRight: "1px solid var(--neutral-stroke-1-hover-3)",
      borderBottom: "1px solid var(--neutral-stroke-1-hover-3)",
      borderLeft: "1px solid var(--neutral-stroke-1-hover-3)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-snudge) * 1px)",
      padding: "7px 15px 7px 15px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "max(0px, calc(var(--spacing-horizontal-l) * 1px - 1px))",
      paddingTop: "max(0px, calc(var(--vertical-s) * 1px - 1px))",
      paddingRight: "max(0px, calc(var(--spacing-horizontal-l) * 1px - 1px))",
      paddingBottom: "max(0px, calc(var(--vertical-s) * 1px - 1px))",
      position: "relative",
      ...props.style,
    }}>
      {props.icon && (
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
          color: "var(--neutral-foreground-1-hover-3)",
        }}>{props.xOutline24 ?? <Placeholder size={"24"} theme={"regular"} />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
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
          padding: "0px 0px 0px 0px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingTop: "0px", paddingBottom: "0px",
          flexShrink: 0,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 600,
            fontSize: 16,
            whiteSpace: "nowrap",
            lineHeight: "22px",
            color: "var(--neutral-foreground-1-hover-3)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.label}</span>
        </div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-1-hover-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
    </div>
  );
  const __body46 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-1-rest-3)",
      borderTop: "1px solid var(--neutral-stroke-1-rest-3)",
      borderRight: "1px solid var(--neutral-stroke-1-rest-3)",
      borderBottom: "1px solid var(--neutral-stroke-1-rest-3)",
      borderLeft: "1px solid var(--neutral-stroke-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      padding: "7px 7px 7px 7px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "max(0px, calc(var(--spacing-horizontal-s) * 1px - 1px))",
      paddingTop: "max(0px, calc(var(--vertical-s) * 1px - 1px))",
      paddingRight: "max(0px, calc(var(--spacing-horizontal-s) * 1px - 1px))",
      paddingBottom: "max(0px, calc(var(--vertical-s) * 1px - 1px))",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xxs) * 1px)",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
            color: "var(--neutral-foreground-1-rest-3)",
          }}>{props.xOutline24 ?? <Placeholder size={"24"} theme={"regular"} />}</div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-1-rest-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
      {props.focus && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 40,
        height: 40,
        overflow: "hidden",
        borderRadius: 4,
        borderTop: "2px solid var(--neutral-stroke-focus-2-rest-4)",
        borderRight: "2px solid var(--neutral-stroke-focus-2-rest-4)",
        borderBottom: "2px solid var(--neutral-stroke-focus-2-rest-4)",
        borderLeft: "2px solid var(--neutral-stroke-focus-2-rest-4)",
      }}>
        <svg width={40} height={40} viewBox="0 0 40 40" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 40,
          height: 40,
          overflow: "hidden",
          borderRadius: 4,
          borderTop: "1px solid var(--neutral-stroke-focus-1-rest-4)",
          borderRight: "1px solid var(--neutral-stroke-focus-1-rest-4)",
          borderBottom: "1px solid var(--neutral-stroke-focus-1-rest-4)",
          borderLeft: "1px solid var(--neutral-stroke-focus-1-rest-4)",
        }}>
          <path d={"M 4 0 L 4 1 L 36 1 L 36 0 L 36 -1 L 4 -1 L 4 0 Z M 40 4 L 39 4 L 39 36 L 40 36 L 41 36 L 41 4 L 40 4 Z M 36 40 L 36 39 L 4 39 L 4 40 L 4 41 L 36 41 L 36 40 Z M 0 36 L 1 36 L 1 4 L 0 4 L -1 4 L -1 36 L 0 36 Z M 4 40 L 4 39 C 2.343 39 1 37.657 1 36 L 0 36 L -1 36 C -1 38.761 1.239 41 4 41 L 4 40 Z M 40 36 L 39 36 C 39 37.657 37.657 39 36 39 L 36 40 L 36 41 C 38.761 41 41 38.761 41 36 L 40 36 Z M 36 0 L 36 1 C 37.657 1 39 2.343 39 4 L 40 4 L 41 4 C 41 1.239 38.761 -1 36 -1 L 36 0 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
    </div>
  );
  const __body47 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-1-rest-3)",
      borderTop: "1px solid var(--neutral-stroke-1-rest-3)",
      borderRight: "1px solid var(--neutral-stroke-1-rest-3)",
      borderBottom: "1px solid var(--neutral-stroke-1-rest-3)",
      borderLeft: "1px solid var(--neutral-stroke-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-snudge) * 1px)",
      padding: "7px 15px 7px 15px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "max(0px, calc(var(--spacing-horizontal-l) * 1px - 1px))",
      paddingTop: "max(0px, calc(var(--vertical-s) * 1px - 1px))",
      paddingRight: "max(0px, calc(var(--spacing-horizontal-l) * 1px - 1px))",
      paddingBottom: "max(0px, calc(var(--vertical-s) * 1px - 1px))",
      position: "relative",
      ...props.style,
    }}>
      {props.icon && (
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
          color: "var(--neutral-foreground-1-rest-3)",
        }}>{props.xOutline24 ?? <Placeholder size={"24"} theme={"regular"} />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
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
          padding: "0px 0px 0px 0px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingTop: "0px", paddingBottom: "0px",
          flexShrink: 0,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 600,
            fontSize: 16,
            whiteSpace: "nowrap",
            lineHeight: "22px",
            color: "var(--neutral-foreground-1-rest-3)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.label}</span>
        </div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-1-rest-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
      {props.focus && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 112,
        height: 40,
        overflow: "hidden",
        borderRadius: 4,
        borderTop: "2px solid var(--neutral-stroke-focus-2-rest-4)",
        borderRight: "2px solid var(--neutral-stroke-focus-2-rest-4)",
        borderBottom: "2px solid var(--neutral-stroke-focus-2-rest-4)",
        borderLeft: "2px solid var(--neutral-stroke-focus-2-rest-4)",
      }}>
        <svg width={112} height={40} viewBox="0 0 112 40" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 112,
          height: 40,
          overflow: "hidden",
          borderRadius: 4,
          borderTop: "1px solid var(--neutral-stroke-focus-1-rest-4)",
          borderRight: "1px solid var(--neutral-stroke-focus-1-rest-4)",
          borderBottom: "1px solid var(--neutral-stroke-focus-1-rest-4)",
          borderLeft: "1px solid var(--neutral-stroke-focus-1-rest-4)",
        }}>
          <path d={"M 4 0 L 4 1 L 108 1 L 108 0 L 108 -1 L 4 -1 L 4 0 Z M 112 4 L 111 4 L 111 36 L 112 36 L 113 36 L 113 4 L 112 4 Z M 108 40 L 108 39 L 4 39 L 4 40 L 4 41 L 108 41 L 108 40 Z M 0 36 L 1 36 L 1 4 L 0 4 L -1 4 L -1 36 L 0 36 Z M 4 40 L 4 39 C 2.343 39 1 37.657 1 36 L 0 36 L -1 36 C -1 38.761 1.239 41 4 41 L 4 40 Z M 112 36 L 111 36 C 111 37.657 109.657 39 108 39 L 108 40 L 108 41 C 110.761 41 113 38.761 113 36 L 112 36 Z M 108 0 L 108 1 C 109.657 1 111 2.343 111 4 L 112 4 L 113 4 C 113 1.239 110.761 -1 108 -1 L 108 0 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
    </div>
  );
  const __body48 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "2px 2px 2px 2px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-xxs) * 1px)",
      paddingTop: "calc(var(--vertical-xxs) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xxs) * 1px)",
      paddingBottom: "calc(var(--vertical-xxs) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
            color: "var(--neutral-foreground-disabled-rest-4)",
          }}>{props.xOutline20 ?? <Placeholder size={"20"} theme={"regular"} />}</div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-disabled-rest-4)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
    </div>
  );
  const __body49 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xs) * 1px)",
      padding: "2px 8px 2px 8px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-s) * 1px)",
      paddingTop: "calc(var(--vertical-xxs) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s) * 1px)",
      paddingBottom: "calc(var(--vertical-xxs) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.icon && (
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
          color: "var(--neutral-foreground-disabled-rest-4)",
        }}>{props.xOutline20 ?? <Placeholder size={"20"} theme={"regular"} />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
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
          padding: "0px 0px 0px 0px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingTop: "0px", paddingBottom: "0px",
          flexShrink: 0,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            whiteSpace: "nowrap",
            lineHeight: "16px",
            color: "var(--neutral-foreground-disabled-rest-4)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.label}</span>
        </div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-disabled-rest-4)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
    </div>
  );
  const __body50 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "2px 2px 2px 2px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-xxs) * 1px)",
      paddingTop: "calc(var(--vertical-xxs) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xxs) * 1px)",
      paddingBottom: "calc(var(--vertical-xxs) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
            color: "var(--neutral-foreground-2-brand-selected-3)",
          }}>{props.xFilled20 ?? <Placeholder size={"20"} theme={"filled"} />}</div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-brand-selected-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"filled"} />}</div>
        )}
      </div>
    </div>
  );
  const __body51 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xs) * 1px)",
      padding: "2px 8px 2px 8px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-s) * 1px)",
      paddingTop: "calc(var(--vertical-xxs) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s) * 1px)",
      paddingBottom: "calc(var(--vertical-xxs) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.icon && (
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
          color: "var(--neutral-foreground-2-brand-selected-3)",
        }}>{props.xFilled20 ?? <Placeholder size={"20"} theme={"filled"} />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
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
          padding: "0px 0px 0px 0px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingTop: "0px", paddingBottom: "0px",
          flexShrink: 0,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            whiteSpace: "nowrap",
            lineHeight: "16px",
            color: "var(--neutral-foreground-2-brand-selected-3)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.label}</span>
        </div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-brand-selected-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"filled"} />}</div>
        )}
      </div>
    </div>
  );
  const __body52 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "2px 2px 2px 2px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-xxs) * 1px)",
      paddingTop: "calc(var(--vertical-xxs) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xxs) * 1px)",
      paddingBottom: "calc(var(--vertical-xxs) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
            color: "var(--neutral-foreground-2-brand-pressed-4)",
          }}>{props.xFilled20 ?? <Placeholder size={"20"} theme={"filled"} />}</div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-brand-pressed-4)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
    </div>
  );
  const __body53 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xs) * 1px)",
      padding: "2px 8px 2px 8px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-s) * 1px)",
      paddingTop: "calc(var(--vertical-xxs) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s) * 1px)",
      paddingBottom: "calc(var(--vertical-xxs) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.icon && (
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
          color: "var(--neutral-foreground-2-brand-pressed-4)",
        }}>{props.xFilled20 ?? <Placeholder size={"20"} theme={"filled"} />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
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
          padding: "0px 0px 0px 0px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingTop: "0px", paddingBottom: "0px",
          flexShrink: 0,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            whiteSpace: "nowrap",
            lineHeight: "16px",
            color: "var(--neutral-foreground-2-brand-pressed-4)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.label}</span>
        </div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-brand-pressed-4)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
    </div>
  );
  const __body54 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "2px 2px 2px 2px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-xxs) * 1px)",
      paddingTop: "calc(var(--vertical-xxs) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xxs) * 1px)",
      paddingBottom: "calc(var(--vertical-xxs) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
            color: "var(--neutral-foreground-2-brand-hover-4)",
          }}>{props.xFilled20 ?? <Placeholder size={"20"} theme={"filled"} />}</div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-brand-hover-4)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
    </div>
  );
  const __body55 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xs) * 1px)",
      padding: "2px 8px 2px 8px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-s) * 1px)",
      paddingTop: "calc(var(--vertical-xxs) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s) * 1px)",
      paddingBottom: "calc(var(--vertical-xxs) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.icon && (
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
          color: "var(--neutral-foreground-2-brand-hover-4)",
        }}>{props.xFilled20 ?? <Placeholder size={"20"} theme={"filled"} />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
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
          padding: "0px 0px 0px 0px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingTop: "0px", paddingBottom: "0px",
          flexShrink: 0,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            whiteSpace: "nowrap",
            lineHeight: "16px",
            color: "var(--neutral-foreground-2-brand-hover-4)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.label}</span>
        </div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-brand-hover-4)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
    </div>
  );
  const __body56 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "2px 2px 2px 2px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-xxs) * 1px)",
      paddingTop: "calc(var(--vertical-xxs) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xxs) * 1px)",
      paddingBottom: "calc(var(--vertical-xxs) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
            color: "var(--neutral-foreground-2-rest-3)",
          }}>{props.xOutline20 ?? <Placeholder size={"20"} theme={"regular"} />}</div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
      {props.focus && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 24,
        height: 24,
        overflow: "hidden",
        borderRadius: 4,
        borderTop: "2px solid var(--neutral-stroke-focus-2-rest-4)",
        borderRight: "2px solid var(--neutral-stroke-focus-2-rest-4)",
        borderBottom: "2px solid var(--neutral-stroke-focus-2-rest-4)",
        borderLeft: "2px solid var(--neutral-stroke-focus-2-rest-4)",
      }}>
        <svg width={24} height={24} viewBox="0 0 24 24" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          overflow: "hidden",
          borderRadius: 4,
          borderTop: "1px solid var(--neutral-stroke-focus-1-rest-4)",
          borderRight: "1px solid var(--neutral-stroke-focus-1-rest-4)",
          borderBottom: "1px solid var(--neutral-stroke-focus-1-rest-4)",
          borderLeft: "1px solid var(--neutral-stroke-focus-1-rest-4)",
        }}>
          <path d={"M 4 0 L 4 1 L 20 1 L 20 0 L 20 -1 L 4 -1 L 4 0 Z M 24 4 L 23 4 L 23 20 L 24 20 L 25 20 L 25 4 L 24 4 Z M 20 24 L 20 23 L 4 23 L 4 24 L 4 25 L 20 25 L 20 24 Z M 0 20 L 1 20 L 1 4 L 0 4 L -1 4 L -1 20 L 0 20 Z M 4 24 L 4 23 C 2.343 23 1 21.657 1 20 L 0 20 L -1 20 C -1 22.761 1.239 25 4 25 L 4 24 Z M 24 20 L 23 20 C 23 21.657 21.657 23 20 23 L 20 24 L 20 25 C 22.761 25 25 22.761 25 20 L 24 20 Z M 20 0 L 20 1 C 21.657 1 23 2.343 23 4 L 24 4 L 25 4 C 25 1.239 22.761 -1 20 -1 L 20 0 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
    </div>
  );
  const __body57 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xs) * 1px)",
      padding: "2px 8px 2px 8px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-s) * 1px)",
      paddingTop: "calc(var(--vertical-xxs) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s) * 1px)",
      paddingBottom: "calc(var(--vertical-xxs) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.icon && (
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
          color: "var(--neutral-foreground-2-rest-3)",
        }}>{props.xOutline20 ?? <Placeholder size={"20"} theme={"regular"} />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
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
          padding: "0px 0px 0px 0px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingTop: "0px", paddingBottom: "0px",
          flexShrink: 0,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            whiteSpace: "nowrap",
            lineHeight: "16px",
            color: "var(--neutral-foreground-2-rest-3)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.label}</span>
        </div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
      {props.focus && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 76,
        height: 24,
        overflow: "hidden",
        borderRadius: 4,
        borderTop: "2px solid var(--neutral-stroke-focus-2-rest-4)",
        borderRight: "2px solid var(--neutral-stroke-focus-2-rest-4)",
        borderBottom: "2px solid var(--neutral-stroke-focus-2-rest-4)",
        borderLeft: "2px solid var(--neutral-stroke-focus-2-rest-4)",
      }}>
        <svg width={76} height={24} viewBox="0 0 76 24" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 76,
          height: 24,
          overflow: "hidden",
          borderRadius: 4,
          borderTop: "1px solid var(--neutral-stroke-focus-1-rest-4)",
          borderRight: "1px solid var(--neutral-stroke-focus-1-rest-4)",
          borderBottom: "1px solid var(--neutral-stroke-focus-1-rest-4)",
          borderLeft: "1px solid var(--neutral-stroke-focus-1-rest-4)",
        }}>
          <path d={"M 4 0 L 4 1 L 72 1 L 72 0 L 72 -1 L 4 -1 L 4 0 Z M 76 4 L 75 4 L 75 20 L 76 20 L 77 20 L 77 4 L 76 4 Z M 72 24 L 72 23 L 4 23 L 4 24 L 4 25 L 72 25 L 72 24 Z M 0 20 L 1 20 L 1 4 L 0 4 L -1 4 L -1 20 L 0 20 Z M 4 24 L 4 23 C 2.343 23 1 21.657 1 20 L 0 20 L -1 20 C -1 22.761 1.239 25 4 25 L 4 24 Z M 76 20 L 75 20 C 75 21.657 73.657 23 72 23 L 72 24 L 72 25 C 74.761 25 77 22.761 77 20 L 76 20 Z M 72 0 L 72 1 C 73.657 1 75 2.343 75 4 L 76 4 L 77 4 C 77 1.239 74.761 -1 72 -1 L 72 0 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
    </div>
  );
  const __body58 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      display: "flex",
      flexDirection: "row",
      padding: "2px 2px 2px 2px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-xxs) * 1px)",
      paddingTop: "calc(var(--vertical-xxs) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xxs) * 1px)",
      paddingBottom: "calc(var(--vertical-xxs) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
            color: "var(--neutral-foreground-disabled-rest-4)",
          }}>{props.xOutline20 ?? <Placeholder size={"20"} theme={"regular"} />}</div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-disabled-rest-4)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
    </div>
  );
  const __body59 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xs) * 1px)",
      padding: "2px 8px 2px 8px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-s) * 1px)",
      paddingTop: "calc(var(--vertical-xxs) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s) * 1px)",
      paddingBottom: "calc(var(--vertical-xxs) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.icon && (
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
          color: "var(--neutral-foreground-disabled-rest-4)",
        }}>{props.xOutline20 ?? <Placeholder size={"20"} theme={"regular"} />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
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
          padding: "0px 0px 0px 0px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingTop: "0px", paddingBottom: "0px",
          flexShrink: 0,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            whiteSpace: "nowrap",
            lineHeight: "16px",
            color: "var(--neutral-foreground-disabled-rest-4)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.label}</span>
        </div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-disabled-rest-4)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
    </div>
  );
  const __body60 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-subtle-selected-3)",
      display: "flex",
      flexDirection: "row",
      padding: "2px 2px 2px 2px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-xxs) * 1px)",
      paddingTop: "calc(var(--vertical-xxs) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xxs) * 1px)",
      paddingBottom: "calc(var(--vertical-xxs) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
            color: "var(--neutral-foreground-2-brand-selected-3)",
          }}>{props.xFilled20 ?? <Placeholder size={"20"} theme={"filled"} />}</div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-selected-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"filled"} />}</div>
        )}
      </div>
    </div>
  );
  const __body61 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-subtle-selected-3)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xs) * 1px)",
      padding: "2px 8px 2px 8px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-s) * 1px)",
      paddingTop: "calc(var(--vertical-xxs) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s) * 1px)",
      paddingBottom: "calc(var(--vertical-xxs) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.icon && (
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
          color: "var(--neutral-foreground-2-brand-selected-3)",
        }}>{props.xFilled20 ?? <Placeholder size={"20"} theme={"filled"} />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
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
          padding: "0px 0px 0px 0px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingTop: "0px", paddingBottom: "0px",
          flexShrink: 0,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            whiteSpace: "nowrap",
            lineHeight: "16px",
            color: "var(--neutral-foreground-1-pressed-3)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.label}</span>
        </div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-selected-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"filled"} />}</div>
        )}
      </div>
    </div>
  );
  const __body62 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-subtle-pressed-3)",
      display: "flex",
      flexDirection: "row",
      padding: "2px 2px 2px 2px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-xxs) * 1px)",
      paddingTop: "calc(var(--vertical-xxs) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xxs) * 1px)",
      paddingBottom: "calc(var(--vertical-xxs) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
            color: "var(--neutral-foreground-2-brand-pressed-4)",
          }}>{props.xFilled20 ?? <Placeholder size={"20"} theme={"filled"} />}</div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-pressed-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
    </div>
  );
  const __body63 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-subtle-pressed-3)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xs) * 1px)",
      padding: "2px 8px 2px 8px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-s) * 1px)",
      paddingTop: "calc(var(--vertical-xxs) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s) * 1px)",
      paddingBottom: "calc(var(--vertical-xxs) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.icon && (
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
          color: "var(--neutral-foreground-2-brand-pressed-4)",
        }}>{props.xFilled20 ?? <Placeholder size={"20"} theme={"filled"} />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
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
          padding: "0px 0px 0px 0px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingTop: "0px", paddingBottom: "0px",
          flexShrink: 0,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            whiteSpace: "nowrap",
            lineHeight: "16px",
            color: "var(--neutral-foreground-1-pressed-3)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.label}</span>
        </div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-pressed-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
    </div>
  );
  const __body64 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-subtle-hover-3)",
      display: "flex",
      flexDirection: "row",
      padding: "2px 2px 2px 2px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-xxs) * 1px)",
      paddingTop: "calc(var(--vertical-xxs) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xxs) * 1px)",
      paddingBottom: "calc(var(--vertical-xxs) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
            color: "var(--neutral-foreground-2-brand-hover-4)",
          }}>{props.xFilled20 ?? <Placeholder size={"20"} theme={"filled"} />}</div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-hover-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
    </div>
  );
  const __body65 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-subtle-hover-3)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xs) * 1px)",
      padding: "2px 8px 2px 8px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-s) * 1px)",
      paddingTop: "calc(var(--vertical-xxs) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s) * 1px)",
      paddingBottom: "calc(var(--vertical-xxs) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.icon && (
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
          color: "var(--neutral-foreground-2-brand-hover-4)",
        }}>{props.xFilled20 ?? <Placeholder size={"20"} theme={"filled"} />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
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
          padding: "0px 0px 0px 0px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingTop: "0px", paddingBottom: "0px",
          flexShrink: 0,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            whiteSpace: "nowrap",
            lineHeight: "16px",
            color: "var(--neutral-foreground-1-hover-3)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.label}</span>
        </div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-hover-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
    </div>
  );
  const __body66 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      borderTop: "1px solid var(--neutral-stroke-disabled-rest-4)",
      borderRight: "1px solid var(--neutral-stroke-disabled-rest-4)",
      borderBottom: "1px solid var(--neutral-stroke-disabled-rest-4)",
      borderLeft: "1px solid var(--neutral-stroke-disabled-rest-4)",
      display: "flex",
      flexDirection: "row",
      padding: "1px 1px 1px 1px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "max(0px, calc(var(--spacing-horizontal-xxs) * 1px - 1px))",
      paddingTop: "max(0px, calc(var(--vertical-xxs) * 1px - 1px))",
      paddingRight: "max(0px, calc(var(--spacing-horizontal-xxs) * 1px - 1px))",
      paddingBottom: "max(0px, calc(var(--vertical-xxs) * 1px - 1px))",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
            color: "var(--neutral-foreground-disabled-rest-4)",
          }}>{props.xOutline20 ?? <Placeholder size={"20"} theme={"regular"} />}</div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-disabled-rest-4)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
    </div>
  );
  const __body67 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      borderTop: "1px solid var(--neutral-stroke-disabled-rest-4)",
      borderRight: "1px solid var(--neutral-stroke-disabled-rest-4)",
      borderBottom: "1px solid var(--neutral-stroke-disabled-rest-4)",
      borderLeft: "1px solid var(--neutral-stroke-disabled-rest-4)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xs) * 1px)",
      padding: "1px 7px 1px 7px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "max(0px, calc(var(--spacing-horizontal-s) * 1px - 1px))",
      paddingTop: "max(0px, calc(var(--vertical-xxs) * 1px - 1px))",
      paddingRight: "max(0px, calc(var(--spacing-horizontal-s) * 1px - 1px))",
      paddingBottom: "max(0px, calc(var(--vertical-xxs) * 1px - 1px))",
      position: "relative",
      ...props.style,
    }}>
      {props.icon && (
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
          color: "var(--neutral-foreground-disabled-rest-4)",
        }}>{props.xOutline20 ?? <Placeholder size={"20"} theme={"regular"} />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
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
          padding: "0px 0px 0px 0px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingTop: "0px", paddingBottom: "0px",
          flexShrink: 0,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            whiteSpace: "nowrap",
            lineHeight: "16px",
            color: "var(--neutral-foreground-disabled-rest-4)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.label}</span>
        </div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-disabled-rest-4)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
    </div>
  );
  const __body68 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      borderTop: "2px solid var(--neutral-stroke-1-selected-3)",
      borderRight: "2px solid var(--neutral-stroke-1-selected-3)",
      borderBottom: "2px solid var(--neutral-stroke-1-selected-3)",
      borderLeft: "2px solid var(--neutral-stroke-1-selected-3)",
      display: "flex",
      flexDirection: "row",
      padding: "2px 2px 2px 2px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-xxs) * 1px)",
      paddingTop: "calc(var(--vertical-xxs) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xxs) * 1px)",
      paddingBottom: "calc(var(--vertical-xxs) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
            color: "var(--neutral-foreground-1-selected-3)",
          }}>{props.xFilled20 ?? <Placeholder size={"20"} theme={"filled"} />}</div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-1-selected-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"filled"} />}</div>
        )}
      </div>
    </div>
  );
  const __body69 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      borderTop: "2px solid var(--neutral-stroke-1-selected-3)",
      borderRight: "2px solid var(--neutral-stroke-1-selected-3)",
      borderBottom: "2px solid var(--neutral-stroke-1-selected-3)",
      borderLeft: "2px solid var(--neutral-stroke-1-selected-3)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xs) * 1px)",
      padding: "2px 8px 2px 8px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-s) * 1px)",
      paddingTop: "calc(var(--vertical-xxs) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s) * 1px)",
      paddingBottom: "calc(var(--vertical-xxs) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.icon && (
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
          color: "var(--neutral-foreground-1-selected-3)",
        }}>{props.xFilled20 ?? <Placeholder size={"20"} theme={"filled"} />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
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
          padding: "0px 0px 0px 0px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingTop: "0px", paddingBottom: "0px",
          flexShrink: 0,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            whiteSpace: "nowrap",
            lineHeight: "16px",
            color: "var(--neutral-foreground-1-pressed-3)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.label}</span>
        </div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-1-selected-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"filled"} />}</div>
        )}
      </div>
    </div>
  );
  const __body70 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      borderTop: "1px solid var(--neutral-stroke-1-pressed-3)",
      borderRight: "1px solid var(--neutral-stroke-1-pressed-3)",
      borderBottom: "1px solid var(--neutral-stroke-1-pressed-3)",
      borderLeft: "1px solid var(--neutral-stroke-1-pressed-3)",
      display: "flex",
      flexDirection: "row",
      padding: "1px 1px 1px 1px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "max(0px, calc(var(--spacing-horizontal-xxs) * 1px - 1px))",
      paddingTop: "max(0px, calc(var(--vertical-xxs) * 1px - 1px))",
      paddingRight: "max(0px, calc(var(--spacing-horizontal-xxs) * 1px - 1px))",
      paddingBottom: "max(0px, calc(var(--vertical-xxs) * 1px - 1px))",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
            color: "var(--neutral-foreground-1-pressed-3)",
          }}>{props.xOutline20 ?? <Placeholder size={"20"} theme={"regular"} />}</div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-1-pressed-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
    </div>
  );
  const __body71 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      borderTop: "1px solid var(--neutral-stroke-1-pressed-3)",
      borderRight: "1px solid var(--neutral-stroke-1-pressed-3)",
      borderBottom: "1px solid var(--neutral-stroke-1-pressed-3)",
      borderLeft: "1px solid var(--neutral-stroke-1-pressed-3)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xs) * 1px)",
      padding: "1px 7px 1px 7px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "max(0px, calc(var(--spacing-horizontal-s) * 1px - 1px))",
      paddingTop: "max(0px, calc(var(--vertical-xxs) * 1px - 1px))",
      paddingRight: "max(0px, calc(var(--spacing-horizontal-s) * 1px - 1px))",
      paddingBottom: "max(0px, calc(var(--vertical-xxs) * 1px - 1px))",
      position: "relative",
      ...props.style,
    }}>
      {props.icon && (
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
          color: "var(--neutral-foreground-1-pressed-3)",
        }}>{props.xOutline20 ?? <Placeholder size={"20"} theme={"regular"} />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
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
          padding: "0px 0px 0px 0px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingTop: "0px", paddingBottom: "0px",
          flexShrink: 0,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            whiteSpace: "nowrap",
            lineHeight: "16px",
            color: "var(--neutral-foreground-1-pressed-3)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.label}</span>
        </div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-1-pressed-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
    </div>
  );
  const __body72 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      borderTop: "1px solid var(--neutral-stroke-1-hover-3)",
      borderRight: "1px solid var(--neutral-stroke-1-hover-3)",
      borderBottom: "1px solid var(--neutral-stroke-1-hover-3)",
      borderLeft: "1px solid var(--neutral-stroke-1-hover-3)",
      display: "flex",
      flexDirection: "row",
      padding: "1px 1px 1px 1px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "max(0px, calc(var(--spacing-horizontal-xxs) * 1px - 1px))",
      paddingTop: "max(0px, calc(var(--vertical-xxs) * 1px - 1px))",
      paddingRight: "max(0px, calc(var(--spacing-horizontal-xxs) * 1px - 1px))",
      paddingBottom: "max(0px, calc(var(--vertical-xxs) * 1px - 1px))",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
            color: "var(--neutral-foreground-1-hover-3)",
          }}>{props.xOutline20 ?? <Placeholder size={"20"} theme={"regular"} />}</div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-1-hover-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
    </div>
  );
  const __body73 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      borderTop: "1px solid var(--neutral-stroke-1-hover-3)",
      borderRight: "1px solid var(--neutral-stroke-1-hover-3)",
      borderBottom: "1px solid var(--neutral-stroke-1-hover-3)",
      borderLeft: "1px solid var(--neutral-stroke-1-hover-3)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xs) * 1px)",
      padding: "1px 7px 1px 7px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "max(0px, calc(var(--spacing-horizontal-s) * 1px - 1px))",
      paddingTop: "max(0px, calc(var(--vertical-xxs) * 1px - 1px))",
      paddingRight: "max(0px, calc(var(--spacing-horizontal-s) * 1px - 1px))",
      paddingBottom: "max(0px, calc(var(--vertical-xxs) * 1px - 1px))",
      position: "relative",
      ...props.style,
    }}>
      {props.icon && (
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
          color: "var(--neutral-foreground-1-hover-3)",
        }}>{props.xOutline20 ?? <Placeholder size={"20"} theme={"regular"} />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
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
          padding: "0px 0px 0px 0px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingTop: "0px", paddingBottom: "0px",
          flexShrink: 0,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            whiteSpace: "nowrap",
            lineHeight: "16px",
            color: "var(--neutral-foreground-1-hover-3)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.label}</span>
        </div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-1-hover-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
    </div>
  );
  const __body74 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      borderTop: "1px solid var(--neutral-stroke-1-rest-3)",
      borderRight: "1px solid var(--neutral-stroke-1-rest-3)",
      borderBottom: "1px solid var(--neutral-stroke-1-rest-3)",
      borderLeft: "1px solid var(--neutral-stroke-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      padding: "1px 1px 1px 1px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "max(0px, calc(var(--spacing-horizontal-xxs) * 1px - 1px))",
      paddingTop: "max(0px, calc(var(--vertical-xxs) * 1px - 1px))",
      paddingRight: "max(0px, calc(var(--spacing-horizontal-xxs) * 1px - 1px))",
      paddingBottom: "max(0px, calc(var(--vertical-xxs) * 1px - 1px))",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
            color: "var(--neutral-foreground-1-rest-3)",
          }}>{props.xOutline20 ?? <Placeholder size={"20"} theme={"regular"} />}</div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-1-rest-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
      {props.focus && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 24,
        height: 24,
        overflow: "hidden",
        borderRadius: 4,
        borderTop: "2px solid var(--neutral-stroke-focus-2-rest-4)",
        borderRight: "2px solid var(--neutral-stroke-focus-2-rest-4)",
        borderBottom: "2px solid var(--neutral-stroke-focus-2-rest-4)",
        borderLeft: "2px solid var(--neutral-stroke-focus-2-rest-4)",
      }}>
        <svg width={24} height={24} viewBox="0 0 24 24" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          overflow: "hidden",
          borderRadius: 4,
          borderTop: "1px solid var(--neutral-stroke-focus-1-rest-4)",
          borderRight: "1px solid var(--neutral-stroke-focus-1-rest-4)",
          borderBottom: "1px solid var(--neutral-stroke-focus-1-rest-4)",
          borderLeft: "1px solid var(--neutral-stroke-focus-1-rest-4)",
        }}>
          <path d={"M 4 0 L 4 1 L 20 1 L 20 0 L 20 -1 L 4 -1 L 4 0 Z M 24 4 L 23 4 L 23 20 L 24 20 L 25 20 L 25 4 L 24 4 Z M 20 24 L 20 23 L 4 23 L 4 24 L 4 25 L 20 25 L 20 24 Z M 0 20 L 1 20 L 1 4 L 0 4 L -1 4 L -1 20 L 0 20 Z M 4 24 L 4 23 C 2.343 23 1 21.657 1 20 L 0 20 L -1 20 C -1 22.761 1.239 25 4 25 L 4 24 Z M 24 20 L 23 20 C 23 21.657 21.657 23 20 23 L 20 24 L 20 25 C 22.761 25 25 22.761 25 20 L 24 20 Z M 20 0 L 20 1 C 21.657 1 23 2.343 23 4 L 24 4 L 25 4 C 25 1.239 22.761 -1 20 -1 L 20 0 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
    </div>
  );
  const __body75 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      borderTop: "1px solid var(--neutral-stroke-1-rest-3)",
      borderRight: "1px solid var(--neutral-stroke-1-rest-3)",
      borderBottom: "1px solid var(--neutral-stroke-1-rest-3)",
      borderLeft: "1px solid var(--neutral-stroke-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xs) * 1px)",
      padding: "1px 7px 1px 7px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "max(0px, calc(var(--spacing-horizontal-s) * 1px - 1px))",
      paddingTop: "max(0px, calc(var(--vertical-xxs) * 1px - 1px))",
      paddingRight: "max(0px, calc(var(--spacing-horizontal-s) * 1px - 1px))",
      paddingBottom: "max(0px, calc(var(--vertical-xxs) * 1px - 1px))",
      position: "relative",
      ...props.style,
    }}>
      {props.icon && (
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
          color: "var(--neutral-foreground-1-rest-3)",
        }}>{props.xOutline20 ?? <Placeholder size={"20"} theme={"regular"} />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
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
          padding: "0px 0px 0px 0px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingTop: "0px", paddingBottom: "0px",
          flexShrink: 0,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            whiteSpace: "nowrap",
            lineHeight: "16px",
            color: "var(--neutral-foreground-1-rest-3)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.label}</span>
        </div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-1-rest-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
      {props.focus && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 76,
        height: 24,
        overflow: "hidden",
        borderRadius: 4,
        borderTop: "2px solid var(--neutral-stroke-focus-2-rest-4)",
        borderRight: "2px solid var(--neutral-stroke-focus-2-rest-4)",
        borderBottom: "2px solid var(--neutral-stroke-focus-2-rest-4)",
        borderLeft: "2px solid var(--neutral-stroke-focus-2-rest-4)",
      }}>
        <svg width={76} height={24} viewBox="0 0 76 24" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 76,
          height: 24,
          overflow: "hidden",
          borderRadius: 4,
          borderTop: "1px solid var(--neutral-stroke-focus-1-rest-4)",
          borderRight: "1px solid var(--neutral-stroke-focus-1-rest-4)",
          borderBottom: "1px solid var(--neutral-stroke-focus-1-rest-4)",
          borderLeft: "1px solid var(--neutral-stroke-focus-1-rest-4)",
        }}>
          <path d={"M 4 0 L 4 1 L 72 1 L 72 0 L 72 -1 L 4 -1 L 4 0 Z M 76 4 L 75 4 L 75 20 L 76 20 L 77 20 L 77 4 L 76 4 Z M 72 24 L 72 23 L 4 23 L 4 24 L 4 25 L 72 25 L 72 24 Z M 0 20 L 1 20 L 1 4 L 0 4 L -1 4 L -1 20 L 0 20 Z M 4 24 L 4 23 C 2.343 23 1 21.657 1 20 L 0 20 L -1 20 C -1 22.761 1.239 25 4 25 L 4 24 Z M 76 20 L 75 20 C 75 21.657 73.657 23 72 23 L 72 24 L 72 25 C 74.761 25 77 22.761 77 20 L 76 20 Z M 72 0 L 72 1 C 73.657 1 75 2.343 75 4 L 76 4 L 77 4 C 77 1.239 74.761 -1 72 -1 L 72 0 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
    </div>
  );
  const __body76 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-disabled-rest-4)",
      display: "flex",
      flexDirection: "row",
      padding: "2px 2px 2px 2px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-xxs) * 1px)",
      paddingTop: "calc(var(--vertical-xxs) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xxs) * 1px)",
      paddingBottom: "calc(var(--vertical-xxs) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
            color: "var(--neutral-foreground-disabled-rest-4)",
          }}>{props.xOutline20 ?? <Placeholder size={"20"} theme={"regular"} />}</div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-disabled-rest-4)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
    </div>
  );
  const __body77 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-disabled-rest-4)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xs) * 1px)",
      padding: "2px 8px 2px 8px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-s) * 1px)",
      paddingTop: "calc(var(--vertical-xxs) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s) * 1px)",
      paddingBottom: "calc(var(--vertical-xxs) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.icon && (
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
          color: "var(--neutral-foreground-disabled-rest-4)",
        }}>{props.xOutline20 ?? <Placeholder size={"20"} theme={"regular"} />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
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
          padding: "0px 0px 0px 0px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingTop: "0px", paddingBottom: "0px",
          flexShrink: 0,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            whiteSpace: "nowrap",
            lineHeight: "16px",
            color: "var(--neutral-foreground-disabled-rest-4)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.label}</span>
        </div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-disabled-rest-4)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
    </div>
  );
  const __body78 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--brand-background-1-selected-3)",
      display: "flex",
      flexDirection: "row",
      padding: "2px 2px 2px 2px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-xxs) * 1px)",
      paddingTop: "calc(var(--vertical-xxs) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xxs) * 1px)",
      paddingBottom: "calc(var(--vertical-xxs) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
            color: "var(--neutral-foreground-on-brand-rest-3)",
          }}>{props.xFilled20 ?? <Placeholder size={"20"} theme={"filled"} />}</div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-on-brand-rest-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"filled"} />}</div>
        )}
      </div>
    </div>
  );
  const __body79 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--brand-background-1-selected-3)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xs) * 1px)",
      padding: "2px 8px 2px 8px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-s) * 1px)",
      paddingTop: "calc(var(--vertical-xxs) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s) * 1px)",
      paddingBottom: "calc(var(--vertical-xxs) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.icon && (
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
          color: "var(--neutral-foreground-on-brand-rest-3)",
        }}>{props.xFilled20 ?? <Placeholder size={"20"} theme={"filled"} />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
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
          padding: "0px 0px 0px 0px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingTop: "0px", paddingBottom: "0px",
          flexShrink: 0,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            whiteSpace: "nowrap",
            lineHeight: "16px",
            color: "var(--neutral-foreground-on-brand-rest-3)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.label}</span>
        </div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-on-brand-rest-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"filled"} />}</div>
        )}
      </div>
    </div>
  );
  const __body80 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--brand-background-1-pressed-3)",
      display: "flex",
      flexDirection: "row",
      padding: "2px 2px 2px 2px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-xxs) * 1px)",
      paddingTop: "calc(var(--vertical-xxs) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xxs) * 1px)",
      paddingBottom: "calc(var(--vertical-xxs) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
            color: "var(--neutral-foreground-on-brand-rest-3)",
          }}>{props.xOutline20 ?? <Placeholder size={"20"} theme={"regular"} />}</div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-on-brand-rest-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
    </div>
  );
  const __body81 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--brand-background-1-pressed-3)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xs) * 1px)",
      padding: "2px 8px 2px 8px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-s) * 1px)",
      paddingTop: "calc(var(--vertical-xxs) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s) * 1px)",
      paddingBottom: "calc(var(--vertical-xxs) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.icon && (
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
          color: "var(--neutral-foreground-on-brand-rest-3)",
        }}>{props.xOutline20 ?? <Placeholder size={"20"} theme={"regular"} />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
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
          padding: "0px 0px 0px 0px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingTop: "0px", paddingBottom: "0px",
          flexShrink: 0,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            whiteSpace: "nowrap",
            lineHeight: "16px",
            color: "var(--neutral-foreground-on-brand-rest-3)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.label}</span>
        </div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-on-brand-rest-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
    </div>
  );
  const __body82 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--brand-background-1-hover-3)",
      display: "flex",
      flexDirection: "row",
      padding: "2px 2px 2px 2px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-xxs) * 1px)",
      paddingTop: "calc(var(--vertical-xxs) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xxs) * 1px)",
      paddingBottom: "calc(var(--vertical-xxs) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
            color: "var(--neutral-foreground-on-brand-rest-3)",
          }}>{props.xOutline20 ?? <Placeholder size={"20"} theme={"regular"} />}</div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-on-brand-rest-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
    </div>
  );
  const __body83 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--brand-background-1-hover-3)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xs) * 1px)",
      padding: "2px 8px 2px 8px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-s) * 1px)",
      paddingTop: "calc(var(--vertical-xxs) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s) * 1px)",
      paddingBottom: "calc(var(--vertical-xxs) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.icon && (
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
          color: "var(--neutral-foreground-on-brand-rest-3)",
        }}>{props.xOutline20 ?? <Placeholder size={"20"} theme={"regular"} />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
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
          padding: "0px 0px 0px 0px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingTop: "0px", paddingBottom: "0px",
          flexShrink: 0,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            whiteSpace: "nowrap",
            lineHeight: "16px",
            color: "var(--neutral-foreground-on-brand-rest-3)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.label}</span>
        </div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-on-brand-rest-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
    </div>
  );
  const __body84 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--brand-background-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      padding: "2px 2px 2px 2px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-xxs) * 1px)",
      paddingTop: "calc(var(--vertical-xxs) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xxs) * 1px)",
      paddingBottom: "calc(var(--vertical-xxs) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
            color: "var(--neutral-foreground-on-brand-rest-3)",
          }}>{props.xOutline20 ?? <Placeholder size={"20"} theme={"regular"} />}</div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-on-brand-rest-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
      {props.focus && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 24,
        height: 24,
        overflow: "hidden",
        borderRadius: 4,
        borderTop: "2px solid var(--neutral-stroke-focus-2-rest-4)",
        borderRight: "2px solid var(--neutral-stroke-focus-2-rest-4)",
        borderBottom: "2px solid var(--neutral-stroke-focus-2-rest-4)",
        borderLeft: "2px solid var(--neutral-stroke-focus-2-rest-4)",
      }}>
        <svg width={24} height={24} viewBox="0 0 24 24" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          overflow: "hidden",
          borderRadius: 4,
          borderTop: "1px solid var(--neutral-stroke-focus-1-rest-4)",
          borderRight: "1px solid var(--neutral-stroke-focus-1-rest-4)",
          borderBottom: "1px solid var(--neutral-stroke-focus-1-rest-4)",
          borderLeft: "1px solid var(--neutral-stroke-focus-1-rest-4)",
        }}>
          <path d={"M 4 0 L 4 1 L 20 1 L 20 0 L 20 -1 L 4 -1 L 4 0 Z M 24 4 L 23 4 L 23 20 L 24 20 L 25 20 L 25 4 L 24 4 Z M 20 24 L 20 23 L 4 23 L 4 24 L 4 25 L 20 25 L 20 24 Z M 0 20 L 1 20 L 1 4 L 0 4 L -1 4 L -1 20 L 0 20 Z M 4 24 L 4 23 C 2.343 23 1 21.657 1 20 L 0 20 L -1 20 C -1 22.761 1.239 25 4 25 L 4 24 Z M 24 20 L 23 20 C 23 21.657 21.657 23 20 23 L 20 24 L 20 25 C 22.761 25 25 22.761 25 20 L 24 20 Z M 20 0 L 20 1 C 21.657 1 23 2.343 23 4 L 24 4 L 25 4 C 25 1.239 22.761 -1 20 -1 L 20 0 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
    </div>
  );
  const __body85 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--brand-background-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xs) * 1px)",
      padding: "2px 8px 2px 8px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-s) * 1px)",
      paddingTop: "calc(var(--vertical-xxs) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-s) * 1px)",
      paddingBottom: "calc(var(--vertical-xxs) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.icon && (
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
          color: "var(--neutral-foreground-on-brand-rest-3)",
        }}>{props.xOutline20 ?? <Placeholder size={"20"} theme={"regular"} />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
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
          padding: "0px 0px 0px 0px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingTop: "0px", paddingBottom: "0px",
          flexShrink: 0,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            whiteSpace: "nowrap",
            lineHeight: "16px",
            color: "var(--neutral-foreground-on-brand-rest-3)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.label}</span>
        </div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-on-brand-rest-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
      {props.focus && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 76,
        height: 24,
        overflow: "hidden",
        borderRadius: 4,
        borderTop: "2px solid var(--neutral-stroke-focus-2-rest-4)",
        borderRight: "2px solid var(--neutral-stroke-focus-2-rest-4)",
        borderBottom: "2px solid var(--neutral-stroke-focus-2-rest-4)",
        borderLeft: "2px solid var(--neutral-stroke-focus-2-rest-4)",
      }}>
        <svg width={76} height={24} viewBox="0 0 76 24" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 76,
          height: 24,
          overflow: "hidden",
          borderRadius: 4,
          borderTop: "1px solid var(--neutral-stroke-focus-1-rest-4)",
          borderRight: "1px solid var(--neutral-stroke-focus-1-rest-4)",
          borderBottom: "1px solid var(--neutral-stroke-focus-1-rest-4)",
          borderLeft: "1px solid var(--neutral-stroke-focus-1-rest-4)",
        }}>
          <path d={"M 4 0 L 4 1 L 72 1 L 72 0 L 72 -1 L 4 -1 L 4 0 Z M 76 4 L 75 4 L 75 20 L 76 20 L 77 20 L 77 4 L 76 4 Z M 72 24 L 72 23 L 4 23 L 4 24 L 4 25 L 72 25 L 72 24 Z M 0 20 L 1 20 L 1 4 L 0 4 L -1 4 L -1 20 L 0 20 Z M 4 24 L 4 23 C 2.343 23 1 21.657 1 20 L 0 20 L -1 20 C -1 22.761 1.239 25 4 25 L 4 24 Z M 76 20 L 75 20 C 75 21.657 73.657 23 72 23 L 72 24 L 72 25 C 74.761 25 77 22.761 77 20 L 76 20 Z M 72 0 L 72 1 C 73.657 1 75 2.343 75 4 L 76 4 L 77 4 C 77 1.239 74.761 -1 72 -1 L 72 0 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
    </div>
  );
  const __body86 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-disabled-rest-4)",
      borderTop: "1px solid var(--neutral-stroke-disabled-rest-4)",
      borderRight: "1px solid var(--neutral-stroke-disabled-rest-4)",
      borderBottom: "1px solid var(--neutral-stroke-disabled-rest-4)",
      borderLeft: "1px solid var(--neutral-stroke-disabled-rest-4)",
      display: "flex",
      flexDirection: "row",
      padding: "1px 1px 1px 1px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "max(0px, calc(var(--spacing-horizontal-xxs) * 1px - 1px))",
      paddingTop: "max(0px, calc(var(--vertical-xxs) * 1px - 1px))",
      paddingRight: "max(0px, calc(var(--spacing-horizontal-xxs) * 1px - 1px))",
      paddingBottom: "max(0px, calc(var(--vertical-xxs) * 1px - 1px))",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
            color: "var(--neutral-foreground-disabled-rest-4)",
          }}>{props.xOutline20 ?? <Placeholder size={"20"} theme={"regular"} />}</div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-disabled-rest-4)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
    </div>
  );
  const __body87 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-disabled-rest-4)",
      borderTop: "1px solid var(--neutral-stroke-disabled-rest-4)",
      borderRight: "1px solid var(--neutral-stroke-disabled-rest-4)",
      borderBottom: "1px solid var(--neutral-stroke-disabled-rest-4)",
      borderLeft: "1px solid var(--neutral-stroke-disabled-rest-4)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xs) * 1px)",
      padding: "1px 7px 1px 7px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "max(0px, calc(var(--spacing-horizontal-s) * 1px - 1px))",
      paddingTop: "max(0px, calc(var(--vertical-xxs) * 1px - 1px))",
      paddingRight: "max(0px, calc(var(--spacing-horizontal-s) * 1px - 1px))",
      paddingBottom: "max(0px, calc(var(--vertical-xxs) * 1px - 1px))",
      position: "relative",
      ...props.style,
    }}>
      {props.icon && (
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
          color: "var(--neutral-foreground-disabled-rest-4)",
        }}>{props.xOutline20 ?? <Placeholder size={"20"} theme={"regular"} />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
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
          padding: "0px 0px 0px 0px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingTop: "0px", paddingBottom: "0px",
          flexShrink: 0,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            whiteSpace: "nowrap",
            lineHeight: "16px",
            color: "var(--neutral-foreground-disabled-rest-4)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.label}</span>
        </div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-disabled-rest-4)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
    </div>
  );
  const __body88 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-1-selected-3)",
      borderTop: "1px solid var(--neutral-stroke-1-selected-3)",
      borderRight: "1px solid var(--neutral-stroke-1-selected-3)",
      borderBottom: "1px solid var(--neutral-stroke-1-selected-3)",
      borderLeft: "1px solid var(--neutral-stroke-1-selected-3)",
      display: "flex",
      flexDirection: "row",
      padding: "1px 1px 1px 1px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "max(0px, calc(var(--spacing-horizontal-xxs) * 1px - 1px))",
      paddingTop: "max(0px, calc(var(--vertical-xxs) * 1px - 1px))",
      paddingRight: "max(0px, calc(var(--spacing-horizontal-xxs) * 1px - 1px))",
      paddingBottom: "max(0px, calc(var(--vertical-xxs) * 1px - 1px))",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
            color: "var(--neutral-foreground-1-selected-3)",
          }}>{props.xFilled20 ?? <Placeholder size={"20"} theme={"filled"} />}</div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-1-selected-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"filled"} />}</div>
        )}
      </div>
    </div>
  );
  const __body89 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-1-selected-3)",
      borderTop: "1px solid var(--neutral-stroke-1-selected-3)",
      borderRight: "1px solid var(--neutral-stroke-1-selected-3)",
      borderBottom: "1px solid var(--neutral-stroke-1-selected-3)",
      borderLeft: "1px solid var(--neutral-stroke-1-selected-3)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xs) * 1px)",
      padding: "1px 7px 1px 7px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "max(0px, calc(var(--spacing-horizontal-s) * 1px - 1px))",
      paddingTop: "max(0px, calc(var(--vertical-xxs) * 1px - 1px))",
      paddingRight: "max(0px, calc(var(--spacing-horizontal-s) * 1px - 1px))",
      paddingBottom: "max(0px, calc(var(--vertical-xxs) * 1px - 1px))",
      position: "relative",
      ...props.style,
    }}>
      {props.icon && (
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
          color: "var(--neutral-foreground-1-selected-3)",
        }}>{props.xFilled20 ?? <Placeholder size={"20"} theme={"filled"} />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
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
          padding: "0px 0px 0px 0px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingTop: "0px", paddingBottom: "0px",
          flexShrink: 0,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            whiteSpace: "nowrap",
            lineHeight: "16px",
            color: "var(--neutral-foreground-1-pressed-3)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.label}</span>
        </div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-1-selected-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"filled"} />}</div>
        )}
      </div>
    </div>
  );
  const __body90 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-1-pressed-3)",
      borderTop: "1px solid var(--neutral-stroke-1-pressed-3)",
      borderRight: "1px solid var(--neutral-stroke-1-pressed-3)",
      borderBottom: "1px solid var(--neutral-stroke-1-pressed-3)",
      borderLeft: "1px solid var(--neutral-stroke-1-pressed-3)",
      display: "flex",
      flexDirection: "row",
      padding: "1px 1px 1px 1px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "max(0px, calc(var(--spacing-horizontal-xxs) * 1px - 1px))",
      paddingTop: "max(0px, calc(var(--vertical-xxs) * 1px - 1px))",
      paddingRight: "max(0px, calc(var(--spacing-horizontal-xxs) * 1px - 1px))",
      paddingBottom: "max(0px, calc(var(--vertical-xxs) * 1px - 1px))",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
            color: "var(--neutral-foreground-1-pressed-3)",
          }}>{props.xOutline20 ?? <Placeholder size={"20"} theme={"regular"} />}</div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-1-pressed-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
    </div>
  );
  const __body91 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-1-pressed-3)",
      borderTop: "1px solid var(--neutral-stroke-1-pressed-3)",
      borderRight: "1px solid var(--neutral-stroke-1-pressed-3)",
      borderBottom: "1px solid var(--neutral-stroke-1-pressed-3)",
      borderLeft: "1px solid var(--neutral-stroke-1-pressed-3)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xs) * 1px)",
      padding: "1px 7px 1px 7px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "max(0px, calc(var(--spacing-horizontal-s) * 1px - 1px))",
      paddingTop: "max(0px, calc(var(--vertical-xxs) * 1px - 1px))",
      paddingRight: "max(0px, calc(var(--spacing-horizontal-s) * 1px - 1px))",
      paddingBottom: "max(0px, calc(var(--vertical-xxs) * 1px - 1px))",
      position: "relative",
      ...props.style,
    }}>
      {props.icon && (
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
          color: "var(--neutral-foreground-1-pressed-3)",
        }}>{props.xOutline20 ?? <Placeholder size={"20"} theme={"regular"} />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
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
          padding: "0px 0px 0px 0px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingTop: "0px", paddingBottom: "0px",
          flexShrink: 0,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            whiteSpace: "nowrap",
            lineHeight: "16px",
            color: "var(--neutral-foreground-1-pressed-3)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.label}</span>
        </div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-1-pressed-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
    </div>
  );
  const __body92 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-1-hover-3)",
      borderTop: "1px solid var(--neutral-stroke-1-hover-3)",
      borderRight: "1px solid var(--neutral-stroke-1-hover-3)",
      borderBottom: "1px solid var(--neutral-stroke-1-hover-3)",
      borderLeft: "1px solid var(--neutral-stroke-1-hover-3)",
      display: "flex",
      flexDirection: "row",
      padding: "1px 1px 1px 1px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "max(0px, calc(var(--spacing-horizontal-xxs) * 1px - 1px))",
      paddingTop: "max(0px, calc(var(--vertical-xxs) * 1px - 1px))",
      paddingRight: "max(0px, calc(var(--spacing-horizontal-xxs) * 1px - 1px))",
      paddingBottom: "max(0px, calc(var(--vertical-xxs) * 1px - 1px))",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
            color: "var(--neutral-foreground-1-hover-3)",
          }}>{props.xOutline20 ?? <Placeholder size={"20"} theme={"regular"} />}</div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-1-hover-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
    </div>
  );
  const __body93 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-1-hover-3)",
      borderTop: "1px solid var(--neutral-stroke-1-hover-3)",
      borderRight: "1px solid var(--neutral-stroke-1-hover-3)",
      borderBottom: "1px solid var(--neutral-stroke-1-hover-3)",
      borderLeft: "1px solid var(--neutral-stroke-1-hover-3)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xs) * 1px)",
      padding: "1px 7px 1px 7px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "max(0px, calc(var(--spacing-horizontal-s) * 1px - 1px))",
      paddingTop: "max(0px, calc(var(--vertical-xxs) * 1px - 1px))",
      paddingRight: "max(0px, calc(var(--spacing-horizontal-s) * 1px - 1px))",
      paddingBottom: "max(0px, calc(var(--vertical-xxs) * 1px - 1px))",
      position: "relative",
      ...props.style,
    }}>
      {props.icon && (
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
          color: "var(--neutral-foreground-1-hover-3)",
        }}>{props.xOutline20 ?? <Placeholder size={"20"} theme={"regular"} />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
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
          padding: "0px 0px 0px 0px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingTop: "0px", paddingBottom: "0px",
          flexShrink: 0,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            whiteSpace: "nowrap",
            lineHeight: "16px",
            color: "var(--neutral-foreground-1-hover-3)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.label}</span>
        </div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-1-hover-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
    </div>
  );
  const __body94 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-1-rest-3)",
      borderTop: "1px solid var(--neutral-stroke-1-rest-3)",
      borderRight: "1px solid var(--neutral-stroke-1-rest-3)",
      borderBottom: "1px solid var(--neutral-stroke-1-rest-3)",
      borderLeft: "1px solid var(--neutral-stroke-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      padding: "1px 1px 1px 1px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "max(0px, calc(var(--spacing-horizontal-xxs) * 1px - 1px))",
      paddingTop: "max(0px, calc(var(--vertical-xxs) * 1px - 1px))",
      paddingRight: "max(0px, calc(var(--spacing-horizontal-xxs) * 1px - 1px))",
      paddingBottom: "max(0px, calc(var(--vertical-xxs) * 1px - 1px))",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
            color: "var(--neutral-foreground-1-rest-3)",
          }}>{props.xOutline20 ?? <Placeholder size={"20"} theme={"regular"} />}</div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-1-rest-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
      {props.focus && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 24,
        height: 24,
        overflow: "hidden",
        borderRadius: 4,
        borderTop: "2px solid var(--neutral-stroke-focus-2-rest-4)",
        borderRight: "2px solid var(--neutral-stroke-focus-2-rest-4)",
        borderBottom: "2px solid var(--neutral-stroke-focus-2-rest-4)",
        borderLeft: "2px solid var(--neutral-stroke-focus-2-rest-4)",
      }}>
        <svg width={24} height={24} viewBox="0 0 24 24" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          overflow: "hidden",
          borderRadius: 4,
          borderTop: "1px solid var(--neutral-stroke-focus-1-rest-4)",
          borderRight: "1px solid var(--neutral-stroke-focus-1-rest-4)",
          borderBottom: "1px solid var(--neutral-stroke-focus-1-rest-4)",
          borderLeft: "1px solid var(--neutral-stroke-focus-1-rest-4)",
        }}>
          <path d={"M 4 0 L 4 1 L 20 1 L 20 0 L 20 -1 L 4 -1 L 4 0 Z M 24 4 L 23 4 L 23 20 L 24 20 L 25 20 L 25 4 L 24 4 Z M 20 24 L 20 23 L 4 23 L 4 24 L 4 25 L 20 25 L 20 24 Z M 0 20 L 1 20 L 1 4 L 0 4 L -1 4 L -1 20 L 0 20 Z M 4 24 L 4 23 C 2.343 23 1 21.657 1 20 L 0 20 L -1 20 C -1 22.761 1.239 25 4 25 L 4 24 Z M 24 20 L 23 20 C 23 21.657 21.657 23 20 23 L 20 24 L 20 25 C 22.761 25 25 22.761 25 20 L 24 20 Z M 20 0 L 20 1 C 21.657 1 23 2.343 23 4 L 24 4 L 25 4 C 25 1.239 22.761 -1 20 -1 L 20 0 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
    </div>
  );
  const __body95 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-1-rest-3)",
      borderTop: "1px solid var(--neutral-stroke-1-rest-3)",
      borderRight: "1px solid var(--neutral-stroke-1-rest-3)",
      borderBottom: "1px solid var(--neutral-stroke-1-rest-3)",
      borderLeft: "1px solid var(--neutral-stroke-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xs) * 1px)",
      padding: "1px 7px 1px 7px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "max(0px, calc(var(--spacing-horizontal-s) * 1px - 1px))",
      paddingTop: "max(0px, calc(var(--vertical-xxs) * 1px - 1px))",
      paddingRight: "max(0px, calc(var(--spacing-horizontal-s) * 1px - 1px))",
      paddingBottom: "max(0px, calc(var(--vertical-xxs) * 1px - 1px))",
      position: "relative",
      ...props.style,
    }}>
      {props.icon && (
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
          color: "var(--neutral-foreground-1-rest-3)",
        }}>{props.xOutline20 ?? <Placeholder size={"20"} theme={"regular"} />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
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
          padding: "0px 0px 0px 0px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingTop: "0px", paddingBottom: "0px",
          flexShrink: 0,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 400,
            fontSize: 12,
            whiteSpace: "nowrap",
            lineHeight: "16px",
            color: "var(--neutral-foreground-1-rest-3)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.label}</span>
        </div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-1-rest-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
      {props.focus && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 76,
        height: 24,
        overflow: "hidden",
        borderRadius: 4,
        borderTop: "2px solid var(--neutral-stroke-focus-2-rest-4)",
        borderRight: "2px solid var(--neutral-stroke-focus-2-rest-4)",
        borderBottom: "2px solid var(--neutral-stroke-focus-2-rest-4)",
        borderLeft: "2px solid var(--neutral-stroke-focus-2-rest-4)",
      }}>
        <svg width={76} height={24} viewBox="0 0 76 24" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 76,
          height: 24,
          overflow: "hidden",
          borderRadius: 4,
          borderTop: "1px solid var(--neutral-stroke-focus-1-rest-4)",
          borderRight: "1px solid var(--neutral-stroke-focus-1-rest-4)",
          borderBottom: "1px solid var(--neutral-stroke-focus-1-rest-4)",
          borderLeft: "1px solid var(--neutral-stroke-focus-1-rest-4)",
        }}>
          <path d={"M 4 0 L 4 1 L 72 1 L 72 0 L 72 -1 L 4 -1 L 4 0 Z M 76 4 L 75 4 L 75 20 L 76 20 L 77 20 L 77 4 L 76 4 Z M 72 24 L 72 23 L 4 23 L 4 24 L 4 25 L 72 25 L 72 24 Z M 0 20 L 1 20 L 1 4 L 0 4 L -1 4 L -1 20 L 0 20 Z M 4 24 L 4 23 C 2.343 23 1 21.657 1 20 L 0 20 L -1 20 C -1 22.761 1.239 25 4 25 L 4 24 Z M 76 20 L 75 20 C 75 21.657 73.657 23 72 23 L 72 24 L 72 25 C 74.761 25 77 22.761 77 20 L 76 20 Z M 72 0 L 72 1 C 73.657 1 75 2.343 75 4 L 76 4 L 77 4 C 77 1.239 74.761 -1 72 -1 L 72 0 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
    </div>
  );
  const __body96 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "6px 6px 6px 6px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-snudge) * 1px)",
      paddingTop: "calc(var(--vertical-snudge) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-snudge) * 1px)",
      paddingBottom: "calc(var(--vertical-snudge) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
            color: "var(--neutral-foreground-disabled-rest-4)",
          }}>{props.xOutline20 ?? <Placeholder size={"20"} theme={"regular"} />}</div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-disabled-rest-4)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
    </div>
  );
  const __body97 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-snudge) * 1px)",
      padding: "6px 12px 6px 12px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-m) * 1px)",
      paddingTop: "calc(var(--vertical-snudge) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-m) * 1px)",
      paddingBottom: "calc(var(--vertical-snudge) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.icon && (
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
          color: "var(--neutral-foreground-disabled-rest-4)",
        }}>{props.xOutline20 ?? <Placeholder size={"20"} theme={"regular"} />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
        padding: "0px 0px 0px 0px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingTop: "0px", paddingBottom: "0px",
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
            color: "var(--neutral-foreground-disabled-rest-4)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.label}</span>
        </div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-disabled-rest-4)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
    </div>
  );
  const __body98 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "6px 6px 6px 6px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-snudge) * 1px)",
      paddingTop: "calc(var(--vertical-snudge) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-snudge) * 1px)",
      paddingBottom: "calc(var(--vertical-snudge) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
            color: "var(--neutral-foreground-2-brand-selected-3)",
          }}>{props.xFilled20 ?? <Placeholder size={"20"} theme={"filled"} />}</div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-brand-selected-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"filled"} />}</div>
        )}
      </div>
    </div>
  );
  const __body99 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-snudge) * 1px)",
      padding: "6px 12px 6px 12px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-m) * 1px)",
      paddingTop: "calc(var(--vertical-snudge) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-m) * 1px)",
      paddingBottom: "calc(var(--vertical-snudge) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.icon && (
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
          color: "var(--neutral-foreground-2-brand-selected-3)",
        }}>{props.xFilled20 ?? <Placeholder size={"20"} theme={"filled"} />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
        padding: "0px 0px 0px 0px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingTop: "0px", paddingBottom: "0px",
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
            color: "var(--neutral-foreground-2-brand-selected-3)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.label}</span>
        </div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-brand-selected-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"filled"} />}</div>
        )}
      </div>
    </div>
  );
  const __body100 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "6px 6px 6px 6px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-snudge) * 1px)",
      paddingTop: "calc(var(--vertical-snudge) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-snudge) * 1px)",
      paddingBottom: "calc(var(--vertical-snudge) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
            color: "var(--neutral-foreground-2-brand-pressed-4)",
          }}>{props.xFilled20 ?? <Placeholder size={"20"} theme={"filled"} />}</div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-brand-pressed-4)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
    </div>
  );
  const __body101 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-snudge) * 1px)",
      padding: "6px 12px 6px 12px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-m) * 1px)",
      paddingTop: "calc(var(--vertical-snudge) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-m) * 1px)",
      paddingBottom: "calc(var(--vertical-snudge) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.icon && (
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
          color: "var(--neutral-foreground-2-brand-pressed-4)",
        }}>{props.xFilled20 ?? <Placeholder size={"20"} theme={"filled"} />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
        padding: "0px 0px 0px 0px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingTop: "0px", paddingBottom: "0px",
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
            color: "var(--neutral-foreground-2-brand-pressed-4)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.label}</span>
        </div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-brand-pressed-4)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
    </div>
  );
  const __body102 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "6px 6px 6px 6px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-snudge) * 1px)",
      paddingTop: "calc(var(--vertical-snudge) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-snudge) * 1px)",
      paddingBottom: "calc(var(--vertical-snudge) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
            color: "var(--neutral-foreground-2-brand-hover-4)",
          }}>{props.xFilled20 ?? <Placeholder size={"20"} theme={"filled"} />}</div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-brand-hover-4)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
    </div>
  );
  const __body103 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-snudge) * 1px)",
      padding: "6px 12px 6px 12px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-m) * 1px)",
      paddingTop: "calc(var(--vertical-snudge) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-m) * 1px)",
      paddingBottom: "calc(var(--vertical-snudge) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.icon && (
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
          color: "var(--neutral-foreground-2-brand-hover-4)",
        }}>{props.xFilled20 ?? <Placeholder size={"20"} theme={"filled"} />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
        padding: "0px 0px 0px 0px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingTop: "0px", paddingBottom: "0px",
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
            color: "var(--neutral-foreground-2-brand-hover-4)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.label}</span>
        </div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-brand-hover-4)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
    </div>
  );
  const __body104 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      padding: "6px 6px 6px 6px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-snudge) * 1px)",
      paddingTop: "calc(var(--vertical-snudge) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-snudge) * 1px)",
      paddingBottom: "calc(var(--vertical-snudge) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
            color: "var(--neutral-foreground-2-rest-3)",
          }}>{props.xOutline20 ?? <Placeholder size={"20"} theme={"regular"} />}</div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
      {props.focus && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 32,
        height: 32,
        overflow: "hidden",
        borderRadius: 4,
        borderTop: "2px solid var(--neutral-stroke-focus-2-rest-4)",
        borderRight: "2px solid var(--neutral-stroke-focus-2-rest-4)",
        borderBottom: "2px solid var(--neutral-stroke-focus-2-rest-4)",
        borderLeft: "2px solid var(--neutral-stroke-focus-2-rest-4)",
      }}>
        <svg width={32} height={32} viewBox="0 0 32 32" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          overflow: "hidden",
          borderRadius: 4,
          borderTop: "1px solid var(--neutral-stroke-focus-1-rest-4)",
          borderRight: "1px solid var(--neutral-stroke-focus-1-rest-4)",
          borderBottom: "1px solid var(--neutral-stroke-focus-1-rest-4)",
          borderLeft: "1px solid var(--neutral-stroke-focus-1-rest-4)",
        }}>
          <path d={"M 4 0 L 4 1 L 28 1 L 28 0 L 28 -1 L 4 -1 L 4 0 Z M 32 4 L 31 4 L 31 28 L 32 28 L 33 28 L 33 4 L 32 4 Z M 28 32 L 28 31 L 4 31 L 4 32 L 4 33 L 28 33 L 28 32 Z M 0 28 L 1 28 L 1 4 L 0 4 L -1 4 L -1 28 L 0 28 Z M 4 32 L 4 31 C 2.343 31 1 29.657 1 28 L 0 28 L -1 28 C -1 30.761 1.239 33 4 33 L 4 32 Z M 32 28 L 31 28 C 31 29.657 29.657 31 28 31 L 28 32 L 28 33 C 30.761 33 33 30.761 33 28 L 32 28 Z M 28 0 L 28 1 C 29.657 1 31 2.343 31 4 L 32 4 L 33 4 C 33 1.239 30.761 -1 28 -1 L 28 0 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
    </div>
  );
  const __body105 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-snudge) * 1px)",
      padding: "6px 12px 6px 12px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-m) * 1px)",
      paddingTop: "calc(var(--vertical-snudge) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-m) * 1px)",
      paddingBottom: "calc(var(--vertical-snudge) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.icon && (
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
          color: "var(--neutral-foreground-2-rest-3)",
        }}>{props.xOutline20 ?? <Placeholder size={"20"} theme={"regular"} />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
        padding: "0px 0px 0px 0px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingTop: "0px", paddingBottom: "0px",
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
            color: "var(--neutral-foreground-2-rest-3)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.label}</span>
        </div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
      {props.focus && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 94,
        height: 32,
        overflow: "hidden",
        borderRadius: 4,
        borderTop: "2px solid var(--neutral-stroke-focus-2-rest-4)",
        borderRight: "2px solid var(--neutral-stroke-focus-2-rest-4)",
        borderBottom: "2px solid var(--neutral-stroke-focus-2-rest-4)",
        borderLeft: "2px solid var(--neutral-stroke-focus-2-rest-4)",
      }}>
        <svg width={94} height={32} viewBox="0 0 94 32" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 94,
          height: 32,
          overflow: "hidden",
          borderRadius: 4,
          borderTop: "1px solid var(--neutral-stroke-focus-1-rest-4)",
          borderRight: "1px solid var(--neutral-stroke-focus-1-rest-4)",
          borderBottom: "1px solid var(--neutral-stroke-focus-1-rest-4)",
          borderLeft: "1px solid var(--neutral-stroke-focus-1-rest-4)",
        }}>
          <path d={"M 4 0 L 4 1 L 90 1 L 90 0 L 90 -1 L 4 -1 L 4 0 Z M 94 4 L 93 4 L 93 28 L 94 28 L 95 28 L 95 4 L 94 4 Z M 90 32 L 90 31 L 4 31 L 4 32 L 4 33 L 90 33 L 90 32 Z M 0 28 L 1 28 L 1 4 L 0 4 L -1 4 L -1 28 L 0 28 Z M 4 32 L 4 31 C 2.343 31 1 29.657 1 28 L 0 28 L -1 28 C -1 30.761 1.239 33 4 33 L 4 32 Z M 94 28 L 93 28 C 93 29.657 91.657 31 90 31 L 90 32 L 90 33 C 92.761 33 95 30.761 95 28 L 94 28 Z M 90 0 L 90 1 C 91.657 1 93 2.343 93 4 L 94 4 L 95 4 C 95 1.239 92.761 -1 90 -1 L 90 0 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
    </div>
  );
  const __body106 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      display: "flex",
      flexDirection: "row",
      padding: "6px 6px 6px 6px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-snudge) * 1px)",
      paddingTop: "calc(var(--vertical-snudge) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-snudge) * 1px)",
      paddingBottom: "calc(var(--vertical-snudge) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
            color: "var(--neutral-foreground-disabled-rest-4)",
          }}>{props.xOutline20 ?? <Placeholder size={"20"} theme={"regular"} />}</div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-disabled-rest-4)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
    </div>
  );
  const __body107 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-snudge) * 1px)",
      padding: "6px 12px 6px 12px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-m) * 1px)",
      paddingTop: "calc(var(--vertical-snudge) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-m) * 1px)",
      paddingBottom: "calc(var(--vertical-snudge) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.icon && (
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
          color: "var(--neutral-foreground-disabled-rest-4)",
        }}>{props.xOutline20 ?? <Placeholder size={"20"} theme={"regular"} />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
        padding: "0px 0px 0px 0px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingTop: "0px", paddingBottom: "0px",
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
            color: "var(--neutral-foreground-disabled-rest-4)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.label}</span>
        </div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-disabled-rest-4)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
    </div>
  );
  const __body108 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-subtle-selected-3)",
      display: "flex",
      flexDirection: "row",
      padding: "6px 6px 6px 6px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-snudge) * 1px)",
      paddingTop: "calc(var(--vertical-snudge) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-snudge) * 1px)",
      paddingBottom: "calc(var(--vertical-snudge) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
            color: "var(--neutral-foreground-2-brand-selected-3)",
          }}>{props.xFilled20 ?? <Placeholder size={"20"} theme={"filled"} />}</div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-selected-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"filled"} />}</div>
        )}
      </div>
    </div>
  );
  const __body109 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-subtle-selected-3)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-snudge) * 1px)",
      padding: "6px 12px 6px 12px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-m) * 1px)",
      paddingTop: "calc(var(--vertical-snudge) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-m) * 1px)",
      paddingBottom: "calc(var(--vertical-snudge) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.icon && (
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
          color: "var(--neutral-foreground-2-brand-selected-3)",
        }}>{props.xFilled20 ?? <Placeholder size={"20"} theme={"filled"} />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
        padding: "0px 0px 0px 0px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingTop: "0px", paddingBottom: "0px",
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
            color: "var(--neutral-foreground-1-pressed-3)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.label}</span>
        </div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-selected-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"filled"} />}</div>
        )}
      </div>
    </div>
  );
  const __body110 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-subtle-pressed-3)",
      display: "flex",
      flexDirection: "row",
      padding: "6px 6px 6px 6px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-snudge) * 1px)",
      paddingTop: "calc(var(--vertical-snudge) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-snudge) * 1px)",
      paddingBottom: "calc(var(--vertical-snudge) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
            color: "var(--neutral-foreground-2-brand-pressed-4)",
          }}>{props.xFilled20 ?? <Placeholder size={"20"} theme={"filled"} />}</div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-pressed-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
    </div>
  );
  const __body111 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-subtle-pressed-3)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-snudge) * 1px)",
      padding: "6px 12px 6px 12px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-m) * 1px)",
      paddingTop: "calc(var(--vertical-snudge) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-m) * 1px)",
      paddingBottom: "calc(var(--vertical-snudge) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.icon && (
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
          color: "var(--neutral-foreground-2-brand-pressed-4)",
        }}>{props.xFilled20 ?? <Placeholder size={"20"} theme={"filled"} />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
        padding: "0px 0px 0px 0px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingTop: "0px", paddingBottom: "0px",
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
            color: "var(--neutral-foreground-1-pressed-3)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.label}</span>
        </div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-pressed-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
    </div>
  );
  const __body112 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-subtle-hover-3)",
      display: "flex",
      flexDirection: "row",
      padding: "6px 6px 6px 6px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-snudge) * 1px)",
      paddingTop: "calc(var(--vertical-snudge) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-snudge) * 1px)",
      paddingBottom: "calc(var(--vertical-snudge) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
            color: "var(--neutral-foreground-2-brand-hover-4)",
          }}>{props.xFilled20 ?? <Placeholder size={"20"} theme={"filled"} />}</div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-hover-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
    </div>
  );
  const __body113 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-subtle-hover-3)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-snudge) * 1px)",
      padding: "6px 12px 6px 12px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-m) * 1px)",
      paddingTop: "calc(var(--vertical-snudge) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-m) * 1px)",
      paddingBottom: "calc(var(--vertical-snudge) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.icon && (
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
          color: "var(--neutral-foreground-2-brand-hover-4)",
        }}>{props.xFilled20 ?? <Placeholder size={"20"} theme={"filled"} />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
        padding: "0px 0px 0px 0px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingTop: "0px", paddingBottom: "0px",
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
            color: "var(--neutral-foreground-1-hover-3)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.label}</span>
        </div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-hover-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
    </div>
  );
  const __body114 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      borderTop: "1px solid var(--neutral-stroke-disabled-rest-4)",
      borderRight: "1px solid var(--neutral-stroke-disabled-rest-4)",
      borderBottom: "1px solid var(--neutral-stroke-disabled-rest-4)",
      borderLeft: "1px solid var(--neutral-stroke-disabled-rest-4)",
      display: "flex",
      flexDirection: "row",
      padding: "5px 5px 5px 5px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "max(0px, calc(var(--spacing-horizontal-snudge) * 1px - 1px))",
      paddingTop: "max(0px, calc(var(--vertical-snudge) * 1px - 1px))",
      paddingRight: "max(0px, calc(var(--spacing-horizontal-snudge) * 1px - 1px))",
      paddingBottom: "max(0px, calc(var(--vertical-snudge) * 1px - 1px))",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
            color: "var(--neutral-foreground-disabled-rest-4)",
          }}>{props.xOutline20 ?? <Placeholder size={"20"} theme={"regular"} />}</div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-disabled-rest-4)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
    </div>
  );
  const __body115 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      borderTop: "1px solid var(--neutral-stroke-disabled-rest-4)",
      borderRight: "1px solid var(--neutral-stroke-disabled-rest-4)",
      borderBottom: "1px solid var(--neutral-stroke-disabled-rest-4)",
      borderLeft: "1px solid var(--neutral-stroke-disabled-rest-4)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-snudge) * 1px)",
      padding: "5px 11px 5px 11px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "max(0px, calc(var(--spacing-horizontal-m) * 1px - 1px))",
      paddingTop: "max(0px, calc(var(--vertical-snudge) * 1px - 1px))",
      paddingRight: "max(0px, calc(var(--spacing-horizontal-m) * 1px - 1px))",
      paddingBottom: "max(0px, calc(var(--vertical-snudge) * 1px - 1px))",
      position: "relative",
      ...props.style,
    }}>
      {props.icon && (
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
          color: "var(--neutral-foreground-disabled-rest-4)",
        }}>{props.xOutline20 ?? <Placeholder size={"20"} theme={"regular"} />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
        padding: "0px 0px 0px 0px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingTop: "0px", paddingBottom: "0px",
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
            color: "var(--neutral-foreground-disabled-rest-4)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.label}</span>
        </div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-disabled-rest-4)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
    </div>
  );
  const __body116 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      borderTop: "2px solid var(--neutral-stroke-1-selected-3)",
      borderRight: "2px solid var(--neutral-stroke-1-selected-3)",
      borderBottom: "2px solid var(--neutral-stroke-1-selected-3)",
      borderLeft: "2px solid var(--neutral-stroke-1-selected-3)",
      display: "flex",
      flexDirection: "row",
      padding: "6px 6px 6px 6px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-snudge) * 1px)",
      paddingTop: "calc(var(--vertical-snudge) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-snudge) * 1px)",
      paddingBottom: "calc(var(--vertical-snudge) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
            color: "var(--neutral-foreground-1-selected-3)",
          }}>{props.xFilled20 ?? <Placeholder size={"20"} theme={"filled"} />}</div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-1-selected-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"filled"} />}</div>
        )}
      </div>
    </div>
  );
  const __body117 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      borderTop: "2px solid var(--neutral-stroke-1-selected-3)",
      borderRight: "2px solid var(--neutral-stroke-1-selected-3)",
      borderBottom: "2px solid var(--neutral-stroke-1-selected-3)",
      borderLeft: "2px solid var(--neutral-stroke-1-selected-3)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-snudge) * 1px)",
      padding: "6px 12px 6px 12px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-m) * 1px)",
      paddingTop: "calc(var(--vertical-snudge) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-m) * 1px)",
      paddingBottom: "calc(var(--vertical-snudge) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.icon && (
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
          color: "var(--neutral-foreground-1-selected-3)",
        }}>{props.xFilled20 ?? <Placeholder size={"20"} theme={"filled"} />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
        padding: "0px 0px 0px 0px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingTop: "0px", paddingBottom: "0px",
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
            color: "var(--neutral-foreground-1-pressed-3)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.label}</span>
        </div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-1-selected-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"filled"} />}</div>
        )}
      </div>
    </div>
  );
  const __body118 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      borderTop: "1px solid var(--neutral-stroke-1-pressed-3)",
      borderRight: "1px solid var(--neutral-stroke-1-pressed-3)",
      borderBottom: "1px solid var(--neutral-stroke-1-pressed-3)",
      borderLeft: "1px solid var(--neutral-stroke-1-pressed-3)",
      display: "flex",
      flexDirection: "row",
      padding: "5px 5px 5px 5px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "max(0px, calc(var(--spacing-horizontal-snudge) * 1px - 1px))",
      paddingTop: "max(0px, calc(var(--vertical-snudge) * 1px - 1px))",
      paddingRight: "max(0px, calc(var(--spacing-horizontal-snudge) * 1px - 1px))",
      paddingBottom: "max(0px, calc(var(--vertical-snudge) * 1px - 1px))",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
            color: "var(--neutral-foreground-1-pressed-3)",
          }}>{props.xOutline20 ?? <Placeholder size={"20"} theme={"regular"} />}</div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-1-pressed-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
    </div>
  );
  const __body119 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      borderTop: "1px solid var(--neutral-stroke-1-pressed-3)",
      borderRight: "1px solid var(--neutral-stroke-1-pressed-3)",
      borderBottom: "1px solid var(--neutral-stroke-1-pressed-3)",
      borderLeft: "1px solid var(--neutral-stroke-1-pressed-3)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-snudge) * 1px)",
      padding: "5px 11px 5px 11px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "max(0px, calc(var(--spacing-horizontal-m) * 1px - 1px))",
      paddingTop: "max(0px, calc(var(--vertical-snudge) * 1px - 1px))",
      paddingRight: "max(0px, calc(var(--spacing-horizontal-m) * 1px - 1px))",
      paddingBottom: "max(0px, calc(var(--vertical-snudge) * 1px - 1px))",
      position: "relative",
      ...props.style,
    }}>
      {props.icon && (
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
          color: "var(--neutral-foreground-1-pressed-3)",
        }}>{props.xOutline20 ?? <Placeholder size={"20"} theme={"regular"} />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
        padding: "0px 0px 0px 0px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingTop: "0px", paddingBottom: "0px",
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
            color: "var(--neutral-foreground-1-pressed-3)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.label}</span>
        </div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-1-pressed-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
    </div>
  );
  const __body120 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      borderTop: "1px solid var(--neutral-stroke-1-hover-3)",
      borderRight: "1px solid var(--neutral-stroke-1-hover-3)",
      borderBottom: "1px solid var(--neutral-stroke-1-hover-3)",
      borderLeft: "1px solid var(--neutral-stroke-1-hover-3)",
      display: "flex",
      flexDirection: "row",
      padding: "5px 5px 5px 5px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "max(0px, calc(var(--spacing-horizontal-snudge) * 1px - 1px))",
      paddingTop: "max(0px, calc(var(--vertical-snudge) * 1px - 1px))",
      paddingRight: "max(0px, calc(var(--spacing-horizontal-snudge) * 1px - 1px))",
      paddingBottom: "max(0px, calc(var(--vertical-snudge) * 1px - 1px))",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
            color: "var(--neutral-foreground-1-hover-3)",
          }}>{props.xOutline20 ?? <Placeholder size={"20"} theme={"regular"} />}</div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-1-hover-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
    </div>
  );
  const __body121 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      borderTop: "1px solid var(--neutral-stroke-1-hover-3)",
      borderRight: "1px solid var(--neutral-stroke-1-hover-3)",
      borderBottom: "1px solid var(--neutral-stroke-1-hover-3)",
      borderLeft: "1px solid var(--neutral-stroke-1-hover-3)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-snudge) * 1px)",
      padding: "5px 11px 5px 11px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "max(0px, calc(var(--spacing-horizontal-m) * 1px - 1px))",
      paddingTop: "max(0px, calc(var(--vertical-snudge) * 1px - 1px))",
      paddingRight: "max(0px, calc(var(--spacing-horizontal-m) * 1px - 1px))",
      paddingBottom: "max(0px, calc(var(--vertical-snudge) * 1px - 1px))",
      position: "relative",
      ...props.style,
    }}>
      {props.icon && (
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
          color: "var(--neutral-foreground-1-hover-3)",
        }}>{props.xOutline20 ?? <Placeholder size={"20"} theme={"regular"} />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
        padding: "0px 0px 0px 0px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingTop: "0px", paddingBottom: "0px",
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
            color: "var(--neutral-foreground-1-hover-3)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.label}</span>
        </div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-1-hover-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
    </div>
  );
  const __body122 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      borderTop: "1px solid var(--neutral-stroke-1-rest-3)",
      borderRight: "1px solid var(--neutral-stroke-1-rest-3)",
      borderBottom: "1px solid var(--neutral-stroke-1-rest-3)",
      borderLeft: "1px solid var(--neutral-stroke-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      padding: "5px 5px 5px 5px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "max(0px, calc(var(--spacing-horizontal-snudge) * 1px - 1px))",
      paddingTop: "max(0px, calc(var(--vertical-snudge) * 1px - 1px))",
      paddingRight: "max(0px, calc(var(--spacing-horizontal-snudge) * 1px - 1px))",
      paddingBottom: "max(0px, calc(var(--vertical-snudge) * 1px - 1px))",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
            color: "var(--neutral-foreground-1-rest-3)",
          }}>{props.xOutline20 ?? <Placeholder size={"20"} theme={"regular"} />}</div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-1-rest-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
      {props.focus && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 32,
        height: 32,
        overflow: "hidden",
        borderRadius: 4,
        borderTop: "2px solid var(--neutral-stroke-focus-2-rest-4)",
        borderRight: "2px solid var(--neutral-stroke-focus-2-rest-4)",
        borderBottom: "2px solid var(--neutral-stroke-focus-2-rest-4)",
        borderLeft: "2px solid var(--neutral-stroke-focus-2-rest-4)",
      }}>
        <svg width={32} height={32} viewBox="0 0 32 32" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          overflow: "hidden",
          borderRadius: 4,
          borderTop: "1px solid var(--neutral-stroke-focus-1-rest-4)",
          borderRight: "1px solid var(--neutral-stroke-focus-1-rest-4)",
          borderBottom: "1px solid var(--neutral-stroke-focus-1-rest-4)",
          borderLeft: "1px solid var(--neutral-stroke-focus-1-rest-4)",
        }}>
          <path d={"M 4 0 L 4 1 L 28 1 L 28 0 L 28 -1 L 4 -1 L 4 0 Z M 32 4 L 31 4 L 31 28 L 32 28 L 33 28 L 33 4 L 32 4 Z M 28 32 L 28 31 L 4 31 L 4 32 L 4 33 L 28 33 L 28 32 Z M 0 28 L 1 28 L 1 4 L 0 4 L -1 4 L -1 28 L 0 28 Z M 4 32 L 4 31 C 2.343 31 1 29.657 1 28 L 0 28 L -1 28 C -1 30.761 1.239 33 4 33 L 4 32 Z M 32 28 L 31 28 C 31 29.657 29.657 31 28 31 L 28 32 L 28 33 C 30.761 33 33 30.761 33 28 L 32 28 Z M 28 0 L 28 1 C 29.657 1 31 2.343 31 4 L 32 4 L 33 4 C 33 1.239 30.761 -1 28 -1 L 28 0 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
    </div>
  );
  const __body123 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "rgba(255,255,255,0)",
      borderTop: "1px solid var(--neutral-stroke-1-rest-3)",
      borderRight: "1px solid var(--neutral-stroke-1-rest-3)",
      borderBottom: "1px solid var(--neutral-stroke-1-rest-3)",
      borderLeft: "1px solid var(--neutral-stroke-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-snudge) * 1px)",
      padding: "5px 11px 5px 11px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "max(0px, calc(var(--spacing-horizontal-m) * 1px - 1px))",
      paddingTop: "max(0px, calc(var(--vertical-snudge) * 1px - 1px))",
      paddingRight: "max(0px, calc(var(--spacing-horizontal-m) * 1px - 1px))",
      paddingBottom: "max(0px, calc(var(--vertical-snudge) * 1px - 1px))",
      position: "relative",
      ...props.style,
    }}>
      {props.icon && (
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
          color: "var(--neutral-foreground-1-rest-3)",
        }}>{props.xOutline20 ?? <Placeholder size={"20"} theme={"regular"} />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
        padding: "0px 0px 0px 0px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingTop: "0px", paddingBottom: "0px",
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
            color: "var(--neutral-foreground-1-rest-3)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.label}</span>
        </div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-1-rest-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
      {props.focus && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 94,
        height: 32,
        overflow: "hidden",
        borderRadius: 4,
        borderTop: "2px solid var(--neutral-stroke-focus-2-rest-4)",
        borderRight: "2px solid var(--neutral-stroke-focus-2-rest-4)",
        borderBottom: "2px solid var(--neutral-stroke-focus-2-rest-4)",
        borderLeft: "2px solid var(--neutral-stroke-focus-2-rest-4)",
      }}>
        <svg width={94} height={32} viewBox="0 0 94 32" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 94,
          height: 32,
          overflow: "hidden",
          borderRadius: 4,
          borderTop: "1px solid var(--neutral-stroke-focus-1-rest-4)",
          borderRight: "1px solid var(--neutral-stroke-focus-1-rest-4)",
          borderBottom: "1px solid var(--neutral-stroke-focus-1-rest-4)",
          borderLeft: "1px solid var(--neutral-stroke-focus-1-rest-4)",
        }}>
          <path d={"M 4 0 L 4 1 L 90 1 L 90 0 L 90 -1 L 4 -1 L 4 0 Z M 94 4 L 93 4 L 93 28 L 94 28 L 95 28 L 95 4 L 94 4 Z M 90 32 L 90 31 L 4 31 L 4 32 L 4 33 L 90 33 L 90 32 Z M 0 28 L 1 28 L 1 4 L 0 4 L -1 4 L -1 28 L 0 28 Z M 4 32 L 4 31 C 2.343 31 1 29.657 1 28 L 0 28 L -1 28 C -1 30.761 1.239 33 4 33 L 4 32 Z M 94 28 L 93 28 C 93 29.657 91.657 31 90 31 L 90 32 L 90 33 C 92.761 33 95 30.761 95 28 L 94 28 Z M 90 0 L 90 1 C 91.657 1 93 2.343 93 4 L 94 4 L 95 4 C 95 1.239 92.761 -1 90 -1 L 90 0 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
    </div>
  );
  const __body124 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-disabled-rest-4)",
      display: "flex",
      flexDirection: "row",
      padding: "6px 6px 6px 6px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-snudge) * 1px)",
      paddingTop: "calc(var(--vertical-snudge) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-snudge) * 1px)",
      paddingBottom: "calc(var(--vertical-snudge) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
            color: "var(--neutral-foreground-disabled-rest-4)",
          }}>{props.xOutline20 ?? <Placeholder size={"20"} theme={"regular"} />}</div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-disabled-rest-4)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
    </div>
  );
  const __body125 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-disabled-rest-4)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-snudge) * 1px)",
      padding: "6px 12px 6px 12px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-m) * 1px)",
      paddingTop: "calc(var(--vertical-snudge) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-m) * 1px)",
      paddingBottom: "calc(var(--vertical-snudge) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.icon && (
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
          color: "var(--neutral-foreground-disabled-rest-4)",
        }}>{props.xOutline20 ?? <Placeholder size={"20"} theme={"regular"} />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
        padding: "0px 0px 0px 0px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingTop: "0px", paddingBottom: "0px",
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
            color: "var(--neutral-foreground-disabled-rest-4)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.label}</span>
        </div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-disabled-rest-4)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
    </div>
  );
  const __body126 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--brand-background-1-selected-3)",
      display: "flex",
      flexDirection: "row",
      padding: "6px 6px 6px 6px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-snudge) * 1px)",
      paddingTop: "calc(var(--vertical-snudge) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-snudge) * 1px)",
      paddingBottom: "calc(var(--vertical-snudge) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
            color: "var(--neutral-foreground-on-brand-rest-3)",
          }}>{props.xFilled20 ?? <Placeholder size={"20"} theme={"filled"} />}</div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-on-brand-rest-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"filled"} />}</div>
        )}
      </div>
    </div>
  );
  const __body127 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--brand-background-1-selected-3)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-snudge) * 1px)",
      padding: "6px 12px 6px 12px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-m) * 1px)",
      paddingTop: "calc(var(--vertical-snudge) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-m) * 1px)",
      paddingBottom: "calc(var(--vertical-snudge) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.icon && (
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
          color: "var(--neutral-foreground-on-brand-rest-3)",
        }}>{props.xFilled20 ?? <Placeholder size={"20"} theme={"filled"} />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
        padding: "0px 0px 0px 0px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingTop: "0px", paddingBottom: "0px",
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
            color: "var(--neutral-foreground-on-brand-rest-3)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.label}</span>
        </div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-on-brand-rest-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"filled"} />}</div>
        )}
      </div>
    </div>
  );
  const __body128 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--brand-background-1-pressed-3)",
      display: "flex",
      flexDirection: "row",
      padding: "6px 6px 6px 6px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-snudge) * 1px)",
      paddingTop: "calc(var(--vertical-snudge) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-snudge) * 1px)",
      paddingBottom: "calc(var(--vertical-snudge) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
            color: "var(--neutral-foreground-on-brand-rest-3)",
          }}>{props.xOutline20 ?? <Placeholder size={"20"} theme={"regular"} />}</div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-on-brand-rest-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
    </div>
  );
  const __body129 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--brand-background-1-pressed-3)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-snudge) * 1px)",
      padding: "6px 12px 6px 12px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-m) * 1px)",
      paddingTop: "calc(var(--vertical-snudge) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-m) * 1px)",
      paddingBottom: "calc(var(--vertical-snudge) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.icon && (
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
          color: "var(--neutral-foreground-on-brand-rest-3)",
        }}>{props.xOutline20 ?? <Placeholder size={"20"} theme={"regular"} />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
        padding: "0px 0px 0px 0px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingTop: "0px", paddingBottom: "0px",
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
            color: "var(--neutral-foreground-on-brand-rest-3)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.label}</span>
        </div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-on-brand-rest-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
    </div>
  );
  const __body130 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--brand-background-1-hover-3)",
      display: "flex",
      flexDirection: "row",
      padding: "6px 6px 6px 6px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-snudge) * 1px)",
      paddingTop: "calc(var(--vertical-snudge) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-snudge) * 1px)",
      paddingBottom: "calc(var(--vertical-snudge) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
            color: "var(--neutral-foreground-on-brand-rest-3)",
          }}>{props.xOutline20 ?? <Placeholder size={"20"} theme={"regular"} />}</div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-on-brand-rest-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
    </div>
  );
  const __body131 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--brand-background-1-hover-3)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-snudge) * 1px)",
      padding: "6px 12px 6px 12px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-m) * 1px)",
      paddingTop: "calc(var(--vertical-snudge) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-m) * 1px)",
      paddingBottom: "calc(var(--vertical-snudge) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.icon && (
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
          color: "var(--neutral-foreground-on-brand-rest-3)",
        }}>{props.xOutline20 ?? <Placeholder size={"20"} theme={"regular"} />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
        padding: "0px 0px 0px 0px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingTop: "0px", paddingBottom: "0px",
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
            color: "var(--neutral-foreground-on-brand-rest-3)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.label}</span>
        </div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-on-brand-rest-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
    </div>
  );
  const __body132 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--brand-background-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      padding: "6px 6px 6px 6px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-snudge) * 1px)",
      paddingTop: "calc(var(--vertical-snudge) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-snudge) * 1px)",
      paddingBottom: "calc(var(--vertical-snudge) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
            color: "var(--neutral-foreground-on-brand-rest-3)",
          }}>{props.xOutline20 ?? <Placeholder size={"20"} theme={"regular"} />}</div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-on-brand-rest-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
      {props.focus && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 32,
        height: 32,
        overflow: "hidden",
        borderRadius: 4,
        borderTop: "2px solid var(--neutral-stroke-focus-2-rest-4)",
        borderRight: "2px solid var(--neutral-stroke-focus-2-rest-4)",
        borderBottom: "2px solid var(--neutral-stroke-focus-2-rest-4)",
        borderLeft: "2px solid var(--neutral-stroke-focus-2-rest-4)",
      }}>
        <svg width={32} height={32} viewBox="0 0 32 32" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          overflow: "hidden",
          borderRadius: 4,
          borderTop: "1px solid var(--neutral-stroke-focus-1-rest-4)",
          borderRight: "1px solid var(--neutral-stroke-focus-1-rest-4)",
          borderBottom: "1px solid var(--neutral-stroke-focus-1-rest-4)",
          borderLeft: "1px solid var(--neutral-stroke-focus-1-rest-4)",
        }}>
          <path d={"M 4 0 L 4 1 L 28 1 L 28 0 L 28 -1 L 4 -1 L 4 0 Z M 32 4 L 31 4 L 31 28 L 32 28 L 33 28 L 33 4 L 32 4 Z M 28 32 L 28 31 L 4 31 L 4 32 L 4 33 L 28 33 L 28 32 Z M 0 28 L 1 28 L 1 4 L 0 4 L -1 4 L -1 28 L 0 28 Z M 4 32 L 4 31 C 2.343 31 1 29.657 1 28 L 0 28 L -1 28 C -1 30.761 1.239 33 4 33 L 4 32 Z M 32 28 L 31 28 C 31 29.657 29.657 31 28 31 L 28 32 L 28 33 C 30.761 33 33 30.761 33 28 L 32 28 Z M 28 0 L 28 1 C 29.657 1 31 2.343 31 4 L 32 4 L 33 4 C 33 1.239 30.761 -1 28 -1 L 28 0 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
    </div>
  );
  const __body133 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--brand-background-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-snudge) * 1px)",
      padding: "6px 12px 6px 12px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-m) * 1px)",
      paddingTop: "calc(var(--vertical-snudge) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-m) * 1px)",
      paddingBottom: "calc(var(--vertical-snudge) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.icon && (
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
          color: "var(--neutral-foreground-on-brand-rest-3)",
        }}>{props.xOutline20 ?? <Placeholder size={"20"} theme={"regular"} />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
        padding: "0px 0px 0px 0px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingTop: "0px", paddingBottom: "0px",
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
            color: "var(--neutral-foreground-on-brand-rest-3)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.label}</span>
        </div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-on-brand-rest-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
      {props.focus && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 94,
        height: 32,
        overflow: "hidden",
        borderRadius: 4,
        borderTop: "2px solid var(--neutral-stroke-focus-2-rest-4)",
        borderRight: "2px solid var(--neutral-stroke-focus-2-rest-4)",
        borderBottom: "2px solid var(--neutral-stroke-focus-2-rest-4)",
        borderLeft: "2px solid var(--neutral-stroke-focus-2-rest-4)",
      }}>
        <svg width={94} height={32} viewBox="0 0 94 32" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 94,
          height: 32,
          overflow: "hidden",
          borderRadius: 4,
          borderTop: "1px solid var(--neutral-stroke-focus-1-rest-4)",
          borderRight: "1px solid var(--neutral-stroke-focus-1-rest-4)",
          borderBottom: "1px solid var(--neutral-stroke-focus-1-rest-4)",
          borderLeft: "1px solid var(--neutral-stroke-focus-1-rest-4)",
        }}>
          <path d={"M 4 0 L 4 1 L 90 1 L 90 0 L 90 -1 L 4 -1 L 4 0 Z M 94 4 L 93 4 L 93 28 L 94 28 L 95 28 L 95 4 L 94 4 Z M 90 32 L 90 31 L 4 31 L 4 32 L 4 33 L 90 33 L 90 32 Z M 0 28 L 1 28 L 1 4 L 0 4 L -1 4 L -1 28 L 0 28 Z M 4 32 L 4 31 C 2.343 31 1 29.657 1 28 L 0 28 L -1 28 C -1 30.761 1.239 33 4 33 L 4 32 Z M 94 28 L 93 28 C 93 29.657 91.657 31 90 31 L 90 32 L 90 33 C 92.761 33 95 30.761 95 28 L 94 28 Z M 90 0 L 90 1 C 91.657 1 93 2.343 93 4 L 94 4 L 95 4 C 95 1.239 92.761 -1 90 -1 L 90 0 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
    </div>
  );
  const __body134 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-disabled-rest-4)",
      borderTop: "1px solid var(--neutral-stroke-disabled-rest-4)",
      borderRight: "1px solid var(--neutral-stroke-disabled-rest-4)",
      borderBottom: "1px solid var(--neutral-stroke-disabled-rest-4)",
      borderLeft: "1px solid var(--neutral-stroke-disabled-rest-4)",
      display: "flex",
      flexDirection: "row",
      padding: "5px 5px 5px 5px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "max(0px, calc(var(--spacing-horizontal-snudge) * 1px - 1px))",
      paddingTop: "max(0px, calc(var(--vertical-snudge) * 1px - 1px))",
      paddingRight: "max(0px, calc(var(--spacing-horizontal-snudge) * 1px - 1px))",
      paddingBottom: "max(0px, calc(var(--vertical-snudge) * 1px - 1px))",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
            color: "var(--neutral-foreground-disabled-rest-4)",
          }}>{props.xOutline20 ?? <Placeholder size={"20"} theme={"regular"} />}</div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-disabled-rest-4)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
    </div>
  );
  const __body135 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-disabled-rest-4)",
      borderTop: "1px solid var(--neutral-stroke-disabled-rest-4)",
      borderRight: "1px solid var(--neutral-stroke-disabled-rest-4)",
      borderBottom: "1px solid var(--neutral-stroke-disabled-rest-4)",
      borderLeft: "1px solid var(--neutral-stroke-disabled-rest-4)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-snudge) * 1px)",
      padding: "5px 11px 5px 11px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "max(0px, calc(var(--spacing-horizontal-m) * 1px - 1px))",
      paddingTop: "max(0px, calc(var(--vertical-snudge) * 1px - 1px))",
      paddingRight: "max(0px, calc(var(--spacing-horizontal-m) * 1px - 1px))",
      paddingBottom: "max(0px, calc(var(--vertical-snudge) * 1px - 1px))",
      position: "relative",
      ...props.style,
    }}>
      {props.icon && (
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
          color: "var(--neutral-foreground-disabled-rest-4)",
        }}>{props.xOutline20 ?? <Placeholder size={"20"} theme={"regular"} />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
        padding: "0px 0px 0px 0px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingTop: "0px", paddingBottom: "0px",
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
            color: "var(--neutral-foreground-disabled-rest-4)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.label}</span>
        </div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-disabled-rest-4)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
    </div>
  );
  const __body136 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-1-selected-3)",
      borderTop: "1px solid var(--neutral-stroke-1-selected-3)",
      borderRight: "1px solid var(--neutral-stroke-1-selected-3)",
      borderBottom: "1px solid var(--neutral-stroke-1-selected-3)",
      borderLeft: "1px solid var(--neutral-stroke-1-selected-3)",
      display: "flex",
      flexDirection: "row",
      padding: "5px 5px 5px 5px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "max(0px, calc(var(--spacing-horizontal-snudge) * 1px - 1px))",
      paddingTop: "max(0px, calc(var(--vertical-snudge) * 1px - 1px))",
      paddingRight: "max(0px, calc(var(--spacing-horizontal-snudge) * 1px - 1px))",
      paddingBottom: "max(0px, calc(var(--vertical-snudge) * 1px - 1px))",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
            color: "var(--neutral-foreground-1-selected-3)",
          }}>{props.xFilled20 ?? <Placeholder size={"20"} theme={"filled"} />}</div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-1-selected-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"filled"} />}</div>
        )}
      </div>
    </div>
  );
  const __body137 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-1-selected-3)",
      borderTop: "1px solid var(--neutral-stroke-1-selected-3)",
      borderRight: "1px solid var(--neutral-stroke-1-selected-3)",
      borderBottom: "1px solid var(--neutral-stroke-1-selected-3)",
      borderLeft: "1px solid var(--neutral-stroke-1-selected-3)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-snudge) * 1px)",
      padding: "5px 11px 5px 11px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "max(0px, calc(var(--spacing-horizontal-m) * 1px - 1px))",
      paddingTop: "max(0px, calc(var(--vertical-snudge) * 1px - 1px))",
      paddingRight: "max(0px, calc(var(--spacing-horizontal-m) * 1px - 1px))",
      paddingBottom: "max(0px, calc(var(--vertical-snudge) * 1px - 1px))",
      position: "relative",
      ...props.style,
    }}>
      {props.icon && (
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
          color: "var(--neutral-foreground-1-selected-3)",
        }}>{props.xFilled20 ?? <Placeholder size={"20"} theme={"filled"} />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
        padding: "0px 0px 0px 0px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingTop: "0px", paddingBottom: "0px",
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
            color: "var(--neutral-foreground-1-pressed-3)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.label}</span>
        </div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-1-selected-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"filled"} />}</div>
        )}
      </div>
    </div>
  );
  const __body138 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-1-pressed-3)",
      borderTop: "1px solid var(--neutral-stroke-1-pressed-3)",
      borderRight: "1px solid var(--neutral-stroke-1-pressed-3)",
      borderBottom: "1px solid var(--neutral-stroke-1-pressed-3)",
      borderLeft: "1px solid var(--neutral-stroke-1-pressed-3)",
      display: "flex",
      flexDirection: "row",
      padding: "5px 5px 5px 5px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "max(0px, calc(var(--spacing-horizontal-snudge) * 1px - 1px))",
      paddingTop: "max(0px, calc(var(--vertical-snudge) * 1px - 1px))",
      paddingRight: "max(0px, calc(var(--spacing-horizontal-snudge) * 1px - 1px))",
      paddingBottom: "max(0px, calc(var(--vertical-snudge) * 1px - 1px))",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
            color: "var(--neutral-foreground-1-pressed-3)",
          }}>{props.xOutline20 ?? <Placeholder size={"20"} theme={"regular"} />}</div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-1-pressed-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
    </div>
  );
  const __body139 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-1-pressed-3)",
      borderTop: "1px solid var(--neutral-stroke-1-pressed-3)",
      borderRight: "1px solid var(--neutral-stroke-1-pressed-3)",
      borderBottom: "1px solid var(--neutral-stroke-1-pressed-3)",
      borderLeft: "1px solid var(--neutral-stroke-1-pressed-3)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-snudge) * 1px)",
      padding: "5px 11px 5px 11px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "max(0px, calc(var(--spacing-horizontal-m) * 1px - 1px))",
      paddingTop: "max(0px, calc(var(--vertical-snudge) * 1px - 1px))",
      paddingRight: "max(0px, calc(var(--spacing-horizontal-m) * 1px - 1px))",
      paddingBottom: "max(0px, calc(var(--vertical-snudge) * 1px - 1px))",
      position: "relative",
      ...props.style,
    }}>
      {props.icon && (
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
          color: "var(--neutral-foreground-1-pressed-3)",
        }}>{props.xOutline20 ?? <Placeholder size={"20"} theme={"regular"} />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
        padding: "0px 0px 0px 0px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingTop: "0px", paddingBottom: "0px",
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
            color: "var(--neutral-foreground-1-pressed-3)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.label}</span>
        </div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-1-pressed-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
    </div>
  );
  const __body140 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-1-hover-3)",
      borderTop: "1px solid var(--neutral-stroke-1-hover-3)",
      borderRight: "1px solid var(--neutral-stroke-1-hover-3)",
      borderBottom: "1px solid var(--neutral-stroke-1-hover-3)",
      borderLeft: "1px solid var(--neutral-stroke-1-hover-3)",
      display: "flex",
      flexDirection: "row",
      padding: "5px 5px 5px 5px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "max(0px, calc(var(--spacing-horizontal-snudge) * 1px - 1px))",
      paddingTop: "max(0px, calc(var(--vertical-snudge) * 1px - 1px))",
      paddingRight: "max(0px, calc(var(--spacing-horizontal-snudge) * 1px - 1px))",
      paddingBottom: "max(0px, calc(var(--vertical-snudge) * 1px - 1px))",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
            color: "var(--neutral-foreground-1-hover-3)",
          }}>{props.xOutline20 ?? <Placeholder size={"20"} theme={"regular"} />}</div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-1-hover-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
    </div>
  );
  const __body141 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-1-hover-3)",
      borderTop: "1px solid var(--neutral-stroke-1-hover-3)",
      borderRight: "1px solid var(--neutral-stroke-1-hover-3)",
      borderBottom: "1px solid var(--neutral-stroke-1-hover-3)",
      borderLeft: "1px solid var(--neutral-stroke-1-hover-3)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-snudge) * 1px)",
      padding: "5px 11px 5px 11px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "max(0px, calc(var(--spacing-horizontal-m) * 1px - 1px))",
      paddingTop: "max(0px, calc(var(--vertical-snudge) * 1px - 1px))",
      paddingRight: "max(0px, calc(var(--spacing-horizontal-m) * 1px - 1px))",
      paddingBottom: "max(0px, calc(var(--vertical-snudge) * 1px - 1px))",
      position: "relative",
      ...props.style,
    }}>
      {props.icon && (
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
          color: "var(--neutral-foreground-1-hover-3)",
        }}>{props.xOutline20 ?? <Placeholder size={"20"} theme={"regular"} />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
        padding: "0px 0px 0px 0px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingTop: "0px", paddingBottom: "0px",
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
            color: "var(--neutral-foreground-1-hover-3)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.label}</span>
        </div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-1-hover-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
    </div>
  );
  const __body142 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-1-rest-3)",
      borderTop: "1px solid var(--neutral-stroke-1-rest-3)",
      borderRight: "1px solid var(--neutral-stroke-1-rest-3)",
      borderBottom: "1px solid var(--neutral-stroke-1-rest-3)",
      borderLeft: "1px solid var(--neutral-stroke-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      padding: "5px 5px 5px 5px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "max(0px, calc(var(--spacing-horizontal-snudge) * 1px - 1px))",
      paddingTop: "max(0px, calc(var(--vertical-snudge) * 1px - 1px))",
      paddingRight: "max(0px, calc(var(--spacing-horizontal-snudge) * 1px - 1px))",
      paddingBottom: "max(0px, calc(var(--vertical-snudge) * 1px - 1px))",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            flexShrink: 0,
            alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
            color: "var(--neutral-foreground-1-rest-3)",
          }}>{props.xOutline20 ?? <Placeholder size={"20"} theme={"regular"} />}</div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-1-rest-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
      {props.focus && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 32,
        height: 32,
        overflow: "hidden",
        borderRadius: 4,
        borderTop: "2px solid var(--neutral-stroke-focus-2-rest-4)",
        borderRight: "2px solid var(--neutral-stroke-focus-2-rest-4)",
        borderBottom: "2px solid var(--neutral-stroke-focus-2-rest-4)",
        borderLeft: "2px solid var(--neutral-stroke-focus-2-rest-4)",
      }}>
        <svg width={32} height={32} viewBox="0 0 32 32" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          overflow: "hidden",
          borderRadius: 4,
          borderTop: "1px solid var(--neutral-stroke-focus-1-rest-4)",
          borderRight: "1px solid var(--neutral-stroke-focus-1-rest-4)",
          borderBottom: "1px solid var(--neutral-stroke-focus-1-rest-4)",
          borderLeft: "1px solid var(--neutral-stroke-focus-1-rest-4)",
        }}>
          <path d={"M 4 0 L 4 1 L 28 1 L 28 0 L 28 -1 L 4 -1 L 4 0 Z M 32 4 L 31 4 L 31 28 L 32 28 L 33 28 L 33 4 L 32 4 Z M 28 32 L 28 31 L 4 31 L 4 32 L 4 33 L 28 33 L 28 32 Z M 0 28 L 1 28 L 1 4 L 0 4 L -1 4 L -1 28 L 0 28 Z M 4 32 L 4 31 C 2.343 31 1 29.657 1 28 L 0 28 L -1 28 C -1 30.761 1.239 33 4 33 L 4 32 Z M 32 28 L 31 28 C 31 29.657 29.657 31 28 31 L 28 32 L 28 33 C 30.761 33 33 30.761 33 28 L 32 28 Z M 28 0 L 28 1 C 29.657 1 31 2.343 31 4 L 32 4 L 33 4 C 33 1.239 30.761 -1 28 -1 L 28 0 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
    </div>
  );
  const __body143 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      borderRadius: 4,
      backgroundColor: "var(--neutral-background-1-rest-3)",
      borderTop: "1px solid var(--neutral-stroke-1-rest-3)",
      borderRight: "1px solid var(--neutral-stroke-1-rest-3)",
      borderBottom: "1px solid var(--neutral-stroke-1-rest-3)",
      borderLeft: "1px solid var(--neutral-stroke-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-snudge) * 1px)",
      padding: "5px 11px 5px 11px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "max(0px, calc(var(--spacing-horizontal-m) * 1px - 1px))",
      paddingTop: "max(0px, calc(var(--vertical-snudge) * 1px - 1px))",
      paddingRight: "max(0px, calc(var(--spacing-horizontal-m) * 1px - 1px))",
      paddingBottom: "max(0px, calc(var(--vertical-snudge) * 1px - 1px))",
      position: "relative",
      ...props.style,
    }}>
      {props.icon && (
      <div style={{
          position: "relative",
          flexShrink: 0,
          alignSelf: "center", display: "flex", alignItems: "center", justifyContent: "center",
          color: "var(--neutral-foreground-1-rest-3)",
        }}>{props.xOutline20 ?? <Placeholder size={"20"} theme={"regular"} />}</div>
      )}
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-xs) * 1px)",
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
          padding: "0px 0px 0px 0px",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          paddingTop: "0px", paddingBottom: "0px",
          flexShrink: 0,
        }}>
          <span style={{
            position: "relative",
            fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
            fontWeight: 600,
            fontSize: 14,
            whiteSpace: "nowrap",
            lineHeight: "20px",
            color: "var(--neutral-foreground-1-rest-3)",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>{props.label}</span>
        </div>
        {props.menuButton && (
        <div style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-1-rest-3)",
          }}>{props.icon1 ?? <Chevron direction={"down"} size={"12"} theme={"regular"} />}</div>
        )}
      </div>
      {props.focus && (
      <div style={{
        position: "absolute",
        left: 0,
        top: 0,
        width: 94,
        height: 32,
        overflow: "hidden",
        borderRadius: 4,
        borderTop: "2px solid var(--neutral-stroke-focus-2-rest-4)",
        borderRight: "2px solid var(--neutral-stroke-focus-2-rest-4)",
        borderBottom: "2px solid var(--neutral-stroke-focus-2-rest-4)",
        borderLeft: "2px solid var(--neutral-stroke-focus-2-rest-4)",
      }}>
        <svg width={94} height={32} viewBox="0 0 94 32" fill="none" style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 94,
          height: 32,
          overflow: "hidden",
          borderRadius: 4,
          borderTop: "1px solid var(--neutral-stroke-focus-1-rest-4)",
          borderRight: "1px solid var(--neutral-stroke-focus-1-rest-4)",
          borderBottom: "1px solid var(--neutral-stroke-focus-1-rest-4)",
          borderLeft: "1px solid var(--neutral-stroke-focus-1-rest-4)",
        }}>
          <path d={"M 4 0 L 4 1 L 90 1 L 90 0 L 90 -1 L 4 -1 L 4 0 Z M 94 4 L 93 4 L 93 28 L 94 28 L 95 28 L 95 4 L 94 4 Z M 90 32 L 90 31 L 4 31 L 4 32 L 4 33 L 90 33 L 90 32 Z M 0 28 L 1 28 L 1 4 L 0 4 L -1 4 L -1 28 L 0 28 Z M 4 32 L 4 31 C 2.343 31 1 29.657 1 28 L 0 28 L -1 28 C -1 30.761 1.239 33 4 33 L 4 32 Z M 94 28 L 93 28 C 93 29.657 91.657 31 90 31 L 90 32 L 90 33 C 92.761 33 95 30.761 95 28 L 94 28 Z M 90 0 L 90 1 C 91.657 1 93 2.343 93 4 L 94 4 L 95 4 C 95 1.239 92.761 -1 90 -1 L 90 0 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
        </svg>
      </div>
      )}
    </div>
  );
  const __impls = {
    // figma: Style=Transparent, State=Disabled, Size=Large, Layout=Icon only
    "style2=transparent|state=disabled|size=lg|layout=icon only": __body0,
    // figma: Style=Transparent, State=Disabled, Size=Large, Layout=Icon and label (Default)
    "style2=transparent|state=disabled|size=lg|layout=icon and label (default)": __body1,
    // figma: Style=Transparent, State=Selected, Size=Large, Layout=Icon only
    "style2=transparent|state=selected|size=lg|layout=icon only": __body2,
    // figma: Style=Transparent, State=Selected, Size=Large, Layout=Icon and label (Default)
    "style2=transparent|state=selected|size=lg|layout=icon and label (default)": __body3,
    // figma: Style=Transparent, State=Pressed, Size=Large, Layout=Icon only
    "style2=transparent|state=pressed|size=lg|layout=icon only": __body4,
    // figma: Style=Transparent, State=Pressed, Size=Large, Layout=Icon and label (Default)
    "style2=transparent|state=pressed|size=lg|layout=icon and label (default)": __body5,
    // figma: Style=Transparent, State=Hover, Size=Large, Layout=Icon only
    "style2=transparent|state=hover|size=lg|layout=icon only": __body6,
    // figma: Style=Transparent, State=Hover, Size=Large, Layout=Icon and label (Default)
    "style2=transparent|state=hover|size=lg|layout=icon and label (default)": __body7,
    // figma: Style=Transparent, State=Rest, Size=Large, Layout=Icon only
    "style2=transparent|state=rest|size=lg|layout=icon only": __body8,
    // figma: Style=Transparent, State=Rest, Size=Large, Layout=Icon and label (Default)
    "style2=transparent|state=rest|size=lg|layout=icon and label (default)": __body9,
    // figma: Style=Subtle, State=Disabled, Size=Large, Layout=Icon only
    "style2=subtle|state=disabled|size=lg|layout=icon only": __body10,
    // figma: Style=Subtle, State=Disabled, Size=Large, Layout=Icon and label (Default)
    "style2=subtle|state=disabled|size=lg|layout=icon and label (default)": __body11,
    // figma: Style=Subtle, State=Selected, Size=Large, Layout=Icon only
    "style2=subtle|state=selected|size=lg|layout=icon only": __body12,
    // figma: Style=Subtle, State=Selected, Size=Large, Layout=Icon and label (Default)
    "style2=subtle|state=selected|size=lg|layout=icon and label (default)": __body13,
    // figma: Style=Subtle, State=Pressed, Size=Large, Layout=Icon only
    "style2=subtle|state=pressed|size=lg|layout=icon only": __body14,
    // figma: Style=Subtle, State=Pressed, Size=Large, Layout=Icon and label (Default)
    "style2=subtle|state=pressed|size=lg|layout=icon and label (default)": __body15,
    // figma: Style=Subtle, State=Hover, Size=Large, Layout=Icon only
    "style2=subtle|state=hover|size=lg|layout=icon only": __body16,
    // figma: Style=Subtle, State=Hover, Size=Large, Layout=Icon and label (Default)
    "style2=subtle|state=hover|size=lg|layout=icon and label (default)": __body17,
    // figma: Style=Subtle, State=Rest, Size=Large, Layout=Icon only
    "style2=subtle|state=rest|size=lg|layout=icon only": __body8,
    // figma: Style=Subtle, State=Rest, Size=Large, Layout=Icon and label (Default)
    "style2=subtle|state=rest|size=lg|layout=icon and label (default)": __body9,
    // figma: Style=Outline, State=Disabled, Size=Large, Layout=Icon only
    "style2=outline|state=disabled|size=lg|layout=icon only": __body18,
    // figma: Style=Outline, State=Disabled, Size=Large, Layout=Icon and label (Default)
    "style2=outline|state=disabled|size=lg|layout=icon and label (default)": __body19,
    // figma: Style=Outline, State=Selected, Size=Large, Layout=Icon only
    "style2=outline|state=selected|size=lg|layout=icon only": __body20,
    // figma: Style=Outline, State=Selected, Size=Large, Layout=Icon and label (Default)
    "style2=outline|state=selected|size=lg|layout=icon and label (default)": __body21,
    // figma: Style=Outline, State=Pressed, Size=Large, Layout=Icon only
    "style2=outline|state=pressed|size=lg|layout=icon only": __body22,
    // figma: Style=Outline, State=Pressed, Size=Large, Layout=Icon and label (Default)
    "style2=outline|state=pressed|size=lg|layout=icon and label (default)": __body23,
    // figma: Style=Outline, State=Hover, Size=Large, Layout=Icon only
    "style2=outline|state=hover|size=lg|layout=icon only": __body24,
    // figma: Style=Outline, State=Hover, Size=Large, Layout=Icon and label (Default)
    "style2=outline|state=hover|size=lg|layout=icon and label (default)": __body25,
    // figma: Style=Outline, State=Rest, Size=Large, Layout=Icon only
    "style2=outline|state=rest|size=lg|layout=icon only": __body26,
    // figma: Style=Outline, State=Rest, Size=Large, Layout=Icon and label (Default)
    "style2=outline|state=rest|size=lg|layout=icon and label (default)": __body27,
    // figma: Style=Primary, State=Disabled, Size=Large, Layout=Icon only
    "style2=primary|state=disabled|size=lg|layout=icon only": __body28,
    // figma: Style=Primary, State=Disabled, Size=Large, Layout=Icon and label (Default)
    "style2=primary|state=disabled|size=lg|layout=icon and label (default)": __body29,
    // figma: Style=Primary, State=Selected, Size=Large, Layout=Icon only
    "style2=primary|state=selected|size=lg|layout=icon only": __body30,
    // figma: Style=Primary, State=Selected, Size=Large, Layout=Icon and label (Default)
    "style2=primary|state=selected|size=lg|layout=icon and label (default)": __body31,
    // figma: Style=Primary, State=Pressed, Size=Large, Layout=Icon only
    "style2=primary|state=pressed|size=lg|layout=icon only": __body32,
    // figma: Style=Primary, State=Pressed, Size=Large, Layout=Icon and label (Default)
    "style2=primary|state=pressed|size=lg|layout=icon and label (default)": __body33,
    // figma: Style=Primary, State=Hover, Size=Large, Layout=Icon only
    "style2=primary|state=hover|size=lg|layout=icon only": __body34,
    // figma: Style=Primary, State=Hover, Size=Large, Layout=Icon and label (Default)
    "style2=primary|state=hover|size=lg|layout=icon and label (default)": __body35,
    // figma: Style=Primary, State=Rest, Size=Large, Layout=Icon only
    "style2=primary|state=rest|size=lg|layout=icon only": __body36,
    // figma: Style=Primary, State=Rest, Size=Large, Layout=Icon and label (Default)
    "style2=primary|state=rest|size=lg|layout=icon and label (default)": __body37,
    // figma: Style=Secondary (Default), State=Disabled, Size=Large, Layout=Icon only
    "style2=secondary (default)|state=disabled|size=lg|layout=icon only": __body38,
    // figma: Style=Secondary (Default), State=Disabled, Size=Large, Layout=Icon and label (Default)
    "style2=secondary (default)|state=disabled|size=lg|layout=icon and label (default)": __body39,
    // figma: Style=Secondary (Default), State=Selected, Size=Large, Layout=Icon only
    "style2=secondary (default)|state=selected|size=lg|layout=icon only": __body40,
    // figma: Style=Secondary (Default), State=Selected, Size=Large, Layout=Icon and label (Default)
    "style2=secondary (default)|state=selected|size=lg|layout=icon and label (default)": __body41,
    // figma: Style=Secondary (Default), State=Pressed, Size=Large, Layout=Icon only
    "style2=secondary (default)|state=pressed|size=lg|layout=icon only": __body42,
    // figma: Style=Secondary (Default), State=Pressed, Size=Large, Layout=Icon and label (Default)
    "style2=secondary (default)|state=pressed|size=lg|layout=icon and label (default)": __body43,
    // figma: Style=Secondary (Default), State=Hover, Size=Large, Layout=Icon only
    "style2=secondary (default)|state=hover|size=lg|layout=icon only": __body44,
    // figma: Style=Secondary (Default), State=Hover, Size=Large, Layout=Icon and label (Default)
    "style2=secondary (default)|state=hover|size=lg|layout=icon and label (default)": __body45,
    // figma: Style=Secondary (Default), State=Rest, Size=Large, Layout=Icon only
    "style2=secondary (default)|state=rest|size=lg|layout=icon only": __body46,
    // figma: Style=Secondary (Default), State=Rest, Size=Large, Layout=Icon and label (Default)
    "style2=secondary (default)|state=rest|size=lg|layout=icon and label (default)": __body47,
    // figma: Style=Transparent, State=Disabled, Size=Small, Layout=Icon only
    "style2=transparent|state=disabled|size=sm|layout=icon only": __body48,
    // figma: Style=Transparent, State=Disabled, Size=Small, Layout=Icon and label (Default)
    "style2=transparent|state=disabled|size=sm|layout=icon and label (default)": __body49,
    // figma: Style=Transparent, State=Selected, Size=Small, Layout=Icon only
    "style2=transparent|state=selected|size=sm|layout=icon only": __body50,
    // figma: Style=Transparent, State=Selected, Size=Small, Layout=Icon and label (Default)
    "style2=transparent|state=selected|size=sm|layout=icon and label (default)": __body51,
    // figma: Style=Transparent, State=Pressed, Size=Small, Layout=Icon only
    "style2=transparent|state=pressed|size=sm|layout=icon only": __body52,
    // figma: Style=Transparent, State=Pressed, Size=Small, Layout=Icon and label (Default)
    "style2=transparent|state=pressed|size=sm|layout=icon and label (default)": __body53,
    // figma: Style=Transparent, State=Hover, Size=Small, Layout=Icon only
    "style2=transparent|state=hover|size=sm|layout=icon only": __body54,
    // figma: Style=Transparent, State=Hover, Size=Small, Layout=Icon and label (Default)
    "style2=transparent|state=hover|size=sm|layout=icon and label (default)": __body55,
    // figma: Style=Transparent, State=Rest, Size=Small, Layout=Icon only
    "style2=transparent|state=rest|size=sm|layout=icon only": __body56,
    // figma: Style=Transparent, State=Rest, Size=Small, Layout=Icon and label (Default)
    "style2=transparent|state=rest|size=sm|layout=icon and label (default)": __body57,
    // figma: Style=Subtle, State=Disabled, Size=Small, Layout=Icon only
    "style2=subtle|state=disabled|size=sm|layout=icon only": __body58,
    // figma: Style=Subtle, State=Disabled, Size=Small, Layout=Icon and label (Default)
    "style2=subtle|state=disabled|size=sm|layout=icon and label (default)": __body59,
    // figma: Style=Subtle, State=Selected, Size=Small, Layout=Icon only
    "style2=subtle|state=selected|size=sm|layout=icon only": __body60,
    // figma: Style=Subtle, State=Selected, Size=Small, Layout=Icon and label (Default)
    "style2=subtle|state=selected|size=sm|layout=icon and label (default)": __body61,
    // figma: Style=Subtle, State=Pressed, Size=Small, Layout=Icon only
    "style2=subtle|state=pressed|size=sm|layout=icon only": __body62,
    // figma: Style=Subtle, State=Pressed, Size=Small, Layout=Icon and label (Default)
    "style2=subtle|state=pressed|size=sm|layout=icon and label (default)": __body63,
    // figma: Style=Subtle, State=Hover, Size=Small, Layout=Icon only
    "style2=subtle|state=hover|size=sm|layout=icon only": __body64,
    // figma: Style=Subtle, State=Hover, Size=Small, Layout=Icon and label (Default)
    "style2=subtle|state=hover|size=sm|layout=icon and label (default)": __body65,
    // figma: Style=Subtle, State=Rest, Size=Small, Layout=Icon only
    "style2=subtle|state=rest|size=sm|layout=icon only": __body56,
    // figma: Style=Subtle, State=Rest, Size=Small, Layout=Icon and label (Default)
    "style2=subtle|state=rest|size=sm|layout=icon and label (default)": __body57,
    // figma: Style=Outline, State=Disabled, Size=Small, Layout=Icon only
    "style2=outline|state=disabled|size=sm|layout=icon only": __body66,
    // figma: Style=Outline, State=Disabled, Size=Small, Layout=Icon and label (Default)
    "style2=outline|state=disabled|size=sm|layout=icon and label (default)": __body67,
    // figma: Style=Outline, State=Selected, Size=Small, Layout=Icon only
    "style2=outline|state=selected|size=sm|layout=icon only": __body68,
    // figma: Style=Outline, State=Selected, Size=Small, Layout=Icon and label (Default)
    "style2=outline|state=selected|size=sm|layout=icon and label (default)": __body69,
    // figma: Style=Outline, State=Pressed, Size=Small, Layout=Icon only
    "style2=outline|state=pressed|size=sm|layout=icon only": __body70,
    // figma: Style=Outline, State=Pressed, Size=Small, Layout=Icon and label (Default)
    "style2=outline|state=pressed|size=sm|layout=icon and label (default)": __body71,
    // figma: Style=Outline, State=Hover, Size=Small, Layout=Icon only
    "style2=outline|state=hover|size=sm|layout=icon only": __body72,
    // figma: Style=Outline, State=Hover, Size=Small, Layout=Icon and label (Default)
    "style2=outline|state=hover|size=sm|layout=icon and label (default)": __body73,
    // figma: Style=Outline, State=Rest, Size=Small, Layout=Icon only
    "style2=outline|state=rest|size=sm|layout=icon only": __body74,
    // figma: Style=Outline, State=Rest, Size=Small, Layout=Icon and label (Default)
    "style2=outline|state=rest|size=sm|layout=icon and label (default)": __body75,
    // figma: Style=Primary, State=Disabled, Size=Small, Layout=Icon only
    "style2=primary|state=disabled|size=sm|layout=icon only": __body76,
    // figma: Style=Primary, State=Disabled, Size=Small, Layout=Icon and label (Default)
    "style2=primary|state=disabled|size=sm|layout=icon and label (default)": __body77,
    // figma: Style=Primary, State=Selected, Size=Small, Layout=Icon only
    "style2=primary|state=selected|size=sm|layout=icon only": __body78,
    // figma: Style=Primary, State=Selected, Size=Small, Layout=Icon and label (Default)
    "style2=primary|state=selected|size=sm|layout=icon and label (default)": __body79,
    // figma: Style=Primary, State=Pressed, Size=Small, Layout=Icon only
    "style2=primary|state=pressed|size=sm|layout=icon only": __body80,
    // figma: Style=Primary, State=Pressed, Size=Small, Layout=Icon and label (Default)
    "style2=primary|state=pressed|size=sm|layout=icon and label (default)": __body81,
    // figma: Style=Primary, State=Hover, Size=Small, Layout=Icon only
    "style2=primary|state=hover|size=sm|layout=icon only": __body82,
    // figma: Style=Primary, State=Hover, Size=Small, Layout=Icon and label (Default)
    "style2=primary|state=hover|size=sm|layout=icon and label (default)": __body83,
    // figma: Style=Primary, State=Rest, Size=Small, Layout=Icon only
    "style2=primary|state=rest|size=sm|layout=icon only": __body84,
    // figma: Style=Primary, State=Rest, Size=Small, Layout=Icon and label (Default)
    "style2=primary|state=rest|size=sm|layout=icon and label (default)": __body85,
    // figma: Style=Secondary (Default), State=Disabled, Size=Small, Layout=Icon only
    "style2=secondary (default)|state=disabled|size=sm|layout=icon only": __body86,
    // figma: Style=Secondary (Default), State=Disabled, Size=Small, Layout=Icon and label (Default)
    "style2=secondary (default)|state=disabled|size=sm|layout=icon and label (default)": __body87,
    // figma: Style=Secondary (Default), State=Selected, Size=Small, Layout=Icon only
    "style2=secondary (default)|state=selected|size=sm|layout=icon only": __body88,
    // figma: Style=Secondary (Default), State=Selected, Size=Small, Layout=Icon and label (Default)
    "style2=secondary (default)|state=selected|size=sm|layout=icon and label (default)": __body89,
    // figma: Style=Secondary (Default), State=Pressed, Size=Small, Layout=Icon only
    "style2=secondary (default)|state=pressed|size=sm|layout=icon only": __body90,
    // figma: Style=Secondary (Default), State=Pressed, Size=Small, Layout=Icon and label (Default)
    "style2=secondary (default)|state=pressed|size=sm|layout=icon and label (default)": __body91,
    // figma: Style=Secondary (Default), State=Hover, Size=Small, Layout=Icon only
    "style2=secondary (default)|state=hover|size=sm|layout=icon only": __body92,
    // figma: Style=Secondary (Default), State=Hover, Size=Small, Layout=Icon and label (Default)
    "style2=secondary (default)|state=hover|size=sm|layout=icon and label (default)": __body93,
    // figma: Style=Secondary (Default), State=Rest, Size=Small, Layout=Icon only
    "style2=secondary (default)|state=rest|size=sm|layout=icon only": __body94,
    // figma: Style=Secondary (Default), State=Rest, Size=Small, Layout=Icon and label (Default)
    "style2=secondary (default)|state=rest|size=sm|layout=icon and label (default)": __body95,
    // figma: Style=Transparent, State=Disabled, Size=Medium (Default), Layout=Icon only
    "style2=transparent|state=disabled|size=medium (default)|layout=icon only": __body96,
    // figma: Style=Transparent, State=Disabled, Size=Medium (Default), Layout=Icon and label (Default)
    "style2=transparent|state=disabled|size=medium (default)|layout=icon and label (default)": __body97,
    // figma: Style=Transparent, State=Selected, Size=Medium (Default), Layout=Icon only
    "style2=transparent|state=selected|size=medium (default)|layout=icon only": __body98,
    // figma: Style=Transparent, State=Selected, Size=Medium (Default), Layout=Icon and label (Default)
    "style2=transparent|state=selected|size=medium (default)|layout=icon and label (default)": __body99,
    // figma: Style=Transparent, State=Pressed, Size=Medium (Default), Layout=Icon only
    "style2=transparent|state=pressed|size=medium (default)|layout=icon only": __body100,
    // figma: Style=Transparent, State=Pressed, Size=Medium (Default), Layout=Icon and label (Default)
    "style2=transparent|state=pressed|size=medium (default)|layout=icon and label (default)": __body101,
    // figma: Style=Transparent, State=Hover, Size=Medium (Default), Layout=Icon only
    "style2=transparent|state=hover|size=medium (default)|layout=icon only": __body102,
    // figma: Style=Transparent, State=Hover, Size=Medium (Default), Layout=Icon and label (Default)
    "style2=transparent|state=hover|size=medium (default)|layout=icon and label (default)": __body103,
    // figma: Style=Transparent, State=Rest, Size=Medium (Default), Layout=Icon only
    "style2=transparent|state=rest|size=medium (default)|layout=icon only": __body104,
    // figma: Style=Transparent, State=Rest, Size=Medium (Default), Layout=Icon and label (Default)
    "style2=transparent|state=rest|size=medium (default)|layout=icon and label (default)": __body105,
    // figma: Style=Subtle, State=Disabled, Size=Medium (Default), Layout=Icon only
    "style2=subtle|state=disabled|size=medium (default)|layout=icon only": __body106,
    // figma: Style=Subtle, State=Disabled, Size=Medium (Default), Layout=Icon and label (Default)
    "style2=subtle|state=disabled|size=medium (default)|layout=icon and label (default)": __body107,
    // figma: Style=Subtle, State=Selected, Size=Medium (Default), Layout=Icon only
    "style2=subtle|state=selected|size=medium (default)|layout=icon only": __body108,
    // figma: Style=Subtle, State=Selected, Size=Medium (Default), Layout=Icon and label (Default)
    "style2=subtle|state=selected|size=medium (default)|layout=icon and label (default)": __body109,
    // figma: Style=Subtle, State=Pressed, Size=Medium (Default), Layout=Icon only
    "style2=subtle|state=pressed|size=medium (default)|layout=icon only": __body110,
    // figma: Style=Subtle, State=Pressed, Size=Medium (Default), Layout=Icon and label (Default)
    "style2=subtle|state=pressed|size=medium (default)|layout=icon and label (default)": __body111,
    // figma: Style=Subtle, State=Hover, Size=Medium (Default), Layout=Icon only
    "style2=subtle|state=hover|size=medium (default)|layout=icon only": __body112,
    // figma: Style=Subtle, State=Hover, Size=Medium (Default), Layout=Icon and label (Default)
    "style2=subtle|state=hover|size=medium (default)|layout=icon and label (default)": __body113,
    // figma: Style=Subtle, State=Rest, Size=Medium (Default), Layout=Icon only
    "style2=subtle|state=rest|size=medium (default)|layout=icon only": __body104,
    // figma: Style=Subtle, State=Rest, Size=Medium (Default), Layout=Icon and label (Default)
    "style2=subtle|state=rest|size=medium (default)|layout=icon and label (default)": __body105,
    // figma: Style=Outline, State=Disabled, Size=Medium (Default), Layout=Icon only
    "style2=outline|state=disabled|size=medium (default)|layout=icon only": __body114,
    // figma: Style=Outline, State=Disabled, Size=Medium (Default), Layout=Icon and label (Default)
    "style2=outline|state=disabled|size=medium (default)|layout=icon and label (default)": __body115,
    // figma: Style=Outline, State=Selected, Size=Medium (Default), Layout=Icon only
    "style2=outline|state=selected|size=medium (default)|layout=icon only": __body116,
    // figma: Style=Outline, State=Selected, Size=Medium (Default), Layout=Icon and label (Default)
    "style2=outline|state=selected|size=medium (default)|layout=icon and label (default)": __body117,
    // figma: Style=Outline, State=Pressed, Size=Medium (Default), Layout=Icon only
    "style2=outline|state=pressed|size=medium (default)|layout=icon only": __body118,
    // figma: Style=Outline, State=Pressed, Size=Medium (Default), Layout=Icon and label (Default)
    "style2=outline|state=pressed|size=medium (default)|layout=icon and label (default)": __body119,
    // figma: Style=Outline, State=Hover, Size=Medium (Default), Layout=Icon only
    "style2=outline|state=hover|size=medium (default)|layout=icon only": __body120,
    // figma: Style=Outline, State=Hover, Size=Medium (Default), Layout=Icon and label (Default)
    "style2=outline|state=hover|size=medium (default)|layout=icon and label (default)": __body121,
    // figma: Style=Outline, State=Rest, Size=Medium (Default), Layout=Icon only
    "style2=outline|state=rest|size=medium (default)|layout=icon only": __body122,
    // figma: Style=Outline, State=Rest, Size=Medium (Default), Layout=Icon and label (Default)
    "style2=outline|state=rest|size=medium (default)|layout=icon and label (default)": __body123,
    // figma: Style=Primary, State=Disabled, Size=Medium (Default), Layout=Icon only
    "style2=primary|state=disabled|size=medium (default)|layout=icon only": __body124,
    // figma: Style=Primary, State=Disabled, Size=Medium (Default), Layout=Icon and label (Default)
    "style2=primary|state=disabled|size=medium (default)|layout=icon and label (default)": __body125,
    // figma: Style=Primary, State=Selected, Size=Medium (Default), Layout=Icon only
    "style2=primary|state=selected|size=medium (default)|layout=icon only": __body126,
    // figma: Style=Primary, State=Selected, Size=Medium (Default), Layout=Icon and label (Default)
    "style2=primary|state=selected|size=medium (default)|layout=icon and label (default)": __body127,
    // figma: Style=Primary, State=Pressed, Size=Medium (Default), Layout=Icon only
    "style2=primary|state=pressed|size=medium (default)|layout=icon only": __body128,
    // figma: Style=Primary, State=Pressed, Size=Medium (Default), Layout=Icon and label (Default)
    "style2=primary|state=pressed|size=medium (default)|layout=icon and label (default)": __body129,
    // figma: Style=Primary, State=Hover, Size=Medium (Default), Layout=Icon only
    "style2=primary|state=hover|size=medium (default)|layout=icon only": __body130,
    // figma: Style=Primary, State=Hover, Size=Medium (Default), Layout=Icon and label (Default)
    "style2=primary|state=hover|size=medium (default)|layout=icon and label (default)": __body131,
    // figma: Style=Primary, State=Rest, Size=Medium (Default), Layout=Icon only
    "style2=primary|state=rest|size=medium (default)|layout=icon only": __body132,
    // figma: Style=Primary, State=Rest, Size=Medium (Default), Layout=Icon and label (Default)
    "style2=primary|state=rest|size=medium (default)|layout=icon and label (default)": __body133,
    // figma: Style=Secondary (Default), State=Disabled, Size=Medium (Default), Layout=Icon only
    "style2=secondary (default)|state=disabled|size=medium (default)|layout=icon only": __body134,
    // figma: Style=Secondary (Default), State=Disabled, Size=Medium (Default), Layout=Icon and label (Default)
    "style2=secondary (default)|state=disabled|size=medium (default)|layout=icon and label (default)": __body135,
    // figma: Style=Secondary (Default), State=Selected, Size=Medium (Default), Layout=Icon only
    "style2=secondary (default)|state=selected|size=medium (default)|layout=icon only": __body136,
    // figma: Style=Secondary (Default), State=Selected, Size=Medium (Default), Layout=Icon and label (Default)
    "style2=secondary (default)|state=selected|size=medium (default)|layout=icon and label (default)": __body137,
    // figma: Style=Secondary (Default), State=Pressed, Size=Medium (Default), Layout=Icon only
    "style2=secondary (default)|state=pressed|size=medium (default)|layout=icon only": __body138,
    // figma: Style=Secondary (Default), State=Pressed, Size=Medium (Default), Layout=Icon and label (Default)
    "style2=secondary (default)|state=pressed|size=medium (default)|layout=icon and label (default)": __body139,
    // figma: Style=Secondary (Default), State=Hover, Size=Medium (Default), Layout=Icon only
    "style2=secondary (default)|state=hover|size=medium (default)|layout=icon only": __body140,
    // figma: Style=Secondary (Default), State=Hover, Size=Medium (Default), Layout=Icon and label (Default)
    "style2=secondary (default)|state=hover|size=medium (default)|layout=icon and label (default)": __body141,
    // figma: Style=Secondary (Default), State=Rest, Size=Medium (Default), Layout=Icon only
    "style2=secondary (default)|state=rest|size=medium (default)|layout=icon only": __body142,
    // figma: Style=Secondary (Default), State=Rest, Size=Medium (Default), Layout=Icon and label (Default)
    "style2=secondary (default)|state=rest|size=medium (default)|layout=icon and label (default)": __body143,
  };
  return (__impls[__vkey(props)] ?? __body37)();
}
/* Windows UI kit: a subtle icon-only button is a 40 × 40 target with a 30 × 30 fill (radius 3 inside 4) and a 16 px glyph.
   Every other variant is the Fluent 2 Web design above. */
export function Button(props = {}) {
  const ap = props.appearance ?? props.style2, sz = props.size;
  if (ap === 'subtle' && props.layout === 'icon only' && (sz == null || sz === 'medium' || sz === 'medium (default)')) {
    const st = props.state ?? 'rest';
    const g = st === 'selected' ? props.xFilled20 ?? props.icon1 ?? props.xOutline20 : props.icon1 ?? props.xOutline20 ?? props.xFilled20;
    const bg = st === 'disabled' ? 'transparent' : st === 'pressed' ? 'var(--surface-pressed)' : st === 'hover' || st === 'selected' ? 'var(--surface-hover)' : 'transparent';
    const fg = st === 'disabled' ? 'var(--text-disabled)' : st === 'pressed' ? 'var(--text-secondary)' : st === 'selected' ? 'var(--accent-text)' : 'var(--text-primary)';
    return <span className={props.className} style={{ position: 'relative', width: 40, height: 40, flex: 'none', display: 'inline-flex', alignItems: 'center', justifyContent: 'center', borderRadius: 4, ...props.style }}>
      <span style={{ position: 'absolute', left: 5, top: 5, width: 30, height: 30, borderRadius: 3, background: bg, outline: props.focus ? '2px solid var(--focus-ring)' : 'none', outlineOffset: 1 }} />
      <span style={{ position: 'relative', width: 16, height: 16, display: 'flex', alignItems: 'center', justifyContent: 'center', color: fg }}>{g}</span>
    </span>;
  }
  /* Fluent 2 sizes: small 24, medium 32, large 40, borders included. The label sits in a 16, 20 or 22 px line box,
     centred by the root's alignItems. */
  const minH = sz === 'sm' || sz === 'small' ? 24 : sz === 'lg' || sz === 'large' || sz == null ? 40 : 32;
  return React.createElement(__FluentWebButton, { ...props, style: { minHeight: minH, ...props.style } });
}
export default Button;
