import { __slateNorm } from '../internal/slateProps.jsx';
// figma node: 9064:763 Badge (112 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "color=" + __venc(p.color) + '|' + "size=" + __venc(p.size) + '|' + "appearance=" + __venc(p.appearance);

export function Badge(_p = {}) {
  _p = __slateNorm(_p, {"color":["brand","danger","warning","success","important","informative","subtle"],"size":["extra large","lg","md","sm"],"appearance":["filled","tint","outline","subtle"]});
  const props = { ..._p, color: _p.color ?? "brand", size: _p.size ?? "extra large", appearance: _p.appearance ?? "filled", leftIcon: _p.leftIcon ?? true, showText: _p.showText ?? true, rightIcon: _p.rightIcon ?? false, text: _p.text ?? "Badge" };
  const __body0 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      minWidth: 32,
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--brand-background-1-rest-3)",
      boxShadow: "inset 0 0 0 1px var(--brand-stroke-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xxs) * 1px)",
      padding: "0px 6px 0px 6px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-snudge) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-snudge) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--neutral-foreground-on-brand-rest-3)",
          }}>
            <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        height: 16,
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 1px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        paddingBottom: "calc(var(--typography-caption-1-strong-offset) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 12,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "16px",
          color: "var(--neutral-foreground-on-brand-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--neutral-foreground-on-brand-rest-3)",
          }}>
            <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      minWidth: 32,
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--status-danger-background-3-rest-3)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xxs) * 1px)",
      padding: "0px 6px 0px 6px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-snudge) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-snudge) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--colors-neutral-grey-white)",
          }}>
            <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        height: 16,
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 1px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 12,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "16px",
          color: "var(--colors-neutral-grey-white)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--colors-neutral-grey-white)",
          }}>
            <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body2 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      minWidth: 32,
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--status-success-background-3-rest-3)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xxs) * 1px)",
      padding: "0px 6px 0px 6px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-snudge) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-snudge) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--colors-neutral-grey-white)",
          }}>
            <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        height: 16,
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 1px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 12,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "16px",
          color: "var(--colors-neutral-grey-white)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--colors-neutral-grey-white)",
          }}>
            <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body3 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      minWidth: 32,
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-foreground-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xxs) * 1px)",
      padding: "0px 6px 0px 6px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-snudge) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-snudge) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--neutral-background-1-rest-3)",
          }}>
            <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        height: 16,
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 1px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 12,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "16px",
          color: "var(--neutral-background-1-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--neutral-background-1-rest-3)",
          }}>
            <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body4 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      minWidth: 32,
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-5-rest-3)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xxs) * 1px)",
      padding: "0px 6px 0px 6px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-snudge) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-snudge) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--neutral-foreground-3-rest-3)",
          }}>
            <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        height: 16,
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 1px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 12,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "16px",
          color: "var(--neutral-foreground-3-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--neutral-foreground-3-rest-3)",
          }}>
            <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body5 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      minWidth: 32,
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-5-rest-3)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xxs) * 1px)",
      padding: "0px 6px 0px 6px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-snudge) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-snudge) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--neutral-foreground-1-rest-3)",
          }}>
            <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        height: 16,
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 1px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 12,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "16px",
          color: "var(--neutral-foreground-1-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--neutral-foreground-1-rest-3)",
          }}>
            <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body6 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      minWidth: 32,
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--status-warning-background-3-rest-3)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xxs) * 1px)",
      padding: "0px 6px 0px 6px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-snudge) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-snudge) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--neutral-foreground-static-rest-3)",
          }}>
            <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        height: 16,
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 1px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 12,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "16px",
          color: "var(--neutral-foreground-static-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--neutral-foreground-static-rest-3)",
          }}>
            <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body7 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      minWidth: 24,
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--brand-background-1-rest-3)",
      boxShadow: "inset 0 0 0 1px var(--brand-stroke-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xxs) * 1px)",
      padding: "0px 4px 0px 4px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-on-brand-rest-3)",
          }}>
            <path d={"M 6 12 C 9.314 12 12 9.314 12 6 C 12 2.686 9.314 0 6 0 C 2.686 0 0 2.686 0 6 C 0 9.314 2.686 12 6 12 Z M 6 11 C 3.239 11 1 8.761 1 6 C 1 3.239 3.239 1 6 1 C 8.761 1 11 3.239 11 6 C 11 8.761 8.761 11 6 11 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        height: 16,
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 1px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        paddingBottom: "calc(var(--typography-caption-1-strong-offset) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 12,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "16px",
          color: "var(--neutral-foreground-on-brand-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-on-brand-rest-3)",
          }}>
            <path d={"M 6 12 C 9.314 12 12 9.314 12 6 C 12 2.686 9.314 0 6 0 C 2.686 0 0 2.686 0 6 C 0 9.314 2.686 12 6 12 Z M 6 11 C 3.239 11 1 8.761 1 6 C 1 3.239 3.239 1 6 1 C 8.761 1 11 3.239 11 6 C 11 8.761 8.761 11 6 11 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body8 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      minWidth: 24,
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--status-danger-background-3-rest-3)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xxs) * 1px)",
      padding: "0px 4px 0px 4px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--colors-neutral-grey-white)",
          }}>
            <path d={"M 6 12 C 9.314 12 12 9.314 12 6 C 12 2.686 9.314 0 6 0 C 2.686 0 0 2.686 0 6 C 0 9.314 2.686 12 6 12 Z M 6 11 C 3.239 11 1 8.761 1 6 C 1 3.239 3.239 1 6 1 C 8.761 1 11 3.239 11 6 C 11 8.761 8.761 11 6 11 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        height: 16,
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 1px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 12,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "16px",
          color: "var(--colors-neutral-grey-white)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--colors-neutral-grey-white)",
          }}>
            <path d={"M 6 12 C 9.314 12 12 9.314 12 6 C 12 2.686 9.314 0 6 0 C 2.686 0 0 2.686 0 6 C 0 9.314 2.686 12 6 12 Z M 6 11 C 3.239 11 1 8.761 1 6 C 1 3.239 3.239 1 6 1 C 8.761 1 11 3.239 11 6 C 11 8.761 8.761 11 6 11 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body9 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      minWidth: 24,
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--status-success-background-3-rest-3)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xxs) * 1px)",
      padding: "0px 4px 0px 4px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--colors-neutral-grey-white)",
          }}>
            <path d={"M 6 12 C 9.314 12 12 9.314 12 6 C 12 2.686 9.314 0 6 0 C 2.686 0 0 2.686 0 6 C 0 9.314 2.686 12 6 12 Z M 6 11 C 3.239 11 1 8.761 1 6 C 1 3.239 3.239 1 6 1 C 8.761 1 11 3.239 11 6 C 11 8.761 8.761 11 6 11 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        height: 16,
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 1px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 12,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "16px",
          color: "var(--colors-neutral-grey-white)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--colors-neutral-grey-white)",
          }}>
            <path d={"M 6 12 C 9.314 12 12 9.314 12 6 C 12 2.686 9.314 0 6 0 C 2.686 0 0 2.686 0 6 C 0 9.314 2.686 12 6 12 Z M 6 11 C 3.239 11 1 8.761 1 6 C 1 3.239 3.239 1 6 1 C 8.761 1 11 3.239 11 6 C 11 8.761 8.761 11 6 11 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body10 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      minWidth: 24,
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-foreground-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xxs) * 1px)",
      padding: "0px 4px 0px 4px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-background-1-rest-3)",
          }}>
            <path d={"M 6 12 C 9.314 12 12 9.314 12 6 C 12 2.686 9.314 0 6 0 C 2.686 0 0 2.686 0 6 C 0 9.314 2.686 12 6 12 Z M 6 11 C 3.239 11 1 8.761 1 6 C 1 3.239 3.239 1 6 1 C 8.761 1 11 3.239 11 6 C 11 8.761 8.761 11 6 11 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        height: 16,
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 1px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 12,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "16px",
          color: "var(--neutral-background-1-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-background-1-rest-3)",
          }}>
            <path d={"M 6 12 C 9.314 12 12 9.314 12 6 C 12 2.686 9.314 0 6 0 C 2.686 0 0 2.686 0 6 C 0 9.314 2.686 12 6 12 Z M 6 11 C 3.239 11 1 8.761 1 6 C 1 3.239 3.239 1 6 1 C 8.761 1 11 3.239 11 6 C 11 8.761 8.761 11 6 11 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body11 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      minWidth: 24,
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-5-rest-3)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xxs) * 1px)",
      padding: "0px 4px 0px 4px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-3-rest-3)",
          }}>
            <path d={"M 6 12 C 9.314 12 12 9.314 12 6 C 12 2.686 9.314 0 6 0 C 2.686 0 0 2.686 0 6 C 0 9.314 2.686 12 6 12 Z M 6 11 C 3.239 11 1 8.761 1 6 C 1 3.239 3.239 1 6 1 C 8.761 1 11 3.239 11 6 C 11 8.761 8.761 11 6 11 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        height: 16,
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 1px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 12,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "16px",
          color: "var(--neutral-foreground-3-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-3-rest-3)",
          }}>
            <path d={"M 6 12 C 9.314 12 12 9.314 12 6 C 12 2.686 9.314 0 6 0 C 2.686 0 0 2.686 0 6 C 0 9.314 2.686 12 6 12 Z M 6 11 C 3.239 11 1 8.761 1 6 C 1 3.239 3.239 1 6 1 C 8.761 1 11 3.239 11 6 C 11 8.761 8.761 11 6 11 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body12 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      minWidth: 24,
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-5-rest-3)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xxs) * 1px)",
      padding: "0px 4px 0px 4px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-1-rest-3)",
          }}>
            <path d={"M 6 12 C 9.314 12 12 9.314 12 6 C 12 2.686 9.314 0 6 0 C 2.686 0 0 2.686 0 6 C 0 9.314 2.686 12 6 12 Z M 6 11 C 3.239 11 1 8.761 1 6 C 1 3.239 3.239 1 6 1 C 8.761 1 11 3.239 11 6 C 11 8.761 8.761 11 6 11 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        height: 16,
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 1px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 12,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "16px",
          color: "var(--neutral-foreground-1-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-1-rest-3)",
          }}>
            <path d={"M 6 12 C 9.314 12 12 9.314 12 6 C 12 2.686 9.314 0 6 0 C 2.686 0 0 2.686 0 6 C 0 9.314 2.686 12 6 12 Z M 6 11 C 3.239 11 1 8.761 1 6 C 1 3.239 3.239 1 6 1 C 8.761 1 11 3.239 11 6 C 11 8.761 8.761 11 6 11 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body13 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      minWidth: 24,
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--status-warning-background-3-rest-3)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xxs) * 1px)",
      padding: "0px 4px 0px 4px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-static-rest-3)",
          }}>
            <path d={"M 6 12 C 9.314 12 12 9.314 12 6 C 12 2.686 9.314 0 6 0 C 2.686 0 0 2.686 0 6 C 0 9.314 2.686 12 6 12 Z M 6 11 C 3.239 11 1 8.761 1 6 C 1 3.239 3.239 1 6 1 C 8.761 1 11 3.239 11 6 C 11 8.761 8.761 11 6 11 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        height: 16,
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 1px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 12,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "16px",
          color: "var(--neutral-foreground-static-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-static-rest-3)",
          }}>
            <path d={"M 6 12 C 9.314 12 12 9.314 12 6 C 12 2.686 9.314 0 6 0 C 2.686 0 0 2.686 0 6 C 0 9.314 2.686 12 6 12 Z M 6 11 C 3.239 11 1 8.761 1 6 C 1 3.239 3.239 1 6 1 C 8.761 1 11 3.239 11 6 C 11 8.761 8.761 11 6 11 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body14 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 20,
      minWidth: 20,
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--brand-background-1-rest-3)",
      boxShadow: "inset 0 0 0 1px var(--brand-stroke-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 4px 0px 4px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none) * 1px)",
      paddingLeft: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--neutral-foreground-on-brand-rest-3)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        height: 14,
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 0.500px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        paddingBottom: "calc(var(--typography-caption-2-strong-offset) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: "calc(var(--typography-caption-2-strong-font-size) * 1px)",
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "14px",
          color: "var(--neutral-foreground-on-brand-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--neutral-foreground-on-brand-rest-3)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body15 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 20,
      minWidth: 20,
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--status-danger-background-3-rest-3)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 4px 0px 4px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none) * 1px)",
      paddingLeft: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--colors-neutral-grey-white)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        height: 14,
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 0.500px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 10,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "14px",
          color: "var(--colors-neutral-grey-white)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--colors-neutral-grey-white)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body16 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 20,
      minWidth: 20,
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--status-success-background-3-rest-3)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 4px 0px 4px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none) * 1px)",
      paddingLeft: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--colors-neutral-grey-white)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        height: 14,
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 0.500px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 10,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "14px",
          color: "var(--colors-neutral-grey-white)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--colors-neutral-grey-white)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body17 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 20,
      minWidth: 20,
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-foreground-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 4px 0px 4px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none) * 1px)",
      paddingLeft: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--neutral-background-1-rest-3)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        height: 14,
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 0.500px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 10,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "14px",
          color: "var(--neutral-background-1-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--neutral-background-1-rest-3)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body18 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 20,
      minWidth: 20,
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-5-rest-3)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 4px 0px 4px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none) * 1px)",
      paddingLeft: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--neutral-foreground-3-rest-3)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        height: 14,
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 0.500px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 10,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "14px",
          color: "var(--neutral-foreground-3-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--neutral-foreground-3-rest-3)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body19 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 20,
      minWidth: 20,
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-5-rest-3)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 4px 0px 4px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none) * 1px)",
      paddingLeft: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--neutral-foreground-1-rest-3)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        height: 14,
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 0.500px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 10,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "14px",
          color: "var(--neutral-foreground-1-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--neutral-foreground-1-rest-3)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body20 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 20,
      minWidth: 20,
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--status-warning-background-3-rest-3)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 4px 0px 4px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none) * 1px)",
      paddingLeft: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--neutral-foreground-static-rest-3)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        height: 14,
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 0.500px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 10,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "14px",
          color: "var(--neutral-foreground-static-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--neutral-foreground-static-rest-3)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body21 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 16,
      minWidth: 16,
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--brand-background-1-rest-3)",
      boxShadow: "inset 0 0 0 1px var(--brand-stroke-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 2px 0px 2px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-xxs) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xxs) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--neutral-foreground-on-brand-rest-3)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 0.500px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        paddingBottom: "calc(var(--typography-caption-2-strong-offset) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 10,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "14px",
          color: "var(--neutral-foreground-on-brand-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--neutral-foreground-on-brand-rest-3)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body22 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 16,
      minWidth: 16,
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--status-danger-background-3-rest-3)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 2px 0px 2px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none) * 1px)",
      paddingLeft: "calc(var(--spacing-horizontal-xxs) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xxs) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--colors-neutral-grey-white)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 0.500px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 10,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "14px",
          color: "var(--colors-neutral-grey-white)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--colors-neutral-grey-white)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body23 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 16,
      minWidth: 16,
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--status-success-background-3-rest-3)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 2px 0px 2px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none) * 1px)",
      paddingLeft: "calc(var(--spacing-horizontal-xxs) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xxs) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--colors-neutral-grey-white)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 0.500px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 10,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "14px",
          color: "var(--colors-neutral-grey-white)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--colors-neutral-grey-white)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body24 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 16,
      minWidth: 16,
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-foreground-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 2px 0px 2px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none) * 1px)",
      paddingLeft: "calc(var(--spacing-horizontal-xxs) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xxs) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--neutral-background-1-rest-3)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 0.500px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 10,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "14px",
          color: "var(--neutral-background-1-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--neutral-background-1-rest-3)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body25 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 16,
      minWidth: 16,
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-5-rest-3)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 2px 0px 2px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none) * 1px)",
      paddingLeft: "calc(var(--spacing-horizontal-xxs) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xxs) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--neutral-foreground-3-rest-3)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 0.500px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 10,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "14px",
          color: "var(--neutral-foreground-3-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--neutral-foreground-3-rest-3)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body26 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 16,
      minWidth: 16,
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-5-rest-3)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 2px 0px 2px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none) * 1px)",
      paddingLeft: "calc(var(--spacing-horizontal-xxs) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xxs) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--neutral-foreground-1-rest-3)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 0.500px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 10,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "14px",
          color: "var(--neutral-foreground-1-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--neutral-foreground-1-rest-3)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body27 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 16,
      minWidth: 16,
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--status-warning-background-3-rest-3)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 2px 0px 2px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none) * 1px)",
      paddingLeft: "calc(var(--spacing-horizontal-xxs) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xxs) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--neutral-foreground-static-rest-3)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 0.500px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 10,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "14px",
          color: "var(--neutral-foreground-static-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--neutral-foreground-static-rest-3)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body28 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      minWidth: 32,
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--brand-background-2-rest-5)",
      boxShadow: "inset 0 0 0 1px var(--brand-stroke-2-rest-3)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xxs) * 1px)",
      padding: "0px 6px 0px 6px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-snudge) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-snudge) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--brand-foreground-2-rest-4)",
          }}>
            <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        height: 16,
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 1px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        paddingBottom: "calc(var(--typography-caption-1-strong-offset) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 12,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "16px",
          color: "var(--brand-foreground-2-rest-4)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--brand-foreground-2-rest-4)",
          }}>
            <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body29 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      minWidth: 32,
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--status-danger-background-1-rest-3)",
      boxShadow: "inset 0 0 0 1px var(--status-danger-stroke-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xxs) * 1px)",
      padding: "0px 6px 0px 6px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-snudge) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-snudge) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--status-danger-foreground-1-rest-3)",
          }}>
            <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        height: 16,
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 1px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 12,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "16px",
          color: "var(--status-danger-foreground-1-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--status-danger-foreground-1-rest-3)",
          }}>
            <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body30 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      minWidth: 32,
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--status-success-background-1-rest-3)",
      boxShadow: "inset 0 0 0 1px var(--status-success-stroke-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xxs) * 1px)",
      padding: "0px 6px 0px 6px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-snudge) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-snudge) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--status-success-foreground-1-rest-3)",
          }}>
            <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        height: 16,
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 1px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 12,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "16px",
          color: "var(--status-success-foreground-1-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--status-success-foreground-1-rest-3)",
          }}>
            <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body31 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      minWidth: 32,
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-foreground-3-rest-3)",
      boxShadow: "inset 0 0 0 1px var(--neutral-stroke-accessible-rest-3)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xxs) * 1px)",
      padding: "0px 6px 0px 6px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-snudge) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-snudge) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--neutral-background-1-rest-3)",
          }}>
            <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        height: 16,
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 1px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 12,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "16px",
          color: "var(--neutral-background-1-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--neutral-background-1-rest-3)",
          }}>
            <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body32 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      minWidth: 32,
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-4-rest-3)",
      boxShadow: "inset 0 0 0 1px var(--neutral-stroke-2-rest-3)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xxs) * 1px)",
      padding: "0px 6px 0px 6px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-snudge) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-snudge) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--neutral-foreground-3-rest-3)",
          }}>
            <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        height: 16,
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 1px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 12,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "16px",
          color: "var(--neutral-foreground-3-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--neutral-foreground-3-rest-3)",
          }}>
            <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body33 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      minWidth: 32,
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-1-rest-3)",
      boxShadow: "inset 0 0 0 1px var(--neutral-stroke-2-rest-3)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xxs) * 1px)",
      padding: "0px 6px 0px 6px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-snudge) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-snudge) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--neutral-foreground-3-rest-3)",
          }}>
            <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        height: 16,
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 1px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 12,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "16px",
          color: "var(--neutral-foreground-3-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--neutral-foreground-3-rest-3)",
          }}>
            <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body34 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      minWidth: 32,
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--status-warning-background-1-rest-3)",
      boxShadow: "inset 0 0 0 1px var(--status-warning-stroke-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xxs) * 1px)",
      padding: "0px 6px 0px 6px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-snudge) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-snudge) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--status-warning-foreground-2-rest-3)",
          }}>
            <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        height: 16,
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 1px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 12,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "16px",
          color: "var(--status-warning-foreground-2-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--status-warning-foreground-2-rest-3)",
          }}>
            <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body35 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      minWidth: 24,
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--brand-background-2-rest-5)",
      boxShadow: "inset 0 0 0 1px var(--brand-stroke-2-rest-3)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xxs) * 1px)",
      padding: "0px 4px 0px 4px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--brand-foreground-2-rest-4)",
          }}>
            <path d={"M 6 12 C 9.314 12 12 9.314 12 6 C 12 2.686 9.314 0 6 0 C 2.686 0 0 2.686 0 6 C 0 9.314 2.686 12 6 12 Z M 6 11 C 3.239 11 1 8.761 1 6 C 1 3.239 3.239 1 6 1 C 8.761 1 11 3.239 11 6 C 11 8.761 8.761 11 6 11 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        height: 16,
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 1px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        paddingBottom: "calc(var(--typography-caption-1-strong-offset) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 12,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "16px",
          color: "var(--brand-foreground-2-rest-4)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--brand-foreground-2-rest-4)",
          }}>
            <path d={"M 6 12 C 9.314 12 12 9.314 12 6 C 12 2.686 9.314 0 6 0 C 2.686 0 0 2.686 0 6 C 0 9.314 2.686 12 6 12 Z M 6 11 C 3.239 11 1 8.761 1 6 C 1 3.239 3.239 1 6 1 C 8.761 1 11 3.239 11 6 C 11 8.761 8.761 11 6 11 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body36 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      minWidth: 24,
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--status-danger-background-1-rest-3)",
      boxShadow: "inset 0 0 0 1px var(--status-danger-stroke-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xxs) * 1px)",
      padding: "0px 4px 0px 4px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--status-danger-foreground-1-rest-3)",
          }}>
            <path d={"M 6 12 C 9.314 12 12 9.314 12 6 C 12 2.686 9.314 0 6 0 C 2.686 0 0 2.686 0 6 C 0 9.314 2.686 12 6 12 Z M 6 11 C 3.239 11 1 8.761 1 6 C 1 3.239 3.239 1 6 1 C 8.761 1 11 3.239 11 6 C 11 8.761 8.761 11 6 11 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        height: 16,
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 1px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 12,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "16px",
          color: "var(--status-danger-foreground-1-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--status-danger-foreground-1-rest-3)",
          }}>
            <path d={"M 6 12 C 9.314 12 12 9.314 12 6 C 12 2.686 9.314 0 6 0 C 2.686 0 0 2.686 0 6 C 0 9.314 2.686 12 6 12 Z M 6 11 C 3.239 11 1 8.761 1 6 C 1 3.239 3.239 1 6 1 C 8.761 1 11 3.239 11 6 C 11 8.761 8.761 11 6 11 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body37 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      minWidth: 24,
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--status-success-background-1-rest-3)",
      boxShadow: "inset 0 0 0 1px var(--status-success-stroke-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xxs) * 1px)",
      padding: "0px 4px 0px 4px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--status-success-foreground-1-rest-3)",
          }}>
            <path d={"M 6 12 C 9.314 12 12 9.314 12 6 C 12 2.686 9.314 0 6 0 C 2.686 0 0 2.686 0 6 C 0 9.314 2.686 12 6 12 Z M 6 11 C 3.239 11 1 8.761 1 6 C 1 3.239 3.239 1 6 1 C 8.761 1 11 3.239 11 6 C 11 8.761 8.761 11 6 11 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        height: 16,
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 1px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 12,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "16px",
          color: "var(--status-success-foreground-1-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--status-success-foreground-1-rest-3)",
          }}>
            <path d={"M 6 12 C 9.314 12 12 9.314 12 6 C 12 2.686 9.314 0 6 0 C 2.686 0 0 2.686 0 6 C 0 9.314 2.686 12 6 12 Z M 6 11 C 3.239 11 1 8.761 1 6 C 1 3.239 3.239 1 6 1 C 8.761 1 11 3.239 11 6 C 11 8.761 8.761 11 6 11 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body38 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      minWidth: 24,
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-foreground-3-rest-3)",
      boxShadow: "inset 0 0 0 1px var(--neutral-stroke-accessible-rest-3)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xxs) * 1px)",
      padding: "0px 4px 0px 4px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-background-1-rest-3)",
          }}>
            <path d={"M 6 12 C 9.314 12 12 9.314 12 6 C 12 2.686 9.314 0 6 0 C 2.686 0 0 2.686 0 6 C 0 9.314 2.686 12 6 12 Z M 6 11 C 3.239 11 1 8.761 1 6 C 1 3.239 3.239 1 6 1 C 8.761 1 11 3.239 11 6 C 11 8.761 8.761 11 6 11 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        height: 16,
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 1px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 12,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "16px",
          color: "var(--neutral-background-1-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-background-1-rest-3)",
          }}>
            <path d={"M 6 12 C 9.314 12 12 9.314 12 6 C 12 2.686 9.314 0 6 0 C 2.686 0 0 2.686 0 6 C 0 9.314 2.686 12 6 12 Z M 6 11 C 3.239 11 1 8.761 1 6 C 1 3.239 3.239 1 6 1 C 8.761 1 11 3.239 11 6 C 11 8.761 8.761 11 6 11 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body39 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      minWidth: 24,
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-4-rest-3)",
      boxShadow: "inset 0 0 0 1px var(--neutral-stroke-2-rest-3)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xxs) * 1px)",
      padding: "0px 4px 0px 4px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-3-rest-3)",
          }}>
            <path d={"M 6 12 C 9.314 12 12 9.314 12 6 C 12 2.686 9.314 0 6 0 C 2.686 0 0 2.686 0 6 C 0 9.314 2.686 12 6 12 Z M 6 11 C 3.239 11 1 8.761 1 6 C 1 3.239 3.239 1 6 1 C 8.761 1 11 3.239 11 6 C 11 8.761 8.761 11 6 11 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        height: 16,
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 1px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 12,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "16px",
          color: "var(--neutral-foreground-3-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-3-rest-3)",
          }}>
            <path d={"M 6 12 C 9.314 12 12 9.314 12 6 C 12 2.686 9.314 0 6 0 C 2.686 0 0 2.686 0 6 C 0 9.314 2.686 12 6 12 Z M 6 11 C 3.239 11 1 8.761 1 6 C 1 3.239 3.239 1 6 1 C 8.761 1 11 3.239 11 6 C 11 8.761 8.761 11 6 11 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body40 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      minWidth: 24,
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-1-rest-3)",
      boxShadow: "inset 0 0 0 1px var(--neutral-stroke-2-rest-3)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xxs) * 1px)",
      padding: "0px 4px 0px 4px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-3-rest-3)",
          }}>
            <path d={"M 6 12 C 9.314 12 12 9.314 12 6 C 12 2.686 9.314 0 6 0 C 2.686 0 0 2.686 0 6 C 0 9.314 2.686 12 6 12 Z M 6 11 C 3.239 11 1 8.761 1 6 C 1 3.239 3.239 1 6 1 C 8.761 1 11 3.239 11 6 C 11 8.761 8.761 11 6 11 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        height: 16,
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 1px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 12,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "16px",
          color: "var(--neutral-foreground-3-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-3-rest-3)",
          }}>
            <path d={"M 6 12 C 9.314 12 12 9.314 12 6 C 12 2.686 9.314 0 6 0 C 2.686 0 0 2.686 0 6 C 0 9.314 2.686 12 6 12 Z M 6 11 C 3.239 11 1 8.761 1 6 C 1 3.239 3.239 1 6 1 C 8.761 1 11 3.239 11 6 C 11 8.761 8.761 11 6 11 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body41 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      minWidth: 24,
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--status-warning-background-1-rest-3)",
      boxShadow: "inset 0 0 0 1px var(--status-warning-stroke-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xxs) * 1px)",
      padding: "0px 4px 0px 4px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--status-warning-foreground-2-rest-3)",
          }}>
            <path d={"M 6 12 C 9.314 12 12 9.314 12 6 C 12 2.686 9.314 0 6 0 C 2.686 0 0 2.686 0 6 C 0 9.314 2.686 12 6 12 Z M 6 11 C 3.239 11 1 8.761 1 6 C 1 3.239 3.239 1 6 1 C 8.761 1 11 3.239 11 6 C 11 8.761 8.761 11 6 11 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        height: 16,
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 1px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 12,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "16px",
          color: "var(--status-warning-foreground-2-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--status-warning-foreground-2-rest-3)",
          }}>
            <path d={"M 6 12 C 9.314 12 12 9.314 12 6 C 12 2.686 9.314 0 6 0 C 2.686 0 0 2.686 0 6 C 0 9.314 2.686 12 6 12 Z M 6 11 C 3.239 11 1 8.761 1 6 C 1 3.239 3.239 1 6 1 C 8.761 1 11 3.239 11 6 C 11 8.761 8.761 11 6 11 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body42 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 20,
      minWidth: 20,
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--brand-background-2-rest-5)",
      boxShadow: "inset 0 0 0 1px var(--brand-stroke-2-rest-3)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 4px 0px 4px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none) * 1px)",
      paddingLeft: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--brand-foreground-2-rest-4)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        height: 14,
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 0.500px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        paddingBottom: "calc(var(--typography-caption-2-strong-offset) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 10,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "14px",
          color: "var(--brand-foreground-2-rest-4)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--brand-foreground-2-rest-4)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body43 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 20,
      minWidth: 20,
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--status-danger-background-1-rest-3)",
      boxShadow: "inset 0 0 0 1px var(--status-danger-stroke-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 4px 0px 4px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none) * 1px)",
      paddingLeft: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--status-danger-foreground-1-rest-3)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        height: 14,
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 0.500px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 10,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "14px",
          color: "var(--status-danger-foreground-1-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--status-danger-foreground-1-rest-3)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body44 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 20,
      minWidth: 20,
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--status-success-background-1-rest-3)",
      boxShadow: "inset 0 0 0 1px var(--status-success-stroke-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 4px 0px 4px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none) * 1px)",
      paddingLeft: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--status-success-foreground-1-rest-3)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        height: 14,
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 0.500px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 10,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "14px",
          color: "var(--status-success-foreground-1-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--status-success-foreground-1-rest-3)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body45 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 20,
      minWidth: 20,
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-foreground-3-rest-3)",
      boxShadow: "inset 0 0 0 1px var(--neutral-stroke-accessible-rest-3)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 4px 0px 4px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none) * 1px)",
      paddingLeft: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--neutral-background-1-rest-3)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        height: 14,
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 0.500px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 10,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "14px",
          color: "var(--neutral-background-1-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--neutral-background-1-rest-3)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body46 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 20,
      minWidth: 20,
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-4-rest-3)",
      boxShadow: "inset 0 0 0 1px var(--neutral-stroke-2-rest-3)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 4px 0px 4px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none) * 1px)",
      paddingLeft: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--neutral-foreground-3-rest-3)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        height: 14,
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 0.500px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 10,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "14px",
          color: "var(--neutral-foreground-3-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--neutral-foreground-3-rest-3)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body47 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 20,
      minWidth: 20,
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-1-rest-3)",
      boxShadow: "inset 0 0 0 1px var(--neutral-stroke-2-rest-3)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 4px 0px 4px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none) * 1px)",
      paddingLeft: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--neutral-foreground-3-rest-3)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        height: 14,
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 0.500px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 10,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "14px",
          color: "var(--neutral-foreground-3-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--neutral-foreground-3-rest-3)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body48 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 20,
      minWidth: 20,
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--status-warning-background-1-rest-3)",
      boxShadow: "inset 0 0 0 1px var(--status-warning-stroke-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 4px 0px 4px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none) * 1px)",
      paddingLeft: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--status-warning-foreground-2-rest-3)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        height: 14,
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 0.500px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 10,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "14px",
          color: "var(--status-warning-foreground-2-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--status-warning-foreground-2-rest-3)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body49 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 16,
      minWidth: 16,
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--brand-background-2-rest-5)",
      boxShadow: "inset 0 0 0 1px var(--brand-stroke-2-rest-3)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 2px 0px 2px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-xxs) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xxs) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--brand-foreground-2-rest-4)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 0.500px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        paddingBottom: "calc(var(--typography-caption-2-strong-offset) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 10,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "14px",
          color: "var(--brand-foreground-2-rest-4)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--brand-foreground-2-rest-4)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body50 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 16,
      minWidth: 16,
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--status-danger-background-1-rest-3)",
      boxShadow: "inset 0 0 0 1px var(--status-danger-stroke-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 2px 0px 2px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none) * 1px)",
      paddingLeft: "calc(var(--spacing-horizontal-xxs) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xxs) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--status-danger-foreground-1-rest-3)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 0.500px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 10,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "14px",
          color: "var(--status-danger-foreground-1-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--status-danger-foreground-1-rest-3)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body51 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 16,
      minWidth: 16,
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--status-success-background-1-rest-3)",
      boxShadow: "inset 0 0 0 1px var(--status-success-stroke-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 2px 0px 2px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none) * 1px)",
      paddingLeft: "calc(var(--spacing-horizontal-xxs) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xxs) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--status-success-foreground-1-rest-3)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 0.500px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 10,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "14px",
          color: "var(--status-success-foreground-1-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--status-success-foreground-1-rest-3)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body52 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 16,
      minWidth: 16,
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-foreground-3-rest-3)",
      boxShadow: "inset 0 0 0 1px var(--neutral-stroke-accessible-rest-3)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 2px 0px 2px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none) * 1px)",
      paddingLeft: "calc(var(--spacing-horizontal-xxs) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xxs) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--neutral-background-1-rest-3)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 0.500px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 10,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "14px",
          color: "var(--neutral-background-1-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--neutral-background-1-rest-3)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body53 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 16,
      minWidth: 16,
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-4-rest-3)",
      boxShadow: "inset 0 0 0 1px var(--neutral-stroke-2-rest-3)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 2px 0px 2px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none) * 1px)",
      paddingLeft: "calc(var(--spacing-horizontal-xxs) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xxs) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--neutral-foreground-3-rest-3)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 0.500px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 10,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "14px",
          color: "var(--neutral-foreground-3-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--neutral-foreground-3-rest-3)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body54 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 16,
      minWidth: 16,
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--neutral-background-1-rest-3)",
      boxShadow: "inset 0 0 0 1px var(--neutral-stroke-2-rest-3)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 2px 0px 2px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none) * 1px)",
      paddingLeft: "calc(var(--spacing-horizontal-xxs) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xxs) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--neutral-foreground-3-rest-3)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 0.500px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 10,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "14px",
          color: "var(--neutral-foreground-3-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--neutral-foreground-3-rest-3)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body55 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 16,
      minWidth: 16,
      overflow: "hidden",
      borderRadius: 9999,
      backgroundColor: "var(--status-warning-background-1-rest-3)",
      boxShadow: "inset 0 0 0 1px var(--status-warning-stroke-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 2px 0px 2px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none) * 1px)",
      paddingLeft: "calc(var(--spacing-horizontal-xxs) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xxs) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--status-warning-foreground-2-rest-3)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 0.500px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 10,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "14px",
          color: "var(--status-warning-foreground-2-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--status-warning-foreground-2-rest-3)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body56 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      minWidth: 32,
      overflow: "hidden",
      borderRadius: 9999,
      boxShadow: "inset 0 0 0 1px var(--brand-stroke-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xxs) * 1px)",
      padding: "0px 6px 0px 6px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-snudge) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-snudge) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--brand-foreground-1-rest-4)",
          }}>
            <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        height: 16,
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 1px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        paddingBottom: "calc(var(--typography-caption-1-strong-offset) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 12,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "16px",
          color: "var(--brand-foreground-1-rest-4)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--brand-foreground-1-rest-4)",
          }}>
            <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body57 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      minWidth: 32,
      overflow: "hidden",
      borderRadius: 9999,
      boxShadow: "inset 0 0 0 1px var(--status-danger-stroke-2-rest-3)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xxs) * 1px)",
      padding: "0px 6px 0px 6px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-snudge) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-snudge) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--status-danger-foreground-3-rest-3)",
          }}>
            <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        height: 16,
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 1px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 12,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "16px",
          color: "var(--status-danger-foreground-3-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--status-danger-foreground-3-rest-3)",
          }}>
            <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body58 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      minWidth: 32,
      overflow: "hidden",
      borderRadius: 9999,
      boxShadow: "inset 0 0 0 1px var(--status-success-stroke-2-rest-3)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xxs) * 1px)",
      padding: "0px 6px 0px 6px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-snudge) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-snudge) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--status-success-foreground-3-rest-3)",
          }}>
            <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        height: 16,
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 1px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 12,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "16px",
          color: "var(--status-success-foreground-3-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--status-success-foreground-3-rest-3)",
          }}>
            <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body59 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      minWidth: 32,
      overflow: "hidden",
      borderRadius: 9999,
      boxShadow: "inset 0 0 0 1px var(--neutral-stroke-accessible-rest-3)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xxs) * 1px)",
      padding: "0px 6px 0px 6px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-snudge) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-snudge) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--neutral-foreground-3-rest-3)",
          }}>
            <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        height: 16,
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 1px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 12,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "16px",
          color: "var(--neutral-foreground-3-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--neutral-foreground-3-rest-3)",
          }}>
            <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body60 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      minWidth: 32,
      overflow: "hidden",
      borderRadius: 9999,
      boxShadow: "inset 0 0 0 1px var(--neutral-stroke-2-rest-3)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xxs) * 1px)",
      padding: "0px 6px 0px 6px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-snudge) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-snudge) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--neutral-foreground-3-rest-3)",
          }}>
            <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        height: 16,
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 1px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 12,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "16px",
          color: "var(--neutral-foreground-3-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--neutral-foreground-3-rest-3)",
          }}>
            <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body61 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      minWidth: 32,
      overflow: "hidden",
      borderRadius: 9999,
      boxShadow: "inset 0 0 0 1px var(--neutral-foreground-on-brand-rest-3)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xxs) * 1px)",
      padding: "0px 6px 0px 6px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-snudge) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-snudge) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--neutral-foreground-on-brand-rest-3)",
          }}>
            <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        height: 16,
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 1px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 12,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "16px",
          color: "var(--neutral-foreground-on-brand-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--neutral-foreground-on-brand-rest-3)",
          }}>
            <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body62 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      minWidth: 32,
      overflow: "hidden",
      borderRadius: 9999,
      boxShadow: "inset 0 0 0 1px var(--status-warning-foreground-2-rest-3)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xxs) * 1px)",
      padding: "0px 6px 0px 6px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-snudge) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-snudge) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--status-warning-foreground-2-rest-3)",
          }}>
            <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        height: 16,
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 1px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 12,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "16px",
          color: "var(--status-warning-foreground-2-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--status-warning-foreground-2-rest-3)",
          }}>
            <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body63 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      minWidth: 32,
      overflow: "hidden",
      borderRadius: 9999,
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xxs) * 1px)",
      padding: "0px 6px 0px 6px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-snudge) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-snudge) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--brand-foreground-1-rest-4)",
          }}>
            <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        height: 16,
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 1px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        paddingBottom: "calc(var(--typography-caption-1-strong-offset) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 12,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "16px",
          color: "var(--brand-foreground-1-rest-4)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--brand-foreground-1-rest-4)",
          }}>
            <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body64 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      minWidth: 32,
      overflow: "hidden",
      borderRadius: 9999,
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xxs) * 1px)",
      padding: "0px 6px 0px 6px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-snudge) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-snudge) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--status-danger-foreground-3-rest-3)",
          }}>
            <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        height: 16,
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 1px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 12,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "16px",
          color: "var(--status-danger-foreground-3-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--status-danger-foreground-3-rest-3)",
          }}>
            <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body65 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      minWidth: 32,
      overflow: "hidden",
      borderRadius: 9999,
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xxs) * 1px)",
      padding: "0px 6px 0px 6px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-snudge) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-snudge) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--status-success-foreground-3-rest-3)",
          }}>
            <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        height: 16,
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 1px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 12,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "16px",
          color: "var(--status-success-foreground-3-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--status-success-foreground-3-rest-3)",
          }}>
            <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body66 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      minWidth: 32,
      overflow: "hidden",
      borderRadius: 9999,
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xxs) * 1px)",
      padding: "0px 6px 0px 6px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-snudge) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-snudge) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--neutral-foreground-3-rest-3)",
          }}>
            <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        height: 16,
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 1px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 12,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "16px",
          color: "var(--neutral-foreground-3-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--neutral-foreground-3-rest-3)",
          }}>
            <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body67 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      minWidth: 32,
      overflow: "hidden",
      borderRadius: 9999,
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xxs) * 1px)",
      padding: "0px 6px 0px 6px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-snudge) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-snudge) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--neutral-foreground-on-brand-rest-3)",
          }}>
            <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        height: 16,
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 1px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 12,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "16px",
          color: "var(--neutral-foreground-on-brand-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--neutral-foreground-on-brand-rest-3)",
          }}>
            <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body68 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 32,
      minWidth: 32,
      overflow: "hidden",
      borderRadius: 9999,
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xxs) * 1px)",
      padding: "0px 6px 0px 6px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-snudge) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-snudge) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--status-warning-foreground-2-rest-3)",
          }}>
            <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        height: 16,
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 1px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 12,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "16px",
          color: "var(--status-warning-foreground-2-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--status-warning-foreground-2-rest-3)",
          }}>
            <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z M 8 15 C 4.134 15 1 11.866 1 8 C 1 4.134 4.134 1 8 1 C 11.866 1 15 4.134 15 8 C 15 11.866 11.866 15 8 15 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body69 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      minWidth: 24,
      overflow: "hidden",
      borderRadius: 9999,
      boxShadow: "inset 0 0 0 1px var(--brand-stroke-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xxs) * 1px)",
      padding: "0px 4px 0px 4px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--brand-foreground-1-rest-4)",
          }}>
            <path d={"M 6 12 C 9.314 12 12 9.314 12 6 C 12 2.686 9.314 0 6 0 C 2.686 0 0 2.686 0 6 C 0 9.314 2.686 12 6 12 Z M 6 11 C 3.239 11 1 8.761 1 6 C 1 3.239 3.239 1 6 1 C 8.761 1 11 3.239 11 6 C 11 8.761 8.761 11 6 11 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        height: 16,
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 1px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        paddingBottom: "calc(var(--typography-caption-1-strong-offset) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 12,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "16px",
          color: "var(--brand-foreground-1-rest-4)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--brand-foreground-1-rest-4)",
          }}>
            <path d={"M 6 12 C 9.314 12 12 9.314 12 6 C 12 2.686 9.314 0 6 0 C 2.686 0 0 2.686 0 6 C 0 9.314 2.686 12 6 12 Z M 6 11 C 3.239 11 1 8.761 1 6 C 1 3.239 3.239 1 6 1 C 8.761 1 11 3.239 11 6 C 11 8.761 8.761 11 6 11 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body70 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      minWidth: 24,
      overflow: "hidden",
      borderRadius: 9999,
      boxShadow: "inset 0 0 0 1px var(--status-danger-stroke-2-rest-3)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xxs) * 1px)",
      padding: "0px 4px 0px 4px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--status-danger-foreground-3-rest-3)",
          }}>
            <path d={"M 6 12 C 9.314 12 12 9.314 12 6 C 12 2.686 9.314 0 6 0 C 2.686 0 0 2.686 0 6 C 0 9.314 2.686 12 6 12 Z M 6 11 C 3.239 11 1 8.761 1 6 C 1 3.239 3.239 1 6 1 C 8.761 1 11 3.239 11 6 C 11 8.761 8.761 11 6 11 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        height: 16,
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 1px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 12,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "16px",
          color: "var(--status-danger-foreground-3-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--status-danger-foreground-3-rest-3)",
          }}>
            <path d={"M 6 12 C 9.314 12 12 9.314 12 6 C 12 2.686 9.314 0 6 0 C 2.686 0 0 2.686 0 6 C 0 9.314 2.686 12 6 12 Z M 6 11 C 3.239 11 1 8.761 1 6 C 1 3.239 3.239 1 6 1 C 8.761 1 11 3.239 11 6 C 11 8.761 8.761 11 6 11 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body71 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      minWidth: 24,
      overflow: "hidden",
      borderRadius: 9999,
      boxShadow: "inset 0 0 0 1px var(--status-success-stroke-2-rest-3)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xxs) * 1px)",
      padding: "0px 4px 0px 4px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--status-success-foreground-3-rest-3)",
          }}>
            <path d={"M 6 12 C 9.314 12 12 9.314 12 6 C 12 2.686 9.314 0 6 0 C 2.686 0 0 2.686 0 6 C 0 9.314 2.686 12 6 12 Z M 6 11 C 3.239 11 1 8.761 1 6 C 1 3.239 3.239 1 6 1 C 8.761 1 11 3.239 11 6 C 11 8.761 8.761 11 6 11 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        height: 16,
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 1px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 12,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "16px",
          color: "var(--status-success-foreground-3-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--status-success-foreground-3-rest-3)",
          }}>
            <path d={"M 6 12 C 9.314 12 12 9.314 12 6 C 12 2.686 9.314 0 6 0 C 2.686 0 0 2.686 0 6 C 0 9.314 2.686 12 6 12 Z M 6 11 C 3.239 11 1 8.761 1 6 C 1 3.239 3.239 1 6 1 C 8.761 1 11 3.239 11 6 C 11 8.761 8.761 11 6 11 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body72 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      minWidth: 24,
      overflow: "hidden",
      borderRadius: 9999,
      boxShadow: "inset 0 0 0 1px var(--neutral-stroke-accessible-rest-3)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xxs) * 1px)",
      padding: "0px 4px 0px 4px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-3-rest-3)",
          }}>
            <path d={"M 6 12 C 9.314 12 12 9.314 12 6 C 12 2.686 9.314 0 6 0 C 2.686 0 0 2.686 0 6 C 0 9.314 2.686 12 6 12 Z M 6 11 C 3.239 11 1 8.761 1 6 C 1 3.239 3.239 1 6 1 C 8.761 1 11 3.239 11 6 C 11 8.761 8.761 11 6 11 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        height: 16,
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 1px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 12,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "16px",
          color: "var(--neutral-foreground-3-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-3-rest-3)",
          }}>
            <path d={"M 6 12 C 9.314 12 12 9.314 12 6 C 12 2.686 9.314 0 6 0 C 2.686 0 0 2.686 0 6 C 0 9.314 2.686 12 6 12 Z M 6 11 C 3.239 11 1 8.761 1 6 C 1 3.239 3.239 1 6 1 C 8.761 1 11 3.239 11 6 C 11 8.761 8.761 11 6 11 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body73 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      minWidth: 24,
      overflow: "hidden",
      borderRadius: 9999,
      boxShadow: "inset 0 0 0 1px var(--neutral-stroke-2-rest-3)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xxs) * 1px)",
      padding: "0px 4px 0px 4px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-3-rest-3)",
          }}>
            <path d={"M 6 12 C 9.314 12 12 9.314 12 6 C 12 2.686 9.314 0 6 0 C 2.686 0 0 2.686 0 6 C 0 9.314 2.686 12 6 12 Z M 6 11 C 3.239 11 1 8.761 1 6 C 1 3.239 3.239 1 6 1 C 8.761 1 11 3.239 11 6 C 11 8.761 8.761 11 6 11 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        height: 16,
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 1px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 12,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "16px",
          color: "var(--neutral-foreground-3-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-3-rest-3)",
          }}>
            <path d={"M 6 12 C 9.314 12 12 9.314 12 6 C 12 2.686 9.314 0 6 0 C 2.686 0 0 2.686 0 6 C 0 9.314 2.686 12 6 12 Z M 6 11 C 3.239 11 1 8.761 1 6 C 1 3.239 3.239 1 6 1 C 8.761 1 11 3.239 11 6 C 11 8.761 8.761 11 6 11 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body74 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      minWidth: 24,
      overflow: "hidden",
      borderRadius: 9999,
      boxShadow: "inset 0 0 0 1px var(--neutral-foreground-on-brand-rest-3)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xxs) * 1px)",
      padding: "0px 4px 0px 4px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-on-brand-rest-3)",
          }}>
            <path d={"M 6 12 C 9.314 12 12 9.314 12 6 C 12 2.686 9.314 0 6 0 C 2.686 0 0 2.686 0 6 C 0 9.314 2.686 12 6 12 Z M 6 11 C 3.239 11 1 8.761 1 6 C 1 3.239 3.239 1 6 1 C 8.761 1 11 3.239 11 6 C 11 8.761 8.761 11 6 11 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        height: 16,
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 1px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 12,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "16px",
          color: "var(--neutral-foreground-on-brand-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-on-brand-rest-3)",
          }}>
            <path d={"M 6 12 C 9.314 12 12 9.314 12 6 C 12 2.686 9.314 0 6 0 C 2.686 0 0 2.686 0 6 C 0 9.314 2.686 12 6 12 Z M 6 11 C 3.239 11 1 8.761 1 6 C 1 3.239 3.239 1 6 1 C 8.761 1 11 3.239 11 6 C 11 8.761 8.761 11 6 11 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body75 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      minWidth: 24,
      overflow: "hidden",
      borderRadius: 9999,
      boxShadow: "inset 0 0 0 1px var(--status-warning-foreground-2-rest-3)",
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xxs) * 1px)",
      padding: "0px 4px 0px 4px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--status-warning-foreground-2-rest-3)",
          }}>
            <path d={"M 6 12 C 9.314 12 12 9.314 12 6 C 12 2.686 9.314 0 6 0 C 2.686 0 0 2.686 0 6 C 0 9.314 2.686 12 6 12 Z M 6 11 C 3.239 11 1 8.761 1 6 C 1 3.239 3.239 1 6 1 C 8.761 1 11 3.239 11 6 C 11 8.761 8.761 11 6 11 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        height: 16,
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 1px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 12,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "16px",
          color: "var(--status-warning-foreground-2-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--status-warning-foreground-2-rest-3)",
          }}>
            <path d={"M 6 12 C 9.314 12 12 9.314 12 6 C 12 2.686 9.314 0 6 0 C 2.686 0 0 2.686 0 6 C 0 9.314 2.686 12 6 12 Z M 6 11 C 3.239 11 1 8.761 1 6 C 1 3.239 3.239 1 6 1 C 8.761 1 11 3.239 11 6 C 11 8.761 8.761 11 6 11 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body76 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      minWidth: 24,
      overflow: "hidden",
      borderRadius: 9999,
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xxs) * 1px)",
      padding: "0px 4px 0px 4px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--brand-foreground-1-rest-4)",
          }}>
            <path d={"M 6 12 C 9.314 12 12 9.314 12 6 C 12 2.686 9.314 0 6 0 C 2.686 0 0 2.686 0 6 C 0 9.314 2.686 12 6 12 Z M 6 11 C 3.239 11 1 8.761 1 6 C 1 3.239 3.239 1 6 1 C 8.761 1 11 3.239 11 6 C 11 8.761 8.761 11 6 11 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        height: 16,
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 1px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        paddingBottom: "calc(var(--typography-caption-1-strong-offset) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 12,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "16px",
          color: "var(--brand-foreground-1-rest-4)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--brand-foreground-1-rest-4)",
          }}>
            <path d={"M 6 12 C 9.314 12 12 9.314 12 6 C 12 2.686 9.314 0 6 0 C 2.686 0 0 2.686 0 6 C 0 9.314 2.686 12 6 12 Z M 6 11 C 3.239 11 1 8.761 1 6 C 1 3.239 3.239 1 6 1 C 8.761 1 11 3.239 11 6 C 11 8.761 8.761 11 6 11 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body77 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      minWidth: 24,
      overflow: "hidden",
      borderRadius: 9999,
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xxs) * 1px)",
      padding: "0px 4px 0px 4px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--status-danger-foreground-3-rest-3)",
          }}>
            <path d={"M 6 12 C 9.314 12 12 9.314 12 6 C 12 2.686 9.314 0 6 0 C 2.686 0 0 2.686 0 6 C 0 9.314 2.686 12 6 12 Z M 6 11 C 3.239 11 1 8.761 1 6 C 1 3.239 3.239 1 6 1 C 8.761 1 11 3.239 11 6 C 11 8.761 8.761 11 6 11 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        height: 16,
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 1px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 12,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "16px",
          color: "var(--status-danger-foreground-3-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--status-danger-foreground-3-rest-3)",
          }}>
            <path d={"M 6 12 C 9.314 12 12 9.314 12 6 C 12 2.686 9.314 0 6 0 C 2.686 0 0 2.686 0 6 C 0 9.314 2.686 12 6 12 Z M 6 11 C 3.239 11 1 8.761 1 6 C 1 3.239 3.239 1 6 1 C 8.761 1 11 3.239 11 6 C 11 8.761 8.761 11 6 11 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body78 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      minWidth: 24,
      overflow: "hidden",
      borderRadius: 9999,
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xxs) * 1px)",
      padding: "0px 4px 0px 4px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--status-success-foreground-3-rest-3)",
          }}>
            <path d={"M 6 12 C 9.314 12 12 9.314 12 6 C 12 2.686 9.314 0 6 0 C 2.686 0 0 2.686 0 6 C 0 9.314 2.686 12 6 12 Z M 6 11 C 3.239 11 1 8.761 1 6 C 1 3.239 3.239 1 6 1 C 8.761 1 11 3.239 11 6 C 11 8.761 8.761 11 6 11 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        height: 16,
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 1px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 12,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "16px",
          color: "var(--status-success-foreground-3-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--status-success-foreground-3-rest-3)",
          }}>
            <path d={"M 6 12 C 9.314 12 12 9.314 12 6 C 12 2.686 9.314 0 6 0 C 2.686 0 0 2.686 0 6 C 0 9.314 2.686 12 6 12 Z M 6 11 C 3.239 11 1 8.761 1 6 C 1 3.239 3.239 1 6 1 C 8.761 1 11 3.239 11 6 C 11 8.761 8.761 11 6 11 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body79 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      minWidth: 24,
      overflow: "hidden",
      borderRadius: 9999,
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xxs) * 1px)",
      padding: "0px 4px 0px 4px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-3-rest-3)",
          }}>
            <path d={"M 6 12 C 9.314 12 12 9.314 12 6 C 12 2.686 9.314 0 6 0 C 2.686 0 0 2.686 0 6 C 0 9.314 2.686 12 6 12 Z M 6 11 C 3.239 11 1 8.761 1 6 C 1 3.239 3.239 1 6 1 C 8.761 1 11 3.239 11 6 C 11 8.761 8.761 11 6 11 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        height: 16,
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 1px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 12,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "16px",
          color: "var(--neutral-foreground-3-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-3-rest-3)",
          }}>
            <path d={"M 6 12 C 9.314 12 12 9.314 12 6 C 12 2.686 9.314 0 6 0 C 2.686 0 0 2.686 0 6 C 0 9.314 2.686 12 6 12 Z M 6 11 C 3.239 11 1 8.761 1 6 C 1 3.239 3.239 1 6 1 C 8.761 1 11 3.239 11 6 C 11 8.761 8.761 11 6 11 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body80 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      minWidth: 24,
      overflow: "hidden",
      borderRadius: 9999,
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xxs) * 1px)",
      padding: "0px 4px 0px 4px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-on-brand-rest-3)",
          }}>
            <path d={"M 6 12 C 9.314 12 12 9.314 12 6 C 12 2.686 9.314 0 6 0 C 2.686 0 0 2.686 0 6 C 0 9.314 2.686 12 6 12 Z M 6 11 C 3.239 11 1 8.761 1 6 C 1 3.239 3.239 1 6 1 C 8.761 1 11 3.239 11 6 C 11 8.761 8.761 11 6 11 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        height: 16,
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 1px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 12,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "16px",
          color: "var(--neutral-foreground-on-brand-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-on-brand-rest-3)",
          }}>
            <path d={"M 6 12 C 9.314 12 12 9.314 12 6 C 12 2.686 9.314 0 6 0 C 2.686 0 0 2.686 0 6 C 0 9.314 2.686 12 6 12 Z M 6 11 C 3.239 11 1 8.761 1 6 C 1 3.239 3.239 1 6 1 C 8.761 1 11 3.239 11 6 C 11 8.761 8.761 11 6 11 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body81 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 24,
      minWidth: 24,
      overflow: "hidden",
      borderRadius: 9999,
      display: "flex",
      flexDirection: "row",
      gap: "calc(var(--spacing-horizontal-xxs) * 1px)",
      padding: "0px 4px 0px 4px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--status-warning-foreground-2-rest-3)",
          }}>
            <path d={"M 6 12 C 9.314 12 12 9.314 12 6 C 12 2.686 9.314 0 6 0 C 2.686 0 0 2.686 0 6 C 0 9.314 2.686 12 6 12 Z M 6 11 C 3.239 11 1 8.761 1 6 C 1 3.239 3.239 1 6 1 C 8.761 1 11 3.239 11 6 C 11 8.761 8.761 11 6 11 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        height: 16,
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 1px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 12,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "16px",
          color: "var(--status-warning-foreground-2-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--status-warning-foreground-2-rest-3)",
          }}>
            <path d={"M 6 12 C 9.314 12 12 9.314 12 6 C 12 2.686 9.314 0 6 0 C 2.686 0 0 2.686 0 6 C 0 9.314 2.686 12 6 12 Z M 6 11 C 3.239 11 1 8.761 1 6 C 1 3.239 3.239 1 6 1 C 8.761 1 11 3.239 11 6 C 11 8.761 8.761 11 6 11 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body82 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 20,
      minWidth: 20,
      overflow: "hidden",
      borderRadius: 9999,
      boxShadow: "inset 0 0 0 1px var(--brand-stroke-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 4px 0px 4px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none) * 1px)",
      paddingLeft: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--brand-foreground-1-rest-4)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        height: 14,
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 0.500px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        paddingBottom: "calc(var(--typography-caption-2-strong-offset) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 10,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "14px",
          color: "var(--brand-foreground-1-rest-4)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--brand-foreground-1-rest-4)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body83 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 20,
      minWidth: 20,
      overflow: "hidden",
      borderRadius: 9999,
      boxShadow: "inset 0 0 0 1px var(--status-danger-stroke-2-rest-3)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 4px 0px 4px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none) * 1px)",
      paddingLeft: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--status-danger-foreground-3-rest-3)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        height: 14,
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 0.500px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 10,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "14px",
          color: "var(--status-danger-foreground-3-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--status-danger-foreground-3-rest-3)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body84 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 20,
      minWidth: 20,
      overflow: "hidden",
      borderRadius: 9999,
      boxShadow: "inset 0 0 0 1px var(--status-success-stroke-2-rest-3)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 4px 0px 4px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none) * 1px)",
      paddingLeft: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--status-success-foreground-3-rest-3)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        height: 14,
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 0.500px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 10,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "14px",
          color: "var(--status-success-foreground-3-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--status-success-foreground-3-rest-3)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body85 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 20,
      minWidth: 20,
      overflow: "hidden",
      borderRadius: 9999,
      boxShadow: "inset 0 0 0 1px var(--neutral-stroke-accessible-rest-3)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 4px 0px 4px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none) * 1px)",
      paddingLeft: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--neutral-foreground-3-rest-3)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        height: 14,
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 0.500px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 10,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "14px",
          color: "var(--neutral-foreground-3-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--neutral-foreground-3-rest-3)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body86 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 20,
      minWidth: 20,
      overflow: "hidden",
      borderRadius: 9999,
      boxShadow: "inset 0 0 0 1px var(--neutral-stroke-2-rest-3)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 4px 0px 4px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none) * 1px)",
      paddingLeft: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--neutral-foreground-3-rest-3)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        height: 14,
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 0.500px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 10,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "14px",
          color: "var(--neutral-foreground-3-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--neutral-foreground-3-rest-3)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body87 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 20,
      minWidth: 20,
      overflow: "hidden",
      borderRadius: 9999,
      boxShadow: "inset 0 0 0 1px var(--neutral-foreground-on-brand-rest-3)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 4px 0px 4px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none) * 1px)",
      paddingLeft: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--neutral-foreground-on-brand-rest-3)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        height: 14,
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 0.500px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 10,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "14px",
          color: "var(--neutral-foreground-on-brand-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--neutral-foreground-on-brand-rest-3)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body88 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 20,
      minWidth: 20,
      overflow: "hidden",
      borderRadius: 9999,
      boxShadow: "inset 0 0 0 1px var(--status-warning-foreground-2-rest-3)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 4px 0px 4px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none) * 1px)",
      paddingLeft: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--status-warning-foreground-2-rest-3)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        height: 14,
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 0.500px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 10,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "14px",
          color: "var(--status-warning-foreground-2-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--status-warning-foreground-2-rest-3)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body89 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 20,
      minWidth: 20,
      overflow: "hidden",
      borderRadius: 9999,
      display: "flex",
      flexDirection: "row",
      padding: "0px 4px 0px 4px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none) * 1px)",
      paddingLeft: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--brand-foreground-1-rest-4)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        height: 14,
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 0.500px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        paddingBottom: "calc(var(--typography-caption-2-strong-offset) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 10,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "14px",
          color: "var(--brand-foreground-1-rest-4)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--brand-foreground-1-rest-4)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body90 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 20,
      minWidth: 20,
      overflow: "hidden",
      borderRadius: 9999,
      display: "flex",
      flexDirection: "row",
      padding: "0px 4px 0px 4px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none) * 1px)",
      paddingLeft: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--status-danger-foreground-3-rest-3)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        height: 14,
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 0.500px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 10,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "14px",
          color: "var(--status-danger-foreground-3-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--status-danger-foreground-3-rest-3)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body91 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 20,
      minWidth: 20,
      overflow: "hidden",
      borderRadius: 9999,
      display: "flex",
      flexDirection: "row",
      padding: "0px 4px 0px 4px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none) * 1px)",
      paddingLeft: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--status-success-foreground-3-rest-3)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        height: 14,
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 0.500px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 10,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "14px",
          color: "var(--status-success-foreground-3-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--status-success-foreground-3-rest-3)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body92 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 20,
      minWidth: 20,
      overflow: "hidden",
      borderRadius: 9999,
      display: "flex",
      flexDirection: "row",
      padding: "0px 4px 0px 4px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none) * 1px)",
      paddingLeft: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--neutral-foreground-3-rest-3)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        height: 14,
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 0.500px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 10,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "14px",
          color: "var(--neutral-foreground-3-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--neutral-foreground-3-rest-3)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body93 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 20,
      minWidth: 20,
      overflow: "hidden",
      borderRadius: 9999,
      display: "flex",
      flexDirection: "row",
      padding: "0px 4px 0px 4px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none) * 1px)",
      paddingLeft: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--neutral-foreground-on-brand-rest-3)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        height: 14,
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 0.500px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 10,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "14px",
          color: "var(--neutral-foreground-on-brand-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--neutral-foreground-on-brand-rest-3)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body94 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 20,
      minWidth: 20,
      overflow: "hidden",
      borderRadius: 9999,
      display: "flex",
      flexDirection: "row",
      padding: "0px 4px 0px 4px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none) * 1px)",
      paddingLeft: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xs) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--status-warning-foreground-2-rest-3)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        height: 14,
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 0.500px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        flexShrink: 0,
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 10,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "14px",
          color: "var(--status-warning-foreground-2-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--status-warning-foreground-2-rest-3)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body95 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 16,
      minWidth: 16,
      overflow: "hidden",
      borderRadius: 9999,
      boxShadow: "inset 0 0 0 1px var(--brand-stroke-1-rest-3)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 2px 0px 2px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-xxs) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xxs) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--brand-foreground-1-rest-4)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 0.500px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        paddingBottom: "calc(var(--typography-caption-2-strong-offset) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 10,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "14px",
          color: "var(--brand-foreground-1-rest-4)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--brand-foreground-1-rest-4)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body96 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 16,
      minWidth: 16,
      overflow: "hidden",
      borderRadius: 9999,
      boxShadow: "inset 0 0 0 1px var(--status-danger-stroke-2-rest-3)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 2px 0px 2px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none) * 1px)",
      paddingLeft: "calc(var(--spacing-horizontal-xxs) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xxs) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--status-danger-foreground-3-rest-3)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 0.500px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 10,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "14px",
          color: "var(--status-danger-foreground-3-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--status-danger-foreground-3-rest-3)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body97 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 16,
      minWidth: 16,
      overflow: "hidden",
      borderRadius: 9999,
      boxShadow: "inset 0 0 0 1px var(--status-success-stroke-2-rest-3)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 2px 0px 2px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none) * 1px)",
      paddingLeft: "calc(var(--spacing-horizontal-xxs) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xxs) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--status-success-foreground-3-rest-3)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 0.500px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 10,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "14px",
          color: "var(--status-success-foreground-3-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--status-success-foreground-3-rest-3)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body98 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 16,
      minWidth: 16,
      overflow: "hidden",
      borderRadius: 9999,
      boxShadow: "inset 0 0 0 1px var(--neutral-stroke-accessible-rest-3)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 2px 0px 2px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none) * 1px)",
      paddingLeft: "calc(var(--spacing-horizontal-xxs) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xxs) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--neutral-foreground-3-rest-3)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 0.500px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 10,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "14px",
          color: "var(--neutral-foreground-3-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--neutral-foreground-3-rest-3)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body99 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 16,
      minWidth: 16,
      overflow: "hidden",
      borderRadius: 9999,
      boxShadow: "inset 0 0 0 1px var(--neutral-stroke-2-rest-3)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 2px 0px 2px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none) * 1px)",
      paddingLeft: "calc(var(--spacing-horizontal-xxs) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xxs) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--neutral-foreground-3-rest-3)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 0.500px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 10,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "14px",
          color: "var(--neutral-foreground-3-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--neutral-foreground-3-rest-3)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body100 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 16,
      minWidth: 16,
      overflow: "hidden",
      borderRadius: 9999,
      boxShadow: "inset 0 0 0 1px var(--neutral-foreground-on-brand-rest-3)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 2px 0px 2px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none) * 1px)",
      paddingLeft: "calc(var(--spacing-horizontal-xxs) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xxs) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--neutral-foreground-on-brand-rest-3)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 0.500px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 10,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "14px",
          color: "var(--neutral-foreground-on-brand-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--neutral-foreground-on-brand-rest-3)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body101 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 16,
      minWidth: 16,
      overflow: "hidden",
      borderRadius: 9999,
      boxShadow: "inset 0 0 0 1px var(--status-warning-foreground-2-rest-3)",
      display: "flex",
      flexDirection: "row",
      padding: "0px 2px 0px 2px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none) * 1px)",
      paddingLeft: "calc(var(--spacing-horizontal-xxs) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xxs) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--status-warning-foreground-2-rest-3)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 0.500px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 10,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "14px",
          color: "var(--status-warning-foreground-2-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--status-warning-foreground-2-rest-3)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body102 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 16,
      minWidth: 16,
      overflow: "hidden",
      borderRadius: 9999,
      display: "flex",
      flexDirection: "row",
      padding: "0px 2px 0px 2px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      paddingLeft: "calc(var(--spacing-horizontal-xxs) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xxs) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--brand-foreground-1-rest-4)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 0.500px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        paddingBottom: "calc(var(--typography-caption-2-strong-offset) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 10,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "14px",
          color: "var(--brand-foreground-1-rest-4)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--brand-foreground-1-rest-4)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body103 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 16,
      minWidth: 16,
      overflow: "hidden",
      borderRadius: 9999,
      display: "flex",
      flexDirection: "row",
      padding: "0px 2px 0px 2px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none) * 1px)",
      paddingLeft: "calc(var(--spacing-horizontal-xxs) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xxs) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--status-danger-foreground-3-rest-3)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 0.500px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 10,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "14px",
          color: "var(--status-danger-foreground-3-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--status-danger-foreground-3-rest-3)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body104 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 16,
      minWidth: 16,
      overflow: "hidden",
      borderRadius: 9999,
      display: "flex",
      flexDirection: "row",
      padding: "0px 2px 0px 2px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none) * 1px)",
      paddingLeft: "calc(var(--spacing-horizontal-xxs) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xxs) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--status-success-foreground-3-rest-3)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 0.500px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 10,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "14px",
          color: "var(--status-success-foreground-3-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--status-success-foreground-3-rest-3)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body105 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 16,
      minWidth: 16,
      overflow: "hidden",
      borderRadius: 9999,
      display: "flex",
      flexDirection: "row",
      padding: "0px 2px 0px 2px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none) * 1px)",
      paddingLeft: "calc(var(--spacing-horizontal-xxs) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xxs) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--neutral-foreground-3-rest-3)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 0.500px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 10,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "14px",
          color: "var(--neutral-foreground-3-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--neutral-foreground-3-rest-3)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body106 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 16,
      minWidth: 16,
      overflow: "hidden",
      borderRadius: 9999,
      display: "flex",
      flexDirection: "row",
      padding: "0px 2px 0px 2px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none) * 1px)",
      paddingLeft: "calc(var(--spacing-horizontal-xxs) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xxs) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--neutral-foreground-on-brand-rest-3)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 0.500px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 10,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "14px",
          color: "var(--neutral-foreground-on-brand-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--neutral-foreground-on-brand-rest-3)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __body107 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      height: 16,
      minWidth: 16,
      overflow: "hidden",
      borderRadius: 9999,
      display: "flex",
      flexDirection: "row",
      padding: "0px 2px 0px 2px",
      justifyContent: "center",
      alignItems: "center",
      flexWrap: "nowrap",
      boxSizing: "border-box",
      gap: "calc(var(--spacing-horizontal-none) * 1px)",
      paddingLeft: "calc(var(--spacing-horizontal-xxs) * 1px)",
      paddingTop: "calc(var(--spacing-vertical-none) * 1px)",
      paddingRight: "calc(var(--spacing-horizontal-xxs) * 1px)",
      paddingBottom: "calc(var(--spacing-vertical-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      {props.leftIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--status-warning-foreground-2-rest-3)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
      {props.showText && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        padding: "0px 2px 0.500px 2px",
        justifyContent: "flex-end",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <span style={{
          position: "relative",
          fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
          fontWeight: 600,
          fontSize: 10,
          textAlign: "center",
          whiteSpace: "nowrap",
          lineHeight: "14px",
          color: "var(--status-warning-foreground-2-rest-3)",
          flexShrink: 0,
        }}>{props.text}</span>
      </div>
      )}
      {props.rightIcon && (
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--spacing-horizontal-mnudge-2) * 1px)",
        alignItems: "center",
        flexWrap: "nowrap",
        flexShrink: 0,
      }}>
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: 10,
          padding: "2px 2px 2px 2px",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          boxSizing: "border-box",
          flexShrink: 0,
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--status-warning-foreground-2-rest-3)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
          </svg>
        </div>
      </div>
      )}
    </div>
  );
  const __impls = {
    // figma: Color=Brand, Size=Extra large, Appearance=Filled
    "color=brand|size=extra large|appearance=filled": __body0,
    // figma: Color=Danger, Size=Extra large, Appearance=Filled
    "color=danger|size=extra large|appearance=filled": __body1,
    // figma: Color=Success, Size=Extra large, Appearance=Filled
    "color=success|size=extra large|appearance=filled": __body2,
    // figma: Color=Important, Size=Extra large, Appearance=Filled
    "color=important|size=extra large|appearance=filled": __body3,
    // figma: Color=Informative, Size=Extra large, Appearance=Filled
    "color=informative|size=extra large|appearance=filled": __body4,
    // figma: Color=Subtle, Size=Extra large, Appearance=Filled
    "color=subtle|size=extra large|appearance=filled": __body5,
    // figma: Color=Warning, Size=Extra large, Appearance=Filled
    "color=warning|size=extra large|appearance=filled": __body6,
    // figma: Color=Brand, Size=Large, Appearance=Filled
    "color=brand|size=lg|appearance=filled": __body7,
    // figma: Color=Danger, Size=Large, Appearance=Filled
    "color=danger|size=lg|appearance=filled": __body8,
    // figma: Color=Success, Size=Large, Appearance=Filled
    "color=success|size=lg|appearance=filled": __body9,
    // figma: Color=Important, Size=Large, Appearance=Filled
    "color=important|size=lg|appearance=filled": __body10,
    // figma: Color=Informative, Size=Large, Appearance=Filled
    "color=informative|size=lg|appearance=filled": __body11,
    // figma: Color=Subtle, Size=Large, Appearance=Filled
    "color=subtle|size=lg|appearance=filled": __body12,
    // figma: Color=Warning, Size=Large, Appearance=Filled
    "color=warning|size=lg|appearance=filled": __body13,
    // figma: Color=Brand, Size=Medium, Appearance=Filled
    "color=brand|size=md|appearance=filled": __body14,
    // figma: Color=Danger, Size=Medium, Appearance=Filled
    "color=danger|size=md|appearance=filled": __body15,
    // figma: Color=Success, Size=Medium, Appearance=Filled
    "color=success|size=md|appearance=filled": __body16,
    // figma: Color=Important, Size=Medium, Appearance=Filled
    "color=important|size=md|appearance=filled": __body17,
    // figma: Color=Informative, Size=Medium, Appearance=Filled
    "color=informative|size=md|appearance=filled": __body18,
    // figma: Color=Subtle, Size=Medium, Appearance=Filled
    "color=subtle|size=md|appearance=filled": __body19,
    // figma: Color=Warning, Size=Medium, Appearance=Filled
    "color=warning|size=md|appearance=filled": __body20,
    // figma: Color=Brand, Size=Small, Appearance=Filled
    "color=brand|size=sm|appearance=filled": __body21,
    // figma: Color=Danger, Size=Small, Appearance=Filled
    "color=danger|size=sm|appearance=filled": __body22,
    // figma: Color=Success, Size=Small, Appearance=Filled
    "color=success|size=sm|appearance=filled": __body23,
    // figma: Color=Important, Size=Small, Appearance=Filled
    "color=important|size=sm|appearance=filled": __body24,
    // figma: Color=Informative, Size=Small, Appearance=Filled
    "color=informative|size=sm|appearance=filled": __body25,
    // figma: Color=Subtle, Size=Small, Appearance=Filled
    "color=subtle|size=sm|appearance=filled": __body26,
    // figma: Color=Warning, Size=Small, Appearance=Filled
    "color=warning|size=sm|appearance=filled": __body27,
    // figma: Color=Brand, Size=Extra large, Appearance=Tint
    "color=brand|size=extra large|appearance=tint": __body28,
    // figma: Color=Danger, Size=Extra large, Appearance=Tint
    "color=danger|size=extra large|appearance=tint": __body29,
    // figma: Color=Success, Size=Extra large, Appearance=Tint
    "color=success|size=extra large|appearance=tint": __body30,
    // figma: Color=Important, Size=Extra large, Appearance=Tint
    "color=important|size=extra large|appearance=tint": __body31,
    // figma: Color=Informative, Size=Extra large, Appearance=Tint
    "color=informative|size=extra large|appearance=tint": __body32,
    // figma: Color=Subtle, Size=Extra large, Appearance=Tint
    "color=subtle|size=extra large|appearance=tint": __body33,
    // figma: Color=Warning, Size=Extra large, Appearance=Tint
    "color=warning|size=extra large|appearance=tint": __body34,
    // figma: Color=Brand, Size=Large, Appearance=Tint
    "color=brand|size=lg|appearance=tint": __body35,
    // figma: Color=Danger, Size=Large, Appearance=Tint
    "color=danger|size=lg|appearance=tint": __body36,
    // figma: Color=Success, Size=Large, Appearance=Tint
    "color=success|size=lg|appearance=tint": __body37,
    // figma: Color=Important, Size=Large, Appearance=Tint
    "color=important|size=lg|appearance=tint": __body38,
    // figma: Color=Informative, Size=Large, Appearance=Tint
    "color=informative|size=lg|appearance=tint": __body39,
    // figma: Color=Subtle, Size=Large, Appearance=Tint
    "color=subtle|size=lg|appearance=tint": __body40,
    // figma: Color=Warning, Size=Large, Appearance=Tint
    "color=warning|size=lg|appearance=tint": __body41,
    // figma: Color=Brand, Size=Medium, Appearance=Tint
    "color=brand|size=md|appearance=tint": __body42,
    // figma: Color=Danger, Size=Medium, Appearance=Tint
    "color=danger|size=md|appearance=tint": __body43,
    // figma: Color=Success, Size=Medium, Appearance=Tint
    "color=success|size=md|appearance=tint": __body44,
    // figma: Color=Important, Size=Medium, Appearance=Tint
    "color=important|size=md|appearance=tint": __body45,
    // figma: Color=Informative, Size=Medium, Appearance=Tint
    "color=informative|size=md|appearance=tint": __body46,
    // figma: Color=Subtle, Size=Medium, Appearance=Tint
    "color=subtle|size=md|appearance=tint": __body47,
    // figma: Color=Warning, Size=Medium, Appearance=Tint
    "color=warning|size=md|appearance=tint": __body48,
    // figma: Color=Brand, Size=Small, Appearance=Tint
    "color=brand|size=sm|appearance=tint": __body49,
    // figma: Color=Danger, Size=Small, Appearance=Tint
    "color=danger|size=sm|appearance=tint": __body50,
    // figma: Color=Success, Size=Small, Appearance=Tint
    "color=success|size=sm|appearance=tint": __body51,
    // figma: Color=Important, Size=Small, Appearance=Tint
    "color=important|size=sm|appearance=tint": __body52,
    // figma: Color=Informative, Size=Small, Appearance=Tint
    "color=informative|size=sm|appearance=tint": __body53,
    // figma: Color=Subtle, Size=Small, Appearance=Tint
    "color=subtle|size=sm|appearance=tint": __body54,
    // figma: Color=Warning, Size=Small, Appearance=Tint
    "color=warning|size=sm|appearance=tint": __body55,
    // figma: Color=Brand, Size=Extra large, Appearance=Outline
    "color=brand|size=extra large|appearance=outline": __body56,
    // figma: Color=Danger, Size=Extra large, Appearance=Outline
    "color=danger|size=extra large|appearance=outline": __body57,
    // figma: Color=Success, Size=Extra large, Appearance=Outline
    "color=success|size=extra large|appearance=outline": __body58,
    // figma: Color=Important, Size=Extra large, Appearance=Outline
    "color=important|size=extra large|appearance=outline": __body59,
    // figma: Color=Informative, Size=Extra large, Appearance=Outline
    "color=informative|size=extra large|appearance=outline": __body60,
    // figma: Color=Subtle, Size=Extra large, Appearance=Outline
    "color=subtle|size=extra large|appearance=outline": __body61,
    // figma: Color=Warning, Size=Extra large, Appearance=Outline
    "color=warning|size=extra large|appearance=outline": __body62,
    // figma: Color=Brand, Size=Extra large, Appearance=Subtle
    "color=brand|size=extra large|appearance=subtle": __body63,
    // figma: Color=Danger, Size=Extra large, Appearance=Subtle
    "color=danger|size=extra large|appearance=subtle": __body64,
    // figma: Color=Success, Size=Extra large, Appearance=Subtle
    "color=success|size=extra large|appearance=subtle": __body65,
    // figma: Color=Important, Size=Extra large, Appearance=Subtle
    "color=important|size=extra large|appearance=subtle": __body66,
    // figma: Color=Informative, Size=Extra large, Appearance=Subtle
    "color=informative|size=extra large|appearance=subtle": __body66,
    // figma: Color=Subtle, Size=Extra large, Appearance=Subtle
    "color=subtle|size=extra large|appearance=subtle": __body67,
    // figma: Color=Warning, Size=Extra large, Appearance=Subtle
    "color=warning|size=extra large|appearance=subtle": __body68,
    // figma: Color=Brand, Size=Large, Appearance=Outline
    "color=brand|size=lg|appearance=outline": __body69,
    // figma: Color=Danger, Size=Large, Appearance=Outline
    "color=danger|size=lg|appearance=outline": __body70,
    // figma: Color=Success, Size=Large, Appearance=Outline
    "color=success|size=lg|appearance=outline": __body71,
    // figma: Color=Important, Size=Large, Appearance=Outline
    "color=important|size=lg|appearance=outline": __body72,
    // figma: Color=Informative, Size=Large, Appearance=Outline
    "color=informative|size=lg|appearance=outline": __body73,
    // figma: Color=Subtle, Size=Large, Appearance=Outline
    "color=subtle|size=lg|appearance=outline": __body74,
    // figma: Color=Warning, Size=Large, Appearance=Outline
    "color=warning|size=lg|appearance=outline": __body75,
    // figma: Color=Brand, Size=Large, Appearance=Subtle
    "color=brand|size=lg|appearance=subtle": __body76,
    // figma: Color=Danger, Size=Large, Appearance=Subtle
    "color=danger|size=lg|appearance=subtle": __body77,
    // figma: Color=Success, Size=Large, Appearance=Subtle
    "color=success|size=lg|appearance=subtle": __body78,
    // figma: Color=Important, Size=Large, Appearance=Subtle
    "color=important|size=lg|appearance=subtle": __body79,
    // figma: Color=Informative, Size=Large, Appearance=Subtle
    "color=informative|size=lg|appearance=subtle": __body79,
    // figma: Color=Subtle, Size=Large, Appearance=Subtle
    "color=subtle|size=lg|appearance=subtle": __body80,
    // figma: Color=Warning, Size=Large, Appearance=Subtle
    "color=warning|size=lg|appearance=subtle": __body81,
    // figma: Color=Brand, Size=Medium, Appearance=Outline
    "color=brand|size=md|appearance=outline": __body82,
    // figma: Color=Danger, Size=Medium, Appearance=Outline
    "color=danger|size=md|appearance=outline": __body83,
    // figma: Color=Success, Size=Medium, Appearance=Outline
    "color=success|size=md|appearance=outline": __body84,
    // figma: Color=Important, Size=Medium, Appearance=Outline
    "color=important|size=md|appearance=outline": __body85,
    // figma: Color=Informative, Size=Medium, Appearance=Outline
    "color=informative|size=md|appearance=outline": __body86,
    // figma: Color=Subtle, Size=Medium, Appearance=Outline
    "color=subtle|size=md|appearance=outline": __body87,
    // figma: Color=Warning, Size=Medium, Appearance=Outline
    "color=warning|size=md|appearance=outline": __body88,
    // figma: Color=Brand, Size=Medium, Appearance=Subtle
    "color=brand|size=md|appearance=subtle": __body89,
    // figma: Color=Danger, Size=Medium, Appearance=Subtle
    "color=danger|size=md|appearance=subtle": __body90,
    // figma: Color=Success, Size=Medium, Appearance=Subtle
    "color=success|size=md|appearance=subtle": __body91,
    // figma: Color=Important, Size=Medium, Appearance=Subtle
    "color=important|size=md|appearance=subtle": __body92,
    // figma: Color=Informative, Size=Medium, Appearance=Subtle
    "color=informative|size=md|appearance=subtle": __body92,
    // figma: Color=Subtle, Size=Medium, Appearance=Subtle
    "color=subtle|size=md|appearance=subtle": __body93,
    // figma: Color=Warning, Size=Medium, Appearance=Subtle
    "color=warning|size=md|appearance=subtle": __body94,
    // figma: Color=Brand, Size=Small, Appearance=Outline
    "color=brand|size=sm|appearance=outline": __body95,
    // figma: Color=Danger, Size=Small, Appearance=Outline
    "color=danger|size=sm|appearance=outline": __body96,
    // figma: Color=Success, Size=Small, Appearance=Outline
    "color=success|size=sm|appearance=outline": __body97,
    // figma: Color=Important, Size=Small, Appearance=Outline
    "color=important|size=sm|appearance=outline": __body98,
    // figma: Color=Informative, Size=Small, Appearance=Outline
    "color=informative|size=sm|appearance=outline": __body99,
    // figma: Color=Subtle, Size=Small, Appearance=Outline
    "color=subtle|size=sm|appearance=outline": __body100,
    // figma: Color=Warning, Size=Small, Appearance=Outline
    "color=warning|size=sm|appearance=outline": __body101,
    // figma: Color=Brand, Size=Small, Appearance=Subtle
    "color=brand|size=sm|appearance=subtle": __body102,
    // figma: Color=Danger, Size=Small, Appearance=Subtle
    "color=danger|size=sm|appearance=subtle": __body103,
    // figma: Color=Success, Size=Small, Appearance=Subtle
    "color=success|size=sm|appearance=subtle": __body104,
    // figma: Color=Important, Size=Small, Appearance=Subtle
    "color=important|size=sm|appearance=subtle": __body105,
    // figma: Color=Informative, Size=Small, Appearance=Subtle
    "color=informative|size=sm|appearance=subtle": __body105,
    // figma: Color=Subtle, Size=Small, Appearance=Subtle
    "color=subtle|size=sm|appearance=subtle": __body106,
    // figma: Color=Warning, Size=Small, Appearance=Subtle
    "color=warning|size=sm|appearance=subtle": __body107,
  };
  return (__impls[__vkey(props)] ?? __body0)();
}
export default Badge;
