import { __slateNorm } from '../internal/slateProps.jsx';
import { Chevron } from '../internal/Chevron.jsx';

// figma node: 9077:5740 .Breadcrumb Item (51 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "size=" + __venc(p.size) + '|' + "type=" + __venc(p.type) + '|' + "states=" + __venc(p.states) + '|' + "icon=" + __venc(p.icon);

export function BreadcrumbItem(_p = {}) {
  _p = __slateNorm(_p, {"size":["lg","md","sm"],"type":["button","overflow"],"states":["rest","hover","pressed","focused","disabled","current"],"icon":["on","off"]});
  const props = { ..._p, size: _p.size ?? "lg", type: _p.type ?? "button", states: _p.states ?? "rest", icon: _p.icon ?? "on", divider: _p.divider ?? true };
  const __body0 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      gap: "calc(var(--horizontal-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 64,
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
        paddingLeft: "calc(var(--horizontal-xs) * 1px)",
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
        }}>
          <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
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
              fontSize: 16,
              whiteSpace: "nowrap",
              lineHeight: "22px",
              color: "var(--neutral-foreground-2-rest)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>{props.label ?? "Text"}</span>
          </div>
        </div>
      </div>
      {props.divider && (
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
            width: 20,
            height: 20,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.icon2 ?? <Chevron direction={"right"} size={"20"} theme={"regular"} />}</div>
      </div>
      )}
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      gap: "calc(var(--horizontal-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 64,
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-subtle-hover)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--horizontal-xs) * 1px)",
        padding: "8px 16px 8px 16px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xs) * 1px)",
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
        }}>
          <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-brand-hover)",
          }}>
            <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z"} fill="currentColor" fillRule="evenodd" />
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
            }}>{props.label ?? "Text"}</span>
          </div>
        </div>
      </div>
      {props.divider && (
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
            width: 20,
            height: 20,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.icon2 ?? <Chevron direction={"right"} size={"20"} theme={"regular"} />}</div>
      </div>
      )}
    </div>
  );
  const __body2 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      gap: "calc(var(--horizontal-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 64,
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-subtle-pressed)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--horizontal-xs) * 1px)",
        padding: "8px 16px 8px 16px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xs) * 1px)",
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
        }}>
          <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-brand-pressed)",
          }}>
            <path d={"M 8 16 C 12.418 16 16 12.418 16 8 C 16 3.582 12.418 0 8 0 C 3.582 0 0 3.582 0 8 C 0 12.418 3.582 16 8 16 Z"} fill="currentColor" fillRule="evenodd" />
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
              color: "var(--neutral-foreground-1-pressed)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>{props.label ?? "Text"}</span>
          </div>
        </div>
      </div>
      {props.divider && (
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
            width: 20,
            height: 20,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.icon2 ?? <Chevron direction={"right"} size={"20"} theme={"regular"} />}</div>
      </div>
      )}
    </div>
  );
  const __body3 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      gap: "calc(var(--horizontal-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 64,
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
        paddingLeft: "calc(var(--horizontal-xs) * 1px)",
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
        }}>
          <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
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
              fontSize: 16,
              whiteSpace: "nowrap",
              lineHeight: "22px",
              color: "var(--neutral-foreground-2-rest)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>{props.label ?? "Text"}</span>
          </div>
        </div>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 64,
          height: 40,
          overflow: "hidden",
          borderRadius: 4,
          borderTop: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderRight: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderBottom: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderLeft: "2px solid var(--neutral-stroke-focus-2-rest)",
        }}>
          <svg width={64} height={40} viewBox="0 0 64 40" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 64,
            height: 40,
            overflow: "hidden",
            borderRadius: 4,
            borderTop: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderRight: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderBottom: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderLeft: "1px solid var(--neutral-stroke-focus-1-rest)",
          }}>
            <path d={"M 4 0 L 4 1 L 60 1 L 60 0 L 60 -1 L 4 -1 L 4 0 Z M 64 4 L 63 4 L 63 36 L 64 36 L 65 36 L 65 4 L 64 4 Z M 60 40 L 60 39 L 4 39 L 4 40 L 4 41 L 60 41 L 60 40 Z M 0 36 L 1 36 L 1 4 L 0 4 L -1 4 L -1 36 L 0 36 Z M 4 40 L 4 39 C 2.343 39 1 37.657 1 36 L 0 36 L -1 36 C -1 38.761 1.239 41 4 41 L 4 40 Z M 64 36 L 63 36 C 63 37.657 61.657 39 60 39 L 60 40 L 60 41 C 62.761 41 65 38.761 65 36 L 64 36 Z M 60 0 L 60 1 C 61.657 1 63 2.343 63 4 L 64 4 L 65 4 C 65 1.239 62.761 -1 60 -1 L 60 0 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      {props.divider && (
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
            width: 20,
            height: 20,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.icon2 ?? <Chevron direction={"right"} size={"20"} theme={"regular"} />}</div>
      </div>
      )}
    </div>
  );
  const __body4 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      gap: "calc(var(--horizontal-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 64,
        borderRadius: 4,
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--horizontal-xs) * 1px)",
        padding: "8px 16px 8px 16px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xs) * 1px)",
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
        }}>
          <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--neutral-foreground-disabled-rest)",
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
              fontSize: 16,
              whiteSpace: "nowrap",
              lineHeight: "22px",
              color: "var(--neutral-foreground-disabled-rest)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>{props.label ?? "Text"}</span>
          </div>
        </div>
      </div>
      {props.divider && (
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
            width: 20,
            height: 20,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.icon2 ?? <Chevron direction={"right"} size={"20"} theme={"regular"} />}</div>
      </div>
      )}
    </div>
  );
  const __body5 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      gap: "calc(var(--horizontal-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 66,
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
        paddingLeft: "calc(var(--horizontal-xs) * 1px)",
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
        }}>
          <svg width={16} height={16} viewBox="0 0 16 16" fill="none" style={{
            position: "relative",
            width: 16,
            height: 16,
            flexShrink: 0,
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
              fontSize: 16,
              whiteSpace: "nowrap",
              lineHeight: "22px",
              color: "var(--neutral-foreground-2-rest)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>{props.label ?? "Text"}</span>
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
      gap: "calc(var(--horizontal-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 51,
        borderRadius: 4,
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--horizontal-xxs) * 1px)",
        padding: "6px 12px 6px 12px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
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
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest)",
          }}>
            <path d={"M 6 12 C 9.314 12 12 9.314 12 6 C 12 2.686 9.314 0 6 0 C 2.686 0 0 2.686 0 6 C 0 9.314 2.686 12 6 12 Z M 6 11 C 3.239 11 1 8.761 1 6 C 1 3.239 3.239 1 6 1 C 8.761 1 11 3.239 11 6 C 11 8.761 8.761 11 6 11 Z"} fill="currentColor" fillRule="evenodd" />
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
            }}>{props.label ?? "Text"}</span>
          </div>
        </div>
      </div>
      {props.divider && (
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
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.icon2 ?? <Chevron direction={"right"} size={"16"} theme={"regular"} />}</div>
      </div>
      )}
    </div>
  );
  const __body7 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      gap: "calc(var(--horizontal-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 51,
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-subtle-hover)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--horizontal-xxs) * 1px)",
        padding: "6px 12px 6px 12px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
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
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-brand-hover)",
          }}>
            <path d={"M 6 12 C 9.314 12 12 9.314 12 6 C 12 2.686 9.314 0 6 0 C 2.686 0 0 2.686 0 6 C 0 9.314 2.686 12 6 12 Z"} fill="currentColor" fillRule="evenodd" />
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
              fontSize: 14,
              whiteSpace: "nowrap",
              lineHeight: "20px",
              color: "var(--neutral-foreground-2-rest)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>{props.label ?? "Text"}</span>
          </div>
        </div>
      </div>
      {props.divider && (
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
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.icon2 ?? <Chevron direction={"right"} size={"16"} theme={"regular"} />}</div>
      </div>
      )}
    </div>
  );
  const __body8 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      gap: "calc(var(--horizontal-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 51,
        borderRadius: 4,
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--horizontal-xxs) * 1px)",
        padding: "6px 12px 6px 12px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
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
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest)",
          }}>
            <path d={"M 6 12 C 9.314 12 12 9.314 12 6 C 12 2.686 9.314 0 6 0 C 2.686 0 0 2.686 0 6 C 0 9.314 2.686 12 6 12 Z M 6 11 C 3.239 11 1 8.761 1 6 C 1 3.239 3.239 1 6 1 C 8.761 1 11 3.239 11 6 C 11 8.761 8.761 11 6 11 Z"} fill="currentColor" fillRule="evenodd" />
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
            }}>{props.label ?? "Text"}</span>
          </div>
        </div>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 51,
          height: 32,
          overflow: "hidden",
          borderRadius: 4,
          borderTop: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderRight: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderBottom: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderLeft: "2px solid var(--neutral-stroke-focus-2-rest)",
        }}>
          <svg width={51} height={32} viewBox="0 0 51 32" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 51,
            height: 32,
            overflow: "hidden",
            borderRadius: 4,
            borderTop: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderRight: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderBottom: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderLeft: "1px solid var(--neutral-stroke-focus-1-rest)",
          }}>
            <path d={"M 4 0 L 4 1 L 47 1 L 47 0 L 47 -1 L 4 -1 L 4 0 Z M 51 4 L 50 4 L 50 28 L 51 28 L 52 28 L 52 4 L 51 4 Z M 47 32 L 47 31 L 4 31 L 4 32 L 4 33 L 47 33 L 47 32 Z M 0 28 L 1 28 L 1 4 L 0 4 L -1 4 L -1 28 L 0 28 Z M 4 32 L 4 31 C 2.343 31 1 29.657 1 28 L 0 28 L -1 28 C -1 30.761 1.239 33 4 33 L 4 32 Z M 51 28 L 50 28 C 50 29.657 48.657 31 47 31 L 47 32 L 47 33 C 49.761 33 52 30.761 52 28 L 51 28 Z M 47 0 L 47 1 C 48.657 1 50 2.343 50 4 L 51 4 L 52 4 C 52 1.239 49.761 -1 47 -1 L 47 0 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      {props.divider && (
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
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.icon2 ?? <Chevron direction={"right"} size={"16"} theme={"regular"} />}</div>
      </div>
      )}
    </div>
  );
  const __body9 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      gap: "calc(var(--horizontal-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 51,
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-subtle-pressed)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--horizontal-xxs) * 1px)",
        padding: "6px 12px 6px 12px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
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
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-brand-pressed)",
          }}>
            <path d={"M 6 12 C 9.314 12 12 9.314 12 6 C 12 2.686 9.314 0 6 0 C 2.686 0 0 2.686 0 6 C 0 9.314 2.686 12 6 12 Z"} fill="currentColor" fillRule="evenodd" />
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
              color: "var(--neutral-foreground-1-pressed)",
              flexShrink: 0,
            }}>{props.label ?? "Text"}</span>
          </div>
        </div>
      </div>
      {props.divider && (
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
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.icon2 ?? <Chevron direction={"right"} size={"16"} theme={"regular"} />}</div>
      </div>
      )}
    </div>
  );
  const __body10 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      gap: "calc(var(--horizontal-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 51,
        borderRadius: 4,
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--horizontal-xxs) * 1px)",
        padding: "6px 12px 6px 12px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
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
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-disabled-rest)",
          }}>
            <path d={"M 6 12 C 9.314 12 12 9.314 12 6 C 12 2.686 9.314 0 6 0 C 2.686 0 0 2.686 0 6 C 0 9.314 2.686 12 6 12 Z M 6 11 C 3.239 11 1 8.761 1 6 C 1 3.239 3.239 1 6 1 C 8.761 1 11 3.239 11 6 C 11 8.761 8.761 11 6 11 Z"} fill="currentColor" fillRule="evenodd" />
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
              color: "var(--neutral-foreground-disabled-rest)",
              flexShrink: 0,
            }}>{props.label ?? "Text"}</span>
          </div>
        </div>
      </div>
      {props.divider && (
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
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.icon2 ?? <Chevron direction={"right"} size={"16"} theme={"regular"} />}</div>
      </div>
      )}
    </div>
  );
  const __body11 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      gap: "calc(var(--horizontal-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 52,
        borderRadius: 4,
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--horizontal-xxs) * 1px)",
        padding: "6px 12px 6px 12px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
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
        }}>
          <svg width={12} height={12} viewBox="0 0 12 12" fill="none" style={{
            position: "relative",
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest)",
          }}>
            <path d={"M 6 12 C 9.314 12 12 9.314 12 6 C 12 2.686 9.314 0 6 0 C 2.686 0 0 2.686 0 6 C 0 9.314 2.686 12 6 12 Z M 6 11 C 3.239 11 1 8.761 1 6 C 1 3.239 3.239 1 6 1 C 8.761 1 11 3.239 11 6 C 11 8.761 8.761 11 6 11 Z"} fill="currentColor" fillRule="evenodd" />
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
            }}>{props.label ?? "Text"}</span>
          </div>
        </div>
      </div>
    </div>
  );
  const __body12 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      gap: "calc(var(--horizontal-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 41,
        borderRadius: 4,
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--horizontal-none) * 1px)",
        padding: "2px 8px 2px 0px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
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
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--neutral-foreground-1-rest)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
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
            }}>{props.label ?? "Text"}</span>
          </div>
        </div>
      </div>
    </div>
  );
  const __body13 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      gap: "calc(var(--horizontal-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 39,
        borderRadius: 4,
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--horizontal-none) * 1px)",
        padding: "2px 8px 2px 0px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
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
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--neutral-foreground-1-rest)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
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
            }}>{props.label ?? "Text"}</span>
          </div>
        </div>
      </div>
      {props.divider && (
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
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.icon2 ?? <Chevron direction={"right"} size={"12"} theme={"regular"} />}</div>
      </div>
      )}
    </div>
  );
  const __body14 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      gap: "calc(var(--horizontal-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 39,
        borderRadius: 4,
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--horizontal-none) * 1px)",
        padding: "2px 8px 2px 0px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
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
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
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
            }}>{props.label ?? "Text"}</span>
          </div>
        </div>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 39,
          height: 24,
          overflow: "hidden",
          borderRadius: 4,
          borderTop: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderRight: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderBottom: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderLeft: "2px solid var(--neutral-stroke-focus-2-rest)",
        }}>
          <svg width={39} height={24} viewBox="0 0 39 24" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 39,
            height: 24,
            overflow: "hidden",
            borderRadius: 4,
            borderTop: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderRight: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderBottom: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderLeft: "1px solid var(--neutral-stroke-focus-1-rest)",
          }}>
            <path d={"M 4 0 L 4 1 L 35 1 L 35 0 L 35 -1 L 4 -1 L 4 0 Z M 39 4 L 38 4 L 38 20 L 39 20 L 40 20 L 40 4 L 39 4 Z M 35 24 L 35 23 L 4 23 L 4 24 L 4 25 L 35 25 L 35 24 Z M 0 20 L 1 20 L 1 4 L 0 4 L -1 4 L -1 20 L 0 20 Z M 4 24 L 4 23 C 2.343 23 1 21.657 1 20 L 0 20 L -1 20 C -1 22.761 1.239 25 4 25 L 4 24 Z M 39 20 L 38 20 C 38 21.657 36.657 23 35 23 L 35 24 L 35 25 C 37.761 25 40 22.761 40 20 L 39 20 Z M 35 0 L 35 1 C 36.657 1 38 2.343 38 4 L 39 4 L 40 4 C 40 1.239 37.761 -1 35 -1 L 35 0 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      {props.divider && (
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
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.icon2 ?? <Chevron direction={"right"} size={"12"} theme={"regular"} />}</div>
      </div>
      )}
    </div>
  );
  const __body15 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      gap: "calc(var(--horizontal-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 39,
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-subtle-hover)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--horizontal-none) * 1px)",
        padding: "2px 8px 2px 0px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
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
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-brand-hover)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z"} fill="currentColor" fillRule="evenodd" />
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
            }}>{props.label ?? "Text"}</span>
          </div>
        </div>
      </div>
      {props.divider && (
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
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.icon2 ?? <Chevron direction={"right"} size={"12"} theme={"regular"} />}</div>
      </div>
      )}
    </div>
  );
  const __body16 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      gap: "calc(var(--horizontal-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 39,
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-subtle-pressed)",
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--horizontal-none) * 1px)",
        padding: "2px 8px 2px 0px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
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
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-brand-pressed)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z"} fill="currentColor" fillRule="evenodd" />
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
              color: "var(--neutral-foreground-1-pressed)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>{props.label ?? "Text"}</span>
          </div>
        </div>
      </div>
      {props.divider && (
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
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.icon2 ?? <Chevron direction={"right"} size={"12"} theme={"regular"} />}</div>
      </div>
      )}
    </div>
  );
  const __body17 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      gap: "calc(var(--horizontal-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        width: 39,
        borderRadius: 4,
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--horizontal-none) * 1px)",
        padding: "2px 8px 2px 0px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
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
        }}>
          <svg width={8} height={8} viewBox="0 0 8 8" fill="none" style={{
            position: "relative",
            width: 8,
            height: 8,
            flexShrink: 0,
            color: "var(--neutral-foreground-disabled-rest)",
          }}>
            <path d={"M 4 8 C 6.209 8 8 6.209 8 4 C 8 1.791 6.209 0 4 0 C 1.791 0 0 1.791 0 4 C 0 6.209 1.791 8 4 8 Z M 4 7 C 2.343 7 1 5.657 1 4 C 1 2.343 2.343 1 4 1 C 5.657 1 7 2.343 7 4 C 7 5.657 5.657 7 4 7 Z"} fill="currentColor" fillRule="evenodd" />
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
              color: "var(--neutral-foreground-disabled-rest)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>{props.label ?? "Text"}</span>
          </div>
        </div>
      </div>
      {props.divider && (
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
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.icon2 ?? <Chevron direction={"right"} size={"12"} theme={"regular"} />}</div>
      </div>
      )}
    </div>
  );
  const __body18 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      gap: "calc(var(--horizontal-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
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
            }}>{props.label ?? "Text"}</span>
          </div>
        </div>
      </div>
      {props.divider && (
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
            width: 20,
            height: 20,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.icon2 ?? <Chevron direction={"right"} size={"20"} theme={"regular"} />}</div>
      </div>
      )}
    </div>
  );
  const __body19 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      gap: "calc(var(--horizontal-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-subtle-hover)",
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
            color: "var(--neutral-foreground-2-brand-hover)",
          }}>
            <path d={"M 10 20 C 15.523 20 20 15.523 20 10 C 20 4.477 15.523 0 10 0 C 4.477 0 0 4.477 0 10 C 0 15.523 4.477 20 10 20 Z"} fill="currentColor" fillRule="evenodd" />
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
            }}>{props.label ?? "Text"}</span>
          </div>
        </div>
      </div>
      {props.divider && (
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
            width: 20,
            height: 20,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.icon2 ?? <Chevron direction={"right"} size={"20"} theme={"regular"} />}</div>
      </div>
      )}
    </div>
  );
  const __body20 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      gap: "calc(var(--horizontal-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-subtle-pressed)",
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
            color: "var(--neutral-foreground-2-brand-pressed)",
          }}>
            <path d={"M 10 20 C 15.523 20 20 15.523 20 10 C 20 4.477 15.523 0 10 0 C 4.477 0 0 4.477 0 10 C 0 15.523 4.477 20 10 20 Z"} fill="currentColor" fillRule="evenodd" />
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
              color: "var(--neutral-foreground-1-pressed)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>{props.label ?? "Text"}</span>
          </div>
        </div>
      </div>
      {props.divider && (
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
            width: 20,
            height: 20,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.icon2 ?? <Chevron direction={"right"} size={"20"} theme={"regular"} />}</div>
      </div>
      )}
    </div>
  );
  const __body21 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      gap: "calc(var(--horizontal-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
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
            }}>{props.label ?? "Text"}</span>
          </div>
        </div>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 44,
          height: 40,
          overflow: "hidden",
          borderRadius: 4,
          borderTop: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderRight: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderBottom: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderLeft: "2px solid var(--neutral-stroke-focus-2-rest)",
        }}>
          <svg width={44} height={40} viewBox="0 0 44 40" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 44,
            height: 40,
            overflow: "hidden",
            borderRadius: 4,
            borderTop: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderRight: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderBottom: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderLeft: "1px solid var(--neutral-stroke-focus-1-rest)",
          }}>
            <path d={"M 4 0 L 4 1 L 40 1 L 40 0 L 40 -1 L 4 -1 L 4 0 Z M 44 4 L 43 4 L 43 36 L 44 36 L 45 36 L 45 4 L 44 4 Z M 40 40 L 40 39 L 4 39 L 4 40 L 4 41 L 40 41 L 40 40 Z M 0 36 L 1 36 L 1 4 L 0 4 L -1 4 L -1 36 L 0 36 Z M 4 40 L 4 39 C 2.343 39 1 37.657 1 36 L 0 36 L -1 36 C -1 38.761 1.239 41 4 41 L 4 40 Z M 44 36 L 43 36 C 43 37.657 41.657 39 40 39 L 40 40 L 40 41 C 42.761 41 45 38.761 45 36 L 44 36 Z M 40 0 L 40 1 C 41.657 1 43 2.343 43 4 L 44 4 L 45 4 C 45 1.239 42.761 -1 40 -1 L 40 0 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      {props.divider && (
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
            width: 20,
            height: 20,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.icon2 ?? <Chevron direction={"right"} size={"20"} theme={"regular"} />}</div>
      </div>
      )}
    </div>
  );
  const __body22 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      gap: "calc(var(--horizontal-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        borderRadius: 4,
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
            color: "var(--neutral-foreground-disabled-rest)",
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
              color: "var(--neutral-foreground-disabled-rest)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>{props.label ?? "Text"}</span>
          </div>
        </div>
      </div>
      {props.divider && (
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
            width: 20,
            height: 20,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.icon2 ?? <Chevron direction={"right"} size={"20"} theme={"regular"} />}</div>
      </div>
      )}
    </div>
  );
  const __body23 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      gap: "calc(var(--horizontal-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
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
            }}>{props.label ?? "Text"}</span>
          </div>
        </div>
      </div>
      {props.divider && (
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
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.icon2 ?? <Chevron direction={"right"} size={"16"} theme={"regular"} />}</div>
      </div>
      )}
    </div>
  );
  const __body24 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      gap: "calc(var(--horizontal-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
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
            }}>{props.label ?? "Text"}</span>
          </div>
        </div>
      </div>
    </div>
  );
  const __body25 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      gap: "calc(var(--horizontal-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-subtle-hover)",
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
              fontSize: 14,
              whiteSpace: "nowrap",
              lineHeight: "20px",
              color: "var(--neutral-foreground-2-rest)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>{props.label ?? "Text"}</span>
          </div>
        </div>
      </div>
      {props.divider && (
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
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.icon2 ?? <Chevron direction={"right"} size={"16"} theme={"regular"} />}</div>
      </div>
      )}
    </div>
  );
  const __body26 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      gap: "calc(var(--horizontal-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-subtle-pressed)",
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
              color: "var(--neutral-foreground-1-pressed)",
              flexShrink: 0,
            }}>{props.label ?? "Text"}</span>
          </div>
        </div>
      </div>
      {props.divider && (
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
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.icon2 ?? <Chevron direction={"right"} size={"16"} theme={"regular"} />}</div>
      </div>
      )}
    </div>
  );
  const __body27 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      gap: "calc(var(--horizontal-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
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
            }}>{props.label ?? "Text"}</span>
          </div>
        </div>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 37,
          height: 32,
          overflow: "hidden",
          borderRadius: 4,
          borderTop: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderRight: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderBottom: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderLeft: "2px solid var(--neutral-stroke-focus-2-rest)",
        }}>
          <svg width={37} height={32} viewBox="0 0 37 32" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 37,
            height: 32,
            overflow: "hidden",
            borderRadius: 4,
            borderTop: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderRight: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderBottom: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderLeft: "1px solid var(--neutral-stroke-focus-1-rest)",
          }}>
            <path d={"M 4 0 L 4 1 L 33 1 L 33 0 L 33 -1 L 4 -1 L 4 0 Z M 37 4 L 36 4 L 36 28 L 37 28 L 38 28 L 38 4 L 37 4 Z M 33 32 L 33 31 L 4 31 L 4 32 L 4 33 L 33 33 L 33 32 Z M 0 28 L 1 28 L 1 4 L 0 4 L -1 4 L -1 28 L 0 28 Z M 4 32 L 4 31 C 2.343 31 1 29.657 1 28 L 0 28 L -1 28 C -1 30.761 1.239 33 4 33 L 4 32 Z M 37 28 L 36 28 C 36 29.657 34.657 31 33 31 L 33 32 L 33 33 C 35.761 33 38 30.761 38 28 L 37 28 Z M 33 0 L 33 1 C 34.657 1 36 2.343 36 4 L 37 4 L 38 4 C 38 1.239 35.761 -1 33 -1 L 33 0 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      {props.divider && (
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
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.icon2 ?? <Chevron direction={"right"} size={"16"} theme={"regular"} />}</div>
      </div>
      )}
    </div>
  );
  const __body28 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      gap: "calc(var(--horizontal-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
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
            }}>{props.label ?? "Text"}</span>
          </div>
        </div>
      </div>
    </div>
  );
  const __body29 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      gap: "calc(var(--horizontal-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
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
            }}>{props.label ?? "Text"}</span>
          </div>
        </div>
      </div>
      {props.divider && (
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
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.icon2 ?? <Chevron direction={"right"} size={"12"} theme={"regular"} />}</div>
      </div>
      )}
    </div>
  );
  const __body30 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      gap: "calc(var(--horizontal-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-subtle-hover)",
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
            }}>{props.label ?? "Text"}</span>
          </div>
        </div>
      </div>
      {props.divider && (
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
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.icon2 ?? <Chevron direction={"right"} size={"12"} theme={"regular"} />}</div>
      </div>
      )}
    </div>
  );
  const __body31 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      gap: "calc(var(--horizontal-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-subtle-pressed)",
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
              color: "var(--neutral-foreground-1-pressed)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>{props.label ?? "Text"}</span>
          </div>
        </div>
      </div>
      {props.divider && (
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
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.icon2 ?? <Chevron direction={"right"} size={"12"} theme={"regular"} />}</div>
      </div>
      )}
    </div>
  );
  const __body32 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      gap: "calc(var(--horizontal-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
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
            }}>{props.label ?? "Text"}</span>
          </div>
        </div>
      </div>
    </div>
  );
  const __body33 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      gap: "calc(var(--horizontal-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
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
            }}>{props.label ?? "Text"}</span>
          </div>
        </div>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 33,
          height: 24,
          overflow: "hidden",
          borderRadius: 4,
          borderTop: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderRight: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderBottom: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderLeft: "2px solid var(--neutral-stroke-focus-2-rest)",
        }}>
          <svg width={33} height={24} viewBox="0 0 33 24" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 33,
            height: 24,
            overflow: "hidden",
            borderRadius: 4,
            borderTop: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderRight: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderBottom: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderLeft: "1px solid var(--neutral-stroke-focus-1-rest)",
          }}>
            <path d={"M 4 0 L 4 1 L 29 1 L 29 0 L 29 -1 L 4 -1 L 4 0 Z M 33 4 L 32 4 L 32 20 L 33 20 L 34 20 L 34 4 L 33 4 Z M 29 24 L 29 23 L 4 23 L 4 24 L 4 25 L 29 25 L 29 24 Z M 0 20 L 1 20 L 1 4 L 0 4 L -1 4 L -1 20 L 0 20 Z M 4 24 L 4 23 C 2.343 23 1 21.657 1 20 L 0 20 L -1 20 C -1 22.761 1.239 25 4 25 L 4 24 Z M 33 20 L 32 20 C 32 21.657 30.657 23 29 23 L 29 24 L 29 25 C 31.761 25 34 22.761 34 20 L 33 20 Z M 29 0 L 29 1 C 30.657 1 32 2.343 32 4 L 33 4 L 34 4 C 34 1.239 31.761 -1 29 -1 L 29 0 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      {props.divider && (
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
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.icon2 ?? <Chevron direction={"right"} size={"12"} theme={"regular"} />}</div>
      </div>
      )}
    </div>
  );
  const __body34 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      gap: "calc(var(--horizontal-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        borderRadius: 4,
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
              color: "var(--neutral-foreground-disabled-rest)",
              flexShrink: 0,
              alignSelf: "stretch",
            }}>{props.label ?? "Text"}</span>
          </div>
        </div>
      </div>
      {props.divider && (
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
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.icon2 ?? <Chevron direction={"right"} size={"12"} theme={"regular"} />}</div>
      </div>
      )}
    </div>
  );
  const __body35 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      gap: "calc(var(--horizontal-none) * 1px)",
      position: "relative",
      ...props.style,
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
      {props.divider && (
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
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.icon2 ?? <Chevron direction={"right"} size={"16"} theme={"regular"} />}</div>
      </div>
      )}
    </div>
  );
  const __body36 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      gap: "calc(var(--horizontal-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-subtle-hover)",
        display: "flex",
        flexDirection: "row",
        padding: "6px 6px 6px 6px",
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
            <svg width={13.500} height={3.500} viewBox="0 0 13.500 3.500" fill="none" style={{
              position: "absolute",
              left: 3.25,
              top: 8.25,
              width: 13.5,
              height: 3.5,
              color: "var(--neutral-foreground-1-rest)",
            }}>
              <path d={"M 3.5 1.75 C 3.5 2.716 2.716 3.5 1.75 3.5 C 0.784 3.5 0 2.716 0 1.75 C 0 0.784 0.784 0 1.75 0 C 2.716 0 3.5 0.784 3.5 1.75 Z M 8.5 1.75 C 8.5 2.716 7.716 3.5 6.75 3.5 C 5.784 3.5 5 2.716 5 1.75 C 5 0.784 5.784 0 6.75 0 C 7.716 0 8.5 0.784 8.5 1.75 Z M 11.75 3.5 C 12.716 3.5 13.5 2.716 13.5 1.75 C 13.5 0.784 12.716 0 11.75 0 C 10.784 0 10 0.784 10 1.75 C 10 2.716 10.784 3.5 11.75 3.5 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
        </div>
      </div>
      {props.divider && (
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
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.icon2 ?? <Chevron direction={"right"} size={"16"} theme={"regular"} />}</div>
      </div>
      )}
    </div>
  );
  const __body37 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      gap: "calc(var(--horizontal-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-subtle-pressed)",
        display: "flex",
        flexDirection: "row",
        padding: "6px 6px 6px 6px",
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
            <svg width={13.500} height={3.500} viewBox="0 0 13.500 3.500" fill="none" style={{
              position: "absolute",
              left: 3.25,
              top: 8.25,
              width: 13.5,
              height: 3.5,
              color: "var(--neutral-foreground-1-rest)",
            }}>
              <path d={"M 3.5 1.75 C 3.5 2.716 2.716 3.5 1.75 3.5 C 0.784 3.5 0 2.716 0 1.75 C 0 0.784 0.784 0 1.75 0 C 2.716 0 3.5 0.784 3.5 1.75 Z M 8.5 1.75 C 8.5 2.716 7.716 3.5 6.75 3.5 C 5.784 3.5 5 2.716 5 1.75 C 5 0.784 5.784 0 6.75 0 C 7.716 0 8.5 0.784 8.5 1.75 Z M 11.75 3.5 C 12.716 3.5 13.5 2.716 13.5 1.75 C 13.5 0.784 12.716 0 11.75 0 C 10.784 0 10 0.784 10 1.75 C 10 2.716 10.784 3.5 11.75 3.5 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
        </div>
      </div>
      {props.divider && (
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
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.icon2 ?? <Chevron direction={"right"} size={"16"} theme={"regular"} />}</div>
      </div>
      )}
    </div>
  );
  const __body38 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      gap: "calc(var(--horizontal-none) * 1px)",
      position: "relative",
      ...props.style,
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
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 32,
          height: 32,
          overflow: "hidden",
          borderRadius: 4,
          borderTop: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderRight: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderBottom: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderLeft: "2px solid var(--neutral-stroke-focus-2-rest)",
        }}>
          <svg width={32} height={32} viewBox="0 0 32 32" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 32,
            height: 32,
            overflow: "hidden",
            borderRadius: 4,
            borderTop: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderRight: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderBottom: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderLeft: "1px solid var(--neutral-stroke-focus-1-rest)",
          }}>
            <path d={"M 4 0 L 4 1 L 28 1 L 28 0 L 28 -1 L 4 -1 L 4 0 Z M 32 4 L 31 4 L 31 28 L 32 28 L 33 28 L 33 4 L 32 4 Z M 28 32 L 28 31 L 4 31 L 4 32 L 4 33 L 28 33 L 28 32 Z M 0 28 L 1 28 L 1 4 L 0 4 L -1 4 L -1 28 L 0 28 Z M 4 32 L 4 31 C 2.343 31 1 29.657 1 28 L 0 28 L -1 28 C -1 30.761 1.239 33 4 33 L 4 32 Z M 32 28 L 31 28 C 31 29.657 29.657 31 28 31 L 28 32 L 28 33 C 30.761 33 33 30.761 33 28 L 32 28 Z M 28 0 L 28 1 C 29.657 1 31 2.343 31 4 L 32 4 L 33 4 C 33 1.239 30.761 -1 28 -1 L 28 0 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      {props.divider && (
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
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.icon2 ?? <Chevron direction={"right"} size={"16"} theme={"regular"} />}</div>
      </div>
      )}
    </div>
  );
  const __body39 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      gap: "calc(var(--horizontal-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        borderRadius: 4,
        display: "flex",
        flexDirection: "row",
        padding: "6px 6px 6px 6px",
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
              color: "var(--neutral-foreground-disabled-rest)",
            }}>
              <path d={"M 2.5 1.25 C 2.5 1.94 1.94 2.5 1.25 2.5 C 0.56 2.5 0 1.94 0 1.25 C 0 0.56 0.56 0 1.25 0 C 1.94 0 2.5 0.56 2.5 1.25 Z M 7.5 1.25 C 7.5 1.94 6.94 2.5 6.25 2.5 C 5.56 2.5 5 1.94 5 1.25 C 5 0.56 5.56 0 6.25 0 C 6.94 0 7.5 0.56 7.5 1.25 Z M 11.25 2.5 C 11.94 2.5 12.5 1.94 12.5 1.25 C 12.5 0.56 11.94 0 11.25 0 C 10.56 0 10 0.56 10 1.25 C 10 1.94 10.56 2.5 11.25 2.5 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
        </div>
      </div>
      {props.divider && (
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
            width: 16,
            height: 16,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.icon2 ?? <Chevron direction={"right"} size={"16"} theme={"regular"} />}</div>
      </div>
      )}
    </div>
  );
  const __body40 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      gap: "calc(var(--horizontal-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        borderRadius: 4,
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        padding: "8px 8px 8px 8px",
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
          gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            width: 24,
            overflow: "hidden",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <svg width={15.500} height={3.500} viewBox="0 0 15.500 3.500" fill="none" style={{
              position: "absolute",
              left: 4.25,
              top: 10.25,
              width: 15.5,
              height: 3.5,
              color: "var(--neutral-foreground-2-rest)",
            }}>
              <path d={"M 3.5 1.75 C 3.5 2.716 2.716 3.5 1.75 3.5 C 0.784 3.5 0 2.716 0 1.75 C 0 0.784 0.784 0 1.75 0 C 2.716 0 3.5 0.784 3.5 1.75 Z M 9.5 1.75 C 9.5 2.716 8.716 3.5 7.75 3.5 C 6.784 3.5 6 2.716 6 1.75 C 6 0.784 6.784 0 7.75 0 C 8.716 0 9.5 0.784 9.5 1.75 Z M 13.75 3.5 C 14.716 3.5 15.5 2.716 15.5 1.75 C 15.5 0.784 14.716 0 13.75 0 C 12.784 0 12 0.784 12 1.75 C 12 2.716 12.784 3.5 13.75 3.5 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
        </div>
      </div>
      {props.divider && (
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
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.icon2 ?? <Chevron direction={"right"} size={"20"} theme={"regular"} />}</div>
      </div>
      )}
    </div>
  );
  const __body41 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      gap: "calc(var(--horizontal-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-subtle-hover)",
        display: "flex",
        flexDirection: "row",
        padding: "8px 8px 8px 8px",
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
          gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            width: 24,
            overflow: "hidden",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <svg width={16} height={4} viewBox="0 0 16 4" fill="none" style={{
              position: "absolute",
              left: 4,
              top: 10,
              width: 16,
              height: 4,
              color: "var(--neutral-foreground-1-rest)",
            }}>
              <path d={"M 4 2 C 4 3.105 3.105 4 2 4 C 0.895 4 0 3.105 0 2 C 0 0.895 0.895 0 2 0 C 3.105 0 4 0.895 4 2 Z M 10 2 C 10 3.105 9.105 4 8 4 C 6.895 4 6 3.105 6 2 C 6 0.895 6.895 0 8 0 C 9.105 0 10 0.895 10 2 Z M 14 4 C 15.105 4 16 3.105 16 2 C 16 0.895 15.105 0 14 0 C 12.895 0 12 0.895 12 2 C 12 3.105 12.895 4 14 4 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
        </div>
      </div>
      {props.divider && (
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
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.icon2 ?? <Chevron direction={"right"} size={"20"} theme={"regular"} />}</div>
      </div>
      )}
    </div>
  );
  const __body42 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      gap: "calc(var(--horizontal-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-subtle-pressed)",
        display: "flex",
        flexDirection: "row",
        padding: "8px 8px 8px 8px",
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
          gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            width: 24,
            overflow: "hidden",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <svg width={16} height={4} viewBox="0 0 16 4" fill="none" style={{
              position: "absolute",
              left: 4,
              top: 10,
              width: 16,
              height: 4,
              color: "var(--neutral-foreground-1-rest)",
            }}>
              <path d={"M 4 2 C 4 3.105 3.105 4 2 4 C 0.895 4 0 3.105 0 2 C 0 0.895 0.895 0 2 0 C 3.105 0 4 0.895 4 2 Z M 10 2 C 10 3.105 9.105 4 8 4 C 6.895 4 6 3.105 6 2 C 6 0.895 6.895 0 8 0 C 9.105 0 10 0.895 10 2 Z M 14 4 C 15.105 4 16 3.105 16 2 C 16 0.895 15.105 0 14 0 C 12.895 0 12 0.895 12 2 C 12 3.105 12.895 4 14 4 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
        </div>
      </div>
      {props.divider && (
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
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.icon2 ?? <Chevron direction={"right"} size={"20"} theme={"regular"} />}</div>
      </div>
      )}
    </div>
  );
  const __body43 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      gap: "calc(var(--horizontal-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        borderRadius: 4,
        backgroundColor: "rgba(255,255,255,0)",
        display: "flex",
        flexDirection: "row",
        padding: "8px 8px 8px 8px",
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
          gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            width: 24,
            overflow: "hidden",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <svg width={15.500} height={3.500} viewBox="0 0 15.500 3.500" fill="none" style={{
              position: "absolute",
              left: 4.25,
              top: 10.25,
              width: 15.5,
              height: 3.5,
              color: "var(--neutral-foreground-2-rest)",
            }}>
              <path d={"M 3.5 1.75 C 3.5 2.716 2.716 3.5 1.75 3.5 C 0.784 3.5 0 2.716 0 1.75 C 0 0.784 0.784 0 1.75 0 C 2.716 0 3.5 0.784 3.5 1.75 Z M 9.5 1.75 C 9.5 2.716 8.716 3.5 7.75 3.5 C 6.784 3.5 6 2.716 6 1.75 C 6 0.784 6.784 0 7.75 0 C 8.716 0 9.5 0.784 9.5 1.75 Z M 13.75 3.5 C 14.716 3.5 15.5 2.716 15.5 1.75 C 15.5 0.784 14.716 0 13.75 0 C 12.784 0 12 0.784 12 1.75 C 12 2.716 12.784 3.5 13.75 3.5 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
        </div>
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 40,
          height: 40,
          overflow: "hidden",
          borderRadius: 4,
          borderTop: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderRight: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderBottom: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderLeft: "2px solid var(--neutral-stroke-focus-2-rest)",
        }}>
          <svg width={40} height={40} viewBox="0 0 40 40" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 40,
            height: 40,
            overflow: "hidden",
            borderRadius: 4,
            borderTop: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderRight: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderBottom: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderLeft: "1px solid var(--neutral-stroke-focus-1-rest)",
          }}>
            <path d={"M 4 0 L 4 1 L 36 1 L 36 0 L 36 -1 L 4 -1 L 4 0 Z M 40 4 L 39 4 L 39 36 L 40 36 L 41 36 L 41 4 L 40 4 Z M 36 40 L 36 39 L 4 39 L 4 40 L 4 41 L 36 41 L 36 40 Z M 0 36 L 1 36 L 1 4 L 0 4 L -1 4 L -1 36 L 0 36 Z M 4 40 L 4 39 C 2.343 39 1 37.657 1 36 L 0 36 L -1 36 C -1 38.761 1.239 41 4 41 L 4 40 Z M 40 36 L 39 36 C 39 37.657 37.657 39 36 39 L 36 40 L 36 41 C 38.761 41 41 38.761 41 36 L 40 36 Z M 36 0 L 36 1 C 37.657 1 39 2.343 39 4 L 40 4 L 41 4 C 41 1.239 38.761 -1 36 -1 L 36 0 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      {props.divider && (
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
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.icon2 ?? <Chevron direction={"right"} size={"20"} theme={"regular"} />}</div>
      </div>
      )}
    </div>
  );
  const __body44 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      gap: "calc(var(--horizontal-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        borderRadius: 4,
        display: "flex",
        flexDirection: "row",
        padding: "8px 8px 8px 8px",
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
          gap: "calc(var(--spacing-horizontal-xxs-2) * 1px)",
          justifyContent: "center",
          alignItems: "center",
          flexWrap: "nowrap",
          flexShrink: 0,
          alignSelf: "stretch",
        }}>
          <div style={{
            position: "relative",
            width: 24,
            overflow: "hidden",
            flexShrink: 0,
            alignSelf: "stretch",
          }}>
            <svg width={15.500} height={3.500} viewBox="0 0 15.500 3.500" fill="none" style={{
              position: "absolute",
              left: 4.25,
              top: 10.25,
              width: 15.5,
              height: 3.5,
              color: "var(--neutral-foreground-disabled-rest)",
            }}>
              <path d={"M 3.5 1.75 C 3.5 2.716 2.716 3.5 1.75 3.5 C 0.784 3.5 0 2.716 0 1.75 C 0 0.784 0.784 0 1.75 0 C 2.716 0 3.5 0.784 3.5 1.75 Z M 9.5 1.75 C 9.5 2.716 8.716 3.5 7.75 3.5 C 6.784 3.5 6 2.716 6 1.75 C 6 0.784 6.784 0 7.75 0 C 8.716 0 9.5 0.784 9.5 1.75 Z M 13.75 3.5 C 14.716 3.5 15.5 2.716 15.5 1.75 C 15.5 0.784 14.716 0 13.75 0 C 12.784 0 12 0.784 12 1.75 C 12 2.716 12.784 3.5 13.75 3.5 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
        </div>
      </div>
      {props.divider && (
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
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.icon2 ?? <Chevron direction={"right"} size={"20"} theme={"regular"} />}</div>
      </div>
      )}
    </div>
  );
  const __body45 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      gap: "calc(var(--horizontal-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
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
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingTop: "calc(var(--vertical-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        paddingBottom: "calc(var(--vertical-xxs) * 1px)",
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
      {props.divider && (
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
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.dividerIcon ?? <Chevron direction={"right"} size={"12"} theme={"regular"} />}</div>
      </div>
      )}
    </div>
  );
  const __body46 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      gap: "calc(var(--horizontal-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-subtle-hover)",
        display: "flex",
        flexDirection: "row",
        padding: "2px 2px 2px 2px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingTop: "calc(var(--vertical-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        paddingBottom: "calc(var(--vertical-xxs) * 1px)",
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
            <svg width={13.500} height={3.500} viewBox="0 0 13.500 3.500" fill="none" style={{
              position: "absolute",
              left: 3.25,
              top: 8.25,
              width: 13.5,
              height: 3.5,
              color: "var(--neutral-foreground-1-rest)",
            }}>
              <path d={"M 3.5 1.75 C 3.5 2.716 2.716 3.5 1.75 3.5 C 0.784 3.5 0 2.716 0 1.75 C 0 0.784 0.784 0 1.75 0 C 2.716 0 3.5 0.784 3.5 1.75 Z M 8.5 1.75 C 8.5 2.716 7.716 3.5 6.75 3.5 C 5.784 3.5 5 2.716 5 1.75 C 5 0.784 5.784 0 6.75 0 C 7.716 0 8.5 0.784 8.5 1.75 Z M 11.75 3.5 C 12.716 3.5 13.5 2.716 13.5 1.75 C 13.5 0.784 12.716 0 11.75 0 C 10.784 0 10 0.784 10 1.75 C 10 2.716 10.784 3.5 11.75 3.5 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
        </div>
      </div>
      {props.divider && (
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
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.dividerIcon ?? <Chevron direction={"right"} size={"12"} theme={"regular"} />}</div>
      </div>
      )}
    </div>
  );
  const __body47 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      gap: "calc(var(--horizontal-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        borderRadius: 4,
        backgroundColor: "var(--neutral-background-subtle-pressed)",
        display: "flex",
        flexDirection: "row",
        padding: "2px 2px 2px 2px",
        justifyContent: "center",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingTop: "calc(var(--vertical-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        paddingBottom: "calc(var(--vertical-xxs) * 1px)",
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
            <svg width={13.500} height={3.500} viewBox="0 0 13.500 3.500" fill="none" style={{
              position: "absolute",
              left: 3.25,
              top: 8.25,
              width: 13.5,
              height: 3.5,
              color: "var(--neutral-foreground-2-brand-pressed)",
            }}>
              <path d={"M 3.5 1.75 C 3.5 2.716 2.716 3.5 1.75 3.5 C 0.784 3.5 0 2.716 0 1.75 C 0 0.784 0.784 0 1.75 0 C 2.716 0 3.5 0.784 3.5 1.75 Z M 8.5 1.75 C 8.5 2.716 7.716 3.5 6.75 3.5 C 5.784 3.5 5 2.716 5 1.75 C 5 0.784 5.784 0 6.75 0 C 7.716 0 8.5 0.784 8.5 1.75 Z M 11.75 3.5 C 12.716 3.5 13.5 2.716 13.5 1.75 C 13.5 0.784 12.716 0 11.75 0 C 10.784 0 10 0.784 10 1.75 C 10 2.716 10.784 3.5 11.75 3.5 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
        </div>
      </div>
      {props.divider && (
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
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.dividerIcon ?? <Chevron direction={"right"} size={"12"} theme={"regular"} />}</div>
      </div>
      )}
    </div>
  );
  const __body48 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      gap: "calc(var(--horizontal-none) * 1px)",
      position: "relative",
      ...props.style,
    }}>
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
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingTop: "calc(var(--vertical-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        paddingBottom: "calc(var(--vertical-xxs) * 1px)",
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
        <div style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 24,
          height: 24,
          overflow: "hidden",
          borderRadius: 4,
          borderTop: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderRight: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderBottom: "2px solid var(--neutral-stroke-focus-2-rest)",
          borderLeft: "2px solid var(--neutral-stroke-focus-2-rest)",
        }}>
          <svg width={24} height={24} viewBox="0 0 24 24" fill="none" style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 24,
            height: 24,
            overflow: "hidden",
            borderRadius: 4,
            borderTop: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderRight: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderBottom: "1px solid var(--neutral-stroke-focus-1-rest)",
            borderLeft: "1px solid var(--neutral-stroke-focus-1-rest)",
          }}>
            <path d={"M 4 0 L 4 1 L 20 1 L 20 0 L 20 -1 L 4 -1 L 4 0 Z M 24 4 L 23 4 L 23 20 L 24 20 L 25 20 L 25 4 L 24 4 Z M 20 24 L 20 23 L 4 23 L 4 24 L 4 25 L 20 25 L 20 24 Z M 0 20 L 1 20 L 1 4 L 0 4 L -1 4 L -1 20 L 0 20 Z M 4 24 L 4 23 C 2.343 23 1 21.657 1 20 L 0 20 L -1 20 C -1 22.761 1.239 25 4 25 L 4 24 Z M 24 20 L 23 20 C 23 21.657 21.657 23 20 23 L 20 24 L 20 25 C 22.761 25 25 22.761 25 20 L 24 20 Z M 20 0 L 20 1 C 21.657 1 23 2.343 23 4 L 24 4 L 25 4 C 25 1.239 22.761 -1 20 -1 L 20 0 Z M 4 0 L 4 -1 C 1.239 -1 -1 1.239 -1 4 L 0 4 L 1 4 C 1 2.343 2.343 1 4 1 L 4 0 Z"} fill="currentColor" fillRule="nonzero" />
          </svg>
        </div>
      </div>
      {props.divider && (
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
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.dividerIcon ?? <Chevron direction={"right"} size={"12"} theme={"regular"} />}</div>
      </div>
      )}
    </div>
  );
  const __body49 = () => (
    <div className={props.className} style={{
      width: "fit-content",
      display: "flex",
      flexDirection: "row",
      alignItems: "center",
      flexWrap: "nowrap",
      gap: "calc(var(--horizontal-none) * 1px)",
      position: "relative",
      ...props.style,
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
        paddingLeft: "calc(var(--horizontal-xxs) * 1px)",
        paddingTop: "calc(var(--vertical-xxs) * 1px)",
        paddingRight: "calc(var(--horizontal-xxs) * 1px)",
        paddingBottom: "calc(var(--vertical-xxs) * 1px)",
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
              color: "var(--neutral-foreground-disabled-rest)",
            }}>
              <path d={"M 2.5 1.25 C 2.5 1.94 1.94 2.5 1.25 2.5 C 0.56 2.5 0 1.94 0 1.25 C 0 0.56 0.56 0 1.25 0 C 1.94 0 2.5 0.56 2.5 1.25 Z M 7.5 1.25 C 7.5 1.94 6.94 2.5 6.25 2.5 C 5.56 2.5 5 1.94 5 1.25 C 5 0.56 5.56 0 6.25 0 C 6.94 0 7.5 0.56 7.5 1.25 Z M 11.25 2.5 C 11.94 2.5 12.5 1.94 12.5 1.25 C 12.5 0.56 11.94 0 11.25 0 C 10.56 0 10 0.56 10 1.25 C 10 1.94 10.56 2.5 11.25 2.5 Z"} fill="currentColor" fillRule="nonzero" />
            </svg>
          </div>
        </div>
      </div>
      {props.divider && (
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
            width: 12,
            height: 12,
            flexShrink: 0,
            color: "var(--neutral-foreground-2-rest)",
          }}>{props.dividerIcon ?? <Chevron direction={"right"} size={"12"} theme={"regular"} />}</div>
      </div>
      )}
    </div>
  );
  const __impls = {
    // figma: Size=Large, Type=Button, States=Rest, Icon=ON
    "size=lg|type=button|states=rest|icon=on": __body0,
    // figma: Size=Large, Type=Button, States=Hover, Icon=ON
    "size=lg|type=button|states=hover|icon=on": __body1,
    // figma: Size=Large, Type=Button, States=Pressed, Icon=ON
    "size=lg|type=button|states=pressed|icon=on": __body2,
    // figma: Size=Large, Type=Button, States=Focused, Icon=ON
    "size=lg|type=button|states=focused|icon=on": __body3,
    // figma: Size=Large, Type=Button, States=Disabled, Icon=ON
    "size=lg|type=button|states=disabled|icon=on": __body4,
    // figma: Size=Large, Type=Button, States=Current, Icon=ON
    "size=lg|type=button|states=current|icon=on": __body5,
    // figma: Size=Medium, Type=Button, States=Rest, Icon=ON
    "size=md|type=button|states=rest|icon=on": __body6,
    // figma: Size=Medium, Type=Button, States=Hover, Icon=ON
    "size=md|type=button|states=hover|icon=on": __body7,
    // figma: Size=Medium, Type=Button, States=Focused, Icon=ON
    "size=md|type=button|states=focused|icon=on": __body8,
    // figma: Size=Medium, Type=Button, States=Pressed, Icon=ON
    "size=md|type=button|states=pressed|icon=on": __body9,
    // figma: Size=Medium, Type=Button, States=Disabled, Icon=ON
    "size=md|type=button|states=disabled|icon=on": __body10,
    // figma: Size=Medium, Type=Button, States=Current, Icon=ON
    "size=md|type=button|states=current|icon=on": __body11,
    // figma: Size=Small, Type=Button, States=Rest, Icon=ON
    "size=sm|type=button|states=rest|icon=on": __body12,
    // figma: Size=Small, Type=Button, States=Current, Icon=ON
    "size=sm|type=button|states=current|icon=on": __body13,
    // figma: Size=Small, Type=Button, States=Focused, Icon=ON
    "size=sm|type=button|states=focused|icon=on": __body14,
    // figma: Size=Small, Type=Button, States=Hover, Icon=ON
    "size=sm|type=button|states=hover|icon=on": __body15,
    // figma: Size=Small, Type=Button, States=Pressed, Icon=ON
    "size=sm|type=button|states=pressed|icon=on": __body16,
    // figma: Size=Small, Type=Button, States=Disabled, Icon=ON
    "size=sm|type=button|states=disabled|icon=on": __body17,
    // figma: Size=Large, Type=Button, States=Rest, Icon=OFF
    "size=lg|type=button|states=rest|icon=off": __body18,
    // figma: Size=Large, Type=Button, States=Hover, Icon=OFF
    "size=lg|type=button|states=hover|icon=off": __body19,
    // figma: Size=Large, Type=Button, States=Pressed, Icon=OFF
    "size=lg|type=button|states=pressed|icon=off": __body20,
    // figma: Size=Large, Type=Button, States=Focused, Icon=OFF
    "size=lg|type=button|states=focused|icon=off": __body21,
    // figma: Size=Large, Type=Button, States=Disabled, Icon=OFF
    "size=lg|type=button|states=disabled|icon=off": __body22,
    // figma: Size=Medium, Type=Button, States=Rest, Icon=OFF
    "size=md|type=button|states=rest|icon=off": __body23,
    // figma: Size=Large, Type=Button, States=Current, Icon=OFF
    "size=lg|type=button|states=current|icon=off": __body24,
    // figma: Size=Medium, Type=Button, States=Hover, Icon=OFF
    "size=md|type=button|states=hover|icon=off": __body25,
    // figma: Size=Medium, Type=Button, States=Pressed, Icon=OFF
    "size=md|type=button|states=pressed|icon=off": __body26,
    // figma: Size=Medium, Type=Button, States=Focused, Icon=OFF
    "size=md|type=button|states=focused|icon=off": __body27,
    // figma: Size=Medium, Type=Button, States=Current, Icon=OFF
    "size=md|type=button|states=current|icon=off": __body28,
    // figma: Size=Medium, Type=Button, States=Disabled, Icon=OFF
    "size=md|type=button|states=disabled|icon=off": __body23,
    // figma: Size=Small, Type=Button, States=Rest, Icon=OFF
    "size=sm|type=button|states=rest|icon=off": __body29,
    // figma: Size=Small, Type=Button, States=Hover, Icon=OFF
    "size=sm|type=button|states=hover|icon=off": __body30,
    // figma: Size=Small, Type=Button, States=Pressed, Icon=OFF
    "size=sm|type=button|states=pressed|icon=off": __body31,
    // figma: Size=Small, Type=Button, States=Current, Icon=OFF
    "size=sm|type=button|states=current|icon=off": __body32,
    // figma: Size=Small, Type=Button, States=Focused, Icon=OFF
    "size=sm|type=button|states=focused|icon=off": __body33,
    // figma: Size=Small, Type=Button, States=Disabled, Icon=OFF
    "size=sm|type=button|states=disabled|icon=off": __body34,
    // figma: Size=Medium, Type=Overflow, States=Rest, Icon=OFF
    "size=md|type=overflow|states=rest|icon=off": __body35,
    // figma: Size=Medium, Type=Overflow, States=Hover, Icon=OFF
    "size=md|type=overflow|states=hover|icon=off": __body36,
    // figma: Size=Medium, Type=Overflow, States=Pressed, Icon=OFF
    "size=md|type=overflow|states=pressed|icon=off": __body37,
    // figma: Size=Medium, Type=Overflow, States=Focused, Icon=OFF
    "size=md|type=overflow|states=focused|icon=off": __body38,
    // figma: Size=Medium, Type=Overflow, States=Disabled, Icon=OFF
    "size=md|type=overflow|states=disabled|icon=off": __body39,
    // figma: Size=Large, Type=Overflow, States=Rest, Icon=OFF
    "size=lg|type=overflow|states=rest|icon=off": __body40,
    // figma: Size=Large, Type=Overflow, States=Hover, Icon=OFF
    "size=lg|type=overflow|states=hover|icon=off": __body41,
    // figma: Size=Large, Type=Overflow, States=Pressed, Icon=OFF
    "size=lg|type=overflow|states=pressed|icon=off": __body42,
    // figma: Size=Large, Type=Overflow, States=Focused, Icon=OFF
    "size=lg|type=overflow|states=focused|icon=off": __body43,
    // figma: Size=Large, Type=Overflow, States=Disabled, Icon=OFF
    "size=lg|type=overflow|states=disabled|icon=off": __body44,
    // figma: Size=Small, Type=Overflow, States=Rest, Icon=OFF
    "size=sm|type=overflow|states=rest|icon=off": __body45,
    // figma: Size=Small, Type=Overflow, States=Hover, Icon=OFF
    "size=sm|type=overflow|states=hover|icon=off": __body46,
    // figma: Size=Small, Type=Overflow, States=Pressed, Icon=OFF
    "size=sm|type=overflow|states=pressed|icon=off": __body47,
    // figma: Size=Small, Type=Overflow, States=Focused, Icon=OFF
    "size=sm|type=overflow|states=focused|icon=off": __body48,
    // figma: Size=Small, Type=Overflow, States=Disabled, Icon=OFF
    "size=sm|type=overflow|states=disabled|icon=off": __body49,
  };
  return (__impls[__vkey(props)] ?? __body0)();
}
export default BreadcrumbItem;
