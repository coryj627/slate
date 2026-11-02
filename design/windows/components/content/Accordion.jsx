import { __slateNorm } from '../internal/slateProps.jsx';
import { Chevron2 } from '../internal/Chevron2.jsx';
import { FocusRing } from '../internal/FocusRing.jsx';
import { Placeholder } from '../internal/Placeholder.jsx';
import { Placeholder3 } from '../internal/Placeholder3.jsx';

// figma node: 9074:913 Accordion (24 variants)
const __venc = (v) => String(v).replace(/[%|=]/g, encodeURIComponent);
const __vkey = (p) => "size=" + __venc(p.size) + '|' + "state=" + __venc(p.state) + '|' + "expanded=" + __venc(p.expanded) + '|' + "chevron=" + __venc(p.chevron);

export function Accordion(_p = {}) {
  _p = __slateNorm(_p, {"size":["md","lg","sm","extra large"],"state":["rest","disabled"],"chevron":["before","after"]});
  const props = { ..._p, icon: _p.icon ?? true, focused: _p.focused ?? false, text: _p.text ?? "Text", size: _p.size ?? "md", state: _p.state ?? "rest", expanded: _p.expanded ?? true, chevron: _p.chevron ?? "before" };
  const __body0 = () => (
    <div className={props.className} style={{
      width: 340,
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        height: 44,
        borderRadius: 4,
        display: "flex",
        flexDirection: "row",
        padding: "0px 10px 0px 10px",
        justifyContent: "space-between",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
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
            gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            flexShrink: 0,
          }}>
            <div style={{
                position: "relative",
                width: 20,
                height: 20,
                flexShrink: 0,
                color: "var(--neutral-foreground-1-rest)",
              }}>{props.icon1 ?? <Chevron2 direction={"down"} size={"20"} theme={"regular"} />}</div>
            <div style={{ position: "relative", flexShrink: 0, color: "var(--neutral-foreground-1-rest)" }}>{props.iconBefore20pxRegular ?? <Placeholder size={"20"} theme={"regular"} />}</div>
          </div>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            padding: "1px 0px 1px 0px",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--spacing-vertical-xxs) * 0.5px)", paddingBottom: "calc(var(--spacing-vertical-xxs) * 0.5px)",
            flexGrow: 1,
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
            }}>{props.text}</span>
          </div>
        </div>
        <div style={{
          position: "relative",
          width: 20,
          height: 20,
          display: "flex",
          flexDirection: "row",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
        }} />
        {props.focused && (
        <div style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 340,
            height: 44,
          }}>
          <FocusRing style={{ transform: "scale(2.208, 0.458)", transformOrigin: "0 0" }} />
        </div>
        )}
      </div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        padding: "0px 12px 12px 12px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-m-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            height: 44,
            flexShrink: 0,
            alignSelf: "stretch",
            width: "auto",
          }}>{props.contentSwap ?? <Placeholder3 slot={"1"} />}</div>
      </div>
    </div>
  );
  const __body1 = () => (
    <div className={props.className} style={{
      width: 340,
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        height: 44,
        borderRadius: 4,
        display: "flex",
        flexDirection: "row",
        padding: "0px 10px 0px 10px",
        justifyContent: "space-between",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
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
            gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            flexShrink: 0,
          }}>
            <div style={{
                position: "relative",
                width: 20,
                height: 20,
                flexShrink: 0,
                color: "var(--neutral-foreground-1-rest)",
              }}>{props.icon1 ?? <Chevron2 direction={"right"} size={"20"} theme={"regular"} />}</div>
            {props.icon && (
            <div style={{ position: "relative", flexShrink: 0, color: "var(--neutral-foreground-1-rest)" }}>{props.iconBefore20pxRegular ?? <Placeholder size={"20"} theme={"regular"} />}</div>
            )}
          </div>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            padding: "1px 0px 1px 0px",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--spacing-vertical-xxs) * 0.5px)", paddingBottom: "calc(var(--spacing-vertical-xxs) * 0.5px)",
            flexGrow: 1,
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
            }}>{props.text}</span>
          </div>
        </div>
        <div style={{
          position: "relative",
          width: 20,
          height: 20,
          display: "flex",
          flexDirection: "row",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
        }} />
        {props.focused && (
        <div style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 340,
            height: 44,
          }}>
          <FocusRing style={{ transform: "scale(2.208, 0.458)", transformOrigin: "0 0" }} />
        </div>
        )}
      </div>
    </div>
  );
  const __body2 = () => (
    <div className={props.className} style={{
      width: 340,
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        height: 44,
        borderRadius: 4,
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--vertical-s) * 1px)",
        padding: "0px 10px 0px 10px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
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
          {props.icon && (
          <div style={{ position: "relative", flexShrink: 0, color: "var(--neutral-foreground-1-rest)" }}>{props.iconBefore20pxRegular ?? <Placeholder size={"20"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            width: 272,
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
              color: "var(--neutral-foreground-1-rest)",
              flexShrink: 0,
            }}>{props.text}</span>
          </div>
        </div>
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
              color: "var(--neutral-foreground-1-rest)",
            }}>{props.icon1 ?? <Chevron2 direction={"up"} size={"20"} theme={"regular"} />}</div>
        </div>
        {props.focused && (
        <div style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 340,
            height: 44,
          }}>
          <FocusRing style={{ transform: "scale(2.208, 0.458)", transformOrigin: "0 0" }} />
        </div>
        )}
      </div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        padding: "0px 12px 12px 12px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-m-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            height: 44,
            flexShrink: 0,
            alignSelf: "stretch",
            width: "auto",
          }}>{props.contentSwap ?? <Placeholder3 slot={"1"} />}</div>
      </div>
    </div>
  );
  const __body3 = () => (
    <div className={props.className} style={{
      width: 340,
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        height: 44,
        borderRadius: 4,
        display: "flex",
        flexDirection: "row",
        padding: "0px 10px 0px 10px",
        justifyContent: "space-between",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
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
          {props.icon && (
          <div style={{ position: "relative", flexShrink: 0, color: "var(--neutral-foreground-1-rest)" }}>{props.iconBefore20pxRegular ?? <Placeholder size={"20"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            padding: "1px 0px 1px 0px",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--spacing-vertical-xxs) * 0.5px)", paddingBottom: "calc(var(--spacing-vertical-xxs) * 0.5px)",
            flexGrow: 1,
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
            }}>{props.text}</span>
          </div>
        </div>
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
              color: "var(--neutral-foreground-1-rest)",
            }}>{props.icon1 ?? <Chevron2 direction={"down"} size={"20"} theme={"regular"} />}</div>
        </div>
        {props.focused && (
        <div style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 340,
            height: 44,
          }}>
          <FocusRing style={{ transform: "scale(2.208, 0.458)", transformOrigin: "0 0" }} />
        </div>
        )}
      </div>
    </div>
  );
  const __body4 = () => (
    <div className={props.className} style={{
      width: 340,
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        height: 44,
        borderRadius: 4,
        display: "flex",
        flexDirection: "row",
        padding: "0px 10px 0px 10px",
        justifyContent: "space-between",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.focused && (
        <div style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 340,
            height: 44,
          }}>
          <FocusRing style={{ transform: "scale(2.208, 0.458)", transformOrigin: "0 0" }} />
        </div>
        )}
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
            gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            flexShrink: 0,
          }}>
            <div style={{
                position: "relative",
                width: 20,
                height: 20,
                flexShrink: 0,
                color: "var(--neutral-foreground-1-rest)",
              }}>{props.icon1 ?? <Chevron2 direction={"down"} size={"20"} theme={"regular"} />}</div>
            <div style={{ position: "relative", flexShrink: 0, color: "var(--neutral-foreground-1-rest)" }}>{props.iconBefore20pxRegular ?? <Placeholder size={"20"} theme={"regular"} />}</div>
          </div>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            padding: "1px 0px 1px 0px",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--spacing-vertical-xxs) * 0.5px)", paddingBottom: "calc(var(--spacing-vertical-xxs) * 0.5px)",
            flexGrow: 1,
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
            }}>{props.text}</span>
          </div>
        </div>
        <div style={{
          position: "relative",
          width: 20,
          height: 20,
          display: "flex",
          flexDirection: "row",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
        }} />
      </div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        padding: "0px 12px 12px 12px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-m-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            height: 44,
            flexShrink: 0,
            alignSelf: "stretch",
            width: "auto",
          }}>{props.contentSwap ?? <Placeholder3 slot={"1"} />}</div>
      </div>
    </div>
  );
  const __body5 = () => (
    <div className={props.className} style={{
      width: 340,
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        height: 44,
        borderRadius: 4,
        display: "flex",
        flexDirection: "row",
        padding: "0px 10px 0px 10px",
        justifyContent: "space-between",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.focused && (
        <div style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 340,
            height: 44,
          }}>
          <FocusRing style={{ transform: "scale(2.208, 0.458)", transformOrigin: "0 0" }} />
        </div>
        )}
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
            gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            flexShrink: 0,
          }}>
            <div style={{
                position: "relative",
                width: 20,
                height: 20,
                flexShrink: 0,
                color: "var(--neutral-foreground-1-rest)",
              }}>{props.icon1 ?? <Chevron2 direction={"right"} size={"20"} theme={"regular"} />}</div>
            {props.icon && (
            <div style={{ position: "relative", flexShrink: 0, color: "var(--neutral-foreground-1-rest)" }}>{props.iconBefore20pxRegular ?? <Placeholder size={"20"} theme={"regular"} />}</div>
            )}
          </div>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            padding: "1px 0px 1px 0px",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--spacing-vertical-xxs) * 0.5px)", paddingBottom: "calc(var(--spacing-vertical-xxs) * 0.5px)",
            flexGrow: 1,
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
            }}>{props.text}</span>
          </div>
        </div>
        <div style={{
          position: "relative",
          width: 20,
          height: 20,
          display: "flex",
          flexDirection: "row",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
        }} />
      </div>
    </div>
  );
  const __body6 = () => (
    <div className={props.className} style={{
      width: 340,
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        height: 44,
        borderRadius: 4,
        display: "flex",
        flexDirection: "row",
        padding: "0px 10px 0px 10px",
        justifyContent: "space-between",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.focused && (
        <div style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 340,
            height: 44,
          }}>
          <FocusRing style={{ transform: "scale(2.208, 0.458)", transformOrigin: "0 0" }} />
        </div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
          alignItems: "center",
          flexWrap: "nowrap",
          flexGrow: 1,
        }}>
          {props.icon && (
          <div style={{ position: "relative", flexShrink: 0, color: "var(--neutral-foreground-1-rest)" }}>{props.iconBefore20pxRegular ?? <Placeholder size={"20"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            padding: "1px 0px 1px 0px",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--spacing-vertical-xxs) * 0.5px)", paddingBottom: "calc(var(--spacing-vertical-xxs) * 0.5px)",
            flexGrow: 1,
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
            }}>{props.text}</span>
          </div>
        </div>
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
              color: "var(--neutral-foreground-1-rest)",
            }}>{props.icon1 ?? <Chevron2 direction={"up"} size={"20"} theme={"regular"} />}</div>
        </div>
      </div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        padding: "0px 12px 12px 12px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-m-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            height: 44,
            flexShrink: 0,
            alignSelf: "stretch",
            width: "auto",
          }}>{props.contentSwap ?? <Placeholder3 slot={"1"} />}</div>
      </div>
    </div>
  );
  const __body7 = () => (
    <div className={props.className} style={{
      width: 340,
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        height: 44,
        borderRadius: 4,
        display: "flex",
        flexDirection: "row",
        padding: "0px 10px 0px 10px",
        justifyContent: "space-between",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.focused && (
        <div style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 340,
            height: 44,
          }}>
          <FocusRing style={{ transform: "scale(2.208, 0.458)", transformOrigin: "0 0" }} />
        </div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
          alignItems: "center",
          flexWrap: "nowrap",
          flexGrow: 1,
        }}>
          {props.icon && (
          <div style={{ position: "relative", flexShrink: 0, color: "var(--neutral-foreground-1-rest)" }}>{props.iconBefore20pxRegular ?? <Placeholder size={"20"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            padding: "1px 0px 1px 0px",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--spacing-vertical-xxs) * 0.5px)", paddingBottom: "calc(var(--spacing-vertical-xxs) * 0.5px)",
            flexGrow: 1,
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
            }}>{props.text}</span>
          </div>
        </div>
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
              color: "var(--neutral-foreground-1-rest)",
            }}>{props.icon1 ?? <Chevron2 direction={"down"} size={"20"} theme={"regular"} />}</div>
        </div>
      </div>
    </div>
  );
  const __body8 = () => (
    <div className={props.className} style={{
      width: 340,
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        height: 44,
        borderRadius: 4,
        display: "flex",
        flexDirection: "row",
        padding: "0px 10px 0px 10px",
        justifyContent: "space-between",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.focused && (
        <div style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 340,
            height: 44,
          }}>
          <FocusRing style={{ transform: "scale(2.208, 0.458)", transformOrigin: "0 0" }} />
        </div>
        )}
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
            gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            flexShrink: 0,
          }}>
            <div style={{
                position: "relative",
                width: 20,
                height: 20,
                flexShrink: 0,
                color: "var(--neutral-foreground-1-rest)",
              }}>{props.icon1 ?? <Chevron2 direction={"down"} size={"20"} theme={"regular"} />}</div>
            {props.icon && (
            <div style={{ position: "relative", flexShrink: 0, color: "var(--neutral-foreground-1-rest)" }}>{props.iconBefore20pxRegular ?? <Placeholder size={"20"} theme={"regular"} />}</div>
            )}
          </div>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            padding: "1px 0px 1px 0px",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--spacing-vertical-xxs) * 0.5px)", paddingBottom: "calc(var(--spacing-vertical-xxs) * 0.5px)",
            flexGrow: 1,
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 600,
              fontSize: 20,
              whiteSpace: "nowrap",
              lineHeight: "28px",
              color: "var(--neutral-foreground-1-rest)",
              flexShrink: 0,
            }}>{props.text}</span>
          </div>
        </div>
        <div style={{
          position: "relative",
          width: 20,
          height: 20,
          display: "flex",
          flexDirection: "row",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
        }} />
      </div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        padding: "0px 12px 12px 12px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-m-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            height: 44,
            flexShrink: 0,
            alignSelf: "stretch",
            width: "auto",
          }}>{props.contentSwap ?? <Placeholder3 slot={"1"} />}</div>
      </div>
    </div>
  );
  const __body9 = () => (
    <div className={props.className} style={{
      width: 340,
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        height: 44,
        borderRadius: 4,
        display: "flex",
        flexDirection: "row",
        padding: "0px 10px 0px 10px",
        justifyContent: "space-between",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.focused && (
        <div style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 340,
            height: 44,
          }}>
          <FocusRing style={{ transform: "scale(2.208, 0.458)", transformOrigin: "0 0" }} />
        </div>
        )}
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
            gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            flexShrink: 0,
          }}>
            <div style={{
                position: "relative",
                width: 20,
                height: 20,
                flexShrink: 0,
                color: "var(--neutral-foreground-1-rest)",
              }}>{props.icon1 ?? <Chevron2 direction={"right"} size={"20"} theme={"regular"} />}</div>
            {props.icon && (
            <div style={{ position: "relative", flexShrink: 0, color: "var(--neutral-foreground-1-rest)" }}>{props.iconBefore20pxRegular ?? <Placeholder size={"20"} theme={"regular"} />}</div>
            )}
          </div>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            padding: "1px 0px 1px 0px",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--spacing-vertical-xxs) * 0.5px)", paddingBottom: "calc(var(--spacing-vertical-xxs) * 0.5px)",
            flexGrow: 1,
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 600,
              fontSize: 20,
              whiteSpace: "nowrap",
              lineHeight: "28px",
              color: "var(--neutral-foreground-1-rest)",
              flexShrink: 0,
            }}>{props.text}</span>
          </div>
        </div>
        <div style={{
          position: "relative",
          width: 20,
          height: 20,
          display: "flex",
          flexDirection: "row",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
        }} />
      </div>
    </div>
  );
  const __body10 = () => (
    <div className={props.className} style={{
      width: 340,
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        height: 44,
        borderRadius: 4,
        display: "flex",
        flexDirection: "row",
        padding: "0px 10px 0px 10px",
        justifyContent: "space-between",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.focused && (
        <div style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 340,
            height: 44,
          }}>
          <FocusRing style={{ transform: "scale(2.208, 0.458)", transformOrigin: "0 0" }} />
        </div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
          alignItems: "center",
          flexWrap: "nowrap",
          flexGrow: 1,
        }}>
          {props.icon && (
          <div style={{ position: "relative", flexShrink: 0, color: "var(--neutral-foreground-1-rest)" }}>{props.iconBefore20pxRegular ?? <Placeholder size={"20"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            padding: "1px 0px 1px 0px",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--spacing-vertical-xxs) * 0.5px)", paddingBottom: "calc(var(--spacing-vertical-xxs) * 0.5px)",
            flexGrow: 1,
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 600,
              fontSize: 20,
              whiteSpace: "nowrap",
              lineHeight: "28px",
              color: "var(--neutral-foreground-1-rest)",
              flexShrink: 0,
            }}>{props.text}</span>
          </div>
        </div>
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
              color: "var(--neutral-foreground-1-rest)",
            }}>{props.icon1 ?? <Chevron2 direction={"up"} size={"20"} theme={"regular"} />}</div>
        </div>
      </div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        padding: "0px 12px 12px 12px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-m-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            height: 44,
            flexShrink: 0,
            alignSelf: "stretch",
            width: "auto",
          }}>{props.contentSwap ?? <Placeholder3 slot={"1"} />}</div>
      </div>
    </div>
  );
  const __body11 = () => (
    <div className={props.className} style={{
      width: 340,
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        height: 44,
        borderRadius: 4,
        display: "flex",
        flexDirection: "row",
        padding: "0px 10px 0px 10px",
        justifyContent: "space-between",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.focused && (
        <div style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 340,
            height: 44,
          }}>
          <FocusRing style={{ transform: "scale(2.208, 0.458)", transformOrigin: "0 0" }} />
        </div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
          alignItems: "center",
          flexWrap: "nowrap",
          flexGrow: 1,
        }}>
          {props.icon && (
          <div style={{ position: "relative", flexShrink: 0, color: "var(--neutral-foreground-1-rest)" }}>{props.iconBefore20pxRegular ?? <Placeholder size={"20"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            padding: "1px 0px 1px 0px",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--spacing-vertical-xxs) * 0.5px)", paddingBottom: "calc(var(--spacing-vertical-xxs) * 0.5px)",
            flexGrow: 1,
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 600,
              fontSize: 20,
              whiteSpace: "nowrap",
              lineHeight: "28px",
              color: "var(--neutral-foreground-1-rest)",
              flexShrink: 0,
            }}>{props.text}</span>
          </div>
        </div>
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
              color: "var(--neutral-foreground-1-rest)",
            }}>{props.icon1 ?? <Chevron2 direction={"down"} size={"20"} theme={"regular"} />}</div>
        </div>
      </div>
    </div>
  );
  const __body12 = () => (
    <div className={props.className} style={{
      width: 340,
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        height: 44,
        borderRadius: 4,
        display: "flex",
        flexDirection: "row",
        padding: "0px 10px 0px 10px",
        justifyContent: "space-between",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
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
            gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            flexShrink: 0,
          }}>
            <div style={{
                position: "relative",
                width: 20,
                height: 20,
                flexShrink: 0,
                color: "var(--neutral-foreground-1-rest)",
              }}>{props.icon1 ?? <Chevron2 direction={"down"} size={"20"} theme={"regular"} />}</div>
            <div style={{ position: "relative", flexShrink: 0, color: "var(--neutral-foreground-1-rest)" }}>{props.iconBefore20pxRegular ?? <Placeholder size={"20"} theme={"regular"} />}</div>
          </div>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            padding: "1px 0px 1px 0px",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--spacing-vertical-xxs) * 0.5px)", paddingBottom: "calc(var(--spacing-vertical-xxs) * 0.5px)",
            flexGrow: 1,
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 400,
              fontSize: 12,
              whiteSpace: "nowrap",
              lineHeight: "16px",
              color: "var(--neutral-foreground-1-rest)",
              flexShrink: 0,
            }}>{props.text}</span>
          </div>
        </div>
        <div style={{
          position: "relative",
          width: 20,
          height: 20,
          display: "flex",
          flexDirection: "row",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
        }} />
        {props.focused && (
        <div style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 340,
            height: 44,
          }}>
          <FocusRing style={{ transform: "scale(2.208, 0.458)", transformOrigin: "0 0" }} />
        </div>
        )}
      </div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        padding: "0px 12px 12px 12px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-m-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            height: 44,
            flexShrink: 0,
            alignSelf: "stretch",
            width: "auto",
          }}>{props.contentSwap ?? <Placeholder3 slot={"1"} />}</div>
      </div>
    </div>
  );
  const __body13 = () => (
    <div className={props.className} style={{
      width: 340,
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        height: 44,
        borderRadius: 4,
        display: "flex",
        flexDirection: "row",
        padding: "0px 10px 0px 10px",
        justifyContent: "space-between",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
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
            gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            flexShrink: 0,
          }}>
            <div style={{
                position: "relative",
                width: 20,
                height: 20,
                flexShrink: 0,
                color: "var(--neutral-foreground-1-rest)",
              }}>{props.icon1 ?? <Chevron2 direction={"right"} size={"20"} theme={"regular"} />}</div>
            {props.icon && (
            <div style={{ position: "relative", flexShrink: 0, color: "var(--neutral-foreground-1-rest)" }}>{props.iconBefore20pxRegular ?? <Placeholder size={"20"} theme={"regular"} />}</div>
            )}
          </div>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            padding: "1px 0px 1px 0px",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--spacing-vertical-xxs) * 0.5px)", paddingBottom: "calc(var(--spacing-vertical-xxs) * 0.5px)",
            flexGrow: 1,
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 400,
              fontSize: 12,
              whiteSpace: "nowrap",
              lineHeight: "16px",
              color: "var(--neutral-foreground-1-rest)",
              flexShrink: 0,
            }}>{props.text}</span>
          </div>
        </div>
        <div style={{
          position: "relative",
          width: 20,
          height: 20,
          display: "flex",
          flexDirection: "row",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
        }} />
        {props.focused && (
        <div style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 340,
            height: 44,
          }}>
          <FocusRing style={{ transform: "scale(2.208, 0.458)", transformOrigin: "0 0" }} />
        </div>
        )}
      </div>
    </div>
  );
  const __body14 = () => (
    <div className={props.className} style={{
      width: 340,
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        height: 44,
        borderRadius: 4,
        display: "flex",
        flexDirection: "row",
        gap: "calc(var(--vertical-s) * 1px)",
        padding: "0px 10px 0px 10px",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
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
          {props.icon && (
          <div style={{ position: "relative", flexShrink: 0, color: "var(--neutral-foreground-1-rest)" }}>{props.iconBefore20pxRegular ?? <Placeholder size={"20"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            width: 272,
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
              color: "var(--neutral-foreground-1-rest)",
              flexShrink: 0,
            }}>{props.text}</span>
          </div>
        </div>
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
              color: "var(--neutral-foreground-1-rest)",
            }}>{props.icon1 ?? <Chevron2 direction={"up"} size={"20"} theme={"regular"} />}</div>
        </div>
        {props.focused && (
        <div style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 340,
            height: 44,
          }}>
          <FocusRing style={{ transform: "scale(2.208, 0.458)", transformOrigin: "0 0" }} />
        </div>
        )}
      </div>
      <div style={{
        position: "relative",
        display: "flex",
        flexDirection: "column",
        padding: "0px 12px 12px 12px",
        alignItems: "flex-start",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-m-2) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-m-2) * 1px)",
        paddingBottom: "calc(var(--spacing-vertical-m-2) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        <div style={{
            position: "relative",
            height: 44,
            flexShrink: 0,
            alignSelf: "stretch",
            width: "auto",
          }}>{props.contentSwap ?? <Placeholder3 slot={"1"} />}</div>
      </div>
    </div>
  );
  const __body15 = () => (
    <div className={props.className} style={{
      width: 340,
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        height: 44,
        borderRadius: 4,
        display: "flex",
        flexDirection: "row",
        padding: "0px 10px 0px 10px",
        justifyContent: "space-between",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
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
          {props.icon && (
          <div style={{ position: "relative", flexShrink: 0, color: "var(--neutral-foreground-1-rest)" }}>{props.iconBefore20pxRegular ?? <Placeholder size={"20"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            padding: "1px 0px 1px 0px",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--spacing-vertical-xxs) * 0.5px)", paddingBottom: "calc(var(--spacing-vertical-xxs) * 0.5px)",
            flexGrow: 1,
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 400,
              fontSize: 12,
              whiteSpace: "nowrap",
              lineHeight: "16px",
              color: "var(--neutral-foreground-1-rest)",
              flexShrink: 0,
            }}>{props.text}</span>
          </div>
        </div>
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
              color: "var(--neutral-foreground-1-rest)",
            }}>{props.icon1 ?? <Chevron2 direction={"down"} size={"20"} theme={"regular"} />}</div>
        </div>
        {props.focused && (
        <div style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 340,
            height: 44,
          }}>
          <FocusRing style={{ transform: "scale(2.208, 0.458)", transformOrigin: "0 0" }} />
        </div>
        )}
      </div>
    </div>
  );
  const __body16 = () => (
    <div className={props.className} style={{
      width: 340,
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        height: 44,
        borderRadius: 4,
        display: "flex",
        flexDirection: "row",
        padding: "0px 10px 0px 10px",
        justifyContent: "space-between",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
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
            gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            flexShrink: 0,
          }}>
            <div style={{
                position: "relative",
                width: 20,
                height: 20,
                flexShrink: 0,
                color: "var(--neutral-foreground-disabled-rest)",
              }}>{props.icon1 ?? <Chevron2 direction={"right"} size={"20"} theme={"regular"} />}</div>
            {props.icon && (
            <div style={{ position: "relative", flexShrink: 0, color: "var(--neutral-foreground-disabled-rest)" }}>{props.iconBefore20pxRegular ?? <Placeholder size={"20"} theme={"regular"} />}</div>
            )}
          </div>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            padding: "1px 0px 1px 0px",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--spacing-vertical-xxs) * 0.5px)", paddingBottom: "calc(var(--spacing-vertical-xxs) * 0.5px)",
            flexGrow: 1,
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
            }}>{props.text}</span>
          </div>
        </div>
        <div style={{
          position: "relative",
          width: 20,
          height: 20,
          display: "flex",
          flexDirection: "row",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
        }} />
        {props.focused && (
        <div style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 340,
            height: 44,
          }}>
          <FocusRing style={{ transform: "scale(2.208, 0.458)", transformOrigin: "0 0" }} />
        </div>
        )}
      </div>
    </div>
  );
  const __body17 = () => (
    <div className={props.className} style={{
      width: 340,
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        height: 44,
        borderRadius: 4,
        display: "flex",
        flexDirection: "row",
        padding: "0px 10px 0px 10px",
        justifyContent: "space-between",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
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
          {props.icon && (
          <div style={{ position: "relative", flexShrink: 0, color: "var(--neutral-foreground-disabled-rest)" }}>{props.iconBefore20pxRegular ?? <Placeholder size={"20"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            padding: "1px 0px 1px 0px",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--spacing-vertical-xxs) * 0.5px)", paddingBottom: "calc(var(--spacing-vertical-xxs) * 0.5px)",
            flexGrow: 1,
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
            }}>{props.text}</span>
          </div>
        </div>
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
              color: "var(--neutral-foreground-disabled-rest)",
            }}>{props.icon1 ?? <Chevron2 direction={"down"} size={"20"} theme={"regular"} />}</div>
        </div>
        {props.focused && (
        <div style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 340,
            height: 44,
          }}>
          <FocusRing style={{ transform: "scale(2.208, 0.458)", transformOrigin: "0 0" }} />
        </div>
        )}
      </div>
    </div>
  );
  const __body18 = () => (
    <div className={props.className} style={{
      width: 340,
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        height: 44,
        borderRadius: 4,
        display: "flex",
        flexDirection: "row",
        padding: "0px 10px 0px 10px",
        justifyContent: "space-between",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.focused && (
        <div style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 340,
            height: 44,
          }}>
          <FocusRing style={{ transform: "scale(2.208, 0.458)", transformOrigin: "0 0" }} />
        </div>
        )}
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
            gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            flexShrink: 0,
          }}>
            <div style={{
                position: "relative",
                width: 20,
                height: 20,
                flexShrink: 0,
                color: "var(--neutral-foreground-disabled-rest)",
              }}>{props.icon1 ?? <Chevron2 direction={"right"} size={"20"} theme={"regular"} />}</div>
            {props.icon && (
            <div style={{ position: "relative", flexShrink: 0, color: "var(--neutral-foreground-disabled-rest)" }}>{props.iconBefore20pxRegular ?? <Placeholder size={"20"} theme={"regular"} />}</div>
            )}
          </div>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            padding: "1px 0px 1px 0px",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--spacing-vertical-xxs) * 0.5px)", paddingBottom: "calc(var(--spacing-vertical-xxs) * 0.5px)",
            flexGrow: 1,
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 600,
              fontSize: 16,
              whiteSpace: "nowrap",
              lineHeight: "22px",
              color: "var(--neutral-foreground-disabled-rest)",
              flexShrink: 0,
            }}>{props.text}</span>
          </div>
        </div>
        <div style={{
          position: "relative",
          width: 20,
          height: 20,
          display: "flex",
          flexDirection: "row",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
        }} />
      </div>
    </div>
  );
  const __body19 = () => (
    <div className={props.className} style={{
      width: 340,
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        height: 44,
        borderRadius: 4,
        display: "flex",
        flexDirection: "row",
        padding: "0px 10px 0px 10px",
        justifyContent: "space-between",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.focused && (
        <div style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 340,
            height: 44,
          }}>
          <FocusRing style={{ transform: "scale(2.208, 0.458)", transformOrigin: "0 0" }} />
        </div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
          alignItems: "center",
          flexWrap: "nowrap",
          flexGrow: 1,
        }}>
          {props.icon && (
          <div style={{ position: "relative", flexShrink: 0, color: "var(--neutral-foreground-disabled-rest)" }}>{props.iconBefore20pxRegular ?? <Placeholder size={"20"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            padding: "1px 0px 1px 0px",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--spacing-vertical-xxs) * 0.5px)", paddingBottom: "calc(var(--spacing-vertical-xxs) * 0.5px)",
            flexGrow: 1,
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 600,
              fontSize: 16,
              whiteSpace: "nowrap",
              lineHeight: "22px",
              color: "var(--neutral-foreground-disabled-rest)",
              flexShrink: 0,
            }}>{props.text}</span>
          </div>
        </div>
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
              color: "var(--neutral-foreground-disabled-rest)",
            }}>{props.icon1 ?? <Chevron2 direction={"down"} size={"20"} theme={"regular"} />}</div>
        </div>
      </div>
    </div>
  );
  const __body20 = () => (
    <div className={props.className} style={{
      width: 340,
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        height: 44,
        borderRadius: 4,
        display: "flex",
        flexDirection: "row",
        padding: "0px 10px 0px 10px",
        justifyContent: "space-between",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.focused && (
        <div style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 340,
            height: 44,
          }}>
          <FocusRing style={{ transform: "scale(2.208, 0.458)", transformOrigin: "0 0" }} />
        </div>
        )}
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
            gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            flexShrink: 0,
          }}>
            <div style={{
                position: "relative",
                width: 20,
                height: 20,
                flexShrink: 0,
                color: "var(--neutral-foreground-disabled-rest)",
              }}>{props.icon1 ?? <Chevron2 direction={"right"} size={"20"} theme={"regular"} />}</div>
            {props.icon && (
            <div style={{ position: "relative", flexShrink: 0, color: "var(--neutral-foreground-disabled-rest)" }}>{props.iconBefore20pxRegular ?? <Placeholder size={"20"} theme={"regular"} />}</div>
            )}
          </div>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            padding: "1px 0px 1px 0px",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--spacing-vertical-xxs) * 0.5px)", paddingBottom: "calc(var(--spacing-vertical-xxs) * 0.5px)",
            flexGrow: 1,
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 600,
              fontSize: 20,
              whiteSpace: "nowrap",
              lineHeight: "28px",
              color: "var(--neutral-foreground-disabled-rest)",
              flexShrink: 0,
            }}>{props.text}</span>
          </div>
        </div>
        <div style={{
          position: "relative",
          width: 20,
          height: 20,
          display: "flex",
          flexDirection: "row",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
        }} />
      </div>
    </div>
  );
  const __body21 = () => (
    <div className={props.className} style={{
      width: 340,
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        height: 44,
        borderRadius: 4,
        display: "flex",
        flexDirection: "row",
        padding: "0px 10px 0px 10px",
        justifyContent: "space-between",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        flexShrink: 0,
        alignSelf: "stretch",
      }}>
        {props.focused && (
        <div style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 340,
            height: 44,
          }}>
          <FocusRing style={{ transform: "scale(2.208, 0.458)", transformOrigin: "0 0" }} />
        </div>
        )}
        <div style={{
          position: "relative",
          display: "flex",
          flexDirection: "row",
          gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
          alignItems: "center",
          flexWrap: "nowrap",
          flexGrow: 1,
        }}>
          {props.icon && (
          <div style={{ position: "relative", flexShrink: 0, color: "var(--neutral-foreground-disabled-rest)" }}>{props.iconBefore20pxRegular ?? <Placeholder size={"20"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            padding: "1px 0px 1px 0px",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--spacing-vertical-xxs) * 0.5px)", paddingBottom: "calc(var(--spacing-vertical-xxs) * 0.5px)",
            flexGrow: 1,
          }}>
            <span style={{
              position: "relative",
              fontFamily: "\"Segoe UI\", -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, \"Helvetica Neue\", Arial, sans-serif",
              fontWeight: 600,
              fontSize: 20,
              whiteSpace: "nowrap",
              lineHeight: "28px",
              color: "var(--neutral-foreground-disabled-rest)",
              flexShrink: 0,
            }}>{props.text}</span>
          </div>
        </div>
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
              color: "var(--neutral-foreground-disabled-rest)",
            }}>{props.icon1 ?? <Chevron2 direction={"down"} size={"20"} theme={"regular"} />}</div>
        </div>
      </div>
    </div>
  );
  const __body22 = () => (
    <div className={props.className} style={{
      width: 340,
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        height: 44,
        borderRadius: 4,
        display: "flex",
        flexDirection: "row",
        padding: "0px 10px 0px 10px",
        justifyContent: "space-between",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
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
            gap: "calc(var(--spacing-horizontal-s-2) * 1px)",
            alignItems: "flex-start",
            flexWrap: "nowrap",
            flexShrink: 0,
          }}>
            <div style={{
                position: "relative",
                width: 20,
                height: 20,
                flexShrink: 0,
                color: "var(--neutral-foreground-disabled-rest)",
              }}>{props.icon1 ?? <Chevron2 direction={"right"} size={"20"} theme={"regular"} />}</div>
            {props.icon && (
            <div style={{ position: "relative", flexShrink: 0, color: "var(--neutral-foreground-disabled-rest)" }}>{props.iconBefore20pxRegular ?? <Placeholder size={"20"} theme={"regular"} />}</div>
            )}
          </div>
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            padding: "1px 0px 1px 0px",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--spacing-vertical-xxs) * 0.5px)", paddingBottom: "calc(var(--spacing-vertical-xxs) * 0.5px)",
            flexGrow: 1,
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
            }}>{props.text}</span>
          </div>
        </div>
        <div style={{
          position: "relative",
          width: 20,
          height: 20,
          display: "flex",
          flexDirection: "row",
          alignItems: "flex-start",
          flexWrap: "nowrap",
          flexShrink: 0,
        }} />
        {props.focused && (
        <div style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 340,
            height: 44,
          }}>
          <FocusRing style={{ transform: "scale(2.208, 0.458)", transformOrigin: "0 0" }} />
        </div>
        )}
      </div>
    </div>
  );
  const __body23 = () => (
    <div className={props.className} style={{
      width: 340,
      borderRadius: 4,
      display: "flex",
      flexDirection: "column",
      alignItems: "flex-start",
      flexWrap: "nowrap",
      position: "relative",
      ...props.style,
    }}>
      <div style={{
        position: "relative",
        height: 44,
        borderRadius: 4,
        display: "flex",
        flexDirection: "row",
        padding: "0px 10px 0px 10px",
        justifyContent: "space-between",
        alignItems: "center",
        flexWrap: "nowrap",
        boxSizing: "border-box",
        paddingLeft: "calc(var(--spacing-horizontal-mnudge) * 1px)",
        paddingRight: "calc(var(--spacing-horizontal-mnudge) * 1px)",
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
          {props.icon && (
          <div style={{ position: "relative", flexShrink: 0, color: "var(--neutral-foreground-disabled-rest)" }}>{props.iconBefore20pxRegular ?? <Placeholder size={"20"} theme={"regular"} />}</div>
          )}
          <div style={{
            position: "relative",
            display: "flex",
            flexDirection: "row",
            padding: "1px 0px 1px 0px",
            alignItems: "center",
            flexWrap: "nowrap",
            boxSizing: "border-box",
            paddingTop: "calc(var(--spacing-vertical-xxs) * 0.5px)", paddingBottom: "calc(var(--spacing-vertical-xxs) * 0.5px)",
            flexGrow: 1,
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
            }}>{props.text}</span>
          </div>
        </div>
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
              color: "var(--neutral-foreground-disabled-rest)",
            }}>{props.icon1 ?? <Chevron2 direction={"down"} size={"20"} theme={"regular"} />}</div>
        </div>
        {props.focused && (
        <div style={{
            position: "absolute",
            left: 0,
            top: 0,
            width: 340,
            height: 44,
          }}>
          <FocusRing style={{ transform: "scale(2.208, 0.458)", transformOrigin: "0 0" }} />
        </div>
        )}
      </div>
    </div>
  );
  const __impls = {
    // figma: Size=Medium, State=Rest, Expanded=True, Chevron=Before
    "size=md|state=rest|expanded=true|chevron=before": __body0,
    // figma: Size=Medium, State=Rest, Expanded=False, Chevron=Before
    "size=md|state=rest|expanded=false|chevron=before": __body1,
    // figma: Size=Medium, State=Rest, Expanded=True, Chevron=After
    "size=md|state=rest|expanded=true|chevron=after": __body2,
    // figma: Size=Medium, State=Rest, Expanded=False, Chevron=After
    "size=md|state=rest|expanded=false|chevron=after": __body3,
    // figma: Size=Large, State=Rest, Expanded=True, Chevron=Before
    "size=lg|state=rest|expanded=true|chevron=before": __body4,
    // figma: Size=Large, State=Rest, Expanded=False, Chevron=Before
    "size=lg|state=rest|expanded=false|chevron=before": __body5,
    // figma: Size=Large, State=Rest, Expanded=True, Chevron=After
    "size=lg|state=rest|expanded=true|chevron=after": __body6,
    // figma: Size=Large, State=Rest, Expanded=False, Chevron=After
    "size=lg|state=rest|expanded=false|chevron=after": __body7,
    // figma: Size=Extra large, State=Rest, Expanded=True, Chevron=Before
    "size=extra large|state=rest|expanded=true|chevron=before": __body8,
    // figma: Size=Extra large, State=Rest, Expanded=False, Chevron=Before
    "size=extra large|state=rest|expanded=false|chevron=before": __body9,
    // figma: Size=Extra large, State=Rest, Expanded=True, Chevron=After
    "size=extra large|state=rest|expanded=true|chevron=after": __body10,
    // figma: Size=Extra large, State=Rest, Expanded=False, Chevron=After
    "size=extra large|state=rest|expanded=false|chevron=after": __body11,
    // figma: Size=Small, State=Rest, Expanded=True, Chevron=Before
    "size=sm|state=rest|expanded=true|chevron=before": __body12,
    // figma: Size=Small, State=Rest, Expanded=False, Chevron=Before
    "size=sm|state=rest|expanded=false|chevron=before": __body13,
    // figma: Size=Small, State=Rest, Expanded=True, Chevron=After
    "size=sm|state=rest|expanded=true|chevron=after": __body14,
    // figma: Size=Small, State=Rest, Expanded=False, Chevron=After
    "size=sm|state=rest|expanded=false|chevron=after": __body15,
    // figma: Size=Medium, State=Disabled, Expanded=False, Chevron=Before
    "size=md|state=disabled|expanded=false|chevron=before": __body16,
    // figma: Size=Medium, State=Disabled, Expanded=False, Chevron=After
    "size=md|state=disabled|expanded=false|chevron=after": __body17,
    // figma: Size=Large, State=Disabled, Expanded=False, Chevron=Before
    "size=lg|state=disabled|expanded=false|chevron=before": __body18,
    // figma: Size=Large, State=Disabled, Expanded=False, Chevron=After
    "size=lg|state=disabled|expanded=false|chevron=after": __body19,
    // figma: Size=Extra large, State=Disabled, Expanded=False, Chevron=Before
    "size=extra large|state=disabled|expanded=false|chevron=before": __body20,
    // figma: Size=Extra large, State=Disabled, Expanded=False, Chevron=After
    "size=extra large|state=disabled|expanded=false|chevron=after": __body21,
    // figma: Size=Small, State=Disabled, Expanded=False, Chevron=Before
    "size=sm|state=disabled|expanded=false|chevron=before": __body22,
    // figma: Size=Small, State=Disabled, Expanded=False, Chevron=After
    "size=sm|state=disabled|expanded=false|chevron=after": __body23,
  };
  return (__impls[__vkey(props)] ?? __body0)();
}
export default Accordion;
